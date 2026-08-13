using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Reflection;

namespace BallisticsLab.Core
{
    internal enum PhysicalTelemetryConnectionStatus
    {
        Detached = 0,
        PublisherAbsent = 1,
        UnsupportedSchema = 2,
        ContractInvalid = 3,
        SubscriptionFailed = 4,
        Attached = 5,
        EventRejected = 6
    }

    internal sealed class PhysicalTelemetryPublisherConnection : IDisposable
    {
        private readonly string _publisherTypeName;
        private readonly int _supportedSchema;
        private MethodInfo? _unsubscribeMethod;
        private Action<object>? _observer;
        private Action<PhysicalTelemetryEventRecord>? _sink;
        private Action<string>? _diagnostic;
        private int _publisherSchema;
        private bool _disposed;

        internal PhysicalTelemetryPublisherConnection()
            : this(
                PhysicalTelemetryContract.PublisherTypeName,
                PhysicalTelemetryContract.SupportedPublisherSchema)
        {
        }

        internal PhysicalTelemetryPublisherConnection(string publisherTypeName, int supportedSchema)
        {
            if (string.IsNullOrWhiteSpace(publisherTypeName))
            {
                throw new ArgumentException("Publisher type name is required.", nameof(publisherTypeName));
            }
            if (supportedSchema <= 0)
            {
                ThrowNonPositiveSchema(nameof(supportedSchema));
            }
            _publisherTypeName = publisherTypeName;
            _supportedSchema = supportedSchema;
            Status = PhysicalTelemetryConnectionStatus.Detached;
            LastFailure = string.Empty;
        }

        internal bool IsAttached => Status == PhysicalTelemetryConnectionStatus.Attached
            || Status == PhysicalTelemetryConnectionStatus.EventRejected;

        internal PhysicalTelemetryConnectionStatus Status { get; private set; }

        internal string LastFailure { get; private set; }

        internal int PublisherSchema => _publisherSchema;

        internal bool TryAttach(
            IEnumerable<Assembly> assemblies,
            Action<PhysicalTelemetryEventRecord> sink,
            Action<string>? diagnostic = null)
        {
            if (_disposed)
            {
                ThrowDisposed();
            }
            if (assemblies == null)
            {
                ThrowNullArgument(nameof(assemblies));
            }
            if (sink == null)
            {
                ThrowNullArgument(nameof(sink));
            }
            if (IsAttached)
            {
                return true;
            }

            Type? publisherType = FindPublisherType(assemblies);
            if (publisherType == null)
            {
                SetStatus(
                    PhysicalTelemetryConnectionStatus.PublisherAbsent,
                    "Physical telemetry publisher is not loaded.");
                return false;
            }

            if (!TryReadSchema(publisherType, out int schema))
            {
                SetStatus(
                    PhysicalTelemetryConnectionStatus.ContractInvalid,
                    "Physical telemetry publisher did not expose an integer SchemaVersion.");
                return false;
            }
            _publisherSchema = schema;
            if (schema != _supportedSchema)
            {
                SetStatus(
                    PhysicalTelemetryConnectionStatus.UnsupportedSchema,
                    "Physical telemetry publisher schema "
                        + schema.ToString(CultureInfo.InvariantCulture)
                        + " is unsupported; expected "
                        + _supportedSchema.ToString(CultureInfo.InvariantCulture) + ".");
                return false;
            }

            Type observerType = typeof(Action<object>);
            MethodInfo? subscribe = publisherType.GetMethod(
                "Subscribe",
                BindingFlags.Public | BindingFlags.Static,
                null,
                new[] { observerType },
                null);
            MethodInfo? unsubscribe = publisherType.GetMethod(
                "Unsubscribe",
                BindingFlags.Public | BindingFlags.Static,
                null,
                new[] { observerType },
                null);
            if (subscribe == null
                || unsubscribe == null
                || !IsVoidStaticMethod(subscribe)
                || !IsVoidStaticMethod(unsubscribe))
            {
                SetStatus(
                    PhysicalTelemetryConnectionStatus.ContractInvalid,
                    "Physical telemetry publisher Subscribe/Unsubscribe contract was missing or invalid.");
                return false;
            }

            _sink = sink;
            _diagnostic = diagnostic;
            _observer = Observe;
            _unsubscribeMethod = unsubscribe;
            try
            {
                subscribe.Invoke(null, new object[] { _observer });
            }
            catch (TargetInvocationException exception)
            {
                ClearConnectionState();
                SetStatus(
                    PhysicalTelemetryConnectionStatus.SubscriptionFailed,
                    "Physical telemetry subscription failed: "
                        + (exception.InnerException?.Message ?? exception.Message));
                return false;
            }
            catch (TargetException exception)
            {
                ClearConnectionState();
                SetStatus(
                    PhysicalTelemetryConnectionStatus.SubscriptionFailed,
                    "Physical telemetry subscription target failed: " + exception.Message);
                return false;
            }
            catch (MethodAccessException exception)
            {
                ClearConnectionState();
                SetStatus(
                    PhysicalTelemetryConnectionStatus.SubscriptionFailed,
                    "Physical telemetry subscription access failed: " + exception.Message);
                return false;
            }

            SetStatus(PhysicalTelemetryConnectionStatus.Attached, string.Empty);
            return true;
        }

        internal bool TryDetach(out string failure)
        {
            failure = string.Empty;
            MethodInfo? unsubscribe = _unsubscribeMethod;
            Action<object>? observer = _observer;
            ClearConnectionState();
            Status = PhysicalTelemetryConnectionStatus.Detached;
            LastFailure = string.Empty;
            if (unsubscribe == null || observer == null)
            {
                return true;
            }

            try
            {
                unsubscribe.Invoke(null, new object[] { observer });
                return true;
            }
            catch (TargetInvocationException exception)
            {
                failure = "Physical telemetry unsubscription failed: "
                    + (exception.InnerException?.Message ?? exception.Message);
                return false;
            }
            catch (TargetException exception)
            {
                failure = "Physical telemetry unsubscription target failed: " + exception.Message;
                return false;
            }
            catch (MethodAccessException exception)
            {
                failure = "Physical telemetry unsubscription access failed: " + exception.Message;
                return false;
            }
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }
            TryDetach(out _);
            _disposed = true;
        }

        private static bool IsVoidStaticMethod(MethodInfo? method)
        {
            return method != null && method.IsStatic && method.ReturnType == typeof(void);
        }

        [DoesNotReturn]
        private static void ThrowNonPositiveSchema(string parameterName)
        {
            throw new ArgumentOutOfRangeException(
                parameterName,
                "Supported schema must be positive.");
        }

        [DoesNotReturn]
        private static void ThrowDisposed()
        {
            throw new ObjectDisposedException(nameof(PhysicalTelemetryPublisherConnection));
        }

        [DoesNotReturn]
        private static void ThrowNullArgument(string parameterName)
        {
            throw new ArgumentNullException(parameterName);
        }

        private Type? FindPublisherType(IEnumerable<Assembly> assemblies)
        {
            foreach (Assembly assembly in assemblies)
            {
                if (assembly == null)
                {
                    continue;
                }
                Type? type = assembly.GetType(_publisherTypeName, throwOnError: false, ignoreCase: false);
                if (type != null)
                {
                    return type;
                }
            }
            return null;
        }

        private static bool TryReadSchema(Type publisherType, out int schema)
        {
            FieldInfo? field = publisherType.GetField(
                "SchemaVersion",
                BindingFlags.Public | BindingFlags.Static);
            object? value = field?.GetValue(null);
            if (value is int fieldSchema)
            {
                schema = fieldSchema;
                return true;
            }

            PropertyInfo? property = publisherType.GetProperty(
                "SchemaVersion",
                BindingFlags.Public | BindingFlags.Static);
            value = property?.GetValue(null, null);
            if (value is int propertySchema)
            {
                schema = propertySchema;
                return true;
            }

            schema = 0;
            return false;
        }

        private void Observe(object source)
        {
            Action<PhysicalTelemetryEventRecord>? sink = _sink;
            if (sink == null)
            {
                return;
            }

            if (!PhysicalTelemetryReflectionReader.TryCopy(
                    _publisherSchema,
                    source,
                    out PhysicalTelemetryEventRecord? record,
                    out string failure)
                || record == null)
            {
                SetStatus(PhysicalTelemetryConnectionStatus.EventRejected, failure);
                _diagnostic?.Invoke(failure);
                return;
            }

            sink(record);
            if (Status == PhysicalTelemetryConnectionStatus.EventRejected)
            {
                SetStatus(PhysicalTelemetryConnectionStatus.Attached, string.Empty);
            }
        }

        private void SetStatus(PhysicalTelemetryConnectionStatus status, string failure)
        {
            Status = status;
            LastFailure = failure;
        }

        private void ClearConnectionState()
        {
            _unsubscribeMethod = null;
            _observer = null;
            _sink = null;
            _diagnostic = null;
        }
    }
}

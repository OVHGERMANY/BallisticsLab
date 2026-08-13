using System;
using System.Collections.Generic;
using BallisticsLab.Core;

namespace BallisticsLab.Runtime.Telemetry
{
    internal static class PhysicalTelemetrySessionBridge
    {
        private static readonly PhysicalTelemetryCaptureBuffer Captured =
            new PhysicalTelemetryCaptureBuffer(LabPolicies.MaximumRecords * 2);

        private static PhysicalTelemetryPublisherConnection? _connection;
        private static DateTime _nextDiscoveryUtc;
        private static PhysicalTelemetryConnectionStatus _reportedStatus;
        private static bool _active;

        internal static int CapturedCount => Captured.Count;

        internal static void Start()
        {
            Stop();
            Captured.Clear();
            _active = true;
            _connection = new PhysicalTelemetryPublisherConnection();
            _nextDiscoveryUtc = DateTime.MinValue;
            _reportedStatus = PhysicalTelemetryConnectionStatus.Detached;
            Update();
        }

        internal static void Update()
        {
            PhysicalTelemetryPublisherConnection? connection = _connection;
            DateTime now = DateTime.UtcNow;
            if (!_active || connection == null || connection.IsAttached || now < _nextDiscoveryUtc)
            {
                return;
            }

            _nextDiscoveryUtc = now.AddSeconds(1d);
            bool attached = connection.TryAttach(
                AppDomain.CurrentDomain.GetAssemblies(),
                Captured.Add,
                ReportRejectedEvent);
            ReportConnectionStatus(connection);
            if (attached)
            {
                Plugin.Log?.LogInfo(
                    "Physical telemetry schema " + connection.PublisherSchema
                    + " attached for the active lab session.");
            }
        }

        internal static IReadOnlyList<PhysicalTelemetryEventRecord> Snapshot()
        {
            return Captured.Snapshot();
        }

        internal static void ClearCaptured()
        {
            Captured.Clear();
        }

        internal static void Stop()
        {
            _active = false;
            PhysicalTelemetryPublisherConnection? connection = _connection;
            _connection = null;
            if (connection == null)
            {
                return;
            }

            if (!connection.TryDetach(out string failure))
            {
                Plugin.Log?.LogWarning(failure);
            }
            connection.Dispose();
            _reportedStatus = PhysicalTelemetryConnectionStatus.Detached;
        }

        private static void ReportConnectionStatus(PhysicalTelemetryPublisherConnection connection)
        {
            if (connection.Status == _reportedStatus)
            {
                return;
            }
            _reportedStatus = connection.Status;
            if (connection.Status == PhysicalTelemetryConnectionStatus.PublisherAbsent)
            {
                Plugin.Log?.LogInfo(
                    "No compatible physical telemetry publisher is loaded; the lab remains fully usable.");
            }
            else if (connection.Status == PhysicalTelemetryConnectionStatus.UnsupportedSchema
                || connection.Status == PhysicalTelemetryConnectionStatus.ContractInvalid
                || connection.Status == PhysicalTelemetryConnectionStatus.SubscriptionFailed)
            {
                Plugin.Log?.LogWarning(connection.LastFailure);
            }
        }

        private static void ReportRejectedEvent(string failure)
        {
            Plugin.Log?.LogWarning("Physical telemetry event rejected: " + failure);
        }
    }
}

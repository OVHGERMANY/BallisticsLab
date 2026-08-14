using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.IO;
using System.Reflection;

namespace BallisticsLab.Core
{
    internal static class PhysicalTelemetryReflectionReader
    {
        internal static bool TryCopy(
            int publisherSchema,
            object? source,
            out PhysicalTelemetryEventRecord? record,
            out string failure)
        {
            record = null;
            failure = string.Empty;
            if (publisherSchema != PhysicalTelemetryContract.SupportedPublisherSchema)
            {
                failure = "Unsupported physical telemetry publisher schema "
                    + publisherSchema.ToString(CultureInfo.InvariantCulture) + ".";
                return false;
            }
            if (source == null)
            {
                failure = "Physical telemetry event was null.";
                return false;
            }

            try
            {
                PhysicalTelemetryStageRecord stage = ReadStage(source);
                string transitionId = RequiredString(source, "TransitionId");
                string outcome = EnumName(source, "Outcome");
                object hostSource = RequiredObject(source, "Host");
                object impactSource = RequiredObject(source, "Impact");
                object parentSource = RequiredObject(source, "Parent");
                ReadOnlyCollection<PhysicalComponentRecord> outputs = ReadComponents(
                    RequiredObject(source, "Outputs"),
                    "Outputs");
                object? conservationSource = OptionalObject(source, "Conservation");
                PhysicalConservationRecord? conservation = conservationSource == null
                    ? null
                    : ReadConservation(conservationSource);

                if (stage == PhysicalTelemetryStageRecord.CollisionPrepared
                    && (outputs.Count != 0 || conservation != null))
                {
                    throw new InvalidDataException(
                        "A prepared physical transition contained resolved output data.");
                }
                if (stage == PhysicalTelemetryStageRecord.CollisionResolved
                    && (outputs.Count == 0 || conservation == null))
                {
                    throw new InvalidDataException(
                        "A resolved physical transition omitted output or conservation data.");
                }

                record = new PhysicalTelemetryEventRecord(
                    PhysicalTelemetryContract.SnapshotSchema,
                    publisherSchema,
                    stage,
                    transitionId,
                    outcome,
                    ReadHost(hostSource),
                    ReadImpact(impactSource),
                    ReadComponent(parentSource),
                    outputs,
                    conservation);
                return true;
            }
            catch (InvalidDataException exception)
            {
                failure = exception.Message;
                return false;
            }
            catch (TargetInvocationException exception)
            {
                failure = "A physical telemetry property getter failed: "
                    + (exception.InnerException?.Message ?? exception.Message);
                return false;
            }
            catch (TargetException exception)
            {
                failure = "Physical telemetry reflection target failed: " + exception.Message;
                return false;
            }
            catch (MethodAccessException exception)
            {
                failure = "Physical telemetry property access failed: " + exception.Message;
                return false;
            }
            catch (AmbiguousMatchException exception)
            {
                failure = "Physical telemetry contract was ambiguous: " + exception.Message;
                return false;
            }
            catch (InvalidCastException exception)
            {
                failure = "Physical telemetry value type was invalid: " + exception.Message;
                return false;
            }
            catch (OverflowException exception)
            {
                failure = "Physical telemetry numeric value overflowed: " + exception.Message;
                return false;
            }
            catch (FormatException exception)
            {
                failure = "Physical telemetry numeric value was malformed: " + exception.Message;
                return false;
            }
            catch (ArgumentException exception)
            {
                failure = "Physical telemetry reflection contract was invalid: " + exception.Message;
                return false;
            }
        }

        private static PhysicalTelemetryStageRecord ReadStage(object source)
        {
            string stage = EnumName(source, "Stage");
            if (string.Equals(stage, "CollisionPrepared", StringComparison.Ordinal))
            {
                return PhysicalTelemetryStageRecord.CollisionPrepared;
            }
            if (string.Equals(stage, "CollisionResolved", StringComparison.Ordinal))
            {
                return PhysicalTelemetryStageRecord.CollisionResolved;
            }
            throw new InvalidDataException("Physical telemetry stage was unsupported: " + stage + ".");
        }

        private static PhysicalTelemetryHostRecord ReadHost(object source)
        {
            return new PhysicalTelemetryHostRecord(
                Int32(source, "RootFireIndex"),
                Int32(source, "RootRandomSeed"),
                Int32(source, "CurrentFireIndex"),
                Int32(source, "CurrentRandomSeed"),
                Int32(source, "CurrentFragmentIndex"),
                Int32(source, "ParentDepth"),
                OptionalString(source, "RootShooterProfileId"),
                OptionalString(source, "AmmunitionTemplateId"),
                OptionalString(source, "AmmunitionTemplateName"));
        }

        private static PhysicalTelemetryImpactRecord ReadImpact(object source)
        {
            return new PhysicalTelemetryImpactRecord(
                ReadVector(RequiredObject(source, "PositionMetres")),
                ReadVector(RequiredObject(source, "SurfaceNormal")),
                FiniteDouble(source, "PhysicalThicknessMetres"),
                FiniteDouble(source, "EffectivePathLengthMetres"),
                OptionalString(source, "TargetProfileId"),
                EnumName(source, "TargetMaterialClass"),
                OptionalStringIfPresent(source, "TargetSurfaceIdentity"),
                FiniteDouble(source, "TargetDensityKilogramsPerCubicMetre"),
                FiniteDouble(source, "TargetResistancePressurePascals"),
                FiniteDouble(source, "ProjectileDeformationCoupling"),
                FiniteDouble(source, "ProjectileFractureCoupling"),
                FiniteDouble(source, "HeatLossFraction"));
        }

        private static PhysicalConservationRecord ReadConservation(object source)
        {
            object lossBudget = RequiredObject(source, "LossBudget");
            return new PhysicalConservationRecord(
                FiniteDouble(source, "ParentMassKilograms"),
                FiniteDouble(source, "AllocatedParentMassKilograms"),
                FiniteDouble(source, "UnallocatedParentMassKilograms"),
                FiniteDouble(source, "TargetSpallMassKilograms"),
                FiniteDouble(source, "ParentEnergyJoules"),
                ReadLossBudget(lossBudget),
                FiniteDouble(source, "ModeledLossEnergyJoules"),
                FiniteDouble(source, "ResidualEnergyJoules"),
                FiniteDouble(source, "OutputEnergyJoules"),
                FiniteDouble(source, "EnergyClosureErrorJoules"),
                Int32(source, "ParentDerivedOutputCount"),
                Int32(source, "TargetSpallOutputCount"));
        }

        private static PhysicalLossBudgetRecord ReadLossBudget(object source)
        {
            return new PhysicalLossBudgetRecord(
                FiniteDouble(source, "PenetrationLossJoules"),
                FiniteDouble(source, "DeformationLossJoules"),
                FiniteDouble(source, "FractureLossJoules"),
                FiniteDouble(source, "HeatLossJoules"),
                FiniteDouble(source, "OtherLossJoules"),
                FiniteDouble(source, "TotalLossJoules"));
        }

        private static ReadOnlyCollection<PhysicalComponentRecord> ReadComponents(
            object source,
            string propertyName)
        {
            if (!(source is IEnumerable enumerable))
            {
                throw new InvalidDataException(
                    "Physical telemetry " + propertyName + " was not enumerable.");
            }

            var records = new List<PhysicalComponentRecord>();
            foreach (object? item in enumerable)
            {
                if (item == null)
                {
                    throw new InvalidDataException(
                        "Physical telemetry " + propertyName + " contained a null item.");
                }
                records.Add(ReadComponent(item));
            }
            return Array.AsReadOnly(records.ToArray());
        }

        private static PhysicalComponentRecord ReadComponent(object source)
        {
            IReadOnlyList<PhysicalCollisionRecordSnapshot> history = ReadCollisionHistory(
                RequiredObject(source, "CollisionHistory"));
            return new PhysicalComponentRecord(
                EnumName(source, "Kind"),
                RequiredString(source, "ProjectileId"),
                RequiredString(source, "RootShotId"),
                OptionalString(source, "ParentProjectileId"),
                OptionalString(source, "SourceProjectileId"),
                OptionalString(source, "SourceMaterialId"),
                EnumName(source, "SourceMaterialClass"),
                OptionalString(source, "SourceCollisionId"),
                Int32(source, "FragmentIndex"),
                Int32(source, "FragmentGeneration"),
                UInt64(source, "DeterministicSeed"),
                EnumName(source, "Construction"),
                EnumName(source, "ShapeClass"),
                FiniteDouble(source, "OriginalMassKilograms"),
                FiniteDouble(source, "RetainedMassKilograms"),
                FiniteDouble(source, "NominalDiameterMetres"),
                FiniteDouble(source, "DeformedDiameterMetres"),
                FiniteDouble(source, "ProjectedAreaSquareMetres"),
                FiniteDouble(source, "EquivalentDiameterMetres"),
                FiniteDouble(source, "LengthMetres"),
                FiniteDouble(source, "AspectRatio"),
                FiniteDouble(source, "DragCoefficient"),
                FiniteDouble(source, "BallisticCoefficientKilogramsPerSquareMetre"),
                ReadVector(RequiredObject(source, "PositionMetres")),
                ReadVector(RequiredObject(source, "VelocityMetresPerSecond")),
                FiniteDouble(source, "SpeedMetresPerSecond"),
                ReadVector(RequiredObject(source, "MomentumKilogramMetresPerSecond")),
                FiniteDouble(source, "TranslationalKineticEnergyJoules"),
                ReadOrientation(RequiredObject(source, "Orientation")),
                FiniteDouble(source, "YawAngleRadians"),
                EnumName(source, "TumbleState"),
                FiniteDouble(source, "PenetrationCapabilityJoulesPerSquareMetre"),
                FiniteDouble(source, "DamageCapabilityJoules"),
                EnumName(source, "TerminalState"),
                EnumName(source, "RenderState"),
                Boolean(source, "IsTargetMaterialOrigin"),
                Boolean(source, "IsParentDerivedMass"),
                history);
        }

        private static ReadOnlyCollection<PhysicalCollisionRecordSnapshot> ReadCollisionHistory(object source)
        {
            if (!(source is IEnumerable enumerable))
            {
                throw new InvalidDataException("Physical collision history was not enumerable.");
            }

            var records = new List<PhysicalCollisionRecordSnapshot>();
            foreach (object? item in enumerable)
            {
                if (item == null)
                {
                    throw new InvalidDataException("Physical collision history contained a null item.");
                }
                records.Add(ReadCollision(item));
            }
            return Array.AsReadOnly(records.ToArray());
        }

        private static PhysicalCollisionRecordSnapshot ReadCollision(object source)
        {
            return new PhysicalCollisionRecordSnapshot(
                RequiredString(source, "CollisionId"),
                RequiredString(source, "MaterialId"),
                EnumName(source, "MaterialClass"),
                Int32(source, "Sequence"),
                ReadVector(RequiredObject(source, "PositionMetres")),
                ReadVector(RequiredObject(source, "IncomingVelocityMetresPerSecond")),
                ReadVector(RequiredObject(source, "OutgoingVelocityMetresPerSecond")),
                FiniteDouble(source, "IncomingTranslationalEnergyJoules"),
                FiniteDouble(source, "OutgoingTranslationalEnergyJoules"),
                FiniteDouble(source, "ImpactAngleRadians"),
                FiniteDouble(source, "EffectivePathLengthMetres"),
                EnumName(source, "Outcome"));
        }

        private static PhysicalVectorRecord ReadVector(object source)
        {
            return new PhysicalVectorRecord(
                FiniteDouble(source, "X"),
                FiniteDouble(source, "Y"),
                FiniteDouble(source, "Z"));
        }

        private static PhysicalOrientationRecord ReadOrientation(object source)
        {
            return new PhysicalOrientationRecord(
                FiniteDouble(source, "X"),
                FiniteDouble(source, "Y"),
                FiniteDouble(source, "Z"),
                FiniteDouble(source, "W"));
        }

        private static object RequiredObject(object source, string propertyName)
        {
            return OptionalObject(source, propertyName)
                ?? throw new InvalidDataException(
                    "Physical telemetry property " + propertyName + " was null.");
        }

        private static object? OptionalObject(object source, string propertyName)
        {
            PropertyInfo property = source.GetType().GetProperty(
                    propertyName,
                    BindingFlags.Instance | BindingFlags.Public)
                ?? throw new InvalidDataException(
                    "Physical telemetry property " + propertyName + " was missing.");
            if (!property.CanRead || property.GetIndexParameters().Length != 0)
            {
                throw new InvalidDataException(
                    "Physical telemetry property " + propertyName + " was not readable.");
            }
            return property.GetValue(source, null);
        }

        private static string RequiredString(object source, string propertyName)
        {
            string value = OptionalString(source, propertyName);
            if (string.IsNullOrWhiteSpace(value))
            {
                throw new InvalidDataException(
                    "Physical telemetry property " + propertyName + " was empty.");
            }
            return value;
        }

        private static string OptionalString(object source, string propertyName)
        {
            object? value = OptionalObject(source, propertyName);
            string text = value == null
                ? string.Empty
                : Convert.ToString(value, CultureInfo.InvariantCulture) ?? string.Empty;
            return DetachedString(text);
        }

        private static string OptionalStringIfPresent(object source, string propertyName)
        {
            PropertyInfo? property = source.GetType().GetProperty(
                propertyName,
                BindingFlags.Instance | BindingFlags.Public);
            if (property == null)
            {
                return string.Empty;
            }
            if (!property.CanRead || property.GetIndexParameters().Length != 0)
            {
                throw new InvalidDataException(
                    "Physical telemetry property " + propertyName + " was not readable.");
            }
            object? value = property.GetValue(source, null);
            string text = value == null
                ? string.Empty
                : Convert.ToString(value, CultureInfo.InvariantCulture) ?? string.Empty;
            return DetachedString(text);
        }

        private static string EnumName(object source, string propertyName)
        {
            object value = RequiredObject(source, propertyName);
            string name = Convert.ToString(value, CultureInfo.InvariantCulture) ?? string.Empty;
            if (string.IsNullOrWhiteSpace(name))
            {
                throw new InvalidDataException(
                    "Physical telemetry enum " + propertyName + " was empty.");
            }
            return DetachedString(name);
        }

        private static int Int32(object source, string propertyName)
        {
            return Convert.ToInt32(RequiredObject(source, propertyName), CultureInfo.InvariantCulture);
        }

        private static ulong UInt64(object source, string propertyName)
        {
            return Convert.ToUInt64(RequiredObject(source, propertyName), CultureInfo.InvariantCulture);
        }

        private static bool Boolean(object source, string propertyName)
        {
            return Convert.ToBoolean(RequiredObject(source, propertyName), CultureInfo.InvariantCulture);
        }

        private static double FiniteDouble(object source, string propertyName)
        {
            double value = Convert.ToDouble(RequiredObject(source, propertyName), CultureInfo.InvariantCulture);
            if (double.IsNaN(value) || double.IsInfinity(value))
            {
                throw new InvalidDataException(
                    "Physical telemetry property " + propertyName + " was not finite.");
            }
            return value;
        }

        private static string DetachedString(string value)
        {
            return value.Length == 0 ? string.Empty : new string(value.ToCharArray());
        }
    }
}

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics.CodeAnalysis;

namespace BallisticsLab.Core
{
    internal static class PhysicalTelemetryContract
    {
        internal const int SupportedPublisherSchema = 1;
        internal const int SnapshotSchema = 1;
        internal const string PublisherTypeName =
            "BallisticPenetration.Core.Physics.PhysicalProjectileTelemetry";
    }

    internal enum PhysicalTelemetryStageRecord
    {
        CollisionPrepared = 0,
        CollisionResolved = 1
    }

    internal readonly struct PhysicalVectorRecord : IEquatable<PhysicalVectorRecord>
    {
        internal PhysicalVectorRecord(double x, double y, double z)
        {
            X = x;
            Y = y;
            Z = z;
        }

        internal double X { get; }
        internal double Y { get; }
        internal double Z { get; }

        public bool Equals(PhysicalVectorRecord other)
        {
            return X.Equals(other.X) && Y.Equals(other.Y) && Z.Equals(other.Z);
        }

        public override bool Equals(object? obj)
        {
            return obj is PhysicalVectorRecord other && Equals(other);
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(X, Y, Z);
        }
    }

    internal readonly struct PhysicalOrientationRecord : IEquatable<PhysicalOrientationRecord>
    {
        internal PhysicalOrientationRecord(double x, double y, double z, double w)
        {
            X = x;
            Y = y;
            Z = z;
            W = w;
        }

        internal double X { get; }
        internal double Y { get; }
        internal double Z { get; }
        internal double W { get; }

        public bool Equals(PhysicalOrientationRecord other)
        {
            return X.Equals(other.X)
                && Y.Equals(other.Y)
                && Z.Equals(other.Z)
                && W.Equals(other.W);
        }

        public override bool Equals(object? obj)
        {
            return obj is PhysicalOrientationRecord other && Equals(other);
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(X, Y, Z, W);
        }
    }

    internal sealed class PhysicalTelemetryHostRecord
    {
        internal PhysicalTelemetryHostRecord(
            int rootFireIndex,
            int rootRandomSeed,
            int currentFireIndex,
            int currentRandomSeed,
            int currentFragmentIndex,
            int parentDepth,
            string rootShooterProfileId,
            string ammunitionTemplateId,
            string ammunitionTemplateName)
        {
            RootFireIndex = rootFireIndex;
            RootRandomSeed = rootRandomSeed;
            CurrentFireIndex = currentFireIndex;
            CurrentRandomSeed = currentRandomSeed;
            CurrentFragmentIndex = currentFragmentIndex;
            ParentDepth = parentDepth;
            RootShooterProfileId = rootShooterProfileId;
            AmmunitionTemplateId = ammunitionTemplateId;
            AmmunitionTemplateName = ammunitionTemplateName;
        }

        internal int RootFireIndex { get; }
        internal int RootRandomSeed { get; }
        internal int CurrentFireIndex { get; }
        internal int CurrentRandomSeed { get; }
        internal int CurrentFragmentIndex { get; }
        internal int ParentDepth { get; }
        internal string RootShooterProfileId { get; }
        internal string AmmunitionTemplateId { get; }
        internal string AmmunitionTemplateName { get; }
    }

    internal sealed class PhysicalTelemetryImpactRecord
    {
        internal PhysicalTelemetryImpactRecord(
            PhysicalVectorRecord positionMetres,
            PhysicalVectorRecord surfaceNormal,
            double physicalThicknessMetres,
            double effectivePathLengthMetres,
            string targetProfileId,
            string targetMaterialClass,
            string targetSurfaceIdentity,
            double targetDensityKilogramsPerCubicMetre,
            double targetResistancePressurePascals,
            double projectileDeformationCoupling,
            double projectileFractureCoupling,
            double heatLossFraction)
        {
            PositionMetres = positionMetres;
            SurfaceNormal = surfaceNormal;
            PhysicalThicknessMetres = physicalThicknessMetres;
            EffectivePathLengthMetres = effectivePathLengthMetres;
            TargetProfileId = targetProfileId;
            TargetMaterialClass = targetMaterialClass;
            TargetSurfaceIdentity = targetSurfaceIdentity;
            TargetDensityKilogramsPerCubicMetre = targetDensityKilogramsPerCubicMetre;
            TargetResistancePressurePascals = targetResistancePressurePascals;
            ProjectileDeformationCoupling = projectileDeformationCoupling;
            ProjectileFractureCoupling = projectileFractureCoupling;
            HeatLossFraction = heatLossFraction;
        }

        internal PhysicalVectorRecord PositionMetres { get; }
        internal PhysicalVectorRecord SurfaceNormal { get; }
        internal double PhysicalThicknessMetres { get; }
        internal double EffectivePathLengthMetres { get; }
        internal string TargetProfileId { get; }
        internal string TargetMaterialClass { get; }
        internal string TargetSurfaceIdentity { get; }
        internal double TargetDensityKilogramsPerCubicMetre { get; }
        internal double TargetResistancePressurePascals { get; }
        internal double ProjectileDeformationCoupling { get; }
        internal double ProjectileFractureCoupling { get; }
        internal double HeatLossFraction { get; }
    }

    internal sealed class PhysicalLossBudgetRecord
    {
        internal PhysicalLossBudgetRecord(
            double penetrationLossJoules,
            double deformationLossJoules,
            double fractureLossJoules,
            double heatLossJoules,
            double otherLossJoules,
            double totalLossJoules)
        {
            PenetrationLossJoules = penetrationLossJoules;
            DeformationLossJoules = deformationLossJoules;
            FractureLossJoules = fractureLossJoules;
            HeatLossJoules = heatLossJoules;
            OtherLossJoules = otherLossJoules;
            TotalLossJoules = totalLossJoules;
        }

        internal double PenetrationLossJoules { get; }
        internal double DeformationLossJoules { get; }
        internal double FractureLossJoules { get; }
        internal double HeatLossJoules { get; }
        internal double OtherLossJoules { get; }
        internal double TotalLossJoules { get; }
    }

    internal sealed class PhysicalConservationRecord
    {
        internal PhysicalConservationRecord(
            double parentMassKilograms,
            double allocatedParentMassKilograms,
            double unallocatedParentMassKilograms,
            double targetSpallMassKilograms,
            double parentEnergyJoules,
            PhysicalLossBudgetRecord lossBudget,
            double modeledLossEnergyJoules,
            double residualEnergyJoules,
            double outputEnergyJoules,
            double energyClosureErrorJoules,
            int parentDerivedOutputCount,
            int targetSpallOutputCount)
        {
            ParentMassKilograms = parentMassKilograms;
            AllocatedParentMassKilograms = allocatedParentMassKilograms;
            UnallocatedParentMassKilograms = unallocatedParentMassKilograms;
            TargetSpallMassKilograms = targetSpallMassKilograms;
            ParentEnergyJoules = parentEnergyJoules;
            LossBudget = lossBudget;
            ModeledLossEnergyJoules = modeledLossEnergyJoules;
            ResidualEnergyJoules = residualEnergyJoules;
            OutputEnergyJoules = outputEnergyJoules;
            EnergyClosureErrorJoules = energyClosureErrorJoules;
            ParentDerivedOutputCount = parentDerivedOutputCount;
            TargetSpallOutputCount = targetSpallOutputCount;
        }

        internal double ParentMassKilograms { get; }
        internal double AllocatedParentMassKilograms { get; }
        internal double UnallocatedParentMassKilograms { get; }
        internal double TargetSpallMassKilograms { get; }
        internal double ParentEnergyJoules { get; }
        internal PhysicalLossBudgetRecord LossBudget { get; }
        internal double ModeledLossEnergyJoules { get; }
        internal double ResidualEnergyJoules { get; }
        internal double OutputEnergyJoules { get; }
        internal double EnergyClosureErrorJoules { get; }
        internal int ParentDerivedOutputCount { get; }
        internal int TargetSpallOutputCount { get; }
    }

    internal sealed class PhysicalCollisionRecordSnapshot
    {
        internal PhysicalCollisionRecordSnapshot(
            string collisionId,
            string materialId,
            string materialClass,
            int sequence,
            PhysicalVectorRecord positionMetres,
            PhysicalVectorRecord incomingVelocityMetresPerSecond,
            PhysicalVectorRecord outgoingVelocityMetresPerSecond,
            double incomingTranslationalEnergyJoules,
            double outgoingTranslationalEnergyJoules,
            double impactAngleRadians,
            double effectivePathLengthMetres,
            string outcome)
        {
            CollisionId = collisionId;
            MaterialId = materialId;
            MaterialClass = materialClass;
            Sequence = sequence;
            PositionMetres = positionMetres;
            IncomingVelocityMetresPerSecond = incomingVelocityMetresPerSecond;
            OutgoingVelocityMetresPerSecond = outgoingVelocityMetresPerSecond;
            IncomingTranslationalEnergyJoules = incomingTranslationalEnergyJoules;
            OutgoingTranslationalEnergyJoules = outgoingTranslationalEnergyJoules;
            ImpactAngleRadians = impactAngleRadians;
            EffectivePathLengthMetres = effectivePathLengthMetres;
            Outcome = outcome;
        }

        internal string CollisionId { get; }
        internal string MaterialId { get; }
        internal string MaterialClass { get; }
        internal int Sequence { get; }
        internal PhysicalVectorRecord PositionMetres { get; }
        internal PhysicalVectorRecord IncomingVelocityMetresPerSecond { get; }
        internal PhysicalVectorRecord OutgoingVelocityMetresPerSecond { get; }
        internal double IncomingTranslationalEnergyJoules { get; }
        internal double OutgoingTranslationalEnergyJoules { get; }
        internal double ImpactAngleRadians { get; }
        internal double EffectivePathLengthMetres { get; }
        internal string Outcome { get; }
    }

    internal sealed class PhysicalComponentRecord
    {
        private readonly ReadOnlyCollection<PhysicalCollisionRecordSnapshot> _collisionHistory;

        internal PhysicalComponentRecord(
            string kind,
            string projectileId,
            string rootShotId,
            string parentProjectileId,
            string sourceProjectileId,
            string sourceMaterialId,
            string sourceMaterialClass,
            string sourceCollisionId,
            int fragmentIndex,
            int fragmentGeneration,
            ulong deterministicSeed,
            string construction,
            string shapeClass,
            double originalMassKilograms,
            double retainedMassKilograms,
            double nominalDiameterMetres,
            double deformedDiameterMetres,
            double projectedAreaSquareMetres,
            double equivalentDiameterMetres,
            double lengthMetres,
            double aspectRatio,
            double dragCoefficient,
            double ballisticCoefficientKilogramsPerSquareMetre,
            PhysicalVectorRecord positionMetres,
            PhysicalVectorRecord velocityMetresPerSecond,
            double speedMetresPerSecond,
            PhysicalVectorRecord momentumKilogramMetresPerSecond,
            double translationalKineticEnergyJoules,
            PhysicalOrientationRecord orientation,
            double yawAngleRadians,
            string tumbleState,
            double penetrationCapabilityJoulesPerSquareMetre,
            double damageCapabilityJoules,
            string terminalState,
            string renderState,
            bool isTargetMaterialOrigin,
            bool isParentDerivedMass,
            IReadOnlyList<PhysicalCollisionRecordSnapshot> collisionHistory)
        {
            Kind = kind;
            ProjectileId = projectileId;
            RootShotId = rootShotId;
            ParentProjectileId = parentProjectileId;
            SourceProjectileId = sourceProjectileId;
            SourceMaterialId = sourceMaterialId;
            SourceMaterialClass = sourceMaterialClass;
            SourceCollisionId = sourceCollisionId;
            FragmentIndex = fragmentIndex;
            FragmentGeneration = fragmentGeneration;
            DeterministicSeed = deterministicSeed;
            Construction = construction;
            ShapeClass = shapeClass;
            OriginalMassKilograms = originalMassKilograms;
            RetainedMassKilograms = retainedMassKilograms;
            NominalDiameterMetres = nominalDiameterMetres;
            DeformedDiameterMetres = deformedDiameterMetres;
            ProjectedAreaSquareMetres = projectedAreaSquareMetres;
            EquivalentDiameterMetres = equivalentDiameterMetres;
            LengthMetres = lengthMetres;
            AspectRatio = aspectRatio;
            DragCoefficient = dragCoefficient;
            BallisticCoefficientKilogramsPerSquareMetre = ballisticCoefficientKilogramsPerSquareMetre;
            PositionMetres = positionMetres;
            VelocityMetresPerSecond = velocityMetresPerSecond;
            SpeedMetresPerSecond = speedMetresPerSecond;
            MomentumKilogramMetresPerSecond = momentumKilogramMetresPerSecond;
            TranslationalKineticEnergyJoules = translationalKineticEnergyJoules;
            Orientation = orientation;
            YawAngleRadians = yawAngleRadians;
            TumbleState = tumbleState;
            PenetrationCapabilityJoulesPerSquareMetre = penetrationCapabilityJoulesPerSquareMetre;
            DamageCapabilityJoules = damageCapabilityJoules;
            TerminalState = terminalState;
            RenderState = renderState;
            IsTargetMaterialOrigin = isTargetMaterialOrigin;
            IsParentDerivedMass = isParentDerivedMass;

            var copy = new PhysicalCollisionRecordSnapshot[collisionHistory.Count];
            for (int index = 0; index < copy.Length; index++)
            {
                copy[index] = collisionHistory[index];
            }
            _collisionHistory = Array.AsReadOnly(copy);
        }

        internal string Kind { get; }
        internal string ProjectileId { get; }
        internal string RootShotId { get; }
        internal string ParentProjectileId { get; }
        internal string SourceProjectileId { get; }
        internal string SourceMaterialId { get; }
        internal string SourceMaterialClass { get; }
        internal string SourceCollisionId { get; }
        internal int FragmentIndex { get; }
        internal int FragmentGeneration { get; }
        internal ulong DeterministicSeed { get; }
        internal string Construction { get; }
        internal string ShapeClass { get; }
        internal double OriginalMassKilograms { get; }
        internal double RetainedMassKilograms { get; }
        internal double NominalDiameterMetres { get; }
        internal double DeformedDiameterMetres { get; }
        internal double ProjectedAreaSquareMetres { get; }
        internal double EquivalentDiameterMetres { get; }
        internal double LengthMetres { get; }
        internal double AspectRatio { get; }
        internal double DragCoefficient { get; }
        internal double BallisticCoefficientKilogramsPerSquareMetre { get; }
        internal PhysicalVectorRecord PositionMetres { get; }
        internal PhysicalVectorRecord VelocityMetresPerSecond { get; }
        internal double SpeedMetresPerSecond { get; }
        internal PhysicalVectorRecord MomentumKilogramMetresPerSecond { get; }
        internal double TranslationalKineticEnergyJoules { get; }
        internal PhysicalOrientationRecord Orientation { get; }
        internal double YawAngleRadians { get; }
        internal string TumbleState { get; }
        internal double PenetrationCapabilityJoulesPerSquareMetre { get; }
        internal double DamageCapabilityJoules { get; }
        internal string TerminalState { get; }
        internal string RenderState { get; }
        internal bool IsTargetMaterialOrigin { get; }
        internal bool IsParentDerivedMass { get; }
        internal IReadOnlyList<PhysicalCollisionRecordSnapshot> CollisionHistory => _collisionHistory;
    }

    internal sealed class PhysicalTelemetryEventRecord
    {
        private readonly ReadOnlyCollection<PhysicalComponentRecord> _outputs;

        internal PhysicalTelemetryEventRecord(
            int snapshotSchema,
            int publisherSchema,
            PhysicalTelemetryStageRecord stage,
            string transitionId,
            string outcome,
            PhysicalTelemetryHostRecord host,
            PhysicalTelemetryImpactRecord impact,
            PhysicalComponentRecord parent,
            IReadOnlyList<PhysicalComponentRecord> outputs,
            PhysicalConservationRecord? conservation)
        {
            SnapshotSchema = snapshotSchema;
            PublisherSchema = publisherSchema;
            Stage = stage;
            TransitionId = transitionId;
            Outcome = outcome;
            Host = host;
            Impact = impact;
            Parent = parent;
            Conservation = conservation;

            var copy = new PhysicalComponentRecord[outputs.Count];
            for (int index = 0; index < copy.Length; index++)
            {
                copy[index] = outputs[index];
            }
            _outputs = Array.AsReadOnly(copy);
        }

        internal int SnapshotSchema { get; }
        internal int PublisherSchema { get; }
        internal PhysicalTelemetryStageRecord Stage { get; }
        internal string TransitionId { get; }
        internal string Outcome { get; }
        internal PhysicalTelemetryHostRecord Host { get; }
        internal PhysicalTelemetryImpactRecord Impact { get; }
        internal PhysicalComponentRecord Parent { get; }
        internal IReadOnlyList<PhysicalComponentRecord> Outputs => _outputs;
        internal PhysicalConservationRecord? Conservation { get; }
    }

    internal sealed class PhysicalTelemetryCaptureBuffer
    {
        private readonly object _sync = new object();
        private readonly List<PhysicalTelemetryEventRecord> _records = new List<PhysicalTelemetryEventRecord>();
        private readonly int _maximumRecords;

        internal PhysicalTelemetryCaptureBuffer(int maximumRecords)
        {
            if (maximumRecords <= 0)
            {
                ThrowNonPositiveMaximum(nameof(maximumRecords));
            }
            _maximumRecords = maximumRecords;
        }

        internal int Count
        {
            get
            {
                lock (_sync)
                {
                    return _records.Count;
                }
            }
        }

        internal void Add(PhysicalTelemetryEventRecord record)
        {
            if (record == null)
            {
                ThrowNullRecord(nameof(record));
            }
            lock (_sync)
            {
                _records.Add(record);
                if (_records.Count > _maximumRecords)
                {
                    _records.RemoveAt(0);
                }
            }
        }

        internal IReadOnlyList<PhysicalTelemetryEventRecord> Snapshot()
        {
            lock (_sync)
            {
                return Array.AsReadOnly(_records.ToArray());
            }
        }

        internal void Clear()
        {
            lock (_sync)
            {
                _records.Clear();
            }
        }

        [DoesNotReturn]
        private static void ThrowNonPositiveMaximum(string parameterName)
        {
            throw new ArgumentOutOfRangeException(
                parameterName,
                "Maximum record count must be positive.");
        }

        [DoesNotReturn]
        private static void ThrowNullRecord(string parameterName)
        {
            throw new ArgumentNullException(parameterName);
        }
    }
}

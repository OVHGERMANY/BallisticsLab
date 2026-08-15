using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics.CodeAnalysis;
using System.Linq;

namespace BallisticsLab.Core
{
    internal enum ProtocolVelocityMeasurementBasis
    {
        TargetImpactProxy = 0,
        EftTrajectoryThreeMetres = 1
    }

    internal enum ProtocolShotQualificationReason
    {
        Qualifying = 0,
        MissingEvidence = 1,
        MeasurementBasisMismatch = 2,
        AmmunitionIdentityUnverified = 3,
        AmmunitionIdentityMismatch = 4,
        WitnessBackstopMissing = 5,
        FixtureMismatch = 6,
        FixtureDistanceOutOfRange = 7,
        ImpactAngleOutOfRange = 8,
        VelocityOutOfRange = 9,
        EdgeDistanceInsufficient = 10,
        NeighbourDistanceInsufficient = 11,
        AmmunitionDesignationVariant = 12,
        AmmunitionPhysicalStateMismatch = 13,
        ExpectedPointMismatch = 14
    }

    internal enum ProtocolScreeningStatus
    {
        InsufficientEvidence = 0,
        NonQualifyingEvidence = 1,
        SimulationScreeningComplete = 2
    }

    internal enum ProtocolObservedOutcome
    {
        NotDetermined = 0,
        NoThroughPenetrationObserved = 1,
        ThroughPenetrationObserved = 2
    }

    internal sealed class ProtocolShotEvidence
    {
        private const double FaceToleranceMetres = 0.000001d;

        internal ProtocolShotEvidence(
            long fixtureId,
            string ammunitionTemplateId,
            ProtocolVelocityMeasurementBasis velocityMeasurementBasis,
            double protocolVelocityMetresPerSecond,
            double projectileMassKilograms,
            double projectileDiameterMetres,
            double impactAngleDegrees,
            double fixtureLocalHitXMetres,
            double fixtureLocalHitYMetres,
            double fixtureFaceWidthMetres,
            double fixtureFaceHeightMetres,
            double fixtureDistanceMetres,
            bool witnessBackstopConfigured,
            bool throughPenetrationObserved)
            : this(
                fixtureId,
                ammunitionTemplateId,
                velocityMeasurementBasis,
                protocolVelocityMetresPerSecond,
                projectileMassKilograms,
                projectileDiameterMetres,
                impactAngleDegrees,
                fixtureLocalHitXMetres,
                fixtureLocalHitYMetres,
                fixtureFaceWidthMetres,
                fixtureFaceHeightMetres,
                fixtureDistanceMetres,
                witnessBackstopConfigured,
                throughPenetrationObserved,
                protocolVelocityMetresPerSecond)
        {
        }

        internal ProtocolShotEvidence(
            long fixtureId,
            string ammunitionTemplateId,
            ProtocolVelocityMeasurementBasis velocityMeasurementBasis,
            double protocolVelocityMetresPerSecond,
            double projectileMassKilograms,
            double projectileDiameterMetres,
            double impactAngleDegrees,
            double fixtureLocalHitXMetres,
            double fixtureLocalHitYMetres,
            double fixtureFaceWidthMetres,
            double fixtureFaceHeightMetres,
            double fixtureDistanceMetres,
            bool witnessBackstopConfigured,
            bool throughPenetrationObserved,
            double targetImpactSpeedMetresPerSecond)
        {
            if (fixtureId <= 0L)
            {
                ThrowInvalidFixture(nameof(fixtureId));
            }
            if (velocityMeasurementBasis != ProtocolVelocityMeasurementBasis.TargetImpactProxy
                && velocityMeasurementBasis
                    != ProtocolVelocityMeasurementBasis.EftTrajectoryThreeMetres)
            {
                ThrowInvalidMeasurementBasis(velocityMeasurementBasis);
            }
            AmmunitionTemplateId = Required(
                ammunitionTemplateId,
                nameof(ammunitionTemplateId));
            ValidateNonNegative(
                protocolVelocityMetresPerSecond,
                nameof(protocolVelocityMetresPerSecond));
            ValidateNonNegative(
                targetImpactSpeedMetresPerSecond,
                nameof(targetImpactSpeedMetresPerSecond));
            ValidatePositive(projectileMassKilograms, nameof(projectileMassKilograms));
            ValidatePositive(projectileDiameterMetres, nameof(projectileDiameterMetres));
            ValidateInclusive(impactAngleDegrees, 0d, 90d, nameof(impactAngleDegrees));
            ValidateFinite(fixtureLocalHitXMetres, nameof(fixtureLocalHitXMetres));
            ValidateFinite(fixtureLocalHitYMetres, nameof(fixtureLocalHitYMetres));
            ValidatePositive(fixtureFaceWidthMetres, nameof(fixtureFaceWidthMetres));
            ValidatePositive(fixtureFaceHeightMetres, nameof(fixtureFaceHeightMetres));
            ValidatePositive(fixtureDistanceMetres, nameof(fixtureDistanceMetres));
            if (Math.Abs(fixtureLocalHitXMetres)
                    > fixtureFaceWidthMetres * 0.5d + FaceToleranceMetres
                || Math.Abs(fixtureLocalHitYMetres)
                    > fixtureFaceHeightMetres * 0.5d + FaceToleranceMetres)
            {
                ThrowHitOutsideFace();
            }

            FixtureId = fixtureId;
            VelocityMeasurementBasis = velocityMeasurementBasis;
            ProtocolVelocityMetresPerSecond = protocolVelocityMetresPerSecond;
            TargetImpactSpeedMetresPerSecond = targetImpactSpeedMetresPerSecond;
            ProjectileMassKilograms = projectileMassKilograms;
            ProjectileDiameterMetres = projectileDiameterMetres;
            ImpactAngleDegrees = impactAngleDegrees;
            FixtureLocalHitXMetres = fixtureLocalHitXMetres;
            FixtureLocalHitYMetres = fixtureLocalHitYMetres;
            FixtureFaceWidthMetres = fixtureFaceWidthMetres;
            FixtureFaceHeightMetres = fixtureFaceHeightMetres;
            FixtureDistanceMetres = fixtureDistanceMetres;
            WitnessBackstopConfigured = witnessBackstopConfigured;
            ThroughPenetrationObserved = throughPenetrationObserved;
        }

        internal long FixtureId { get; }
        internal string AmmunitionTemplateId { get; }
        internal ProtocolVelocityMeasurementBasis VelocityMeasurementBasis { get; }
        internal double ProtocolVelocityMetresPerSecond { get; }
        internal double TargetImpactSpeedMetresPerSecond { get; }
        internal double ProjectileMassKilograms { get; }
        internal double ProjectileDiameterMetres { get; }
        internal double ImpactAngleDegrees { get; }
        internal double FixtureLocalHitXMetres { get; }
        internal double FixtureLocalHitYMetres { get; }
        internal double FixtureFaceWidthMetres { get; }
        internal double FixtureFaceHeightMetres { get; }
        internal double FixtureDistanceMetres { get; }
        internal bool WitnessBackstopConfigured { get; }
        internal bool ThroughPenetrationObserved { get; }

        internal double DistanceToNearestEdgeMetres => Math.Min(
            FixtureFaceWidthMetres * 0.5d - Math.Abs(FixtureLocalHitXMetres),
            FixtureFaceHeightMetres * 0.5d - Math.Abs(FixtureLocalHitYMetres));

        private static string Required(string value, string parameterName)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                ThrowMissingText(parameterName);
            }
            return value;
        }

        private static void ValidateFinite(double value, string parameterName)
        {
            if (!IsFinite(value))
            {
                ThrowNonFinite(parameterName);
            }
        }

        private static void ValidateNonNegative(double value, string parameterName)
        {
            if (!IsFinite(value) || value < 0d)
            {
                ThrowNonNegative(parameterName);
            }
        }

        private static void ValidatePositive(double value, string parameterName)
        {
            if (!IsFinite(value) || value <= 0d)
            {
                ThrowPositive(parameterName);
            }
        }

        private static void ValidateInclusive(
            double value,
            double minimum,
            double maximum,
            string parameterName)
        {
            if (!IsFinite(value) || value < minimum || value > maximum)
            {
                ThrowOutsideRange(parameterName, minimum, maximum);
            }
        }

        private static bool IsFinite(double value)
        {
            return !double.IsNaN(value) && !double.IsInfinity(value);
        }

        [DoesNotReturn]
        private static void ThrowInvalidFixture(string parameterName)
        {
            throw new ArgumentOutOfRangeException(parameterName, "Fixture identity must be positive.");
        }

        [DoesNotReturn]
        private static void ThrowInvalidMeasurementBasis(ProtocolVelocityMeasurementBasis value)
        {
            throw new ArgumentOutOfRangeException(
                nameof(value),
                value,
                "Unsupported protocol velocity-measurement basis.");
        }

        [DoesNotReturn]
        private static void ThrowMissingText(string parameterName)
        {
            throw new ArgumentException("A nonempty value is required.", parameterName);
        }

        [DoesNotReturn]
        private static void ThrowNonFinite(string parameterName)
        {
            throw new ArgumentOutOfRangeException(parameterName, "Value must be finite.");
        }

        [DoesNotReturn]
        private static void ThrowNonNegative(string parameterName)
        {
            throw new ArgumentOutOfRangeException(parameterName, "Value must be finite and non-negative.");
        }

        [DoesNotReturn]
        private static void ThrowPositive(string parameterName)
        {
            throw new ArgumentOutOfRangeException(parameterName, "Value must be finite and positive.");
        }

        [DoesNotReturn]
        private static void ThrowOutsideRange(
            string parameterName,
            double minimum,
            double maximum)
        {
            throw new ArgumentOutOfRangeException(
                parameterName,
                "Value must be between " + minimum + " and " + maximum + ".");
        }

        [DoesNotReturn]
        private static void ThrowHitOutsideFace()
        {
            throw new ArgumentOutOfRangeException(
                "fixtureLocalHit",
                "The fixture-local impact point must be on the recorded plate face.");
        }
    }

    internal sealed class ProtocolThreatDefinition
    {
        internal ProtocolThreatDefinition(
            string threatId,
            string protectionClass,
            string cartridgeDesignation,
            string projectileConstruction,
            double nominalProjectileMassKilograms,
            double minimumVelocityMetresPerSecond,
            double maximumVelocityMetresPerSecond,
            double testDistanceMetres,
            double testDistanceToleranceMetres,
            double velocityMeasurementDistanceMetres,
            int requiredQualifyingShots,
            double maximumImpactAngleDegrees,
            double minimumSeparationDiameters,
            IReadOnlyList<ProtocolAmmunitionMapping> ammunitionMappings)
        {
            ThreatId = Required(threatId, nameof(threatId));
            ProtectionClass = Required(protectionClass, nameof(protectionClass));
            CartridgeDesignation = Required(
                cartridgeDesignation,
                nameof(cartridgeDesignation));
            ProjectileConstruction = Required(
                projectileConstruction,
                nameof(projectileConstruction));
            ValidatePositive(
                nominalProjectileMassKilograms,
                nameof(nominalProjectileMassKilograms));
            ValidatePositive(
                minimumVelocityMetresPerSecond,
                nameof(minimumVelocityMetresPerSecond));
            ValidatePositive(
                maximumVelocityMetresPerSecond,
                nameof(maximumVelocityMetresPerSecond));
            if (maximumVelocityMetresPerSecond < minimumVelocityMetresPerSecond)
            {
                ThrowInvalidVelocityRange();
            }
            ValidatePositive(testDistanceMetres, nameof(testDistanceMetres));
            ValidateNonNegative(
                testDistanceToleranceMetres,
                nameof(testDistanceToleranceMetres));
            ValidatePositive(
                velocityMeasurementDistanceMetres,
                nameof(velocityMeasurementDistanceMetres));
            if (requiredQualifyingShots <= 0 || requiredQualifyingShots > 100)
            {
                ThrowInvalidShotCount();
            }
            ValidateInclusive(
                maximumImpactAngleDegrees,
                0d,
                90d,
                nameof(maximumImpactAngleDegrees));
            ValidatePositive(
                minimumSeparationDiameters,
                nameof(minimumSeparationDiameters));
            if (ammunitionMappings == null)
            {
                ThrowNullAmmunitionMappings(nameof(ammunitionMappings));
            }

            NominalProjectileMassKilograms = nominalProjectileMassKilograms;
            MinimumVelocityMetresPerSecond = minimumVelocityMetresPerSecond;
            MaximumVelocityMetresPerSecond = maximumVelocityMetresPerSecond;
            TestDistanceMetres = testDistanceMetres;
            TestDistanceToleranceMetres = testDistanceToleranceMetres;
            VelocityMeasurementDistanceMetres = velocityMeasurementDistanceMetres;
            RequiredQualifyingShots = requiredQualifyingShots;
            MaximumImpactAngleDegrees = maximumImpactAngleDegrees;
            MinimumSeparationDiameters = minimumSeparationDiameters;
            ProtocolAmmunitionMapping[] mappings = ammunitionMappings.ToArray();
            if (mappings.Any(mapping => mapping == null))
            {
                ThrowNullAmmunitionMapping(nameof(ammunitionMappings));
            }
            string[] duplicateTemplateIds = mappings
                .Where(mapping => mapping.HasInstalledTemplate)
                .GroupBy(mapping => mapping.TemplateId, StringComparer.Ordinal)
                .Where(group => group.Count() > 1)
                .Select(group => group.Key)
                .ToArray();
            if (duplicateTemplateIds.Length != 0)
            {
                ThrowDuplicateAmmunitionMapping(
                    duplicateTemplateIds[0],
                    nameof(ammunitionMappings));
            }
            AmmunitionMappings = Array.AsReadOnly(mappings);
            string[] ids = mappings
                .Where(mapping => mapping.CanQualifySimulationScreening)
                .Select(mapping => mapping.TemplateId)
                .Distinct(StringComparer.Ordinal)
                .OrderBy(id => id, StringComparer.Ordinal)
                .ToArray();
            VerifiedAmmunitionTemplateIds = Array.AsReadOnly(ids);
        }

        internal string ThreatId { get; }
        internal string ProtectionClass { get; }
        internal string CartridgeDesignation { get; }
        internal string ProjectileConstruction { get; }
        internal double NominalProjectileMassKilograms { get; }
        internal double MinimumVelocityMetresPerSecond { get; }
        internal double MaximumVelocityMetresPerSecond { get; }
        internal double TestDistanceMetres { get; }
        internal double TestDistanceToleranceMetres { get; }
        internal double VelocityMeasurementDistanceMetres { get; }
        internal int RequiredQualifyingShots { get; }
        internal double MaximumImpactAngleDegrees { get; }
        internal double MinimumSeparationDiameters { get; }
        internal IReadOnlyList<ProtocolAmmunitionMapping> AmmunitionMappings { get; }
        internal IReadOnlyList<string> VerifiedAmmunitionTemplateIds { get; }
        internal bool SimulationScreeningOnly { get; } = true;

        internal bool TryGetAmmunitionMapping(
            string templateId,
            out ProtocolAmmunitionMapping? mapping)
        {
            mapping = AmmunitionMappings.FirstOrDefault(candidate =>
                candidate.HasInstalledTemplate
                && string.Equals(candidate.TemplateId, templateId, StringComparison.Ordinal));
            return mapping != null;
        }

        private static string Required(string value, string parameterName)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                ThrowMissingText(parameterName);
            }
            return value;
        }

        private static void ValidatePositive(double value, string parameterName)
        {
            if (!IsFinite(value) || value <= 0d)
            {
                ThrowPositive(parameterName);
            }
        }

        private static void ValidateNonNegative(double value, string parameterName)
        {
            if (!IsFinite(value) || value < 0d)
            {
                ThrowNonNegative(parameterName);
            }
        }

        private static void ValidateInclusive(
            double value,
            double minimum,
            double maximum,
            string parameterName)
        {
            if (!IsFinite(value) || value < minimum || value > maximum)
            {
                ThrowOutsideRange(parameterName, minimum, maximum);
            }
        }

        private static bool IsFinite(double value)
        {
            return !double.IsNaN(value) && !double.IsInfinity(value);
        }

        [DoesNotReturn]
        private static void ThrowMissingText(string parameterName)
        {
            throw new ArgumentException("A nonempty value is required.", parameterName);
        }

        [DoesNotReturn]
        private static void ThrowPositive(string parameterName)
        {
            throw new ArgumentOutOfRangeException(parameterName, "Value must be finite and positive.");
        }

        [DoesNotReturn]
        private static void ThrowNonNegative(string parameterName)
        {
            throw new ArgumentOutOfRangeException(parameterName, "Value must be finite and non-negative.");
        }

        [DoesNotReturn]
        private static void ThrowOutsideRange(
            string parameterName,
            double minimum,
            double maximum)
        {
            throw new ArgumentOutOfRangeException(
                parameterName,
                "Value must be between " + minimum + " and " + maximum + ".");
        }

        [DoesNotReturn]
        private static void ThrowInvalidVelocityRange()
        {
            throw new ArgumentOutOfRangeException(
                "maximumVelocityMetresPerSecond",
                "Maximum velocity must not be less than minimum velocity.");
        }

        [DoesNotReturn]
        private static void ThrowInvalidShotCount()
        {
            throw new ArgumentOutOfRangeException(
                "requiredQualifyingShots",
                "Required qualifying shot count must be between 1 and 100.");
        }

        [DoesNotReturn]
        private static void ThrowNullAmmunitionMappings(string parameterName)
        {
            throw new ArgumentNullException(parameterName);
        }

        [DoesNotReturn]
        private static void ThrowNullAmmunitionMapping(string parameterName)
        {
            throw new ArgumentException(
                "Ammunition mappings cannot contain a null entry.",
                parameterName);
        }

        [DoesNotReturn]
        private static void ThrowDuplicateAmmunitionMapping(
            string templateId,
            string parameterName)
        {
            throw new ArgumentException(
                "Ammunition template mapping is duplicated: " + templateId,
                parameterName);
        }
    }

    internal sealed class ProtocolShotQualification
    {
        internal ProtocolShotQualification(
            int shotIndex,
            ProtocolShotQualificationReason reason,
            ProtocolShotEvidence evidence)
        {
            ShotIndex = shotIndex;
            Reason = reason;
            Evidence = evidence ?? throw new ArgumentNullException(nameof(evidence));
        }

        internal int ShotIndex { get; }
        internal ProtocolShotQualificationReason Reason { get; }
        internal ProtocolShotEvidence Evidence { get; }
        internal bool IsQualifying => Reason == ProtocolShotQualificationReason.Qualifying;
    }

    internal sealed class ProtocolScreeningEvaluation
    {
        internal ProtocolScreeningEvaluation(
            ProtocolScreeningStatus status,
            ProtocolObservedOutcome observedOutcome,
            int requiredQualifyingShots,
            IReadOnlyList<ProtocolShotQualification> shots,
            bool hasKnownNominalMassMismatch,
            bool hasInstalledLocaleMassMismatch)
        {
            Status = status;
            ObservedOutcome = observedOutcome;
            RequiredQualifyingShots = requiredQualifyingShots;
            var copy = new ProtocolShotQualification[shots.Count];
            for (int index = 0; index < copy.Length; index++)
            {
                copy[index] = shots[index];
            }
            Shots = Array.AsReadOnly(copy);
            QualifyingShotCount = copy.Count(shot => shot.IsQualifying);
            HasKnownNominalMassMismatch = hasKnownNominalMassMismatch;
            HasInstalledLocaleMassMismatch = hasInstalledLocaleMassMismatch;
        }

        internal ProtocolScreeningStatus Status { get; }
        internal ProtocolObservedOutcome ObservedOutcome { get; }
        internal int RequiredQualifyingShots { get; }
        internal int QualifyingShotCount { get; }
        internal IReadOnlyList<ProtocolShotQualification> Shots { get; }
        internal bool HasKnownNominalMassMismatch { get; }
        internal bool HasInstalledLocaleMassMismatch { get; }
        internal bool IsCertificationClaim { get; }
    }

    internal static class ProtocolScreeningEvaluator
    {
        internal static ProtocolScreeningEvaluation Evaluate(
            ProtocolThreatDefinition threat,
            IReadOnlyList<ProtocolShotEvidence> evidence)
        {
            if (threat == null)
            {
                ThrowNullThreat(nameof(threat));
            }
            if (evidence == null)
            {
                ThrowNullEvidence(nameof(evidence));
            }

            var results = new List<ProtocolShotQualification>(evidence.Count);
            var qualifying = new List<ProtocolShotEvidence>(evidence.Count);
            long fixtureId = evidence.Count == 0 ? 0L : evidence[0].FixtureId;
            for (int index = 0; index < evidence.Count; index++)
            {
                ProtocolShotEvidence? shot = evidence[index];
                if (shot == null)
                {
                    ThrowNullShot(nameof(evidence));
                }
                ProtocolShotQualificationReason reason = Qualify(
                    threat,
                    shot,
                    fixtureId,
                    qualifying);
                results.Add(new ProtocolShotQualification(index, reason, shot));
                if (reason == ProtocolShotQualificationReason.Qualifying)
                {
                    qualifying.Add(shot);
                }
            }

            bool missingFoundation = evidence.Count == 0
                || results.Any(result => result.Reason
                    == ProtocolShotQualificationReason.MissingEvidence)
                || results.Any(result => result.Reason
                    == ProtocolShotQualificationReason.MeasurementBasisMismatch)
                || results.Any(result => result.Reason
                    == ProtocolShotQualificationReason.AmmunitionIdentityUnverified)
                || results.Any(result => result.Reason
                    == ProtocolShotQualificationReason.AmmunitionDesignationVariant)
                || results.Any(result => result.Reason
                    == ProtocolShotQualificationReason.AmmunitionPhysicalStateMismatch);
            ProtocolScreeningStatus status = qualifying.Count >= threat.RequiredQualifyingShots
                ? ProtocolScreeningStatus.SimulationScreeningComplete
                : missingFoundation
                    ? ProtocolScreeningStatus.InsufficientEvidence
                    : ProtocolScreeningStatus.NonQualifyingEvidence;
            ProtocolObservedOutcome outcome = ProtocolObservedOutcome.NotDetermined;
            if (status == ProtocolScreeningStatus.SimulationScreeningComplete)
            {
                outcome = qualifying.Any(shot => shot.ThroughPenetrationObserved)
                    ? ProtocolObservedOutcome.ThroughPenetrationObserved
                    : ProtocolObservedOutcome.NoThroughPenetrationObserved;
            }
            bool hasKnownNominalMassMismatch = false;
            bool hasInstalledLocaleMassMismatch = false;
            for (int index = 0; index < qualifying.Count; index++)
            {
                ProtocolShotEvidence shot = qualifying[index];
                if (!threat.TryGetAmmunitionMapping(
                        shot.AmmunitionTemplateId,
                        out ProtocolAmmunitionMapping? mapping)
                    || mapping == null)
                {
                    continue;
                }
                hasKnownNominalMassMismatch |= !mapping.InstalledMassMatchesNominal(
                    threat.NominalProjectileMassKilograms);
                hasInstalledLocaleMassMismatch |= !mapping.InstalledMassMatchesLocale();
            }
            return new ProtocolScreeningEvaluation(
                status,
                outcome,
                threat.RequiredQualifyingShots,
                new ReadOnlyCollection<ProtocolShotQualification>(results),
                hasKnownNominalMassMismatch,
                hasInstalledLocaleMassMismatch);
        }

        private static ProtocolShotQualificationReason Qualify(
            ProtocolThreatDefinition threat,
            ProtocolShotEvidence shot,
            long fixtureId,
            List<ProtocolShotEvidence> qualifying)
        {
            if (shot.VelocityMeasurementBasis
                != ProtocolVelocityMeasurementBasis.EftTrajectoryThreeMetres)
            {
                return ProtocolShotQualificationReason.MeasurementBasisMismatch;
            }
            if (threat.TryGetAmmunitionMapping(
                    shot.AmmunitionTemplateId,
                    out ProtocolAmmunitionMapping? mapping)
                && mapping != null)
            {
                if (mapping.IdentityStatus
                    == ProtocolAmmunitionIdentityStatus.VariantDesignation)
                {
                    return ProtocolShotQualificationReason.AmmunitionDesignationVariant;
                }
                if (!mapping.CanQualifySimulationScreening)
                {
                    return ProtocolShotQualificationReason.AmmunitionIdentityUnverified;
                }
                if (!mapping.MatchesRecordedProjectileMass(shot.ProjectileMassKilograms))
                {
                    return ProtocolShotQualificationReason.AmmunitionPhysicalStateMismatch;
                }
                if (!mapping.MatchesRecordedProjectileDiameter(shot.ProjectileDiameterMetres))
                {
                    return ProtocolShotQualificationReason.AmmunitionPhysicalStateMismatch;
                }
            }
            else
            {
                if (threat.VerifiedAmmunitionTemplateIds.Count == 0)
                {
                    return ProtocolShotQualificationReason.AmmunitionIdentityUnverified;
                }
                return ProtocolShotQualificationReason.AmmunitionIdentityMismatch;
            }
            if (!shot.WitnessBackstopConfigured)
            {
                return ProtocolShotQualificationReason.WitnessBackstopMissing;
            }
            if (fixtureId != 0L && shot.FixtureId != fixtureId)
            {
                return ProtocolShotQualificationReason.FixtureMismatch;
            }
            if (Math.Abs(shot.FixtureDistanceMetres - threat.TestDistanceMetres)
                > threat.TestDistanceToleranceMetres)
            {
                return ProtocolShotQualificationReason.FixtureDistanceOutOfRange;
            }
            if (shot.ImpactAngleDegrees > threat.MaximumImpactAngleDegrees)
            {
                return ProtocolShotQualificationReason.ImpactAngleOutOfRange;
            }
            bool withinVelocity = shot.ProtocolVelocityMetresPerSecond
                    >= threat.MinimumVelocityMetresPerSecond
                && shot.ProtocolVelocityMetresPerSecond <= threat.MaximumVelocityMetresPerSecond;
            bool severityException = (
                    shot.ProtocolVelocityMetresPerSecond < threat.MinimumVelocityMetresPerSecond
                    && shot.ThroughPenetrationObserved)
                || (shot.ProtocolVelocityMetresPerSecond > threat.MaximumVelocityMetresPerSecond
                    && !shot.ThroughPenetrationObserved);
            if (!withinVelocity && !severityException)
            {
                return ProtocolShotQualificationReason.VelocityOutOfRange;
            }
            double minimumEdgeDistance = shot.ProjectileDiameterMetres
                * threat.MinimumSeparationDiameters;
            if (shot.DistanceToNearestEdgeMetres < minimumEdgeDistance)
            {
                return ProtocolShotQualificationReason.EdgeDistanceInsufficient;
            }
            for (int index = 0; index < qualifying.Count; index++)
            {
                ProtocolShotEvidence previous = qualifying[index];
                double deltaX = shot.FixtureLocalHitXMetres - previous.FixtureLocalHitXMetres;
                double deltaY = shot.FixtureLocalHitYMetres - previous.FixtureLocalHitYMetres;
                double distance = Math.Sqrt(deltaX * deltaX + deltaY * deltaY);
                double required = Math.Max(
                        shot.ProjectileDiameterMetres,
                        previous.ProjectileDiameterMetres)
                    * threat.MinimumSeparationDiameters;
                if (distance < required)
                {
                    return ProtocolShotQualificationReason.NeighbourDistanceInsufficient;
                }
            }
            return ProtocolShotQualificationReason.Qualifying;
        }

        [DoesNotReturn]
        private static void ThrowNullThreat(string parameterName)
        {
            throw new ArgumentNullException(parameterName);
        }

        [DoesNotReturn]
        private static void ThrowNullEvidence(string parameterName)
        {
            throw new ArgumentNullException(parameterName);
        }

        [DoesNotReturn]
        private static void ThrowNullShot(string parameterName)
        {
            throw new ArgumentException("Protocol evidence cannot contain a null shot.", parameterName);
        }
    }
}

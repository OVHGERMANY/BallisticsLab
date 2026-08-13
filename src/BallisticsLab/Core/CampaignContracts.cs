using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Text;

namespace BallisticsLab.Core
{
    internal enum CampaignFixtureSelectorKind
    {
        ExactTemplate = 0,
        MaterialAndArmorClass = 1
    }

    internal enum CampaignResetPolicy
    {
        BeforeEachCase = 0,
        BeforeEachShot = 1
    }

    internal enum CampaignRunState
    {
        Idle = 0,
        AwaitingFixture = 1,
        AwaitingShot = 2,
        AwaitingReset = 3,
        Completed = 4,
        Stopped = 5
    }

    internal enum CampaignAttemptStatus
    {
        Accepted = 0,
        FixtureMismatch = 1,
        VelocityOutOfRange = 2,
        IncompleteLayerChain = 3,
        MissingBackstop = 4,
        MissingPhysicalEvidence = 5,
        ConservationFailure = 6,
        MissingConservationEvidence = 7
    }

    internal sealed class CampaignCaseDefinition
    {
        internal CampaignCaseDefinition(
            string caseId,
            string label,
            CampaignFixtureSelectorKind selectorKind,
            string templateId,
            string material,
            int armorClass,
            int layerCount,
            double layerSpacingMetres,
            double plateThicknessMetres,
            double distanceMetres,
            double angleDegrees,
            bool backstopEnabled,
            int requiredRepetitions,
            CampaignResetPolicy resetPolicy,
            double minimumVelocityFraction,
            double maximumVelocityFraction,
            bool requireBackstopEvidence,
            bool requirePhysicalEvidence,
            bool requireConservationEvidence,
            double maximumMassClosureErrorKilograms,
            double maximumEnergyClosureErrorJoules)
        {
            CaseId = Required(caseId, nameof(caseId));
            Label = Required(label, nameof(label));
            ValidateSelector(selectorKind, templateId, material, armorClass);
            ValidateRange(layerCount, 1, LabPolicies.MaximumLayers, nameof(layerCount));
            ValidatePositive(plateThicknessMetres, nameof(plateThicknessMetres));
            ValidatePositive(distanceMetres, nameof(distanceMetres));
            ValidateNonNegative(layerSpacingMetres, nameof(layerSpacingMetres));
            ValidateInclusive(angleDegrees, -89d, 89d, nameof(angleDegrees));
            ValidateRange(requiredRepetitions, 1, 1000, nameof(requiredRepetitions));
            ValidateNonNegative(minimumVelocityFraction, nameof(minimumVelocityFraction));
            ValidatePositive(maximumVelocityFraction, nameof(maximumVelocityFraction));
            if (maximumVelocityFraction < minimumVelocityFraction)
            {
                ThrowInvalidRange(nameof(maximumVelocityFraction));
            }
            if (requireConservationEvidence && !requirePhysicalEvidence)
            {
                ThrowConservationRequiresPhysical(nameof(requireConservationEvidence));
            }
            ValidateNonNegative(
                maximumMassClosureErrorKilograms,
                nameof(maximumMassClosureErrorKilograms));
            ValidateNonNegative(
                maximumEnergyClosureErrorJoules,
                nameof(maximumEnergyClosureErrorJoules));

            SelectorKind = selectorKind;
            TemplateId = templateId ?? string.Empty;
            Material = material ?? string.Empty;
            ArmorClass = armorClass;
            LayerCount = layerCount;
            LayerSpacingMetres = layerSpacingMetres;
            PlateThicknessMetres = plateThicknessMetres;
            DistanceMetres = distanceMetres;
            AngleDegrees = angleDegrees;
            BackstopEnabled = backstopEnabled;
            RequiredRepetitions = requiredRepetitions;
            ResetPolicy = resetPolicy;
            MinimumVelocityFraction = minimumVelocityFraction;
            MaximumVelocityFraction = maximumVelocityFraction;
            RequireBackstopEvidence = requireBackstopEvidence;
            RequirePhysicalEvidence = requirePhysicalEvidence;
            RequireConservationEvidence = requireConservationEvidence;
            MaximumMassClosureErrorKilograms = maximumMassClosureErrorKilograms;
            MaximumEnergyClosureErrorJoules = maximumEnergyClosureErrorJoules;
        }

        internal string CaseId { get; }
        internal string Label { get; }
        internal CampaignFixtureSelectorKind SelectorKind { get; }
        internal string TemplateId { get; }
        internal string Material { get; }
        internal int ArmorClass { get; }
        internal int LayerCount { get; }
        internal double LayerSpacingMetres { get; }
        internal double PlateThicknessMetres { get; }
        internal double DistanceMetres { get; }
        internal double AngleDegrees { get; }
        internal bool BackstopEnabled { get; }
        internal int RequiredRepetitions { get; }
        internal CampaignResetPolicy ResetPolicy { get; }
        internal double MinimumVelocityFraction { get; }
        internal double MaximumVelocityFraction { get; }
        internal bool RequireBackstopEvidence { get; }
        internal bool RequirePhysicalEvidence { get; }
        internal bool RequireConservationEvidence { get; }
        internal double MaximumMassClosureErrorKilograms { get; }
        internal double MaximumEnergyClosureErrorJoules { get; }

        private static void ValidateSelector(
            CampaignFixtureSelectorKind selectorKind,
            string templateId,
            string material,
            int armorClass)
        {
            if (selectorKind == CampaignFixtureSelectorKind.ExactTemplate)
            {
                if (string.IsNullOrWhiteSpace(templateId))
                {
                    ThrowMissingSelector(nameof(templateId));
                }
                return;
            }
            if (selectorKind == CampaignFixtureSelectorKind.MaterialAndArmorClass)
            {
                if (string.IsNullOrWhiteSpace(material) || armorClass <= 0)
                {
                    ThrowMissingSelector(nameof(material));
                }
                return;
            }
            ThrowUnsupportedSelector(selectorKind);
        }

        private static string Required(string value, string parameterName)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                ThrowMissingText(parameterName);
            }
            return value;
        }

        private static void ValidateRange(int value, int minimum, int maximum, string parameterName)
        {
            if (value < minimum || value > maximum)
            {
                ThrowOutsideRange(parameterName, minimum, maximum);
            }
        }

        private static void ValidatePositive(double value, string parameterName)
        {
            if (!IsFinite(value) || value <= 0d)
            {
                ThrowNonPositive(parameterName);
            }
        }

        private static void ValidateNonNegative(double value, string parameterName)
        {
            if (!IsFinite(value) || value < 0d)
            {
                ThrowNegativeOrNonFinite(parameterName);
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

        [DoesNotReturn]
        private static void ThrowMissingText(string parameterName)
        {
            throw new ArgumentException("A nonempty value is required.", parameterName);
        }

        [DoesNotReturn]
        private static void ThrowMissingSelector(string parameterName)
        {
            throw new ArgumentException("The fixture selector is incomplete.", parameterName);
        }

        [DoesNotReturn]
        private static void ThrowUnsupportedSelector(CampaignFixtureSelectorKind selectorKind)
        {
            throw new ArgumentOutOfRangeException(
                nameof(selectorKind),
                selectorKind,
                "Unsupported campaign fixture selector.");
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
        private static void ThrowNonPositive(string parameterName)
        {
            throw new ArgumentOutOfRangeException(parameterName, "Value must be finite and positive.");
        }

        [DoesNotReturn]
        private static void ThrowNegativeOrNonFinite(string parameterName)
        {
            throw new ArgumentOutOfRangeException(parameterName, "Value must be finite and non-negative.");
        }

        [DoesNotReturn]
        private static void ThrowInvalidRange(string parameterName)
        {
            throw new ArgumentOutOfRangeException(
                parameterName,
                "Maximum velocity fraction must not be below the minimum.");
        }

        [DoesNotReturn]
        private static void ThrowConservationRequiresPhysical(string parameterName)
        {
            throw new ArgumentException(
                "Conservation evidence cannot be required without physical-transition evidence.",
                parameterName);
        }

        private static bool IsFinite(double value)
        {
            return !double.IsNaN(value) && !double.IsInfinity(value);
        }
    }

    internal sealed class CampaignDefinition
    {
        internal CampaignDefinition(
            string campaignId,
            string name,
            IReadOnlyList<CampaignCaseDefinition> cases)
        {
            CampaignId = Required(campaignId, nameof(campaignId));
            Name = Required(name, nameof(name));
            if (cases == null)
            {
                ThrowNullCases(nameof(cases));
            }
            if (cases.Count == 0)
            {
                ThrowEmptyCases(nameof(cases));
            }

            var caseIds = new HashSet<string>(StringComparer.Ordinal);
            var copy = new CampaignCaseDefinition[cases.Count];
            for (int index = 0; index < cases.Count; index++)
            {
                CampaignCaseDefinition campaignCase = cases[index];
                if (campaignCase == null)
                {
                    ThrowNullCase(nameof(cases));
                }
                if (!caseIds.Add(campaignCase.CaseId))
                {
                    ThrowDuplicateCase(campaignCase.CaseId);
                }
                copy[index] = campaignCase;
            }
            Cases = Array.AsReadOnly(copy);
        }

        internal string CampaignId { get; }
        internal string Name { get; }
        internal IReadOnlyList<CampaignCaseDefinition> Cases { get; }
        internal int RequiredRepetitions => Cases.Sum(campaignCase => campaignCase.RequiredRepetitions);

        private static string Required(string value, string parameterName)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                ThrowMissingText(parameterName);
            }
            return value;
        }

        [DoesNotReturn]
        private static void ThrowMissingText(string parameterName)
        {
            throw new ArgumentException("A nonempty value is required.", parameterName);
        }

        [DoesNotReturn]
        private static void ThrowNullCases(string parameterName)
        {
            throw new ArgumentNullException(parameterName);
        }

        [DoesNotReturn]
        private static void ThrowEmptyCases(string parameterName)
        {
            throw new ArgumentException("At least one campaign case is required.", parameterName);
        }

        [DoesNotReturn]
        private static void ThrowNullCase(string parameterName)
        {
            throw new ArgumentException("Campaign cases cannot contain null entries.", parameterName);
        }

        [DoesNotReturn]
        private static void ThrowDuplicateCase(string caseId)
        {
            throw new ArgumentException("Duplicate campaign case ID: " + caseId, nameof(caseId));
        }
    }

    internal sealed class CampaignShotEvidence
    {
        internal CampaignShotEvidence(
            long fixtureId,
            string chainId,
            string fixtureTemplateId,
            string fixtureArmorMaterial,
            int fixtureArmorClass,
            int fixtureLayerCount,
            int rootFireIndex,
            int observedRootRandomSeed,
            string rootShooterProfileId,
            string ammunitionTemplateId,
            double velocityFraction,
            IReadOnlyList<int> hitLayers,
            string outcome,
            bool reachedBackstop,
            int physicalTransitionCount,
            int conservationRecordCount,
            double maximumMassClosureErrorKilograms,
            double maximumEnergyClosureErrorJoules,
            ProtocolShotEvidence? protocolEvidence = null)
        {
            if (fixtureId <= 0L)
            {
                ThrowInvalidFixture(nameof(fixtureId));
            }
            ChainId = Required(chainId, nameof(chainId));
            FixtureTemplateId = Required(fixtureTemplateId, nameof(fixtureTemplateId));
            FixtureArmorMaterial = Required(fixtureArmorMaterial, nameof(fixtureArmorMaterial));
            ValidateRange(fixtureArmorClass, 1, 100, nameof(fixtureArmorClass));
            ValidateRange(fixtureLayerCount, 1, LabPolicies.MaximumLayers, nameof(fixtureLayerCount));
            AmmunitionTemplateId = Required(ammunitionTemplateId, nameof(ammunitionTemplateId));
            if (protocolEvidence != null
                && (protocolEvidence.FixtureId != fixtureId
                    || !string.Equals(
                        protocolEvidence.AmmunitionTemplateId,
                        AmmunitionTemplateId,
                        StringComparison.Ordinal)))
            {
                ThrowMismatchedProtocolEvidence();
            }
            Outcome = Required(outcome, nameof(outcome));
            ValidateNonNegative(velocityFraction, nameof(velocityFraction));
            ValidateNonNegative(
                maximumMassClosureErrorKilograms,
                nameof(maximumMassClosureErrorKilograms));
            ValidateNonNegative(
                maximumEnergyClosureErrorJoules,
                nameof(maximumEnergyClosureErrorJoules));
            if (physicalTransitionCount < 0)
            {
                ThrowNegativeCount(nameof(physicalTransitionCount));
            }
            if (conservationRecordCount < 0 || conservationRecordCount > physicalTransitionCount)
            {
                ThrowInvalidConservationCount(nameof(conservationRecordCount));
            }
            if (hitLayers == null)
            {
                ThrowNullLayers(nameof(hitLayers));
            }

            int[] layers = hitLayers.Distinct().OrderBy(layer => layer).ToArray();
            if (layers.Any(layer => layer < 0))
            {
                ThrowNegativeLayer(nameof(hitLayers));
            }
            if (layers.Any(layer => layer >= fixtureLayerCount))
            {
                ThrowLayerOutsideFixture(nameof(hitLayers));
            }

            FixtureId = fixtureId;
            FixtureArmorClass = fixtureArmorClass;
            FixtureLayerCount = fixtureLayerCount;
            RootFireIndex = rootFireIndex;
            ObservedRootRandomSeed = observedRootRandomSeed;
            RootShooterProfileId = rootShooterProfileId ?? string.Empty;
            VelocityFraction = velocityFraction;
            HitLayers = Array.AsReadOnly(layers);
            ReachedBackstop = reachedBackstop;
            PhysicalTransitionCount = physicalTransitionCount;
            ConservationRecordCount = conservationRecordCount;
            MaximumMassClosureErrorKilograms = maximumMassClosureErrorKilograms;
            MaximumEnergyClosureErrorJoules = maximumEnergyClosureErrorJoules;
            ProtocolEvidence = protocolEvidence;
        }

        internal long FixtureId { get; }
        internal string ChainId { get; }
        internal string FixtureTemplateId { get; }
        internal string FixtureArmorMaterial { get; }
        internal int FixtureArmorClass { get; }
        internal int FixtureLayerCount { get; }
        internal int RootFireIndex { get; }
        internal int ObservedRootRandomSeed { get; }
        internal string RootShooterProfileId { get; }
        internal string AmmunitionTemplateId { get; }
        internal double VelocityFraction { get; }
        internal IReadOnlyList<int> HitLayers { get; }
        internal string Outcome { get; }
        internal bool ReachedBackstop { get; }
        internal int PhysicalTransitionCount { get; }
        internal int ConservationRecordCount { get; }
        internal double MaximumMassClosureErrorKilograms { get; }
        internal double MaximumEnergyClosureErrorJoules { get; }
        internal ProtocolShotEvidence? ProtocolEvidence { get; }

        private static string Required(string value, string parameterName)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                ThrowMissingText(parameterName);
            }
            return value;
        }

        private static void ValidateNonNegative(double value, string parameterName)
        {
            if (!IsFinite(value) || value < 0d)
            {
                ThrowNegativeOrNonFinite(parameterName);
            }
        }

        private static void ValidateRange(int value, int minimum, int maximum, string parameterName)
        {
            if (value < minimum || value > maximum)
            {
                throw new ArgumentOutOfRangeException(
                    parameterName,
                    "Value must be between " + minimum + " and " + maximum + ".");
            }
        }

        [DoesNotReturn]
        private static void ThrowInvalidFixture(string parameterName)
        {
            throw new ArgumentOutOfRangeException(parameterName, "Fixture identity must be positive.");
        }

        [DoesNotReturn]
        private static void ThrowMissingText(string parameterName)
        {
            throw new ArgumentException("A nonempty value is required.", parameterName);
        }

        [DoesNotReturn]
        private static void ThrowNegativeOrNonFinite(string parameterName)
        {
            throw new ArgumentOutOfRangeException(parameterName, "Value must be finite and non-negative.");
        }

        [DoesNotReturn]
        private static void ThrowNegativeCount(string parameterName)
        {
            throw new ArgumentOutOfRangeException(parameterName, "Count must be non-negative.");
        }

        [DoesNotReturn]
        private static void ThrowInvalidConservationCount(string parameterName)
        {
            throw new ArgumentOutOfRangeException(
                parameterName,
                "Conservation count must be non-negative and no greater than the physical transition count.");
        }

        [DoesNotReturn]
        private static void ThrowNullLayers(string parameterName)
        {
            throw new ArgumentNullException(parameterName);
        }

        [DoesNotReturn]
        private static void ThrowNegativeLayer(string parameterName)
        {
            throw new ArgumentOutOfRangeException(parameterName, "Layer indices must be non-negative.");
        }

        [DoesNotReturn]
        private static void ThrowLayerOutsideFixture(string parameterName)
        {
            throw new ArgumentOutOfRangeException(
                parameterName,
                "Layer indices must be within the recorded fixture layer count.");
        }

        [DoesNotReturn]
        private static void ThrowMismatchedProtocolEvidence()
        {
            throw new ArgumentException(
                "Protocol evidence must identify the same fixture and ammunition as the campaign shot.",
                "protocolEvidence");
        }

        private static bool IsFinite(double value)
        {
            return !double.IsNaN(value) && !double.IsInfinity(value);
        }
    }

    internal sealed class CampaignAttemptRecord
    {
        internal CampaignAttemptRecord(
            long attemptOrdinal,
            int caseIndex,
            string caseId,
            int repetitionIndex,
            int attemptIndex,
            ulong labCaseSeed,
            CampaignAttemptStatus status,
            CampaignShotEvidence evidence)
        {
            AttemptOrdinal = attemptOrdinal;
            CaseIndex = caseIndex;
            CaseId = caseId;
            RepetitionIndex = repetitionIndex;
            AttemptIndex = attemptIndex;
            LabCaseSeed = labCaseSeed;
            Status = status;
            Evidence = evidence;
        }

        internal long AttemptOrdinal { get; }
        internal int CaseIndex { get; }
        internal string CaseId { get; }
        internal int RepetitionIndex { get; }
        internal int AttemptIndex { get; }
        internal ulong LabCaseSeed { get; }
        internal CampaignAttemptStatus Status { get; }
        internal CampaignShotEvidence Evidence { get; }
    }

    internal sealed class CampaignRunSnapshot
    {
        internal CampaignRunSnapshot(
            string campaignId,
            string campaignName,
            ulong runSeed,
            CampaignRunState state,
            int currentCaseIndex,
            int currentRepetitionIndex,
            int currentAttemptIndex,
            long currentFixtureId,
            IReadOnlyList<CampaignAttemptRecord> attempts)
        {
            if (attempts == null)
            {
                ThrowNullAttempts(nameof(attempts));
            }
            CampaignId = campaignId;
            CampaignName = campaignName;
            RunSeed = runSeed;
            State = state;
            CurrentCaseIndex = currentCaseIndex;
            CurrentRepetitionIndex = currentRepetitionIndex;
            CurrentAttemptIndex = currentAttemptIndex;
            CurrentFixtureId = currentFixtureId;
            var copy = new CampaignAttemptRecord[attempts.Count];
            for (int index = 0; index < copy.Length; index++)
            {
                copy[index] = attempts[index];
            }
            Attempts = Array.AsReadOnly(copy);
        }

        internal string CampaignId { get; }
        internal string CampaignName { get; }
        internal ulong RunSeed { get; }
        internal CampaignRunState State { get; }
        internal int CurrentCaseIndex { get; }
        internal int CurrentRepetitionIndex { get; }
        internal int CurrentAttemptIndex { get; }
        internal long CurrentFixtureId { get; }
        internal IReadOnlyList<CampaignAttemptRecord> Attempts { get; }

        [DoesNotReturn]
        private static void ThrowNullAttempts(string parameterName)
        {
            throw new ArgumentNullException(parameterName);
        }
    }

    internal sealed class CampaignEvidenceRevisionTracker
    {
        private long _revision;
        private DateTime _lastUpdatedUtc;

        internal long Revision => _revision;
        internal DateTime LastUpdatedUtc => _lastUpdatedUtc;

        internal long Mark(DateTime updatedUtc)
        {
            _revision++;
            _lastUpdatedUtc = updatedUtc;
            return _revision;
        }

        internal void ResetTimestamp()
        {
            _lastUpdatedUtc = DateTime.MinValue;
        }
    }

    internal sealed class CampaignRunTracker
    {
        private readonly object _sync = new object();
        private readonly CampaignDefinition _definition;
        private readonly ulong _runSeed;
        private readonly List<CampaignAttemptRecord> _attempts = new List<CampaignAttemptRecord>();
        private readonly HashSet<string> _observedChains = new HashSet<string>(StringComparer.Ordinal);
        private CampaignRunState _state;
        private int _caseIndex;
        private int _repetitionIndex;
        private int _attemptIndex;
        private long _fixtureId;
        private long _attemptOrdinal;

        internal CampaignRunTracker(CampaignDefinition definition, ulong runSeed)
        {
            if (definition == null)
            {
                ThrowNullDefinition(nameof(definition));
            }
            _definition = definition;
            _runSeed = runSeed;
            _state = CampaignRunState.Idle;
        }

        internal CampaignDefinition Definition => _definition;

        internal bool Start()
        {
            lock (_sync)
            {
                if (_state != CampaignRunState.Idle)
                {
                    return false;
                }
                _state = CampaignRunState.AwaitingFixture;
                return true;
            }
        }

        internal bool Stop()
        {
            lock (_sync)
            {
                if (_state == CampaignRunState.Idle
                    || _state == CampaignRunState.Completed
                    || _state == CampaignRunState.Stopped)
                {
                    return false;
                }
                _state = CampaignRunState.Stopped;
                return true;
            }
        }

        internal bool AttachFixture(long fixtureId)
        {
            lock (_sync)
            {
                if (_state != CampaignRunState.AwaitingFixture || fixtureId <= 0L)
                {
                    return false;
                }
                _fixtureId = fixtureId;
                _state = CampaignRunState.AwaitingShot;
                return true;
            }
        }

        internal CampaignAttemptRecord? RecordShot(CampaignShotEvidence evidence)
        {
            if (evidence == null)
            {
                ThrowNullEvidence(nameof(evidence));
            }

            lock (_sync)
            {
                if (_state != CampaignRunState.AwaitingShot
                    || evidence.FixtureId != _fixtureId
                    || !_observedChains.Add(evidence.ChainId))
                {
                    return null;
                }

                CampaignCaseDefinition campaignCase = _definition.Cases[_caseIndex];
                CampaignAttemptStatus status = CampaignEvidenceEvaluator.Evaluate(campaignCase, evidence);
                var record = new CampaignAttemptRecord(
                    ++_attemptOrdinal,
                    _caseIndex,
                    campaignCase.CaseId,
                    _repetitionIndex,
                    _attemptIndex,
                    CampaignSeedDeriver.Derive(
                        _runSeed,
                        _definition.CampaignId,
                        campaignCase.CaseId,
                        _repetitionIndex),
                    status,
                    evidence);
                _attempts.Add(record);
                _attemptIndex++;

                if (status == CampaignAttemptStatus.Accepted)
                {
                    AcceptRepetition(campaignCase);
                }
                else if (campaignCase.ResetPolicy == CampaignResetPolicy.BeforeEachShot)
                {
                    _state = CampaignRunState.AwaitingReset;
                }
                return record;
            }
        }

        internal bool ConfirmReset(long fixtureId)
        {
            lock (_sync)
            {
                if (_state != CampaignRunState.AwaitingReset || fixtureId != _fixtureId)
                {
                    return false;
                }
                _state = CampaignRunState.AwaitingShot;
                return true;
            }
        }

        internal CampaignRunSnapshot Snapshot()
        {
            lock (_sync)
            {
                return new CampaignRunSnapshot(
                    _definition.CampaignId,
                    _definition.Name,
                    _runSeed,
                    _state,
                    _caseIndex,
                    _repetitionIndex,
                    _attemptIndex,
                    _fixtureId,
                    new ReadOnlyCollection<CampaignAttemptRecord>(_attempts.ToArray()));
            }
        }

        private void AcceptRepetition(CampaignCaseDefinition campaignCase)
        {
            _repetitionIndex++;
            _attemptIndex = 0;
            if (_repetitionIndex < campaignCase.RequiredRepetitions)
            {
                _state = campaignCase.ResetPolicy == CampaignResetPolicy.BeforeEachShot
                    ? CampaignRunState.AwaitingReset
                    : CampaignRunState.AwaitingShot;
                return;
            }

            _caseIndex++;
            _repetitionIndex = 0;
            _fixtureId = 0L;
            _state = _caseIndex >= _definition.Cases.Count
                ? CampaignRunState.Completed
                : CampaignRunState.AwaitingFixture;
        }

        [DoesNotReturn]
        private static void ThrowNullDefinition(string parameterName)
        {
            throw new ArgumentNullException(parameterName);
        }

        [DoesNotReturn]
        private static void ThrowNullEvidence(string parameterName)
        {
            throw new ArgumentNullException(parameterName);
        }
    }

    internal static class CampaignEvidenceEvaluator
    {
        internal static CampaignAttemptStatus Evaluate(
            CampaignCaseDefinition campaignCase,
            CampaignShotEvidence evidence)
        {
            if (campaignCase == null)
            {
                ThrowNullCampaignCase(nameof(campaignCase));
            }
            if (evidence == null)
            {
                ThrowNullEvidence(nameof(evidence));
            }
            bool selectorMatches = campaignCase.SelectorKind == CampaignFixtureSelectorKind.ExactTemplate
                ? string.Equals(
                    evidence.FixtureTemplateId,
                    campaignCase.TemplateId,
                    StringComparison.Ordinal)
                : campaignCase.SelectorKind == CampaignFixtureSelectorKind.MaterialAndArmorClass
                    && string.Equals(
                        evidence.FixtureArmorMaterial,
                        campaignCase.Material,
                        StringComparison.Ordinal)
                    && evidence.FixtureArmorClass == campaignCase.ArmorClass;
            if (!selectorMatches || evidence.FixtureLayerCount != campaignCase.LayerCount)
            {
                return CampaignAttemptStatus.FixtureMismatch;
            }
            if (evidence.VelocityFraction < campaignCase.MinimumVelocityFraction
                || evidence.VelocityFraction > campaignCase.MaximumVelocityFraction)
            {
                return CampaignAttemptStatus.VelocityOutOfRange;
            }
            for (int layer = 0; layer < campaignCase.LayerCount; layer++)
            {
                if (!evidence.HitLayers.Contains(layer))
                {
                    return CampaignAttemptStatus.IncompleteLayerChain;
                }
            }
            if (campaignCase.RequireBackstopEvidence && !evidence.ReachedBackstop)
            {
                return CampaignAttemptStatus.MissingBackstop;
            }
            if (campaignCase.RequirePhysicalEvidence && evidence.PhysicalTransitionCount == 0)
            {
                return CampaignAttemptStatus.MissingPhysicalEvidence;
            }
            if (campaignCase.RequireConservationEvidence
                && evidence.ConservationRecordCount != evidence.PhysicalTransitionCount)
            {
                return CampaignAttemptStatus.MissingConservationEvidence;
            }
            if (evidence.MaximumMassClosureErrorKilograms
                    > campaignCase.MaximumMassClosureErrorKilograms
                || evidence.MaximumEnergyClosureErrorJoules
                    > campaignCase.MaximumEnergyClosureErrorJoules)
            {
                return CampaignAttemptStatus.ConservationFailure;
            }
            return CampaignAttemptStatus.Accepted;
        }

        [DoesNotReturn]
        private static void ThrowNullCampaignCase(string parameterName)
        {
            throw new ArgumentNullException(parameterName);
        }

        [DoesNotReturn]
        private static void ThrowNullEvidence(string parameterName)
        {
            throw new ArgumentNullException(parameterName);
        }
    }

    internal static class CampaignSeedDeriver
    {
        private const ulong OffsetBasis = 14695981039346656037UL;
        private const ulong Prime = 1099511628211UL;

        internal static ulong Derive(
            ulong runSeed,
            string campaignId,
            string caseId,
            int repetitionIndex)
        {
            if (string.IsNullOrWhiteSpace(campaignId))
            {
                ThrowMissingText(nameof(campaignId));
            }
            if (string.IsNullOrWhiteSpace(caseId))
            {
                ThrowMissingText(nameof(caseId));
            }
            if (repetitionIndex < 0)
            {
                ThrowNegativeRepetition(nameof(repetitionIndex));
            }

            ulong hash = OffsetBasis;
            for (int shift = 0; shift < 64; shift += 8)
            {
                AddByte(ref hash, (byte)((runSeed >> shift) & byte.MaxValue));
            }
            AddText(ref hash, campaignId);
            AddByte(ref hash, byte.MaxValue);
            AddText(ref hash, caseId);
            AddByte(ref hash, byte.MaxValue);
            uint repetition = (uint)repetitionIndex;
            for (int shift = 0; shift < 32; shift += 8)
            {
                AddByte(ref hash, (byte)((repetition >> shift) & byte.MaxValue));
            }

            unchecked
            {
                hash += 0x9E3779B97F4A7C15UL;
                hash = (hash ^ (hash >> 30)) * 0xBF58476D1CE4E5B9UL;
                hash = (hash ^ (hash >> 27)) * 0x94D049BB133111EBUL;
            }
            hash ^= hash >> 31;
            return hash == 0UL ? 0xA0761D6478BD642FUL : hash;
        }

        private static void AddText(ref ulong hash, string value)
        {
            byte[] bytes = Encoding.UTF8.GetBytes(value);
            for (int index = 0; index < bytes.Length; index++)
            {
                AddByte(ref hash, bytes[index]);
            }
        }

        private static void AddByte(ref ulong hash, byte value)
        {
            unchecked
            {
                hash ^= value;
                hash *= Prime;
            }
        }

        [DoesNotReturn]
        private static void ThrowMissingText(string parameterName)
        {
            throw new ArgumentException("A nonempty value is required.", parameterName);
        }

        [DoesNotReturn]
        private static void ThrowNegativeRepetition(string parameterName)
        {
            throw new ArgumentOutOfRangeException(parameterName, "Repetition index must be non-negative.");
        }
    }

    internal sealed class CampaignCaseMatrixRow
    {
        internal CampaignCaseMatrixRow(
            int caseIndex,
            string caseId,
            string label,
            int requiredRepetitions,
            int attemptCount,
            int acceptedCount,
            int fixtureRejectCount,
            int velocityRejectCount,
            int layerRejectCount,
            int backstopRejectCount,
            int physicalRejectCount,
            int conservationRejectCount,
            int missingConservationRejectCount,
            double meanVelocityFraction,
            double meanLayersHit,
            double maximumMassClosureErrorKilograms,
            double maximumEnergyClosureErrorJoules)
        {
            CaseIndex = caseIndex;
            CaseId = caseId;
            Label = label;
            RequiredRepetitions = requiredRepetitions;
            AttemptCount = attemptCount;
            AcceptedCount = acceptedCount;
            FixtureRejectCount = fixtureRejectCount;
            VelocityRejectCount = velocityRejectCount;
            LayerRejectCount = layerRejectCount;
            BackstopRejectCount = backstopRejectCount;
            PhysicalRejectCount = physicalRejectCount;
            ConservationRejectCount = conservationRejectCount;
            MissingConservationRejectCount = missingConservationRejectCount;
            MeanVelocityFraction = meanVelocityFraction;
            MeanLayersHit = meanLayersHit;
            MaximumMassClosureErrorKilograms = maximumMassClosureErrorKilograms;
            MaximumEnergyClosureErrorJoules = maximumEnergyClosureErrorJoules;
        }

        internal int CaseIndex { get; }
        internal string CaseId { get; }
        internal string Label { get; }
        internal int RequiredRepetitions { get; }
        internal int AttemptCount { get; }
        internal int AcceptedCount { get; }
        internal int RejectedCount => AttemptCount - AcceptedCount;
        internal int FixtureRejectCount { get; }
        internal int VelocityRejectCount { get; }
        internal int LayerRejectCount { get; }
        internal int BackstopRejectCount { get; }
        internal int PhysicalRejectCount { get; }
        internal int ConservationRejectCount { get; }
        internal int MissingConservationRejectCount { get; }
        internal double MeanVelocityFraction { get; }
        internal double MeanLayersHit { get; }
        internal double MaximumMassClosureErrorKilograms { get; }
        internal double MaximumEnergyClosureErrorJoules { get; }
        internal bool Complete => AcceptedCount >= RequiredRepetitions;
    }

    internal sealed class CampaignResultMatrix
    {
        internal CampaignResultMatrix(
            string campaignId,
            string campaignName,
            ulong runSeed,
            CampaignRunState state,
            IReadOnlyList<CampaignCaseMatrixRow> rows)
        {
            CampaignId = campaignId;
            CampaignName = campaignName;
            RunSeed = runSeed;
            State = state;
            Rows = rows;
        }

        internal string CampaignId { get; }
        internal string CampaignName { get; }
        internal ulong RunSeed { get; }
        internal CampaignRunState State { get; }
        internal IReadOnlyList<CampaignCaseMatrixRow> Rows { get; }
        internal int AttemptCount => Rows.Sum(row => row.AttemptCount);
        internal int AcceptedCount => Rows.Sum(row => row.AcceptedCount);
        internal bool Complete => Rows.All(row => row.Complete);
    }

    internal static class CampaignResultMatrixBuilder
    {
        internal static CampaignResultMatrix Build(
            CampaignDefinition definition,
            CampaignRunSnapshot snapshot)
        {
            if (definition == null)
            {
                ThrowNullDefinition(nameof(definition));
            }
            if (snapshot == null)
            {
                ThrowNullSnapshot(nameof(snapshot));
            }
            if (!string.Equals(
                    definition.CampaignId,
                    snapshot.CampaignId,
                    StringComparison.Ordinal))
            {
                ThrowMismatchedCampaign(nameof(snapshot));
            }

            var rows = new CampaignCaseMatrixRow[definition.Cases.Count];
            for (int caseIndex = 0; caseIndex < definition.Cases.Count; caseIndex++)
            {
                CampaignCaseDefinition campaignCase = definition.Cases[caseIndex];
                CampaignAttemptRecord[] attempts = snapshot.Attempts
                    .Where(attempt => attempt.CaseIndex == caseIndex)
                    .ToArray();
                rows[caseIndex] = new CampaignCaseMatrixRow(
                    caseIndex,
                    campaignCase.CaseId,
                    campaignCase.Label,
                    campaignCase.RequiredRepetitions,
                    attempts.Length,
                    Count(attempts, CampaignAttemptStatus.Accepted),
                    Count(attempts, CampaignAttemptStatus.FixtureMismatch),
                    Count(attempts, CampaignAttemptStatus.VelocityOutOfRange),
                    Count(attempts, CampaignAttemptStatus.IncompleteLayerChain),
                    Count(attempts, CampaignAttemptStatus.MissingBackstop),
                    Count(attempts, CampaignAttemptStatus.MissingPhysicalEvidence),
                    Count(attempts, CampaignAttemptStatus.ConservationFailure),
                    Count(attempts, CampaignAttemptStatus.MissingConservationEvidence),
                    Mean(attempts, attempt => attempt.Evidence.VelocityFraction),
                    Mean(attempts, attempt => attempt.Evidence.HitLayers.Count),
                    Maximum(
                        attempts,
                        attempt => attempt.Evidence.MaximumMassClosureErrorKilograms),
                    Maximum(
                        attempts,
                        attempt => attempt.Evidence.MaximumEnergyClosureErrorJoules));
            }

            return new CampaignResultMatrix(
                definition.CampaignId,
                definition.Name,
                snapshot.RunSeed,
                snapshot.State,
                Array.AsReadOnly(rows));
        }

        private static int Count(
            CampaignAttemptRecord[] attempts,
            CampaignAttemptStatus status)
        {
            int count = 0;
            for (int index = 0; index < attempts.Length; index++)
            {
                if (attempts[index].Status == status)
                {
                    count++;
                }
            }
            return count;
        }

        private static double Mean(
            CampaignAttemptRecord[] attempts,
            Func<CampaignAttemptRecord, double> selector)
        {
            if (attempts.Length == 0)
            {
                return 0d;
            }
            double total = 0d;
            for (int index = 0; index < attempts.Length; index++)
            {
                total += selector(attempts[index]);
            }
            return total / attempts.Length;
        }

        private static double Maximum(
            CampaignAttemptRecord[] attempts,
            Func<CampaignAttemptRecord, double> selector)
        {
            double maximum = 0d;
            for (int index = 0; index < attempts.Length; index++)
            {
                maximum = Math.Max(maximum, selector(attempts[index]));
            }
            return maximum;
        }

        [DoesNotReturn]
        private static void ThrowNullDefinition(string parameterName)
        {
            throw new ArgumentNullException(parameterName);
        }

        [DoesNotReturn]
        private static void ThrowNullSnapshot(string parameterName)
        {
            throw new ArgumentNullException(parameterName);
        }

        [DoesNotReturn]
        private static void ThrowMismatchedCampaign(string parameterName)
        {
            throw new ArgumentException("Snapshot belongs to a different campaign.", parameterName);
        }
    }
}

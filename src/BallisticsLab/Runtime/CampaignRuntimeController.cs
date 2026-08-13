using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Diagnostics.CodeAnalysis;
using BallisticsLab.Core;
using BallisticsLab.Runtime.Telemetry;

namespace BallisticsLab.Runtime
{
    internal static class CampaignRuntimeController
    {
        private const string FixturePlate = "FIXTURE PLATE";
        private const string FixtureBackstop = "FIXTURE BACKSTOP";
        private static readonly TimeSpan EvidenceQuietPeriod = TimeSpan.FromSeconds(0.35d);
        private static readonly object Sync = new object();
        private static readonly CampaignEvidenceRevisionTracker EvidenceRevision =
            new CampaignEvidenceRevisionTracker();

        private static CampaignDefinition? _definition;
        private static CampaignRunTracker? _tracker;
        private static CampaignAttemptRecord? _latestAttempt;
        private static readonly HashSet<string> IgnoredChainIds =
            new HashSet<string>(StringComparer.Ordinal);
        private static readonly List<string> QueuedProtocolChainIds = new List<string>();
        private static readonly Dictionary<string, DateTime> QueuedProtocolChainTimes =
            new Dictionary<string, DateTime>(StringComparer.Ordinal);
        private static string _pendingChainId = string.Empty;
        private static DateTime _pendingLastObservedUtc;
        private static string _lastFailure = string.Empty;
        private static bool _fixtureRestartRequested;

        internal static bool HasCampaign
        {
            get
            {
                lock (Sync)
                {
                    return _tracker != null;
                }
            }
        }

        internal static bool IsRunning
        {
            get
            {
                lock (Sync)
                {
                    CampaignRunState state = _tracker?.Snapshot().State ?? CampaignRunState.Idle;
                    return IsRunningState(state);
                }
            }
        }

        internal static bool Start(CampaignDefinition definition, ulong runSeed)
        {
            if (definition == null)
            {
                throw new ArgumentNullException(nameof(definition));
            }

            lock (Sync)
            {
                if (_tracker != null)
                {
                    return false;
                }

                var tracker = new CampaignRunTracker(definition, runSeed);
                if (!tracker.Start())
                {
                    return false;
                }
                _definition = definition;
                _tracker = tracker;
                _latestAttempt = null;
                _pendingChainId = string.Empty;
                _pendingLastObservedUtc = DateTime.MinValue;
                _lastFailure = string.Empty;
                _fixtureRestartRequested = false;
                IgnoredChainIds.Clear();
                ClearQueuedProtocolChains();
                EvidenceRevision.ResetTimestamp();
                return true;
            }
        }

        internal static bool Stop()
        {
            lock (Sync)
            {
                if (_tracker == null || !_tracker.Stop())
                {
                    return false;
                }
                _pendingChainId = string.Empty;
                _pendingLastObservedUtc = DateTime.MinValue;
                _fixtureRestartRequested = false;
                ClearQueuedProtocolChains();
                if (_tracker.Snapshot().Attempts.Count != 0)
                {
                    MarkEvidenceChanged();
                }
                return true;
            }
        }

        internal static void Clear()
        {
            lock (Sync)
            {
                _definition = null;
                _tracker = null;
                _latestAttempt = null;
                _pendingChainId = string.Empty;
                _pendingLastObservedUtc = DateTime.MinValue;
                _lastFailure = string.Empty;
                _fixtureRestartRequested = false;
                IgnoredChainIds.Clear();
                ClearQueuedProtocolChains();
                EvidenceRevision.ResetTimestamp();
            }
        }

        internal static bool AttachFixture(long fixtureId)
        {
            lock (Sync)
            {
                if (_tracker == null || !_tracker.AttachFixture(fixtureId))
                {
                    return false;
                }
                _pendingChainId = string.Empty;
                _pendingLastObservedUtc = DateTime.MinValue;
                _lastFailure = string.Empty;
                return true;
            }
        }

        internal static bool ConfirmReset(long fixtureId)
        {
            lock (Sync)
            {
                if (_tracker == null || !_tracker.ConfirmReset(fixtureId))
                {
                    return false;
                }
                return true;
            }
        }

        internal static void Observe(ShotRecord record)
        {
            if (record == null || record.FixtureId <= 0L || string.IsNullOrEmpty(record.ChainId))
            {
                return;
            }

            lock (Sync)
            {
                if (_tracker == null)
                {
                    return;
                }
                CampaignRunSnapshot snapshot = _tracker.Snapshot();
                if (snapshot.State != CampaignRunState.AwaitingShot
                    || record.FixtureId != snapshot.CurrentFixtureId)
                {
                    return;
                }

                if (string.IsNullOrEmpty(_pendingChainId))
                {
                    _pendingChainId = record.ChainId;
                }
                else if (!string.Equals(_pendingChainId, record.ChainId, StringComparison.Ordinal))
                {
                    if (CurrentCaseIsProtocolSequence(_tracker, _definition))
                    {
                        QueueProtocolChain(record.ChainId, DateTime.UtcNow);
                        return;
                    }
                    IgnoredChainIds.Add(record.ChainId);
                    return;
                }
                _pendingLastObservedUtc = DateTime.UtcNow;
            }
        }

        internal static bool TryFinalizePending(
            DateTime utcNow,
            out CampaignAttemptRecord? attempt)
        {
            attempt = null;
            string chainId;
            long fixtureId;
            DateTime pendingLastObservedUtc;
            CampaignRunTracker tracker;
            CampaignCaseDefinition campaignCase;
            lock (Sync)
            {
                if (_tracker == null
                    || string.IsNullOrEmpty(_pendingChainId)
                    || utcNow - _pendingLastObservedUtc < EvidenceQuietPeriod)
                {
                    return false;
                }
                tracker = _tracker;
                CampaignRunSnapshot snapshot = tracker.Snapshot();
                if (snapshot.State != CampaignRunState.AwaitingShot
                    || _definition == null
                    || snapshot.CurrentCaseIndex < 0
                    || snapshot.CurrentCaseIndex >= _definition.Cases.Count)
                {
                    return false;
                }
                chainId = _pendingChainId;
                fixtureId = snapshot.CurrentFixtureId;
                pendingLastObservedUtc = _pendingLastObservedUtc;
                campaignCase = _definition.Cases[snapshot.CurrentCaseIndex];
            }

            IReadOnlyList<PhysicalTransitionRecord> transitions =
                PhysicalTelemetrySessionBridge.SnapshotTransitions(out _, out DateTime physicalUpdatedUtc);
            if (physicalUpdatedUtc > pendingLastObservedUtc
                && utcNow - physicalUpdatedUtc < EvidenceQuietPeriod)
            {
                return false;
            }

            IReadOnlyList<ShotRecord> records = TelemetryStore.SnapshotChain(chainId);
            if (!TryBuildEvidence(
                    records,
                    transitions,
                    fixtureId,
                    chainId,
                    campaignCase,
                    out CampaignShotEvidence? evidence))
            {
                lock (Sync)
                {
                    _lastFailure = "The pending chain did not contain valid fixture evidence.";
                    if (ReferenceEquals(_tracker, tracker)
                        && tracker.InvalidateCurrentProtocolSample())
                    {
                        _fixtureRestartRequested = true;
                        PromoteOrDiscardQueuedProtocolChains(tracker);
                        MarkEvidenceChanged();
                    }
                    _pendingChainId = string.Empty;
                    _pendingLastObservedUtc = DateTime.MinValue;
                }
                return false;
            }

            lock (Sync)
            {
                if (!ReferenceEquals(_tracker, tracker)
                    || !string.Equals(_pendingChainId, chainId, StringComparison.Ordinal))
                {
                    return false;
                }
                attempt = tracker.RecordShot(
                    evidence,
                    deferProtocolCompletion: QueuedProtocolChainIds.Count != 0);
                _pendingChainId = string.Empty;
                _pendingLastObservedUtc = DateTime.MinValue;
                if (attempt == null)
                {
                    _lastFailure = "The campaign rejected a stale or duplicate shot chain.";
                    return false;
                }
                _latestAttempt = attempt;
                _lastFailure = string.Empty;
                PromoteOrDiscardQueuedProtocolChains(tracker);
                MarkEvidenceChanged();
                return true;
            }
        }

        internal static bool TryGetCurrentCase(out CampaignCaseDefinition? campaignCase)
        {
            lock (Sync)
            {
                if (_tracker == null || _definition == null)
                {
                    campaignCase = null;
                    return false;
                }
                CampaignRunSnapshot snapshot = _tracker.Snapshot();
                if (snapshot.CurrentCaseIndex < 0
                    || snapshot.CurrentCaseIndex >= _definition.Cases.Count
                    || snapshot.State == CampaignRunState.Completed
                    || snapshot.State == CampaignRunState.Stopped)
                {
                    campaignCase = null;
                    return false;
                }
                campaignCase = _definition.Cases[snapshot.CurrentCaseIndex];
                return true;
            }
        }

        internal static bool TryConsumeFixtureRestartRequest(out string reason)
        {
            lock (Sync)
            {
                if (!_fixtureRestartRequested)
                {
                    reason = string.Empty;
                    return false;
                }
                _fixtureRestartRequested = false;
                reason = _lastFailure;
                return true;
            }
        }

        internal static bool TryGetExpectedImpactPoint(
            double faceWidthMetres,
            double faceHeightMetres,
            out ProtocolImpactPoint point,
            out double projectileDiameterMetres,
            out int shotNumber,
            out int requiredShots,
            out string failure)
        {
            lock (Sync)
            {
                point = default;
                projectileDiameterMetres = 0d;
                shotNumber = 0;
                requiredShots = 0;
                failure = string.Empty;
                if (_tracker == null || _definition == null)
                {
                    failure = "No guided campaign is loaded.";
                    return false;
                }
                CampaignRunSnapshot snapshot = _tracker.Snapshot();
                if (snapshot.CurrentCaseIndex < 0
                    || snapshot.CurrentCaseIndex >= _definition.Cases.Count)
                {
                    failure = "The campaign has no current case.";
                    return false;
                }
                CampaignCaseDefinition campaignCase = _definition.Cases[snapshot.CurrentCaseIndex];
                if (!campaignCase.IsProtocolSequence)
                {
                    return false;
                }
                if (!campaignCase.TryGetProtocolDefinition(
                        out ProtocolThreatDefinition? threat,
                        out ProtocolAmmunitionMapping? mapping)
                    || threat == null
                    || mapping == null
                    || !ProtocolImpactPattern.TryCreate(
                        threat,
                        mapping,
                        faceWidthMetres,
                        faceHeightMetres,
                        out ProtocolImpactPattern? pattern,
                        out failure)
                    || pattern == null
                    || snapshot.CurrentRepetitionIndex < 0
                    || snapshot.CurrentRepetitionIndex >= pattern.Points.Count)
                {
                    if (string.IsNullOrEmpty(failure))
                    {
                        failure = "The next protocol impact point could not be resolved.";
                    }
                    return false;
                }
                point = pattern.Points[snapshot.CurrentRepetitionIndex];
                projectileDiameterMetres = pattern.ProjectileDiameterMetres;
                shotNumber = snapshot.CurrentRepetitionIndex + 1;
                requiredShots = threat.RequiredQualifyingShots;
                return true;
            }
        }

        internal static bool TrySnapshot(
            out CampaignDefinition? definition,
            out CampaignRunSnapshot? snapshot,
            out long revision,
            out DateTime lastUpdatedUtc)
        {
            lock (Sync)
            {
                definition = _definition;
                snapshot = _tracker?.Snapshot();
                revision = EvidenceRevision.Revision;
                lastUpdatedUtc = EvidenceRevision.LastUpdatedUtc;
                return definition != null && snapshot != null;
            }
        }

        internal static string Describe()
        {
            lock (Sync)
            {
                if (_tracker == null || _definition == null)
                {
                    return "No guided campaign is loaded.";
                }

                CampaignRunSnapshot snapshot = _tracker.Snapshot();
                CampaignResultMatrix matrix = CampaignResultMatrixBuilder.Build(_definition, snapshot);
                string progress = matrix.AcceptedCount.ToString(CultureInfo.InvariantCulture)
                    + "/" + _definition.RequiredRepetitions.ToString(CultureInfo.InvariantCulture)
                    + " accepted";
                string position = snapshot.CurrentCaseIndex < _definition.Cases.Count
                    ? "case " + (snapshot.CurrentCaseIndex + 1).ToString(CultureInfo.InvariantCulture)
                        + "/" + _definition.Cases.Count.ToString(CultureInfo.InvariantCulture)
                        + " - " + _definition.Cases[snapshot.CurrentCaseIndex].Label
                    : "all cases processed";
                string latest = _latestAttempt == null
                    ? string.Empty
                    : " | last " + _latestAttempt.Status
                        + (_latestAttempt.ProtocolQualificationReason.HasValue
                            ? " (" + _latestAttempt.ProtocolQualificationReason.Value + ")"
                            : string.Empty);
                CampaignCaseDefinition? currentCase = snapshot.CurrentCaseIndex >= 0
                    && snapshot.CurrentCaseIndex < _definition.Cases.Count
                        ? _definition.Cases[snapshot.CurrentCaseIndex]
                        : null;
                string sequence = currentCase?.IsProtocolSequence == true
                    ? " | sample shot "
                        + (snapshot.CurrentRepetitionIndex + 1).ToString(CultureInfo.InvariantCulture)
                        + "/" + currentCase.RequiredRepetitions.ToString(CultureInfo.InvariantCulture)
                    : string.Empty;
                string ignored = IgnoredChainIds.Count == 0
                    ? string.Empty
                    : " | " + IgnoredChainIds.Count.ToString(CultureInfo.InvariantCulture)
                        + " rapid extra chain(s) ignored";
                string queued = QueuedProtocolChainIds.Count == 0
                    ? string.Empty
                    : " | " + QueuedProtocolChainIds.Count.ToString(CultureInfo.InvariantCulture)
                        + " rapid protocol chain(s) queued";
                string failure = string.IsNullOrEmpty(_lastFailure)
                    ? string.Empty
                    : " | " + _lastFailure;
                return _definition.Name + " | " + snapshot.State + " | " + position
                    + " | " + progress + sequence + latest + queued + ignored + failure;
            }
        }

        private static bool TryBuildEvidence(
            IReadOnlyList<ShotRecord> records,
            IReadOnlyList<PhysicalTransitionRecord> transitions,
            long fixtureId,
            string chainId,
            CampaignCaseDefinition campaignCase,
            [NotNullWhen(true)] out CampaignShotEvidence? evidence)
        {
            ShotRecord[] fixtureRecords = records
                .Where(record => record.FixtureId == fixtureId
                    && (string.Equals(record.TargetKind, FixturePlate, StringComparison.Ordinal)
                        || string.Equals(record.TargetKind, FixtureBackstop, StringComparison.Ordinal)))
                .OrderBy(record => record.Sequence)
                .ToArray();
            ShotRecord[] plateRecords = fixtureRecords
                .Where(record => string.Equals(record.TargetKind, FixturePlate, StringComparison.Ordinal))
                .ToArray();
            ShotRecord? identity = plateRecords.FirstOrDefault();
            if (identity == null
                || string.IsNullOrWhiteSpace(identity.AmmoTemplateId)
                || string.IsNullOrWhiteSpace(identity.FixtureTemplateId)
                || string.IsNullOrWhiteSpace(identity.FixtureArmorMaterial)
                || identity.FixtureArmorClass <= 0
                || identity.FixtureLayerCount <= 0
                || plateRecords.Any(record => !HasSameFixtureIdentity(record, identity))
                || !IsFiniteNonNegative(identity.Fraction)
                || !IsFiniteNonNegative(identity.ImpactSpeed)
                || !IsFinitePositive(identity.ProjectileMassKilograms)
                || !IsFinitePositive(identity.ProjectileDiameterMetres)
                || !identity.HasFixtureFacePoint
                || !IsFiniteNonNegative(identity.ImpactAngle)
                || identity.ImpactAngle > 90f
                || !IsFinitePositive(identity.FixtureFaceWidth)
                || !IsFinitePositive(identity.FixtureFaceHeight)
                || !IsFinitePositive(identity.ImpactDistanceMetres))
            {
                evidence = null;
                return false;
            }

            int[] hitLayers = plateRecords
                .Where(record => record.LayerIndex >= 0)
                .Select(record => record.LayerIndex)
                .Distinct()
                .OrderBy(layer => layer)
                .ToArray();
            bool reachedBackstop = fixtureRecords.Any(
                record => string.Equals(record.TargetKind, FixtureBackstop, StringComparison.Ordinal));
            ShotRecord terminal = fixtureRecords[fixtureRecords.Length - 1];
            CampaignPhysicalEvidenceSummary physical = CampaignPhysicalEvidenceCalculator.Calculate(
                transitions,
                identity.RootFireIndex,
                identity.RootRandomSeed,
                identity.AmmoTemplateId,
                identity.RootShooterProfileId);
            ProtocolVelocityMeasurementBasis velocityBasis = identity.HasThreeMetreVelocity
                ? ProtocolVelocityMeasurementBasis.EftTrajectoryThreeMetres
                : ProtocolVelocityMeasurementBasis.TargetImpactProxy;
            double protocolVelocity = identity.HasThreeMetreVelocity
                ? identity.ThreeMetreVelocity
                : identity.ImpactSpeed;
            var protocolEvidence = new ProtocolShotEvidence(
                fixtureId,
                identity.AmmoTemplateId,
                velocityBasis,
                protocolVelocity,
                identity.ProjectileMassKilograms,
                identity.ProjectileDiameterMetres,
                identity.ImpactAngle,
                identity.FixtureLocalHitX,
                identity.FixtureLocalHitY,
                identity.FixtureFaceWidth,
                identity.FixtureFaceHeight,
                identity.ImpactDistanceMetres,
                campaignCase.BackstopEnabled,
                reachedBackstop,
                identity.ImpactSpeed);
            evidence = new CampaignShotEvidence(
                fixtureId,
                chainId,
                identity.FixtureTemplateId,
                identity.FixtureArmorMaterial,
                identity.FixtureArmorClass,
                identity.FixtureLayerCount,
                identity.RootFireIndex,
                identity.RootRandomSeed,
                identity.RootShooterProfileId,
                identity.AmmoTemplateId,
                identity.Fraction,
                hitLayers,
                terminal.Outcome,
                reachedBackstop,
                physical.TransitionCount,
                physical.ConservationRecordCount,
                physical.MaximumMassClosureErrorKilograms,
                physical.MaximumEnergyClosureErrorJoules,
                protocolEvidence);
            return true;
        }

        private static bool HasSameFixtureIdentity(ShotRecord record, ShotRecord identity)
        {
            return string.Equals(
                    record.FixtureTemplateId,
                    identity.FixtureTemplateId,
                    StringComparison.Ordinal)
                && string.Equals(
                    record.FixtureArmorMaterial,
                    identity.FixtureArmorMaterial,
                    StringComparison.Ordinal)
                && record.FixtureArmorClass == identity.FixtureArmorClass
                && record.FixtureLayerCount == identity.FixtureLayerCount;
        }

        private static bool IsRunningState(CampaignRunState state)
        {
            return state == CampaignRunState.AwaitingFixture
                || state == CampaignRunState.AwaitingShot
                || state == CampaignRunState.AwaitingReset;
        }

        private static bool CurrentCaseIsProtocolSequence(
            CampaignRunTracker tracker,
            CampaignDefinition? definition)
        {
            if (definition == null)
            {
                return false;
            }
            CampaignRunSnapshot snapshot = tracker.Snapshot();
            return snapshot.CurrentCaseIndex >= 0
                && snapshot.CurrentCaseIndex < definition.Cases.Count
                && definition.Cases[snapshot.CurrentCaseIndex].IsProtocolSequence;
        }

        private static void QueueProtocolChain(string chainId, DateTime observedUtc)
        {
            if (!QueuedProtocolChainTimes.ContainsKey(chainId))
            {
                QueuedProtocolChainIds.Add(chainId);
            }
            QueuedProtocolChainTimes[chainId] = observedUtc;
        }

        private static void PromoteOrDiscardQueuedProtocolChains(CampaignRunTracker tracker)
        {
            if (QueuedProtocolChainIds.Count == 0)
            {
                return;
            }
            if (tracker.Snapshot().State == CampaignRunState.AwaitingShot)
            {
                string next = QueuedProtocolChainIds[0];
                QueuedProtocolChainIds.RemoveAt(0);
                _pendingChainId = next;
                _pendingLastObservedUtc = QueuedProtocolChainTimes[next];
                QueuedProtocolChainTimes.Remove(next);
                return;
            }
            for (int index = 0; index < QueuedProtocolChainIds.Count; index++)
            {
                IgnoredChainIds.Add(QueuedProtocolChainIds[index]);
            }
            ClearQueuedProtocolChains();
        }

        private static void ClearQueuedProtocolChains()
        {
            QueuedProtocolChainIds.Clear();
            QueuedProtocolChainTimes.Clear();
        }

        private static bool IsFiniteNonNegative(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value) && value >= 0f;
        }

        private static bool IsFinitePositive(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value) && value > 0f;
        }

        private static void MarkEvidenceChanged()
        {
            EvidenceRevision.Mark(DateTime.UtcNow);
        }
    }
}

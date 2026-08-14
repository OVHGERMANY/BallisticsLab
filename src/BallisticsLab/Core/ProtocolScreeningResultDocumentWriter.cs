using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Linq;
using System.Text;

namespace BallisticsLab.Core
{
    internal static class ProtocolScreeningResultDocumentWriter
    {
        internal static string BuildOrNull(
            CampaignDefinition? definition,
            CampaignRunSnapshot? snapshot)
        {
            var builder = new StringBuilder(1024);
            AppendOrNull(builder, definition, snapshot);
            return builder.ToString();
        }

        internal static void AppendOrNull(
            StringBuilder builder,
            CampaignDefinition? definition,
            CampaignRunSnapshot? snapshot)
        {
            if (builder == null)
            {
                ThrowNullBuilder(nameof(builder));
            }
            if ((definition == null) != (snapshot == null))
            {
                ThrowPartialCampaign();
            }
            if (definition == null || snapshot == null)
            {
                builder.Append("null");
                return;
            }
            if (!string.Equals(
                    definition.CampaignId,
                    snapshot.CampaignId,
                    StringComparison.Ordinal))
            {
                ThrowCampaignIdentityMismatch();
            }

            var protocolCases = definition.Cases
                .Select((campaignCase, index) => new IndexedCase(index, campaignCase))
                .Where(candidate => candidate.Case.IsProtocolSequence)
                .ToArray();
            if (protocolCases.Length == 0)
            {
                builder.Append("null");
                return;
            }

            builder.Append("{\"documentSchema\":1")
                .Append(",\"documentType\":")
                .Append(LabPolicies.Json("BallisticsLabProtocolScreening"))
                .Append(",\"pluginVersion\":")
                .Append(LabPolicies.Json(LabBuild.PluginVersion))
                .Append(",\"classificationStandard\":")
                .Append(LabPolicies.Json(GostProtocolCatalog.ClassificationStandard))
                .Append(",\"classificationSource\":")
                .Append(LabPolicies.Json(GostProtocolCatalog.ClassificationSource))
                .Append(",\"testMethodStandard\":")
                .Append(LabPolicies.Json(GostProtocolCatalog.TestMethodStandard))
                .Append(",\"testMethodSource\":")
                .Append(LabPolicies.Json(GostProtocolCatalog.TestMethodSource))
                .Append(",\"simulationScreeningOnly\":true")
                .Append(",\"certificationClaim\":false")
                .Append(",\"campaignId\":")
                .Append(LabPolicies.Json(definition.CampaignId))
                .Append(",\"runInstanceId\":")
                .Append(LabPolicies.Json(snapshot.RunInstanceId))
                .Append(",\"campaignState\":")
                .Append(LabPolicies.Json(snapshot.State.ToString()))
                .Append(",\"runSeed\":")
                .Append(snapshot.RunSeed.ToString(CultureInfo.InvariantCulture))
                .Append(",\"protocolCompletionDeferred\":")
                .Append(snapshot.ProtocolCompletionDeferred ? "true" : "false")
                .Append(",\"screenings\":[");
            for (int index = 0; index < protocolCases.Length; index++)
            {
                if (index > 0)
                {
                    builder.Append(',');
                }
                AppendScreening(builder, protocolCases[index], snapshot);
            }
            builder.Append("]}");
        }

        private static void AppendScreening(
            StringBuilder builder,
            IndexedCase indexedCase,
            CampaignRunSnapshot snapshot)
        {
            CampaignCaseDefinition campaignCase = indexedCase.Case;
            if (!campaignCase.TryGetProtocolDefinition(
                    out ProtocolThreatDefinition? threat,
                    out ProtocolAmmunitionMapping? mapping)
                || threat == null
                || mapping == null)
            {
                ThrowInvalidProtocolCase(campaignCase.CaseId);
            }

            CampaignAttemptRecord[] attempts = snapshot.Attempts
                .Where(attempt => attempt.CaseIndex == indexedCase.Index)
                .OrderBy(attempt => attempt.AttemptOrdinal)
                .ToArray();
            ProtocolShotEvidence[] acceptedEvidence = attempts
                .Where(attempt => attempt.Status == CampaignAttemptStatus.Accepted
                    && attempt.Evidence.ProtocolEvidence != null)
                .Select(attempt => attempt.Evidence.ProtocolEvidence!)
                .ToArray();
            ProtocolScreeningEvaluation evaluation = ProtocolScreeningEvaluator.Evaluate(
                threat,
                acceptedEvidence);
            IGrouping<int, CampaignAttemptRecord>[] sampleGroups = attempts
                .GroupBy(attempt => attempt.SampleOrdinal)
                .OrderBy(group => group.Key)
                .ToArray();
            int currentSampleOrdinal = ResolveCurrentSampleOrdinal(sampleGroups);
            int invalidatedSampleCount = sampleGroups.Count(IsInvalidatedSample);
            bool caseAdvanced = snapshot.CurrentCaseIndex > indexedCase.Index;
            bool complete = caseAdvanced
                && evaluation.Status == ProtocolScreeningStatus.SimulationScreeningComplete;
            bool latestSampleInvalidated = sampleGroups.Length != 0
                && IsInvalidatedSample(sampleGroups[sampleGroups.Length - 1]);
            string resultStatus = ResolveResultStatus(
                snapshot.State,
                acceptedEvidence.Length,
                threat.RequiredQualifyingShots,
                complete,
                latestSampleInvalidated);
            int currentThroughPenetrationCount = acceptedEvidence.Count(shot =>
                shot.ThroughPenetrationObserved);

            builder.Append("{\"caseIndex\":")
                .Append(indexedCase.Index.ToString(CultureInfo.InvariantCulture))
                .Append(",\"caseId\":")
                .Append(LabPolicies.Json(campaignCase.CaseId))
                .Append(",\"threatId\":")
                .Append(LabPolicies.Json(threat.ThreatId))
                .Append(",\"protectionClass\":")
                .Append(LabPolicies.Json(threat.ProtectionClass))
                .Append(",\"cartridgeDesignation\":")
                .Append(LabPolicies.Json(threat.CartridgeDesignation))
                .Append(",\"projectileConstruction\":")
                .Append(LabPolicies.Json(threat.ProjectileConstruction))
                .Append(",\"ammunitionTemplateId\":")
                .Append(LabPolicies.Json(mapping.TemplateId))
                .Append(",\"installedAmmunitionName\":")
                .Append(LabPolicies.Json(mapping.InstalledDisplayName))
                .Append(",\"installedAmmunitionInternalName\":")
                .Append(LabPolicies.Json(mapping.InstalledInternalName))
                .Append(",\"installedCaliber\":")
                .Append(LabPolicies.Json(mapping.InstalledCaliber))
                .Append(",\"identityStatus\":")
                .Append(LabPolicies.Json(mapping.IdentityStatus.ToString()))
                .Append(",\"fixtureMaterial\":")
                .Append(LabPolicies.Json(campaignCase.Material))
                .Append(",\"fixtureArmorClass\":")
                .Append(campaignCase.ArmorClass.ToString(CultureInfo.InvariantCulture))
                .Append(",\"fixtureLayerCount\":")
                .Append(campaignCase.LayerCount.ToString(CultureInfo.InvariantCulture))
                .Append(",\"fixtureLayerSpacingMetres\":")
                .Append(Number(campaignCase.LayerSpacingMetres))
                .Append(",\"fixturePlateThicknessMetres\":")
                .Append(Number(campaignCase.PlateThicknessMetres))
                .Append(",\"nominalProjectileMassKilograms\":")
                .Append(Number(threat.NominalProjectileMassKilograms))
                .Append(",\"installedProjectileMassKilograms\":")
                .Append(Number(mapping.InstalledProjectileMassKilograms))
                .Append(",\"installedProjectileDiameterMetres\":")
                .Append(Number(mapping.InstalledProjectileDiameterMetres))
                .Append(",\"localeProjectileMassKilograms\":")
                .Append(Number(mapping.LocaleProjectileMassKilograms))
                .Append(",\"installedInitialSpeedMetresPerSecond\":")
                .Append(Number(mapping.InstalledInitialSpeedMetresPerSecond))
                .Append(",\"installedMassMatchesNominal\":")
                .Append(mapping.InstalledMassMatchesNominal(
                    threat.NominalProjectileMassKilograms) ? "true" : "false")
                .Append(",\"installedMassMatchesLocale\":")
                .Append(mapping.InstalledMassMatchesLocale() ? "true" : "false")
                .Append(",\"minimumVelocityMetresPerSecond\":")
                .Append(Number(threat.MinimumVelocityMetresPerSecond))
                .Append(",\"maximumVelocityMetresPerSecond\":")
                .Append(Number(threat.MaximumVelocityMetresPerSecond))
                .Append(",\"testDistanceMetres\":")
                .Append(Number(threat.TestDistanceMetres))
                .Append(",\"testDistanceToleranceMetres\":")
                .Append(Number(threat.TestDistanceToleranceMetres))
                .Append(",\"velocityMeasurementDistanceMetres\":")
                .Append(Number(threat.VelocityMeasurementDistanceMetres))
                .Append(",\"maximumImpactAngleDegrees\":")
                .Append(Number(threat.MaximumImpactAngleDegrees))
                .Append(",\"minimumSeparationDiameters\":")
                .Append(Number(threat.MinimumSeparationDiameters))
                .Append(",\"evaluationStatus\":")
                .Append(LabPolicies.Json(evaluation.Status.ToString()))
                .Append(",\"resultStatus\":")
                .Append(LabPolicies.Json(resultStatus))
                .Append(",\"observedOutcome\":")
                .Append(LabPolicies.Json((complete
                    ? evaluation.ObservedOutcome
                    : ProtocolObservedOutcome.NotDetermined).ToString()))
                .Append(",\"requiredQualifyingShots\":")
                .Append(threat.RequiredQualifyingShots.ToString(CultureInfo.InvariantCulture))
                .Append(",\"qualifyingShotCount\":")
                .Append(evaluation.QualifyingShotCount.ToString(CultureInfo.InvariantCulture))
                .Append(",\"currentThroughPenetrationCount\":")
                .Append(currentThroughPenetrationCount.ToString(CultureInfo.InvariantCulture))
                .Append(",\"currentNoThroughPenetrationCount\":")
                .Append((acceptedEvidence.Length - currentThroughPenetrationCount).ToString(
                    CultureInfo.InvariantCulture))
                .Append(",\"minimumObservedProtocolVelocityMetresPerSecond\":")
                .Append(Number(MinimumOrZero(
                    acceptedEvidence,
                    shot => shot.ProtocolVelocityMetresPerSecond)))
                .Append(",\"maximumObservedProtocolVelocityMetresPerSecond\":")
                .Append(Number(MaximumOrZero(
                    acceptedEvidence,
                    shot => shot.ProtocolVelocityMetresPerSecond)))
                .Append(",\"meanObservedProtocolVelocityMetresPerSecond\":")
                .Append(Number(MeanOrZero(
                    acceptedEvidence,
                    shot => shot.ProtocolVelocityMetresPerSecond)))
                .Append(",\"meanObservedTargetImpactSpeedMetresPerSecond\":")
                .Append(Number(MeanOrZero(
                    acceptedEvidence,
                    shot => shot.TargetImpactSpeedMetresPerSecond)))
                .Append(",\"currentSampleOrdinal\":")
                .Append(currentSampleOrdinal.ToString(CultureInfo.InvariantCulture))
                .Append(",\"sampleCount\":")
                .Append(sampleGroups.Length.ToString(CultureInfo.InvariantCulture))
                .Append(",\"invalidatedSampleCount\":")
                .Append(invalidatedSampleCount.ToString(CultureInfo.InvariantCulture))
                .Append(",\"totalAttemptCount\":")
                .Append(attempts.Length.ToString(CultureInfo.InvariantCulture))
                .Append(",\"protocolRejectCount\":")
                .Append(attempts.Count(attempt => attempt.Status
                    == CampaignAttemptStatus.ProtocolRejected).ToString(
                        CultureInfo.InvariantCulture))
                .Append(",\"sequenceInvalidatedAttemptCount\":")
                .Append(attempts.Count(attempt => attempt.Status
                    == CampaignAttemptStatus.SequenceInvalidated).ToString(
                        CultureInfo.InvariantCulture))
                .Append(",\"complete\":")
                .Append(complete ? "true" : "false")
                .Append(",\"samples\":[");
            for (int index = 0; index < sampleGroups.Length; index++)
            {
                if (index > 0)
                {
                    builder.Append(',');
                }
                AppendSample(
                    builder,
                    sampleGroups[index],
                    threat.RequiredQualifyingShots,
                    caseAdvanced);
            }
            builder.Append("]}");
        }

        private static void AppendSample(
            StringBuilder builder,
            IGrouping<int, CampaignAttemptRecord> sample,
            int requiredQualifyingShots,
            bool caseAdvanced)
        {
            CampaignAttemptRecord[] attempts = sample
                .OrderBy(attempt => attempt.AttemptOrdinal)
                .ToArray();
            int qualifying = attempts.Count(attempt => attempt.ProtocolQualificationReason
                == ProtocolShotQualificationReason.Qualifying);
            int accepted = attempts.Count(attempt => attempt.Status
                == CampaignAttemptStatus.Accepted);
            int invalidated = attempts.Count(attempt => attempt.Status
                == CampaignAttemptStatus.SequenceInvalidated);
            int rejected = attempts.Length - accepted - invalidated;
            bool sampleInvalidated = invalidated != 0 || rejected != 0;
            string state = sampleInvalidated
                ? "Invalidated"
                : accepted >= requiredQualifyingShots
                    ? caseAdvanced
                        ? "Complete"
                        : "PendingEvidence"
                    : "InProgress";
            CampaignAttemptRecord terminal = attempts[attempts.Length - 1];
            long fixtureId = attempts.Select(attempt => attempt.Evidence.FixtureId)
                .Distinct()
                .Count() == 1
                    ? attempts[0].Evidence.FixtureId
                    : 0L;
            int through = attempts.Count(attempt => attempt.ProtocolQualificationReason
                    == ProtocolShotQualificationReason.Qualifying
                && attempt.Evidence.ProtocolEvidence?.ThroughPenetrationObserved == true);

            builder.Append("{\"sampleOrdinal\":")
                .Append(sample.Key.ToString(CultureInfo.InvariantCulture))
                .Append(",\"fixtureId\":")
                .Append(fixtureId.ToString(CultureInfo.InvariantCulture))
                .Append(",\"state\":")
                .Append(LabPolicies.Json(state))
                .Append(",\"attemptCount\":")
                .Append(attempts.Length.ToString(CultureInfo.InvariantCulture))
                .Append(",\"qualifyingAttemptCount\":")
                .Append(qualifying.ToString(CultureInfo.InvariantCulture))
                .Append(",\"acceptedAttemptCount\":")
                .Append(accepted.ToString(CultureInfo.InvariantCulture))
                .Append(",\"rejectedAttemptCount\":")
                .Append(rejected.ToString(CultureInfo.InvariantCulture))
                .Append(",\"invalidatedAttemptCount\":")
                .Append(invalidated.ToString(CultureInfo.InvariantCulture))
                .Append(",\"throughPenetrationCount\":")
                .Append(through.ToString(CultureInfo.InvariantCulture))
                .Append(",\"noThroughPenetrationCount\":")
                .Append((qualifying - through).ToString(CultureInfo.InvariantCulture))
                .Append(",\"firstAttemptOrdinal\":")
                .Append(attempts[0].AttemptOrdinal.ToString(CultureInfo.InvariantCulture))
                .Append(",\"lastAttemptOrdinal\":")
                .Append(terminal.AttemptOrdinal.ToString(CultureInfo.InvariantCulture))
                .Append(",\"terminalAttemptStatus\":")
                .Append(LabPolicies.Json(terminal.Status.ToString()))
                .Append(",\"terminalQualificationReason\":")
                .Append(LabPolicies.Json(
                    terminal.ProtocolQualificationReason?.ToString() ?? string.Empty))
                .Append('}');
        }

        private static int ResolveCurrentSampleOrdinal(
            IGrouping<int, CampaignAttemptRecord>[] sampleGroups)
        {
            if (sampleGroups.Length == 0)
            {
                return 0;
            }
            IGrouping<int, CampaignAttemptRecord> latest = sampleGroups[sampleGroups.Length - 1];
            return IsInvalidatedSample(latest) ? latest.Key + 1 : latest.Key;
        }

        private static bool IsInvalidatedSample(IEnumerable<CampaignAttemptRecord> sample)
        {
            return sample.Any(attempt => attempt.Status != CampaignAttemptStatus.Accepted);
        }

        private static string ResolveResultStatus(
            CampaignRunState campaignState,
            int acceptedEvidenceCount,
            int requiredQualifyingShots,
            bool complete,
            bool latestSampleInvalidated)
        {
            if (complete)
            {
                return "SimulationScreeningComplete";
            }
            if (latestSampleInvalidated)
            {
                return "SampleInvalidated";
            }
            if (acceptedEvidenceCount >= requiredQualifyingShots)
            {
                return "PendingEvidence";
            }
            if (acceptedEvidenceCount != 0)
            {
                return campaignState == CampaignRunState.Stopped ? "Incomplete" : "InProgress";
            }
            return "InsufficientEvidence";
        }

        private static string Number(double value)
        {
            if (double.IsNaN(value) || double.IsInfinity(value))
            {
                ThrowNonFiniteResult();
            }
            return value.ToString("R", CultureInfo.InvariantCulture);
        }

        private static double MinimumOrZero(
            ProtocolShotEvidence[] evidence,
            Func<ProtocolShotEvidence, double> selector)
        {
            return evidence.Length == 0 ? 0d : evidence.Min(selector);
        }

        private static double MaximumOrZero(
            ProtocolShotEvidence[] evidence,
            Func<ProtocolShotEvidence, double> selector)
        {
            return evidence.Length == 0 ? 0d : evidence.Max(selector);
        }

        private static double MeanOrZero(
            ProtocolShotEvidence[] evidence,
            Func<ProtocolShotEvidence, double> selector)
        {
            return evidence.Length == 0 ? 0d : evidence.Average(selector);
        }

        private readonly struct IndexedCase
        {
            internal IndexedCase(int index, CampaignCaseDefinition campaignCase)
            {
                Index = index;
                Case = campaignCase;
            }

            internal int Index { get; }
            internal CampaignCaseDefinition Case { get; }
        }

        [DoesNotReturn]
        private static void ThrowNullBuilder(string parameterName)
        {
            throw new ArgumentNullException(parameterName);
        }

        [DoesNotReturn]
        private static void ThrowPartialCampaign()
        {
            throw new ArgumentException(
                "Protocol result output requires both the campaign definition and snapshot.");
        }

        [DoesNotReturn]
        private static void ThrowCampaignIdentityMismatch()
        {
            throw new ArgumentException(
                "Protocol result campaign definition and snapshot identities differ.");
        }

        [DoesNotReturn]
        private static void ThrowInvalidProtocolCase(string caseId)
        {
            throw new InvalidOperationException(
                "Protocol case metadata could not be resolved for " + caseId + ".");
        }

        [DoesNotReturn]
        private static void ThrowNonFiniteResult()
        {
            throw new InvalidOperationException(
                "Protocol result output contains a non-finite number.");
        }
    }
}

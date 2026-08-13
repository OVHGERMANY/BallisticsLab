using System;
using System.Globalization;
using System.Text;

namespace BallisticsLab.Core
{
    internal static class CampaignJsonWriter
    {
        internal static void AppendOrNull(
            StringBuilder builder,
            CampaignDefinition? definition,
            CampaignRunSnapshot? snapshot)
        {
            if (definition == null && snapshot == null)
            {
                builder.Append("null");
                return;
            }
            if (definition == null || snapshot == null)
            {
                throw new ArgumentException(
                    "Campaign definition and snapshot must either both be present or both be absent.");
            }
            if (!string.Equals(
                    definition.CampaignId,
                    snapshot.CampaignId,
                    StringComparison.Ordinal))
            {
                throw new ArgumentException("Campaign definition and snapshot identities differ.", nameof(snapshot));
            }

            CampaignResultMatrix matrix = CampaignResultMatrixBuilder.Build(definition, snapshot);
            builder.Append('{');
            bool first = true;
            String(builder, ref first, "campaignId", definition.CampaignId);
            String(builder, ref first, "name", definition.Name);
            Number(builder, ref first, "runSeed", snapshot.RunSeed);
            String(builder, ref first, "state", snapshot.State.ToString());
            Number(builder, ref first, "currentCaseIndex", snapshot.CurrentCaseIndex);
            Number(builder, ref first, "currentRepetitionIndex", snapshot.CurrentRepetitionIndex);
            Number(builder, ref first, "currentAttemptIndex", snapshot.CurrentAttemptIndex);
            Number(builder, ref first, "currentFixtureId", snapshot.CurrentFixtureId);
            Boolean(builder, ref first, "gameShotSeedOverridden", false);
            String(
                builder,
                ref first,
                "seedSemantics",
                "runSeed derives stable Lab case identifiers; observed game shot seeds are recorded, not overridden");
            Property(builder, ref first, "cases");
            AppendCases(builder, definition);
            Property(builder, ref first, "attempts");
            AppendAttempts(builder, snapshot);
            Property(builder, ref first, "matrix");
            AppendMatrix(builder, matrix);
            builder.Append('}');
        }

        private static void AppendCases(StringBuilder builder, CampaignDefinition definition)
        {
            builder.Append('[');
            for (int index = 0; index < definition.Cases.Count; index++)
            {
                if (index != 0)
                {
                    builder.Append(',');
                }
                CampaignCaseDefinition campaignCase = definition.Cases[index];
                builder.Append('{');
                bool first = true;
                Number(builder, ref first, "caseIndex", index);
                String(builder, ref first, "caseId", campaignCase.CaseId);
                String(builder, ref first, "label", campaignCase.Label);
                String(builder, ref first, "selectorKind", campaignCase.SelectorKind.ToString());
                String(builder, ref first, "templateId", campaignCase.TemplateId);
                String(builder, ref first, "material", campaignCase.Material);
                Number(builder, ref first, "armorClass", campaignCase.ArmorClass);
                Number(builder, ref first, "layerCount", campaignCase.LayerCount);
                Number(builder, ref first, "layerSpacingMetres", campaignCase.LayerSpacingMetres);
                Number(builder, ref first, "plateThicknessMetres", campaignCase.PlateThicknessMetres);
                Number(builder, ref first, "distanceMetres", campaignCase.DistanceMetres);
                Number(builder, ref first, "angleDegrees", campaignCase.AngleDegrees);
                Boolean(builder, ref first, "backstopEnabled", campaignCase.BackstopEnabled);
                Number(builder, ref first, "requiredRepetitions", campaignCase.RequiredRepetitions);
                String(builder, ref first, "resetPolicy", campaignCase.ResetPolicy.ToString());
                String(
                    builder,
                    ref first,
                    "shotSequencePolicy",
                    campaignCase.ShotSequencePolicy.ToString());
                String(builder, ref first, "protocolThreatId", campaignCase.ProtocolThreatId);
                String(
                    builder,
                    ref first,
                    "protocolAmmunitionTemplateId",
                    campaignCase.ProtocolAmmunitionTemplateId);
                Number(builder, ref first, "minimumVelocityFraction", campaignCase.MinimumVelocityFraction);
                Number(builder, ref first, "maximumVelocityFraction", campaignCase.MaximumVelocityFraction);
                Boolean(builder, ref first, "requireBackstopEvidence", campaignCase.RequireBackstopEvidence);
                Boolean(builder, ref first, "requirePhysicalEvidence", campaignCase.RequirePhysicalEvidence);
                Boolean(
                    builder,
                    ref first,
                    "requireConservationEvidence",
                    campaignCase.RequireConservationEvidence);
                Number(
                    builder,
                    ref first,
                    "maximumMassClosureErrorKilograms",
                    campaignCase.MaximumMassClosureErrorKilograms);
                Number(
                    builder,
                    ref first,
                    "maximumEnergyClosureErrorJoules",
                    campaignCase.MaximumEnergyClosureErrorJoules);
                builder.Append('}');
            }
            builder.Append(']');
        }

        private static void AppendAttempts(StringBuilder builder, CampaignRunSnapshot snapshot)
        {
            builder.Append('[');
            for (int index = 0; index < snapshot.Attempts.Count; index++)
            {
                if (index != 0)
                {
                    builder.Append(',');
                }
                CampaignAttemptRecord attempt = snapshot.Attempts[index];
                CampaignShotEvidence evidence = attempt.Evidence;
                builder.Append('{');
                bool first = true;
                Number(builder, ref first, "attemptOrdinal", attempt.AttemptOrdinal);
                Number(builder, ref first, "caseIndex", attempt.CaseIndex);
                String(builder, ref first, "caseId", attempt.CaseId);
                Number(builder, ref first, "sampleOrdinal", attempt.SampleOrdinal);
                Number(builder, ref first, "repetitionIndex", attempt.RepetitionIndex);
                Number(builder, ref first, "attemptIndex", attempt.AttemptIndex);
                Number(builder, ref first, "labCaseSeed", attempt.LabCaseSeed);
                String(builder, ref first, "status", attempt.Status.ToString());
                String(
                    builder,
                    ref first,
                    "protocolQualificationReason",
                    attempt.ProtocolQualificationReason?.ToString() ?? string.Empty);
                Number(builder, ref first, "fixtureId", evidence.FixtureId);
                String(builder, ref first, "chainId", evidence.ChainId);
                String(builder, ref first, "fixtureTemplateId", evidence.FixtureTemplateId);
                String(builder, ref first, "fixtureArmorMaterial", evidence.FixtureArmorMaterial);
                Number(builder, ref first, "fixtureArmorClass", evidence.FixtureArmorClass);
                Number(builder, ref first, "fixtureLayerCount", evidence.FixtureLayerCount);
                Number(builder, ref first, "rootFireIndex", evidence.RootFireIndex);
                Number(builder, ref first, "observedRootRandomSeed", evidence.ObservedRootRandomSeed);
                String(builder, ref first, "rootShooterProfileId", evidence.RootShooterProfileId);
                String(builder, ref first, "ammunitionTemplateId", evidence.AmmunitionTemplateId);
                Number(builder, ref first, "velocityFraction", evidence.VelocityFraction);
                Property(builder, ref first, "protocolEvidence");
                AppendProtocolEvidence(builder, evidence.ProtocolEvidence);
                Property(builder, ref first, "hitLayers");
                AppendLayers(builder, evidence);
                String(builder, ref first, "outcome", evidence.Outcome);
                Boolean(builder, ref first, "reachedBackstop", evidence.ReachedBackstop);
                Number(builder, ref first, "physicalTransitionCount", evidence.PhysicalTransitionCount);
                Number(builder, ref first, "conservationRecordCount", evidence.ConservationRecordCount);
                Number(
                    builder,
                    ref first,
                    "maximumMassClosureErrorKilograms",
                    evidence.MaximumMassClosureErrorKilograms);
                Number(
                    builder,
                    ref first,
                    "maximumEnergyClosureErrorJoules",
                    evidence.MaximumEnergyClosureErrorJoules);
                builder.Append('}');
            }
            builder.Append(']');
        }

        private static void AppendProtocolEvidence(
            StringBuilder builder,
            ProtocolShotEvidence? evidence)
        {
            if (evidence == null)
            {
                builder.Append("null");
                return;
            }

            builder.Append('{');
            bool first = true;
            String(
                builder,
                ref first,
                "velocityMeasurementBasis",
                evidence.VelocityMeasurementBasis.ToString());
            Number(
                builder,
                ref first,
                "protocolVelocityMetresPerSecond",
                evidence.ProtocolVelocityMetresPerSecond);
            Number(
                builder,
                ref first,
                "targetImpactSpeedMetresPerSecond",
                evidence.TargetImpactSpeedMetresPerSecond);
            Number(
                builder,
                ref first,
                "projectileMassKilograms",
                evidence.ProjectileMassKilograms);
            Number(
                builder,
                ref first,
                "projectileDiameterMetres",
                evidence.ProjectileDiameterMetres);
            Number(
                builder,
                ref first,
                "impactAngleDegrees",
                evidence.ImpactAngleDegrees);
            Number(
                builder,
                ref first,
                "fixtureLocalHitXMetres",
                evidence.FixtureLocalHitXMetres);
            Number(
                builder,
                ref first,
                "fixtureLocalHitYMetres",
                evidence.FixtureLocalHitYMetres);
            Number(
                builder,
                ref first,
                "fixtureFaceWidthMetres",
                evidence.FixtureFaceWidthMetres);
            Number(
                builder,
                ref first,
                "fixtureFaceHeightMetres",
                evidence.FixtureFaceHeightMetres);
            Number(
                builder,
                ref first,
                "fixtureDistanceMetres",
                evidence.FixtureDistanceMetres);
            Boolean(
                builder,
                ref first,
                "witnessBackstopConfigured",
                evidence.WitnessBackstopConfigured);
            Boolean(
                builder,
                ref first,
                "throughPenetrationObserved",
                evidence.ThroughPenetrationObserved);
            builder.Append('}');
        }

        private static void AppendLayers(StringBuilder builder, CampaignShotEvidence evidence)
        {
            builder.Append('[');
            for (int index = 0; index < evidence.HitLayers.Count; index++)
            {
                if (index != 0)
                {
                    builder.Append(',');
                }
                builder.Append(evidence.HitLayers[index].ToString(CultureInfo.InvariantCulture));
            }
            builder.Append(']');
        }

        private static void AppendMatrix(StringBuilder builder, CampaignResultMatrix matrix)
        {
            builder.Append('{');
            bool first = true;
            Number(builder, ref first, "attemptCount", matrix.AttemptCount);
            Number(builder, ref first, "acceptedCount", matrix.AcceptedCount);
            Boolean(builder, ref first, "complete", matrix.Complete);
            Property(builder, ref first, "rows");
            builder.Append('[');
            for (int index = 0; index < matrix.Rows.Count; index++)
            {
                if (index != 0)
                {
                    builder.Append(',');
                }
                CampaignCaseMatrixRow row = matrix.Rows[index];
                builder.Append('{');
                bool rowFirst = true;
                Number(builder, ref rowFirst, "caseIndex", row.CaseIndex);
                String(builder, ref rowFirst, "caseId", row.CaseId);
                String(builder, ref rowFirst, "label", row.Label);
                Number(builder, ref rowFirst, "requiredRepetitions", row.RequiredRepetitions);
                Number(builder, ref rowFirst, "attemptCount", row.AttemptCount);
                Number(builder, ref rowFirst, "acceptedCount", row.AcceptedCount);
                Number(builder, ref rowFirst, "rejectedCount", row.RejectedCount);
                Number(builder, ref rowFirst, "fixtureRejectCount", row.FixtureRejectCount);
                Number(builder, ref rowFirst, "velocityRejectCount", row.VelocityRejectCount);
                Number(builder, ref rowFirst, "layerRejectCount", row.LayerRejectCount);
                Number(builder, ref rowFirst, "backstopRejectCount", row.BackstopRejectCount);
                Number(builder, ref rowFirst, "physicalRejectCount", row.PhysicalRejectCount);
                Number(
                    builder,
                    ref rowFirst,
                    "missingConservationRejectCount",
                    row.MissingConservationRejectCount);
                Number(builder, ref rowFirst, "conservationRejectCount", row.ConservationRejectCount);
                Number(builder, ref rowFirst, "protocolRejectCount", row.ProtocolRejectCount);
                Number(
                    builder,
                    ref rowFirst,
                    "sequenceInvalidatedCount",
                    row.SequenceInvalidatedCount);
                Number(builder, ref rowFirst, "meanVelocityFraction", row.MeanVelocityFraction);
                Number(builder, ref rowFirst, "meanLayersHit", row.MeanLayersHit);
                Number(
                    builder,
                    ref rowFirst,
                    "maximumMassClosureErrorKilograms",
                    row.MaximumMassClosureErrorKilograms);
                Number(
                    builder,
                    ref rowFirst,
                    "maximumEnergyClosureErrorJoules",
                    row.MaximumEnergyClosureErrorJoules);
                Boolean(builder, ref rowFirst, "complete", row.Complete);
                builder.Append('}');
            }
            builder.Append("]}");
        }

        private static void Property(StringBuilder builder, ref bool first, string name)
        {
            Separator(builder, ref first);
            builder.Append(LabPolicies.Json(name)).Append(':');
        }

        private static void String(StringBuilder builder, ref bool first, string name, string value)
        {
            Property(builder, ref first, name);
            builder.Append(LabPolicies.Json(value));
        }

        private static void Boolean(StringBuilder builder, ref bool first, string name, bool value)
        {
            Property(builder, ref first, name);
            builder.Append(value ? "true" : "false");
        }

        private static void Number(StringBuilder builder, ref bool first, string name, int value)
        {
            Property(builder, ref first, name);
            builder.Append(value.ToString(CultureInfo.InvariantCulture));
        }

        private static void Number(StringBuilder builder, ref bool first, string name, long value)
        {
            Property(builder, ref first, name);
            builder.Append(value.ToString(CultureInfo.InvariantCulture));
        }

        private static void Number(StringBuilder builder, ref bool first, string name, ulong value)
        {
            Property(builder, ref first, name);
            builder.Append(value.ToString(CultureInfo.InvariantCulture));
        }

        private static void Number(StringBuilder builder, ref bool first, string name, double value)
        {
            Property(builder, ref first, name);
            builder.Append(value.ToString("R", CultureInfo.InvariantCulture));
        }

        private static void Separator(StringBuilder builder, ref bool first)
        {
            if (!first)
            {
                builder.Append(',');
            }
            first = false;
        }
    }
}

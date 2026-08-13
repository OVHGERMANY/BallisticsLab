using System.Text.Json;
using BallisticsLab.Core;

internal static class CampaignReportInvariantValidator
{
    internal static bool Validate(
        JsonElement root,
        out int attemptCount,
        out string failure)
    {
        attemptCount = 0;
        failure = string.Empty;
        if (!root.TryGetProperty("campaign", out JsonElement campaign)
            || campaign.ValueKind == JsonValueKind.Null)
        {
            return true;
        }
        if (campaign.ValueKind != JsonValueKind.Object)
        {
            failure = "campaign is not an object or null";
            return false;
        }
        if (!TryText(campaign, "campaignId", out string campaignId)
            || !TryText(campaign, "name", out string campaignName)
            || !TryUInt64(campaign, "runSeed", out ulong runSeed)
            || !TryEnum(campaign, "state", out CampaignRunState state)
            || !TryInt32(campaign, "currentCaseIndex", out int currentCaseIndex)
            || !TryInt32(campaign, "currentRepetitionIndex", out int currentRepetitionIndex)
            || !TryInt32(campaign, "currentAttemptIndex", out int currentAttemptIndex)
            || !TryInt64(campaign, "currentFixtureId", out long currentFixtureId)
            || !campaign.TryGetProperty("gameShotSeedOverridden", out JsonElement seedOverridden)
            || seedOverridden.ValueKind != JsonValueKind.False
            || !TryText(campaign, "seedSemantics", out _))
        {
            failure = "campaign header or seed semantics are invalid";
            return false;
        }
        if (!campaign.TryGetProperty("cases", out JsonElement casesElement)
            || casesElement.ValueKind != JsonValueKind.Array
            || !campaign.TryGetProperty("attempts", out JsonElement attemptsElement)
            || attemptsElement.ValueKind != JsonValueKind.Array
            || !campaign.TryGetProperty("matrix", out JsonElement matrixElement)
            || matrixElement.ValueKind != JsonValueKind.Object)
        {
            failure = "campaign cases, attempts, or matrix are missing";
            return false;
        }

        if (!TryCases(casesElement, out List<CampaignCaseDefinition> cases, out failure))
        {
            return false;
        }
        var definition = new CampaignDefinition(campaignId, campaignName, cases);
        if (!TryAttempts(
                attemptsElement,
                definition,
                runSeed,
                out List<CampaignAttemptRecord> attempts,
                out failure))
        {
            return false;
        }
        attemptCount = attempts.Count;
        if (currentCaseIndex < 0
            || currentCaseIndex > definition.Cases.Count
            || currentRepetitionIndex < 0
            || currentAttemptIndex < 0
            || currentFixtureId < 0L)
        {
            failure = "campaign cursor contains a negative or out-of-range value";
            return false;
        }
        if (!ReplayMatchesHeader(
                definition,
                runSeed,
                attempts,
                state,
                currentCaseIndex,
                currentRepetitionIndex,
                currentAttemptIndex,
                currentFixtureId,
                out failure))
        {
            return false;
        }

        var snapshot = new CampaignRunSnapshot(
            campaignId,
            campaignName,
            runSeed,
            state,
            currentCaseIndex,
            currentRepetitionIndex,
            currentAttemptIndex,
            currentFixtureId,
            attempts);
        CampaignResultMatrix expected = CampaignResultMatrixBuilder.Build(definition, snapshot);
        if (state == CampaignRunState.Completed
            && (currentCaseIndex != definition.Cases.Count || !expected.Complete))
        {
            failure = "completed campaign cursor or matrix is incomplete";
            return false;
        }
        if (state != CampaignRunState.Completed && expected.Complete)
        {
            failure = "campaign matrix is complete but state is not Completed";
            return false;
        }
        return MatrixMatches(matrixElement, expected, out failure);
    }

    private static bool ReplayMatchesHeader(
        CampaignDefinition definition,
        ulong runSeed,
        List<CampaignAttemptRecord> attempts,
        CampaignRunState expectedState,
        int expectedCaseIndex,
        int expectedRepetitionIndex,
        int expectedAttemptIndex,
        long expectedFixtureId,
        out string failure)
    {
        failure = string.Empty;
        var tracker = new CampaignRunTracker(definition, runSeed);
        if (!tracker.Start())
        {
            failure = "campaign replay could not start";
            return false;
        }

        for (int index = 0; index < attempts.Count; index++)
        {
            CampaignAttemptRecord recorded = attempts[index];
            CampaignRunSnapshot before = tracker.Snapshot();
            if (before.State == CampaignRunState.AwaitingReset)
            {
                if (!tracker.ConfirmReset(before.CurrentFixtureId))
                {
                    failure = "campaign replay could not confirm reset before attempt "
                        + recorded.AttemptOrdinal;
                    return false;
                }
                before = tracker.Snapshot();
            }
            if (before.State == CampaignRunState.AwaitingFixture)
            {
                if (!tracker.AttachFixture(recorded.Evidence.FixtureId))
                {
                    failure = "campaign replay could not attach fixture for attempt "
                        + recorded.AttemptOrdinal;
                    return false;
                }
                before = tracker.Snapshot();
            }
            if (before.State != CampaignRunState.AwaitingShot
                || before.CurrentCaseIndex != recorded.CaseIndex
                || before.CurrentRepetitionIndex != recorded.RepetitionIndex
                || before.CurrentAttemptIndex != recorded.AttemptIndex
                || before.CurrentFixtureId != recorded.Evidence.FixtureId)
            {
                failure = "campaign attempt order or cursor is invalid at attempt "
                    + recorded.AttemptOrdinal;
                return false;
            }

            CampaignAttemptRecord? replayed = tracker.RecordShot(recorded.Evidence);
            if (replayed == null
                || replayed.AttemptOrdinal != recorded.AttemptOrdinal
                || replayed.CaseIndex != recorded.CaseIndex
                || replayed.RepetitionIndex != recorded.RepetitionIndex
                || replayed.AttemptIndex != recorded.AttemptIndex
                || replayed.LabCaseSeed != recorded.LabCaseSeed
                || replayed.Status != recorded.Status)
            {
                failure = "campaign replay disagrees with attempt " + recorded.AttemptOrdinal;
                return false;
            }
        }

        CampaignRunSnapshot replay = tracker.Snapshot();
        if (expectedState == CampaignRunState.AwaitingShot)
        {
            if (replay.State == CampaignRunState.AwaitingReset)
            {
                tracker.ConfirmReset(replay.CurrentFixtureId);
            }
            else if (replay.State == CampaignRunState.AwaitingFixture)
            {
                tracker.AttachFixture(expectedFixtureId);
            }
        }
        else if (expectedState == CampaignRunState.Stopped)
        {
            tracker.Stop();
        }

        replay = tracker.Snapshot();
        if (replay.State != expectedState
            || replay.CurrentCaseIndex != expectedCaseIndex
            || replay.CurrentRepetitionIndex != expectedRepetitionIndex
            || replay.CurrentAttemptIndex != expectedAttemptIndex
            || replay.CurrentFixtureId != expectedFixtureId)
        {
            failure = "campaign header cursor does not match replayed attempts";
            return false;
        }
        return true;
    }

    private static bool TryCases(
        JsonElement array,
        out List<CampaignCaseDefinition> cases,
        out string failure)
    {
        cases = new List<CampaignCaseDefinition>();
        failure = string.Empty;
        int expectedIndex = 0;
        foreach (JsonElement element in array.EnumerateArray())
        {
            if (element.ValueKind != JsonValueKind.Object
                || !TryInt32(element, "caseIndex", out int caseIndex)
                || caseIndex != expectedIndex
                || !TryText(element, "caseId", out string caseId)
                || !TryText(element, "label", out string label)
                || !TryEnum(element, "selectorKind", out CampaignFixtureSelectorKind selectorKind)
                || !TryTextAllowEmpty(element, "templateId", out string templateId)
                || !TryTextAllowEmpty(element, "material", out string material)
                || !TryInt32(element, "armorClass", out int armorClass)
                || !TryInt32(element, "layerCount", out int layerCount)
                || !TryDouble(element, "layerSpacingMetres", out double layerSpacingMetres)
                || !TryDouble(element, "plateThicknessMetres", out double plateThicknessMetres)
                || !TryDouble(element, "distanceMetres", out double distanceMetres)
                || !TryDouble(element, "angleDegrees", out double angleDegrees)
                || !TryBoolean(element, "backstopEnabled", out bool backstopEnabled)
                || !TryInt32(element, "requiredRepetitions", out int repetitions)
                || !TryEnum(element, "resetPolicy", out CampaignResetPolicy resetPolicy)
                || !TryDouble(element, "minimumVelocityFraction", out double minimumVelocity)
                || !TryDouble(element, "maximumVelocityFraction", out double maximumVelocity)
                || !TryBoolean(element, "requireBackstopEvidence", out bool requireBackstop)
                || !TryBoolean(element, "requirePhysicalEvidence", out bool requirePhysical)
                || !TryBoolean(
                    element,
                    "requireConservationEvidence",
                    out bool requireConservation)
                || !TryDouble(
                    element,
                    "maximumMassClosureErrorKilograms",
                    out double maximumMassError)
                || !TryDouble(
                    element,
                    "maximumEnergyClosureErrorJoules",
                    out double maximumEnergyError))
            {
                failure = "campaign case " + expectedIndex + " is invalid";
                return false;
            }
            cases.Add(new CampaignCaseDefinition(
                caseId,
                label,
                selectorKind,
                templateId,
                material,
                armorClass,
                layerCount,
                layerSpacingMetres,
                plateThicknessMetres,
                distanceMetres,
                angleDegrees,
                backstopEnabled,
                repetitions,
                resetPolicy,
                minimumVelocity,
                maximumVelocity,
                requireBackstop,
                requirePhysical,
                requireConservation,
                maximumMassError,
                maximumEnergyError));
            expectedIndex++;
        }
        if (cases.Count == 0)
        {
            failure = "campaign has no cases";
            return false;
        }
        return true;
    }

    private static bool TryAttempts(
        JsonElement array,
        CampaignDefinition definition,
        ulong runSeed,
        out List<CampaignAttemptRecord> attempts,
        out string failure)
    {
        attempts = new List<CampaignAttemptRecord>();
        failure = string.Empty;
        long expectedOrdinal = 1L;
        var chains = new HashSet<string>(StringComparer.Ordinal);
        foreach (JsonElement element in array.EnumerateArray())
        {
            if (element.ValueKind != JsonValueKind.Object
                || !TryInt64(element, "attemptOrdinal", out long ordinal)
                || ordinal != expectedOrdinal
                || !TryInt32(element, "caseIndex", out int caseIndex)
                || caseIndex < 0
                || caseIndex >= definition.Cases.Count
                || !TryText(element, "caseId", out string caseId)
                || !string.Equals(
                    caseId,
                    definition.Cases[caseIndex].CaseId,
                    StringComparison.Ordinal)
                || !TryInt32(element, "repetitionIndex", out int repetitionIndex)
                || repetitionIndex < 0
                || repetitionIndex >= definition.Cases[caseIndex].RequiredRepetitions
                || !TryInt32(element, "attemptIndex", out int attemptIndex)
                || attemptIndex < 0
                || !TryUInt64(element, "labCaseSeed", out ulong labCaseSeed)
                || labCaseSeed != CampaignSeedDeriver.Derive(
                    runSeed,
                    definition.CampaignId,
                    caseId,
                    repetitionIndex)
                || !TryEnum(element, "status", out CampaignAttemptStatus status)
                || !TryInt64(element, "fixtureId", out long fixtureId)
                || fixtureId <= 0L
                || !TryText(element, "chainId", out string chainId)
                || !chains.Add(chainId)
                || !TryText(element, "fixtureTemplateId", out string fixtureTemplateId)
                || !TryText(element, "fixtureArmorMaterial", out string fixtureArmorMaterial)
                || !TryInt32(element, "fixtureArmorClass", out int fixtureArmorClass)
                || !TryInt32(element, "fixtureLayerCount", out int fixtureLayerCount)
                || !TryInt32(element, "rootFireIndex", out int rootFireIndex)
                || !TryInt32(element, "observedRootRandomSeed", out int observedRootSeed)
                || !TryTextAllowEmpty(
                    element,
                    "rootShooterProfileId",
                    out string rootShooterProfileId)
                || !TryText(element, "ammunitionTemplateId", out string ammunitionTemplateId)
                || !TryDouble(element, "velocityFraction", out double velocityFraction)
                || !TryLayers(element, out List<int> hitLayers)
                || !TryText(element, "outcome", out string outcome)
                || !TryBoolean(element, "reachedBackstop", out bool reachedBackstop)
                || !TryInt32(element, "physicalTransitionCount", out int physicalCount)
                || !TryInt32(element, "conservationRecordCount", out int conservationCount)
                || !TryDouble(
                    element,
                    "maximumMassClosureErrorKilograms",
                    out double maximumMassError)
                || !TryDouble(
                    element,
                    "maximumEnergyClosureErrorJoules",
                    out double maximumEnergyError))
            {
                failure = "campaign attempt " + expectedOrdinal + " is invalid";
                return false;
            }

            var evidence = new CampaignShotEvidence(
                fixtureId,
                chainId,
                fixtureTemplateId,
                fixtureArmorMaterial,
                fixtureArmorClass,
                fixtureLayerCount,
                rootFireIndex,
                observedRootSeed,
                rootShooterProfileId,
                ammunitionTemplateId,
                velocityFraction,
                hitLayers,
                outcome,
                reachedBackstop,
                physicalCount,
                conservationCount,
                maximumMassError,
                maximumEnergyError);
            if (CampaignEvidenceEvaluator.Evaluate(definition.Cases[caseIndex], evidence) != status)
            {
                failure = "campaign attempt status does not match its evidence gates";
                return false;
            }
            attempts.Add(new CampaignAttemptRecord(
                ordinal,
                caseIndex,
                caseId,
                repetitionIndex,
                attemptIndex,
                labCaseSeed,
                status,
                evidence));
            expectedOrdinal++;
        }
        return true;
    }

    private static bool MatrixMatches(
        JsonElement matrix,
        CampaignResultMatrix expected,
        out string failure)
    {
        failure = string.Empty;
        if (!TryInt32(matrix, "attemptCount", out int attemptCount)
            || attemptCount != expected.AttemptCount
            || !TryInt32(matrix, "acceptedCount", out int acceptedCount)
            || acceptedCount != expected.AcceptedCount
            || !TryBoolean(matrix, "complete", out bool complete)
            || complete != expected.Complete
            || !matrix.TryGetProperty("rows", out JsonElement rows)
            || rows.ValueKind != JsonValueKind.Array
            || rows.GetArrayLength() != expected.Rows.Count)
        {
            failure = "campaign matrix summary is invalid";
            return false;
        }

        int index = 0;
        foreach (JsonElement row in rows.EnumerateArray())
        {
            CampaignCaseMatrixRow expectedRow = expected.Rows[index];
            if (!TryInt32(row, "caseIndex", out int caseIndex)
                || caseIndex != expectedRow.CaseIndex
                || !TryText(row, "caseId", out string caseId)
                || caseId != expectedRow.CaseId
                || !TryText(row, "label", out string label)
                || label != expectedRow.Label
                || !MatchesInt(row, "requiredRepetitions", expectedRow.RequiredRepetitions)
                || !MatchesInt(row, "attemptCount", expectedRow.AttemptCount)
                || !MatchesInt(row, "acceptedCount", expectedRow.AcceptedCount)
                || !MatchesInt(row, "rejectedCount", expectedRow.RejectedCount)
                || !MatchesInt(row, "fixtureRejectCount", expectedRow.FixtureRejectCount)
                || !MatchesInt(row, "velocityRejectCount", expectedRow.VelocityRejectCount)
                || !MatchesInt(row, "layerRejectCount", expectedRow.LayerRejectCount)
                || !MatchesInt(row, "backstopRejectCount", expectedRow.BackstopRejectCount)
                || !MatchesInt(row, "physicalRejectCount", expectedRow.PhysicalRejectCount)
                || !MatchesInt(
                    row,
                    "missingConservationRejectCount",
                    expectedRow.MissingConservationRejectCount)
                || !MatchesInt(row, "conservationRejectCount", expectedRow.ConservationRejectCount)
                || !MatchesDouble(row, "meanVelocityFraction", expectedRow.MeanVelocityFraction)
                || !MatchesDouble(row, "meanLayersHit", expectedRow.MeanLayersHit)
                || !MatchesDouble(
                    row,
                    "maximumMassClosureErrorKilograms",
                    expectedRow.MaximumMassClosureErrorKilograms)
                || !MatchesDouble(
                    row,
                    "maximumEnergyClosureErrorJoules",
                    expectedRow.MaximumEnergyClosureErrorJoules)
                || !TryBoolean(row, "complete", out bool rowComplete)
                || rowComplete != expectedRow.Complete)
            {
                failure = "campaign matrix row " + index + " does not match attempts";
                return false;
            }
            index++;
        }
        return true;
    }

    private static bool TryLayers(JsonElement owner, out List<int> layers)
    {
        layers = new List<int>();
        if (!owner.TryGetProperty("hitLayers", out JsonElement array)
            || array.ValueKind != JsonValueKind.Array)
        {
            return false;
        }
        int previous = -1;
        foreach (JsonElement item in array.EnumerateArray())
        {
            if (!item.TryGetInt32(out int layer) || layer < 0 || layer <= previous)
            {
                return false;
            }
            layers.Add(layer);
            previous = layer;
        }
        return true;
    }

    private static bool MatchesInt(JsonElement owner, string name, int expected)
    {
        return TryInt32(owner, name, out int value) && value == expected;
    }

    private static bool MatchesDouble(JsonElement owner, string name, double expected)
    {
        return TryDouble(owner, name, out double value)
            && Math.Abs(value - expected) <= 0.000000001d * Math.Max(1d, Math.Abs(expected));
    }

    private static bool TryText(JsonElement owner, string name, out string value)
    {
        return TryTextAllowEmpty(owner, name, out value) && !string.IsNullOrWhiteSpace(value);
    }

    private static bool TryTextAllowEmpty(JsonElement owner, string name, out string value)
    {
        if (owner.TryGetProperty(name, out JsonElement element)
            && element.ValueKind == JsonValueKind.String)
        {
            value = element.GetString() ?? string.Empty;
            return true;
        }
        value = string.Empty;
        return false;
    }

    private static bool TryBoolean(JsonElement owner, string name, out bool value)
    {
        if (owner.TryGetProperty(name, out JsonElement element)
            && (element.ValueKind == JsonValueKind.True || element.ValueKind == JsonValueKind.False))
        {
            value = element.GetBoolean();
            return true;
        }
        value = false;
        return false;
    }

    private static bool TryInt32(JsonElement owner, string name, out int value)
    {
        value = 0;
        return owner.TryGetProperty(name, out JsonElement element) && element.TryGetInt32(out value);
    }

    private static bool TryInt64(JsonElement owner, string name, out long value)
    {
        value = 0L;
        return owner.TryGetProperty(name, out JsonElement element) && element.TryGetInt64(out value);
    }

    private static bool TryUInt64(JsonElement owner, string name, out ulong value)
    {
        value = 0UL;
        return owner.TryGetProperty(name, out JsonElement element) && element.TryGetUInt64(out value);
    }

    private static bool TryDouble(JsonElement owner, string name, out double value)
    {
        value = 0d;
        return owner.TryGetProperty(name, out JsonElement element)
            && element.TryGetDouble(out value)
            && !double.IsNaN(value)
            && !double.IsInfinity(value);
    }

    private static bool TryEnum<T>(JsonElement owner, string name, out T value)
        where T : struct, Enum
    {
        value = default;
        return TryText(owner, name, out string text)
            && Enum.TryParse(text, ignoreCase: false, out value)
            && Enum.IsDefined(value);
    }
}

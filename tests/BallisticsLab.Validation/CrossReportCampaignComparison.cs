using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using BallisticsLab.Core;

internal static class CrossReportCampaignComparison
{
    private const string MaterialCampaignId = "physical-material-matrix";

    [SuppressMessage(
        "Design",
        "CA1031:Do not catch general exception types",
        Justification = "Arbitrary report and file faults are returned as comparison failures by contract.")]
    internal static bool TryBuild(
        IEnumerable<string> reportFiles,
        out string comparisonJson,
        out string failure)
    {
        comparisonJson = string.Empty;
        failure = string.Empty;
        ArgumentNullException.ThrowIfNull(reportFiles);

        try
        {
            string[] selected = CurrentReportSetValidator.Select(
                    reportFiles,
                    LabBuild.ReportSchema,
                    LabBuild.PluginVersion)
                .OrderBy(File.GetLastWriteTimeUtc)
                .ThenBy(path => path, StringComparer.Ordinal)
                .ToArray();
            var runs = new Dictionary<string, RunDocument>(StringComparer.Ordinal);
            foreach (string reportFile in selected)
            {
                if (!ReportInvariantValidator.Validate(reportFile, out string reportFailure))
                {
                    failure = Path.GetFileName(reportFile) + ": " + reportFailure;
                    return false;
                }

                using JsonDocument document = JsonDocument.Parse(File.ReadAllText(reportFile));
                JsonElement root = document.RootElement;
                if (!root.TryGetProperty("campaign", out JsonElement campaign)
                    || campaign.ValueKind != JsonValueKind.Object
                    || !campaign.TryGetProperty("attempts", out JsonElement attempts)
                    || attempts.ValueKind != JsonValueKind.Array
                    || attempts.GetArrayLength() == 0)
                {
                    continue;
                }

                string runInstanceId = RequiredText(campaign, "runInstanceId");
                var candidate = new RunDocument(
                    Path.GetFileName(reportFile),
                    campaign.Clone(),
                    root.TryGetProperty("protocolScreeningResult", out JsonElement protocolResult)
                        ? protocolResult.Clone()
                        : default);
                if (!runs.TryGetValue(runInstanceId, out RunDocument? current))
                {
                    runs.Add(runInstanceId, candidate);
                    continue;
                }
                if (!TrySelectSnapshot(current, candidate, out RunDocument selectedRun, out failure))
                {
                    return false;
                }
                runs[runInstanceId] = selectedRun;
            }

            RunDocument[] uniqueRuns = runs.Values
                .OrderBy(run => RequiredText(run.Campaign, "runInstanceId"), StringComparer.Ordinal)
                .ToArray();
            MaterialRunData[] materialRuns = uniqueRuns
                .Where(run => string.Equals(
                    RequiredText(run.Campaign, "campaignId"),
                    MaterialCampaignId,
                    StringComparison.Ordinal))
                .Select(BuildMaterialRun)
                .ToArray();
            ProtocolRunData[] protocolRuns = uniqueRuns
                .SelectMany(BuildProtocolRuns)
                .ToArray();
            if (!MaterialCohortsAreCompatible(materialRuns, out failure))
            {
                return false;
            }

            comparisonJson = BuildDocument(selected.Length, uniqueRuns.Length, materialRuns, protocolRuns)
                .ToJsonString(new JsonSerializerOptions { WriteIndented = true });
            return true;
        }
        catch (Exception exception)
        {
            failure = exception.Message;
            return false;
        }
    }

    internal static string FormatSummary(string comparisonJson)
    {
        using JsonDocument document = JsonDocument.Parse(comparisonJson);
        JsonElement root = document.RootElement;
        var builder = new StringBuilder(512);
        builder.Append("Schema-4 campaign comparison: ")
            .Append(root.GetProperty("sourceReportCount").GetInt32().ToString(CultureInfo.InvariantCulture))
            .Append(" reports, ")
            .Append(root.GetProperty("uniqueCampaignRunCount").GetInt32().ToString(
                CultureInfo.InvariantCulture))
            .AppendLine(" unique runs.");
        JsonElement cohorts = root.GetProperty("materialCohorts");
        builder.Append("Comparable material cohorts: ")
            .Append(cohorts.GetArrayLength().ToString(CultureInfo.InvariantCulture))
            .AppendLine(".");
        foreach (JsonElement cohort in cohorts.EnumerateArray())
        {
            builder.Append("  ammo ")
                .Append(cohort.GetProperty("ammunitionTemplateId").GetString())
                .Append(": ")
                .Append(cohort.GetProperty("runCount").GetInt32().ToString(CultureInfo.InvariantCulture))
                .Append(" complete runs across ")
                .Append(cohort.GetProperty("materials").GetArrayLength().ToString(
                    CultureInfo.InvariantCulture))
                .AppendLine(" materials.");
        }
        JsonElement protocols = root.GetProperty("protocolThreatSummaries");
        builder.Append("Protocol screening groups: ")
            .Append(protocols.GetArrayLength().ToString(CultureInfo.InvariantCulture))
            .AppendLine(" (simulation evidence only; no certification claim). ");
        foreach (JsonElement protocol in protocols.EnumerateArray())
        {
            builder.Append("  ")
                .Append(protocol.GetProperty("threatId").GetString())
                .Append(": ")
                .Append(protocol.GetProperty("completeRunCount").GetInt32().ToString(
                    CultureInfo.InvariantCulture))
                .Append('/')
                .Append(protocol.GetProperty("runCount").GetInt32().ToString(
                    CultureInfo.InvariantCulture))
                .AppendLine(" complete.");
        }
        return builder.ToString().TrimEnd();
    }

    internal static void WriteAtomically(string outputPath, string comparisonJson)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(outputPath);
        ArgumentException.ThrowIfNullOrWhiteSpace(comparisonJson);
        string fullPath = Path.GetFullPath(outputPath);
        string? directory = Path.GetDirectoryName(fullPath);
        if (string.IsNullOrEmpty(directory))
        {
            throw new ArgumentException("Comparison output directory could not be resolved.", nameof(outputPath));
        }
        Directory.CreateDirectory(directory);
        string temporaryPath = Path.Combine(
            directory,
            "." + Path.GetFileName(fullPath) + "." + Guid.NewGuid().ToString("N") + ".tmp");
        try
        {
            File.WriteAllText(temporaryPath, comparisonJson + Environment.NewLine);
            File.Move(temporaryPath, fullPath, overwrite: true);
        }
        finally
        {
            if (File.Exists(temporaryPath))
            {
                File.Delete(temporaryPath);
            }
        }
    }

    internal static bool SyntheticComparisonDeduplicatesProgressiveReports()
    {
        string directory = NewTemporaryDirectory("Deduplicate");
        try
        {
            CampaignDefinition definition = CampaignCatalog.PhysicalMaterialMatrix();
            string runId = Guid.Parse("00112233-4455-6677-8899-aabbccddeeff")
                .ToString("N");
            var tracker = new CampaignRunTracker(definition, 91UL, runId);
            tracker.Start();
            AppendAcceptedMaterialCase(tracker, definition, 0, "ammo-one", 1000L);
            string first = WriteReport(directory, "first", definition, tracker.Snapshot());
            AppendAcceptedMaterialCase(tracker, definition, 1, "ammo-one", 1100L);
            string second = WriteReport(directory, "second", definition, tracker.Snapshot());

            if (!TryBuild(new[] { first, second }, out string comparison, out _))
            {
                return false;
            }
            using JsonDocument document = JsonDocument.Parse(comparison);
            JsonElement root = document.RootElement;
            JsonElement run = root.GetProperty("materialComparisonRuns")[0];
            return root.GetProperty("sourceReportCount").GetInt32() == 2
                && root.GetProperty("uniqueCampaignRunCount").GetInt32() == 1
                && run.GetProperty("runInstanceId").GetString() == runId
                && run.GetProperty("ineligibleReason").GetString() == "CampaignIncomplete"
                && run.GetProperty("materials")[0]
                    .GetProperty("acceptedShotCount").GetInt32() == 3
                && run.GetProperty("materials")[1]
                    .GetProperty("acceptedShotCount").GetInt32() == 3;
        }
        finally
        {
            Directory.Delete(directory, true);
        }
    }

    internal static bool SyntheticComparisonRejectsDivergentRunHistory()
    {
        string directory = NewTemporaryDirectory("Divergent");
        try
        {
            CampaignDefinition definition = CampaignCatalog.PhysicalMaterialMatrix();
            const string runId = "102132435465768798a9babcbddcedfe";
            var firstTracker = new CampaignRunTracker(definition, 92UL, runId);
            firstTracker.Start();
            AppendAcceptedMaterialCase(firstTracker, definition, 0, "ammo-one", 1200L);
            var secondTracker = new CampaignRunTracker(definition, 92UL, runId);
            secondTracker.Start();
            AppendAcceptedMaterialCase(secondTracker, definition, 0, "ammo-one", 1300L);
            string first = WriteReport(directory, "first", definition, firstTracker.Snapshot());
            string second = WriteReport(directory, "second", definition, secondTracker.Snapshot());
            return !TryBuild(new[] { first, second }, out _, out string failure)
                && failure.Contains("divergent attempt history", StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            Directory.Delete(directory, true);
        }
    }

    internal static bool SyntheticComparisonRequiresExactAmmunitionCohort()
    {
        string directory = NewTemporaryDirectory("Cohorts");
        try
        {
            CampaignDefinition definition = CampaignCatalog.PhysicalMaterialMatrix();
            var first = new CampaignRunTracker(
                definition,
                93UL,
                "2132435465768798a9bacbdcedfe0f10");
            CompleteMaterialCampaign(first, definition, "ammo-one", string.Empty, 1400L);
            var second = new CampaignRunTracker(
                definition,
                94UL,
                "32435465768798a9bacbdcedfe0f1021");
            CompleteMaterialCampaign(second, definition, "ammo-one", "ammo-two", 2000L);
            string firstPath = WriteReport(directory, "first", definition, first.Snapshot());
            string secondPath = WriteReport(directory, "second", definition, second.Snapshot());

            if (!TryBuild(new[] { firstPath, secondPath }, out string comparison, out _))
            {
                return false;
            }
            using JsonDocument document = JsonDocument.Parse(comparison);
            JsonElement root = document.RootElement;
            JsonElement runs = root.GetProperty("materialComparisonRuns");
            JsonElement cohorts = root.GetProperty("materialCohorts");
            JsonElement eligible = runs.EnumerateArray().Single(run =>
                run.GetProperty("eligibleForComparison").GetBoolean());
            JsonElement ineligible = runs.EnumerateArray().Single(run =>
                !run.GetProperty("eligibleForComparison").GetBoolean());
            return cohorts.GetArrayLength() == 1
                && cohorts[0].GetProperty("ammunitionTemplateId").GetString() == "ammo-one"
                && cohorts[0].GetProperty("materials").GetArrayLength() == 7
                && cohorts[0].GetProperty("materials")[0]
                    .GetProperty("acceptedShotCount").GetInt32() == 3
                && eligible.GetProperty("ammunitionTemplateId").GetString() == "ammo-one"
                && ineligible.GetProperty("ineligibleReason").GetString() == "MixedAmmunition";
        }
        finally
        {
            Directory.Delete(directory, true);
        }
    }

    internal static bool SyntheticComparisonPreservesProtocolClassification()
    {
        string directory = NewTemporaryDirectory("Protocol");
        try
        {
            CampaignDefinition definition = CampaignCatalog.GostSimulationScreening(
                "gost-34286-br4-7.62x39-ps-57-n-231");
            CampaignCaseDefinition campaignCase = definition.Cases[0];
            var tracker = new CampaignRunTracker(
                definition,
                95UL,
                "435465768798a9bacbdcedfe0f102132");
            tracker.Start();
            tracker.AttachFixture(3000L);
            if (!campaignCase.TryGetProtocolDefinition(
                    out ProtocolThreatDefinition? threat,
                    out ProtocolAmmunitionMapping? mapping)
                || threat == null
                || mapping == null
                || !ProtocolImpactPattern.TryCreate(
                    threat,
                    mapping,
                    0.7d,
                    0.7d,
                    out ProtocolImpactPattern? pattern,
                    out _)
                || pattern == null)
            {
                return false;
            }
            for (int index = 0; index < pattern.Points.Count; index++)
            {
                ProtocolImpactPoint point = pattern.Points[index];
                var protocol = new ProtocolShotEvidence(
                    3000L,
                    mapping.TemplateId,
                    ProtocolVelocityMeasurementBasis.EftTrajectoryThreeMetres,
                    (threat.MinimumVelocityMetresPerSecond + threat.MaximumVelocityMetresPerSecond)
                        * 0.5d,
                    mapping.InstalledProjectileMassKilograms,
                    mapping.InstalledProjectileDiameterMetres,
                    0d,
                    point.LocalXMetres,
                    point.LocalYMetres,
                    0.7d,
                    0.7d,
                    threat.TestDistanceMetres,
                    true,
                    false);
                CampaignAttemptRecord? recorded = tracker.RecordShot(CreateEvidence(
                    campaignCase,
                    3000L,
                    "protocol-" + index.ToString(CultureInfo.InvariantCulture),
                    mapping.TemplateId,
                    protocol));
                if (recorded?.Status != CampaignAttemptStatus.Accepted)
                {
                    return false;
                }
            }
            string path = WriteReport(directory, "protocol", definition, tracker.Snapshot());
            if (!TryBuild(new[] { path }, out string comparison, out _))
            {
                return false;
            }
            using JsonDocument document = JsonDocument.Parse(comparison);
            JsonElement root = document.RootElement;
            JsonElement summary = root.GetProperty("protocolThreatSummaries")[0];
            return root.GetProperty("simulationEvidenceOnly").GetBoolean()
                && !root.GetProperty("certificationClaim").GetBoolean()
                && summary.GetProperty("completeRunCount").GetInt32() == 1
                && summary.GetProperty("noThroughPenetrationCount").GetInt32() == 5
                && summary.GetProperty("simulationEvidenceOnly").GetBoolean()
                && !summary.GetProperty("certificationClaim").GetBoolean();
        }
        finally
        {
            Directory.Delete(directory, true);
        }
    }

    internal static bool SyntheticComparisonRejectsInvalidRunIdentity()
    {
        CampaignDefinition definition = CampaignCatalog.PhysicalMaterialMatrix();
        var tracker = new CampaignRunTracker(
            definition,
            96UL,
            "5465768798a9bacbdcedfe0f10213243");
        tracker.Start();
        AppendAcceptedMaterialCase(tracker, definition, 0, "ammo-one", 4000L);
        JsonNode report = JsonNode.Parse(PhysicalReportDocumentWriter.Build(
            "[]",
            Array.Empty<PhysicalTransitionRecord>(),
            definition,
            tracker.Snapshot()))!;
        report["campaign"]!["runInstanceId"] = "not-a-run-id";
        using JsonDocument document = JsonDocument.Parse(report.ToJsonString());
        return !CampaignReportInvariantValidator.Validate(
            document.RootElement,
            out _,
            out string failure)
            && failure.Contains("header", StringComparison.OrdinalIgnoreCase);
    }

    internal static bool SyntheticComparisonRejectsUnlikeFixtureCohort()
    {
        string directory = NewTemporaryDirectory("UnlikeFixture");
        try
        {
            CampaignDefinition definition = CampaignCatalog.PhysicalMaterialMatrix();
            var first = new CampaignRunTracker(
                definition,
                97UL,
                "65768798a9bacbdcedfe0f1021324354");
            CompleteMaterialCampaign(first, definition, "ammo-one", string.Empty, 5000L);
            var second = new CampaignRunTracker(
                definition,
                98UL,
                "768798a9bacbdcedfe0f102132435465");
            CompleteMaterialCampaign(second, definition, "ammo-one", string.Empty, 6000L);
            string firstPath = WriteReport(directory, "first", definition, first.Snapshot());
            string secondPath = WriteReport(directory, "second", definition, second.Snapshot());
            JsonNode changed = JsonNode.Parse(File.ReadAllText(secondPath))!;
            changed["campaign"]!["cases"]![0]!["plateThicknessMetres"] = 0.02d;
            File.WriteAllText(secondPath, changed.ToJsonString());
            return !TryBuild(new[] { firstPath, secondPath }, out _, out string failure)
                && failure.Contains("fixture definitions", StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            Directory.Delete(directory, true);
        }
    }

    internal static bool SyntheticComparisonWriterCommitsOneCompleteDocument()
    {
        string directory = NewTemporaryDirectory("Writer");
        try
        {
            string output = Path.Combine(directory, "comparison.json");
            const string document = "{\"documentSchema\":1,\"complete\":true}";
            WriteAtomically(output, document);
            string[] files = Directory.GetFiles(directory, "*", SearchOption.TopDirectoryOnly);
            return files.Length == 1
                && string.Equals(files[0], output, StringComparison.OrdinalIgnoreCase)
                && string.Equals(File.ReadAllText(output), document + Environment.NewLine, StringComparison.Ordinal);
        }
        finally
        {
            Directory.Delete(directory, true);
        }
    }

    private static bool TrySelectSnapshot(
        RunDocument current,
        RunDocument candidate,
        out RunDocument selected,
        out string failure)
    {
        selected = current;
        failure = string.Empty;
        string currentCampaignId = RequiredText(current.Campaign, "campaignId");
        string candidateCampaignId = RequiredText(candidate.Campaign, "campaignId");
        ulong currentSeed = current.Campaign.GetProperty("runSeed").GetUInt64();
        ulong candidateSeed = candidate.Campaign.GetProperty("runSeed").GetUInt64();
        if (!string.Equals(currentCampaignId, candidateCampaignId, StringComparison.Ordinal)
            || currentSeed != candidateSeed
            || !JsonEquals(
                current.Campaign.GetProperty("cases"),
                candidate.Campaign.GetProperty("cases")))
        {
            failure = "campaign run instance " + RequiredText(current.Campaign, "runInstanceId")
                + " changes identity or case definitions";
            return false;
        }

        JsonElement currentAttempts = current.Campaign.GetProperty("attempts");
        JsonElement candidateAttempts = candidate.Campaign.GetProperty("attempts");
        int sharedLength = Math.Min(currentAttempts.GetArrayLength(), candidateAttempts.GetArrayLength());
        for (int index = 0; index < sharedLength; index++)
        {
            if (!JsonEquals(currentAttempts[index], candidateAttempts[index]))
            {
                failure = "campaign run instance " + RequiredText(current.Campaign, "runInstanceId")
                    + " has divergent attempt history in " + current.SourceName
                    + " and " + candidate.SourceName;
                return false;
            }
        }
        if (candidateAttempts.GetArrayLength() >= currentAttempts.GetArrayLength())
        {
            selected = candidate;
        }
        return true;
    }

    private static MaterialRunData BuildMaterialRun(RunDocument run)
    {
        JsonElement campaign = run.Campaign;
        JsonElement attempts = campaign.GetProperty("attempts");
        JsonElement cases = campaign.GetProperty("cases");
        JsonElement matrixRows = campaign.GetProperty("matrix").GetProperty("rows");
        string[] ammunitionIds = attempts.EnumerateArray()
            .Where(IsAccepted)
            .Select(attempt => RequiredText(attempt, "ammunitionTemplateId"))
            .Distinct(StringComparer.Ordinal)
            .OrderBy(value => value, StringComparer.Ordinal)
            .ToArray();
        var rows = new MaterialRowData[cases.GetArrayLength()];
        bool allRowsComplete = true;
        for (int caseIndex = 0; caseIndex < rows.Length; caseIndex++)
        {
            JsonElement caseDefinition = cases[caseIndex];
            JsonElement matrix = matrixRows[caseIndex];
            JsonElement[] accepted = attempts.EnumerateArray()
                .Where(attempt => IsAccepted(attempt)
                    && attempt.GetProperty("caseIndex").GetInt32() == caseIndex)
                .ToArray();
            int required = caseDefinition.GetProperty("requiredRepetitions").GetInt32();
            allRowsComplete &= matrix.GetProperty("complete").GetBoolean()
                && accepted.Length >= required;
            rows[caseIndex] = BuildMaterialRow(caseDefinition, accepted, required);
        }

        bool campaignComplete = string.Equals(
                RequiredText(campaign, "state"),
                "Completed",
                StringComparison.Ordinal)
            && campaign.GetProperty("matrix").GetProperty("complete").GetBoolean()
            && allRowsComplete;
        string ineligibleReason = campaignComplete
            ? ammunitionIds.Length == 1
                ? string.Empty
                : ammunitionIds.Length == 0
                    ? "NoAcceptedAmmunition"
                    : "MixedAmmunition"
            : "CampaignIncomplete";
        return new MaterialRunData(
            RequiredText(campaign, "runInstanceId"),
            campaign.GetProperty("runSeed").GetUInt64(),
            RequiredText(campaign, "state"),
            ammunitionIds.Length == 1 ? ammunitionIds[0] : string.Empty,
            string.IsNullOrEmpty(ineligibleReason),
            ineligibleReason,
            rows);
    }

    private static MaterialRowData BuildMaterialRow(
        JsonElement caseDefinition,
        JsonElement[] accepted,
        int requiredRepetitions)
    {
        Dictionary<string, int> outcomes = accepted
            .GroupBy(attempt => RequiredText(attempt, "outcome"), StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.Count(), StringComparer.Ordinal);
        return new MaterialRowData(
            caseDefinition.GetRawText(),
            RequiredText(caseDefinition, "caseId"),
            RequiredText(caseDefinition, "material"),
            caseDefinition.GetProperty("armorClass").GetInt32(),
            caseDefinition.GetProperty("layerCount").GetInt32(),
            caseDefinition.GetProperty("plateThicknessMetres").GetDouble(),
            requiredRepetitions,
            accepted.Length,
            accepted.Count(attempt => attempt.GetProperty("reachedBackstop").GetBoolean()),
            MeanOrZero(accepted, "velocityFraction"),
            MeanLayerCountOrZero(accepted),
            MeanOrZero(accepted, "physicalTransitionCount"),
            MaximumOrZero(accepted, "maximumMassClosureErrorKilograms"),
            MaximumOrZero(accepted, "maximumEnergyClosureErrorJoules"),
            outcomes);
    }

    private static ProtocolRunData[] BuildProtocolRuns(RunDocument run)
    {
        if (run.ProtocolResult.ValueKind != JsonValueKind.Object)
        {
            return Array.Empty<ProtocolRunData>();
        }
        string runInstanceId = RequiredText(run.Campaign, "runInstanceId");
        ulong runSeed = run.Campaign.GetProperty("runSeed").GetUInt64();
        return run.ProtocolResult.GetProperty("screenings").EnumerateArray()
            .Select(screening => new ProtocolRunData(
                runInstanceId,
                runSeed,
                RequiredText(screening, "threatId"),
                RequiredText(screening, "protectionClass"),
                RequiredText(screening, "resultStatus"),
                RequiredText(screening, "observedOutcome"),
                screening.GetProperty("complete").GetBoolean(),
                screening.GetProperty("qualifyingShotCount").GetInt32(),
                screening.GetProperty("sampleCount").GetInt32(),
                screening.GetProperty("invalidatedSampleCount").GetInt32(),
                screening.GetProperty("currentThroughPenetrationCount").GetInt32(),
                screening.GetProperty("currentNoThroughPenetrationCount").GetInt32(),
                screening.GetProperty("meanObservedProtocolVelocityMetresPerSecond").GetDouble()))
            .ToArray();
    }

    private static JsonObject BuildDocument(
        int sourceReportCount,
        int uniqueRunCount,
        MaterialRunData[] materialRuns,
        ProtocolRunData[] protocolRuns)
    {
        var document = new JsonObject
        {
            ["documentSchema"] = 1,
            ["documentType"] = "BallisticsLabCrossReportComparison",
            ["pluginVersion"] = LabBuild.PluginVersion,
            ["simulationEvidenceOnly"] = true,
            ["certificationClaim"] = false,
            ["sourceReportCount"] = sourceReportCount,
            ["uniqueCampaignRunCount"] = uniqueRunCount,
            ["comparisonBasis"] = "Exact run identity and exact ammunition template; no material ranking is inferred from ineligible runs.",
            ["materialComparisonRuns"] = BuildMaterialRuns(materialRuns),
            ["materialCohorts"] = BuildMaterialCohorts(materialRuns),
            ["protocolScreeningRuns"] = BuildProtocolRuns(protocolRuns),
            ["protocolThreatSummaries"] = BuildProtocolSummaries(protocolRuns)
        };
        return document;
    }

    private static JsonArray BuildMaterialRuns(MaterialRunData[] runs)
    {
        var result = new JsonArray();
        foreach (MaterialRunData run in runs.OrderBy(value => value.RunInstanceId, StringComparer.Ordinal))
        {
            var rows = new JsonArray();
            foreach (MaterialRowData row in run.Rows)
            {
                rows.Add(BuildMaterialRowNode(row));
            }
            result.Add(new JsonObject
            {
                ["runInstanceId"] = run.RunInstanceId,
                ["runSeed"] = run.RunSeed,
                ["campaignState"] = run.CampaignState,
                ["eligibleForComparison"] = run.Eligible,
                ["ineligibleReason"] = run.IneligibleReason,
                ["ammunitionTemplateId"] = run.AmmunitionTemplateId,
                ["materials"] = rows
            });
        }
        return result;
    }

    private static JsonArray BuildMaterialCohorts(MaterialRunData[] runs)
    {
        var result = new JsonArray();
        foreach (IGrouping<string, MaterialRunData> cohort in runs
            .Where(run => run.Eligible)
            .GroupBy(run => run.AmmunitionTemplateId, StringComparer.Ordinal)
            .OrderBy(group => group.Key, StringComparer.Ordinal))
        {
            MaterialRunData[] cohortRuns = cohort.ToArray();
            var rows = new JsonArray();
            foreach (IGrouping<string, MaterialRowData> material in cohortRuns
                .SelectMany(run => run.Rows)
                .GroupBy(row => row.CaseId, StringComparer.Ordinal)
                .OrderBy(group => group.First().Material, StringComparer.Ordinal))
            {
                rows.Add(BuildAggregateMaterialRow(material.ToArray()));
            }
            result.Add(new JsonObject
            {
                ["ammunitionTemplateId"] = cohort.Key,
                ["runCount"] = cohortRuns.Length,
                ["materials"] = rows
            });
        }
        return result;
    }

    private static bool MaterialCohortsAreCompatible(
        MaterialRunData[] runs,
        out string failure)
    {
        failure = string.Empty;
        foreach (IGrouping<string, MaterialRunData> cohort in runs
            .Where(run => run.Eligible)
            .GroupBy(run => run.AmmunitionTemplateId, StringComparer.Ordinal))
        {
            MaterialRunData[] values = cohort.ToArray();
            MaterialRowData[] expected = values[0].Rows;
            for (int runIndex = 1; runIndex < values.Length; runIndex++)
            {
                MaterialRowData[] actual = values[runIndex].Rows;
                if (actual.Length != expected.Length)
                {
                    failure = "material cohort " + cohort.Key
                        + " changes fixture definitions between runs";
                    return false;
                }
                for (int rowIndex = 0; rowIndex < expected.Length; rowIndex++)
                {
                    if (!SameFixtureDefinition(expected[rowIndex], actual[rowIndex]))
                    {
                        failure = "material cohort " + cohort.Key
                            + " changes fixture definitions between runs";
                        return false;
                    }
                }
            }
        }
        return true;
    }

    private static bool SameFixtureDefinition(MaterialRowData left, MaterialRowData right)
    {
        JsonNode? leftNode = JsonNode.Parse(left.CaseDefinitionJson);
        JsonNode? rightNode = JsonNode.Parse(right.CaseDefinitionJson);
        return JsonNode.DeepEquals(leftNode, rightNode);
    }

    private static JsonObject BuildAggregateMaterialRow(MaterialRowData[] rows)
    {
        MaterialRowData first = rows[0];
        int totalAccepted = rows.Sum(row => row.AcceptedShotCount);
        var outcomes = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach (MaterialRowData row in rows)
        {
            foreach (KeyValuePair<string, int> outcome in row.OutcomeCounts)
            {
                outcomes[outcome.Key] = outcomes.TryGetValue(outcome.Key, out int current)
                    ? current + outcome.Value
                    : outcome.Value;
            }
        }
        return new JsonObject
        {
            ["caseId"] = first.CaseId,
            ["material"] = first.Material,
            ["armorClass"] = first.ArmorClass,
            ["layerCount"] = first.LayerCount,
            ["plateThicknessMetres"] = first.PlateThicknessMetres,
            ["runCount"] = rows.Length,
            ["acceptedShotCount"] = totalAccepted,
            ["backstopReachCount"] = rows.Sum(row => row.BackstopReachCount),
            ["meanVelocityFraction"] = WeightedMean(rows, row => row.MeanVelocityFraction),
            ["meanHitLayers"] = WeightedMean(rows, row => row.MeanHitLayers),
            ["meanPhysicalTransitionCount"] = WeightedMean(
                rows,
                row => row.MeanPhysicalTransitionCount),
            ["maximumMassClosureErrorKilograms"] = rows.Max(
                row => row.MaximumMassClosureErrorKilograms),
            ["maximumEnergyClosureErrorJoules"] = rows.Max(
                row => row.MaximumEnergyClosureErrorJoules),
            ["outcomes"] = BuildOutcomeNode(outcomes)
        };
    }

    private static JsonObject BuildMaterialRowNode(MaterialRowData row)
    {
        return new JsonObject
        {
            ["caseId"] = row.CaseId,
            ["material"] = row.Material,
            ["armorClass"] = row.ArmorClass,
            ["layerCount"] = row.LayerCount,
            ["plateThicknessMetres"] = row.PlateThicknessMetres,
            ["requiredShotCount"] = row.RequiredShotCount,
            ["acceptedShotCount"] = row.AcceptedShotCount,
            ["backstopReachCount"] = row.BackstopReachCount,
            ["meanVelocityFraction"] = row.MeanVelocityFraction,
            ["meanHitLayers"] = row.MeanHitLayers,
            ["meanPhysicalTransitionCount"] = row.MeanPhysicalTransitionCount,
            ["maximumMassClosureErrorKilograms"] = row.MaximumMassClosureErrorKilograms,
            ["maximumEnergyClosureErrorJoules"] = row.MaximumEnergyClosureErrorJoules,
            ["outcomes"] = BuildOutcomeNode(row.OutcomeCounts)
        };
    }

    private static JsonObject BuildOutcomeNode(IReadOnlyDictionary<string, int> outcomes)
    {
        var node = new JsonObject();
        foreach (KeyValuePair<string, int> outcome in outcomes.OrderBy(
            pair => pair.Key,
            StringComparer.Ordinal))
        {
            node[outcome.Key] = outcome.Value;
        }
        return node;
    }

    private static JsonArray BuildProtocolRuns(ProtocolRunData[] runs)
    {
        var result = new JsonArray();
        foreach (ProtocolRunData run in runs
            .OrderBy(value => value.ThreatId, StringComparer.Ordinal)
            .ThenBy(value => value.RunInstanceId, StringComparer.Ordinal))
        {
            result.Add(new JsonObject
            {
                ["runInstanceId"] = run.RunInstanceId,
                ["runSeed"] = run.RunSeed,
                ["threatId"] = run.ThreatId,
                ["protectionClass"] = run.ProtectionClass,
                ["resultStatus"] = run.ResultStatus,
                ["observedOutcome"] = run.ObservedOutcome,
                ["complete"] = run.Complete,
                ["qualifyingShotCount"] = run.QualifyingShotCount,
                ["sampleCount"] = run.SampleCount,
                ["invalidatedSampleCount"] = run.InvalidatedSampleCount,
                ["throughPenetrationCount"] = run.ThroughPenetrationCount,
                ["noThroughPenetrationCount"] = run.NoThroughPenetrationCount,
                ["meanObservedProtocolVelocityMetresPerSecond"] = run.MeanProtocolVelocity
            });
        }
        return result;
    }

    private static JsonArray BuildProtocolSummaries(ProtocolRunData[] runs)
    {
        var result = new JsonArray();
        foreach (IGrouping<string, ProtocolRunData> group in runs
            .GroupBy(run => run.ThreatId, StringComparer.Ordinal)
            .OrderBy(value => value.Key, StringComparer.Ordinal))
        {
            ProtocolRunData[] values = group.ToArray();
            result.Add(new JsonObject
            {
                ["threatId"] = group.Key,
                ["protectionClass"] = values[0].ProtectionClass,
                ["runCount"] = values.Length,
                ["completeRunCount"] = values.Count(value => value.Complete),
                ["qualifyingShotCount"] = values.Sum(value => value.QualifyingShotCount),
                ["invalidatedSampleCount"] = values.Sum(value => value.InvalidatedSampleCount),
                ["throughPenetrationCount"] = values.Sum(value => value.ThroughPenetrationCount),
                ["noThroughPenetrationCount"] = values.Sum(value => value.NoThroughPenetrationCount),
                ["meanObservedProtocolVelocityMetresPerSecond"] = WeightedProtocolVelocity(values),
                ["simulationEvidenceOnly"] = true,
                ["certificationClaim"] = false
            });
        }
        return result;
    }

    private static double WeightedProtocolVelocity(ProtocolRunData[] runs)
    {
        int count = runs.Sum(run => run.QualifyingShotCount);
        return count == 0
            ? 0d
            : runs.Sum(run => run.MeanProtocolVelocity * run.QualifyingShotCount) / count;
    }

    private static string NewTemporaryDirectory(string suffix)
    {
        string directory = Path.Combine(
            Path.GetTempPath(),
            "BallisticsLab.Comparison." + suffix + "." + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        return directory;
    }

    private static string WriteReport(
        string directory,
        string name,
        CampaignDefinition definition,
        CampaignRunSnapshot snapshot)
    {
        string path = Path.Combine(directory, "BallisticsLab-" + name + ".json");
        File.WriteAllText(
            path,
            PhysicalReportDocumentWriter.Build(
                "[]",
                Array.Empty<PhysicalTransitionRecord>(),
                definition,
                snapshot));
        return path;
    }

    private static void CompleteMaterialCampaign(
        CampaignRunTracker tracker,
        CampaignDefinition definition,
        string ammunitionTemplateId,
        string finalCaseAmmunitionTemplateId,
        long firstFixtureId)
    {
        tracker.Start();
        for (int caseIndex = 0; caseIndex < definition.Cases.Count; caseIndex++)
        {
            string ammunition = caseIndex == definition.Cases.Count - 1
                    && !string.IsNullOrEmpty(finalCaseAmmunitionTemplateId)
                ? finalCaseAmmunitionTemplateId
                : ammunitionTemplateId;
            AppendAcceptedMaterialCase(
                tracker,
                definition,
                caseIndex,
                ammunition,
                firstFixtureId + caseIndex * 10L);
        }
    }

    private static void AppendAcceptedMaterialCase(
        CampaignRunTracker tracker,
        CampaignDefinition definition,
        int caseIndex,
        string ammunitionTemplateId,
        long fixtureId)
    {
        CampaignRunSnapshot before = tracker.Snapshot();
        if (before.CurrentCaseIndex != caseIndex || !tracker.AttachFixture(fixtureId))
        {
            throw new InvalidOperationException("Synthetic material campaign cursor is invalid.");
        }
        CampaignCaseDefinition campaignCase = definition.Cases[caseIndex];
        for (int repetition = 0; repetition < campaignCase.RequiredRepetitions; repetition++)
        {
            CampaignAttemptRecord? attempt = tracker.RecordShot(CreateEvidence(
                campaignCase,
                fixtureId,
                "material-" + caseIndex.ToString(CultureInfo.InvariantCulture)
                    + '-' + repetition.ToString(CultureInfo.InvariantCulture),
                ammunitionTemplateId,
                protocolEvidence: null));
            if (attempt?.Status != CampaignAttemptStatus.Accepted)
            {
                throw new InvalidOperationException("Synthetic material shot was not accepted.");
            }
            CampaignRunSnapshot after = tracker.Snapshot();
            if (after.State == CampaignRunState.AwaitingReset
                && !tracker.ConfirmReset(fixtureId))
            {
                throw new InvalidOperationException("Synthetic material reset failed.");
            }
        }
    }

    private static CampaignShotEvidence CreateEvidence(
        CampaignCaseDefinition campaignCase,
        long fixtureId,
        string chainId,
        string ammunitionTemplateId,
        ProtocolShotEvidence? protocolEvidence)
    {
        string templateId = campaignCase.SelectorKind == CampaignFixtureSelectorKind.ExactTemplate
            ? campaignCase.TemplateId
            : "synthetic-" + campaignCase.CaseId;
        string material = campaignCase.SelectorKind
                == CampaignFixtureSelectorKind.MaterialAndArmorClass
            ? campaignCase.Material
            : "Synthetic";
        int armorClass = campaignCase.ArmorClass > 0 ? campaignCase.ArmorClass : 4;
        int[] layers = Enumerable.Range(0, campaignCase.LayerCount).ToArray();
        return new CampaignShotEvidence(
            fixtureId,
            chainId,
            templateId,
            material,
            armorClass,
            campaignCase.LayerCount,
            rootFireIndex: (int)(fixtureId % int.MaxValue),
            observedRootRandomSeed: (int)((fixtureId + 1L) % int.MaxValue),
            rootShooterProfileId: "synthetic-shooter",
            ammunitionTemplateId,
            velocityFraction: 1d,
            layers,
            outcome: "Stopped",
            reachedBackstop: campaignCase.RequireBackstopEvidence,
            physicalTransitionCount: campaignCase.RequirePhysicalEvidence ? 1 : 0,
            conservationRecordCount: campaignCase.RequireConservationEvidence ? 1 : 0,
            maximumMassClosureErrorKilograms: 0d,
            maximumEnergyClosureErrorJoules: 0d,
            protocolEvidence);
    }

    private static double WeightedMean(
        MaterialRowData[] rows,
        Func<MaterialRowData, double> selector)
    {
        int count = rows.Sum(row => row.AcceptedShotCount);
        return count == 0
            ? 0d
            : rows.Sum(row => selector(row) * row.AcceptedShotCount) / count;
    }

    private static double MeanOrZero(JsonElement[] attempts, string propertyName)
    {
        return attempts.Length == 0
            ? 0d
            : attempts.Average(attempt => attempt.GetProperty(propertyName).GetDouble());
    }

    private static double MeanLayerCountOrZero(JsonElement[] attempts)
    {
        return attempts.Length == 0
            ? 0d
            : attempts.Average(attempt => attempt.GetProperty("hitLayers").GetArrayLength());
    }

    private static double MaximumOrZero(JsonElement[] attempts, string propertyName)
    {
        return attempts.Length == 0
            ? 0d
            : attempts.Max(attempt => attempt.GetProperty(propertyName).GetDouble());
    }

    private static bool IsAccepted(JsonElement attempt)
    {
        return string.Equals(
            RequiredText(attempt, "status"),
            "Accepted",
            StringComparison.Ordinal);
    }

    private static bool JsonEquals(JsonElement left, JsonElement right)
    {
        JsonNode? leftNode = JsonNode.Parse(left.GetRawText());
        JsonNode? rightNode = JsonNode.Parse(right.GetRawText());
        return JsonNode.DeepEquals(leftNode, rightNode);
    }

    private static string RequiredText(JsonElement element, string propertyName)
    {
        string value = element.GetProperty(propertyName).GetString() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new InvalidDataException("Required comparison field is empty: " + propertyName);
        }
        return value;
    }

    private sealed class RunDocument
    {
        internal RunDocument(string sourceName, JsonElement campaign, JsonElement protocolResult)
        {
            SourceName = sourceName;
            Campaign = campaign;
            ProtocolResult = protocolResult;
        }

        internal string SourceName { get; }
        internal JsonElement Campaign { get; }
        internal JsonElement ProtocolResult { get; }
    }

    private sealed class MaterialRunData
    {
        internal MaterialRunData(
            string runInstanceId,
            ulong runSeed,
            string campaignState,
            string ammunitionTemplateId,
            bool eligible,
            string ineligibleReason,
            MaterialRowData[] rows)
        {
            RunInstanceId = runInstanceId;
            RunSeed = runSeed;
            CampaignState = campaignState;
            AmmunitionTemplateId = ammunitionTemplateId;
            Eligible = eligible;
            IneligibleReason = ineligibleReason;
            Rows = rows;
        }

        internal string RunInstanceId { get; }
        internal ulong RunSeed { get; }
        internal string CampaignState { get; }
        internal string AmmunitionTemplateId { get; }
        internal bool Eligible { get; }
        internal string IneligibleReason { get; }
        internal MaterialRowData[] Rows { get; }
    }

    private sealed class MaterialRowData
    {
        internal MaterialRowData(
            string caseDefinitionJson,
            string caseId,
            string material,
            int armorClass,
            int layerCount,
            double plateThicknessMetres,
            int requiredShotCount,
            int acceptedShotCount,
            int backstopReachCount,
            double meanVelocityFraction,
            double meanHitLayers,
            double meanPhysicalTransitionCount,
            double maximumMassClosureErrorKilograms,
            double maximumEnergyClosureErrorJoules,
            IReadOnlyDictionary<string, int> outcomeCounts)
        {
            CaseDefinitionJson = caseDefinitionJson;
            CaseId = caseId;
            Material = material;
            ArmorClass = armorClass;
            LayerCount = layerCount;
            PlateThicknessMetres = plateThicknessMetres;
            RequiredShotCount = requiredShotCount;
            AcceptedShotCount = acceptedShotCount;
            BackstopReachCount = backstopReachCount;
            MeanVelocityFraction = meanVelocityFraction;
            MeanHitLayers = meanHitLayers;
            MeanPhysicalTransitionCount = meanPhysicalTransitionCount;
            MaximumMassClosureErrorKilograms = maximumMassClosureErrorKilograms;
            MaximumEnergyClosureErrorJoules = maximumEnergyClosureErrorJoules;
            OutcomeCounts = outcomeCounts;
        }

        internal string CaseDefinitionJson { get; }
        internal string CaseId { get; }
        internal string Material { get; }
        internal int ArmorClass { get; }
        internal int LayerCount { get; }
        internal double PlateThicknessMetres { get; }
        internal int RequiredShotCount { get; }
        internal int AcceptedShotCount { get; }
        internal int BackstopReachCount { get; }
        internal double MeanVelocityFraction { get; }
        internal double MeanHitLayers { get; }
        internal double MeanPhysicalTransitionCount { get; }
        internal double MaximumMassClosureErrorKilograms { get; }
        internal double MaximumEnergyClosureErrorJoules { get; }
        internal IReadOnlyDictionary<string, int> OutcomeCounts { get; }
    }

    private sealed class ProtocolRunData
    {
        internal ProtocolRunData(
            string runInstanceId,
            ulong runSeed,
            string threatId,
            string protectionClass,
            string resultStatus,
            string observedOutcome,
            bool complete,
            int qualifyingShotCount,
            int sampleCount,
            int invalidatedSampleCount,
            int throughPenetrationCount,
            int noThroughPenetrationCount,
            double meanProtocolVelocity)
        {
            RunInstanceId = runInstanceId;
            RunSeed = runSeed;
            ThreatId = threatId;
            ProtectionClass = protectionClass;
            ResultStatus = resultStatus;
            ObservedOutcome = observedOutcome;
            Complete = complete;
            QualifyingShotCount = qualifyingShotCount;
            SampleCount = sampleCount;
            InvalidatedSampleCount = invalidatedSampleCount;
            ThroughPenetrationCount = throughPenetrationCount;
            NoThroughPenetrationCount = noThroughPenetrationCount;
            MeanProtocolVelocity = meanProtocolVelocity;
        }

        internal string RunInstanceId { get; }
        internal ulong RunSeed { get; }
        internal string ThreatId { get; }
        internal string ProtectionClass { get; }
        internal string ResultStatus { get; }
        internal string ObservedOutcome { get; }
        internal bool Complete { get; }
        internal int QualifyingShotCount { get; }
        internal int SampleCount { get; }
        internal int InvalidatedSampleCount { get; }
        internal int ThroughPenetrationCount { get; }
        internal int NoThroughPenetrationCount { get; }
        internal double MeanProtocolVelocity { get; }
    }
}

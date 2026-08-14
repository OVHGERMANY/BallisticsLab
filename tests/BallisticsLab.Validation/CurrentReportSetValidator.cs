using System.Globalization;
using System.Text.Json;

internal static class CurrentReportSetValidator
{
    internal static IReadOnlyList<string> Select(
        IEnumerable<string> reportFiles,
        int expectedSchema,
        string expectedPluginVersion)
    {
        List<string> selected = new();
        foreach (string reportFile in reportFiles)
        {
            using JsonDocument candidate = JsonDocument.Parse(File.ReadAllText(reportFile));
            bool currentSchema = candidate.RootElement.TryGetProperty(
                    "schema",
                    out JsonElement schemaElement)
                && schemaElement.TryGetInt32(out int schema)
                && schema == expectedSchema;
            string pluginVersion = candidate.RootElement.TryGetProperty(
                    "pluginVersion",
                    out JsonElement versionElement)
                ? versionElement.GetString() ?? string.Empty
                : string.Empty;
            bool hasRecords = candidate.RootElement.TryGetProperty(
                    "records",
                    out JsonElement records)
                && records.ValueKind == JsonValueKind.Array
                && records.GetArrayLength() > 0;
            bool hasPhysicalTransitions = candidate.RootElement.TryGetProperty(
                    "physicalTransitions",
                    out JsonElement transitions)
                && transitions.ValueKind == JsonValueKind.Array
                && transitions.GetArrayLength() > 0;
            bool hasCampaignAttempts = candidate.RootElement.TryGetProperty(
                    "campaign",
                    out JsonElement campaign)
                && campaign.ValueKind == JsonValueKind.Object
                && campaign.TryGetProperty("attempts", out JsonElement attempts)
                && attempts.ValueKind == JsonValueKind.Array
                && attempts.GetArrayLength() > 0;
            if (currentSchema
                && (hasRecords || hasPhysicalTransitions || hasCampaignAttempts)
                && string.Equals(pluginVersion, expectedPluginVersion, StringComparison.Ordinal))
            {
                selected.Add(reportFile);
            }
        }

        return selected;
    }

    internal static bool ValidateInvariants(
        IEnumerable<string> reportFiles,
        int expectedSchema,
        string expectedPluginVersion,
        out int validatedCount,
        out string failure)
    {
        IReadOnlyList<string> currentReports = Select(
            reportFiles,
            expectedSchema,
            expectedPluginVersion);
        validatedCount = 0;
        failure = string.Empty;
        if (currentReports.Count == 0)
        {
            failure = "no nonempty current-build reports were found";
            return false;
        }

        foreach (string currentReport in currentReports)
        {
            validatedCount++;
            if (!ReportInvariantValidator.Validate(currentReport, out string reportFailure))
            {
                failure = Path.GetFileName(currentReport) + ": " + reportFailure;
                return false;
            }
        }

        return true;
    }

    internal static bool RejectsCorruptEarlierCurrentReport()
    {
        return TestSyntheticSet(
            "0.2.6",
            expectedValid: false,
            expectedCount: 1,
            expectedFailure: "decision penetration");
    }

    internal static bool IgnoresCorruptHistoricalReport()
    {
        return TestSyntheticSet(
            "0.2.5",
            expectedValid: true,
            expectedCount: 1,
            expectedFailure: string.Empty);
    }

    internal static bool SelectsPhysicalOnlySchemaFourReport()
    {
        string directory = Path.Combine(
            Path.GetTempPath(),
            "BallisticsLab.PhysicalReportSet." + Guid.NewGuid().ToString("N", CultureInfo.InvariantCulture));
        Directory.CreateDirectory(directory);
        try
        {
            string report = Path.Combine(directory, "BallisticsLab-physical.json");
            File.WriteAllText(
                report,
                "{\"schema\":4,\"pluginVersion\":\"0.2.8\",\"records\":[],"
                    + "\"physicalTransitions\":[{\"transitionId\":\"physical-only\"}]}");
            IReadOnlyList<string> selected = Select(new[] { report }, 4, "0.2.8");
            return selected.Count == 1 && selected[0] == report;
        }
        finally
        {
            Directory.Delete(directory, true);
        }
    }

    internal static bool SelectsCampaignOnlySchemaFourReport()
    {
        string directory = Path.Combine(
            Path.GetTempPath(),
            "BallisticsLab.CampaignReportSet." + Guid.NewGuid().ToString("N", CultureInfo.InvariantCulture));
        Directory.CreateDirectory(directory);
        try
        {
            string report = Path.Combine(directory, "BallisticsLab-campaign.json");
            File.WriteAllText(
                report,
                "{\"schema\":4,\"pluginVersion\":\"0.2.8\",\"records\":[],"
                    + "\"physicalTransitions\":[],\"campaign\":{\"attempts\":[{}]}}");
            IReadOnlyList<string> selected = Select(new[] { report }, 4, "0.2.8");
            return selected.Count == 1 && selected[0] == report;
        }
        finally
        {
            Directory.Delete(directory, true);
        }
    }

    private static bool TestSyntheticSet(
        string earlierVersion,
        bool expectedValid,
        int expectedCount,
        string expectedFailure)
    {
        string directory = Path.Combine(
            Path.GetTempPath(),
            "BallisticsLab.ReportSet." + Guid.NewGuid().ToString("N", CultureInfo.InvariantCulture));
        Directory.CreateDirectory(directory);
        try
        {
            string earlier = Path.Combine(directory, "BallisticsLab-earlier.json");
            string latest = Path.Combine(directory, "BallisticsLab-latest.json");
            File.WriteAllText(
                earlier,
                ReportInvariantValidator.SyntheticReportJson(
                    corruptPenetration: true,
                    pluginVersion: earlierVersion));
            File.WriteAllText(
                latest,
                ReportInvariantValidator.SyntheticReportJson(
                    corruptPenetration: false,
                    pluginVersion: "0.2.6"));

            bool valid = ValidateInvariants(
                new[] { earlier, latest },
                expectedSchema: 3,
                expectedPluginVersion: "0.2.6",
                out int count,
                out string failure);
            return valid == expectedValid
                && count == expectedCount
                && (string.IsNullOrEmpty(expectedFailure)
                    ? string.IsNullOrEmpty(failure)
                    : failure.Contains(expectedFailure, StringComparison.OrdinalIgnoreCase));
        }
        finally
        {
            Directory.Delete(directory, true);
        }
    }
}

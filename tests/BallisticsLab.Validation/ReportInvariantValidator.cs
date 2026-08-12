using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;

internal static class ReportInvariantValidator
{
    private static readonly string[] FiniteNonNegativeFields =
    {
        "angleDegrees", "impactSpeed", "templateSpeed", "fraction",
        "incomingDamage", "incomingPenetration", "decisionDamage", "decisionPenetration",
        "armorRealResistance", "armorClassResistance", "armorCf", "penetrationChancePercent",
        "durabilityBefore", "durabilityAfter", "fixtureMaximumDurability",
        "bodyHealthBefore", "bodyHealthAfter", "layerSpacing", "colliderThickness",
        "continuationPenetrationFactor", "continuationVelocityFactor",
        "continuationOutcomeFactor", "continuationArmorCf", "continuationDamageBefore",
        "continuationPenetrationBefore", "continuationDamageAfter",
        "continuationPenetrationAfter"
    };

    internal static bool AcceptsSyntheticReport()
    {
        return ValidateSyntheticReport(SyntheticCorruption.None, out _);
    }

    internal static bool RejectsIncorrectFalloff()
    {
        return !ValidateSyntheticReport(SyntheticCorruption.Penetration, out string failure)
            && failure.Contains("decision penetration", StringComparison.OrdinalIgnoreCase);
    }

    internal static bool RejectsDetachedTrajectoryEndpoint()
    {
        return !ValidateSyntheticReport(SyntheticCorruption.Path, out string failure)
            && failure.Contains("trajectory endpoint", StringComparison.OrdinalIgnoreCase);
    }

    internal static bool RejectsMissingForwardHitState()
    {
        return !ValidateSyntheticReport(SyntheticCorruption.ForwardState, out string failure)
            && failure.Contains("forward-hit state", StringComparison.OrdinalIgnoreCase);
    }

    internal static bool RejectsMismatchedContinuationSource()
    {
        return !ValidateSyntheticLineage(
                mismatchedSource: true,
                mismatchedRootSeed: false,
                out string failure)
            && failure.Contains("continuation source", StringComparison.OrdinalIgnoreCase);
    }

    internal static bool RejectsChangedRootIdentity()
    {
        return !ValidateSyntheticLineage(
                mismatchedSource: false,
                mismatchedRootSeed: true,
                out string failure)
            && failure.Contains("root random seed", StringComparison.OrdinalIgnoreCase);
    }

    internal static string SyntheticReportJson(bool corruptPenetration, string pluginVersion)
    {
        return CreateSyntheticReportJson(
            corruptPenetration ? SyntheticCorruption.Penetration : SyntheticCorruption.None,
            pluginVersion);
    }

    internal static bool Validate(string jsonPath, out string failure)
    {
        failure = string.Empty;
        try
        {
            using JsonDocument document = JsonDocument.Parse(File.ReadAllText(jsonPath));
            if (!document.RootElement.TryGetProperty("records", out JsonElement records)
                || records.ValueKind != JsonValueKind.Array
                || records.GetArrayLength() == 0)
            {
                failure = "report contains no record array";
                return false;
            }

            HashSet<long> sequences = new();
            Dictionary<string, List<ChainLink>> chains = new(StringComparer.Ordinal);
            int row = 0;
            foreach (JsonElement record in records.EnumerateArray())
            {
                row++;
                if (!TryInt64(record, "sequence", out long sequence) || !sequences.Add(sequence))
                {
                    failure = Row(row, "sequence is missing or duplicated");
                    return false;
                }
                if (!TryInt32(record, "parentDepth", out int parentDepth) || parentDepth < 0)
                {
                    failure = Row(row, "parent depth is invalid");
                    return false;
                }
                if (!TryInt32(record, "fragmentCount", out int fragmentCount) || fragmentCount < 0)
                {
                    failure = Row(row, "fragment count is invalid");
                    return false;
                }
                if (!TryInt32(record, "fireIndex", out int fireIndex) || fireIndex < 0)
                {
                    failure = Row(row, "fire index is invalid");
                    return false;
                }
                if (!TryInt32(record, "fragmentIndex", out int fragmentIndex) || fragmentIndex < 0)
                {
                    failure = Row(row, "fragment index is invalid");
                    return false;
                }
                if (!TryInt32(record, "rootRandomSeed", out int rootRandomSeed) || rootRandomSeed < 0)
                {
                    failure = Row(row, "root random seed is invalid");
                    return false;
                }
                if (!TryInt32(record, "fixtureId", out int fixtureId) || fixtureId < 0)
                {
                    failure = Row(row, "fixture identity is invalid");
                    return false;
                }
                if (!TryInt32(record, "layer", out int layer) || layer < -1)
                {
                    failure = Row(row, "fixture layer is invalid");
                    return false;
                }

                foreach (string field in FiniteNonNegativeFields)
                {
                    if (!TryFinite(record, field, out double value) || value < 0d)
                    {
                        failure = Row(row, field + " is missing, non-finite, or negative");
                        return false;
                    }
                }

                if (!TryFinite(record, "angleDegrees", out double angle) || angle > 90.0001d)
                {
                    failure = Row(row, "impact angle is outside zero to ninety degrees");
                    return false;
                }

                double impactSpeed = Required(record, "impactSpeed");
                double templateSpeed = Required(record, "templateSpeed");
                double fraction = Required(record, "fraction");
                double expectedFraction = templateSpeed > 0d ? impactSpeed / templateSpeed : 0d;
                if (!Nearly(fraction, expectedFraction))
                {
                    failure = Row(row, "velocity fraction does not equal impact speed divided by template speed");
                    return false;
                }

                if (!record.TryGetProperty("isForwardHit", out JsonElement forward)
                    || forward.ValueKind is not (JsonValueKind.True or JsonValueKind.False))
                {
                    failure = Row(row, "forward-hit state is missing or invalid");
                    return false;
                }
                bool isForwardHit = forward.GetBoolean();
                if (isForwardHit && templateSpeed > 0d)
                {
                    double incomingDamage = Required(record, "incomingDamage");
                    double incomingPenetration = Required(record, "incomingPenetration");
                    double expectedDamage = incomingDamage * Math.Pow(fraction, 0.4d);
                    double expectedPenetration = incomingPenetration * Math.Pow(fraction, 1.4d);
                    if (!Nearly(Required(record, "decisionDamage"), expectedDamage))
                    {
                        failure = Row(row, "decision damage does not follow the locked 0.4 curve");
                        return false;
                    }
                    if (!Nearly(Required(record, "decisionPenetration"), expectedPenetration))
                    {
                        failure = Row(row, "decision penetration does not follow the locked 1.4 curve");
                        return false;
                    }
                }

                string targetKind = String(record, "targetKind");
                if (string.Equals(targetKind, "FIXTURE PLATE", StringComparison.Ordinal)
                    && Required(record, "durabilityAfter") > Required(record, "durabilityBefore") + 0.0001d)
                {
                    failure = Row(row, "fixture durability increased after a hit");
                    return false;
                }

                string continuationKind = String(record, "continuationKind");
                int continuationSourceFixtureId = 0;
                int continuationSourceLayer = -1;
                if (!string.IsNullOrEmpty(continuationKind))
                {
                    if (parentDepth <= 0)
                    {
                        failure = Row(row, "continuation record has no parent depth");
                        return false;
                    }
                    if (!Nearly(Required(record, "incomingDamage"), Required(record, "continuationDamageAfter"))
                        || !Nearly(
                            Required(record, "incomingPenetration"),
                            Required(record, "continuationPenetrationAfter")))
                    {
                        failure = Row(row, "continuation input differs from the corrected child output");
                        return false;
                    }
                    if (!TryInt32(
                            record,
                            "continuationSourceFixtureId",
                            out continuationSourceFixtureId)
                        || continuationSourceFixtureId <= 0
                        || !TryInt32(
                            record,
                            "continuationSourceLayer",
                            out continuationSourceLayer)
                        || continuationSourceLayer < 0)
                    {
                        failure = Row(row, "continuation source fixture or layer is invalid");
                        return false;
                    }
                }

                if (!TryPoint(record, "hitPoint", out Point3 hitPoint)
                    || !record.TryGetProperty("path", out JsonElement path)
                    || path.ValueKind != JsonValueKind.Array
                    || path.GetArrayLength() == 0
                    || !TryPoint(path[path.GetArrayLength() - 1], out Point3 pathEnd)
                    || !Nearly(hitPoint.X, pathEnd.X)
                    || !Nearly(hitPoint.Y, pathEnd.Y)
                    || !Nearly(hitPoint.Z, pathEnd.Z))
                {
                    failure = Row(row, "trajectory endpoint does not equal hitPoint");
                    return false;
                }

                string chainId = String(record, "chainId");
                if (string.IsNullOrWhiteSpace(chainId))
                {
                    failure = Row(row, "chain identity is empty");
                    return false;
                }
                if (!chains.TryGetValue(chainId, out List<ChainLink> chain))
                {
                    chain = new List<ChainLink>();
                    chains.Add(chainId, chain);
                }
                chain.Add(new ChainLink(
                    sequence,
                    parentDepth,
                    continuationKind,
                    fireIndex,
                    fragmentIndex,
                    rootRandomSeed,
                    fixtureId,
                    layer,
                    continuationSourceFixtureId,
                    continuationSourceLayer));
            }

            foreach (KeyValuePair<string, List<ChainLink>> pair in chains)
            {
                List<ChainLink> links = pair.Value.OrderBy(link => link.Sequence).ToList();
                if (links.Select(link => link.FireIndex).Distinct().Count() != 1)
                {
                    failure = "chain " + pair.Key + " changes fire index";
                    return false;
                }
                if (links.Select(link => link.RootRandomSeed).Distinct().Count() != 1)
                {
                    failure = "chain " + pair.Key + " changes root random seed";
                    return false;
                }
                foreach (ChainLink link in links)
                {
                    if (link.ParentDepth == 0 || string.IsNullOrEmpty(link.ContinuationKind))
                    {
                        continue;
                    }
                    if (!links.Any(candidate =>
                            candidate.Sequence < link.Sequence
                            && candidate.ParentDepth == link.ParentDepth - 1
                            && candidate.FixtureId == link.ContinuationSourceFixtureId
                            && candidate.Layer == link.ContinuationSourceLayer))
                    {
                        failure = "chain " + pair.Key + " has depth "
                            + link.ParentDepth.ToString(CultureInfo.InvariantCulture)
                            + " without an earlier parent matching its continuation source";
                        return false;
                    }
                }
            }

            return true;
        }
        catch (Exception exception)
        {
            failure = exception.GetType().Name + ": " + exception.Message;
            return false;
        }
    }

    private static bool ValidateSyntheticReport(SyntheticCorruption corruption, out string failure)
    {
        string directory = Path.Combine(
            Path.GetTempPath(),
            "BallisticsLab.Invariants." + Guid.NewGuid().ToString("N", CultureInfo.InvariantCulture));
        Directory.CreateDirectory(directory);
        try
        {
            string path = Path.Combine(directory, "BallisticsLab-synthetic.json");
            File.WriteAllText(path, CreateSyntheticReportJson(corruption, "0.2.6"));
            return Validate(path, out failure);
        }
        finally
        {
            Directory.Delete(directory, true);
        }
    }

    private static bool ValidateSyntheticLineage(
        bool mismatchedSource,
        bool mismatchedRootSeed,
        out string failure)
    {
        string directory = Path.Combine(
            Path.GetTempPath(),
            "BallisticsLab.Lineage." + Guid.NewGuid().ToString("N", CultureInfo.InvariantCulture));
        Directory.CreateDirectory(directory);
        try
        {
            JsonObject root = JsonNode.Parse(CreateSyntheticReportJson(
                    SyntheticCorruption.None,
                    "0.2.6"))?.AsObject()
                ?? throw new InvalidDataException("synthetic report root is missing");
            JsonArray records = root["records"]?.AsArray()
                ?? throw new InvalidDataException("synthetic report records are missing");
            JsonObject child = records[0]?.DeepClone().AsObject()
                ?? throw new InvalidDataException("synthetic parent record is missing");
            child["sequence"] = 2;
            child["parentDepth"] = 1;
            child["continuationKind"] = "DeviationHit";
            child["fixtureId"] = 2;
            child["layer"] = 1;
            child["continuationSourceFixtureId"] = mismatchedSource ? 99 : 1;
            child["continuationSourceLayer"] = 0;
            child["continuationDamageAfter"] = 100d;
            child["continuationPenetrationAfter"] = 50d;
            if (mismatchedRootSeed)
            {
                child["rootRandomSeed"] = 8;
            }
            records.Add(child);

            string path = Path.Combine(directory, "BallisticsLab-lineage.json");
            File.WriteAllText(path, root.ToJsonString());
            return Validate(path, out failure);
        }
        finally
        {
            Directory.Delete(directory, true);
        }
    }

    private static string CreateSyntheticReportJson(
        SyntheticCorruption corruption,
        string pluginVersion)
    {
        const double impactSpeed = 500d;
        const double templateSpeed = 1000d;
        const double incomingDamage = 100d;
        const double incomingPenetration = 50d;
        double fraction = impactSpeed / templateSpeed;
        double decisionDamage = incomingDamage * Math.Pow(fraction, 0.4d);
        double decisionPenetration = incomingPenetration * Math.Pow(fraction, 1.4d);
        if (corruption == SyntheticCorruption.Penetration)
        {
            decisionPenetration += 10d;
        }

        double[] hitPoint = { 1d, 2d, 3d };
        double[] pathEnd = corruption == SyntheticCorruption.Path
            ? new[] { 4d, 5d, 6d }
            : hitPoint;
        Dictionary<string, object> record = new(StringComparer.Ordinal)
        {
            ["sequence"] = 1,
            ["chainId"] = "synthetic:1:1",
            ["fireIndex"] = 1,
            ["fragmentIndex"] = 0,
            ["fragmentCount"] = 0,
            ["parentDepth"] = 0,
            ["rootRandomSeed"] = 7,
            ["isForwardHit"] = true,
            ["targetKind"] = "FIXTURE PLATE",
            ["fixtureId"] = 1,
            ["layer"] = 0,
            ["angleDegrees"] = 0d,
            ["impactSpeed"] = impactSpeed,
            ["templateSpeed"] = templateSpeed,
            ["fraction"] = fraction,
            ["incomingDamage"] = incomingDamage,
            ["incomingPenetration"] = incomingPenetration,
            ["decisionDamage"] = decisionDamage,
            ["decisionPenetration"] = decisionPenetration,
            ["armorRealResistance"] = 30d,
            ["armorClassResistance"] = 30d,
            ["armorCf"] = 0.8d,
            ["penetrationChancePercent"] = 50d,
            ["durabilityBefore"] = 50d,
            ["durabilityAfter"] = 49d,
            ["fixtureMaximumDurability"] = 50d,
            ["bodyHealthBefore"] = 0d,
            ["bodyHealthAfter"] = 0d,
            ["layerSpacing"] = 0.15d,
            ["colliderThickness"] = 0.0127d,
            ["continuationKind"] = string.Empty,
            ["continuationSourceFixtureId"] = 0,
            ["continuationSourceLayer"] = -1,
            ["continuationPenetrationFactor"] = 1d,
            ["continuationVelocityFactor"] = 1d,
            ["continuationOutcomeFactor"] = 1d,
            ["continuationArmorCf"] = 1d,
            ["continuationDamageBefore"] = 0d,
            ["continuationPenetrationBefore"] = 0d,
            ["continuationDamageAfter"] = 0d,
            ["continuationPenetrationAfter"] = 0d,
            ["hitPoint"] = hitPoint,
            ["path"] = new[] { new[] { 0d, 2d, 3d }, pathEnd }
        };
        if (corruption == SyntheticCorruption.ForwardState)
        {
            record.Remove("isForwardHit");
        }

        return JsonSerializer.Serialize(new
        {
            schema = 3,
            pluginVersion,
            records = new[] { record }
        });
    }

    private static string Row(int row, string message)
    {
        return "row " + row.ToString(CultureInfo.InvariantCulture) + ": " + message;
    }

    private static string String(JsonElement record, string name)
    {
        return record.TryGetProperty(name, out JsonElement value) && value.ValueKind == JsonValueKind.String
            ? value.GetString() ?? string.Empty
            : string.Empty;
    }

    private static double Required(JsonElement record, string name)
    {
        if (!TryFinite(record, name, out double value))
        {
            throw new InvalidDataException(name + " is missing or non-finite");
        }
        return value;
    }

    private static bool TryFinite(JsonElement record, string name, out double value)
    {
        value = 0d;
        return record.TryGetProperty(name, out JsonElement element)
            && element.ValueKind == JsonValueKind.Number
            && element.TryGetDouble(out value)
            && !double.IsNaN(value)
            && !double.IsInfinity(value);
    }

    private static bool TryInt32(JsonElement record, string name, out int value)
    {
        value = 0;
        return record.TryGetProperty(name, out JsonElement element) && element.TryGetInt32(out value);
    }

    private static bool TryInt64(JsonElement record, string name, out long value)
    {
        value = 0;
        return record.TryGetProperty(name, out JsonElement element) && element.TryGetInt64(out value);
    }

    private static bool TryPoint(JsonElement record, string name, out Point3 point)
    {
        point = default;
        return record.TryGetProperty(name, out JsonElement element) && TryPoint(element, out point);
    }

    private static bool TryPoint(JsonElement element, out Point3 point)
    {
        point = default;
        if (element.ValueKind != JsonValueKind.Array
            || element.GetArrayLength() != 3
            || !element[0].TryGetDouble(out double x)
            || !element[1].TryGetDouble(out double y)
            || !element[2].TryGetDouble(out double z)
            || !double.IsFinite(x)
            || !double.IsFinite(y)
            || !double.IsFinite(z))
        {
            return false;
        }
        point = new Point3(x, y, z);
        return true;
    }

    private static bool Nearly(double actual, double expected)
    {
        double scale = Math.Max(1d, Math.Max(Math.Abs(actual), Math.Abs(expected)));
        return Math.Abs(actual - expected) <= scale * 0.00001d;
    }

    private enum SyntheticCorruption
    {
        None,
        Penetration,
        Path,
        ForwardState
    }

    private readonly record struct Point3(double X, double Y, double Z);
    private readonly record struct ChainLink(
        long Sequence,
        int ParentDepth,
        string ContinuationKind,
        int FireIndex,
        int FragmentIndex,
        int RootRandomSeed,
        int FixtureId,
        int Layer,
        int ContinuationSourceFixtureId,
        int ContinuationSourceLayer);
}

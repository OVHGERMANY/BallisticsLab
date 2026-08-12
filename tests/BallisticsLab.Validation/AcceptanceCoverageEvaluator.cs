using System.Globalization;
using System.Text.Json;

internal sealed class AcceptanceReportCoverage
{
    internal AcceptanceReportCoverage(
        int uniqueRecords,
        int uniqueChains,
        IReadOnlyList<AcceptanceCoverageCheck> checks)
    {
        UniqueRecords = uniqueRecords;
        UniqueChains = uniqueChains;
        Checks = checks;
        Missing = checks
            .Where(check => !check.Passed)
            .Select(check => check.Name)
            .ToArray();
    }

    internal int UniqueRecords { get; }

    internal int UniqueChains { get; }

    internal IReadOnlyList<AcceptanceCoverageCheck> Checks { get; }

    internal IReadOnlyList<string> Missing { get; }

    internal bool Complete => Missing.Count == 0;

    internal bool Passed(string name)
    {
        return Checks.Any(
            check => string.Equals(check.Name, name, StringComparison.Ordinal)
                && check.Passed);
    }
}

internal sealed class AcceptanceCoverageCheck
{
    internal AcceptanceCoverageCheck(string name, bool passed)
    {
        Name = name;
        Passed = passed;
    }

    internal string Name { get; }

    internal bool Passed { get; }
}

internal static class AcceptanceCoverageEvaluator
{
    private const string FixturePlate = "FIXTURE PLATE";
    private const string FixtureBackstop = "FIXTURE BACKSTOP";
    private const string Bot = "BOT";
    private const string ArmoredSteel = "ArmoredSteel";
    private const string GranitBr4TemplateId = "65573fa5655447403702a816";
    private const string GranitBr5TemplateId = "64afc71497cf3a403c01ff38";

    internal static AcceptanceReportCoverage Evaluate(
        IEnumerable<string> reportFiles,
        int expectedSchema,
        string expectedPluginVersion)
    {
        IReadOnlyList<string> currentReports = CurrentReportSetValidator.Select(
            reportFiles,
            expectedSchema,
            expectedPluginVersion);
        List<AcceptanceRecord> records = new();
        HashSet<string> seen = new(StringComparer.Ordinal);

        foreach (string reportFile in currentReports)
        {
            using JsonDocument report = JsonDocument.Parse(File.ReadAllText(reportFile));
            foreach (JsonElement element in report.RootElement.GetProperty("records").EnumerateArray())
            {
                AcceptanceRecord record = AcceptanceRecord.From(element);
                if (seen.Add(record.Identity))
                {
                    records.Add(record);
                }
            }
        }

        IReadOnlyList<IGrouping<string, AcceptanceRecord>> chains = records
            .Where(record => !string.IsNullOrWhiteSpace(record.ChainId))
            .GroupBy(record => record.ChainId, StringComparer.Ordinal)
            .ToArray();

        Dictionary<int, bool> layerCoverage = new();
        for (int layerCount = 1; layerCount <= 6; layerCount++)
        {
            int expectedLayers = layerCount;
            layerCoverage[layerCount] = chains.Any(
                chain => chain.Any(record => record.LayerCount == expectedLayers)
                    && CompleteLayerChain(chain, expectedLayers));
        }

        bool spacedArmor = chains.Any(chain =>
        {
            AcceptanceRecord fixture = chain.FirstOrDefault(record => record.TargetKind == FixturePlate);
            return fixture != null
                && fixture.LayerCount > 1
                && fixture.LayerSpacing > 0d
                && fixture.ColliderThickness > 0d
                && CompleteLayerChain(chain, fixture.LayerCount);
        });

        bool steelOneC6 = chains.Any(chain => SteelPresetChain(chain, 1, 6));
        bool steelTwoC3 = chains.Any(chain => SteelPresetChain(chain, 2, 3));
        bool steelThreeC4 = chains.Any(chain => SteelPresetChain(chain, 3, 4));
        bool steelThreeC6 = chains.Any(chain => SteelPresetChain(chain, 3, 6));
        bool granitBr4 = GranitPresetEvidence(records, GranitBr4TemplateId);
        bool granitBr5 = GranitPresetEvidence(records, GranitBr5TemplateId);

        AcceptanceRecord[] botRecords = records
            .Where(record => record.TargetKind == Bot)
            .ToArray();
        bool botHealth = botRecords.Any(
            record => record.HasBodyHealthBefore && record.HasBodyHealthAfter);
        bool botArmor = botRecords.Any(
            record => !string.IsNullOrWhiteSpace(record.ArmorChanges));
        bool postDeathArmor = botRecords.Any(
            record => record.HasTargetAliveBefore
                && record.HasTargetAliveAfter
                && !record.TargetAliveBefore
                && !record.TargetAliveAfter
                && !string.IsNullOrWhiteSpace(record.ArmorChanges));

        bool deviationContinuation = HasContinuationIntoLaterLayer(chains, "DeviationHit");
        bool fragmentContinuation = HasContinuationIntoLaterLayer(chains, "FragmentationHit");
        bool backstop = records.Any(record => record.TargetKind == FixtureBackstop);

        AcceptanceCoverageCheck[] checks =
        {
            new("SinglePlateChain", layerCoverage[1]),
            new("Layer1Chain", layerCoverage[1]),
            new("Layer2Chain", layerCoverage[2]),
            new("Layer3Chain", layerCoverage[3]),
            new("Layer4Chain", layerCoverage[4]),
            new("Layer5Chain", layerCoverage[5]),
            new("Layer6Chain", layerCoverage[6]),
            new("SpacedArmor", spacedArmor),
            new("Steel1LayerClass6", steelOneC6),
            new("Steel2LayerClass3", steelTwoC3),
            new("Steel3LayerClass4", steelThreeC4),
            new("Steel3LayerClass6", steelThreeC6),
            new("GranitBr4GamePreset", granitBr4),
            new("GranitBr5GamePreset", granitBr5),
            new("BotHealthTelemetry", botHealth),
            new("BotArmorTelemetry", botArmor),
            new("PostDeathArmorTelemetry", postDeathArmor),
            new("DeviationContinuationChain", deviationContinuation),
            new("FragmentContinuationChain", fragmentContinuation),
            new("BackstopRecord", backstop)
        };

        return new AcceptanceReportCoverage(records.Count, chains.Count, checks);
    }

    internal static bool CompleteSyntheticCoveragePasses()
    {
        string directory = TemporaryDirectory("CompleteCoverage");
        try
        {
            List<Dictionary<string, object>> records = new();
            AddSteelChain(records, "steel-1-c6", 1, 6, 10);
            AddSteelChain(records, "steel-2-c3", 2, 3, 20, "DeviationHit");
            AddSteelChain(records, "steel-3-c4", 3, 4, 30);
            AddSteelChain(records, "steel-3-c6", 3, 6, 40);
            AddGenericChain(records, "layers-4", 4, 50);
            AddGenericChain(records, "layers-5", 5, 60);
            AddGenericChain(records, "layers-6", 6, 70, "FragmentationHit");
            records.Add(GranitRecord("br4", GranitBr4TemplateId, 80));
            records.Add(GranitRecord("br5", GranitBr5TemplateId, 81));
            records.Add(BotRecord("bot", 82));
            records.Add(BaseRecord("backstop", 83, FixtureBackstop));

            string report = WriteReport(directory, "complete.json", records);
            AcceptanceReportCoverage coverage = Evaluate(new[] { report }, 3, "0.2.8");
            return coverage.Complete
                && coverage.UniqueRecords == records.Count
                && coverage.Missing.Count == 0;
        }
        finally
        {
            Directory.Delete(directory, true);
        }
    }

    internal static bool CasualBotTrafficCannotSatisfyFixtureCoverage()
    {
        string directory = TemporaryDirectory("CasualCoverage");
        try
        {
            string report = WriteReport(
                directory,
                "casual.json",
                new[] { BotRecord("casual-bot", 1) });
            AcceptanceReportCoverage coverage = Evaluate(new[] { report }, 3, "0.2.8");
            return coverage.Passed("BotHealthTelemetry")
                && coverage.Passed("BotArmorTelemetry")
                && coverage.Passed("PostDeathArmorTelemetry")
                && !coverage.Passed("SinglePlateChain")
                && !coverage.Passed("SpacedArmor")
                && !coverage.Complete;
        }
        finally
        {
            Directory.Delete(directory, true);
        }
    }

    internal static bool DuplicateBatchesDoNotInflateCoverage()
    {
        string directory = TemporaryDirectory("DuplicateCoverage");
        try
        {
            Dictionary<string, object> record = PlateRecord(
                "duplicate-chain",
                sequence: 1,
                layer: 0,
                layerCount: 1,
                armorClass: 6,
                material: ArmoredSteel);
            string first = WriteReport(directory, "first.json", new[] { record });
            string second = WriteReport(directory, "second.json", new[] { record });
            AcceptanceReportCoverage coverage = Evaluate(
                new[] { first, second },
                3,
                "0.2.8");
            return coverage.UniqueRecords == 1
                && coverage.UniqueChains == 1
                && coverage.Passed("SinglePlateChain");
        }
        finally
        {
            Directory.Delete(directory, true);
        }
    }

    private static bool CompleteLayerChain(
        IEnumerable<AcceptanceRecord> chain,
        int expectedLayers)
    {
        if (expectedLayers < 1)
        {
            return false;
        }

        int[] layers = chain
            .Where(record => record.TargetKind == FixturePlate)
            .Select(record => record.Layer)
            .Where(layer => layer >= 0 && layer < expectedLayers)
            .Distinct()
            .OrderBy(layer => layer)
            .ToArray();
        return layers.SequenceEqual(Enumerable.Range(0, expectedLayers));
    }

    private static bool SteelPresetChain(
        IEnumerable<AcceptanceRecord> chain,
        int expectedLayers,
        int expectedClass)
    {
        AcceptanceRecord[] records = chain.ToArray();
        AcceptanceRecord[] plates = records
            .Where(record => record.TargetKind == FixturePlate)
            .ToArray();
        return plates.Length > 0
            && records.Any(record => record.LayerCount == expectedLayers)
            && CompleteLayerChain(records, expectedLayers)
            && plates.All(record => record.FixtureArmorClass == expectedClass
                && record.FixtureArmorMaterial == ArmoredSteel);
    }

    private static bool GranitPresetEvidence(
        IEnumerable<AcceptanceRecord> records,
        string templateId)
    {
        return records.Any(record => record.TargetKind == FixturePlate
            && record.FixtureTemplateId == templateId
            && record.FixtureArmorClass > 0
            && !string.IsNullOrWhiteSpace(record.FixtureArmorMaterial)
            && !string.IsNullOrWhiteSpace(record.Outcome)
            && record.HasArmorRealResistance
            && record.HasArmorClassResistance
            && record.HasPenetrationChancePercent
            && record.HasDurabilityBefore
            && record.HasDurabilityAfter
            && record.DurabilityAfter < record.DurabilityBefore);
    }

    private static bool HasContinuationIntoLaterLayer(
        IEnumerable<IGrouping<string, AcceptanceRecord>> chains,
        string kind)
    {
        foreach (IGrouping<string, AcceptanceRecord> chain in chains)
        {
            AcceptanceRecord[] records = chain.ToArray();
            foreach (AcceptanceRecord candidate in records)
            {
                if (candidate.ParentDepth <= 0
                    || candidate.ContinuationKind != kind
                    || !candidate.HasContinuationFactors)
                {
                    continue;
                }

                if (records.Any(record => record.Sequence > candidate.Sequence
                    && record.TargetKind == FixturePlate
                    && record.Layer > candidate.ContinuationSourceLayer))
                {
                    return true;
                }
            }
        }

        return false;
    }

    private static string TemporaryDirectory(string label)
    {
        string directory = Path.Combine(
            Path.GetTempPath(),
            "BallisticsLab." + label + "." + Guid.NewGuid().ToString("N", CultureInfo.InvariantCulture));
        Directory.CreateDirectory(directory);
        return directory;
    }

    private static string WriteReport(
        string directory,
        string name,
        IEnumerable<Dictionary<string, object>> records)
    {
        string path = Path.Combine(directory, name);
        File.WriteAllText(
            path,
            JsonSerializer.Serialize(new
            {
                schema = 3,
                pluginVersion = "0.2.8",
                records
            }));
        return path;
    }

    private static void AddSteelChain(
        ICollection<Dictionary<string, object>> records,
        string chain,
        int layerCount,
        int armorClass,
        int sequence,
        string continuationKind = "")
    {
        for (int layer = 0; layer < layerCount; layer++)
        {
            Dictionary<string, object> record = PlateRecord(
                chain,
                sequence + layer,
                layer,
                layerCount,
                armorClass,
                ArmoredSteel);
            if (layer == 0 && !string.IsNullOrEmpty(continuationKind))
            {
                AddContinuation(record, continuationKind, sourceLayer: 0);
            }
            records.Add(record);
        }
    }

    private static void AddGenericChain(
        ICollection<Dictionary<string, object>> records,
        string chain,
        int layerCount,
        int sequence,
        string continuationKind = "")
    {
        for (int layer = 0; layer < layerCount; layer++)
        {
            Dictionary<string, object> record = PlateRecord(
                chain,
                sequence + layer,
                layer,
                layerCount,
                armorClass: 2,
                material: "Titan");
            if (layer == 0 && !string.IsNullOrEmpty(continuationKind))
            {
                AddContinuation(record, continuationKind, sourceLayer: 0);
            }
            records.Add(record);
        }
    }

    private static void AddContinuation(
        IDictionary<string, object> record,
        string kind,
        int sourceLayer)
    {
        record["parentDepth"] = 1;
        record["continuationKind"] = kind;
        record["continuationSourceLayer"] = sourceLayer;
        record["continuationPenetrationFactor"] = 0.9d;
        record["continuationVelocityFactor"] = 0.8d;
        record["continuationOutcomeFactor"] = 0.7d;
        record["continuationArmorCf"] = 0.6d;
    }

    private static Dictionary<string, object> GranitRecord(
        string chain,
        string templateId,
        int sequence)
    {
        Dictionary<string, object> record = PlateRecord(
            chain,
            sequence,
            layer: 0,
            layerCount: 1,
            armorClass: 6,
            material: "Ceramic");
        record["fixtureTemplateId"] = templateId;
        return record;
    }

    private static Dictionary<string, object> BotRecord(string chain, int sequence)
    {
        Dictionary<string, object> record = BaseRecord(chain, sequence, Bot);
        record["bodyHealthBefore"] = 10d;
        record["bodyHealthAfter"] = 0d;
        record["targetAliveBefore"] = false;
        record["targetAliveAfter"] = false;
        record["armorChanges"] = "armor:10->9";
        return record;
    }

    private static Dictionary<string, object> PlateRecord(
        string chain,
        int sequence,
        int layer,
        int layerCount,
        int armorClass,
        string material)
    {
        Dictionary<string, object> record = BaseRecord(chain, sequence, FixturePlate);
        record["layer"] = layer;
        record["layerCount"] = layerCount;
        record["fixtureTemplateId"] = "template-" + chain;
        record["fixtureArmorClass"] = armorClass;
        record["fixtureArmorMaterial"] = material;
        record["layerSpacing"] = layerCount > 1 ? 0.02d : 0d;
        record["colliderThickness"] = 0.01d;
        record["outcome"] = "PENETRATED / CONTINUING";
        record["armorRealResistance"] = 20d;
        record["armorClassResistance"] = 20d;
        record["penetrationChancePercent"] = 50d;
        record["durabilityBefore"] = 10d;
        record["durabilityAfter"] = 9d;
        return record;
    }

    private static Dictionary<string, object> BaseRecord(
        string chain,
        int sequence,
        string targetKind)
    {
        return new Dictionary<string, object>(StringComparer.Ordinal)
        {
            ["chainId"] = chain,
            ["fireIndex"] = sequence,
            ["fragmentIndex"] = 0,
            ["parentDepth"] = 0,
            ["sequence"] = sequence,
            ["target"] = targetKind + " target",
            ["targetKind"] = targetKind,
            ["outcome"] = "STOPPED"
        };
    }

    private sealed class AcceptanceRecord
    {
        internal static AcceptanceRecord From(JsonElement record)
        {
            return new AcceptanceRecord
            {
                ChainId = Text(record, "chainId"),
                FireIndex = Integer(record, "fireIndex", -1),
                FragmentIndex = Integer(record, "fragmentIndex", -1),
                ParentDepth = Integer(record, "parentDepth", -1),
                Sequence = Integer(record, "sequence", -1),
                TargetKind = Text(record, "targetKind"),
                Target = Text(record, "target"),
                Outcome = Text(record, "outcome"),
                Layer = Integer(record, "layer", -1),
                LayerCount = Integer(record, "layerCount", 0),
                FixtureTemplateId = Text(record, "fixtureTemplateId"),
                FixtureArmorClass = Integer(record, "fixtureArmorClass", 0),
                FixtureArmorMaterial = Text(record, "fixtureArmorMaterial"),
                LayerSpacing = Number(record, "layerSpacing", out _),
                ColliderThickness = Number(record, "colliderThickness", out _),
                ArmorRealResistance = Number(record, "armorRealResistance", out bool hasArmorRealResistance),
                HasArmorRealResistance = hasArmorRealResistance,
                ArmorClassResistance = Number(record, "armorClassResistance", out bool hasArmorClassResistance),
                HasArmorClassResistance = hasArmorClassResistance,
                PenetrationChancePercent = Number(record, "penetrationChancePercent", out bool hasPenetrationChance),
                HasPenetrationChancePercent = hasPenetrationChance,
                DurabilityBefore = Number(record, "durabilityBefore", out bool hasDurabilityBefore),
                HasDurabilityBefore = hasDurabilityBefore,
                DurabilityAfter = Number(record, "durabilityAfter", out bool hasDurabilityAfter),
                HasDurabilityAfter = hasDurabilityAfter,
                BodyHealthBefore = Number(record, "bodyHealthBefore", out bool hasBodyHealthBefore),
                HasBodyHealthBefore = hasBodyHealthBefore,
                BodyHealthAfter = Number(record, "bodyHealthAfter", out bool hasBodyHealthAfter),
                HasBodyHealthAfter = hasBodyHealthAfter,
                TargetAliveBefore = Boolean(record, "targetAliveBefore", out bool hasTargetAliveBefore),
                HasTargetAliveBefore = hasTargetAliveBefore,
                TargetAliveAfter = Boolean(record, "targetAliveAfter", out bool hasTargetAliveAfter),
                HasTargetAliveAfter = hasTargetAliveAfter,
                ArmorChanges = Text(record, "armorChanges"),
                ContinuationKind = Text(record, "continuationKind"),
                ContinuationSourceLayer = Integer(record, "continuationSourceLayer", -1),
                HasContinuationFactors = HasFiniteNumber(record, "continuationPenetrationFactor")
                    && HasFiniteNumber(record, "continuationVelocityFactor")
                    && HasFiniteNumber(record, "continuationOutcomeFactor")
                    && HasFiniteNumber(record, "continuationArmorCf")
            };
        }

        internal string ChainId { get; private set; }
        internal int FireIndex { get; private set; }
        internal int FragmentIndex { get; private set; }
        internal int ParentDepth { get; private set; }
        internal int Sequence { get; private set; }
        internal string TargetKind { get; private set; }
        internal string Target { get; private set; }
        internal string Outcome { get; private set; }
        internal int Layer { get; private set; }
        internal int LayerCount { get; private set; }
        internal string FixtureTemplateId { get; private set; }
        internal int FixtureArmorClass { get; private set; }
        internal string FixtureArmorMaterial { get; private set; }
        internal double LayerSpacing { get; private set; }
        internal double ColliderThickness { get; private set; }
        internal double ArmorRealResistance { get; private set; }
        internal bool HasArmorRealResistance { get; private set; }
        internal double ArmorClassResistance { get; private set; }
        internal bool HasArmorClassResistance { get; private set; }
        internal double PenetrationChancePercent { get; private set; }
        internal bool HasPenetrationChancePercent { get; private set; }
        internal double DurabilityBefore { get; private set; }
        internal bool HasDurabilityBefore { get; private set; }
        internal double DurabilityAfter { get; private set; }
        internal bool HasDurabilityAfter { get; private set; }
        internal double BodyHealthBefore { get; private set; }
        internal bool HasBodyHealthBefore { get; private set; }
        internal double BodyHealthAfter { get; private set; }
        internal bool HasBodyHealthAfter { get; private set; }
        internal bool TargetAliveBefore { get; private set; }
        internal bool HasTargetAliveBefore { get; private set; }
        internal bool TargetAliveAfter { get; private set; }
        internal bool HasTargetAliveAfter { get; private set; }
        internal string ArmorChanges { get; private set; }
        internal string ContinuationKind { get; private set; }
        internal int ContinuationSourceLayer { get; private set; }
        internal bool HasContinuationFactors { get; private set; }

        internal string Identity => string.Join(
            "|",
            ChainId,
            FireIndex.ToString(CultureInfo.InvariantCulture),
            FragmentIndex.ToString(CultureInfo.InvariantCulture),
            ParentDepth.ToString(CultureInfo.InvariantCulture),
            Sequence.ToString(CultureInfo.InvariantCulture),
            Target,
            Outcome);

        private static string Text(JsonElement record, string name)
        {
            return record.TryGetProperty(name, out JsonElement value)
                && value.ValueKind == JsonValueKind.String
                    ? value.GetString() ?? string.Empty
                    : string.Empty;
        }

        private static int Integer(JsonElement record, string name, int fallback)
        {
            if (!record.TryGetProperty(name, out JsonElement value))
            {
                return fallback;
            }
            if (value.ValueKind == JsonValueKind.Number
                && value.TryGetInt32(out int number))
            {
                return number;
            }
            if (value.ValueKind == JsonValueKind.String
                && int.TryParse(
                    value.GetString(),
                    NumberStyles.Integer,
                    CultureInfo.InvariantCulture,
                    out number))
            {
                return number;
            }
            return fallback;
        }

        private static double Number(JsonElement record, string name, out bool valid)
        {
            valid = false;
            if (!record.TryGetProperty(name, out JsonElement value))
            {
                return 0d;
            }

            double number;
            if (value.ValueKind == JsonValueKind.Number
                && value.TryGetDouble(out number))
            {
                valid = double.IsFinite(number);
                return valid ? number : 0d;
            }
            if (value.ValueKind == JsonValueKind.String
                && double.TryParse(
                    value.GetString(),
                    NumberStyles.Float,
                    CultureInfo.InvariantCulture,
                    out number))
            {
                valid = double.IsFinite(number);
                return valid ? number : 0d;
            }
            return 0d;
        }

        private static bool HasFiniteNumber(JsonElement record, string name)
        {
            Number(record, name, out bool valid);
            return valid;
        }

        private static bool Boolean(JsonElement record, string name, out bool valid)
        {
            valid = false;
            if (!record.TryGetProperty(name, out JsonElement value))
            {
                return false;
            }
            if (value.ValueKind == JsonValueKind.True)
            {
                valid = true;
                return true;
            }
            if (value.ValueKind == JsonValueKind.False)
            {
                valid = true;
                return false;
            }
            if (value.ValueKind == JsonValueKind.String
                && bool.TryParse(value.GetString(), out bool result))
            {
                valid = true;
                return result;
            }
            return false;
        }
    }
}

using System.Globalization;
using System.Text.Json;
using BallisticsLab.Core;

const string plateParent = "644120aa86ffbe10ee032b6f";
const string granitBr4 = "65573fa5655447403702a816";
const string granitBr5 = "64afc71497cf3a403c01ff38";

string itemsPath = args.Length > 0
    ? Path.GetFullPath(args[0])
    : @"E:\Games\SPT\SPT_Runtime\SPT_Data\database\templates\items.json";
string reportsPath = args.Length > 1
    ? Path.GetFullPath(args[1])
    : string.Empty;

List<string> failures = new();
int passed = 0;
Dictionary<string, AmmoRow> ammunition = new(StringComparer.Ordinal);

Check(
    LabBuild.PluginGuid == "com.janky.ballisticslab"
    && LabBuild.PluginName == "Janky-BallisticsLab"
    && LabBuild.PluginVersion == "0.2.4"
    && LabBuild.ReportSchema == 3,
    "report provenance constants match the current plugin build");
Check(LabPolicies.OutcomeName(0, false, false) == "PENETRATED / CONTINUING", "continuing taxonomy");
Check(LabPolicies.OutcomeName(1, false, false) == "PENETRATED / DEVIATED", "deviation taxonomy");
Check(LabPolicies.OutcomeName(3, false, false) == "PENETRATED / FRAGMENTED", "fragment taxonomy");
Check(LabPolicies.OutcomeName(4, true, false) == "STOPPED / ARMOR BLOCK", "armor block taxonomy");
Check(LabPolicies.OutcomeName(2, false, true) == "RICOCHET", "ricochet taxonomy");
Check(Math.Abs(LabPolicies.ImpactAngleDegrees(1f)) < 0.0001f, "normal impact angle");
Check(Math.Abs(LabPolicies.ImpactAngleDegrees(0f) - 90f) < 0.0001f, "grazing impact angle");
Check(Math.Abs(LabPolicies.ImpactAngleDegrees(-0.5f) - 60f) < 0.0001f, "back-face angle folds into zero-to-ninety range");
Check(LabPolicies.Csv("a,b") == "\"a,b\"", "CSV escaping");
Check(LabPolicies.Json("a\n\"b") == "\"a\\n\\\"b\"", "JSON escaping");
Check(ReportPairValidator.ParserHandlesQuotedFields(), "CSV report parser handles commas, quotes, and embedded newlines");
Check(ReportPairValidator.ValidatorMatchesSyntheticPair(), "CSV and JSON report validator accepts a matching schema-3 pair");
Check(ReportPairValidator.ValidatorRejectsSyntheticMismatch(), "CSV and JSON report validator rejects a field mismatch");
Check(!LabPolicies.IsFiniteNonNegative(float.NaN) && LabPolicies.IsFiniteNonNegative(0f), "finite guard");
Check(
    LabPolicies.RequiresFixtureContinuationCorrection(1)
    && LabPolicies.RequiresFixtureContinuationCorrection(3)
    && !LabPolicies.RequiresFixtureContinuationCorrection(0)
    && !LabPolicies.RequiresFixtureContinuationCorrection(2)
    && !LabPolicies.RequiresFixtureContinuationCorrection(4),
    "only deviated and fragmented children require fixture body-armor correction");
Check(
    Nearly(LabPolicies.PenetratedChildFactor(40f, 0f, 0f, 0f), 0.4f),
    "penetrated child factor reproduces EFT body-plate branch");
Check(
    Nearly(LabPolicies.PenetratedChildFactor(25f, 10f, 0.05f, -0.03f), 0.17f),
    "penetrated child factor includes collider and ammunition modifiers");
Check(
    Nearly(LabPolicies.PenetratedChildFactor(-10f, 0f, 0f, 0f), 0f)
    && Nearly(LabPolicies.PenetratedChildFactor(250f, 0f, 0f, 0f), 1f),
    "penetrated child factor clamps to EFT zero-to-one range");
Check(
    Nearly(LabPolicies.DeviatedChildVelocityFactor(0f), 0.8f)
    && Nearly(LabPolicies.DeviatedChildVelocityFactor(1f), 1f),
    "deviated child velocity factor reproduces EFT interpolation");
Check(
    Nearly(LabPolicies.DeviatedChildOutcomeFactor(0f), 0.2f)
    && Nearly(LabPolicies.DeviatedChildOutcomeFactor(1f), 1f),
    "deviated child future-outcome factor reproduces EFT interpolation");
float firstLayerPenetration = 50f
    * LabPolicies.PenetratedChildFactor(50f, 0f, 0f, 0f)
    * 0.7f;
float secondLayerPenetration = firstLayerPenetration
    * LabPolicies.PenetratedChildFactor(firstLayerPenetration, 0f, 0f, 0f)
    * 0.7f;
Check(
    Nearly(firstLayerPenetration, 17.5f)
    && Nearly(secondLayerPenetration, 2.14375f),
    "layered continuation compounds from the corrected child rather than the original shot");
Check(
    LabPolicies.ShotChainId("profile", 17, 42) == "profile:17:42",
    "shot chain identity is stable across child records");
Check(
    LabPolicies.ShotChainId("profile", 17, 42) != LabPolicies.ShotChainId("profile", 18, 42)
    && LabPolicies.ShotChainId("profile", 17, 42) != LabPolicies.ShotChainId("profile", 17, 43),
    "shot chain identity separates different fires and root seeds");
Check(
    LabPolicies.AuthoritativeAmmoValue("5c0d5e4486f77478390952fe", "5c0d688c86f77413ae3407b2")
        == "5c0d5e4486f77478390952fe"
    && LabPolicies.AuthoritativeAmmoValue(string.Empty, "5c0d688c86f77413ae3407b2")
        == "5c0d688c86f77413ae3407b2"
    && LabPolicies.AuthoritativeAmmoValue(null, null) == string.Empty,
    "runtime ammo template identity overrides stale item identity");
Check(
    LabPolicies.ShouldDisplayBodyTelemetry(50f, 10f, string.Empty)
    && LabPolicies.ShouldDisplayBodyTelemetry(0f, 0f, "armor 40.00->35.00")
    && !LabPolicies.ShouldDisplayBodyTelemetry(0f, 0f, string.Empty),
    "postmortem armor changes remain visible at zero body health");
float fixtureGap = 0.15f;
float fixtureThickness = 0.0127f;
bool exactFaceGaps = true;
for (int layer = 1; layer < LabPolicies.MaximumLayers; layer++)
{
    float previousBackFace = LabPolicies.LayerCenterOffset(
        layer - 1,
        fixtureGap,
        fixtureThickness) + fixtureThickness * 0.5f;
    float currentFrontFace = LabPolicies.LayerCenterOffset(
        layer,
        fixtureGap,
        fixtureThickness) - fixtureThickness * 0.5f;
    exactFaceGaps &= Nearly(currentFrontFace - previousBackFace, fixtureGap);
}
Check(exactFaceGaps, "one-to-six-layer geometry preserves the configured face gap");
float lastPlateBackFace = LabPolicies.LayerCenterOffset(
    LabPolicies.MaximumLayers - 1,
    fixtureGap,
    fixtureThickness) + fixtureThickness * 0.5f;
float backstopFrontFace = LabPolicies.BackstopCenterOffset(
    LabPolicies.MaximumLayers,
    fixtureGap,
    fixtureThickness,
    1f,
    0.08f) - 0.04f;
Check(
    Nearly(backstopFrontFace - lastPlateBackFace, 1f),
    "six-layer backstop retains one meter of face clearance");
Check(LabPolicies.MaximumLayers == 6, "fixture layer limit remains six");

if (!File.Exists(itemsPath))
{
    failures.Add("items.json was not found: " + itemsPath);
}
else
{
    using JsonDocument document = JsonDocument.Parse(File.ReadAllText(itemsPath));
    List<PlateRow> plates = new();
    foreach (JsonProperty item in document.RootElement.EnumerateObject())
    {
        JsonElement value = item.Value;
        if (value.TryGetProperty("_name", out JsonElement ammunitionNameElement)
            && value.TryGetProperty("_props", out JsonElement ammunitionProps)
            && ammunitionProps.TryGetProperty("InitialSpeed", out JsonElement initialSpeedElement)
            && TryGetFloat(initialSpeedElement, out float initialSpeed))
        {
            ammunition[item.Name] = new AmmoRow(
                ammunitionNameElement.GetString() ?? item.Name,
                initialSpeed);
        }

        if (!value.TryGetProperty("_parent", out JsonElement parent)
            || parent.GetString() != plateParent
            || !value.TryGetProperty("_props", out JsonElement props)
            || !props.TryGetProperty("armorClass", out JsonElement armorClassElement)
            || !TryGetInt(armorClassElement, out int armorClass)
            || armorClass <= 0
            || !props.TryGetProperty("Durability", out JsonElement durabilityElement)
            || !TryGetInt(durabilityElement, out int durability)
            || durability <= 0
            || !props.TryGetProperty("ArmorMaterial", out JsonElement materialElement))
        {
            continue;
        }

        string name = value.TryGetProperty("_name", out JsonElement nameElement)
            ? nameElement.GetString() ?? item.Name
            : item.Name;
        plates.Add(new PlateRow(item.Name, name, armorClass, materialElement.GetString() ?? string.Empty, durability));
    }

    Check(plates.Count == 39, "39 usable installed plate templates");
    string[] installedMaterials = plates
        .Select(plate => plate.Material)
        .Distinct(StringComparer.Ordinal)
        .OrderBy(material => material, StringComparer.Ordinal)
        .ToArray();
    string[] expectedMaterials =
    {
        "Aluminium",
        "Aramid",
        "ArmoredSteel",
        "Ceramic",
        "Combined",
        "Titan",
        "UHMWPE"
    };
    Check(
        installedMaterials.SequenceEqual(expectedMaterials, StringComparer.Ordinal),
        "exact installed plate-material set matches the lab controls");
    Check(plates.Any(plate => plate.Id == granitBr4 && plate.ArmorClass == 5 && plate.Material == "Ceramic"), "Granit Br4 regression");
    Check(plates.Any(plate => plate.Id == granitBr5 && plate.ArmorClass == 6 && plate.Material == "Ceramic"), "Granit Br5 regression");
    Check(Enumerable.Range(3, 4).All(armorClass => plates.Any(plate => plate.Material == "ArmoredSteel" && plate.ArmorClass == armorClass)), "steel classes 3 through 6");
    Check(plates.All(plate => plate.Durability > 0 && plate.ArmorClass is >= 2 and <= 6), "plate class and durability bounds");

    Console.WriteLine("Catalog: " + plates.Count.ToString(CultureInfo.InvariantCulture) + " usable plates");
    foreach (IGrouping<string, PlateRow> group in plates.GroupBy(plate => plate.Material).OrderBy(group => group.Key, StringComparer.Ordinal))
    {
        Console.WriteLine("  " + group.Key + ": " + group.Count().ToString(CultureInfo.InvariantCulture));
    }
}

if (!string.IsNullOrEmpty(reportsPath))
{
    string latestReport = Directory.Exists(reportsPath)
        ? Directory.GetFiles(reportsPath, "BallisticsLab-*.json", SearchOption.TopDirectoryOnly)
            .OrderBy(File.GetLastWriteTimeUtc)
            .ThenBy(path => path, StringComparer.Ordinal)
            .LastOrDefault() ?? string.Empty
        : string.Empty;
    Check(!string.IsNullOrEmpty(latestReport), "latest BallisticsLab report exists");

    if (!string.IsNullOrEmpty(latestReport))
    {
        using JsonDocument report = JsonDocument.Parse(File.ReadAllText(latestReport));
        bool currentSchema = report.RootElement.TryGetProperty("schema", out JsonElement schemaElement)
            && schemaElement.TryGetInt32(out int schema)
            && schema == LabBuild.ReportSchema;
        Check(currentSchema, "latest BallisticsLab report uses the current schema");
        string reportVersion = report.RootElement.TryGetProperty(
                "pluginVersion",
                out JsonElement versionElement)
            ? versionElement.GetString() ?? string.Empty
            : string.Empty;
        Check(
            string.Equals(reportVersion, LabBuild.PluginVersion, StringComparison.Ordinal),
            "latest BallisticsLab report identifies the current plugin version");

        JsonElement recordsElement = report.RootElement.TryGetProperty("records", out JsonElement records)
            ? records
            : default;
        bool hasRecords = recordsElement.ValueKind == JsonValueKind.Array
            && recordsElement.GetArrayLength() > 0;
        Check(hasRecords, "latest BallisticsLab report contains shot records");

        string identityFailure = string.Empty;
        if (hasRecords)
        {
            foreach (JsonElement record in recordsElement.EnumerateArray())
            {
                string templateId = record.TryGetProperty("ammoTemplateId", out JsonElement idElement)
                    ? idElement.GetString() ?? string.Empty
                    : string.Empty;
                string reportedName = record.TryGetProperty("ammoName", out JsonElement nameElement)
                    ? nameElement.GetString() ?? string.Empty
                    : string.Empty;
                float reportedSpeed = 0f;
                bool hasReportedSpeed = record.TryGetProperty("templateSpeed", out JsonElement speedElement)
                    && TryGetFloat(speedElement, out reportedSpeed);

                if (!ammunition.TryGetValue(templateId, out AmmoRow expected))
                {
                    identityFailure = "unknown template " + templateId;
                    break;
                }
                if (!string.Equals(reportedName, expected.Name, StringComparison.Ordinal))
                {
                    identityFailure = templateId + " reports name " + reportedName
                        + " but the installed template is " + expected.Name;
                    break;
                }
                if (!hasReportedSpeed || !NearlyWithin(reportedSpeed, expected.InitialSpeed, 0.001f))
                {
                    identityFailure = templateId + " reports a template speed that differs from the installed template";
                    break;
                }
            }
        }
        else
        {
            identityFailure = "report contains no records";
        }

        Check(
            string.IsNullOrEmpty(identityFailure),
            string.IsNullOrEmpty(identityFailure)
                ? "latest report ammo identity and speed match the installed live template"
                : "latest report ammo identity mismatch: " + identityFailure);
        bool pairMatches = ReportPairValidator.Validate(latestReport, out string pairFailure);
        Check(
            pairMatches,
            pairMatches
                ? "latest CSV and JSON exports match field for field"
                : "latest CSV and JSON export mismatch: " + pairFailure);
        Console.WriteLine("Latest report: " + Path.GetFileName(latestReport));
    }
}

if (failures.Count > 0)
{
    Console.Error.WriteLine("FAILED " + failures.Count.ToString(CultureInfo.InvariantCulture));
    foreach (string failure in failures)
    {
        Console.Error.WriteLine("  " + failure);
    }
    return 1;
}

Console.WriteLine("PASS " + passed.ToString(CultureInfo.InvariantCulture) + " checks");
return 0;

void Check(bool condition, string name)
{
    if (condition)
    {
        passed++;
    }
    else
    {
        failures.Add(name);
    }
}

bool TryGetInt(JsonElement element, out int value)
{
    if (element.ValueKind == JsonValueKind.Number)
    {
        return element.TryGetInt32(out value);
    }

    if (element.ValueKind == JsonValueKind.String)
    {
        return int.TryParse(element.GetString(), NumberStyles.Integer, CultureInfo.InvariantCulture, out value);
    }

    value = 0;
    return false;
}

bool TryGetFloat(JsonElement element, out float value)
{
    if (element.ValueKind == JsonValueKind.Number)
    {
        return element.TryGetSingle(out value);
    }

    if (element.ValueKind == JsonValueKind.String)
    {
        return float.TryParse(element.GetString(), NumberStyles.Float, CultureInfo.InvariantCulture, out value);
    }

    value = 0f;
    return false;
}

bool Nearly(float actual, float expected)
{
    return Math.Abs(actual - expected) <= 0.00001f;
}

bool NearlyWithin(float actual, float expected, float tolerance)
{
    return Math.Abs(actual - expected) <= tolerance;
}

internal sealed record PlateRow(string Id, string Name, int ArmorClass, string Material, int Durability);
internal sealed record AmmoRow(string Name, float InitialSpeed);

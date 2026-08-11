using System.Globalization;
using System.Text.Json;
using BallisticsLab.Core;

const string plateParent = "644120aa86ffbe10ee032b6f";
const string granitBr4 = "65573fa5655447403702a816";
const string granitBr5 = "64afc71497cf3a403c01ff38";

string itemsPath = args.Length > 0
    ? Path.GetFullPath(args[0])
    : @"E:\Games\SPT\SPT_Runtime\SPT_Data\database\templates\items.json";

List<string> failures = new();
int passed = 0;

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

bool Nearly(float actual, float expected)
{
    return Math.Abs(actual - expected) <= 0.00001f;
}

internal sealed record PlateRow(string Id, string Name, int ArmorClass, string Material, int Durability);

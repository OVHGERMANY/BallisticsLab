using System.Security.Cryptography;
using System.Text.Json;
using BallisticsLab.Core;

internal static class GostAmmunitionMappingTests
{
    private const string SevenNThirteenLatin = "7N13";
    private const string SevenNThirteenCyrillic = "7Н13";
    private const string SevenBzThreeLatin = "7-BZ-3";
    private const string SevenBzThreeCyrillic = "7-БЗ-3";

    internal static bool SupportedDatabaseFilesMatchCatalogSnapshot(string itemsPath)
    {
        return TryGetDatabasePaths(
                itemsPath,
                out string englishLocalePath,
                out string russianLocalePath)
            && Hash(itemsPath) == GostProtocolCatalog.SupportedItemsSha256
            && Hash(englishLocalePath) == GostProtocolCatalog.SupportedEnglishLocaleSha256
            && Hash(russianLocalePath) == GostProtocolCatalog.SupportedRussianLocaleSha256;
    }

    internal static bool InstalledMappingsMatchDatabaseAndLocales(string itemsPath)
    {
        if (!TryGetDatabasePaths(
                itemsPath,
                out string englishLocalePath,
                out string russianLocalePath))
        {
            return false;
        }

        using JsonDocument items = JsonDocument.Parse(File.ReadAllText(itemsPath));
        using JsonDocument english = JsonDocument.Parse(File.ReadAllText(englishLocalePath));
        using JsonDocument russian = JsonDocument.Parse(File.ReadAllText(russianLocalePath));
        ProtocolAmmunitionMapping[] mappings = [.. GostProtocolCatalog.All
            .SelectMany(threat => threat.AmmunitionMappings)
            .Where(mapping => mapping.HasInstalledTemplate)];
        return mappings.Length == 6
            && mappings.All(mapping => MappingMatches(
                mapping,
                items.RootElement,
                english.RootElement,
                russian.RootElement));
    }

    internal static bool UnavailableThreatDesignationsRemainAbsent(string itemsPath)
    {
        if (!TryGetDatabasePaths(
                itemsPath,
                out string englishLocalePath,
                out string russianLocalePath))
        {
            return false;
        }

        using JsonDocument items = JsonDocument.Parse(File.ReadAllText(itemsPath));
        using JsonDocument english = JsonDocument.Parse(File.ReadAllText(englishLocalePath));
        using JsonDocument russian = JsonDocument.Parse(File.ReadAllText(russianLocalePath));
        int unavailable = GostProtocolCatalog.All
            .SelectMany(threat => threat.AmmunitionMappings)
            .Count(mapping => mapping.IdentityStatus
                == ProtocolAmmunitionIdentityStatus.NotAvailableInSupportedDatabase);
        return unavailable == 2
            && !AnyInstalledRifleCartridgeContains(
                items.RootElement,
                english.RootElement,
                russian.RootElement,
                SevenNThirteenLatin,
                SevenNThirteenCyrillic)
            && !AnyInstalledRifleCartridgeContains(
                items.RootElement,
                english.RootElement,
                russian.RootElement,
                SevenBzThreeLatin,
                SevenBzThreeCyrillic);
    }

    private static bool MappingMatches(
        ProtocolAmmunitionMapping mapping,
        JsonElement items,
        JsonElement english,
        JsonElement russian)
    {
        if (!items.TryGetProperty(mapping.TemplateId, out JsonElement item)
            || !item.TryGetProperty("_name", out JsonElement internalName)
            || !item.TryGetProperty("_props", out JsonElement properties)
            || !properties.TryGetProperty("Caliber", out JsonElement caliber)
            || !properties.TryGetProperty("BulletMassGram", out JsonElement mass)
            || !properties.TryGetProperty("InitialSpeed", out JsonElement speed)
            || !english.TryGetProperty(mapping.TemplateId + " Name", out JsonElement englishName)
            || !english.TryGetProperty(
                mapping.TemplateId + " Description",
                out JsonElement englishDescription)
            || !russian.TryGetProperty(
                mapping.TemplateId + " Description",
                out JsonElement russianDescription))
        {
            return false;
        }

        string englishText = englishDescription.GetString() ?? string.Empty;
        string russianText = russianDescription.GetString() ?? string.Empty;
        return string.Equals(
                internalName.GetString(),
                mapping.InstalledInternalName,
                StringComparison.Ordinal)
            && string.Equals(
                englishName.GetString(),
                mapping.InstalledDisplayName,
                StringComparison.Ordinal)
            && string.Equals(
                caliber.GetString(),
                mapping.InstalledCaliber,
                StringComparison.Ordinal)
            && Nearly(
                mass.GetDouble() / 1000d,
                mapping.InstalledProjectileMassKilograms)
            && Nearly(speed.GetDouble(), mapping.InstalledInitialSpeedMetresPerSecond)
            && englishText.Contains(
                mapping.EnglishDesignationEvidence,
                StringComparison.OrdinalIgnoreCase)
            && englishText.Contains(
                mapping.EnglishMassEvidence,
                StringComparison.OrdinalIgnoreCase)
            && russianText.Contains(
                mapping.RussianDesignationEvidence,
                StringComparison.OrdinalIgnoreCase);
    }

    private static bool AnyInstalledRifleCartridgeContains(
        JsonElement items,
        JsonElement english,
        JsonElement russian,
        string latinDesignation,
        string cyrillicDesignation)
    {
        foreach (JsonProperty candidate in items.EnumerateObject())
        {
            JsonElement item = candidate.Value;
            if (!item.TryGetProperty("_props", out JsonElement properties)
                || !properties.TryGetProperty("Caliber", out JsonElement caliber)
                || !string.Equals(
                    caliber.GetString(),
                    "Caliber762x54R",
                    StringComparison.Ordinal))
            {
                continue;
            }

            string internalName = item.TryGetProperty(
                    "_name",
                    out JsonElement internalNameElement)
                ? internalNameElement.GetString() ?? string.Empty
                : string.Empty;
            string searchable = internalName
                + "\n"
                + LocaleText(english, candidate.Name)
                + "\n"
                + LocaleText(russian, candidate.Name);
            if (ContainsDesignation(searchable, latinDesignation)
                || ContainsDesignation(searchable, cyrillicDesignation))
            {
                return true;
            }
        }
        return false;
    }

    private static string LocaleText(JsonElement locale, string templateId)
    {
        string name = locale.TryGetProperty(templateId + " Name", out JsonElement nameElement)
            ? nameElement.GetString() ?? string.Empty
            : string.Empty;
        string shortName = locale.TryGetProperty(
                templateId + " ShortName",
                out JsonElement shortNameElement)
            ? shortNameElement.GetString() ?? string.Empty
            : string.Empty;
        string description = locale.TryGetProperty(
                templateId + " Description",
                out JsonElement descriptionElement)
            ? descriptionElement.GetString() ?? string.Empty
            : string.Empty;
        return name + "\n" + shortName + "\n" + description;
    }

    private static bool ContainsDesignation(string source, string designation)
    {
        return NormalizeDesignation(source).Contains(
            NormalizeDesignation(designation),
            StringComparison.OrdinalIgnoreCase);
    }

    private static string NormalizeDesignation(string value)
    {
        return value
            .Replace("-", string.Empty, StringComparison.Ordinal)
            .Replace(" ", string.Empty, StringComparison.Ordinal)
            .Replace("‑", string.Empty, StringComparison.Ordinal);
    }

    private static bool TryGetDatabasePaths(
        string itemsPath,
        out string englishLocalePath,
        out string russianLocalePath)
    {
        englishLocalePath = string.Empty;
        russianLocalePath = string.Empty;
        string fullItemsPath = Path.GetFullPath(itemsPath);
        DirectoryInfo? templatesDirectory = Directory.GetParent(fullItemsPath);
        DirectoryInfo? databaseDirectory = templatesDirectory?.Parent;
        if (!File.Exists(fullItemsPath) || databaseDirectory == null)
        {
            return false;
        }
        englishLocalePath = Path.Combine(
            databaseDirectory.FullName,
            "locales",
            "global",
            "en.json");
        russianLocalePath = Path.Combine(
            databaseDirectory.FullName,
            "locales",
            "global",
            "ru.json");
        return File.Exists(englishLocalePath) && File.Exists(russianLocalePath);
    }

    private static string Hash(string path)
    {
        using FileStream stream = File.OpenRead(path);
        return Convert.ToHexString(SHA256.HashData(stream));
    }

    private static bool Nearly(double actual, double expected)
    {
        return Math.Abs(actual - expected) < 0.000000001d;
    }
}

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics.CodeAnalysis;
using System.Linq;

namespace BallisticsLab.Core
{
    internal static class GostProtocolCatalog
    {
        internal const string ClassificationStandard = "GOST 34286-2017";
        internal const string TestMethodStandard = "GOST R 55623-2013";
        internal const string ClassificationSource =
            "https://protect.gost.ru/gost/details/1f0d4f81-957c-42b1-a509-1963dd51e528";
        internal const string TestMethodSource =
            "https://protect.gost.ru/gost/details/8db08cb9-43cc-4656-8f9a-bb11a14deb6b";
        internal const string SupportedItemsSha256 =
            "5093B348FD0D507CDDC1E957114A77B44FF9358846299CBC98AC5E0AD4967A34";
        internal const string SupportedEnglishLocaleSha256 =
            "5334136BB45ABF0DB57E2AB452E7A92F513C5C0D84FCED45818BE954C0890971";
        internal const string SupportedRussianLocaleSha256 =
            "66B073ABA65EE938C2992AF49057236F1C665DF17693EF029A80FA42A2125CA9";

        private const double ShortRangeMetres = 5d;
        private const double RifleRangeMetres = 10d;
        private const double HeavyRangeMetres = 50d;
        private const double ShortRangeToleranceMetres = 0.1d;
        private const double HeavyRangeToleranceMetres = 0.5d;
        private const double VelocityMeasurementDistanceMetres = 3d;
        private const int RequiredRifledShots = 5;
        private const double MaximumImpactAngleDegrees = 5d;
        private const double MinimumSeparationDiameters = 5d;

        private static readonly IReadOnlyList<ProtocolThreatDefinition> Definitions =
            new ReadOnlyCollection<ProtocolThreatDefinition>(
                new[]
                {
                    Threat(
                        "gost-34286-br1-9x18-pst",
                        "Br1",
                        "9x18 mm Pst 57-N-181S",
                        "Steel core",
                        0.0059d,
                        335d,
                        10d,
                        ShortRangeMetres,
                        ShortRangeToleranceMetres,
                        ProtocolAmmunitionMapping.Installed(
                            ProtocolAmmunitionIdentityStatus.VariantDesignation,
                            "5737201124597760fc4431f1",
                            "patron_9x18pm_PST_gzh",
                            "9x18mm PM Pst gzh",
                            "Caliber9x18PM",
                            0.0059d,
                            0.0059d,
                            298d,
                            "GAU Index - 57-N-181S-01",
                            "Индекс ГАУ - 57-Н-181С-01",
                            "5.9 gram",
                            "The installed cartridge is the -01 variant, not the unsuffixed 57-N-181S threat.")),
                    Threat(
                        "gost-34286-br2-9x21-p-7n28",
                        "Br2",
                        "9x21 mm P 7N28",
                        "Lead core",
                        0.00793d,
                        390d,
                        10d,
                        ShortRangeMetres,
                        ShortRangeToleranceMetres,
                        ProtocolAmmunitionMapping.Installed(
                            ProtocolAmmunitionIdentityStatus.ExactDesignation,
                            "5a26abfac4a28232980eabff",
                            "patron_9x21_sp11",
                            "9x21mm P gzh",
                            "Caliber9x21",
                            0.0079d,
                            0.0075d,
                            413d,
                            "GRAU Index - 7N28",
                            "Индекс ГРАУ - 7Н28",
                            "7.5 gram",
                            "The designation is exact; the installed mass, locale mass, and nominal threat mass differ.")),
                    Threat(
                        "gost-34286-br3-9x19-pst-7n21",
                        "Br3",
                        "9x19 mm Pst 7N21",
                        "Heat-treated steel core",
                        0.007d,
                        410d,
                        10d,
                        ShortRangeMetres,
                        ShortRangeToleranceMetres,
                        ProtocolAmmunitionMapping.Installed(
                            ProtocolAmmunitionIdentityStatus.ExactDesignation,
                            "56d59d3ad2720bdb418b4577",
                            "patron_9x19_PST_gzh",
                            "9x19mm Pst gzh",
                            "Caliber9x19PARA",
                            0.0054d,
                            0.0054d,
                            457d,
                            "GRAU Index - 7N21",
                            "индекс ГРАУ - 7Н21",
                            "5.4 gram",
                            "The designation is exact; the installed projectile mass differs from the nominal threat mass.")),
                    Threat(
                        "gost-34286-br4-5.45x39-pp-7n10",
                        "Br4",
                        "5.45x39 mm PP 7N10",
                        "Heat-treated steel core",
                        0.0035d,
                        895d,
                        15d,
                        RifleRangeMetres,
                        ShortRangeToleranceMetres,
                        ProtocolAmmunitionMapping.Installed(
                            ProtocolAmmunitionIdentityStatus.ExactDesignation,
                            "56dff2ced2720bb4668b4567",
                            "patron_545x39_PP",
                            "5.45x39mm PP gs",
                            "Caliber545x39",
                            0.00368d,
                            0.0035d,
                            886d,
                            "GRAU Index - 7N10",
                            "Индекс ГРАУ - 7Н10",
                            "3.5 gram",
                            "The designation is exact; the installed mass differs from both the locale and nominal threat mass.")),
                    Threat(
                        "gost-34286-br4-7.62x39-ps-57-n-231",
                        "Br4",
                        "7.62x39 mm PS 57-N-231",
                        "Heat-treated steel core",
                        0.0079d,
                        720d,
                        15d,
                        RifleRangeMetres,
                        ShortRangeToleranceMetres,
                        ProtocolAmmunitionMapping.Installed(
                            ProtocolAmmunitionIdentityStatus.ExactDesignation,
                            "5656d7c34bdc2d9d198b4587",
                            "patron_762x39_PS",
                            "7.62x39mm PS gzh",
                            "Caliber762x39",
                            0.0079d,
                            0.0079d,
                            717d,
                            "GAU Index - 57-N-231",
                            "Индекс ГАУ - 57-Н-231",
                            "7.9 gram",
                            "The installed designation and projectile mass match the nominal threat.")),
                    Threat(
                        "gost-34286-br5-7.62x54-pp-7n13",
                        "Br5",
                        "7.62x54 mm PP 7N13",
                        "Heat-treated steel core",
                        0.0094d,
                        830d,
                        15d,
                        RifleRangeMetres,
                        ShortRangeToleranceMetres,
                        ProtocolAmmunitionMapping.Unavailable(
                            "No installed 7.62x54R template or English/Russian locale entry identifies GRAU 7N13.")),
                    Threat(
                        "gost-34286-br5-7.62x54-b32-7-bz-3",
                        "Br5",
                        "7.62x54 mm B-32 7-BZ-3",
                        "Armor-piercing incendiary steel core",
                        0.0104d,
                        810d,
                        15d,
                        RifleRangeMetres,
                        ShortRangeToleranceMetres,
                        ProtocolAmmunitionMapping.Unavailable(
                            "No installed 7.62x54R template or English/Russian locale entry identifies 7-BZ-3.")),
                    Threat(
                        "gost-34286-br6-12.7x108-b32-57-bz-542",
                        "Br6",
                        "12.7x108 mm B-32 57-BZ-542",
                        "Armor-piercing incendiary steel core",
                        0.0482d,
                        830d,
                        20d,
                        HeavyRangeMetres,
                        HeavyRangeToleranceMetres,
                        ProtocolAmmunitionMapping.Installed(
                            ProtocolAmmunitionIdentityStatus.ExactDesignation,
                            "5cde8864d7f00c0010373be1",
                            "patron_127x108",
                            "12.7x108mm B-32",
                            "Caliber127x108",
                            0.0483d,
                            0.048d,
                            818d,
                            "GAU Index - 57-BZ-542",
                            "Индекс ГАУ - 57-БЗ-542",
                            "48 gram",
                            "The designation is exact; the installed mass differs from both the locale and nominal threat mass."))
                });

        internal static IReadOnlyList<ProtocolThreatDefinition> All => Definitions;

        internal static IReadOnlyList<ProtocolThreatDefinition> ForClass(string protectionClass)
        {
            if (string.IsNullOrWhiteSpace(protectionClass))
            {
                ThrowMissingProtectionClass(nameof(protectionClass));
            }
            return Array.AsReadOnly(
                Definitions
                    .Where(definition => string.Equals(
                        definition.ProtectionClass,
                        protectionClass,
                        StringComparison.Ordinal))
                    .ToArray());
        }

        internal static bool TryGet(string threatId, out ProtocolThreatDefinition? definition)
        {
            definition = Definitions.FirstOrDefault(candidate => string.Equals(
                candidate.ThreatId,
                threatId,
                StringComparison.Ordinal));
            return definition != null;
        }

        private static ProtocolThreatDefinition Threat(
            string threatId,
            string protectionClass,
            string cartridgeDesignation,
            string projectileConstruction,
            double projectileMassKilograms,
            double nominalVelocityMetresPerSecond,
            double velocityToleranceMetresPerSecond,
            double testDistanceMetres,
            double testDistanceToleranceMetres,
            ProtocolAmmunitionMapping ammunitionMapping)
        {
            return new ProtocolThreatDefinition(
                threatId,
                protectionClass,
                cartridgeDesignation,
                projectileConstruction,
                projectileMassKilograms,
                nominalVelocityMetresPerSecond - velocityToleranceMetresPerSecond,
                nominalVelocityMetresPerSecond + velocityToleranceMetresPerSecond,
                testDistanceMetres,
                testDistanceToleranceMetres,
                VelocityMeasurementDistanceMetres,
                RequiredRifledShots,
                MaximumImpactAngleDegrees,
                MinimumSeparationDiameters,
                new[] { ammunitionMapping });
        }

        [DoesNotReturn]
        private static void ThrowMissingProtectionClass(string parameterName)
        {
            throw new ArgumentException("A protection-class identifier is required.", parameterName);
        }
    }
}

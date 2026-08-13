using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace BallisticsLab.Core
{
    internal static class CampaignCatalog
    {
        private const string GranitBr4TemplateId = "65573fa5655447403702a816";
        private const string GranitBr5TemplateId = "64afc71497cf3a403c01ff38";
        private const double DefaultSpacingMetres = 0.15d;
        private const double DefaultThicknessMetres = 0.0127d;
        private const double DefaultDistanceMetres = 8d;
        private const double MaximumUnallocatedEnergyJoules = 1000000000000d;
        private const int MinimumInstalledSteelArmorClass = 3;
        private const int MaximumInstalledSteelArmorClass = 6;
        private static readonly IReadOnlyList<ProtocolThreatDefinition> ScreeningThreats =
            Array.AsReadOnly(GostProtocolCatalog.All
                .Where(threat => threat.AmmunitionMappings.Count(mapping =>
                    mapping.CanQualifySimulationScreening) == 1
                    && TryParseInstalledSteelArmorClass(threat, out _))
                .ToArray());

        internal static IReadOnlyList<ProtocolThreatDefinition> ProtocolScreeningThreats =>
            ScreeningThreats;

        internal static CampaignDefinition ControlledFixtureBaseline()
        {
            return new CampaignDefinition(
                "controlled-fixture-baseline",
                "Controlled fixture baseline",
                new List<CampaignCaseDefinition>
                {
                    MaterialCase("steel-c6-one", "One-layer class-6 steel", "ArmoredSteel", 6, 1),
                    MaterialCase("steel-c3-two", "Two-layer class-3 steel", "ArmoredSteel", 3, 2),
                    MaterialCase("steel-c4-three", "Three-layer class-4 steel", "ArmoredSteel", 4, 3),
                    MaterialCase("steel-c6-three", "Three-layer class-6 steel", "ArmoredSteel", 6, 3),
                    MaterialCase("steel-c3-four", "Four-layer class-3 steel", "ArmoredSteel", 3, 4),
                    MaterialCase("steel-c3-five", "Five-layer class-3 steel", "ArmoredSteel", 3, 5),
                    MaterialCase("steel-c3-six", "Six-layer class-3 steel", "ArmoredSteel", 3, 6),
                    MaterialCase(
                        "steel-c3-spaced-two",
                        "Spaced two-layer class-3 steel",
                        "ArmoredSteel",
                        3,
                        2,
                        spacingMetres: 0.30d),
                    TemplateCase("granit-br4", "Granit BR4 game preset", GranitBr4TemplateId),
                    TemplateCase("granit-br5", "Granit BR5 game preset", GranitBr5TemplateId)
                });
        }

        internal static CampaignDefinition PhysicalMaterialMatrix()
        {
            return new CampaignDefinition(
                "physical-material-matrix",
                "Physical material matrix",
                new List<CampaignCaseDefinition>
                {
                    PhysicalMaterialCase("steel", "Armored steel", "ArmoredSteel"),
                    PhysicalMaterialCase("ceramic", "Ceramic", "Ceramic"),
                    PhysicalMaterialCase("uhmwpe", "UHMWPE", "UHMWPE"),
                    PhysicalMaterialCase("titan", "Titan", "Titan"),
                    PhysicalMaterialCase("aluminium", "Aluminium", "Aluminium"),
                    PhysicalMaterialCase("aramid", "Aramid", "Aramid", armorClass: 2),
                    PhysicalMaterialCase("combined", "Combined", "Combined")
                });
        }

        internal static CampaignDefinition GostSimulationScreening(string threatId)
        {
            if (!GostProtocolCatalog.TryGet(threatId, out ProtocolThreatDefinition? threat)
                || threat == null)
            {
                throw new ArgumentException("Unknown protocol threat.", nameof(threatId));
            }
            ProtocolAmmunitionMapping[] exactMappings = threat.AmmunitionMappings
                .Where(mapping => mapping.CanQualifySimulationScreening)
                .ToArray();
            if (exactMappings.Length != 1
                || !TryParseInstalledSteelArmorClass(threat, out int armorClass))
            {
                throw new InvalidOperationException(
                    "The selected threat does not have one unambiguous installed ammunition mapping and armored-steel sample class.");
            }
            ProtocolAmmunitionMapping mapping = exactMappings[0];
            var campaignCase = new CampaignCaseDefinition(
                threat.ThreatId,
                threat.ProtectionClass + " - " + threat.CartridgeDesignation,
                CampaignFixtureSelectorKind.MaterialAndArmorClass,
                string.Empty,
                "ArmoredSteel",
                armorClass,
                1,
                DefaultSpacingMetres,
                DefaultThicknessMetres,
                threat.TestDistanceMetres,
                0d,
                true,
                threat.RequiredQualifyingShots,
                CampaignResetPolicy.BeforeEachCase,
                0d,
                10d,
                false,
                true,
                true,
                0.000001d,
                MaximumUnallocatedEnergyJoules,
                CampaignShotSequencePolicy.SameFixtureProtocolPattern,
                threat.ThreatId,
                mapping.TemplateId);
            return new CampaignDefinition(
                "simulation-screening-" + threat.ThreatId,
                threat.ProtectionClass + " simulation screening",
                new[] { campaignCase });
        }

        private static bool TryParseInstalledSteelArmorClass(
            ProtocolThreatDefinition threat,
            out int armorClass)
        {
            armorClass = 0;
            return threat.ProtectionClass.Length >= 3
                && int.TryParse(
                    threat.ProtectionClass.AsSpan(2),
                    NumberStyles.None,
                    CultureInfo.InvariantCulture,
                    out armorClass)
                && armorClass >= MinimumInstalledSteelArmorClass
                && armorClass <= MaximumInstalledSteelArmorClass;
        }

        private static CampaignCaseDefinition MaterialCase(
            string caseId,
            string label,
            string material,
            int armorClass,
            int layerCount,
            double spacingMetres = DefaultSpacingMetres)
        {
            return new CampaignCaseDefinition(
                caseId,
                label,
                CampaignFixtureSelectorKind.MaterialAndArmorClass,
                string.Empty,
                material,
                armorClass,
                layerCount,
                spacingMetres,
                DefaultThicknessMetres,
                DefaultDistanceMetres,
                0d,
                true,
                1,
                CampaignResetPolicy.BeforeEachShot,
                0.10d,
                2.50d,
                false,
                false,
                false,
                0.000001d,
                MaximumUnallocatedEnergyJoules);
        }

        private static CampaignCaseDefinition TemplateCase(
            string caseId,
            string label,
            string templateId)
        {
            return new CampaignCaseDefinition(
                caseId,
                label,
                CampaignFixtureSelectorKind.ExactTemplate,
                templateId,
                string.Empty,
                0,
                1,
                DefaultSpacingMetres,
                DefaultThicknessMetres,
                DefaultDistanceMetres,
                0d,
                true,
                1,
                CampaignResetPolicy.BeforeEachShot,
                0.10d,
                2.50d,
                false,
                false,
                false,
                0.000001d,
                MaximumUnallocatedEnergyJoules);
        }

        private static CampaignCaseDefinition PhysicalMaterialCase(
            string caseId,
            string label,
            string material,
            int armorClass = 4)
        {
            return new CampaignCaseDefinition(
                caseId,
                label,
                CampaignFixtureSelectorKind.MaterialAndArmorClass,
                string.Empty,
                material,
                armorClass,
                1,
                DefaultSpacingMetres,
                DefaultThicknessMetres,
                DefaultDistanceMetres,
                0d,
                true,
                3,
                CampaignResetPolicy.BeforeEachShot,
                0.40d,
                1.50d,
                false,
                true,
                true,
                0.000001d,
                MaximumUnallocatedEnergyJoules);
        }
    }
}

using System.Collections.Generic;

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

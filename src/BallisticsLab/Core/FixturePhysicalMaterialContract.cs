using System;
using System.Globalization;

namespace BallisticsLab.Core
{
    internal static class FixturePhysicalMaterialContract
    {
        internal const int SurfaceSchema = 1;

        internal static string CreatePlateSurfaceIdentity(long fixtureId, int layerIndex)
        {
            if (fixtureId <= 0L)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(fixtureId),
                    "Fixture identity must be positive.");
            }
            if (layerIndex < 0)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(layerIndex),
                    "Layer index must be non-negative.");
            }

            return "fixture/"
                + fixtureId.ToString(CultureInfo.InvariantCulture)
                + "/plate/"
                + layerIndex.ToString(CultureInfo.InvariantCulture);
        }

        internal static string CreateBackstopSurfaceIdentity(long fixtureId)
        {
            if (fixtureId <= 0L)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(fixtureId),
                    "Fixture identity must be positive.");
            }
            return "fixture/"
                + fixtureId.ToString(CultureInfo.InvariantCulture)
                + "/backstop";
        }

        internal static bool TryMapArmorMaterial(
            string? armorMaterial,
            out string physicalMaterialClass)
        {
            switch (armorMaterial)
            {
                case "ArmoredSteel":
                    physicalMaterialClass = "ArmoredSteel";
                    return true;
                case "Ceramic":
                    physicalMaterialClass = "Ceramic";
                    return true;
                case "UHMWPE":
                    physicalMaterialClass = "Polymer";
                    return true;
                case "Titan":
                    physicalMaterialClass = "Titanium";
                    return true;
                case "Aluminium":
                    physicalMaterialClass = "Aluminum";
                    return true;
                case "Aramid":
                    physicalMaterialClass = "Fabric";
                    return true;
                case "Combined":
                    physicalMaterialClass = "CompositeArmor";
                    return true;
                case "Glass":
                    physicalMaterialClass = "Glass";
                    return true;
                default:
                    physicalMaterialClass = string.Empty;
                    return false;
            }
        }

        internal static bool IsCanonicalPhysicalMaterialClass(string? materialClass)
        {
            switch (materialClass)
            {
                case "SoftTissue":
                case "Bone":
                case "Fabric":
                case "Polymer":
                case "Wood":
                case "Glass":
                case "Aluminum":
                case "MildSteel":
                case "ArmoredSteel":
                case "Titanium":
                case "Ceramic":
                case "CompositeArmor":
                case "Concrete":
                case "Soil":
                case "Other":
                    return true;
                default:
                    return false;
            }
        }
    }
}

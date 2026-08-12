using System;
using System.Globalization;
using System.Text;

namespace BallisticsLab.Core
{
    public static class LabPolicies
    {
        public const int MaximumLayers = 6;
        public const int MaximumRecords = 500;
        public const float InstalledBodyArmorFragmentationChance = 0.249f;

        public static bool IsFiniteNonNegative(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value) && value >= 0f;
        }

        public static float ResolveBodyArmorFragmentationChance(float presetValue)
        {
            return IsFiniteNonNegative(presetValue)
                && presetValue > 0f
                && presetValue <= 1f
                    ? presetValue
                    : InstalledBodyArmorFragmentationChance;
        }

        public static string AuthoritativeAmmoValue(string templateValue, string itemValue)
        {
            return !string.IsNullOrWhiteSpace(templateValue)
                ? templateValue
                : itemValue ?? string.Empty;
        }

        public static bool ShouldDisplayBodyTelemetry(
            float healthBefore,
            float healthAfter,
            string armorChanges)
        {
            return healthBefore > 0f
                || healthAfter > 0f
                || !string.IsNullOrWhiteSpace(armorChanges);
        }

        public static float LayerCenterOffset(
            int layerIndex,
            float faceGap,
            float plateThickness)
        {
            return Math.Max(0, layerIndex) * (faceGap + plateThickness);
        }

        public static float BackstopCenterOffset(
            int layerCount,
            float faceGap,
            float plateThickness,
            float faceClearance,
            float backstopThickness)
        {
            int lastLayerIndex = Math.Max(0, layerCount - 1);
            return LayerCenterOffset(lastLayerIndex, faceGap, plateThickness)
                + plateThickness * 0.5f
                + faceClearance
                + backstopThickness * 0.5f;
        }

        public static float ImpactAngleDegrees(float directionDotNormal)
        {
            float cosine = Math.Abs(directionDotNormal);
            if (cosine > 1f)
            {
                cosine = 1f;
            }

            return (float)(Math.Acos(cosine) * 180.0 / Math.PI);
        }

        public static bool RequiresFixtureContinuationCorrection(int bulletState)
        {
            return bulletState == 1 || bulletState == 3;
        }

        public static float PenetratedChildFactor(
            float penetrationPower,
            float penetrationLevel,
            float colliderDamageModifier,
            float ammunitionDamageModifier)
        {
            float factor = (penetrationPower - penetrationLevel) / 100f
                + colliderDamageModifier
                + ammunitionDamageModifier;
            return Clamp01(factor);
        }

        public static float DeviatedChildVelocityFactor(float colliderPenetrationChance)
        {
            return 0.8f + 0.2f * Clamp01(colliderPenetrationChance);
        }

        public static float DeviatedChildOutcomeFactor(float colliderPenetrationChance)
        {
            return 0.2f + 0.8f * Clamp01(colliderPenetrationChance);
        }

        public static string ShotChainId(string shooterProfileId, int fireIndex, int rootRandomSeed)
        {
            return (shooterProfileId ?? string.Empty)
                + ":" + fireIndex.ToString(CultureInfo.InvariantCulture)
                + ":" + rootRandomSeed.ToString(CultureInfo.InvariantCulture);
        }

        public static string OutcomeName(int bulletState, bool blocked, bool deflected)
        {
            if (deflected || bulletState == 2)
            {
                return "RICOCHET";
            }

            if (bulletState == 4)
            {
                return blocked ? "STOPPED / ARMOR BLOCK" : "STOPPED";
            }

            if (bulletState == 1)
            {
                return "PENETRATED / DEVIATED";
            }

            if (bulletState == 3)
            {
                return "PENETRATED / FRAGMENTED";
            }

            if (bulletState == 0)
            {
                return "PENETRATED / CONTINUING";
            }

            return "UNRESOLVED (" + bulletState.ToString(CultureInfo.InvariantCulture) + ")";
        }

        public static string Csv(string value)
        {
            value ??= string.Empty;
            if (value.IndexOfAny(new[] { ',', '"', '\r', '\n' }) < 0)
            {
                return value;
            }

            return "\"" + value.Replace("\"", "\"\"") + "\"";
        }

        public static string Json(string value)
        {
            if (value == null)
            {
                return "null";
            }

            StringBuilder builder = new StringBuilder(value.Length + 2);
            builder.Append('"');
            foreach (char character in value)
            {
                switch (character)
                {
                    case '"': builder.Append("\\\""); break;
                    case '\\': builder.Append("\\\\"); break;
                    case '\b': builder.Append("\\b"); break;
                    case '\f': builder.Append("\\f"); break;
                    case '\n': builder.Append("\\n"); break;
                    case '\r': builder.Append("\\r"); break;
                    case '\t': builder.Append("\\t"); break;
                    default:
                        if (character < 32)
                        {
                            builder.Append("\\u");
                            builder.Append(((int)character).ToString("x4", CultureInfo.InvariantCulture));
                        }
                        else
                        {
                            builder.Append(character);
                        }
                        break;
                }
            }

            builder.Append('"');
            return builder.ToString();
        }

        private static float Clamp01(float value)
        {
            if (value <= 0f)
            {
                return 0f;
            }

            if (value >= 1f)
            {
                return 1f;
            }

            return value;
        }
    }
}

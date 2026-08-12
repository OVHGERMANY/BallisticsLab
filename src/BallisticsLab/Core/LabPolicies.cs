using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace BallisticsLab.Core
{
    public readonly struct LabColliderBallisticSettings
    {
        public LabColliderBallisticSettings(
            float penetrationLevel,
            float penetrationChance,
            float ricochetChance,
            float fragmentationChance,
            float trajectoryDeviationChance,
            float trajectoryDeviation)
        {
            PenetrationLevel = penetrationLevel;
            PenetrationChance = penetrationChance;
            RicochetChance = ricochetChance;
            FragmentationChance = fragmentationChance;
            TrajectoryDeviationChance = trajectoryDeviationChance;
            TrajectoryDeviation = trajectoryDeviation;
        }

        public float PenetrationLevel { get; }
        public float PenetrationChance { get; }
        public float RicochetChance { get; }
        public float FragmentationChance { get; }
        public float TrajectoryDeviationChance { get; }
        public float TrajectoryDeviation { get; }
    }

    public static class LabPolicies
    {
        public const int MaximumLayers = 6;
        public const int MaximumRecords = 500;
        public const float InstalledBodyArmorPenetrationLevel = 0f;
        public const float InstalledBodyArmorPenetrationChance = 0.097f;
        public const float InstalledBodyArmorRicochetChance = 0.378f;
        public const float InstalledBodyArmorFragmentationChance = 0.249f;
        public const float InstalledBodyArmorTrajectoryDeviationChance = 0.28f;
        public const float InstalledBodyArmorTrajectoryDeviation = 0.463f;

        public static bool ShouldSaveReport(int recordCount, long revision, long savedRevision)
        {
            return recordCount > 0 && revision > savedRevision;
        }

        public static IReadOnlyList<T> SelectChangedChains<T>(
            IReadOnlyList<T> records,
            long savedSequence,
            Func<T, long> sequence,
            Func<T, string> chainId)
        {
            if (records == null || records.Count == 0 || sequence == null || chainId == null)
            {
                return Array.Empty<T>();
            }

            HashSet<string> changedChains = new HashSet<string>(StringComparer.Ordinal);
            for (int index = 0; index < records.Count; index++)
            {
                T record = records[index];
                string id = chainId(record);
                if (sequence(record) > savedSequence && !string.IsNullOrEmpty(id))
                {
                    changedChains.Add(id);
                }
            }

            List<T> selected = new List<T>();
            for (int index = 0; index < records.Count; index++)
            {
                T record = records[index];
                string id = chainId(record);
                if ((!string.IsNullOrEmpty(id) && changedChains.Contains(id))
                    || (string.IsNullOrEmpty(id) && sequence(record) > savedSequence))
                {
                    selected.Add(record);
                }
            }
            return selected;
        }

        public static string ReportStem(DateTime utc, int captureOrdinal)
        {
            return "BallisticsLab-"
                + utc.ToUniversalTime().ToString("yyyyMMdd-HHmmss-fff", CultureInfo.InvariantCulture)
                + "-"
                + Math.Max(1, captureOrdinal).ToString("D3", CultureInfo.InvariantCulture);
        }

        public static bool IsFiniteNonNegative(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value) && value >= 0f;
        }

        public static LabColliderBallisticSettings ResolveBodyArmorBallisticSettings(
            IReadOnlyList<float> presetValues)
        {
            if (presetValues != null
                && presetValues.Count >= 6
                && IsFiniteNonNegative(presetValues[0])
                && IsProbability(presetValues[1])
                && IsProbability(presetValues[2])
                && IsPositiveProbability(presetValues[3])
                && IsProbability(presetValues[4])
                && IsProbability(presetValues[5]))
            {
                return new LabColliderBallisticSettings(
                    presetValues[0],
                    presetValues[1],
                    presetValues[2],
                    presetValues[3],
                    presetValues[4],
                    presetValues[5]);
            }

            return new LabColliderBallisticSettings(
                InstalledBodyArmorPenetrationLevel,
                InstalledBodyArmorPenetrationChance,
                InstalledBodyArmorRicochetChance,
                InstalledBodyArmorFragmentationChance,
                InstalledBodyArmorTrajectoryDeviationChance,
                InstalledBodyArmorTrajectoryDeviation);
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

        private static bool IsProbability(float value)
        {
            return IsFiniteNonNegative(value) && value <= 1f;
        }

        private static bool IsPositiveProbability(float value)
        {
            return IsProbability(value) && value > 0f;
        }
    }
}

using EFT;
using EFT.Ballistics;
using UnityEngine;
using BallisticsLab.Core;

namespace BallisticsLab.Runtime.Fixtures
{
    internal sealed class LabPlateCollider : BallisticCollider
    {
        internal LabPlateRuntime Runtime { get; private set; }

        internal void Configure(LabPlateRuntime runtime)
        {
            Runtime = runtime;
            TypeOfMaterial = MaterialType.BodyArmor;
            PenetrationLevel = 0f;
            PenetrationChance = 0f;
            RicochetChance = 0f;
            FragmentationChance = LabPolicies.ResolveBodyArmorFragmentationChance(
                ReadBodyArmorFragmentationChance());
            TrajectoryDeviationChance = 0f;
            TrajectoryDeviation = 0f;
            Associate(TypeOfMaterial);
        }

        private static float ReadBodyArmorFragmentationChance()
        {
            BallisticPreset[] presets = EFTHardSettings.Instance?.ColliderPresets;
            if (presets != null)
            {
                foreach (BallisticPreset preset in presets)
                {
                    if (preset != null
                        && preset.MaterialType == MaterialType.BodyArmor
                        && preset.values != null
                        && preset.values.Length > 3)
                    {
                        return preset[3];
                    }
                }
            }

            return float.NaN;
        }

        public override bool Deflects(
            float hitCosDirectionToNormal,
            Shot shot,
            Vector3 hitPoint,
            Vector3 shotNormal,
            Vector3 shotDirection)
        {
            if (Runtime?.Armor == null || Runtime.Armor.Repairable.Durability <= 0f)
            {
                return false;
            }

            if (Runtime.Armor.Deflects(shotDirection, shotNormal, shot))
            {
                return true;
            }

            Runtime.Armor.SetPenetrationStatus(shot);
            return false;
        }

        public override bool IsPenetrated(Shot shot, Vector3 hitPoint)
        {
            if (Runtime?.Armor == null || Runtime.Armor.Repairable.Durability <= 0f)
            {
                return true;
            }

            if (shot.BlockedBy.HasValue)
            {
                return false;
            }

            if (!Runtime.TryGetResistance(shot.PenetrationPower, out ArmorResistanceData resistance))
            {
                return true;
            }

            return shot.PenetrationPower * resistance.CF > PenetrationLevel;
        }

        public override PlayerHitInfo ApplyHit(DamageInfo damageInfo, ShotId shotID)
        {
            base.ApplyHit(damageInfo, shotID);
            if (damageInfo.IsForwardHit && Runtime != null)
            {
                Runtime.ApplyDurabilityDamage(ref damageInfo);
            }

            return null;
        }
    }
}

using System;
using EFT;
using EFT.Ballistics;
using UnityEngine;
using BallisticsLab.Core;

namespace BallisticsLab.Runtime.Fixtures
{
    public sealed class LabPlateCollider : BallisticCollider
    {
        internal LabPlateRuntime? Runtime { get; private set; }

        internal void Configure(LabPlateRuntime runtime)
        {
            Runtime = runtime;
            TypeOfMaterial = MaterialType.BodyArmor;
            LabColliderBallisticSettings settings = LabPolicies.ResolveBodyArmorBallisticSettings(
                ReadBodyArmorPresetValues());
            PenetrationLevel = settings.PenetrationLevel;
            PenetrationChance = settings.PenetrationChance;
            RicochetChance = settings.RicochetChance;
            FragmentationChance = settings.FragmentationChance;
            TrajectoryDeviationChance = settings.TrajectoryDeviationChance;
            TrajectoryDeviation = settings.TrajectoryDeviation;
            Associate(TypeOfMaterial);
        }

        private static float[]? ReadBodyArmorPresetValues()
        {
            BallisticPreset[]? presets = EFTHardSettings.Instance?.ColliderPresets;
            if (presets != null)
            {
                foreach (BallisticPreset preset in presets)
                {
                    if (preset != null
                        && preset.MaterialType == MaterialType.BodyArmor
                        && preset.values != null
                        && preset.values.Length >= 6)
                    {
                        return preset.values;
                    }
                }
            }

            return null;
        }

        public override bool Deflects(
            float _hitCosDirectionToNormal,
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
            if (shot == null)
            {
                throw new ArgumentNullException(nameof(shot));
            }

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

        public override PlayerHitInfo? ApplyHit(DamageInfo damageInfo, ShotId shotID)
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

using System;
using System.Reflection;
using BallisticsLab.Core;
using BallisticsLab.Runtime.Fixtures;
using BallisticsLab.Runtime.Telemetry;
using EFT;
using EFT.Ballistics;
using EFT.InventoryLogic;
using HarmonyLib;
using SPT.Reflection.Patching;

namespace BallisticsLab.Runtime.Patches
{
    internal sealed class FragmentContinuationPatch : ModulePatch
    {
        private readonly MethodInfo _target;

        internal FragmentContinuationPatch(MethodInfo target)
            : base("com.janky.ballisticslab.fixture-continuation")
        {
            _target = target ?? throw new ArgumentNullException(nameof(target));
        }

        protected override MethodBase GetTargetMethod()
        {
            return _target;
        }

        [PatchPostfix]
        [HarmonyPriority(Priority.Last)]
        private static void Postfix(Shot __instance)
        {
            try
            {
                if (!LabRuntime.IsSessionActive
                    || __instance == null
                    || !__instance.IsForwardHit
                    || !(__instance.HittedBallisticCollider is LabPlateCollider collider)
                    || collider.Runtime == null
                    || __instance.Fragments == null
                    || __instance.Fragments.Count == 0)
                {
                    return;
                }

                if (!LabPolicies.RequiresFixtureContinuationCorrection((int)__instance.BulletState))
                {
                    return;
                }

                Ammo ammunition = __instance.Ammo as Ammo;
                // EFT applies these factors only when the hit collider is a BodyPartCollider.
                // A fixture stays generic to avoid same-body-part suppression between physical layers.
                float penetrationFactor = LabPolicies.PenetratedChildFactor(
                    __instance.PenetrationPower,
                    collider.PenetrationLevel,
                    0f,
                    ammunition?.PenetrationDamageMod ?? 0f);
                float velocityFactor = __instance.BulletState == Shot.EBulletState.DeviationHit
                    ? LabPolicies.DeviatedChildVelocityFactor(collider.PenetrationChance)
                    : 1f;
                float outcomeFactor = __instance.BulletState == Shot.EBulletState.DeviationHit
                    ? LabPolicies.DeviatedChildOutcomeFactor(collider.PenetrationChance)
                    : 1f;

                if (!LabPolicies.IsFiniteNonNegative(penetrationFactor)
                    || !LabPolicies.IsFiniteNonNegative(velocityFactor)
                    || !LabPolicies.IsFiniteNonNegative(outcomeFactor))
                {
                    return;
                }

                foreach (Shot child in __instance.Fragments)
                {
                    if (child == null)
                    {
                        continue;
                    }

                    float damageBefore = child.Damage;
                    float penetrationBefore = child.PenetrationPower;
                    if (!LabPolicies.IsFiniteNonNegative(damageBefore)
                        || !LabPolicies.IsFiniteNonNegative(penetrationBefore))
                    {
                        continue;
                    }

                    float correctedDamage = damageBefore * penetrationFactor;
                    float correctedPenetration = penetrationBefore * penetrationFactor;
                    float armorCf = 1f;
                    if (collider.Runtime.TryGetResistance(
                            correctedPenetration,
                            out ArmorResistanceData resistance))
                    {
                        armorCf = resistance.CF;
                    }

                    if (!LabPolicies.IsFiniteNonNegative(armorCf))
                    {
                        continue;
                    }

                    correctedDamage *= armorCf;
                    correctedPenetration *= armorCf;
                    if (!LabPolicies.IsFiniteNonNegative(correctedDamage)
                        || !LabPolicies.IsFiniteNonNegative(correctedPenetration))
                    {
                        continue;
                    }

                    if (__instance.BulletState == Shot.EBulletState.DeviationHit)
                    {
                        float correctedPenetrationChance = child.PenetrationChance * outcomeFactor;
                        float correctedRicochetChance = child.RicochetChance * outcomeFactor;
                        float correctedFragmentationChance = child.FragmentationChance * outcomeFactor;
                        if (!LabPolicies.IsFiniteNonNegative(correctedPenetrationChance)
                            || !LabPolicies.IsFiniteNonNegative(correctedRicochetChance)
                            || !LabPolicies.IsFiniteNonNegative(correctedFragmentationChance)
                            || !LabPolicies.IsFiniteNonNegative(child.Speed * velocityFactor))
                        {
                            continue;
                        }

                        child.PenetrationChance = correctedPenetrationChance;
                        child.RicochetChance = correctedRicochetChance;
                        child.FragmentationChance = correctedFragmentationChance;
                        ScaleChildTrajectory(child, velocityFactor);
                    }

                    child.Damage = correctedDamage;
                    child.PenetrationPower = correctedPenetration;
                    ContinuationAdjustmentStore.Set(
                        child,
                        new ContinuationAdjustment(
                            child,
                            collider.Runtime.FixtureId,
                            collider.Runtime.LayerIndex,
                            __instance.BulletState.ToString(),
                            penetrationFactor,
                            velocityFactor,
                            outcomeFactor,
                            armorCf,
                            damageBefore,
                            penetrationBefore,
                            correctedDamage,
                            correctedPenetration));
                }
            }
            catch (Exception exception)
            {
                Plugin.Log?.LogWarning("Fixture continuation scaling was skipped: " + exception.Message);
            }
        }

        private static void ScaleChildTrajectory(Shot child, float velocityFactor)
        {
            child.Speed *= velocityFactor;
            child.StartVelocity = child.Direction * child.Speed;
            child._currentVelocity = child.StartVelocity;
            if (child.TrajectoryInfo != null)
            {
                child.TrajectoryInfo.Initialize(
                    child.StartPosition,
                    child.StartVelocity,
                    child.BulletMassGram,
                    child.BulletDiameterMilimeters,
                    child.BallisticCoefficient,
                    child.IsPlayerNotAIWithGrenade(child.Player, child.Weapon));
            }
        }
    }
}

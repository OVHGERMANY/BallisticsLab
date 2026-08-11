using System;
using System.Reflection;
using BallisticsLab.Runtime.Telemetry;
using EFT.Ballistics;
using HarmonyLib;
using SPT.Reflection.Patching;
using UnityEngine;

namespace BallisticsLab.Runtime.Patches
{
    internal sealed class CollisionCapturePatch : ModulePatch
    {
        private readonly MethodInfo _target;

        internal CollisionCapturePatch(MethodInfo target)
            : base("com.janky.ballisticslab.collision-capture")
        {
            _target = target ?? throw new ArgumentNullException(nameof(target));
        }

        protected override MethodBase GetTargetMethod()
        {
            return _target;
        }

        [PatchPrefix]
        [HarmonyPriority(Priority.First)]
        private static void Prefix(Shot __instance, Vector3 prevVector3, Vector3 prevVelocity)
        {
            try
            {
                if (!LabRuntime.IsSessionActive || __instance == null)
                {
                    return;
                }

                CollisionSnapshotStore.Set(
                    __instance,
                    new CollisionSnapshot(
                        __instance.Damage,
                        __instance.PenetrationPower,
                        prevVelocity.magnitude,
                        prevVector3));
            }
            catch (Exception exception)
            {
                Plugin.Log?.LogWarning("Collision telemetry was skipped: " + exception.Message);
            }
        }
    }
}


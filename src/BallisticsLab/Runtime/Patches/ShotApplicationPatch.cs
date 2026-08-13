using System;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using BallisticsLab.Runtime.Telemetry;
using EFT.Ballistics;
using HarmonyLib;
using SPT.Reflection.Patching;

namespace BallisticsLab.Runtime.Patches
{
    internal sealed class ShotApplicationPatch : ModulePatch
    {
        private readonly MethodInfo _target;

        internal ShotApplicationPatch(MethodInfo target)
            : base("com.janky.ballisticslab.shot-application")
        {
            _target = target ?? throw new ArgumentNullException(nameof(target));
        }

        protected override MethodBase GetTargetMethod()
        {
            return _target;
        }

        [PatchPrefix]
        [HarmonyPriority(Priority.First)]
        [SuppressMessage(
            "Design",
            "CA1031:Do not catch general exception types",
            Justification = "Optional telemetry must fail open instead of interrupting EFT shot application.")]
        private static void Prefix(Shot shotResult, out ShotApplicationState? __state)
        {
            __state = null;
            try
            {
                if (!LabRuntime.ShouldRecord(shotResult))
                {
                    return;
                }

                __state = new ShotApplicationState(
                    shotResult,
                    CollisionSnapshotStore.Take(shotResult));
            }
            catch (Exception exception)
            {
                Plugin.Log?.LogWarning("Shot application telemetry was skipped: " + exception.Message);
            }
        }

        [PatchPostfix]
        [HarmonyPriority(Priority.Last)]
        [SuppressMessage(
            "Design",
            "CA1031:Do not catch general exception types",
            Justification = "Optional report completion must fail open instead of interrupting EFT shot application.")]
        private static void Postfix(ShotApplicationState? __state)
        {
            try
            {
                if (__state != null)
                {
                    TelemetryStore.Complete(__state);
                }
            }
            catch (Exception exception)
            {
                Plugin.Log?.LogWarning("Shot report completion was skipped: " + exception.Message);
            }
        }
    }
}

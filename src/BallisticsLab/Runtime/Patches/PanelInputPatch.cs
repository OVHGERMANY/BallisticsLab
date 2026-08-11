using System;
using System.Reflection;
using EFT.InputSystem;
using HarmonyLib;
using SPT.Reflection.Patching;
using static EFT.InputSystem.InputNode;

namespace BallisticsLab.Runtime.Patches
{
    internal sealed class PanelInputPatch : ModulePatch
    {
        private readonly MethodInfo _target;

        internal PanelInputPatch(MethodInfo target)
            : base("com.janky.ballisticslab.panel-input")
        {
            _target = target ?? throw new ArgumentNullException(nameof(target));
        }

        protected override MethodBase GetTargetMethod()
        {
            return _target;
        }

        [PatchPrefix]
        [HarmonyPriority(Priority.First)]
        private static bool Prefix(ECommand command, ref ETranslateResult __result)
        {
            if (!LabRuntime.ShouldBlockShootingCommand(command))
            {
                return true;
            }

            __result = ETranslateResult.Block;
            return false;
        }
    }
}

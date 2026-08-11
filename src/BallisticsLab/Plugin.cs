using System;
using System.IO;
using System.Reflection;
using System.Security.Cryptography;
using BepInEx;
using BepInEx.Bootstrap;
using BepInEx.Logging;
using BallisticsLab.Runtime;
using BallisticsLab.Runtime.Patches;
using SPT.Reflection.Patching;

namespace BallisticsLab
{
    [BepInPlugin(PluginGuid, PluginName, PluginVersion)]
    [BepInProcess("EscapeFromTarkov.exe")]
    [BepInDependency(SptVersionCompatibility.CorePluginGuid, SptVersionCompatibility.SupportedCoreVersionText)]
    public sealed class Plugin : BaseUnityPlugin
    {
        internal const string PluginGuid = "com.janky.ballisticslab";
        internal const string PluginName = "Janky-BallisticsLab";
        internal const string PluginVersion = "0.2.3";

        private CollisionCapturePatch _collisionCapturePatch;
        private FragmentContinuationPatch _fragmentContinuationPatch;
        private ShotApplicationPatch _shotApplicationPatch;
        private PanelInputPatch _panelInputPatch;

        internal static PluginConfiguration Configuration { get; private set; }
        internal static ManualLogSource Log { get; private set; }

        private void Awake()
        {
            Log = Logger;
            Configuration = new PluginConfiguration(Config);

            try
            {
                if (!Configuration.Enabled.Value)
                {
                    Logger.LogInfo(
                        PluginName + " " + PluginVersion
                        + " loaded disabled. No game methods were patched; enable the lab and restart to use it.");
                    return;
                }

                RequireExactSptVersion();
                VerifyGameAssembly();

                MethodInfo collision = TargetMethodResolver.ResolveHandleCollision();
                MethodInfo fragments = TargetMethodResolver.ResolveCreateFragments();
                MethodInfo application = TargetMethodResolver.ResolveShotDelegate();
                MethodInfo playerCommand = TargetMethodResolver.ResolvePlayerCommand();

                _collisionCapturePatch = new CollisionCapturePatch(collision);
                _fragmentContinuationPatch = new FragmentContinuationPatch(fragments);
                _shotApplicationPatch = new ShotApplicationPatch(application);
                _panelInputPatch = new PanelInputPatch(playerCommand);
                EnablePatchesTransactionally();

                LabRuntime.Initialize();
                Logger.LogInfo(
                    PluginName + " " + PluginVersion + " loaded for SPT "
                    + SptVersionCompatibility.SupportedCoreVersionText
                    + ". Lab patches are enabled.");
            }
            catch (Exception exception)
            {
                DisablePatches();
                Logger.LogError(PluginName + " failed to load and left no active patch: " + exception);
                throw;
            }
        }

        private void Update()
        {
            LabRuntime.Update();
        }

        private void LateUpdate()
        {
            LabRuntime.LateUpdate();
        }

        private void OnGUI()
        {
            LabRuntime.OnGUI();
        }

        private void OnDestroy()
        {
            LabRuntime.Shutdown();
            DisablePatches();
        }

        private void RequireExactSptVersion()
        {
            PluginInfo core;
            if (!Chainloader.PluginInfos.TryGetValue(SptVersionCompatibility.CorePluginGuid, out core)
                || core?.Metadata?.Version == null
                || !SptVersionCompatibility.IsExactSupportedCoreVersion(core.Metadata.Version))
            {
                string actual = core?.Metadata?.Version?.ToString() ?? "missing";
                throw new InvalidOperationException(
                    "Loaded SPT core version is " + actual + "; exact version "
                    + SptVersionCompatibility.SupportedCoreVersionText + " is required.");
            }
        }

        private void VerifyGameAssembly()
        {
            string location = typeof(EFT.Ballistics.Shot).Assembly.Location;
            if (string.IsNullOrWhiteSpace(location))
            {
                location = Path.Combine(
                    BepInEx.Paths.GameRootPath,
                    "EscapeFromTarkov_Data",
                    "Managed",
                    "Assembly-CSharp.dll");
            }

            if (!File.Exists(location))
            {
                Logger.LogWarning(
                    "Assembly-CSharp could not be hashed because the file path was unavailable. "
                    + "Exact method signatures will remain the compatibility gate.");
                return;
            }

            using (FileStream stream = File.OpenRead(location))
            using (SHA256 sha = SHA256.Create())
            {
                string actual = BitConverter.ToString(sha.ComputeHash(stream)).Replace("-", string.Empty);
                if (!string.Equals(actual, SptVersionCompatibility.VerifiedAssemblyHash, StringComparison.OrdinalIgnoreCase))
                {
                    Logger.LogWarning(
                        "Assembly-CSharp hash is " + actual + "; verified hash is "
                        + SptVersionCompatibility.VerifiedAssemblyHash
                        + ". Exact signatures resolved, but this build is unverified.");
                }
            }
        }

        private void EnablePatchesTransactionally()
        {
            try
            {
                _panelInputPatch.Enable();
                _collisionCapturePatch.Enable();
                _fragmentContinuationPatch.Enable();
                _shotApplicationPatch.Enable();
            }
            catch
            {
                DisablePatches();
                throw;
            }
        }

        private void DisablePatches()
        {
            Disable(_shotApplicationPatch);
            Disable(_fragmentContinuationPatch);
            Disable(_collisionCapturePatch);
            Disable(_panelInputPatch);
        }

        private void Disable(ModulePatch patch)
        {
            if (patch?.TargetMethod == null)
            {
                return;
            }

            try
            {
                patch.Disable();
            }
            catch (Exception exception)
            {
                Logger?.LogError("Patch rollback failed: " + exception);
            }
        }
    }
}

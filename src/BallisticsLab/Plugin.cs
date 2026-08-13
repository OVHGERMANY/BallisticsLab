using System;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Reflection;
using System.Security.Cryptography;
using BepInEx;
using BepInEx.Bootstrap;
using BepInEx.Logging;
using BallisticsLab.Core;
using BallisticsLab.Runtime;
using BallisticsLab.Runtime.Patches;
using SPT.Reflection.Patching;

namespace BallisticsLab
{
#pragma warning disable CA2243 // BepInEx uses reverse-domain plugin identifiers, not System.Guid values.
    [BepInPlugin(PluginGuid, PluginName, PluginVersion)]
    [BepInProcess("EscapeFromTarkov.exe")]
    [BepInDependency(SptVersionCompatibility.CorePluginGuid, SptVersionCompatibility.SupportedCoreVersionText)]
#pragma warning restore CA2243
    public sealed class Plugin : BaseUnityPlugin
    {
        internal const string PluginGuid = LabBuild.PluginGuid;
        internal const string PluginName = LabBuild.PluginName;
        internal const string PluginVersion = LabBuild.PluginVersion;

        private CollisionCapturePatch? _collisionCapturePatch;
        private FragmentContinuationPatch? _fragmentContinuationPatch;
        private ShotApplicationPatch? _shotApplicationPatch;
        private PanelInputPatch? _panelInputPatch;
        private bool _runtimeInitialized;

        internal static PluginConfiguration? Configuration { get; private set; }
        internal static ManualLogSource? Log { get; private set; }

        private void Awake()
        {
            Log = Logger;
            PluginConfiguration configuration = new PluginConfiguration(Config);
            Configuration = configuration;

            try
            {
                if (!configuration.Enabled.Value)
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

                CollisionCapturePatch collisionCapturePatch = new CollisionCapturePatch(collision);
                FragmentContinuationPatch fragmentContinuationPatch = new FragmentContinuationPatch(fragments);
                ShotApplicationPatch shotApplicationPatch = new ShotApplicationPatch(application);
                PanelInputPatch panelInputPatch = new PanelInputPatch(playerCommand);
                _collisionCapturePatch = collisionCapturePatch;
                _fragmentContinuationPatch = fragmentContinuationPatch;
                _shotApplicationPatch = shotApplicationPatch;
                _panelInputPatch = panelInputPatch;
                EnablePatchesTransactionally(
                    panelInputPatch,
                    collisionCapturePatch,
                    fragmentContinuationPatch,
                    shotApplicationPatch);

                LabRuntime.Initialize();
                _runtimeInitialized = true;
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
            if (_runtimeInitialized)
            {
                LabRuntime.Update();
            }
        }

        private void LateUpdate()
        {
            if (_runtimeInitialized)
            {
                LabRuntime.LateUpdate();
            }
        }

        private void OnGUI()
        {
            if (_runtimeInitialized)
            {
                LabRuntime.OnGUI();
            }
        }

        private void OnDestroy()
        {
            if (_runtimeInitialized)
            {
                LabRuntime.Shutdown();
                _runtimeInitialized = false;
            }
            DisablePatches();
        }

        private static void RequireExactSptVersion()
        {
            if (!Chainloader.PluginInfos.TryGetValue(
                    SptVersionCompatibility.CorePluginGuid,
                    out PluginInfo? core)
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
                string actual = BitConverter.ToString(sha.ComputeHash(stream))
                    .Replace("-", string.Empty, StringComparison.Ordinal);
                if (!string.Equals(actual, SptVersionCompatibility.VerifiedAssemblyHash, StringComparison.OrdinalIgnoreCase))
                {
                    Logger.LogWarning(
                        "Assembly-CSharp hash is " + actual + "; verified hash is "
                        + SptVersionCompatibility.VerifiedAssemblyHash
                        + ". Exact signatures resolved, but this build is unverified.");
                }
            }
        }

        private void EnablePatchesTransactionally(
            PanelInputPatch panelInputPatch,
            CollisionCapturePatch collisionCapturePatch,
            FragmentContinuationPatch fragmentContinuationPatch,
            ShotApplicationPatch shotApplicationPatch)
        {
            try
            {
                panelInputPatch.Enable();
                collisionCapturePatch.Enable();
                fragmentContinuationPatch.Enable();
                shotApplicationPatch.Enable();
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

        [SuppressMessage(
            "Design",
            "CA1031:Do not catch general exception types",
            Justification = "Patch rollback must continue and must not replace the original startup failure.")]
        private void Disable(ModulePatch? patch)
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

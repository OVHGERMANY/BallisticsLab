using BepInEx.Configuration;
using UnityEngine;

namespace BallisticsLab.Runtime
{
    internal sealed class PluginConfiguration
    {
        internal PluginConfiguration(ConfigFile config)
        {
            Enabled = config.Bind(
                "General",
                "Enabled",
                false,
                "Master switch (restart required). No game methods are patched when false.");
            PanelShortcut = config.Bind(
                "Controls",
                "Panel Shortcut",
                new KeyboardShortcut(KeyCode.F10, KeyCode.LeftControl),
                "Opens or closes the lab panel while the master switch is enabled.");
            DefaultDistance = config.Bind(
                "Fixture",
                "Default Distance Meters",
                8f,
                new ConfigDescription("Placement distance from the active camera.", new AcceptableValueRange<float>(3f, 50f)));
            DefaultLayerCount = config.Bind(
                "Fixture",
                "Default Layer Count",
                1,
                new ConfigDescription("Initial number of physical armor layers.", new AcceptableValueRange<int>(1, 6)));
            LayerSpacing = config.Bind(
                "Fixture",
                "Layer Spacing Meters",
                0.15f,
                new ConfigDescription("Air gap between plate faces.", new AcceptableValueRange<float>(0.02f, 1f)));
            PlateThickness = config.Bind(
                "Fixture",
                "Plate Thickness Meters",
                0.0127f,
                new ConfigDescription("Physical collider thickness used by every plate.", new AcceptableValueRange<float>(0.003f, 0.1f)));
            CatcherEnabled = config.Bind(
                "Fixture",
                "Enable Backstop",
                true,
                "Places a terminal backstop behind the configured plate stack.");
            TraceEnabled = config.Bind(
                "Telemetry",
                "Show Last Shot Path",
                true,
                "Draws the most recent recorded trajectory while a lab session is active.");
            TraceLifetime = config.Bind(
                "Telemetry",
                "Trace Lifetime Seconds",
                8f,
                new ConfigDescription("How long the last trajectory remains visible.", new AcceptableValueRange<float>(0.25f, 60f)));
            RecordAllSessionShots = config.Bind(
                "Telemetry",
                "Record All Session Shots",
                true,
                "Records shots outside a fixture while the lab session is active, including selected bot hits.");
            AutomaticReportSaving = config.Bind(
                "Telemetry",
                "Automatic Report Saving",
                true,
                "Saves each changed shot-chain batch as a nonempty CSV/JSON pair after a completed burst and when the session or world ends.");
            CampaignSeed = config.Bind(
                "Campaigns",
                "Lab Campaign Seed",
                104729,
                new ConfigDescription(
                    "Derives stable Lab case identifiers. The game shot seed is observed in evidence and is not overridden.",
                    new AcceptableValueRange<int>(1, int.MaxValue)));
        }

        internal ConfigEntry<bool> Enabled { get; }
        internal ConfigEntry<KeyboardShortcut> PanelShortcut { get; }
        internal ConfigEntry<float> DefaultDistance { get; }
        internal ConfigEntry<int> DefaultLayerCount { get; }
        internal ConfigEntry<float> LayerSpacing { get; }
        internal ConfigEntry<float> PlateThickness { get; }
        internal ConfigEntry<bool> CatcherEnabled { get; }
        internal ConfigEntry<bool> TraceEnabled { get; }
        internal ConfigEntry<float> TraceLifetime { get; }
        internal ConfigEntry<bool> RecordAllSessionShots { get; }
        internal ConfigEntry<bool> AutomaticReportSaving { get; }
        internal ConfigEntry<int> CampaignSeed { get; }
    }
}

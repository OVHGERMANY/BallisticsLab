using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Comfort.Common;
using BallisticsLab.Core;
using BallisticsLab.Runtime.Bots;
using BallisticsLab.Runtime.Fixtures;
using BallisticsLab.Runtime.Telemetry;
using EFT;
using EFT.Ballistics;
using EFT.InputSystem;
using UnityEngine;

namespace BallisticsLab.Runtime
{
    internal static class LabRuntime
    {
        private const int WindowId = 1412137;

        private static readonly BotTestController BotController = new BotTestController();
        private static readonly int[] PresetIndices = new int[LabPolicies.MaximumLayers];

        private static bool _initialized;
        private static bool _panelVisible;
        private static bool _sessionActive;
        private static GameWorld? _world;
        private static object? _hideoutPlayerOwner;
        private static bool _enteredShootingRange;
        private static PlateCatalog? _catalog;
        private static FixtureRig? _rig;
        private static Rect _window = new Rect(40f, 60f, 840f, 900f);
        private static Vector2 _scroll;
        private static int _layerCount = 1;
        private static int _selectedLayer;
        private static float _distance = 8f;
        private static float _spacing = 0.15f;
        private static float _thickness = 0.0127f;
        private static float _angle;
        private static string _search = string.Empty;
        private static string _status = "Lab is idle.";
        private static string _shootingModeStatus = "not active";
        private static CursorLockMode _previousCursorLock;
        private static bool _previousCursorVisible;
        private static LineRenderer? _trace;
        private static Material? _traceMaterial;
        private static ShotRecord? _latestRecord;
        private static float _traceUntil;
        private static bool _showAdvancedFixtureControls;
        private static bool _showShotDetails;
        private static GUIStyle? _titleStyle;
        private static GUIStyle? _sectionStyle;
        private static GUIStyle? _statusStyle;
        private static GUIStyle? _buttonStyle;

        internal static bool IsSessionActive => _sessionActive;

        private static PluginConfiguration ActiveConfiguration =>
            Plugin.Configuration
            ?? throw new InvalidOperationException("The lab configuration is not initialized.");

        private static PlateCatalog ActiveCatalog =>
            _catalog
            ?? throw new InvalidOperationException("The armor catalog is not initialized.");

        internal static bool ShouldBlockShootingCommand(ECommand command)
        {
            if (command != ECommand.ToggleShooting
                || !_panelVisible
                || Plugin.Configuration?.Enabled.Value != true)
            {
                return false;
            }

            Vector3 mouse = Input.mousePosition;
            Vector2 guiPoint = new Vector2(mouse.x, Screen.height - mouse.y);
            return _window.Contains(guiPoint);
        }

        internal static void Initialize()
        {
            if (_initialized)
            {
                return;
            }

            PluginConfiguration? config = Plugin.Configuration;
            _layerCount = config?.DefaultLayerCount.Value ?? 1;
            _distance = config?.DefaultDistance.Value ?? 8f;
            _spacing = config?.LayerSpacing.Value ?? 0.15f;
            _thickness = config?.PlateThickness.Value ?? 0.0127f;
            _initialized = true;
        }

        internal static void Update()
        {
            if (!_initialized)
            {
                return;
            }

            PluginConfiguration? config = Plugin.Configuration;
            if (config == null || !config.Enabled.Value)
            {
                if (_sessionActive)
                {
                    EndSession("Master switch disabled; session destroyed.");
                }
                if (_panelVisible)
                {
                    SetPanelVisible(false);
                }
                return;
            }

            GameWorld? currentWorld = Singleton<GameWorld>.Instantiated
                ? Singleton<GameWorld>.Instance
                : null;
            if (!ReferenceEquals(currentWorld, _world))
            {
                if (_sessionActive)
                {
                    EndSession("World changed; session destroyed.");
                }
                _world = currentWorld;
            }

            if (config.PanelShortcut.Value.IsDown())
            {
                SetPanelVisible(!_panelVisible);
            }

            if (_sessionActive)
            {
                PhysicalTelemetrySessionBridge.Update();
                BotController.Update();
                RefreshHideoutShootingModeStatus();
                UpdateTrace();
                SaveAutomaticReport(false);
            }
        }

        internal static void OnGUI()
        {
            if (!_panelVisible || Plugin.Configuration?.Enabled.Value != true)
            {
                return;
            }

            KeepPanelCursorAvailable();

            _window.width = Mathf.Min(_window.width, Screen.width - 20f);
            _window.height = Mathf.Min(_window.height, Screen.height - 20f);
            _window = GUI.Window(WindowId, _window, DrawWindow, "Ballistics Lab " + Plugin.PluginVersion);
        }

        internal static void LateUpdate()
        {
            if (_panelVisible && Plugin.Configuration?.Enabled.Value == true)
            {
                KeepPanelCursorAvailable();
            }
        }

        internal static bool ShouldRecord(Shot shot)
        {
            if (!_sessionActive || shot == null || shot.IsFlyingOutOfTime)
            {
                return false;
            }

            if (shot.HittedBallisticCollider is LabPlateCollider
                || shot.HittedBallisticCollider is LabBackstopCollider)
            {
                return true;
            }

            if (Plugin.Configuration?.RecordAllSessionShots.Value == true)
            {
                return true;
            }

            if (shot.HittedBallisticCollider is BodyPartCollider body
                && BotController.SelectedPlayer != null
                && ReferenceEquals(body.Player, BotController.SelectedPlayer))
            {
                return true;
            }

            return false;
        }

        internal static void NotifyRecord(ShotRecord record)
        {
            _latestRecord = record;
            _traceUntil = Time.time + (Plugin.Configuration?.TraceLifetime.Value ?? 8f);
            _status = "Recorded shot #" + record.Sequence + ": " + record.Outcome + ".";
        }

        internal static void Shutdown()
        {
            EndSession("Plugin shutdown.");
            SetPanelVisible(false);
            _initialized = false;
        }

        [SuppressMessage(
            "Design",
            "CA1031:Do not catch general exception types",
            Justification = "Manual export reports failures in the panel instead of breaking Unity's IMGUI loop.")]
        private static void DrawWindow(int windowId)
        {
            if (_titleStyle == null)
            {
                _titleStyle = new GUIStyle(GUI.skin.label)
                {
                    fontSize = 20,
                    wordWrap = true
                };
                _sectionStyle = new GUIStyle(GUI.skin.label)
                {
                    fontSize = 16
                };
                _statusStyle = new GUIStyle(GUI.skin.box)
                {
                    fontSize = 14,
                    wordWrap = true,
                    padding = new RectOffset(12, 12, 10, 10)
                };
                _buttonStyle = new GUIStyle(GUI.skin.button)
                {
                    fontSize = 15,
                    wordWrap = true,
                    padding = new RectOffset(10, 10, 8, 8)
                };
            }

            GUIStyle sectionStyle = _sectionStyle
                ?? throw new InvalidOperationException("The section style was not initialized.");
            GUIStyle buttonStyle = _buttonStyle
                ?? throw new InvalidOperationException("The button style was not initialized.");

            GUILayout.BeginVertical(GUILayout.ExpandHeight(true));
            GUILayout.Label("BALLISTICS LAB", _titleStyle);
            GUILayout.Label("World: " + DescribeWorld());

            if (!_sessionActive)
            {
                GUILayout.Space(10f);
                if (GUILayout.Button("START LAB SESSION", _buttonStyle, GUILayout.Height(52f)))
                {
                    StartSession();
                }
                GUILayout.Space(8f);
                GUILayout.Box(_status, _statusStyle, GUILayout.ExpandWidth(true));
                GUILayout.EndVertical();
                GUI.DragWindow(new Rect(0f, 0f, 10000f, 28f));
                return;
            }

            _scroll = GUILayout.BeginScrollView(_scroll);
            GUILayout.Label(
                "SESSION ACTIVE | " + TelemetryStore.Count + " recorded shots | "
                    + PhysicalTelemetrySessionBridge.TransitionCount + " physical transitions",
                _sectionStyle);
            GUILayout.Box(_status, _statusStyle, GUILayout.ExpandWidth(true));
            if (_world is HideoutGameWorld)
            {
                GUILayout.Label("Hideout shooting mode: " + _shootingModeStatus);
            }

            if (GUILayout.Button("CLOSE PANEL / RETURN TO SHOOTING", _buttonStyle, GUILayout.Height(48f)))
            {
                SetPanelVisible(false);
            }

            DrawFixtureControls(sectionStyle, buttonStyle);
            DrawBotControls(sectionStyle, buttonStyle);
            DrawLatestShot();

            GUILayout.Space(10f);
            GUILayout.Label("REPORTS", _sectionStyle);
            GUILayout.Label(
                ActiveConfiguration.AutomaticReportSaving.Value
                    ? "Automatic saving is ON. Changed shot chains and physical transitions save after a burst and when the session ends."
                    : "Automatic saving is OFF. Use the manual export button before ending the session.");
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("SAVE REPORT NOW", _buttonStyle, GUILayout.Height(42f)))
            {
                try
                {
                    _status = "Exported: " + TelemetryStore.Export();
                }
                catch (Exception exception)
                {
                    _status = "Export failed: " + exception.Message;
                }
            }
            if (GUILayout.Button("CLEAR RECORDS", _buttonStyle, GUILayout.Height(42f)))
            {
                SaveAutomaticReport(true);
                TelemetryStore.Clear();
                PhysicalTelemetrySessionBridge.ClearCaptured();
                _latestRecord = null;
                HideTrace();
                _status = "Shot and physical-transition records cleared.";
            }
            GUILayout.EndHorizontal();

            if (GUILayout.Button("END SESSION", _buttonStyle, GUILayout.Height(48f)))
            {
                EndSession("Session ended by user.");
            }
            GUILayout.EndScrollView();
            GUILayout.EndVertical();
            GUI.DragWindow(new Rect(0f, 0f, 10000f, 28f));
        }

        private static void DrawFixtureControls(GUIStyle sectionStyle, GUIStyle buttonStyle)
        {
            GUILayout.Space(12f);
            GUILayout.Label("QUICK FIXTURES - ONE CLICK BUILDS, PLACES, AND CLOSES", sectionStyle);
            GUILayout.Label("Pick a fixture and shoot its center. Open the panel again only when you want the next fixture.");
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("1 LAYER\nSTEEL C6", buttonStyle, GUILayout.Height(58f)))
            {
                ApplyPresetAndPlace(ActiveCatalog.FindSteel(6), 1, "one-layer class-6 steel");
            }
            if (GUILayout.Button("2 LAYERS\nSTEEL C3", buttonStyle, GUILayout.Height(58f)))
            {
                ApplyPresetAndPlace(ActiveCatalog.FindSteel(3), 2, "two-layer class-3 steel");
            }
            if (GUILayout.Button("3 LAYERS\nSTEEL C4", buttonStyle, GUILayout.Height(58f)))
            {
                ApplyPresetAndPlace(ActiveCatalog.FindSteel(4), 3, "three-layer class-4 steel");
            }
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            if (GUILayout.Button("3 LAYERS\nSTEEL C6", buttonStyle, GUILayout.Height(58f)))
            {
                ApplyPresetAndPlace(ActiveCatalog.FindSteel(6), 3, "three-layer class-6 steel");
            }
            if (GUILayout.Button("GRANIT BR4\nGAME PRESET", buttonStyle, GUILayout.Height(58f)))
            {
                ApplyPresetAndPlace(
                    ActiveCatalog.FindByTemplateId(PlateCatalog.GranitBr4TemplateId),
                    1,
                    "Granit BR4 game preset");
            }
            if (GUILayout.Button("GRANIT BR5\nGAME PRESET", buttonStyle, GUILayout.Height(58f)))
            {
                ApplyPresetAndPlace(
                    ActiveCatalog.FindByTemplateId(PlateCatalog.GranitBr5TemplateId),
                    1,
                    "Granit BR5 game preset");
            }
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            GUILayout.Label("SAME PLATE, CHOOSE LAYERS:", GUILayout.Width(220f));
            for (int layers = 1; layers <= LabPolicies.MaximumLayers; layers++)
            {
                int requestedLayers = layers;
                if (GUILayout.Button(
                        Invariant(requestedLayers),
                        buttonStyle,
                        GUILayout.Height(44f),
                        GUILayout.MinWidth(54f)))
                {
                    ApplyPresetAndPlace(
                        ClampPresetIndex(PresetIndices[_selectedLayer]),
                        requestedLayers,
                        requestedLayers + "-layer custom fixture");
                }
            }
            GUILayout.EndHorizontal();

            if (_rig != null)
            {
                string durability = string.Join(
                    " | ",
                    _rig.Plates.Select(
                        plate => "L" + (plate.LayerIndex + 1) + " "
                            + Invariant(plate.Durability, "F1") + "/" + Invariant(plate.MaximumDurability, "F1")));
                GUILayout.Box(
                    "ACTIVE FIXTURE #" + _rig.FixtureId + " | " + _rig.Plates.Count + " layer(s)\n"
                    + durability,
                    GUILayout.ExpandWidth(true));
            }

            GUILayout.BeginHorizontal();
            if (GUILayout.Button("REBUILD CURRENT", buttonStyle, GUILayout.Height(44f)))
            {
                PlaceFixture();
            }
            if (GUILayout.Button("RESET DURABILITY", buttonStyle, GUILayout.Height(44f)))
            {
                ResetFixtureDurability(false);
            }
            GUILayout.EndHorizontal();

            _showAdvancedFixtureControls = GUILayout.Toggle(
                _showAdvancedFixtureControls,
                "SHOW ADVANCED FIXTURE CONTROLS",
                buttonStyle,
                GUILayout.Height(42f));
            if (!_showAdvancedFixtureControls)
            {
                return;
            }

            GUILayout.Space(8f);
            GUILayout.Label("ADVANCED FIXTURE CONTROLS", sectionStyle);
            GUILayout.BeginHorizontal();
            GUILayout.Label("Layers: " + _layerCount, GUILayout.Width(120f));
            if (GUILayout.Button("-", GUILayout.Width(40f)))
            {
                _layerCount = Math.Max(1, _layerCount - 1);
                _selectedLayer = Math.Min(_selectedLayer, _layerCount - 1);
            }
            if (GUILayout.Button("+", GUILayout.Width(40f)))
            {
                _layerCount = Math.Min(LabPolicies.MaximumLayers, _layerCount + 1);
            }
            GUILayout.Label("Edit layer " + (_selectedLayer + 1), GUILayout.Width(110f));
            if (GUILayout.Button("Prev", GUILayout.Width(55f)))
            {
                _selectedLayer = (_selectedLayer + _layerCount - 1) % _layerCount;
            }
            if (GUILayout.Button("Next", GUILayout.Width(55f)))
            {
                _selectedLayer = (_selectedLayer + 1) % _layerCount;
            }
            GUILayout.EndHorizontal();

            _search = GUILayout.TextField(_search ?? string.Empty);
            List<int> matches = ActiveCatalog.Search(_search);
            int currentIndex = ClampPresetIndex(PresetIndices[_selectedLayer]);
            PlateCatalogEntry current = ActiveCatalog.Entries[currentIndex];
            GUILayout.Label("Preset: " + current.DisplayName);
            GUILayout.Label("Template: " + current.TemplateId + " | blunt throughput " + Invariant(current.BluntThroughput, "F3"));
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("< matching preset"))
            {
                PresetIndices[_selectedLayer] = CycleMatch(matches, currentIndex, -1);
            }
            if (GUILayout.Button("matching preset >"))
            {
                PresetIndices[_selectedLayer] = CycleMatch(matches, currentIndex, 1);
            }
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            GUILayout.Label("Selected layer material:", GUILayout.Width(145f));
            if (GUILayout.Button("Steel")) SetSelectedMaterial(EFT.InventoryLogic.EArmorMaterial.ArmoredSteel);
            if (GUILayout.Button("Ceramic")) SetSelectedMaterial(EFT.InventoryLogic.EArmorMaterial.Ceramic);
            if (GUILayout.Button("UHMWPE")) SetSelectedMaterial(EFT.InventoryLogic.EArmorMaterial.UHMWPE);
            if (GUILayout.Button("Titan")) SetSelectedMaterial(EFT.InventoryLogic.EArmorMaterial.Titan);
            GUILayout.EndHorizontal();
            GUILayout.BeginHorizontal();
            GUILayout.Space(145f);
            if (GUILayout.Button("Aluminium")) SetSelectedMaterial(EFT.InventoryLogic.EArmorMaterial.Aluminium);
            if (GUILayout.Button("Aramid")) SetSelectedMaterial(EFT.InventoryLogic.EArmorMaterial.Aramid);
            if (GUILayout.Button("Combined")) SetSelectedMaterial(EFT.InventoryLogic.EArmorMaterial.Combined);
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            GUILayout.Label("Distance " + Invariant(_distance, "F1") + " m", GUILayout.Width(135f));
            if (GUILayout.Button("-1m", GUILayout.Width(55f))) _distance = Mathf.Max(3f, _distance - 1f);
            if (GUILayout.Button("+1m", GUILayout.Width(55f))) _distance = Mathf.Min(50f, _distance + 1f);
            GUILayout.Label("Angle " + Invariant(_angle, "F0") + " deg", GUILayout.Width(120f));
            if (GUILayout.Button("-5", GUILayout.Width(45f))) _angle = Mathf.Max(-75f, _angle - 5f);
            if (GUILayout.Button("+5", GUILayout.Width(45f))) _angle = Mathf.Min(75f, _angle + 5f);
            GUILayout.EndHorizontal();
            GUILayout.Label("Thickness controls collider geometry; resistance and durability come from the selected EFT armor template.");

            GUILayout.BeginHorizontal();
            bool backstopEnabled = ActiveConfiguration.CatcherEnabled.Value;
            if (GUILayout.Button("Backstop: " + (backstopEnabled ? "ON" : "OFF")))
            {
                ActiveConfiguration.CatcherEnabled.Value = !backstopEnabled;
                _status = "Backstop " + (!backstopEnabled ? "enabled" : "disabled")
                    + "; rebuild the fixture to apply.";
            }
            bool traceEnabled = ActiveConfiguration.TraceEnabled.Value;
            if (GUILayout.Button("Last-shot trace: " + (traceEnabled ? "ON" : "OFF")))
            {
                ActiveConfiguration.TraceEnabled.Value = !traceEnabled;
                if (traceEnabled)
                {
                    HideTrace();
                }
            }
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            GUILayout.Label("Gap " + Invariant(_spacing, "F2") + " m", GUILayout.Width(135f));
            if (GUILayout.Button("-0.05", GUILayout.Width(60f))) _spacing = Mathf.Max(0.02f, _spacing - 0.05f);
            if (GUILayout.Button("+0.05", GUILayout.Width(60f))) _spacing = Mathf.Min(1f, _spacing + 0.05f);
            GUILayout.Label("Thickness " + Invariant(_thickness * 1000f, "F1") + " mm", GUILayout.Width(145f));
            if (GUILayout.Button("-1mm", GUILayout.Width(60f))) _thickness = Mathf.Max(0.003f, _thickness - 0.001f);
            if (GUILayout.Button("+1mm", GUILayout.Width(60f))) _thickness = Mathf.Min(0.1f, _thickness + 0.001f);
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Place / Rebuild Fixture", GUILayout.Height(32f)))
            {
                PlaceFixture();
            }
            if (GUILayout.Button("Reset Plate Durability", GUILayout.Height(32f)))
            {
                if (_rig == null)
                {
                    _status = "No fixture exists.";
                }
                else
                {
                    ResetFixtureDurability(false);
                }
            }
            if (GUILayout.Button("Reset Durability + Clear Records", GUILayout.Height(32f)))
            {
                if (_rig == null)
                {
                    _status = "No fixture exists.";
                }
                else
                {
                    ResetFixtureDurability(true);
                }
            }
            GUILayout.EndHorizontal();

        }

        private static void DrawBotControls(GUIStyle sectionStyle, GUIStyle buttonStyle)
        {
            GUILayout.Space(12f);
            GUILayout.Label("BOT TARGET", sectionStyle);
            GUILayout.Box(BotController.Describe(), GUILayout.ExpandWidth(true));
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("SELECT BOT\nUNDER CROSSHAIR", buttonStyle, GUILayout.Height(52f)))
            {
                _status = BotController.SelectUnderCrosshair(_world);
            }
            if (GUILayout.Button("HOLD BOT\nMOVEMENT + FIRE", buttonStyle, GUILayout.Height(52f)))
            {
                _status = BotController.Freeze();
            }
            if (GUILayout.Button("RELEASE\nBOT HOLD", buttonStyle, GUILayout.Height(52f)))
            {
                _status = BotController.ReleaseHold();
            }
            if (GUILayout.Button("CLEAR BOT\nSELECTION", buttonStyle, GUILayout.Height(52f)))
            {
                _status = BotController.ClearSelection();
            }
            GUILayout.EndHorizontal();
            GUILayout.Label("The lab never heals, respawns, re-equips, or writes a bot or player profile.");
        }

        private static void DrawLatestShot()
        {
            GUILayout.Space(12f);
            GUILayout.Label("LATEST SHOT", _sectionStyle);
            ShotRecord? record = _latestRecord ?? TelemetryStore.Latest;
            if (record == null)
            {
                GUILayout.Label("No shot recorded.");
                return;
            }

            string targetLayer = record.LayerIndex >= 0
                ? "L" + (record.LayerIndex + 1) + "/" + record.FixtureLayerCount
                : record.TargetKind;
            GUILayout.Box(
                "#" + record.Sequence + "  " + record.Outcome + "\n"
                + targetLayer + " | " + Invariant(record.ImpactSpeed, "F0") + " m/s"
                + " | penetration " + Invariant(record.DecisionPenetration, "F1"),
                _statusStyle,
                GUILayout.ExpandWidth(true));
            _showShotDetails = GUILayout.Toggle(
                _showShotDetails,
                "SHOW TECHNICAL SHOT DETAILS",
                _buttonStyle,
                GUILayout.Height(40f));
            if (!_showShotDetails)
            {
                return;
            }

            GUILayout.Label(record.Target + " | " + record.Material);
            GUILayout.Label(
                "Chain " + record.ChainId + " | fire " + record.FireIndex
                + " | fragment " + record.FragmentIndex + " | depth " + record.ParentDepth
                + " | " + (record.IsForwardHit ? "forward" : "back-face"));
            GUILayout.Label(
                "Impact " + Invariant(record.ImpactSpeed, "F1") + " m/s | base " + Invariant(record.TemplateSpeed, "F1")
                + " | ratio " + Invariant(record.Fraction, "F3") + " | angle " + Invariant(record.ImpactAngle, "F1") + " deg");
            GUILayout.Label(
                "Incoming D/P " + Invariant(record.IncomingDamage, "F2") + "/" + Invariant(record.IncomingPenetration, "F2")
                + " | decision D/P " + Invariant(record.DecisionDamage, "F2") + "/" + Invariant(record.DecisionPenetration, "F2"));
            if (record.LayerIndex >= 0)
            {
                GUILayout.Label(
                    "Layer " + (record.LayerIndex + 1) + " durability "
                    + Invariant(record.DurabilityBefore, "F2") + " -> " + Invariant(record.DurabilityAfter, "F2"));
                GUILayout.Label(
                    "Fixture #" + record.FixtureId + " | " + record.FixtureName
                    + " | C" + record.FixtureArmorClass + " " + record.FixtureArmorMaterial
                    + " | template " + record.FixtureTemplateId);
                if (record.HasArmorAnalysis)
                {
                    GUILayout.Label(
                        "Resistance real/class " + Invariant(record.ArmorRealResistance, "F2")
                        + "/" + Invariant(record.ArmorClassResistance, "F2")
                        + " | CF " + Invariant(record.ArmorCf, "F4")
                        + " | calculated penetration chance " + Invariant(record.PenetrationChancePercent, "F1") + "%");
                }
            }
            if (LabPolicies.ShouldDisplayBodyTelemetry(
                    record.BodyHealthBefore,
                    record.BodyHealthAfter,
                    record.ArmorChanges))
            {
                GUILayout.Label(
                    "Body health " + Invariant(record.BodyHealthBefore, "F2") + " -> " + Invariant(record.BodyHealthAfter, "F2")
                    + (string.IsNullOrEmpty(record.ArmorChanges) ? string.Empty : " | armor " + record.ArmorChanges));
            }
            GUILayout.Label(
                "BlockedBy " + (string.IsNullOrEmpty(record.BlockedBy) ? "none" : record.BlockedBy)
                + " | DeflectedBy " + (string.IsNullOrEmpty(record.DeflectedBy) ? "none" : record.DeflectedBy)
                + " | child shots " + record.FragmentCount);
            if (!string.IsNullOrEmpty(record.ContinuationKind))
            {
                GUILayout.Label(
                    "Incoming continuation from fixture #" + record.ContinuationSourceFixtureId
                    + " layer " + (record.ContinuationSourceLayerIndex + 1)
                    + " | penetration x" + Invariant(record.ContinuationPenetrationFactor, "F4")
                    + " | velocity x" + Invariant(record.ContinuationVelocityFactor, "F4")
                    + " | future outcomes x" + Invariant(record.ContinuationOutcomeFactor, "F4")
                    + " | armor CF x" + Invariant(record.ContinuationArmorCf, "F4"));
                GUILayout.Label(
                    "Continuation D/P " + Invariant(record.ContinuationDamageBefore, "F2")
                    + "/" + Invariant(record.ContinuationPenetrationBefore, "F2")
                    + " -> " + Invariant(record.ContinuationDamageAfter, "F2")
                    + "/" + Invariant(record.ContinuationPenetrationAfter, "F2"));
            }

            IReadOnlyList<ShotRecord> chain = TelemetryStore.SnapshotChain(record.ChainId);
            if (chain.Count > 1)
            {
                GUILayout.Label("Current chain:");
                foreach (ShotRecord link in chain)
                {
                    string layer = link.LayerIndex >= 0
                        ? "L" + (link.LayerIndex + 1)
                        : link.TargetKind;
                    GUILayout.Label(
                        "  #" + link.Sequence + " " + layer + " " + link.Outcome
                        + " | " + Invariant(link.ImpactSpeed, "F1") + "m/s"
                        + " | D/P " + Invariant(link.DecisionDamage, "F1")
                        + "/" + Invariant(link.DecisionPenetration, "F1"));
                }
            }
        }

        private static void DrawRecentShots()
        {
            GUILayout.Space(8f);
            GUILayout.Label("RECENT");
            IReadOnlyList<ShotRecord> snapshot = TelemetryStore.Snapshot();
            int start = Math.Max(0, snapshot.Count - 8);
            for (int index = snapshot.Count - 1; index >= start; index--)
            {
                ShotRecord record = snapshot[index];
                GUILayout.Label(
                    "#" + record.Sequence + " " + record.Outcome
                    + " | " + Invariant(record.ImpactSpeed, "F0") + "m/s"
                    + " | P " + Invariant(record.DecisionPenetration, "F1")
                    + " | " + record.Target);
            }
        }

        [SuppressMessage(
            "Design",
            "CA1031:Do not catch general exception types",
            Justification = "Session startup must roll back every partially constructed Unity fixture on failure.")]
        private static void StartSession()
        {
            if (_world == null || _world.MainPlayer == null)
            {
                _status = "Enter the hideout shooting range or an offline local raid first.";
                return;
            }

            try
            {
                _catalog ??= PlateCatalog.Build();
                int defaultIndex = ActiveCatalog.FindByTemplateId(PlateCatalog.GranitBr4TemplateId);
                if (defaultIndex < 0)
                {
                    defaultIndex = 0;
                }
                for (int index = 0; index < PresetIndices.Length; index++)
                {
                    PresetIndices[index] = defaultIndex;
                }
                TelemetryStore.Clear();
                _sessionActive = true;
                PhysicalTelemetrySessionBridge.Start();
                EnterHideoutShootingRangeIfNeeded();
                if (!PlaceFixture())
                {
                    throw new InvalidOperationException(_status);
                }
                _status = "Session active. Fixture items are transient and are not profile-owned.";
            }
            catch (Exception exception)
            {
                string failure = "Session start failed: " + exception.Message;
                EndSession(failure);
                _status = failure;
                Plugin.Log?.LogError(_status + " " + exception);
            }
        }

        private static void EndSession(string status)
        {
            PhysicalTelemetrySessionBridge.Stop();
            SaveAutomaticReport(true);
            BotController.ClearSelection();
            _rig?.Dispose();
            _rig = null;
            ExitHideoutShootingRangeIfOwned();
            HideTrace(true);
            _sessionActive = false;
            _latestRecord = null;
            _status = status;
        }

        [SuppressMessage(
            "Design",
            "CA1031:Do not catch general exception types",
            Justification = "Optional telemetry persistence must not interrupt gameplay.")]
        private static void SaveAutomaticReport(bool force)
        {
            if (Plugin.Configuration?.AutomaticReportSaving.Value != true)
            {
                return;
            }

            try
            {
                string? result = TelemetryStore.ExportAutomatic(force);
                if (!string.IsNullOrEmpty(result) && !force)
                {
                    _status = "Changed ballistic evidence saved. Keep shooting or choose the next fixture.";
                }
            }
            catch (Exception exception)
            {
                _status = "Automatic report failed: " + exception.Message;
                Plugin.Log?.LogWarning(_status);
            }
        }

        private static void EnterHideoutShootingRangeIfNeeded()
        {
            if (!(_world is HideoutGameWorld) || _world.MainPlayer == null)
            {
                _shootingModeStatus = "host-managed";
                return;
            }

            Type? ownerType = typeof(GameWorld).Assembly.GetType("EFT.HideoutPlayerOwner", throwOnError: false);
            if (ownerType == null)
            {
                _shootingModeStatus = "owner type unavailable";
                return;
            }

            _hideoutPlayerOwner = _world.MainPlayer.GetComponent(ownerType)
                ?? _world.MainPlayer.GetComponentInParent(ownerType)
                ?? UnityEngine.Object.FindObjectOfType(ownerType);
            if (_hideoutPlayerOwner == null)
            {
                _shootingModeStatus = "owner not found";
                return;
            }
            object owner = _hideoutPlayerOwner;
            if (ReadShootingRangeState(owner, ownerType))
            {
                _shootingModeStatus = "already active";
                return;
            }

            Type? representationType = typeof(GameWorld).Assembly.GetType(
                "EFT.Hideout.HideoutRepresentation",
                throwOnError: false);
            if (representationType == null)
            {
                _shootingModeStatus = "hideout representation type unavailable";
                return;
            }

            Type singletonType = typeof(Singleton<>).MakeGenericType(representationType);
            PropertyInfo? instantiatedProperty = singletonType.GetProperty(
                "Instantiated",
                BindingFlags.Static | BindingFlags.Public);
            bool instantiated = instantiatedProperty?.GetValue(null, null) is bool exists && exists;
            object? representation = instantiated
                ? singletonType.GetProperty("Instance", BindingFlags.Static | BindingFlags.Public)?.GetValue(null, null)
                : null;
            if (representation == null)
            {
                _shootingModeStatus = "hideout representation not ready";
                return;
            }

            MethodInfo? enter = representationType.GetMethod(
                "ActivateWeapon",
                BindingFlags.Instance | BindingFlags.Public,
                binder: null,
                types: new[] { ownerType },
                modifiers: null);
            if (enter == null)
            {
                throw new MissingMethodException(representationType.FullName, "ActivateWeapon(HideoutPlayerOwner)");
            }

            enter.Invoke(representation, new[] { owner });
            _enteredShootingRange = ReadShootingRangeState(owner, ownerType);
            RefreshHideoutShootingModeStatus();
        }

        [SuppressMessage(
            "Design",
            "CA1031:Do not catch general exception types",
            Justification = "Cleanup must not block world teardown when a reflected EFT method fails.")]
        private static void ExitHideoutShootingRangeIfOwned()
        {
            object? owner = _hideoutPlayerOwner;
            bool shouldExit = _enteredShootingRange
                && owner != null
                && ReadShootingRangeState(owner, owner.GetType());
            _hideoutPlayerOwner = null;
            _enteredShootingRange = false;
            _shootingModeStatus = "not active";
            if (!shouldExit || owner == null)
            {
                return;
            }

            Type ownerType = owner.GetType();

            try
            {
                MethodInfo? exit = ownerType.GetMethod(
                    "ExitShootingRange",
                    BindingFlags.Instance | BindingFlags.Public,
                    binder: null,
                    types: Type.EmptyTypes,
                    modifiers: null);
                Task? task = exit?.Invoke(owner, null) as Task;
                task?.ContinueWith(
                    failed => Plugin.Log?.LogError("Hideout shooting-range restore failed: " + failed.Exception),
                    CancellationToken.None,
                    TaskContinuationOptions.OnlyOnFaulted,
                    TaskScheduler.Default);
            }
            catch (Exception exception)
            {
                Plugin.Log?.LogError("Hideout shooting-range restore failed: " + exception);
            }
        }

        private static void RefreshHideoutShootingModeStatus()
        {
            if (!(_world is HideoutGameWorld))
            {
                _shootingModeStatus = "host-managed";
                return;
            }

            object? owner = _hideoutPlayerOwner;
            if (owner == null)
            {
                return;
            }

            Type ownerType = owner.GetType();

            bool active = ReadShootingRangeState(owner, ownerType);
            object? hideoutPlayer = ownerType.GetProperty(
                "HideoutPlayer",
                BindingFlags.Instance | BindingFlags.Public)?.GetValue(owner, null);
            bool inventoryUpdating = false;
            if (hideoutPlayer != null)
            {
                PropertyInfo? updatingProperty = hideoutPlayer.GetType().GetProperty(
                    "IsUpdateHideoutPlayerInventoryInProgress",
                    BindingFlags.Instance | BindingFlags.Public);
                inventoryUpdating = updatingProperty?.GetValue(hideoutPlayer, null) is bool updating && updating;
            }

            string hands = _world.MainPlayer?.HandsController?.GetType().Name ?? "none";
            bool firearmsBlocked = _world.MainPlayer?.MovementContext?.BlockFirearms ?? true;
            _shootingModeStatus = (active ? "active" : "inactive")
                + " | inventory " + (inventoryUpdating ? "updating" : "ready")
                + " | firearms " + (firearmsBlocked ? "blocked" : "ready")
                + " | hands " + hands;
        }

        private static bool ReadShootingRangeState(object owner, Type ownerType)
        {
            PropertyInfo property = ownerType.GetProperty(
                "InShootingRange",
                BindingFlags.Instance | BindingFlags.Public);
            return property != null && property.GetValue(owner, null) is bool value && value;
        }

        private static bool PlaceFixture()
        {
            if (!_sessionActive || _catalog == null)
            {
                _status = "Start a lab session first.";
                return false;
            }

            Camera camera = Camera.main;
            if (camera == null)
            {
                _status = "No active game camera was found.";
                return false;
            }

            Vector3 forward = Vector3.ProjectOnPlane(camera.transform.forward, Vector3.up).normalized;
            if (forward.sqrMagnitude < 0.001f)
            {
                forward = Vector3.forward;
            }
            Vector3 position = camera.transform.position + forward * _distance - Vector3.up * 0.1f;
            Quaternion rotation = Quaternion.LookRotation(forward, Vector3.up) * Quaternion.Euler(0f, _angle, 0f);
            List<PlateCatalogEntry> presets = new List<PlateCatalogEntry>(_layerCount);
            for (int index = 0; index < _layerCount; index++)
            {
                presets.Add(ActiveCatalog.Entries[ClampPresetIndex(PresetIndices[index])]);
            }

            FixtureRig replacement = FixtureRig.Create(
                presets,
                position,
                rotation,
                _spacing,
                _thickness,
                ActiveConfiguration.CatcherEnabled.Value);
            _rig?.Dispose();
            _rig = replacement;
            _status = "Placed " + _layerCount + " layer(s) at " + Invariant(_distance, "F1")
                + " m and " + Invariant(_angle, "F0") + " degrees as fixture #" + replacement.FixtureId + ".";
            return true;
        }

        private static void SetAllPresets(int presetIndex, int layers)
        {
            if (presetIndex < 0)
            {
                _status = "Requested preset was not present in this database.";
                return;
            }

            _layerCount = Math.Max(1, Math.Min(LabPolicies.MaximumLayers, layers));
            _selectedLayer = 0;
            for (int index = 0; index < PresetIndices.Length; index++)
            {
                PresetIndices[index] = presetIndex;
            }
        }

        private static void ApplyPresetAndPlace(int presetIndex, int layers, string label)
        {
            if (presetIndex < 0)
            {
                _status = "Requested preset was not present in this database.";
                return;
            }

            SetAllPresets(presetIndex, layers);
            if (PlaceFixture())
            {
                _status = "READY: " + label + " placed. Shoot the center; reports save automatically.";
                SetPanelVisible(false);
            }
        }

        private static void ResetFixtureDurability(bool clearRecords)
        {
            if (_rig == null)
            {
                _status = "No fixture exists.";
                return;
            }

            _rig.ResetDurability();
            if (!clearRecords)
            {
                _status = "Fixture durability restored to maximum.";
                return;
            }

            SaveAutomaticReport(true);
            TelemetryStore.Clear();
            PhysicalTelemetrySessionBridge.ClearCaptured();
            _latestRecord = null;
            HideTrace();
            _status = "Fixture durability restored and ballistic records cleared.";
        }

        private static void SetSelectedMaterial(EFT.InventoryLogic.EArmorMaterial material)
        {
            int currentIndex = ClampPresetIndex(PresetIndices[_selectedLayer]);
            int armorClass = ActiveCatalog.Entries[currentIndex].ArmorClass;
            int replacement = ActiveCatalog.FindMaterial(material, armorClass);
            if (replacement < 0)
            {
                _status = "No " + material + " armor template exists in the installed database.";
                return;
            }

            PresetIndices[_selectedLayer] = replacement;
            _status = "Layer " + (_selectedLayer + 1) + " now uses "
                + ActiveCatalog.Entries[replacement].DisplayName + ".";
        }

        private static int ClampPresetIndex(int index)
        {
            PlateCatalog? catalog = _catalog;
            if (catalog == null || catalog.Entries.Count == 0)
            {
                return 0;
            }
            return Math.Max(0, Math.Min(catalog.Entries.Count - 1, index));
        }

        private static int CycleMatch(List<int> matches, int current, int direction)
        {
            if (matches == null || matches.Count == 0)
            {
                _status = "No armor preset matches the filter.";
                return current;
            }

            int at = matches.IndexOf(current);
            if (at < 0)
            {
                return matches[0];
            }

            return matches[(at + direction + matches.Count) % matches.Count];
        }

        private static void SetPanelVisible(bool visible)
        {
            if (_panelVisible == visible)
            {
                return;
            }

            _panelVisible = visible;
            if (visible)
            {
                _previousCursorLock = Cursor.lockState;
                _previousCursorVisible = Cursor.visible;
                Cursor.lockState = CursorLockMode.None;
                Cursor.visible = true;
                _window.x = Mathf.Clamp(_window.x, 0f, Math.Max(0f, Screen.width - _window.width));
                _window.y = Mathf.Clamp(_window.y, 0f, Math.Max(0f, Screen.height - _window.height));
            }
            else
            {
                Cursor.lockState = _previousCursorLock;
                Cursor.visible = _previousCursorVisible;
            }
        }

        private static void KeepPanelCursorAvailable()
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }

        private static string DescribeWorld()
        {
            if (_world == null)
            {
                return "none";
            }
            return _world.GetType().Name + " | local player "
                + (_world.MainPlayer?.Profile?.Nickname ?? "not ready");
        }

        private static void UpdateTrace()
        {
            if (Plugin.Configuration?.TraceEnabled.Value != true
                || _latestRecord == null
                || Time.time > _traceUntil
                || _latestRecord.Path == null
                || _latestRecord.Path.Count < 2)
            {
                HideTrace();
                return;
            }

            EnsureTrace();
            if (_trace == null)
            {
                return;
            }

            Vector3[] points = _latestRecord.Path.ToArray();
            _trace.positionCount = points.Length;
            _trace.SetPositions(points);
            _trace.enabled = true;
        }

        private static void EnsureTrace()
        {
            if (_trace != null)
            {
                return;
            }

            Shader shader = Shader.Find("Sprites/Default") ?? Shader.Find("Unlit/Color");
            if (shader == null)
            {
                return;
            }

            GameObject host = new GameObject("BallisticsLab_LastShotTrace")
            {
                hideFlags = HideFlags.HideAndDontSave
            };
            _traceMaterial = new Material(shader)
            {
                hideFlags = HideFlags.HideAndDontSave,
                color = new Color(0.15f, 0.95f, 0.90f, 1f)
            };
            _trace = host.AddComponent<LineRenderer>();
            _trace.sharedMaterial = _traceMaterial;
            _trace.useWorldSpace = true;
            _trace.startWidth = 0.012f;
            _trace.endWidth = 0.006f;
            _trace.startColor = _traceMaterial.color;
            _trace.endColor = _traceMaterial.color;
            _trace.enabled = false;
        }

        private static void HideTrace(bool destroy = false)
        {
            if (_trace != null)
            {
                _trace.enabled = false;
                if (destroy)
                {
                    UnityEngine.Object.Destroy(_trace.gameObject);
                    _trace = null;
                }
            }

            if (destroy && _traceMaterial != null)
            {
                UnityEngine.Object.Destroy(_traceMaterial);
                _traceMaterial = null;
            }
        }

        private static string Invariant(float value, string format)
        {
            return value.ToString(format, CultureInfo.InvariantCulture);
        }

        private static string Invariant(int value)
        {
            return value.ToString(CultureInfo.InvariantCulture);
        }
    }
}

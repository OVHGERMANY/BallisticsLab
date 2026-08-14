using System;
using System.Collections.Generic;
using System.Threading;
using Comfort.Common;
using EFT;
using EFT.Ballistics;
using EFT.InventoryLogic;
using BallisticsLab.Core;
using UnityEngine;
using UnityEngine.Rendering;

namespace BallisticsLab.Runtime.Fixtures
{
    internal sealed class FixtureRig : IDisposable
    {
        private const float BackstopFaceClearance = 1f;
        private const float BackstopThickness = 0.08f;
        internal const float PlateFaceWidthMetres = 1f;
        internal const float PlateFaceHeightMetres = 1.5f;

        private static long _nextFixtureId;

        private readonly GameObject _root;
        private readonly List<LabPlateRuntime> _plates = new List<LabPlateRuntime>();
        private readonly List<Material> _materials = new List<Material>();
        private LineRenderer? _aimHorizontal;
        private LineRenderer? _aimVertical;
        private float _aimMarkerX;
        private float _aimMarkerY;
        private float _aimMarkerZ;

        private FixtureRig(GameObject root, long fixtureId)
        {
            _root = root;
            FixtureId = fixtureId;
        }

        internal long FixtureId { get; }
        internal IReadOnlyList<LabPlateRuntime> Plates => _plates;
        internal Vector3 Position => _root != null ? _root.transform.position : Vector3.zero;

        internal static FixtureRig Create(
            IReadOnlyList<PlateCatalogEntry> presets,
            Vector3 position,
            Quaternion rotation,
            float spacing,
            float thickness,
            bool addBackstop)
        {
            if (presets == null || presets.Count == 0)
            {
                throw new ArgumentException("At least one plate preset is required.", nameof(presets));
            }

            GameObject root = new GameObject("BallisticsLab_FixtureRig")
            {
                hideFlags = HideFlags.HideAndDontSave
            };
            root.transform.SetPositionAndRotation(position, rotation);

            FixtureRig rig = new FixtureRig(root, Interlocked.Increment(ref _nextFixtureId));
            try
            {
                rig.Build(presets, spacing, thickness, addBackstop);
                return rig;
            }
            catch
            {
                rig.Dispose();
                throw;
            }
        }

        private void Build(
            IReadOnlyList<PlateCatalogEntry> presets,
            float spacing,
            float thickness,
            bool addBackstop)
        {
            ItemFactory factory = Singleton<ItemFactory>.Instance;
            List<Item> items = new List<Item>(presets.Count);
            List<ArmorComponent> armors = new List<ArmorComponent>(presets.Count);

            for (int index = 0; index < presets.Count; index++)
            {
                PlateCatalogEntry preset = presets[index];
                Item item = factory.CreateItem(factory.NextId, preset.TemplateId, null);
                ArmorComponent armor = item.GetItemComponent<ArmorComponent>();
                if (armor == null)
                {
                    throw new InvalidOperationException("ArmorComponent was missing from template " + preset.TemplateId + ".");
                }

                items.Add(item);
                armors.Add(armor);
            }

            int ballisticLayer = LayerMask.NameToLayer("HighPolyCollider");
            if (ballisticLayer < 0)
            {
                throw new InvalidOperationException("Unity layer HighPolyCollider was not found.");
            }

            for (int index = 0; index < presets.Count; index++)
            {
                PlateCatalogEntry preset = presets[index];
                LabPlateRuntime runtime = new LabPlateRuntime(
                    FixtureId,
                    index,
                    presets.Count,
                    preset,
                    items[index],
                    armors[index],
                    preset.PlateCollider,
                    armors,
                    spacing,
                    thickness);
                _plates.Add(runtime);

                GameObject plate = GameObject.CreatePrimitive(PrimitiveType.Cube);
                plate.name = "BallisticsLab_Plate_" + (index + 1) + "_" + preset.TemplateId;
                plate.hideFlags = HideFlags.HideAndDontSave;
                plate.layer = ballisticLayer;
                plate.transform.SetParent(_root.transform, false);
                plate.transform.localPosition = new Vector3(
                    0f,
                    0f,
                    LabPolicies.LayerCenterOffset(index, spacing, thickness));
                plate.transform.localRotation = Quaternion.identity;
                plate.transform.localScale = new Vector3(
                    PlateFaceWidthMetres,
                    PlateFaceHeightMetres,
                    thickness);

                Material material = CreateMaterial(MaterialColor(preset.Material));
                _materials.Add(material);
                MeshRenderer renderer = plate.GetComponent<MeshRenderer>();
                renderer.sharedMaterial = material;

                LabPlateCollider ballistic = plate.AddComponent<LabPlateCollider>();
                ballistic.Configure(runtime);
            }

            CreateAimMarker(thickness);

            if (addBackstop)
            {
                GameObject backstop = GameObject.CreatePrimitive(PrimitiveType.Cube);
                backstop.name = "BallisticsLab_Backstop";
                backstop.hideFlags = HideFlags.HideAndDontSave;
                backstop.layer = ballisticLayer;
                backstop.transform.SetParent(_root.transform, false);
                backstop.transform.localPosition = new Vector3(
                    0f,
                    0f,
                    LabPolicies.BackstopCenterOffset(
                        presets.Count,
                        spacing,
                        thickness,
                        BackstopFaceClearance,
                        BackstopThickness));
                backstop.transform.localScale = new Vector3(2f, 2.2f, BackstopThickness);

                Material material = CreateMaterial(new Color(0.16f, 0.16f, 0.18f, 1f));
                _materials.Add(material);
                backstop.GetComponent<MeshRenderer>().sharedMaterial = material;

                LabBackstopCollider catcher = backstop.AddComponent<LabBackstopCollider>();
                catcher.Configure(FixtureId, presets.Count);
            }
        }

        internal void ResetDurability()
        {
            foreach (LabPlateRuntime plate in _plates)
            {
                plate.ResetDurability();
            }
        }

        internal void SetAimPoint(ProtocolImpactPoint point, double projectileDiameterMetres)
        {
            if (_aimHorizontal == null || _aimVertical == null)
            {
                return;
            }
            float diameter = (float)projectileDiameterMetres;
            float halfLength = (float)point.TargetToleranceMetres * 0.9f;
            float width = Mathf.Clamp(diameter * 0.4f, 0.003f, 0.008f);
            float x = (float)point.LocalXMetres;
            float y = (float)point.LocalYMetres;
            _aimMarkerX = x;
            _aimMarkerY = y;
            ConfigureAimLine(
                _aimHorizontal,
                new Vector3(x - halfLength, y, _aimMarkerZ),
                new Vector3(x + halfLength, y, _aimMarkerZ),
                width);
            ConfigureAimLine(
                _aimVertical,
                new Vector3(x, y - halfLength, _aimMarkerZ),
                new Vector3(x, y + halfLength, _aimMarkerZ),
                width);
        }

        internal void UpdateAimMarkerVisibility(Camera? camera)
        {
            if (_aimHorizontal == null || _aimVertical == null)
            {
                return;
            }

            bool visible = false;
            if (camera != null && _root != null)
            {
                Vector3 markerPosition = _root.transform.TransformPoint(
                    new Vector3(_aimMarkerX, _aimMarkerY, _aimMarkerZ));
                Vector3 cameraOffset = camera.transform.position - markerPosition;
                Vector3 viewport = camera.WorldToViewportPoint(markerPosition);
                visible = LabPresentationPolicies.ShouldShowAimMarker(
                    cameraOffset.magnitude,
                    Vector3.Dot(_root.transform.forward, cameraOffset),
                    viewport.z);
            }

            _aimHorizontal.enabled = visible;
            _aimVertical.enabled = visible;
        }

        public void Dispose()
        {
            if (_root != null)
            {
                UnityEngine.Object.Destroy(_root);
            }

            foreach (Material material in _materials)
            {
                if (material != null)
                {
                    UnityEngine.Object.Destroy(material);
                }
            }

            _materials.Clear();
            _plates.Clear();
        }

        private static Material CreateMaterial(Color color)
        {
            Shader shader = Shader.Find("Standard")
                ?? Shader.Find("Legacy Shaders/Diffuse")
                ?? Shader.Find("Unlit/Color");
            if (shader == null)
            {
                throw new InvalidOperationException("No fixture shader was available.");
            }

            Material material = new Material(shader)
            {
                hideFlags = HideFlags.HideAndDontSave,
                color = color
            };
            return material;
        }

        private static Color MaterialColor(EArmorMaterial material)
        {
            switch (material)
            {
                case EArmorMaterial.ArmoredSteel: return new Color(0.30f, 0.34f, 0.39f, 1f);
                case EArmorMaterial.Ceramic: return new Color(0.82f, 0.76f, 0.62f, 1f);
                case EArmorMaterial.UHMWPE: return new Color(0.35f, 0.72f, 0.78f, 1f);
                case EArmorMaterial.Titan: return new Color(0.48f, 0.46f, 0.61f, 1f);
                case EArmorMaterial.Aluminium: return new Color(0.63f, 0.67f, 0.71f, 1f);
                case EArmorMaterial.Aramid: return new Color(0.72f, 0.59f, 0.27f, 1f);
                case EArmorMaterial.Glass: return new Color(0.35f, 0.72f, 0.88f, 1f);
                default: return new Color(0.50f, 0.50f, 0.50f, 1f);
            }
        }

        private void CreateAimMarker(float thickness)
        {
            Shader shader = Shader.Find("Sprites/Default") ?? Shader.Find("Unlit/Color");
            if (shader == null)
            {
                return;
            }

            Material material = new Material(shader)
            {
                hideFlags = HideFlags.HideAndDontSave,
                color = new Color(1f, 0.24f, 0.12f, 1f)
            };
            _materials.Add(material);
            _aimMarkerZ = -thickness * 0.51f - 0.002f;
            _aimHorizontal = CreateAimLine(
                "BallisticsLab_AimHorizontal",
                material,
                new Vector3(-0.12f, 0f, _aimMarkerZ),
                new Vector3(0.12f, 0f, _aimMarkerZ));
            _aimVertical = CreateAimLine(
                "BallisticsLab_AimVertical",
                material,
                new Vector3(0f, -0.12f, _aimMarkerZ),
                new Vector3(0f, 0.12f, _aimMarkerZ));
        }

        private static void ConfigureAimLine(
            LineRenderer line,
            Vector3 start,
            Vector3 end,
            float width)
        {
            line.SetPosition(0, start);
            line.SetPosition(1, end);
            line.startWidth = width;
            line.endWidth = width;
        }

        private LineRenderer CreateAimLine(
            string name,
            Material material,
            Vector3 start,
            Vector3 end)
        {
            GameObject lineObject = new GameObject(name)
            {
                hideFlags = HideFlags.HideAndDontSave
            };
            lineObject.transform.SetParent(_root.transform, false);
            LineRenderer line = lineObject.AddComponent<LineRenderer>();
            line.sharedMaterial = material;
            line.useWorldSpace = false;
            line.alignment = LineAlignment.TransformZ;
            line.shadowCastingMode = ShadowCastingMode.Off;
            line.receiveShadows = false;
            line.positionCount = 2;
            line.SetPosition(0, start);
            line.SetPosition(1, end);
            line.startWidth = 0.012f;
            line.endWidth = 0.012f;
            line.startColor = material.color;
            line.endColor = material.color;
            line.enabled = false;
            return line;
        }
    }
}

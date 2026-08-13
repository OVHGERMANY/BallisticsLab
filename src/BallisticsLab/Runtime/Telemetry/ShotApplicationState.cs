using System.Collections.Generic;
using BallisticsLab.Core;
using EFT;
using EFT.Ballistics;
using EFT.InventoryLogic;
using BallisticsLab.Runtime.Fixtures;
using UnityEngine;

namespace BallisticsLab.Runtime.Telemetry
{
    internal sealed class ArmorTelemetrySnapshot
    {
        internal ArmorTelemetrySnapshot(
            string itemId,
            string templateId,
            string name,
            int armorClass,
            string material,
            float durability,
            float maximumDurability)
        {
            ItemId = itemId;
            TemplateId = templateId;
            Name = name;
            ArmorClass = armorClass;
            Material = material;
            Durability = durability;
            MaximumDurability = maximumDurability;
        }

        internal string ItemId { get; }
        internal string TemplateId { get; }
        internal string Name { get; }
        internal int ArmorClass { get; }
        internal string Material { get; }
        internal float Durability { get; }
        internal float MaximumDurability { get; }
    }

    internal sealed class ShotApplicationState
    {
        internal ShotApplicationState(Shot shot, CollisionSnapshot? collision)
        {
            Shot = shot;
            Collision = collision;
            Continuation = ContinuationAdjustmentStore.Take(shot);
            DecisionDamage = shot.Damage;
            DecisionPenetration = shot.PenetrationPower;
            ImpactSpeed = shot.CurrentVelocity.magnitude;
            IsForwardHit = shot.IsForwardHit;
            FireIndex = shot.FireIndex;
            FragmentIndex = shot.FragmentIndex;
            Shot root = FindRoot(shot, out int parentDepth);
            ParentDepth = parentDepth;
            RootFireIndex = root.FireIndex;
            RootRandomSeed = root.RandomSeed;
            RootShooterProfileId = root.Player?.iPlayer?.ProfileId ?? root.PlayerProfileID ?? string.Empty;
            ChainId = LabPolicies.ShotChainId(RootShooterProfileId, RootFireIndex, RootRandomSeed);
            TargetKind = "WORLD";
            TargetName = shot.HittedBallisticCollider != null
                ? shot.HittedBallisticCollider.name
                : "<none>";
            Material = shot.HittedBallisticCollider != null
                ? shot.HittedBallisticCollider.TypeOfMaterial.ToString()
                : "None";

            if (shot.HittedBallisticCollider is LabPlateCollider labPlate && labPlate.Runtime != null)
            {
                LabPlate = labPlate.Runtime;
                DurabilityBefore = LabPlate.Durability;
                LayerIndex = LabPlate.LayerIndex;
                FixtureId = LabPlate.FixtureId;
                FixtureLayerCount = LabPlate.LayerCount;
                FixtureTemplateId = LabPlate.Preset.TemplateId;
                FixtureName = LabPlate.Preset.Name;
                FixtureArmorClass = LabPlate.Preset.ArmorClass;
                FixtureArmorMaterial = LabPlate.Preset.Material.ToString();
                FixtureMaximumDurability = LabPlate.MaximumDurability;
                FixtureLayerSpacing = LabPlate.LayerSpacing;
                FixtureColliderThickness = LabPlate.ColliderThickness;
                CaptureFixtureFacePoint(labPlate, shot.HitPoint);
                TargetKind = "FIXTURE PLATE";
                TargetName = "Fixture " + FixtureId + " layer " + (LayerIndex + 1)
                    + "/" + FixtureLayerCount + " | " + FixtureName;
                Material = FixtureArmorMaterial;
                if (LabPlate.TryGetAnalysis(
                        DecisionPenetration,
                        out ArmorResistanceData resistance,
                        out float penetrationChancePercent))
                {
                    ArmorRealResistance = resistance.RealResistance;
                    ArmorClassResistance = resistance.ArmorClassResistance;
                    ArmorCf = resistance.CF;
                    PenetrationChancePercent = penetrationChancePercent;
                    HasArmorAnalysis = true;
                }
            }
            else if (shot.HittedBallisticCollider is LabBackstopCollider backstop)
            {
                FixtureId = backstop.FixtureId;
                FixtureLayerCount = backstop.LayerCount;
                TargetKind = "FIXTURE BACKSTOP";
                TargetName = "Fixture " + FixtureId + " backstop after " + FixtureLayerCount + " layer(s)";
            }

            if (shot.HittedBallisticCollider is BodyPartCollider bodyPart && bodyPart.Player is Player player)
            {
                TargetPlayer = player;
                BodyPart = bodyPart.BodyPartType;
                HealthBefore = player.HealthController.GetBodyPartHealth(BodyPart).Current;
                ArmorBefore = CaptureArmors(player);
                TargetAliveBefore = player.HealthController.IsAlive;
                TargetKind = player.IsAI ? "BOT" : "PLAYER";
                TargetName = player.Profile?.Nickname ?? player.ProfileId;
            }
        }

        internal Shot Shot { get; }
        internal CollisionSnapshot? Collision { get; }
        internal ContinuationAdjustment? Continuation { get; }
        internal string ChainId { get; }
        internal int FireIndex { get; }
        internal int FragmentIndex { get; }
        internal int ParentDepth { get; }
        internal int RootFireIndex { get; }
        internal int RootRandomSeed { get; }
        internal string RootShooterProfileId { get; }
        internal bool IsForwardHit { get; }
        internal float DecisionDamage { get; }
        internal float DecisionPenetration { get; }
        internal float ImpactSpeed { get; }
        internal string TargetName { get; }
        internal string TargetKind { get; }
        internal string Material { get; }
        internal LabPlateRuntime? LabPlate { get; }
        internal long FixtureId { get; }
        internal int LayerIndex { get; } = -1;
        internal int FixtureLayerCount { get; }
        internal string FixtureTemplateId { get; } = string.Empty;
        internal string FixtureName { get; } = string.Empty;
        internal int FixtureArmorClass { get; }
        internal string FixtureArmorMaterial { get; } = string.Empty;
        internal float FixtureMaximumDurability { get; }
        internal float FixtureLayerSpacing { get; }
        internal float FixtureColliderThickness { get; }
        internal bool HasFixtureFacePoint { get; private set; }
        internal float FixtureLocalHitX { get; private set; }
        internal float FixtureLocalHitY { get; private set; }
        internal float FixtureFaceWidth { get; private set; }
        internal float FixtureFaceHeight { get; private set; }
        internal bool HasArmorAnalysis { get; }
        internal float ArmorRealResistance { get; }
        internal float ArmorClassResistance { get; }
        internal float ArmorCf { get; }
        internal float PenetrationChancePercent { get; }
        internal float DurabilityBefore { get; }
        internal Player? TargetPlayer { get; }
        internal EBodyPart BodyPart { get; }
        internal float HealthBefore { get; }
        internal bool TargetAliveBefore { get; }
        internal Dictionary<string, ArmorTelemetrySnapshot> ArmorBefore { get; } =
            new Dictionary<string, ArmorTelemetrySnapshot>();

        internal static Dictionary<string, ArmorTelemetrySnapshot> CaptureArmors(Player player)
        {
            Dictionary<string, ArmorTelemetrySnapshot> values =
                new Dictionary<string, ArmorTelemetrySnapshot>();
            if (player?.Inventory == null)
            {
                return values;
            }

            foreach (ArmorComponent armor in player.Inventory.GetPutOnArmors())
            {
                if (armor?.Item == null || armor.Repairable == null)
                {
                    continue;
                }

                string itemId = armor.Item.Id.ToString();
                string name = string.IsNullOrWhiteSpace(armor.Item.ShortName)
                    ? armor.Item.TemplateId
                    : armor.Item.ShortName;
                values[itemId] = new ArmorTelemetrySnapshot(
                    itemId,
                    armor.Item.TemplateId,
                    name,
                    armor.ArmorClass,
                    armor.Template?.ArmorMaterial.ToString() ?? "Unknown",
                    armor.Repairable.Durability,
                    armor.Repairable.MaxDurability);
            }

            return values;
        }

        private static Shot FindRoot(Shot shot, out int parentDepth)
        {
            parentDepth = 0;
            Shot root = shot;
            while (root.Parent != null && parentDepth < 64)
            {
                root = root.Parent;
                parentDepth++;
            }

            return root;
        }

        private void CaptureFixtureFacePoint(LabPlateCollider plate, Vector3 hitPoint)
        {
            if (plate == null || !IsFinite(hitPoint))
            {
                return;
            }

            Vector3 local = plate.transform.InverseTransformPoint(hitPoint);
            Vector3 scale = plate.transform.lossyScale;
            float width = Mathf.Abs(scale.x);
            float height = Mathf.Abs(scale.y);
            float localX = local.x * width;
            float localY = local.y * height;
            if (!IsFinite(localX)
                || !IsFinite(localY)
                || !IsFinite(width)
                || !IsFinite(height)
                || width <= 0f
                || height <= 0f)
            {
                return;
            }

            HasFixtureFacePoint = true;
            FixtureLocalHitX = localX;
            FixtureLocalHitY = localY;
            FixtureFaceWidth = width;
            FixtureFaceHeight = height;
        }

        private static bool IsFinite(Vector3 value)
        {
            return IsFinite(value.x) && IsFinite(value.y) && IsFinite(value.z);
        }

        private static bool IsFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }
    }
}

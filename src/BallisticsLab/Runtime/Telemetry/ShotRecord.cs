using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using BallisticsLab.Core;
using EFT.Ballistics;
using EFT.InventoryLogic;
using UnityEngine;

namespace BallisticsLab.Runtime.Telemetry
{
    internal sealed class ShotRecord
    {
        internal static ShotRecord Complete(long sequence, ShotApplicationState state)
        {
            Shot shot = state.Shot;
            AmmoTemplate? ammoTemplate = shot.Ammo?.Template as AmmoTemplate;
            float templateSpeed = ammoTemplate?.InitialSpeed ?? 0f;
            float durabilityAfter = state.LabPlate?.Durability ?? 0f;
            float healthAfter = state.TargetPlayer != null
                ? state.TargetPlayer.HealthController.GetBodyPartHealth(state.BodyPart).Current
                : 0f;
            Dictionary<string, ArmorTelemetrySnapshot> armorAfter = state.TargetPlayer != null
                ? ShotApplicationState.CaptureArmors(state.TargetPlayer)
                : new Dictionary<string, ArmorTelemetrySnapshot>();

            List<string> armorChanges = new List<string>();
            if (state.ArmorBefore != null)
            {
                foreach (KeyValuePair<string, ArmorTelemetrySnapshot> before in state.ArmorBefore)
                {
                    if (armorAfter.TryGetValue(before.Key, out ArmorTelemetrySnapshot after)
                        && Math.Abs(after.Durability - before.Value.Durability) > 0.0001f)
                    {
                        armorChanges.Add(
                            before.Value.Name + " [C" + before.Value.ArmorClass + " " + before.Value.Material + "] "
                            + before.Key + ":" + before.Value.Durability.ToString("F2", CultureInfo.InvariantCulture)
                            + "->" + after.Durability.ToString("F2", CultureInfo.InvariantCulture));
                    }
                }
            }

            List<Vector3> path = new List<Vector3>();
            if (shot.PositionHistory != null)
            {
                int start = Math.Max(0, shot.PositionHistory.Count - 64);
                for (int index = start; index < shot.PositionHistory.Count; index++)
                {
                    Vector3 point = shot.PositionHistory[index];
                    if (IsFinite(point))
                    {
                        path.Add(point);
                    }
                }
            }

            if (path.Count == 0 && state.Collision != null)
            {
                path.Add(state.Collision.SegmentStart);
            }
            if (IsFinite(shot.HitPoint) && (path.Count == 0 || path[path.Count - 1] != shot.HitPoint))
            {
                path.Add(shot.HitPoint);
            }

            float directionDotNormal = Vector3.Dot(
                -shot.CurrentDirection.normalized,
                shot.HitNormal.normalized);
            float angle = LabPolicies.ImpactAngleDegrees(directionDotNormal);
            return new ShotRecord
            {
                Sequence = sequence,
                Utc = DateTime.UtcNow,
                ChainId = state.ChainId,
                FireIndex = state.FireIndex,
                FragmentIndex = state.FragmentIndex,
                ParentDepth = state.ParentDepth,
                RootFireIndex = state.RootFireIndex,
                RootRandomSeed = state.RootRandomSeed,
                RootShooterProfileId = state.RootShooterProfileId,
                IsForwardHit = state.IsForwardHit,
                AmmoTemplateId = LabPolicies.AuthoritativeAmmoValue(
                    ammoTemplate?.StringId,
                    shot.Ammo?.TemplateId),
                AmmoName = LabPolicies.AuthoritativeAmmoValue(
                    ammoTemplate?._name,
                    shot.Ammo?.ShortName),
                ShooterProfileId = shot.Player?.iPlayer?.ProfileId ?? shot.PlayerProfileID ?? string.Empty,
                Target = state.TargetName,
                TargetKind = state.TargetKind,
                Material = state.Material,
                FixtureId = state.FixtureId,
                LayerIndex = state.LayerIndex,
                FixtureLayerCount = state.FixtureLayerCount,
                FixtureTemplateId = state.FixtureTemplateId ?? string.Empty,
                FixtureName = state.FixtureName ?? string.Empty,
                FixtureArmorClass = state.FixtureArmorClass,
                FixtureArmorMaterial = state.FixtureArmorMaterial ?? string.Empty,
                FixtureMaximumDurability = state.FixtureMaximumDurability,
                FixtureLayerSpacing = state.FixtureLayerSpacing,
                FixtureColliderThickness = state.FixtureColliderThickness,
                HasArmorAnalysis = state.HasArmorAnalysis,
                ArmorRealResistance = state.ArmorRealResistance,
                ArmorClassResistance = state.ArmorClassResistance,
                ArmorCf = state.ArmorCf,
                PenetrationChancePercent = state.PenetrationChancePercent,
                BulletState = (int)shot.BulletState,
                Outcome = LabPolicies.OutcomeName((int)shot.BulletState, shot.BlockedBy.HasValue, shot.DeflectedBy.HasValue),
                ImpactAngle = angle,
                ImpactSpeed = state.ImpactSpeed,
                HasThreeMetreVelocity = state.HasThreeMetreVelocity,
                ThreeMetreVelocity = state.ThreeMetreVelocity,
                TemplateSpeed = templateSpeed,
                Fraction = templateSpeed > 0f ? state.ImpactSpeed / templateSpeed : 0f,
                ProjectileMassKilograms = (ammoTemplate?.BulletMassGram ?? 0f) * 0.001f,
                ProjectileDiameterMetres = (ammoTemplate?.BulletDiameterMilimeters ?? 0f) * 0.001f,
                HasFixtureFacePoint = state.HasFixtureFacePoint,
                FixtureLocalHitX = state.FixtureLocalHitX,
                FixtureLocalHitY = state.FixtureLocalHitY,
                FixtureFaceWidth = state.FixtureFaceWidth,
                FixtureFaceHeight = state.FixtureFaceHeight,
                ImpactDistanceMetres = IsFinite(shot.StartPosition) && IsFinite(shot.HitPoint)
                    ? Vector3.Distance(shot.StartPosition, shot.HitPoint)
                    : 0f,
                IncomingDamage = state.Collision?.Damage ?? state.DecisionDamage,
                IncomingPenetration = state.Collision?.PenetrationPower ?? state.DecisionPenetration,
                DecisionDamage = state.DecisionDamage,
                DecisionPenetration = state.DecisionPenetration,
                BlockedBy = shot.BlockedBy?.ToString() ?? string.Empty,
                DeflectedBy = shot.DeflectedBy?.ToString() ?? string.Empty,
                FragmentCount = shot.Fragments?.Count ?? 0,
                DurabilityBefore = state.DurabilityBefore,
                DurabilityAfter = durabilityAfter,
                BodyHealthBefore = state.HealthBefore,
                BodyHealthAfter = healthAfter,
                TargetAliveBefore = state.TargetAliveBefore,
                TargetAliveAfter = state.TargetPlayer?.HealthController?.IsAlive ?? false,
                ArmorChanges = string.Join(";", armorChanges),
                ContinuationKind = state.Continuation?.Kind ?? string.Empty,
                ContinuationSourceFixtureId = state.Continuation?.FixtureId ?? 0L,
                ContinuationSourceLayerIndex = state.Continuation?.SourceLayerIndex ?? -1,
                ContinuationPenetrationFactor = state.Continuation?.PenetrationFactor ?? 1f,
                ContinuationVelocityFactor = state.Continuation?.VelocityFactor ?? 1f,
                ContinuationOutcomeFactor = state.Continuation?.OutcomeFactor ?? 1f,
                ContinuationArmorCf = state.Continuation?.ArmorCf ?? 1f,
                ContinuationDamageBefore = state.Continuation?.DamageBefore ?? 0f,
                ContinuationPenetrationBefore = state.Continuation?.PenetrationBefore ?? 0f,
                ContinuationDamageAfter = state.Continuation?.DamageAfter ?? 0f,
                ContinuationPenetrationAfter = state.Continuation?.PenetrationAfter ?? 0f,
                HitPoint = shot.HitPoint,
                Path = path
            };
        }

        internal long Sequence { get; private set; }
        internal DateTime Utc { get; private set; }
        internal string ChainId { get; private set; } = string.Empty;
        internal int FireIndex { get; private set; }
        internal int FragmentIndex { get; private set; }
        internal int ParentDepth { get; private set; }
        internal int RootFireIndex { get; private set; }
        internal int RootRandomSeed { get; private set; }
        internal string RootShooterProfileId { get; private set; } = string.Empty;
        internal bool IsForwardHit { get; private set; }
        internal string AmmoTemplateId { get; private set; } = string.Empty;
        internal string AmmoName { get; private set; } = string.Empty;
        internal string ShooterProfileId { get; private set; } = string.Empty;
        internal string Target { get; private set; } = string.Empty;
        internal string TargetKind { get; private set; } = string.Empty;
        internal string Material { get; private set; } = string.Empty;
        internal long FixtureId { get; private set; }
        internal int LayerIndex { get; private set; }
        internal int FixtureLayerCount { get; private set; }
        internal string FixtureTemplateId { get; private set; } = string.Empty;
        internal string FixtureName { get; private set; } = string.Empty;
        internal int FixtureArmorClass { get; private set; }
        internal string FixtureArmorMaterial { get; private set; } = string.Empty;
        internal float FixtureMaximumDurability { get; private set; }
        internal float FixtureLayerSpacing { get; private set; }
        internal float FixtureColliderThickness { get; private set; }
        internal bool HasArmorAnalysis { get; private set; }
        internal float ArmorRealResistance { get; private set; }
        internal float ArmorClassResistance { get; private set; }
        internal float ArmorCf { get; private set; }
        internal float PenetrationChancePercent { get; private set; }
        internal int BulletState { get; private set; }
        internal string Outcome { get; private set; } = string.Empty;
        internal float ImpactAngle { get; private set; }
        internal float ImpactSpeed { get; private set; }
        internal bool HasThreeMetreVelocity { get; private set; }
        internal float ThreeMetreVelocity { get; private set; }
        internal float TemplateSpeed { get; private set; }
        internal float Fraction { get; private set; }
        internal float ProjectileMassKilograms { get; private set; }
        internal float ProjectileDiameterMetres { get; private set; }
        internal bool HasFixtureFacePoint { get; private set; }
        internal float FixtureLocalHitX { get; private set; }
        internal float FixtureLocalHitY { get; private set; }
        internal float FixtureFaceWidth { get; private set; }
        internal float FixtureFaceHeight { get; private set; }
        internal float ImpactDistanceMetres { get; private set; }
        internal float IncomingDamage { get; private set; }
        internal float IncomingPenetration { get; private set; }
        internal float DecisionDamage { get; private set; }
        internal float DecisionPenetration { get; private set; }
        internal string BlockedBy { get; private set; } = string.Empty;
        internal string DeflectedBy { get; private set; } = string.Empty;
        internal int FragmentCount { get; private set; }
        internal float DurabilityBefore { get; private set; }
        internal float DurabilityAfter { get; private set; }
        internal float BodyHealthBefore { get; private set; }
        internal float BodyHealthAfter { get; private set; }
        internal bool TargetAliveBefore { get; private set; }
        internal bool TargetAliveAfter { get; private set; }
        internal string ArmorChanges { get; private set; } = string.Empty;
        internal string ContinuationKind { get; private set; } = string.Empty;
        internal long ContinuationSourceFixtureId { get; private set; }
        internal int ContinuationSourceLayerIndex { get; private set; }
        internal float ContinuationPenetrationFactor { get; private set; }
        internal float ContinuationVelocityFactor { get; private set; }
        internal float ContinuationOutcomeFactor { get; private set; }
        internal float ContinuationArmorCf { get; private set; }
        internal float ContinuationDamageBefore { get; private set; }
        internal float ContinuationPenetrationBefore { get; private set; }
        internal float ContinuationDamageAfter { get; private set; }
        internal float ContinuationPenetrationAfter { get; private set; }
        internal Vector3 HitPoint { get; private set; }
        internal List<Vector3> Path { get; private set; } = new List<Vector3>();

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

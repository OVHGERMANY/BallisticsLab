using System.Collections.Generic;
using EFT;
using EFT.Ballistics;
using EFT.InventoryLogic;

namespace BallisticsLab.Runtime.Fixtures
{
    internal sealed class LabPlateRuntime
    {
        private static readonly SkillManager.FloatBuff NeutralLightBuff = new SkillManager.FloatBuff();
        private static readonly SkillManager.FloatBuff NeutralHeavyBuff = new SkillManager.FloatBuff();

        internal LabPlateRuntime(
            long fixtureId,
            int layerIndex,
            int layerCount,
            PlateCatalogEntry preset,
            Item item,
            ArmorComponent armor,
            EArmorPlateCollider plateCollider,
            List<ArmorComponent> stackArmors,
            float layerSpacing,
            float colliderThickness)
        {
            FixtureId = fixtureId;
            LayerIndex = layerIndex;
            LayerCount = layerCount;
            Preset = preset;
            Item = item;
            Armor = armor;
            PlateCollider = plateCollider;
            StackArmors = stackArmors;
            LayerSpacing = layerSpacing;
            ColliderThickness = colliderThickness;
        }

        internal long FixtureId { get; }
        internal int LayerIndex { get; }
        internal int LayerCount { get; }
        internal PlateCatalogEntry Preset { get; }
        internal Item Item { get; }
        internal ArmorComponent Armor { get; }
        internal EArmorPlateCollider PlateCollider { get; }
        internal List<ArmorComponent> StackArmors { get; }
        internal float LayerSpacing { get; }
        internal float ColliderThickness { get; }

        internal float Durability => Armor?.Repairable?.Durability ?? 0f;
        internal float MaximumDurability => Armor?.Repairable?.MaxDurability ?? 0f;

        internal bool TryGetResistance(float penetrationPower, out ArmorResistanceData resistance)
        {
            resistance = default(ArmorResistanceData);
            if (Armor?.Repairable == null || Armor.Repairable.Durability <= 0f)
            {
                return false;
            }

            resistance = ShotSharedMethods.RealResistance(
                Armor.Repairable.Durability,
                Armor.Repairable.TemplateDurability,
                Armor.ArmorClass,
                penetrationPower);
            return true;
        }

        internal bool TryGetAnalysis(
            float penetrationPower,
            out ArmorResistanceData resistance,
            out float penetrationChancePercent)
        {
            penetrationChancePercent = 0f;
            if (!TryGetResistance(penetrationPower, out resistance))
            {
                return false;
            }

            penetrationChancePercent = resistance.GetPenetrationChance(penetrationPower);
            return true;
        }

        internal float ApplyDurabilityDamage(ref DamageInfo damageInfo)
        {
            if (Armor == null)
            {
                return 0f;
            }

            return Armor.ApplyDamage(
                ref damageInfo,
                EBodyPartColliderType.RibcageUp,
                PlateCollider,
                true,
                StackArmors,
                NeutralLightBuff,
                NeutralHeavyBuff);
        }

        internal void ResetDurability()
        {
            if (Armor?.Repairable == null)
            {
                return;
            }

            Armor.Repairable.Durability = Armor.Repairable.MaxDurability;
            Item.RaiseRefreshEvent(false, false);
        }
    }
}

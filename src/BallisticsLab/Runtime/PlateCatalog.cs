using System;
using System.Collections.Generic;
using System.Linq;
using Comfort.Common;
using EFT;
using EFT.InventoryLogic;

namespace BallisticsLab.Runtime
{
    internal sealed class PlateCatalogEntry
    {
        internal PlateCatalogEntry(
            string templateId,
            string name,
            int armorClass,
            EArmorMaterial material,
            int durability,
            float bluntThroughput,
            EArmorPlateCollider plateCollider)
        {
            TemplateId = templateId;
            Name = name;
            ArmorClass = armorClass;
            Material = material;
            Durability = durability;
            BluntThroughput = bluntThroughput;
            PlateCollider = plateCollider;
        }

        internal string TemplateId { get; }
        internal string Name { get; }
        internal int ArmorClass { get; }
        internal EArmorMaterial Material { get; }
        internal int Durability { get; }
        internal float BluntThroughput { get; }
        internal EArmorPlateCollider PlateCollider { get; }

        internal string DisplayName =>
            "C" + ArmorClass + " | " + Material + " | " + Name + " | " + Durability + " DP";
    }

    internal sealed class PlateCatalog
    {
        internal const string GranitBr4TemplateId = "65573fa5655447403702a816";
        internal const string GranitBr5TemplateId = "64afc71497cf3a403c01ff38";

        private readonly List<PlateCatalogEntry> _entries;

        private PlateCatalog(List<PlateCatalogEntry> entries)
        {
            _entries = entries;
        }

        internal IReadOnlyList<PlateCatalogEntry> Entries => _entries;

        internal static PlateCatalog Build()
        {
            ItemFactory factory = Singleton<ItemFactory>.Instance;
            List<PlateCatalogEntry> entries = new List<PlateCatalogEntry>();

            foreach (KeyValuePair<MongoID, ItemTemplate> pair in factory.ItemTemplates)
            {
                ArmoredEquipmentTemplate armor = pair.Value as ArmoredEquipmentTemplate;
                if (armor == null || armor.armorClass <= 0 || armor.MaxDurability <= 0)
                {
                    continue;
                }

                if (!(armor is ArmorPlateTemplate) && armor.ArmorMaterial != EArmorMaterial.Glass)
                {
                    continue;
                }

                EArmorPlateCollider plateCollider = EArmorPlateCollider.Plate_Granit_SAPI_chest;
                if (armor.armorPlateColliders != null && armor.armorPlateColliders.Length > 0)
                {
                    plateCollider = armor.armorPlateColliders[0];
                }

                string name = string.IsNullOrWhiteSpace(armor.Name) ? armor._name : armor.Name;
                entries.Add(new PlateCatalogEntry(
                    armor.StringId,
                    name ?? armor.StringId,
                    armor.armorClass,
                    armor.ArmorMaterial,
                    armor.Durability,
                    armor.BluntThroughput,
                    plateCollider));
            }

            entries = entries
                .OrderBy(entry => entry.ArmorClass)
                .ThenBy(entry => entry.Material)
                .ThenBy(entry => entry.Name, StringComparer.OrdinalIgnoreCase)
                .ToList();

            if (entries.Count < 39)
            {
                throw new InvalidOperationException(
                    "Only " + entries.Count + " usable armor templates resolved; expected at least 39 plate templates.");
            }

            return new PlateCatalog(entries);
        }

        internal int FindByTemplateId(string templateId)
        {
            return _entries.FindIndex(entry => string.Equals(entry.TemplateId, templateId, StringComparison.Ordinal));
        }

        internal int FindSteel(int armorClass)
        {
            int exact = _entries.FindIndex(
                entry => entry.Material == EArmorMaterial.ArmoredSteel && entry.ArmorClass == armorClass);
            if (exact >= 0)
            {
                return exact;
            }

            return _entries.FindIndex(entry => entry.Material == EArmorMaterial.ArmoredSteel);
        }

        internal int FindMaterial(EArmorMaterial material, int preferredArmorClass)
        {
            int exact = _entries.FindIndex(
                entry => entry.Material == material && entry.ArmorClass == preferredArmorClass);
            if (exact >= 0)
            {
                return exact;
            }

            PlateCatalogEntry nearest = _entries
                .Where(entry => entry.Material == material)
                .OrderBy(entry => Math.Abs(entry.ArmorClass - preferredArmorClass))
                .ThenByDescending(entry => entry.Durability)
                .FirstOrDefault();
            return nearest == null ? -1 : _entries.IndexOf(nearest);
        }

        internal List<int> Search(string query)
        {
            if (string.IsNullOrWhiteSpace(query))
            {
                return Enumerable.Range(0, _entries.Count).ToList();
            }

            string needle = query.Trim();
            List<int> result = new List<int>();
            for (int index = 0; index < _entries.Count; index++)
            {
                PlateCatalogEntry entry = _entries[index];
                if (entry.DisplayName.IndexOf(needle, StringComparison.OrdinalIgnoreCase) >= 0
                    || entry.TemplateId.IndexOf(needle, StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    result.Add(index);
                }
            }

            return result;
        }
    }
}

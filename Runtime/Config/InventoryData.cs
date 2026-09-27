using System;
using System.Collections.Generic;
using UnityEngine;

namespace InventorySystem
{
    /// <summary>How an inventory limits what it can hold.</summary>
    public enum InventoryType
    {
        /// <summary>No limit: slots open as long as items keep coming.</summary>
        Unlimited,

        /// <summary>Unlimited slots, but the total weight of every item can't go over <c>MaxWeight</c>.</summary>
        Weight,

        /// <summary>A fixed number of slots.</summary>
        MaxSlot,

        /// <summary>A <c>Columns</c> x <c>Rows</c> grid; every item covers a rectangle of cells, its <see cref="ItemDefinition.Size"/>.</summary>
        Grid
    }

    /// <summary>
    /// Capacity settings of one inventory, shown at the top of each group of an
    /// <see cref="InventoryData"/>. Also usable on its own - e.g. as a serialized field on a chest - with
    /// <see cref="Inventory.Create"/>.
    /// </summary>
    [Serializable]
    public sealed class InventorySettings
    {
        /// <summary>The largest Columns/Rows value the Inspector allows.</summary>
        public const int MaxGridSize = 32;

        [SerializeField] private InventoryType inventoryType = InventoryType.MaxSlot;

        [Tooltip("The most the inventory can carry, in the same unit as the items' Weight.")]
        [Min(0.01f)] [SerializeField] private float maxWeight = 50f;

        [Tooltip("How many slots the inventory has.")]
        [Min(1)] [SerializeField] private int maxSlots = 20;

        [Tooltip("Width of the grid, in cells.")]
        [Range(1, MaxGridSize)] [SerializeField] private int columns = 8;

        [Tooltip("Height of the grid, in cells.")]
        [Range(1, MaxGridSize)] [SerializeField] private int rows = 5;

        public InventorySettings()
        {
        }

        public InventorySettings(InventoryType inventoryType, float maxWeight = 50f, int maxSlots = 20, int columns = 8, int rows = 5)
        {
            this.inventoryType = inventoryType;
            this.maxWeight = maxWeight;
            this.maxSlots = maxSlots;
            this.columns = columns;
            this.rows = rows;
        }

        /// <summary>A copy of these settings, so two owners never share one.</summary>
        public InventorySettings Clone() => (InventorySettings)MemberwiseClone();

        public static InventorySettings Unlimited() => new(InventoryType.Unlimited);

        public static InventorySettings WithMaxWeight(float maxWeight) => new(InventoryType.Weight, maxWeight: maxWeight);

        public static InventorySettings WithMaxSlots(int maxSlots) => new(InventoryType.MaxSlot, maxSlots: maxSlots);

        public static InventorySettings WithGrid(int columns, int rows) => new(InventoryType.Grid, columns: columns, rows: rows);

        public InventoryType InventoryType => inventoryType;

        /// <summary>Used only by <see cref="InventoryType.Weight"/>.</summary>
        public float MaxWeight => maxWeight;

        /// <summary>Used only by <see cref="InventoryType.MaxSlot"/>.</summary>
        public int MaxSlots => maxSlots;

        /// <summary>Used only by <see cref="InventoryType.Grid"/>.</summary>
        public int Columns => columns;

        /// <summary>Used only by <see cref="InventoryType.Grid"/>.</summary>
        public int Rows => rows;
    }

    /// <summary>
    /// A named group of <see cref="ItemDefinition"/>s (e.g. "Weapons", "Books") with an inventory of its own: every
    /// group has its own <see cref="InventorySettings"/>, so one can be unlimited and another a grid. Its name becomes
    /// an <see cref="ItemGroups"/> member on Compile. With more than one group, the inventory window gets a popup and a
    /// tab button per group (see InventoryRoles).
    /// </summary>
    [Serializable]
    public sealed class ItemGroup
    {
        // Stable ItemGroups value, assigned once by InventoryData and never reused, so renaming or reordering
        // groups never shifts an ItemGroups already saved in a scene or prefab.
        [HideInInspector] [SerializeField] internal int id;

        [SerializeField] private string header = "New Group";
        [SerializeField] private InventorySettings settings = new();
        [SerializeField] private List<ItemDefinition> items = new();

        /// <summary>The group's stable id - the same number as <see cref="Type"/>.</summary>
        public int Id => id;

        /// <summary>The group's <see cref="ItemGroups"/> member.</summary>
        public ItemGroups Type => (ItemGroups)id;

        public string Header => header;

        /// <summary>Capacity settings of this group's inventory.</summary>
        public InventorySettings Settings => settings;

        public List<ItemDefinition> Items => items;

        internal void SetSettings(InventorySettings value) => settings = value;
    }

    /// <summary>
    /// Everything the inventory system needs: every item the game knows about, organized in groups, each group with
    /// the capacity settings of its own inventory. Press "Compile" to generate the <see cref="ItemTypes"/> and
    /// <see cref="ItemGroups"/> enums and give every item's prefab a <see cref="Collectible"/>. At runtime, hand
    /// this asset to <see cref="InventoryManager.Initialize"/>.
    /// </summary>
    [CreateAssetMenu(menuName = "Inventory System/Inventory Data", fileName = "NewInventoryData")]
    public sealed class InventoryData : ScriptableObject
    {
        // From before every group had settings of its own: the whole inventory's. Copied into every group once
        // (MigrateLegacySettings), then unused - kept so an asset saved back then still loads.
        [HideInInspector] [SerializeField] private InventorySettings settings = new();
        [HideInInspector] [SerializeField] private bool settingsMigrated;

        [SerializeField] private List<ItemGroup> groups = new() { new ItemGroup() };

        // The next ids to hand out. They never go down, so a deleted item's or group's id is never reused.
        [HideInInspector] [SerializeField] private int nextItemId = 1;
        [HideInInspector] [SerializeField] private int nextGroupId = 1;

        /// <summary>Every group of items.</summary>
        public List<ItemGroup> Groups => groups;

        /// <summary>
        /// Gives every item (and group) without an id, or with an id another one already has (e.g. a duplicated
        /// list element), a fresh one; hands the old whole-inventory settings to the groups once. Returns true if
        /// anything changed.
        /// </summary>
        internal bool EnsureUniqueIds()
        {
            var changed = MigrateLegacySettings();
            changed |= EnsureUniqueGroupIds();
            changed |= EnsureUniqueItemIds();
            return changed;
        }

        private bool MigrateLegacySettings()
        {
            if (settingsMigrated)
            {
                return false;
            }

            foreach (var group in groups)
            {
                group?.SetSettings(settings.Clone());
            }

            settingsMigrated = true;
            return true;
        }

        private bool EnsureUniqueGroupIds()
        {
            foreach (var group in groups)
            {
                if (group != null && group.id >= nextGroupId)
                {
                    nextGroupId = group.id + 1;
                }
            }

            var changed = false;
            var seen = new HashSet<int>();
            foreach (var group in groups)
            {
                if (group == null || (group.id > 0 && seen.Add(group.id)))
                {
                    continue;
                }

                group.id = nextGroupId++;
                seen.Add(group.id);
                changed = true;
            }

            return changed;
        }

        private bool EnsureUniqueItemIds()
        {
            foreach (var group in groups)
            {
                if (group == null) continue;

                foreach (var item in group.Items)
                {
                    if (item != null && item.id >= nextItemId)
                    {
                        nextItemId = item.id + 1;
                    }
                }
            }

            var changed = false;
            var seen = new HashSet<int>();
            foreach (var group in groups)
            {
                if (group == null) continue;

                foreach (var item in group.Items)
                {
                    if (item == null || (item.id > 0 && seen.Add(item.id)))
                    {
                        continue;
                    }

                    item.id = nextItemId++;
                    seen.Add(item.id);
                    changed = true;
                }
            }

            return changed;
        }

#if UNITY_EDITOR
        private void OnValidate() => EnsureUniqueIds();
#endif
    }
}

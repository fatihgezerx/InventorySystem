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

        /// <summary>A <c>Columns</c> x <c>Rows</c> grid; every item covers the cells of its <see cref="ItemShape"/>.</summary>
        Grid
    }

    /// <summary>
    /// Capacity settings of one inventory, shown as the "Inventory Settings" block of an
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
    /// A named group of <see cref="ItemDefinition"/>s (e.g. "Weapons", "Consumables"). Purely
    /// organizational - grouping has no effect at runtime, it only keeps a large item list readable in
    /// the Inspector.
    /// </summary>
    [Serializable]
    public sealed class ItemGroup
    {
        [SerializeField] private string header = "New Group";
        [SerializeField] private List<ItemDefinition> items = new();

        public string Header => header;

        public List<ItemDefinition> Items => items;
    }

    /// <summary>
    /// Everything the inventory system needs: the inventory's capacity settings on top, every item the
    /// game knows about below, organized in groups. Press "Compile" to generate the
    /// <see cref="ItemTypes"/> enum and give every item's prefab a <see cref="Collectible"/>. At runtime,
    /// hand this asset to <see cref="InventoryManager.Initialize"/>.
    /// </summary>
    [CreateAssetMenu(menuName = "Inventory System/Inventory Data", fileName = "NewInventoryData")]
    public sealed class InventoryData : ScriptableObject
    {
        [SerializeField] private InventorySettings settings = new();

        [SerializeField] private List<ItemGroup> groups = new() { new ItemGroup() };

        // The next id to hand out. Never goes down, so a deleted item's id is never reused.
        [HideInInspector] [SerializeField] private int nextItemId = 1;

        /// <summary>Capacity settings of the inventory <see cref="InventoryManager"/> creates.</summary>
        public InventorySettings Settings => settings;

        /// <summary>Every group of items.</summary>
        public List<ItemGroup> Groups => groups;

        /// <summary>
        /// Gives every item without an id, or with an id another item already has (e.g. a duplicated
        /// list element), a fresh one. Returns true if anything changed.
        /// </summary>
        internal bool EnsureUniqueIds()
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

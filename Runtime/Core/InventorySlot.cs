using System;
using System.Collections.Generic;
using UnityEngine;

namespace InventorySystem
{
    /// <summary>
    /// One slot of an <see cref="Inventory"/>: an item and how many of it. Read-only from outside - change
    /// it through the inventory. A UI typically keeps one widget per slot, bound by <see cref="Index"/>,
    /// and redraws it on <c>SlotChanged</c>.
    /// </summary>
    /// <remarks>
    /// In a <see cref="GridInventory"/>, a slot is one placed item: <see cref="Position"/> is its top-left
    /// cell and <see cref="Rotation"/> how many times it is turned 90° clockwise; empty slots have no cells.
    /// </remarks>
    public sealed class InventorySlot
    {
        internal InventorySlot(int index)
        {
            Index = index;
        }

        /// <summary>Position of this slot in <see cref="Inventory.Slots"/>. Never changes.</summary>
        public int Index { get; }

        /// <summary>The item in this slot, or null when it is empty.</summary>
        public ItemDefinition Item { get; internal set; }

        public int Amount { get; internal set; }

        /// <summary>Grid only: the top-left cell the item covers.</summary>
        public Vector2Int Position { get; internal set; }

        /// <summary>Grid only: clockwise quarter turns (0-3). In a UI, rotate the icon by <c>-90 * Rotation</c> degrees on Z.</summary>
        public int Rotation { get; internal set; }

        public bool IsEmpty => Item == null;

        /// <summary>The item's type, or <see cref="ItemTypes.None"/> when empty.</summary>
        public ItemTypes Type => Item != null ? Item.Type : ItemTypes.None;

        /// <summary>Whether the slot holds its item's <see cref="ItemDefinition.MaxStack"/>.</summary>
        public bool IsFull => Item != null && Amount >= Item.MaxStack;

        /// <summary>How many more of its item fit in this slot. 0 when empty.</summary>
        public int SpaceLeft => Item != null ? Item.MaxStack - Amount : 0;

        /// <summary>Total weight of the slot's contents.</summary>
        public float Weight => Item != null ? Item.Weight * Amount : 0f;

        /// <summary>Grid only: width and height, in cells, of the area the item covers in its current rotation.</summary>
        public Vector2Int Size => Item != null ? ItemShapes.GetSize(Item.Shape, Rotation) : Vector2Int.zero;

        /// <summary>Grid only: the cells the item covers, relative to <see cref="Position"/>.</summary>
        public IReadOnlyList<Vector2Int> Cells => Item != null ? ItemShapes.GetCells(Item.Shape, Rotation) : Array.Empty<Vector2Int>();
    }
}

using System;
using UnityEngine;

namespace InventorySystem
{
    /// <summary>
    /// A <see cref="Columns"/> x <see cref="Rows"/> grid where every item covers the cells of its
    /// <see cref="ItemShape"/>. Each slot is one placed item (a stack, for stackable items) with a
    /// <see cref="InventorySlot.Position"/> and <see cref="InventorySlot.Rotation"/>. New items go into the
    /// first spot they fit, scanning row by row - unrotated first, then (for items with Can Rotate) each
    /// other rotation.
    /// </summary>
    /// <remarks>
    /// Occupancy is a flat <c>int[]</c> of cells holding "slot index + 1" (0 = free), so hit-testing a
    /// cell is one array read and a fit test touches only the item's own 1-4 cells. A free-cell count
    /// lets "is there room at all?" fail in O(1) when the grid is full.
    /// </remarks>
    public sealed class GridInventory : Inventory
    {
        // Marks cells taken by a dry run (CountFreeSlotsFor); never left behind.
        private const int Reserved = -1;

        private readonly int _columns;
        private readonly int _rows;
        private readonly int[] _cells;
        private int _freeCells;

        internal GridInventory(ItemDatabase database, int columns, int rows) : base(database)
        {
            _columns = Math.Max(1, columns);
            _rows = Math.Max(1, rows);
            _cells = new int[_columns * _rows];
            _freeCells = _cells.Length;
        }

        public override InventoryType Type => InventoryType.Grid;

        public int Columns => _columns;

        public int Rows => _rows;

        public int CellCount => _cells.Length;

        public int FreeCellCount => _freeCells;

        protected override bool CanRearrange => false;

        #region Queries

        public bool IsInside(Vector2Int cell) => (uint)cell.x < (uint)_columns && (uint)cell.y < (uint)_rows;

        /// <summary>Index of the slot covering <paramref name="cell"/>, or -1 if the cell is free or outside the grid.</summary>
        public int GetSlotIndexAt(Vector2Int cell) => IsInside(cell) ? _cells[cell.y * _columns + cell.x] - 1 : -1;

        public bool IsCellFree(Vector2Int cell) => IsInside(cell) && _cells[cell.y * _columns + cell.x] == 0;

        /// <summary>Whether a new <paramref name="type"/> would fit with its top-left cell at <paramref name="position"/>.</summary>
        public bool CanPlace(ItemTypes type, Vector2Int position, int rotation = 0)
        {
            var item = Database.Get(type);
            return IsRotationAllowed(item, rotation) && Fits(item.Shape, position, ItemShapes.NormalizeRotation(item.Shape, rotation), 0);
        }

        /// <summary>
        /// Whether the item in <paramref name="slotIndex"/> could move to <paramref name="position"/> in
        /// <paramref name="rotation"/> - its own current cells count as free. E.g. to tint a drag preview.
        /// </summary>
        public bool CanMoveTo(int slotIndex, Vector2Int position, int rotation)
        {
            if (!IsValidSlot(slotIndex))
            {
                return false;
            }

            var item = Slots[slotIndex].Item;
            return item != null
                   && IsRotationAllowed(item, rotation)
                   && Fits(item.Shape, position, ItemShapes.NormalizeRotation(item.Shape, rotation), slotIndex + 1);
        }

        #endregion

        #region Changes

        /// <summary>
        /// Moves the item in <paramref name="slotIndex"/> so its top-left cell is <paramref name="position"/>,
        /// turned <paramref name="rotation"/> quarter turns clockwise. Returns false if it doesn't fit there.
        /// To merge a stack into another stack of the same item instead, use <see cref="Inventory.Move"/>.
        /// </summary>
        public bool MoveTo(int slotIndex, Vector2Int position, int rotation)
        {
            if (!CanMoveTo(slotIndex, position, rotation))
            {
                return false;
            }

            var slot = Slots[slotIndex];
            var shape = slot.Item.Shape;
            rotation = ItemShapes.NormalizeRotation(shape, rotation);
            if (slot.Position == position && slot.Rotation == rotation)
            {
                return true;
            }

            Mark(shape, slot.Position, slot.Rotation, 0);
            slot.Position = position;
            slot.Rotation = rotation;
            Mark(shape, position, rotation, slotIndex + 1);

            NotifySlotChanged(slotIndex);
            NotifyChanged();
            return true;
        }

        /// <summary>Turns the item in <paramref name="slotIndex"/> 90° clockwise in place. Returns false if it can't rotate or doesn't fit.</summary>
        public bool Rotate(int slotIndex)
        {
            if (!IsValidSlot(slotIndex))
            {
                return false;
            }

            var slot = Slots[slotIndex];
            if (slot.Item == null || !slot.Item.CanRotate || ItemShapes.GetRotationCount(slot.Item.Shape) == 1)
            {
                return false;
            }

            return MoveTo(slotIndex, slot.Position, slot.Rotation + 1);
        }

        #endregion

        protected override int CountFreeSlotsFor(ItemDefinition item, int needed)
        {
            // Dry run: place as many as possible on Reserved cells, count them, then free them again.
            var count = 0;
            while (count < needed && TryFindPlacement(item, out var position, out var rotation))
            {
                Mark(item.Shape, position, rotation, Reserved);
                count++;
            }

            if (count > 0)
            {
                for (var i = 0; i < _cells.Length; i++)
                {
                    if (_cells[i] == Reserved)
                    {
                        _cells[i] = 0;
                        _freeCells++;
                    }
                }
            }

            return count;
        }

        protected override bool TryAllocateSlot(ItemDefinition item, out int index)
        {
            if (!TryFindPlacement(item, out var position, out var rotation))
            {
                index = -1;
                return false;
            }

            index = FindFirstEmptySlot();
            if (index < 0)
            {
                index = AppendSlot();
            }

            var slot = Slots[index];
            slot.Position = position;
            slot.Rotation = rotation;
            Mark(item.Shape, position, rotation, index + 1);
            return true;
        }

        protected override void OnSlotReleased(InventorySlot slot) => Mark(slot.Item.Shape, slot.Position, slot.Rotation, 0);

        private static bool IsRotationAllowed(ItemDefinition item, int rotation) =>
            item.CanRotate || ItemShapes.NormalizeRotation(item.Shape, rotation) == 0;

        private bool TryFindPlacement(ItemDefinition item, out Vector2Int position, out int rotation)
        {
            var shape = item.Shape;
            if (ItemShapes.GetCellCount(shape) <= _freeCells)
            {
                var rotations = item.CanRotate ? ItemShapes.GetRotationCount(shape) : 1;
                for (rotation = 0; rotation < rotations; rotation++)
                {
                    var size = ItemShapes.GetSize(shape, rotation);
                    for (var y = 0; y <= _rows - size.y; y++)
                    {
                        for (var x = 0; x <= _columns - size.x; x++)
                        {
                            position = new Vector2Int(x, y);
                            if (Fits(shape, position, rotation, 0))
                            {
                                return true;
                            }
                        }
                    }
                }
            }

            position = default;
            rotation = 0;
            return false;
        }

        // `ignore` is the cell value (slot index + 1) that counts as free - the moving item's own cells.
        private bool Fits(ItemShape shape, Vector2Int position, int rotation, int ignore)
        {
            foreach (var offset in ItemShapes.GetCellArray(shape, rotation))
            {
                var x = position.x + offset.x;
                var y = position.y + offset.y;
                if ((uint)x >= (uint)_columns || (uint)y >= (uint)_rows)
                {
                    return false;
                }

                var value = _cells[y * _columns + x];
                if (value != 0 && value != ignore)
                {
                    return false;
                }
            }

            return true;
        }

        private void Mark(ItemShape shape, Vector2Int position, int rotation, int value)
        {
            foreach (var offset in ItemShapes.GetCellArray(shape, rotation))
            {
                var index = (position.y + offset.y) * _columns + position.x + offset.x;
                var previous = _cells[index];
                if (previous == 0 && value != 0)
                {
                    _freeCells--;
                }
                else if (previous != 0 && value == 0)
                {
                    _freeCells++;
                }

                _cells[index] = value;
            }
        }
    }
}

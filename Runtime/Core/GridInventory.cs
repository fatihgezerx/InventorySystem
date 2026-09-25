using System;
using System.Collections.Generic;
using UnityEngine;

namespace InventorySystem
{
    /// <summary>
    /// A <see cref="Columns"/> x <see cref="Rows"/> grid, like a Resident Evil 4 attaché case: every item
    /// covers a rectangle of cells (its <see cref="ItemDefinition.Size"/>). Each slot is one placed item (a
    /// stack, for stackable items) with a <see cref="InventorySlot.Position"/> and
    /// <see cref="InventorySlot.Rotation"/>. New items go into the first spot they fit, scanning row by row -
    /// unturned first, then (for items with Can Rotate) turned. <see cref="Organize"/> packs everything
    /// tightly; <see cref="Resize"/> makes the case bigger (or smaller) with the items where they are.
    /// </summary>
    /// <remarks>
    /// Occupancy is a flat <c>int[]</c> of cells holding "slot index + 1" (0 = free), so hit-testing a
    /// cell is one array read and a fit test touches only the item's own cells. A free-cell count lets
    /// "is there room at all?" fail in O(1) when the grid is full.
    /// </remarks>
    public sealed class GridInventory : Inventory
    {
        // Marks cells taken by a dry run (CountFreeSlotsFor); never left behind.
        private const int Reserved = -1;

        private int _columns;
        private int _rows;
        private int[] _cells;
        private int _freeCells;

        // Organize's working buffers, grown to the slot count when needed and reused afterwards.
        private int[] _order = Array.Empty<int>();
        private Vector2Int[] _savedPositions = Array.Empty<Vector2Int>();
        private int[] _savedRotations = Array.Empty<int>();
        private readonly PackingOrder _packingOrder;

        internal GridInventory(ItemDatabase database, int columns, int rows) : base(database)
        {
            _columns = Math.Max(1, columns);
            _rows = Math.Max(1, rows);
            _cells = new int[_columns * _rows];
            _freeCells = _cells.Length;
            _packingOrder = new PackingOrder(this);
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
            return IsRotationAllowed(item, rotation) && Fits(item.GetSize(item.NormalizeRotation(rotation)), position, 0);
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
                   && Fits(item.GetSize(item.NormalizeRotation(rotation)), position, slotIndex + 1);
        }

        #endregion

        #region Changes

        /// <summary>
        /// Moves the item in <paramref name="slotIndex"/> so its top-left cell is <paramref name="position"/>,
        /// turned (<paramref name="rotation"/> 1) or not (0). Returns false if it doesn't fit there. To merge
        /// a stack into another stack of the same item instead, use <see cref="Inventory.Move"/>.
        /// </summary>
        public bool MoveTo(int slotIndex, Vector2Int position, int rotation)
        {
            if (!CanMoveTo(slotIndex, position, rotation))
            {
                return false;
            }

            var slot = Slots[slotIndex];
            rotation = slot.Item.NormalizeRotation(rotation);
            if (slot.Position == position && slot.Rotation == rotation)
            {
                return true;
            }

            Mark(slot.Size, slot.Position, 0);
            slot.Position = position;
            slot.Rotation = rotation;
            Mark(slot.Size, position, slotIndex + 1);

            NotifySlotChanged(slotIndex);
            NotifyChanged();
            return true;
        }

        /// <summary>Turns the item in <paramref name="slotIndex"/> 90° in place. Returns false if it can't rotate or doesn't fit.</summary>
        public bool Rotate(int slotIndex)
        {
            if (!IsValidSlot(slotIndex))
            {
                return false;
            }

            var slot = Slots[slotIndex];
            return slot.Item != null && slot.Item.IsRotatable && MoveTo(slotIndex, slot.Position, slot.Rotation + 1);
        }

        /// <summary>
        /// Packs every item tightly, like an attaché case's "Organize": the largest first (by cells, then by
        /// longest side), each into the first spot it fits, row by row, turned if that is the only way.
        /// Stacks stay as they are. Returns false - and moves nothing - if the packing can't find room for
        /// every item (possible when the grid is nearly full).
        /// </summary>
        public bool Organize()
        {
            var count = 0;
            EnsureBuffers();
            for (var i = 0; i < SlotCount; i++)
            {
                var slot = Slots[i];
                _savedPositions[i] = slot.Position;
                _savedRotations[i] = slot.Rotation;
                if (!slot.IsEmpty)
                {
                    _order[count++] = i;
                }
            }

            if (count == 0)
            {
                return true;
            }

            Array.Sort(_order, 0, count, _packingOrder);
            ClearCells();

            for (var i = 0; i < count; i++)
            {
                var slot = Slots[_order[i]];
                if (!TryFindPlacement(slot.Item, out var position, out var rotation))
                {
                    RestoreSavedLayout();
                    return false;
                }

                slot.Position = position;
                slot.Rotation = rotation;
                Mark(slot.Size, position, slot.Index + 1);
            }

            var moved = false;
            for (var i = 0; i < count; i++)
            {
                var index = _order[i];
                var slot = Slots[index];
                if (slot.Position != _savedPositions[index] || slot.Rotation != _savedRotations[index])
                {
                    NotifySlotChanged(index);
                    moved = true;
                }
            }

            if (moved)
            {
                NotifyChanged();
            }

            return true;
        }

        /// <summary>
        /// Changes the grid to <paramref name="columns"/> x <paramref name="rows"/> - e.g. a bigger case bought
        /// from a merchant. Every item keeps its cell. Returns false - and changes nothing - if an item would
        /// end up outside a smaller grid. Raises <see cref="Inventory.LayoutChanged"/>, then
        /// <see cref="Inventory.Changed"/>.
        /// </summary>
        public bool Resize(int columns, int rows)
        {
            columns = Math.Max(1, columns);
            rows = Math.Max(1, rows);
            if (columns == _columns && rows == _rows)
            {
                return true;
            }

            foreach (var slot in Slots)
            {
                if (slot.IsEmpty)
                {
                    continue;
                }

                var end = slot.Position + slot.Size;
                if (end.x > columns || end.y > rows)
                {
                    return false;
                }
            }

            _columns = columns;
            _rows = rows;
            _cells = new int[columns * rows];
            _freeCells = _cells.Length;
            MarkAllSlots();

            NotifyLayoutChanged();
            NotifyChanged();
            return true;
        }

        #endregion

        protected override int CountFreeSlotsFor(ItemDefinition item, int needed)
        {
            // Dry run: place as many as possible on Reserved cells, count them, then free them again.
            var count = 0;
            while (count < needed && TryFindPlacement(item, out var position, out var rotation))
            {
                Mark(item.GetSize(rotation), position, Reserved);
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
            Mark(item.GetSize(rotation), position, index + 1);
            return true;
        }

        protected override void OnSlotReleased(InventorySlot slot) => Mark(slot.Size, slot.Position, 0);

        private static bool IsRotationAllowed(ItemDefinition item, int rotation) => item.CanRotate || (rotation & 1) == 0;

        private bool TryFindPlacement(ItemDefinition item, out Vector2Int position, out int rotation)
        {
            if (item.CellCount <= _freeCells)
            {
                var rotations = item.IsRotatable ? 2 : 1;
                for (rotation = 0; rotation < rotations; rotation++)
                {
                    var size = item.GetSize(rotation);
                    for (var y = 0; y <= _rows - size.y; y++)
                    {
                        for (var x = 0; x <= _columns - size.x; x++)
                        {
                            position = new Vector2Int(x, y);
                            if (Fits(size, position, 0))
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
        private bool Fits(Vector2Int size, Vector2Int position, int ignore)
        {
            if (position.x < 0 || position.y < 0 || position.x + size.x > _columns || position.y + size.y > _rows)
            {
                return false;
            }

            for (var y = position.y; y < position.y + size.y; y++)
            {
                var row = y * _columns;
                for (var x = position.x; x < position.x + size.x; x++)
                {
                    var value = _cells[row + x];
                    if (value != 0 && value != ignore)
                    {
                        return false;
                    }
                }
            }

            return true;
        }

        private void Mark(Vector2Int size, Vector2Int position, int value)
        {
            for (var y = position.y; y < position.y + size.y; y++)
            {
                var row = y * _columns;
                for (var x = position.x; x < position.x + size.x; x++)
                {
                    var index = row + x;
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

        private void MarkAllSlots()
        {
            foreach (var slot in Slots)
            {
                if (!slot.IsEmpty)
                {
                    Mark(slot.Size, slot.Position, slot.Index + 1);
                }
            }
        }

        private void ClearCells()
        {
            Array.Clear(_cells, 0, _cells.Length);
            _freeCells = _cells.Length;
        }

        // Organize couldn't place everything: every item goes back where it was.
        private void RestoreSavedLayout()
        {
            for (var i = 0; i < SlotCount; i++)
            {
                var slot = Slots[i];
                slot.Position = _savedPositions[i];
                slot.Rotation = _savedRotations[i];
            }

            ClearCells();
            MarkAllSlots();
        }

        private void EnsureBuffers()
        {
            if (_order.Length >= SlotCount)
            {
                return;
            }

            var length = Math.Max(SlotCount, _order.Length * 2);
            _order = new int[length];
            _savedPositions = new Vector2Int[length];
            _savedRotations = new int[length];
        }

        // Organize's order: most cells first, then the longest side, then by item and slot so equal items
        // end up side by side and the result is always the same.
        private sealed class PackingOrder : IComparer<int>
        {
            private readonly GridInventory _grid;

            public PackingOrder(GridInventory grid) => _grid = grid;

            public int Compare(int a, int b)
            {
                var itemA = _grid.Slots[a].Item;
                var itemB = _grid.Slots[b].Item;

                var byCells = itemB.CellCount.CompareTo(itemA.CellCount);
                if (byCells != 0)
                {
                    return byCells;
                }

                var sizeA = itemA.Size;
                var sizeB = itemB.Size;
                var byLongestSide = Math.Max(sizeB.x, sizeB.y).CompareTo(Math.Max(sizeA.x, sizeA.y));
                if (byLongestSide != 0)
                {
                    return byLongestSide;
                }

                var byItem = itemA.Id.CompareTo(itemB.Id);
                return byItem != 0 ? byItem : a.CompareTo(b);
            }
        }
    }
}

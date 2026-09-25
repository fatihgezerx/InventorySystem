using System;
using System.Collections.Generic;

namespace InventorySystem
{
    /// <summary>
    /// A container of <see cref="InventorySlot"/>s. Pure C#: no MonoBehaviour, no Update, no allocation
    /// on the steady-state Add/Remove path. Stacking is handled here the same way for every mode -
    /// stackable items top up the stacks already held (up to <see cref="ItemDefinition.MaxStack"/>)
    /// before opening a new slot, everything else opens a new slot per item - while <i>where</i> a new
    /// slot can go is up to the subclass: <see cref="ListInventory"/> (Unlimited, Weight, MaxSlot) or
    /// <see cref="GridInventory"/> (Grid). Create one with <see cref="Create"/>.
    /// </summary>
    /// <remarks>
    /// Every change is reported through the C# events below, in this order: <see cref="SlotCountChanged"/>
    /// (a slot was added, before it is filled), <see cref="SlotChanged"/> per touched slot,
    /// <see cref="SlotCountChanged"/> (empty slots trimmed from the end), then <see cref="ItemAdded"/> /
    /// <see cref="ItemRemoved"/> / <see cref="AddRejected"/>, and <see cref="Changed"/> last, once per call.
    /// The inventory created by <see cref="InventoryManager"/> also publishes them as EventManager events.
    /// </remarks>
    public abstract class Inventory
    {
        private readonly List<InventorySlot> _slots = new();
        // Slots trimmed from the end, kept to be reused when the inventory grows again. They always
        // come back at the index they left from (see AppendSlot), so their Index stays right.
        private readonly Stack<InventorySlot> _spareSlots = new();
        private readonly int[] _counts;
        private double _weight;
        private int _usedSlotCount;

        protected Inventory(ItemDatabase database)
        {
            Database = database ?? throw new ArgumentNullException(nameof(database));
            _counts = new int[database.IdCapacity];
        }

        /// <summary>Creates the inventory <paramref name="settings"/> describes, holding items of <paramref name="database"/>.</summary>
        public static Inventory Create(ItemDatabase database, InventorySettings settings)
        {
            if (settings == null) throw new ArgumentNullException(nameof(settings));

            return settings.InventoryType switch
            {
                InventoryType.Weight => new ListInventory(database, InventoryType.Weight, int.MaxValue, settings.MaxWeight),
                InventoryType.MaxSlot => new ListInventory(database, InventoryType.MaxSlot, settings.MaxSlots, float.PositiveInfinity),
                InventoryType.Grid => new GridInventory(database, settings.Columns, settings.Rows),
                _ => new ListInventory(database, InventoryType.Unlimited, int.MaxValue, float.PositiveInfinity)
            };
        }

        /// <summary>A slot's contents changed (index). Redraw that slot.</summary>
        public event Action<int> SlotChanged;

        /// <summary>The number of slots changed (new count). Unlimited, Weight and Grid inventories grow and shrink; MaxSlot never does.</summary>
        public event Action<int> SlotCountChanged;

        /// <summary>Items were added (item, amount actually added).</summary>
        public event Action<ItemDefinition, int> ItemAdded;

        /// <summary>Items were removed (item, amount actually removed). Not raised by <see cref="Clear"/>.</summary>
        public event Action<ItemDefinition, int> ItemRemoved;

        /// <summary>An add didn't fit, fully or partly (item, amount that didn't fit) - e.g. to show "Inventory full".</summary>
        public event Action<ItemDefinition, int> AddRejected;

        /// <summary>Anything changed. Raised once per call, after every other event - e.g. to refresh a weight bar.</summary>
        public event Action Changed;

        /// <summary>
        /// The whole layout changed at once - e.g. a grid was resized - so per-slot events don't describe it.
        /// Redraw everything. Raised before <see cref="Changed"/>.
        /// </summary>
        public event Action LayoutChanged;

        /// <summary>The items this inventory can hold.</summary>
        public ItemDatabase Database { get; }

        public abstract InventoryType Type { get; }

        /// <summary>Every slot, empty ones included. Index a slot here by <see cref="InventorySlot.Index"/>.</summary>
        public IReadOnlyList<InventorySlot> Slots => _slots;

        public int SlotCount => _slots.Count;

        /// <summary>How many slots hold something.</summary>
        public int UsedSlotCount => _usedSlotCount;

        public bool IsEmpty => _usedSlotCount == 0;

        /// <summary>Total weight of everything held. Tracked in every mode; only <see cref="InventoryType.Weight"/> limits it.</summary>
        public float TotalWeight => (float)_weight;

        /// <summary>Whether the slot list has a fixed length (MaxSlot). Otherwise slots are added as needed and empty ones trimmed from the end.</summary>
        protected virtual bool IsFixedSize => false;

        /// <summary>Whether slots can be swapped, or a stack moved into an empty slot, by <see cref="Move"/>. Grids place items by cell instead.</summary>
        protected virtual bool CanRearrange => true;

        #region Queries

        /// <summary>How many of <paramref name="type"/> the inventory holds, across every slot. O(1).</summary>
        public int GetCount(ItemTypes type)
        {
            var id = (int)type;
            return (uint)id < (uint)_counts.Length ? _counts[id] : 0;
        }

        public bool Contains(ItemTypes type, int amount = 1) => GetCount(type) >= amount;

        /// <summary>Index of the first slot holding <paramref name="type"/>, or -1.</summary>
        public int FindSlot(ItemTypes type)
        {
            if (GetCount(type) == 0)
            {
                return -1;
            }

            for (var i = 0; i < _slots.Count; i++)
            {
                if (_slots[i].Type == type)
                {
                    return i;
                }
            }

            return -1;
        }

        /// <summary>How many of <paramref name="amount"/> x <paramref name="type"/> would fit right now. Changes nothing.</summary>
        public int GetAddableAmount(ItemTypes type, int amount)
        {
            var item = Database.Get(type);
            if (amount <= 0)
            {
                return 0;
            }

            var limit = LimitByCapacity(item, amount);
            var stackSpace = GetStackSpace(item, limit);
            if (stackSpace >= limit)
            {
                return limit;
            }

            var maxStack = item.MaxStack;
            var slotsNeeded = (limit - stackSpace + maxStack - 1) / maxStack;
            var newSlots = CountFreeSlotsFor(item, slotsNeeded);
            return Math.Min(limit, stackSpace + newSlots * maxStack);
        }

        /// <summary>Whether all <paramref name="amount"/> x <paramref name="type"/> would fit right now. Changes nothing.</summary>
        public bool CanAdd(ItemTypes type, int amount = 1) => amount > 0 && GetAddableAmount(type, amount) == amount;

        #endregion

        #region Changes

        /// <summary>
        /// Adds as many of <paramref name="amount"/> x <paramref name="type"/> as fit and returns how many
        /// did. Whatever doesn't fit is reported through <see cref="AddRejected"/>; use <see cref="CanAdd"/>
        /// first for all-or-nothing.
        /// </summary>
        public int Add(ItemTypes type, int amount = 1)
        {
            var item = Database.Get(type);
            if (amount <= 0)
            {
                return 0;
            }

            var accepted = LimitByCapacity(item, amount);
            var remaining = accepted;
            var maxStack = item.MaxStack;

            // 1. Top up the stacks already held - skipped entirely when none of this item is held.
            if (maxStack > 1 && _counts[item.Id] > 0)
            {
                for (var i = 0; i < _slots.Count && remaining > 0; i++)
                {
                    var slot = _slots[i];
                    if (slot.Item != item || slot.Amount >= maxStack)
                    {
                        continue;
                    }

                    var put = Math.Min(remaining, maxStack - slot.Amount);
                    SetSlot(slot, item, slot.Amount + put);
                    remaining -= put;
                }
            }

            // 2. Open new slots for the rest.
            while (remaining > 0 && TryAllocateSlot(item, out var index))
            {
                var put = Math.Min(remaining, maxStack);
                SetSlot(_slots[index], item, put);
                remaining -= put;
            }

            var added = accepted - remaining;
            if (added > 0)
            {
                ItemAdded?.Invoke(item, added);
            }

            if (added < amount)
            {
                AddRejected?.Invoke(item, amount - added);
            }

            if (added > 0)
            {
                Changed?.Invoke();
            }

            return added;
        }

        /// <summary>
        /// Removes up to <paramref name="amount"/> x <paramref name="type"/>, emptying the last stacks
        /// first, and returns how many were removed. Use <see cref="Contains"/> first for all-or-nothing
        /// (e.g. crafting costs).
        /// </summary>
        public int Remove(ItemTypes type, int amount = 1)
        {
            var item = Database.Get(type);
            if (amount <= 0 || _counts[item.Id] == 0)
            {
                return 0;
            }

            var remaining = amount;
            for (var i = _slots.Count - 1; i >= 0 && remaining > 0; i--)
            {
                var slot = _slots[i];
                if (slot.Item != item)
                {
                    continue;
                }

                var take = Math.Min(remaining, slot.Amount);
                SetSlot(slot, item, slot.Amount - take);
                remaining -= take;
            }

            var removed = amount - remaining;
            TrimTrailingEmptySlots();
            ItemRemoved?.Invoke(item, removed);
            Changed?.Invoke();
            return removed;
        }

        /// <summary>Removes up to <paramref name="amount"/> (default: all) from one slot - e.g. "drop" or "use" on a slot in the UI. Returns how many were removed.</summary>
        public int RemoveAt(int slotIndex, int amount = int.MaxValue)
        {
            if (!IsValidSlot(slotIndex) || amount <= 0)
            {
                return 0;
            }

            var slot = _slots[slotIndex];
            var item = slot.Item;
            if (item == null)
            {
                return 0;
            }

            var removed = Math.Min(amount, slot.Amount);
            SetSlot(slot, item, slot.Amount - removed);
            TrimTrailingEmptySlots();
            ItemRemoved?.Invoke(item, removed);
            Changed?.Invoke();
            return removed;
        }

        /// <summary>
        /// Drag and drop between two slots of this inventory: moves up to <paramref name="amount"/>
        /// (default: the whole stack) from <paramref name="from"/> to <paramref name="to"/>. The same
        /// stackable item merges as far as it fits; an empty target receives the stack (or the part
        /// moved); a different item swaps places when the whole stack is moved. Grids only merge - to
        /// reposition an item there, use <see cref="GridInventory.MoveTo"/>. Returns true if anything changed.
        /// </summary>
        public bool Move(int from, int to, int amount = int.MaxValue)
        {
            if (!IsValidSlot(from) || !IsValidSlot(to) || from == to || amount <= 0)
            {
                return false;
            }

            var source = _slots[from];
            var target = _slots[to];
            var item = source.Item;
            if (item == null)
            {
                return false;
            }

            amount = Math.Min(amount, source.Amount);

            if (target.Item == item)
            {
                var moved = Math.Min(amount, target.SpaceLeft);
                if (moved == 0)
                {
                    return false;
                }

                SetSlot(target, item, target.Amount + moved);
                SetSlot(source, item, source.Amount - moved);
            }
            else if (!CanRearrange)
            {
                return false;
            }
            else if (target.IsEmpty)
            {
                SetSlot(target, item, amount);
                SetSlot(source, item, source.Amount - amount);
            }
            else
            {
                // A different item: only a whole stack can swap places with it.
                if (amount < source.Amount)
                {
                    return false;
                }

                var targetItem = target.Item;
                var targetAmount = target.Amount;
                SetSlot(target, item, source.Amount);
                SetSlot(source, targetItem, targetAmount);
            }

            TrimTrailingEmptySlots();
            Changed?.Invoke();
            return true;
        }

        /// <summary>Moves <paramref name="amount"/> out of a stack into a new slot. Returns the new slot's index, or -1 if it can't.</summary>
        public int Split(int slotIndex, int amount)
        {
            if (!IsValidSlot(slotIndex))
            {
                return -1;
            }

            var slot = _slots[slotIndex];
            var item = slot.Item;
            if (item == null || amount <= 0 || amount >= slot.Amount || !TryAllocateSlot(item, out var index))
            {
                return -1;
            }

            SetSlot(_slots[index], item, amount);
            SetSlot(slot, item, slot.Amount - amount);
            Changed?.Invoke();
            return index;
        }

        /// <summary>
        /// Moves up to <paramref name="amount"/> (default: all) of one slot into <paramref name="target"/>
        /// (e.g. player &lt;-&gt; chest), wherever it fits there. Returns how many moved.
        /// </summary>
        public int TransferTo(Inventory target, int slotIndex, int amount = int.MaxValue)
        {
            if (target == null) throw new ArgumentNullException(nameof(target));

            if (target == this || !IsValidSlot(slotIndex) || _slots[slotIndex].IsEmpty || amount <= 0)
            {
                return 0;
            }

            var slot = _slots[slotIndex];
            var moved = target.Add(slot.Type, Math.Min(amount, slot.Amount));
            if (moved > 0)
            {
                RemoveAt(slotIndex, moved);
            }

            return moved;
        }

        /// <summary>Empties every slot. Raises <see cref="SlotChanged"/> and <see cref="Changed"/>, not <see cref="ItemRemoved"/>.</summary>
        public void Clear()
        {
            if (_usedSlotCount == 0)
            {
                return;
            }

            foreach (var slot in _slots)
            {
                if (!slot.IsEmpty)
                {
                    SetSlot(slot, null, 0);
                }
            }

            TrimTrailingEmptySlots();
            Changed?.Invoke();
        }

        #endregion

        #region Subclass contract

        /// <summary>How many of <paramref name="amount"/> x <paramref name="item"/> a capacity rule other than slots (e.g. weight) allows.</summary>
        protected virtual int LimitByCapacity(ItemDefinition item, int amount) => amount;

        /// <summary>How many new slots (up to <paramref name="needed"/>) could be opened for <paramref name="item"/>. Must not change anything.</summary>
        protected abstract int CountFreeSlotsFor(ItemDefinition item, int needed);

        /// <summary>
        /// Finds (or appends, with <see cref="AppendSlot"/>) an empty slot that can take one stack of
        /// <paramref name="item"/>, and reserves whatever else it needs (e.g. grid cells). The caller fills it.
        /// </summary>
        protected abstract bool TryAllocateSlot(ItemDefinition item, out int index);

        /// <summary>Called just before a slot is emptied, while it still holds its item - e.g. to free its grid cells.</summary>
        protected virtual void OnSlotReleased(InventorySlot slot)
        {
        }

        /// <summary>Adds an empty slot at the end and returns its index.</summary>
        protected int AppendSlot()
        {
            var index = _slots.Count;
            _slots.Add(_spareSlots.Count > 0 ? _spareSlots.Pop() : new InventorySlot(index));
            SlotCountChanged?.Invoke(_slots.Count);
            return index;
        }

        /// <summary>Index of the first empty slot, or -1. O(1) when there is none.</summary>
        protected int FindFirstEmptySlot()
        {
            if (_usedSlotCount == _slots.Count)
            {
                return -1;
            }

            for (var i = 0; i < _slots.Count; i++)
            {
                if (_slots[i].IsEmpty)
                {
                    return i;
                }
            }

            return -1;
        }

        protected bool IsValidSlot(int index) => (uint)index < (uint)_slots.Count;

        protected void NotifySlotChanged(int index) => SlotChanged?.Invoke(index);

        protected void NotifyChanged() => Changed?.Invoke();

        protected void NotifyLayoutChanged() => LayoutChanged?.Invoke();

        #endregion

        // The only place a slot's contents change, so the per-item counts, total weight and used-slot
        // count can never drift from what the slots actually hold.
        private void SetSlot(InventorySlot slot, ItemDefinition item, int amount)
        {
            var oldItem = slot.Item;
            if (oldItem != null)
            {
                _counts[oldItem.Id] -= slot.Amount;
                _weight -= (double)oldItem.Weight * slot.Amount;
            }

            if (item == null || amount <= 0)
            {
                if (oldItem != null)
                {
                    OnSlotReleased(slot);
                    _usedSlotCount--;
                }

                slot.Item = null;
                slot.Amount = 0;
            }
            else
            {
                if (oldItem == null)
                {
                    _usedSlotCount++;
                }

                slot.Item = item;
                slot.Amount = amount;
                _counts[item.Id] += amount;
                _weight += (double)item.Weight * amount;
            }

            // Drops the floating-point residue many adds/removes leave behind.
            if (_usedSlotCount == 0)
            {
                _weight = 0d;
            }

            SlotChanged?.Invoke(slot.Index);
        }

        // Free room in the stacks of `item` already held, counted only up to `cap`.
        private int GetStackSpace(ItemDefinition item, int cap)
        {
            if (item.MaxStack <= 1 || _counts[item.Id] == 0)
            {
                return 0;
            }

            var space = 0;
            for (var i = 0; i < _slots.Count && space < cap; i++)
            {
                if (_slots[i].Item == item)
                {
                    space += _slots[i].SpaceLeft;
                }
            }

            return Math.Min(space, cap);
        }

        private void TrimTrailingEmptySlots()
        {
            if (IsFixedSize)
            {
                return;
            }

            var count = _slots.Count;
            while (count > 0 && _slots[count - 1].IsEmpty)
            {
                count--;
            }

            if (count == _slots.Count)
            {
                return;
            }

            // Pushed from the end down, so the next AppendSlot pops exactly the slot at index `count`.
            for (var i = _slots.Count - 1; i >= count; i--)
            {
                _spareSlots.Push(_slots[i]);
            }

            _slots.RemoveRange(count, _slots.Count - count);
            SlotCountChanged?.Invoke(_slots.Count);
        }
    }
}

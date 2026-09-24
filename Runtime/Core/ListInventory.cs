using System;

namespace InventorySystem
{
    /// <summary>
    /// An inventory of slots in a row, shared by three modes:
    /// <list type="bullet">
    /// <item><see cref="InventoryType.Unlimited"/>: slots are added whenever needed.</item>
    /// <item><see cref="InventoryType.Weight"/>: same, but the total weight stays at or below <see cref="MaxWeight"/>.</item>
    /// <item><see cref="InventoryType.MaxSlot"/>: exactly <see cref="MaxSlots"/> slots, created up front.</item>
    /// </list>
    /// A new stack goes into the first empty slot; in Unlimited and Weight, empty slots at the end are
    /// trimmed, while empty slots in the middle stay where they are so the rest keep their index.
    /// </summary>
    public sealed class ListInventory : Inventory
    {
        // Leaves room for float error when a weight fits exactly (e.g. 10 x 0.1 into 1.0).
        private const double WeightTolerance = 1e-4;

        private readonly InventoryType _type;
        private readonly int _maxSlots;
        private readonly float _maxWeight;

        internal ListInventory(ItemDatabase database, InventoryType type, int maxSlots, float maxWeight) : base(database)
        {
            _type = type;
            _maxSlots = Math.Max(1, maxSlots);
            _maxWeight = maxWeight;

            if (IsFixedSize)
            {
                for (var i = 0; i < _maxSlots; i++)
                {
                    AppendSlot();
                }
            }
        }

        public override InventoryType Type => _type;

        /// <summary>The slot limit: <c>int.MaxValue</c> unless this is a MaxSlot inventory.</summary>
        public int MaxSlots => _maxSlots;

        /// <summary>The weight limit: infinity unless this is a Weight inventory.</summary>
        public float MaxWeight => _maxWeight;

        /// <summary>How much more weight fits. Infinity unless this is a Weight inventory.</summary>
        public float RemainingWeight => Math.Max(0f, _maxWeight - TotalWeight);

        protected override bool IsFixedSize => _type == InventoryType.MaxSlot;

        protected override int LimitByCapacity(ItemDefinition item, int amount)
        {
            if (_type != InventoryType.Weight || item.Weight <= 0f)
            {
                return amount;
            }

            var fits = Math.Floor((_maxWeight - (double)TotalWeight + WeightTolerance) / item.Weight);
            return fits <= 0d ? 0 : (int)Math.Min(amount, fits);
        }

        protected override int CountFreeSlotsFor(ItemDefinition item, int needed)
        {
            var free = SlotCount - UsedSlotCount;
            if (!IsFixedSize)
            {
                free += Math.Min(needed, _maxSlots - SlotCount);
            }

            return Math.Min(free, needed);
        }

        protected override bool TryAllocateSlot(ItemDefinition item, out int index)
        {
            index = FindFirstEmptySlot();
            if (index >= 0)
            {
                return true;
            }

            if (IsFixedSize || SlotCount >= _maxSlots)
            {
                return false;
            }

            index = AppendSlot();
            return true;
        }
    }
}

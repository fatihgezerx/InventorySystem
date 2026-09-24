namespace InventorySystem
{
    // Published by InventoryManager for its Main inventory through EventSystem's EventManager, e.g.:
    //     EventManager.Register<ItemAddedEvent>(OnItemAdded);
    // All of them are readonly structs, so raising them never allocates. Other inventories (chests...)
    // raise the same changes through their own C# events instead.

    /// <summary>Items were added to <see cref="Inventory"/>.</summary>
    public readonly struct ItemAddedEvent
    {
        public readonly Inventory Inventory;
        public readonly ItemDefinition Item;

        /// <summary>How many were actually added.</summary>
        public readonly int Amount;

        public ItemAddedEvent(Inventory inventory, ItemDefinition item, int amount)
        {
            Inventory = inventory;
            Item = item;
            Amount = amount;
        }
    }

    /// <summary>Items were removed from <see cref="Inventory"/>.</summary>
    public readonly struct ItemRemovedEvent
    {
        public readonly Inventory Inventory;
        public readonly ItemDefinition Item;

        /// <summary>How many were actually removed.</summary>
        public readonly int Amount;

        public ItemRemovedEvent(Inventory inventory, ItemDefinition item, int amount)
        {
            Inventory = inventory;
            Item = item;
            Amount = amount;
        }
    }

    /// <summary>An add didn't fit into <see cref="Inventory"/>, fully or partly - e.g. to show "Inventory full".</summary>
    public readonly struct InventoryFullEvent
    {
        public readonly Inventory Inventory;
        public readonly ItemDefinition Item;

        /// <summary>How many didn't fit.</summary>
        public readonly int Amount;

        public InventoryFullEvent(Inventory inventory, ItemDefinition item, int amount)
        {
            Inventory = inventory;
            Item = item;
            Amount = amount;
        }
    }

    /// <summary>The contents of one slot changed - redraw it.</summary>
    public readonly struct SlotChangedEvent
    {
        public readonly Inventory Inventory;
        public readonly int SlotIndex;

        public SlotChangedEvent(Inventory inventory, int slotIndex)
        {
            Inventory = inventory;
            SlotIndex = slotIndex;
        }

        /// <summary>The slot as it is now.</summary>
        public InventorySlot Slot => Inventory.Slots[SlotIndex];
    }

    /// <summary>Slots were added or trimmed (never in MaxSlot inventories).</summary>
    public readonly struct SlotCountChangedEvent
    {
        public readonly Inventory Inventory;
        public readonly int Count;

        public SlotCountChangedEvent(Inventory inventory, int count)
        {
            Inventory = inventory;
            Count = count;
        }
    }

    /// <summary>Anything changed; raised once per call, after the other events - e.g. to refresh a weight bar.</summary>
    public readonly struct InventoryChangedEvent
    {
        public readonly Inventory Inventory;

        public InventoryChangedEvent(Inventory inventory) => Inventory = inventory;
    }
}

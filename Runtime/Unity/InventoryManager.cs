using System;
using System.Collections.Generic;
using EventSystem;
using UnityEngine;

namespace InventorySystem
{
    /// <summary>
    /// Static entry point of the inventory system. Call <see cref="Initialize"/> once (e.g. from a
    /// GameManager) with an <see cref="InventoryData"/>; it builds the <see cref="ItemDatabase"/> and one inventory
    /// per item group - each as its group's settings describe - and publishes every change of them through
    /// <see cref="EventManager"/>, so a UI only needs to listen, never poll.
    /// </summary>
    /// <remarks>
    /// An item lives in the inventory of its group: <see cref="Add"/>, <see cref="Remove"/>, <see cref="GetCount"/>
    /// and the other shortcuts find it by the item's <see cref="ItemTypes"/>. With a single group there is one inventory,
    /// <see cref="Main"/>. Published events: <see cref="ItemAddedEvent"/>, <see cref="ItemRemovedEvent"/>,
    /// <see cref="InventoryFullEvent"/>, <see cref="SlotChangedEvent"/>, <see cref="SlotCountChangedEvent"/>
    /// and <see cref="InventoryChangedEvent"/>, each carrying the inventory it happened in. Other inventories (chests,
    /// shops...) can be made with <see cref="Inventory.Create"/> and <see cref="Database"/>; they report through their
    /// own C# events.
    /// </remarks>
    public static class InventoryManager
    {
        private static Inventory[] _byGroupId = Array.Empty<Inventory>();
        private static Inventory[] _all = Array.Empty<Inventory>();
        private static readonly List<EventRelay> Relays = new();

        /// <summary>Whether <see cref="Initialize"/> has been called and the inventories are ready.</summary>
        public static bool IsInitialized => Main != null;

        /// <summary>The data the system was initialized with, or null.</summary>
        public static InventoryData Data { get; private set; }

        /// <summary>Every item of <see cref="Data"/>, looked up by <see cref="ItemTypes"/>. Null until initialized.</summary>
        public static ItemDatabase Database { get; private set; }

        /// <summary>
        /// The player's inventory when the data has one group; with several, the first group's - use
        /// <see cref="GetInventory"/> or <see cref="GetInventoryOf"/> for the others. Null until initialized.
        /// </summary>
        public static Inventory Main { get; private set; }

        /// <summary>Every group's inventory, in the order of the data's groups. Empty until initialized.</summary>
        public static IReadOnlyList<Inventory> Inventories => _all;

        /// <summary>
        /// Raised after <see cref="Initialize"/> with the first group's inventory (<see cref="Main"/>), and with null
        /// after <see cref="Shutdown"/> - e.g. for a UI that may be enabled before the GameManager initializes: it
        /// asks for its inventories again.
        /// </summary>
        public static event Action<Inventory> MainChanged;

        /// <summary>
        /// Builds the item database and every group's inventory from <paramref name="data"/>. Calling it again
        /// starts over with new, empty inventories.
        /// </summary>
        public static void Initialize(InventoryData data)
        {
            if (data == null) throw new ArgumentNullException(nameof(data));

            Shutdown();

            var maxId = 0;
            var groups = new List<ItemGroup>(data.Groups.Count);
            foreach (var group in data.Groups)
            {
                if (group == null || group.Id <= 0)
                {
                    Debug.LogWarning($"[InventoryManager] A group of '{data.name}' has no id yet; ignoring it. Open the InventoryData and press Compile.", data);
                    continue;
                }

                groups.Add(group);
                maxId = Math.Max(maxId, group.Id);
            }

            if (groups.Count == 0)
            {
                throw new InvalidOperationException($"[InventoryManager] '{data.name}' has no item group. Add one and press Compile.");
            }

            Data = data;
            Database = new ItemDatabase(data);

            _byGroupId = new Inventory[maxId + 1];
            _all = new Inventory[groups.Count];
            for (var i = 0; i < groups.Count; i++)
            {
                var inventory = Inventory.Create(Database, groups[i].Settings);
                _all[i] = inventory;
                _byGroupId[groups[i].Id] = inventory;
                Relays.Add(new EventRelay(inventory));
            }

            Main = _all[0];
            MainChanged?.Invoke(Main);
        }

        /// <summary>Drops the inventories and stops publishing events.</summary>
        public static void Shutdown()
        {
            var hadMain = Main != null;
            foreach (var relay in Relays)
            {
                relay.Detach();
            }

            Relays.Clear();
            _byGroupId = Array.Empty<Inventory>();
            _all = Array.Empty<Inventory>();
            Main = null;
            Database = null;
            Data = null;

            if (hadMain)
            {
                MainChanged?.Invoke(null);
            }
        }

        #region Inventories

        /// <summary>The inventory of <paramref name="group"/>. Throws if the data has no such group.</summary>
        public static Inventory GetInventory(ItemGroups group) =>
            TryGetInventory(group, out var inventory)
                ? inventory
                : throw new ArgumentException($"[InventoryManager] '{group}' is not a group of the initialized InventoryData. " +
                                              "Add it and press Compile.", nameof(group));

        public static bool TryGetInventory(ItemGroups group, out Inventory inventory)
        {
            var id = (int)group;
            inventory = (uint)id < (uint)_byGroupId.Length ? _byGroupId[id] : null;
            return inventory != null;
        }

        /// <summary>The inventory the item <paramref name="type"/> lives in: its group's. Throws if the data has no such item.</summary>
        public static Inventory GetInventoryOf(ItemTypes type)
        {
            var database = RequireDatabase();
            return TryGetInventory(database.GroupOf(type), out var inventory)
                ? inventory
                : throw new ArgumentException($"[InventoryManager] '{type}' is not an item of this InventoryData. " +
                                              "Add it and press Compile.", nameof(type));
        }

        #endregion

        #region Shortcuts

        /// <summary>The definition (name, icon, max stack...) of <paramref name="type"/>.</summary>
        public static ItemDefinition GetItem(ItemTypes type) => RequireDatabase().Get(type);

        public static bool TryGetItem(ItemTypes type, out ItemDefinition item)
        {
            item = null;
            return Database != null && Database.TryGet(type, out item);
        }

        /// <inheritdoc cref="Inventory.Add"/>
        public static int Add(ItemTypes type, int amount = 1) => GetInventoryOf(type).Add(type, amount);

        /// <inheritdoc cref="Inventory.Remove"/>
        public static int Remove(ItemTypes type, int amount = 1) => GetInventoryOf(type).Remove(type, amount);

        /// <inheritdoc cref="Inventory.GetCount"/>
        public static int GetCount(ItemTypes type) => GetInventoryOf(type).GetCount(type);

        /// <inheritdoc cref="Inventory.Contains"/>
        public static bool Contains(ItemTypes type, int amount = 1) => GetInventoryOf(type).Contains(type, amount);

        /// <inheritdoc cref="Inventory.CanAdd"/>
        public static bool CanAdd(ItemTypes type, int amount = 1) => GetInventoryOf(type).CanAdd(type, amount);

        #endregion

        private static ItemDatabase RequireDatabase() =>
            Database ?? throw new InvalidOperationException("[InventoryManager] Not initialized. Call InventoryManager.Initialize(inventoryData) first.");

        // Publishes one inventory's changes through EventManager, with the inventory in every event. One per
        // group, made once by Initialize - so nothing is allocated when an event is raised.
        private sealed class EventRelay
        {
            private readonly Inventory _inventory;

            public EventRelay(Inventory inventory)
            {
                _inventory = inventory;
                inventory.ItemAdded += OnItemAdded;
                inventory.ItemRemoved += OnItemRemoved;
                inventory.AddRejected += OnAddRejected;
                inventory.SlotChanged += OnSlotChanged;
                inventory.SlotCountChanged += OnSlotCountChanged;
                inventory.Changed += OnChanged;
            }

            public void Detach()
            {
                _inventory.ItemAdded -= OnItemAdded;
                _inventory.ItemRemoved -= OnItemRemoved;
                _inventory.AddRejected -= OnAddRejected;
                _inventory.SlotChanged -= OnSlotChanged;
                _inventory.SlotCountChanged -= OnSlotCountChanged;
                _inventory.Changed -= OnChanged;
            }

            private void OnItemAdded(ItemDefinition item, int amount) =>
                EventManager.Invoke(new ItemAddedEvent(_inventory, item, amount));

            private void OnItemRemoved(ItemDefinition item, int amount) =>
                EventManager.Invoke(new ItemRemovedEvent(_inventory, item, amount));

            private void OnAddRejected(ItemDefinition item, int amount) =>
                EventManager.Invoke(new InventoryFullEvent(_inventory, item, amount));

            private void OnSlotChanged(int index) =>
                EventManager.Invoke(new SlotChangedEvent(_inventory, index));

            private void OnSlotCountChanged(int count) =>
                EventManager.Invoke(new SlotCountChangedEvent(_inventory, count));

            private void OnChanged() =>
                EventManager.Invoke(new InventoryChangedEvent(_inventory));
        }

        // Keeps the static state clean when "Enter Play Mode Options" skips the domain reload.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            MainChanged = null;
            Relays.Clear();
            _byGroupId = Array.Empty<Inventory>();
            _all = Array.Empty<Inventory>();
            Main = null;
            Database = null;
            Data = null;
        }
    }
}

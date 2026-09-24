using System;
using EventSystem;
using UnityEngine;

namespace InventorySystem
{
    /// <summary>
    /// Static entry point of the inventory system. Call <see cref="Initialize"/> once (e.g. from a
    /// GameManager) with an <see cref="InventoryData"/>; it builds the <see cref="ItemDatabase"/> and the
    /// <see cref="Main"/> inventory the data's settings describe, and publishes every change of that
    /// inventory through <see cref="EventManager"/> - so a UI only needs to listen, never poll.
    /// </summary>
    /// <remarks>
    /// Published events: <see cref="ItemAddedEvent"/>, <see cref="ItemRemovedEvent"/>,
    /// <see cref="InventoryFullEvent"/>, <see cref="SlotChangedEvent"/>, <see cref="SlotCountChangedEvent"/>
    /// and <see cref="InventoryChangedEvent"/>. Other inventories (chests, shops...) can be made with
    /// <see cref="Inventory.Create"/> and <see cref="Database"/>; they report through their own C# events.
    /// </remarks>
    public static class InventoryManager
    {
        /// <summary>Whether <see cref="Initialize"/> has been called and <see cref="Main"/> is ready.</summary>
        public static bool IsInitialized => Main != null;

        /// <summary>The data the system was initialized with, or null.</summary>
        public static InventoryData Data { get; private set; }

        /// <summary>Every item of <see cref="Data"/>, looked up by <see cref="ItemTypes"/>. Null until initialized.</summary>
        public static ItemDatabase Database { get; private set; }

        /// <summary>The player's inventory. Null until initialized.</summary>
        public static Inventory Main { get; private set; }

        /// <summary>
        /// Raised with the new <see cref="Main"/> after <see cref="Initialize"/>, and with null after
        /// <see cref="Shutdown"/> - e.g. for a UI that may be enabled before the GameManager initializes.
        /// </summary>
        public static event Action<Inventory> MainChanged;

        /// <summary>
        /// Builds the item database and the main inventory from <paramref name="data"/>. Calling it again
        /// starts over with a new, empty inventory.
        /// </summary>
        public static void Initialize(InventoryData data)
        {
            if (data == null) throw new ArgumentNullException(nameof(data));

            Shutdown();

            Data = data;
            Database = new ItemDatabase(data);
            Main = Inventory.Create(Database, data.Settings);

            Main.ItemAdded += OnItemAdded;
            Main.ItemRemoved += OnItemRemoved;
            Main.AddRejected += OnAddRejected;
            Main.SlotChanged += OnSlotChanged;
            Main.SlotCountChanged += OnSlotCountChanged;
            Main.Changed += OnChanged;

            MainChanged?.Invoke(Main);
        }

        /// <summary>Drops the main inventory and stops publishing events.</summary>
        public static void Shutdown()
        {
            var hadMain = Main != null;
            if (hadMain)
            {
                Main.ItemAdded -= OnItemAdded;
                Main.ItemRemoved -= OnItemRemoved;
                Main.AddRejected -= OnAddRejected;
                Main.SlotChanged -= OnSlotChanged;
                Main.SlotCountChanged -= OnSlotCountChanged;
                Main.Changed -= OnChanged;
            }

            Main = null;
            Database = null;
            Data = null;

            if (hadMain)
            {
                MainChanged?.Invoke(null);
            }
        }

        #region Shortcuts to Main

        /// <summary>The definition (name, icon, max stack...) of <paramref name="type"/>.</summary>
        public static ItemDefinition GetItem(ItemTypes type) => RequireDatabase().Get(type);

        public static bool TryGetItem(ItemTypes type, out ItemDefinition item)
        {
            item = null;
            return Database != null && Database.TryGet(type, out item);
        }

        /// <inheritdoc cref="Inventory.Add"/>
        public static int Add(ItemTypes type, int amount = 1) => RequireMain().Add(type, amount);

        /// <inheritdoc cref="Inventory.Remove"/>
        public static int Remove(ItemTypes type, int amount = 1) => RequireMain().Remove(type, amount);

        /// <inheritdoc cref="Inventory.GetCount"/>
        public static int GetCount(ItemTypes type) => RequireMain().GetCount(type);

        /// <inheritdoc cref="Inventory.Contains"/>
        public static bool Contains(ItemTypes type, int amount = 1) => RequireMain().Contains(type, amount);

        /// <inheritdoc cref="Inventory.CanAdd"/>
        public static bool CanAdd(ItemTypes type, int amount = 1) => RequireMain().CanAdd(type, amount);

        #endregion

        private static Inventory RequireMain() =>
            Main ?? throw new InvalidOperationException("[InventoryManager] Not initialized. Call InventoryManager.Initialize(inventoryData) first.");

        private static ItemDatabase RequireDatabase() =>
            Database ?? throw new InvalidOperationException("[InventoryManager] Not initialized. Call InventoryManager.Initialize(inventoryData) first.");

        private static void OnItemAdded(ItemDefinition item, int amount) =>
            EventManager.Invoke(new ItemAddedEvent(Main, item, amount));

        private static void OnItemRemoved(ItemDefinition item, int amount) =>
            EventManager.Invoke(new ItemRemovedEvent(Main, item, amount));

        private static void OnAddRejected(ItemDefinition item, int amount) =>
            EventManager.Invoke(new InventoryFullEvent(Main, item, amount));

        private static void OnSlotChanged(int index) =>
            EventManager.Invoke(new SlotChangedEvent(Main, index));

        private static void OnSlotCountChanged(int count) =>
            EventManager.Invoke(new SlotCountChangedEvent(Main, count));

        private static void OnChanged() =>
            EventManager.Invoke(new InventoryChangedEvent(Main));

        // Keeps the static state clean when "Enter Play Mode Options" skips the domain reload.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            MainChanged = null;
            Main = null;
            Database = null;
            Data = null;
        }
    }
}

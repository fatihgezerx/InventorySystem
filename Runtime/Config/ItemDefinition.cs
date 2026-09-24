using System;
using UnityEngine;

namespace InventorySystem
{
    /// <summary>
    /// One item the inventory knows about: its name, icon, world prefab and stacking rules, plus the
    /// fields only one inventory mode uses (<see cref="Weight"/> for <see cref="InventoryType.Weight"/>,
    /// <see cref="Shape"/>/<see cref="CanRotate"/> for <see cref="InventoryType.Grid"/>). Edited inside an
    /// <see cref="InventoryData"/>; Compile turns its name into an <see cref="ItemTypes"/> member.
    /// </summary>
    [Serializable]
    public sealed class ItemDefinition
    {
        /// <summary>The highest value the Max Stack slider goes to.</summary>
        public const int MaxStackLimit = 999;

        // Stable ItemTypes value, assigned once by InventoryData and never reused, so reordering or
        // deleting items never shifts an ItemTypes already saved in a scene, prefab or asset.
        [HideInInspector] [SerializeField] internal int id;

        [Tooltip("The item's name, e.g. for UI. Compile turns it into its ItemTypes member (\"Health Potion\" -> ItemTypes.HealthPotion), so it must be unique.")]
        [SerializeField] private string displayName = string.Empty;

        [Tooltip("The item's icon in the UI.")]
        [SerializeField] private Sprite icon;

        [Tooltip("The world object that gives this item. Compile adds a Collectible to it. Optional: leave empty for items that only come from code (crafting, rewards...).")]
        [SerializeField] private GameObject prefab;

        [Tooltip("Stackable items pile up in one slot up to Max Stack before opening a new one. Others take a new slot every time.")]
        [SerializeField] private bool stackable;

        [Tooltip("How many fit in one slot. Used only when Stackable is on.")]
        [Range(2, MaxStackLimit)] [SerializeField] private int maxStack = 10;

        [Tooltip("Weight of one item. Used only by Weight inventories.")]
        [Min(0f)] [SerializeField] private float weight = 1f;

        [Tooltip("The cells the item covers. Used only by Grid inventories.")]
        [SerializeField] private ItemShape shape = ItemShape.Square1x1;

        [Tooltip("Whether the item may be turned 90 degrees to fit. Used only by Grid inventories.")]
        [SerializeField] private bool canRotate = true;

        /// <summary>The item's stable id - the same number as <see cref="Type"/>.</summary>
        public int Id => id;

        /// <summary>The item's <see cref="ItemTypes"/> member.</summary>
        public ItemTypes Type => (ItemTypes)id;

        /// <summary>The item's name as entered in <see cref="InventoryData"/>, e.g. "Health Potion".</summary>
        public string DisplayName => displayName;

        public Sprite Icon => icon;

        /// <summary>The world object that gives this item, or null.</summary>
        public GameObject Prefab => prefab;

        public bool Stackable => stackable;

        /// <summary>How many fit in one slot: the Max Stack value for stackable items, 1 otherwise.</summary>
        public int MaxStack => stackable ? maxStack : 1;

        /// <summary>Weight of one item. Only <see cref="InventoryType.Weight"/> inventories limit it.</summary>
        public float Weight => weight;

        /// <summary>The cells the item covers in a <see cref="InventoryType.Grid"/> inventory.</summary>
        public ItemShape Shape => shape;

        /// <summary>Whether a <see cref="InventoryType.Grid"/> inventory may turn the item 90 degrees to fit it.</summary>
        public bool CanRotate => canRotate;

        public override string ToString() => displayName;
    }
}

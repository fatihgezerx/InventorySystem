using System;
using UnityEngine;

namespace InventorySystem
{
    /// <summary>
    /// One item the inventory knows about: its name, description, icon, world prefab and stacking rules, plus the
    /// fields only one inventory mode uses (<see cref="Weight"/> for <see cref="InventoryType.Weight"/>,
    /// <see cref="Size"/>/<see cref="CanRotate"/> for <see cref="InventoryType.Grid"/>). Edited inside an
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

        // Name and Description are offered for translation by LocalizationSystem's "Sync Project". The
        // symbol is set only while LocalizationSystem is in the project, so the attributes appear as soon
        // as it is installed and nothing breaks without it.
#if HAS_LOCALIZATION_SYSTEM
        [LocalizationSystem.Localize]
#endif
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

#if HAS_LOCALIZATION_SYSTEM
        [LocalizationSystem.Localize]
#endif
        [Tooltip("What the item is, e.g. for a tooltip. Optional.")]
        [SerializeField] private string description = string.Empty;

        [Tooltip("Weight of one item. Used only by Weight inventories.")]
        [Min(0f)] [SerializeField] private float weight = 1f;

        [Tooltip("Width x height of the rectangle the item covers, in cells (e.g. 3 x 2 for a handgun, 9 x 2 for a rifle). Used only by Grid inventories.")]
        [SerializeField] private Vector2Int size = Vector2Int.one;

        [Tooltip("Whether the item may be turned 90 degrees to fit. Used only by Grid inventories.")]
        [SerializeField] private bool canRotate = true;

        /// <summary>The item's stable id - the same number as <see cref="Type"/>.</summary>
        public int Id => id;

        /// <summary>The item's <see cref="ItemTypes"/> member.</summary>
        public ItemTypes Type => (ItemTypes)id;

        /// <summary>The item's name as entered in <see cref="InventoryData"/>, e.g. "Health Potion".</summary>
        public string DisplayName => displayName;

        /// <summary>The item's description as entered in <see cref="InventoryData"/>. May be empty.</summary>
        public string Description => description;

        public Sprite Icon => icon;

        /// <summary>The world object that gives this item, or null.</summary>
        public GameObject Prefab => prefab;

        public bool Stackable => stackable;

        /// <summary>How many fit in one slot: the Max Stack value for stackable items, 1 otherwise.</summary>
        public int MaxStack => stackable ? maxStack : 1;

        /// <summary>Weight of one item. Only <see cref="InventoryType.Weight"/> inventories limit it.</summary>
        public float Weight => weight;

        /// <summary>
        /// Width (x) and height (y), in cells, of the rectangle the item covers in a
        /// <see cref="InventoryType.Grid"/> inventory, unturned. At least 1 x 1.
        /// </summary>
        public Vector2Int Size => Vector2Int.Max(size, Vector2Int.one);

        /// <summary>Whether a <see cref="InventoryType.Grid"/> inventory may turn the item 90 degrees to fit it.</summary>
        public bool CanRotate => canRotate;

        /// <summary>Whether turning the item changes anything: <see cref="CanRotate"/> is on and it isn't a square.</summary>
        public bool IsRotatable => canRotate && size.x != size.y;

        /// <summary>How many cells the item covers.</summary>
        public int CellCount => Size.x * Size.y;

        /// <summary>
        /// <see cref="Size"/> in <paramref name="rotation"/>: 0 as set, 1 turned 90 degrees clockwise (width and
        /// height swapped). Only the lowest bit counts, so any number of quarter turns works.
        /// </summary>
        public Vector2Int GetSize(int rotation)
        {
            var unturned = Size;
            return (rotation & 1) == 0 ? unturned : new Vector2Int(unturned.y, unturned.x);
        }

        /// <summary>Maps any rotation to 0 or 1, and to 0 when turning the item changes nothing.</summary>
        public int NormalizeRotation(int rotation) => IsRotatable ? rotation & 1 : 0;

        public override string ToString() => displayName;
    }
}

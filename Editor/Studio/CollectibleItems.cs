#if HAS_VENEER
using System;
using Veneer;
using UnityEditor;
using UnityEngine.UIElements;

namespace InventorySystem
{
    /// <summary>
    /// The pieces of the <see cref="Collectible"/> Inspector that are not plain fields, registered with Veneer so the default layout (and
    /// a user's copy of it) can place them: the state pill, the "no item" hint and the widget of the read-only <c>itemType</c> field
    /// (icon, name and a button that selects the Inventory Data). They can be moved or hidden in the Inspector Designer, not changed.
    /// </summary>
    internal static class CollectibleItems
    {
        /// <summary>Id of the header state pill: On contact, Manual or Mixed (header badge).</summary>
        public const string StateId = "collectible.state";

        /// <summary>Id of the "no item yet" hint (card item).</summary>
        public const string NoItemId = "collectible.no-item";

        /// <summary>The serialized path of the item the object gives; its widget is the read-only item row.</summary>
        public const string ItemPath = "itemType";

        private const string CollectPath = "collectOnTrigger";
        private const string NoItemMessage = "No item yet. Add this object's prefab to an item in the Inventory Data and press Compile.";
        private const string ItemTooltip = "Assigned by InventoryData > Compile. Edit the item in the Inventory Data.";
        private const string PingTooltip = "Select the Inventory Data this item is defined in.";

        /// <summary>Registers the custom items and the item widget of <see cref="Collectible"/>. Safe to call again (items are replaced).</summary>
        public static void Register()
        {
            Type type = typeof(Collectible);

            VeneerCustomItems.Register(type, new VeneerCustomItem
            {
                Id = StateId,
                Label = "Pickup pill",
                Icon = "check-circle",
                Description = "A pill in the header: On contact, Manual, or Mixed when the selected objects differ.",
                Slot = VeneerCustomItemSlot.HeaderBadge,
                Build = BuildState,
            });

            VeneerCustomItems.Register(type, new VeneerCustomItem
            {
                Id = NoItemId,
                Label = "No item check",
                Icon = "warning",
                Description = "A warning while no item is assigned (Compile in the Inventory Data assigns it).",
                Slot = VeneerCustomItemSlot.Item,
                Build = BuildNoItem,
            });

            VeneerCustomItems.RegisterWidget(type, ItemPath, BuildItemRow);
        }

        private static VisualElement BuildState(VeneerCustomItemContext context)
        {
            SerializedObject so = context.SerializedObject;
            SerializedProperty collect = so.FindProperty(CollectPath);
            Label pill = VeneerStyle.Pill(string.Empty, "idle");
            Action refresh = () =>
            {
                so.UpdateIfRequiredOrScript();
                if (collect.hasMultipleDifferentValues)
                {
                    VeneerStyle.SetPill(pill, "idle", "Mixed");
                }
                else if (collect.boolValue)
                {
                    VeneerStyle.SetPill(pill, "info", "On contact");
                }
                else
                {
                    VeneerStyle.SetPill(pill, "idle", "Manual");
                }
            };

            // The first run of Poll is on the first tick: show the right state at once.
            refresh();
            context.Poll(refresh);
            return pill;
        }

        private static VisualElement BuildNoItem(VeneerCustomItemContext context)
        {
            SerializedObject so = context.SerializedObject;
            SerializedProperty itemType = so.FindProperty(ItemPath);
            context.Builder.ConditionalHint(
                () =>
                {
                    so.UpdateIfRequiredOrScript();
                    return !itemType.hasMultipleDifferentValues && itemType.intValue == 0 ? NoItemMessage : null;
                },
                "warn");
            return null;
        }

        // The item this object gives: icon, name and a button that pings the Inventory Data it is defined in.
        private static VisualElement BuildItemRow(VeneerCustomItemContext context, SerializedProperty itemType)
        {
            VeneerBrand brand = context.Brand;
            SerializedProperty tracked = itemType.Copy();
            var inner = new VisualElement();
            inner.AddToClassList("vn-row");
            var thumb = new VisualElement();
            thumb.AddToClassList("inv-thumb");
            VisualElement placeholder = VeneerStyle.IconElement("image", "vn-icon--sm", brand);
            thumb.Add(placeholder);
            inner.Add(thumb);
            Label name = VeneerStyle.EllipsisLabel();
            name.AddToClassList("vn-grow");
            inner.Add(name);
            InventoryData owner = null;
            Button ping = VeneerStyle.IconButton("cursor", PingTooltip, () =>
            {
                if (owner != null)
                {
                    Selection.activeObject = owner;
                    EditorGUIUtility.PingObject(owner);
                }
            }, brand);
            inner.Add(ping);

            VisualElement row = VeneerStyle.FieldRow("Item", inner, ItemTooltip, out Label title);
            VeneerBinding.Hook(row, tracked, title, row, () =>
            {
                owner = null;
                ItemDefinition definition = null;
                if (tracked.hasMultipleDifferentValues)
                {
                    name.text = "Mixed";
                }
                else if (tracked.intValue == 0)
                {
                    name.text = "None";
                }
                else
                {
                    definition = Find(tracked.intValue, out owner);
                    name.text = definition != null ? definition.DisplayName : "Item #" + tracked.intValue + " (not in any Inventory Data)";
                }

                InventoryStudioHelpers.SetSprite(thumb, placeholder, definition != null ? definition.Icon : null);
                ping.SetEnabled(owner != null);
            });
            return row;
        }

        // The definition with this id in the first Inventory Data asset that has it.
        private static ItemDefinition Find(int id, out InventoryData owner)
        {
            foreach (string guid in AssetDatabase.FindAssets("t:" + nameof(InventoryData)))
            {
                var data = AssetDatabase.LoadAssetAtPath<InventoryData>(AssetDatabase.GUIDToAssetPath(guid));
                if (data == null)
                {
                    continue;
                }

                foreach (ItemGroup group in data.Groups)
                {
                    if (group == null)
                    {
                        continue;
                    }

                    foreach (ItemDefinition item in group.Items)
                    {
                        if (item != null && item.Id == id)
                        {
                            owner = data;
                            return item;
                        }
                    }
                }
            }

            owner = null;
            return null;
        }
    }
}
#endif

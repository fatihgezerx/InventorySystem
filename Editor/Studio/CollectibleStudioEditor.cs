#if HAS_VENEER
using Veneer;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace InventorySystem
{
    /// <summary>
    /// Inspector of <see cref="Collectible"/> in the studio look. Pickup tab: the item it gives (read-only, with its icon - the
    /// Inventory Data is where it is edited, Compile assigns it), the amount, and the pickup options (collect on contact, collector
    /// tag). Events tab: On Take, On Release and On Inventory Full. Works with several objects selected.
    /// </summary>
    [CustomEditor(typeof(Collectible))]
    [CanEditMultipleObjects]
    internal sealed class CollectibleStudioEditor : VeneerInspector<Collectible>
    {
        protected override VeneerBrand Brand => InventoryBrand.Value;

        protected override void Build(VeneerInspectorBuilder b)
        {
            SerializedProperty itemType = serializedObject.FindProperty("itemType");
            SerializedProperty collectOnTrigger = serializedObject.FindProperty("collectOnTrigger");
            SerializedProperty collectorTag = serializedObject.FindProperty("collectorTag");

            b.Header("Collectible", "Gives an item to the inventory");
            b.StatePill(() =>
            {
                serializedObject.UpdateIfRequiredOrScript();
                if (collectOnTrigger.hasMultipleDifferentValues)
                {
                    return ("Mixed", "idle");
                }

                return collectOnTrigger.boolValue ? ("On contact", "info") : ("Manual", "idle");
            });

            VisualElement[] tabs = b.Tabs(("Pickup", "tag"), ("Events", "bolt"));
            using (b.Into(tabs[0]))
            {
                b.Card("Item", "tag", () =>
                {
                    b.Add(BuildItemRow(itemType));
                    b.ConditionalHint(() =>
                    {
                        serializedObject.UpdateIfRequiredOrScript();
                        return !itemType.hasMultipleDifferentValues && itemType.intValue == 0
                            ? "No item yet. Add this object's prefab to an item in the Inventory Data and press Compile."
                            : null;
                    }, "warn");
                    b.Number("amount", "Amount", "items");
                });

                b.Card("Pick up", "target", () =>
                {
                    b.Chip("collectOnTrigger", "Collect on trigger", "bolt");

                    var tag = new TagField();
                    tag.BindProperty(collectorTag);
                    tag.tooltip = collectorTag.tooltip;
                    VisualElement tagRow = VeneerStyle.FieldRow("Collector tag", tag, collectorTag.tooltip, out Label tagLabel);
                    VeneerBinding.Hook(tagRow, collectorTag, tagLabel, tagRow, null);
                    b.Add(tagRow);
                    b.VisibleWhen(tagRow, "collectOnTrigger", true);

                    b.VisibleWhen(
                        b.Hint("Without Collect On Trigger, call Collect() from code or from an Interactable's On Interact.", "info"),
                        "collectOnTrigger", false);
                });
            }

            using (b.Into(tabs[1]))
            {
                b.Card("Events", "bolt", () =>
                {
                    b.Field("onTake", "On Take");
                    b.Field("onRelease", "On Release");
                    b.Field("onInventoryFull", "On Inventory Full");
                });
            }
        }

        // The item this object gives: icon, name and a button that pings the Inventory Data it is defined in.
        private VisualElement BuildItemRow(SerializedProperty itemType)
        {
            SerializedProperty tracked = itemType.Copy();
            var inner = new VisualElement();
            inner.AddToClassList("vn-row");
            var thumb = new VisualElement();
            thumb.AddToClassList("inv-thumb");
            VisualElement placeholder = VeneerStyle.IconElement("image", "vn-icon--sm", Brand);
            thumb.Add(placeholder);
            inner.Add(thumb);
            Label name = VeneerStyle.EllipsisLabel();
            name.AddToClassList("vn-grow");
            inner.Add(name);
            InventoryData owner = null;
            Button ping = VeneerStyle.IconButton("cursor", "Select the Inventory Data this item is defined in.", () =>
            {
                if (owner != null)
                {
                    Selection.activeObject = owner;
                    EditorGUIUtility.PingObject(owner);
                }
            }, Brand);
            inner.Add(ping);

            string tooltip = "Assigned by InventoryData > Compile. Edit the item in the Inventory Data.";
            VisualElement row = VeneerStyle.FieldRow("Item", inner, tooltip, out Label title);
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

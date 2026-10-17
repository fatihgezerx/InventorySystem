#if HAS_VENEER
using System;
using System.Text;
using Veneer;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace InventorySystem
{
    /// <summary>
    /// The Items tab of the InventoryData Inspector: chips to pick a group, that group's items as reorderable rows (icon slot, name,
    /// key numbers) and, under the list, the details of the selected item (icon, prefab, stacking, description, and the weight or grid
    /// footprint its inventory mode uses). The Inspector tells the pane when the data changed; the pane redraws only what differs.
    /// </summary>
    internal sealed class InventoryItemsPane
    {
        private const float RowHeight = 46f;
        private const string SessionPrefix = "InventorySystem.InventoryData.Item";

        private readonly InventoryStudioContext ctx;
        private readonly VisualElement root = new VisualElement();
        private readonly VisualElement chipBar = new VisualElement();
        private readonly VisualElement body = new VisualElement();

        private int groupIndex;
        private int selectedGroupId;
        private int selectedItemId;
        private string idKey;
        private string chipKey;
        private string itemsKey;

        private SerializedProperty itemsProp;
        private ListView list;
        private VisualElement details;
        private VisualElement emptyState;
        private Label countPill;
        private Label capacityPill;
        private Label capacityText;

        /// <summary>Called when the user asks for a first group from the empty state.</summary>
        public Action RequestAddGroup;

        public InventoryItemsPane(InventoryStudioContext ctx)
        {
            this.ctx = ctx;
            chipBar.AddToClassList("inv-chipbar");
            root.Add(chipBar);
            root.Add(body);
        }

        /// <summary>The element to put into the tab page.</summary>
        public VisualElement Root => root;

        internal SerializedProperty ItemsProp => itemsProp;

        internal int GroupIndex => groupIndex;

        /// <summary>Brings the pane in step with the data. The first call builds it.</summary>
        public void OnDataChanged()
        {
            SerializedProperty groups = ctx.So.FindProperty("groups");
            string ids = InventoryStudioHelpers.GroupIdKey(groups);
            if (!string.Equals(ids, idKey, StringComparison.Ordinal))
            {
                idKey = ids;
                ResolveGroup(groups);
                RebuildChips(groups);
                BuildBody();
                return;
            }

            if (!string.Equals(ChipKey(groups), chipKey, StringComparison.Ordinal))
            {
                RebuildChips(groups);
            }

            UpdateBody();
        }

        /// <summary>Selects item <paramref name="item"/> of group <paramref name="group"/> and scrolls to it.</summary>
        public void Show(int group, int item)
        {
            SerializedProperty groups = ctx.So.FindProperty("groups");
            if (group < 0 || group >= groups.arraySize)
            {
                return;
            }

            if (group != groupIndex)
            {
                SelectGroup(group);
            }

            if (list != null && item >= 0 && item < itemsProp.arraySize)
            {
                list.SetSelection(item);
                list.ScrollToItem(item);
            }
        }

        // ------------------------------------------------------------------ used by the rows

        internal void SelectItem(int index)
        {
            if (list != null && itemsProp != null && index >= 0 && index < itemsProp.arraySize && list.selectedIndex != index)
            {
                list.SetSelection(index);
            }
        }

        internal void RemoveItem(int index)
        {
            if (itemsProp == null || index < 0 || index >= itemsProp.arraySize)
            {
                return;
            }

            itemsProp.DeleteArrayElementAtIndex(index);
            ctx.So.ApplyModifiedProperties();
            list.RefreshItems();
        }

        internal string IssueFor(int index)
        {
            InventoryDataCheck.Report report = ctx.GetReport();
            StringBuilder text = null;
            for (int i = 0; i < report.Issues.Count; i++)
            {
                InventoryDataCheck.Issue issue = report.Issues[i];
                if (issue.Group == groupIndex && issue.Item == index)
                {
                    text = text == null ? new StringBuilder(issue.Message) : text.Append('\n').Append(issue.Message);
                }
            }

            return text == null ? null : text.ToString();
        }

        internal string Describe(SerializedProperty element)
        {
            return InventoryStudioHelpers.DescribeItem(element, InventoryStudioHelpers.ReadType(ctx.So, groupIndex));
        }

        internal VeneerBrand Brand => ctx.Brand;

        // ------------------------------------------------------------------ groups

        private static string ChipKey(SerializedProperty groups)
        {
            var key = new StringBuilder(64);
            for (int i = 0; i < groups.arraySize; i++)
            {
                SerializedProperty group = groups.GetArrayElementAtIndex(i);
                key.Append(group.FindPropertyRelative("header").stringValue).Append('#')
                    .Append(group.FindPropertyRelative("items").arraySize).Append(';');
            }

            return key.ToString();
        }

        private void ResolveGroup(SerializedProperty groups)
        {
            int found = -1;
            if (selectedGroupId > 0)
            {
                for (int i = 0; i < groups.arraySize; i++)
                {
                    if (groups.GetArrayElementAtIndex(i).FindPropertyRelative("id").intValue == selectedGroupId)
                    {
                        found = i;
                        break;
                    }
                }
            }

            groupIndex = found >= 0 ? found : Mathf.Clamp(groupIndex, 0, Mathf.Max(0, groups.arraySize - 1));
            selectedGroupId = groups.arraySize > 0 ? groups.GetArrayElementAtIndex(groupIndex).FindPropertyRelative("id").intValue : 0;
        }

        private void RebuildChips(SerializedProperty groups)
        {
            chipKey = ChipKey(groups);
            chipBar.Clear();
            VeneerStyle.SetDisplay(chipBar, groups.arraySize > 1);
            for (int i = 0; i < groups.arraySize; i++)
            {
                int index = i;
                SerializedProperty group = groups.GetArrayElementAtIndex(i);
                string name = group.FindPropertyRelative("header").stringValue;
                if (string.IsNullOrWhiteSpace(name))
                {
                    name = "(unnamed)";
                }

                string label = name + "  " + group.FindPropertyRelative("items").arraySize;
                VisualElement chip = VeneerStyle.Chip(label, null, i == groupIndex, on => SelectGroup(index), ctx.Brand);
                chip.tooltip = "Show the items of '" + name + "'.";
                chipBar.Add(chip);
            }
        }

        private void SelectGroup(int index)
        {
            SerializedProperty groups = ctx.So.FindProperty("groups");
            if (index < 0 || index >= groups.arraySize)
            {
                return;
            }

            groupIndex = index;
            selectedGroupId = groups.GetArrayElementAtIndex(index).FindPropertyRelative("id").intValue;
            for (int i = 0; i < chipBar.childCount; i++)
            {
                VeneerStyle.SetChipOn(chipBar[i], i == index);
            }

            BuildBody();
        }

        // ------------------------------------------------------------------ body

        private void BuildBody()
        {
            if (list != null)
            {
                list.Unbind();
                list = null;
            }

            body.Clear();
            itemsProp = null;
            details = null;
            itemsKey = null;
            selectedItemId = 0;

            SerializedProperty groups = ctx.So.FindProperty("groups");
            if (groups.arraySize == 0)
            {
                VisualElement empty = VeneerStyle.EmptyState("database", "No groups yet",
                    "Items live in groups, and every group is an inventory of its own. Add the first one.", ctx.Brand);
                empty.Add(VeneerStyle.Button("Add group", "plus", "Adds a group and opens the Groups tab.", () =>
                {
                    if (RequestAddGroup != null)
                    {
                        RequestAddGroup();
                    }
                }, true, ctx.Brand));
                body.Add(empty);
                return;
            }

            groupIndex = Mathf.Clamp(groupIndex, 0, groups.arraySize - 1);
            itemsProp = ctx.So.FindProperty("groups.Array.data[" + groupIndex + "].items");

            VisualElement summary = new VisualElement();
            summary.AddToClassList("inv-summary");
            capacityPill = VeneerStyle.Pill(string.Empty, "info");
            capacityText = VeneerStyle.MutedLabel(string.Empty);
            summary.Add(capacityPill);
            summary.Add(capacityText);
            body.Add(summary);

            var block = new VisualElement();
            block.AddToClassList("vn-list");
            var header = new VisualElement();
            header.AddToClassList("vn-list__header");
            var title = new Label("Items");
            title.AddToClassList("vn-list__title");
            header.Add(title);
            countPill = VeneerStyle.Pill("0", "info");
            header.Add(countPill);
            header.Add(VeneerStyle.Spacer());
            header.Add(VeneerStyle.Button("Add item", "plus", "Adds a blank item to this group and selects it.", AddItem, false, ctx.Brand));
            block.Add(header);

            list = new ListView
            {
                bindingPath = itemsProp.propertyPath,
                reorderable = true,
                reorderMode = ListViewReorderMode.Animated,
                virtualizationMethod = CollectionVirtualizationMethod.FixedHeight,
                fixedItemHeight = RowHeight,
                selectionType = SelectionType.Single,
                showAddRemoveFooter = false,
                showFoldoutHeader = false,
                showBoundCollectionSize = false,
                showAlternatingRowBackgrounds = AlternatingRowBackground.ContentOnly,
            };
            list.AddToClassList("vn-list__view");
            list.AddToClassList("inv-list");
            list.makeItem = () => new InventoryItemRow(this);
            list.bindItem = (element, index) => ((InventoryItemRow)element).Bind(index);
            list.unbindItem = (element, index) => ((InventoryItemRow)element).Unbind();
            list.selectedIndicesChanged += indices => OnSelectionChanged();
            block.Add(list);

            emptyState = VeneerStyle.EmptyState("tag", "No items in this group",
                "Press Add item, then drop a sprite on the icon slot and give it a name.", ctx.Brand);
            block.Add(emptyState);
            body.Add(block);

            details = new VisualElement();
            body.Add(details);

            list.Bind(ctx.So);
            UpdateBody();
            if (itemsProp.arraySize > 0)
            {
                list.SetSelectionWithoutNotify(new[] { 0 });
            }

            OnSelectionChanged();
        }

        private void UpdateBody()
        {
            if (itemsProp == null || list == null)
            {
                return;
            }

            string key = ItemsKey();
            if (!string.Equals(key, itemsKey, StringComparison.Ordinal))
            {
                bool first = itemsKey == null;
                itemsKey = key;
                list.RefreshItems();
                if (!first)
                {
                    Reselect();
                }
            }

            list.Query<InventoryItemRow>().ForEach(RefreshRow);

            int count = itemsProp.arraySize;
            VeneerStyle.SetPill(countPill, "info", count.ToString());
            VeneerStyle.SetDisplay(emptyState, count == 0);
            VeneerStyle.SetDisplay(list, count > 0);

            SerializedProperty settings = ctx.So.FindProperty("groups.Array.data[" + groupIndex + "].settings");
            InventoryType type = (InventoryType)settings.FindPropertyRelative("inventoryType").intValue;
            VeneerStyle.SetPill(capacityPill, "info", InventoryStudioHelpers.TypeNames[(int)type]);
            capacityText.text = InventoryStudioHelpers.DescribeCapacity(settings) + "  |  change it in the Groups tab";
        }

        private static void RefreshRow(InventoryItemRow row)
        {
            row.Refresh();
        }

        private string ItemsKey()
        {
            var key = new StringBuilder(64);
            key.Append(itemsProp.arraySize).Append(':');
            for (int i = 0; i < itemsProp.arraySize; i++)
            {
                key.Append(IdAt(i)).Append(';');
            }

            return key.ToString();
        }

        private int IdAt(int index)
        {
            return itemsProp.GetArrayElementAtIndex(index).FindPropertyRelative("id").intValue;
        }

        // The items moved (reorder, add, remove, Undo): keep the same item selected when it still exists.
        private void Reselect()
        {
            int index = -1;
            if (selectedItemId > 0)
            {
                for (int i = 0; i < itemsProp.arraySize; i++)
                {
                    if (IdAt(i) == selectedItemId)
                    {
                        index = i;
                        break;
                    }
                }
            }

            if (index < 0)
            {
                index = Mathf.Min(list.selectedIndex, itemsProp.arraySize - 1);
            }

            if (index >= 0)
            {
                list.SetSelectionWithoutNotify(new[] { index });
            }
            else
            {
                list.ClearSelection();
            }

            selectedItemId = index >= 0 ? IdAt(index) : 0;
            ShowDetails();
        }

        private void AddItem()
        {
            if (itemsProp == null)
            {
                return;
            }

            itemsProp.arraySize++;
            int last = itemsProp.arraySize - 1;
            InventoryStudioHelpers.ResetItem(itemsProp.GetArrayElementAtIndex(last));
            ctx.So.ApplyModifiedProperties();
            list.RefreshItems();
            list.SetSelection(last);
            list.ScrollToItem(last);
        }

        private void OnSelectionChanged()
        {
            if (itemsProp == null || list == null)
            {
                return;
            }

            int index = list.selectedIndex;
            selectedItemId = index >= 0 && index < itemsProp.arraySize ? IdAt(index) : 0;
            ShowDetails();
        }

        private void ShowDetails()
        {
            if (details == null)
            {
                return;
            }

            details.Clear();
            int index = list.selectedIndex;
            if (index < 0 || index >= itemsProp.arraySize)
            {
                details.Add(VeneerStyle.Hint("Select an item to edit its icon, prefab, stacking and size.", "info", ctx.Brand));
                return;
            }

            details.Add(BuildDetails(index));
        }

        private VisualElement BuildDetails(int item)
        {
            var holder = new VisualElement();
            var nb = new VeneerInspectorBuilder(ctx.So, holder, ctx.Brand, SessionPrefix);
            int group = groupIndex;
            string groupPath = "groups.Array.data[" + group + "]";
            string itemPath = groupPath + ".items.Array.data[" + item + "]";
            string typePath = groupPath + ".settings.inventoryType";

            nb.Card("Item", "tag", () =>
            {
                nb.Object(itemPath + ".icon", "Icon", typeof(Sprite));
                nb.Object(itemPath + ".prefab", "Prefab", typeof(GameObject));
                nb.Chip(itemPath + ".stackable", "Stackable", "layers");
                VisualElement stack = nb.Number(itemPath + ".maxStack", "Max stack", "per slot");
                nb.VisibleWhen(stack, itemPath + ".stackable", true);

                SerializedProperty description = ctx.So.FindProperty(itemPath + ".description");
                var text = new TextField { multiline = true };
                text.AddToClassList("inv-desc");
                text.BindProperty(description);
                VisualElement row = VeneerStyle.FieldRow("Description", text, description.tooltip, out Label label);
                VeneerBinding.Hook(row, description, label, row, null);
                nb.Add(row);
            });

            VisualElement weightCard = nb.Card("Weight", "scale", () => nb.Number(itemPath + ".weight", "Weight"));
            nb.VisibleWhen(weightCard, typePath, (int)InventoryType.Weight);

            var preview = new InventoryCellPreview();
            VisualElement gridCard = nb.Card("Grid footprint", "grid", () =>
            {
                VisualElement row = nb.Row();
                using (nb.Into(row))
                {
                    nb.Add(preview);
                    var fields = new VisualElement();
                    fields.AddToClassList("inv-grid-fields");
                    nb.Add(fields);
                    using (nb.Into(fields))
                    {
                        VisualElement width = nb.Number(itemPath + ".size.x", "Width", "cells");
                        InventoryStudioHelpers.ClampInt(width, ctx.So.FindProperty(itemPath + ".size.x"), 1, InventorySettings.MaxGridSize);
                        VisualElement height = nb.Number(itemPath + ".size.y", "Height", "cells");
                        InventoryStudioHelpers.ClampInt(height, ctx.So.FindProperty(itemPath + ".size.y"), 1, InventorySettings.MaxGridSize);
                        nb.Chip(itemPath + ".canRotate", "Can rotate", "rotate");
                    }
                }

                nb.ConditionalHint(() =>
                {
                    ctx.So.UpdateIfRequiredOrScript();
                    return InventoryStudioHelpers.FitsGrid(ctx.So, group, item, out Vector2Int size, out int columns, out int rows)
                        ? null
                        : size.x + " x " + size.y + " cells is too big for the " + columns + " x " + rows + " grid in any rotation.";
                }, "warn");
            });
            nb.VisibleWhen(gridCard, typePath, (int)InventoryType.Grid);

            Action updatePreview = () =>
            {
                ctx.So.UpdateIfRequiredOrScript();
                bool fits = InventoryStudioHelpers.FitsGrid(ctx.So, group, item, out Vector2Int size, out int columns, out int rows);
                preview.Set(size, fits, fits
                    ? "How the item sits in the grid: " + size.x + " x " + size.y + " cells."
                    : size.x + " x " + size.y + " cells: too big for the grid in any rotation.");
            };
            updatePreview();
            InventoryStudioHelpers.Tracker(holder, ctx.So, updatePreview);

            nb.Finish();
            return holder;
        }
    }

    /// <summary>One row of the item list: icon slot (drop a sprite on it), name, key numbers, a warning mark and a remove button.</summary>
    internal sealed class InventoryItemRow : VisualElement
    {
        private readonly InventoryItemsPane pane;
        private readonly VisualElement thumb;
        private readonly VisualElement placeholder;
        private readonly VisualElement warning;
        private readonly TextField nameField;
        private readonly Label summary;
        private int index = -1;

        public InventoryItemRow(InventoryItemsPane pane)
        {
            this.pane = pane;
            AddToClassList("inv-row");

            thumb = new VisualElement();
            thumb.AddToClassList("inv-thumb");
            thumb.tooltip = "Icon. Drop a sprite here to set it.";
            placeholder = VeneerStyle.IconElement("image", "vn-icon--sm", pane.Brand);
            thumb.Add(placeholder);
            InventoryStudioHelpers.EnableSpriteDrop(thumb, GetIconProperty);
            Add(thumb);

            var text = new VisualElement();
            text.AddToClassList("inv-row__text");
            nameField = new TextField();
            nameField.AddToClassList("inv-row__name");
            nameField.textEdition.placeholder = "Item name";
            nameField.RegisterCallback<FocusInEvent>(evt => pane.SelectItem(index));
            text.Add(nameField);
            summary = new Label();
            summary.AddToClassList("inv-row__summary");
            summary.pickingMode = PickingMode.Ignore;
            text.Add(summary);
            Add(text);

            warning = VeneerStyle.IconElement("warning", "vn-icon--warn", pane.Brand);
            VeneerStyle.SetDisplay(warning, false);
            Add(warning);

            Add(VeneerStyle.IconButton("trash", "Remove this item (Undo brings it back).", () => pane.RemoveItem(index), pane.Brand));
        }

        /// <summary>Points the row at item <paramref name="itemIndex"/> and binds its name field.</summary>
        public void Bind(int itemIndex)
        {
            index = itemIndex;
            SerializedProperty items = pane.ItemsProp;
            if (items == null || itemIndex < 0 || itemIndex >= items.arraySize)
            {
                return;
            }

            nameField.Unbind();
            nameField.BindProperty(items.GetArrayElementAtIndex(itemIndex).FindPropertyRelative("displayName"));
            Refresh();
        }

        /// <summary>Lets go of the item when the list recycles the row.</summary>
        public void Unbind()
        {
            nameField.Unbind();
            index = -1;
        }

        /// <summary>Redraws the parts that follow the data (icon, numbers, warning); the name field binds itself.</summary>
        public void Refresh()
        {
            SerializedProperty items = pane.ItemsProp;
            if (items == null || index < 0 || index >= items.arraySize)
            {
                return;
            }

            SerializedProperty element = items.GetArrayElementAtIndex(index);
            InventoryStudioHelpers.SetSprite(thumb, placeholder, element.FindPropertyRelative("icon").objectReferenceValue as Sprite);
            summary.text = pane.Describe(element);
            string issue = pane.IssueFor(index);
            VeneerStyle.SetDisplay(warning, issue != null);
            warning.tooltip = issue;
        }

        private SerializedProperty GetIconProperty()
        {
            SerializedProperty items = pane.ItemsProp;
            if (items == null || index < 0 || index >= items.arraySize)
            {
                return null;
            }

            return items.GetArrayElementAtIndex(index).FindPropertyRelative("icon");
        }
    }
}
#endif

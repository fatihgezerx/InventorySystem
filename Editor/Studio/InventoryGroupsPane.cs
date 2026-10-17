#if HAS_VENEER
using System;
using Veneer;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace InventorySystem
{
    /// <summary>
    /// The Groups tab of the InventoryData Inspector: the groups as reorderable rows (drag a row to reorder, name, capacity pill, item
    /// count, remove) and, under the list, the capacity settings of the selected group - its inventory type and only the numbers that
    /// type uses, with a plain-words explanation of the mode.
    /// </summary>
    internal sealed class InventoryGroupsPane
    {
        private const float RowHeight = 40f;
        private const string SessionPrefix = "InventorySystem.InventoryData.Group";

        private readonly InventoryStudioContext ctx;
        private readonly VisualElement root = new VisualElement();
        private readonly ListView list;
        private readonly VisualElement details = new VisualElement();
        private readonly Label countPill;
        private SerializedProperty groupsProp;
        private string idKey;
        private int selectedGroupId;

        public InventoryGroupsPane(InventoryStudioContext ctx)
        {
            this.ctx = ctx;
            groupsProp = ctx.So.FindProperty("groups");

            var block = new VisualElement();
            block.AddToClassList("vn-list");
            var header = new VisualElement();
            header.AddToClassList("vn-list__header");
            var title = new Label("Groups");
            title.AddToClassList("vn-list__title");
            header.Add(title);
            countPill = VeneerStyle.Pill("0", "info");
            header.Add(countPill);
            header.Add(VeneerStyle.Spacer());
            header.Add(VeneerStyle.Button("Add group", "plus", "Adds an empty group at the end and selects it.", AddGroup, false, ctx.Brand));
            block.Add(header);

            list = new ListView
            {
                bindingPath = groupsProp.propertyPath,
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
            list.makeItem = () => new InventoryGroupRow(this);
            list.bindItem = (element, index) => ((InventoryGroupRow)element).Bind(index);
            list.unbindItem = (element, index) => ((InventoryGroupRow)element).Unbind();
            list.selectedIndicesChanged += indices => OnSelectionChanged();
            block.Add(list);
            root.Add(block);
            root.Add(details);
            list.Bind(ctx.So);
        }

        /// <summary>The element to put into the tab page.</summary>
        public VisualElement Root => root;

        internal SerializedProperty GroupsProp => groupsProp;

        internal VeneerBrand Brand => ctx.Brand;

        /// <summary>Brings the pane in step with the data. The first call selects the first group.</summary>
        public void OnDataChanged()
        {
            groupsProp = ctx.So.FindProperty("groups");
            string key = InventoryStudioHelpers.GroupIdKey(groupsProp);
            if (!string.Equals(key, idKey, StringComparison.Ordinal))
            {
                bool first = idKey == null;
                idKey = key;
                list.RefreshItems();
                Reselect(first);
            }

            list.Query<InventoryGroupRow>().ForEach(RefreshRow);
            VeneerStyle.SetPill(countPill, "info", groupsProp.arraySize.ToString());
        }

        /// <summary>Adds an empty group at the end, selects it and scrolls to it.</summary>
        public void AddGroup()
        {
            groupsProp.arraySize++;
            int last = groupsProp.arraySize - 1;
            InventoryStudioHelpers.ResetGroup(groupsProp.GetArrayElementAtIndex(last));
            ctx.So.ApplyModifiedProperties();
            list.RefreshItems();
            list.SetSelection(last);
            list.ScrollToItem(last);
        }

        /// <summary>Selects group <paramref name="index"/> and scrolls to it.</summary>
        public void Show(int index)
        {
            if (index >= 0 && index < groupsProp.arraySize)
            {
                list.SetSelection(index);
                list.ScrollToItem(index);
            }
        }

        internal void SelectGroup(int index)
        {
            if (index >= 0 && index < groupsProp.arraySize && list.selectedIndex != index)
            {
                list.SetSelection(index);
            }
        }

        internal void RemoveGroup(int index)
        {
            if (index < 0 || index >= groupsProp.arraySize)
            {
                return;
            }

            groupsProp.DeleteArrayElementAtIndex(index);
            ctx.So.ApplyModifiedProperties();
            list.RefreshItems();
        }

        internal int ItemCount(int index)
        {
            return groupsProp.GetArrayElementAtIndex(index).FindPropertyRelative("items").arraySize;
        }

        private static void RefreshRow(InventoryGroupRow row)
        {
            row.Refresh();
        }

        private int IdAt(int index)
        {
            return groupsProp.GetArrayElementAtIndex(index).FindPropertyRelative("id").intValue;
        }

        private void Reselect(bool first)
        {
            int index = -1;
            if (!first && selectedGroupId > 0)
            {
                for (int i = 0; i < groupsProp.arraySize; i++)
                {
                    if (IdAt(i) == selectedGroupId)
                    {
                        index = i;
                        break;
                    }
                }
            }

            if (index < 0)
            {
                index = first ? 0 : list.selectedIndex;
                index = Mathf.Min(index, groupsProp.arraySize - 1);
            }

            if (index >= 0)
            {
                list.SetSelectionWithoutNotify(new[] { index });
            }
            else
            {
                list.ClearSelection();
            }

            selectedGroupId = index >= 0 ? IdAt(index) : 0;
            ShowDetails();
        }

        private void OnSelectionChanged()
        {
            int index = list.selectedIndex;
            selectedGroupId = index >= 0 && index < groupsProp.arraySize ? IdAt(index) : 0;
            ShowDetails();
        }

        private void ShowDetails()
        {
            details.Clear();
            int index = list.selectedIndex;
            if (index < 0 || index >= groupsProp.arraySize)
            {
                details.Add(VeneerStyle.Hint("Add a group, or select one, to edit what its inventory can hold.", "info", ctx.Brand));
                return;
            }

            details.Add(BuildDetails(index));
        }

        private VisualElement BuildDetails(int group)
        {
            var holder = new VisualElement();
            var nb = new VeneerInspectorBuilder(ctx.So, holder, ctx.Brand, SessionPrefix);
            string settingsPath = "groups.Array.data[" + group + "].settings";
            string typePath = settingsPath + ".inventoryType";
            var cellsLabel = VeneerStyle.MutedLabel(string.Empty);

            nb.Card("Capacity of this group", "database", () =>
            {
                nb.Enum(typePath, InventoryStudioHelpers.TypeIcons, InventoryStudioHelpers.TypeNames, "Type");
                nb.VisibleWhen(nb.Number(settingsPath + ".maxWeight", "Max weight"), typePath, (int)InventoryType.Weight);
                nb.VisibleWhen(nb.Number(settingsPath + ".maxSlots", "Max slots", "slots"), typePath, (int)InventoryType.MaxSlot);
                nb.VisibleWhen(nb.Number(settingsPath + ".columns", "Columns", "cells"), typePath, (int)InventoryType.Grid);
                nb.VisibleWhen(nb.Number(settingsPath + ".rows", "Rows", "cells"), typePath, (int)InventoryType.Grid);
                VisualElement cells = VeneerStyle.FieldRow("Total cells", cellsLabel, null, out Label unused);
                nb.Add(cells);
                nb.VisibleWhen(cells, typePath, (int)InventoryType.Grid);

                nb.VisibleWhen(nb.Hint("No limit: slots are added as long as items keep coming.", "info"), typePath, (int)InventoryType.Unlimited);
                nb.VisibleWhen(nb.Hint("Unlimited slots, but the total weight can't go over Max Weight. Every item gets a Weight field.", "info"),
                    typePath, (int)InventoryType.Weight);
                nb.VisibleWhen(nb.Hint("A fixed number of slots. Stackable items fill their stacks up to Max Stack before taking a new slot; " +
                                       "everything else takes a new slot every time.", "info"), typePath, (int)InventoryType.MaxSlot);
                nb.VisibleWhen(nb.Hint("A Columns x Rows grid, like an attache case. Every item covers a Width x Height rectangle of cells " +
                                       "and, with Can Rotate, may be turned 90 degrees to fit. Every item gets Size and Can Rotate fields.", "info"),
                    typePath, (int)InventoryType.Grid);
            });

            Action updateCells = () =>
            {
                ctx.So.UpdateIfRequiredOrScript();
                SerializedProperty columns = ctx.So.FindProperty(settingsPath + ".columns");
                SerializedProperty rows = ctx.So.FindProperty(settingsPath + ".rows");
                if (columns != null && rows != null)
                {
                    cellsLabel.text = (columns.intValue * rows.intValue).ToString();
                }
            };
            updateCells();
            InventoryStudioHelpers.Tracker(holder, ctx.So, updateCells);

            nb.Finish();
            return holder;
        }
    }

    /// <summary>One row of the group list: name, capacity pill, item count and a remove button.</summary>
    internal sealed class InventoryGroupRow : VisualElement
    {
        private readonly InventoryGroupsPane pane;
        private readonly TextField nameField;
        private readonly Label capacity;
        private readonly Label count;
        private int index = -1;

        public InventoryGroupRow(InventoryGroupsPane pane)
        {
            this.pane = pane;
            AddToClassList("inv-row");

            nameField = new TextField();
            nameField.AddToClassList("inv-row__name");
            nameField.textEdition.placeholder = "Group name";
            nameField.RegisterCallback<FocusInEvent>(evt => pane.SelectGroup(index));
            Add(nameField);

            capacity = VeneerStyle.Pill(string.Empty, "info");
            Add(capacity);
            count = new Label();
            count.AddToClassList("inv-row__count");
            count.pickingMode = PickingMode.Ignore;
            Add(count);
            Add(VeneerStyle.IconButton("trash", "Remove this group and its items (Undo brings it back).", () => pane.RemoveGroup(index), pane.Brand));
        }

        /// <summary>Points the row at group <paramref name="groupIndex"/> and binds its name field.</summary>
        public void Bind(int groupIndex)
        {
            index = groupIndex;
            SerializedProperty groups = pane.GroupsProp;
            if (groups == null || groupIndex < 0 || groupIndex >= groups.arraySize)
            {
                return;
            }

            nameField.Unbind();
            nameField.BindProperty(groups.GetArrayElementAtIndex(groupIndex).FindPropertyRelative("header"));
            Refresh();
        }

        /// <summary>Lets go of the group when the list recycles the row.</summary>
        public void Unbind()
        {
            nameField.Unbind();
            index = -1;
        }

        /// <summary>Redraws the capacity pill and the item count.</summary>
        public void Refresh()
        {
            SerializedProperty groups = pane.GroupsProp;
            if (groups == null || index < 0 || index >= groups.arraySize)
            {
                return;
            }

            SerializedProperty group = groups.GetArrayElementAtIndex(index);
            VeneerStyle.SetPill(capacity, "info", InventoryStudioHelpers.DescribeCapacity(group.FindPropertyRelative("settings")));
            int items = group.FindPropertyRelative("items").arraySize;
            count.text = items == 1 ? "1 item" : items + " items";
        }
    }
}
#endif

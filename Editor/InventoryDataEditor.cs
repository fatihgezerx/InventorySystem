using System.Collections.Generic;
using UnityEditor;
using UnityEditorInternal;
using UnityEngine;

namespace InventorySystem
{
    /// <summary>
    /// Custom Inspector for <see cref="InventoryData"/>: an INVENTORY SETTINGS box showing only the
    /// capacity fields of the selected <see cref="InventoryType"/>, an ITEMS box of named groups (reordered
    /// by dragging their handle), and a "Compile" button that hands off to <see cref="InventoryCompiler"/>.
    /// </summary>
    [CustomEditor(typeof(InventoryData))]
    internal sealed class InventoryDataEditor : Editor
    {
        private const float HeaderHeight = 28f;
        private const float GroupHeaderHeight = 22f;

        private const string ItemsInfo =
            "Group names only organize your items in this Inspector (e.g. \"Weapons\", \"Consumables\"). " +
            "Each item's Name becomes its ItemTypes member (\"Health Potion\" -> ItemTypes.HealthPotion), so " +
            "names must be unique. Compile also adds a Collectible to every item's prefab. Press Compile after any change.";

        // Same green as PoolData's and InteractData's Compile buttons.
        private static readonly Color CompileButtonColor = new(0.4f, 0.75f, 0.4f);

        private SerializedProperty _settings;
        private SerializedProperty _groups;
        private readonly List<ReorderableList> _groupLists = new();
        private readonly GroupDragReorder _groupReorder = new();
        private GUIStyle _headerStyle;
        private GUIStyle _groupHeaderStyle;
        private GUIStyle _hintStyle;

        private GUIStyle HeaderStyle => _headerStyle ??= new GUIStyle(EditorStyles.boldLabel)
        {
            fontSize = 20,
            fixedHeight = HeaderHeight
        };

        private GUIStyle GroupHeaderStyle => _groupHeaderStyle ??= new GUIStyle(EditorStyles.textField)
        {
            fontSize = 14,
            fontStyle = FontStyle.Bold,
            fixedHeight = GroupHeaderHeight,
            alignment = TextAnchor.MiddleLeft
        };

        private GUIStyle HintStyle => _hintStyle ??= new GUIStyle(EditorStyles.label)
        {
            fontStyle = FontStyle.Italic,
            alignment = TextAnchor.MiddleLeft,
            padding = new RectOffset(6, 0, 0, 0),
            normal = { textColor = new Color(0.5f, 0.5f, 0.5f) }
        };

        private void OnEnable()
        {
            _settings = serializedObject.FindProperty("settings");
            _groups = serializedObject.FindProperty("groups");
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            DrawInventorySettings();
            EditorGUILayout.Space(10);
            DrawItems();

            serializedObject.ApplyModifiedProperties();

            EditorGUILayout.Space(14);

            var buttonColor = GUI.backgroundColor;
            GUI.backgroundColor = CompileButtonColor;
            if (GUILayout.Button("Compile", GUILayout.Height(34)))
            {
                InventoryCompiler.Compile((InventoryData)target);
            }
            GUI.backgroundColor = buttonColor;
        }

        private void DrawInventorySettings()
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField("INVENTORY SETTINGS", HeaderStyle, GUILayout.Height(HeaderHeight));
            EditorGUILayout.Space(4);

            var type = _settings.FindPropertyRelative("inventoryType");
            EditorGUILayout.PropertyField(type);

            var inventoryType = (InventoryType)type.intValue;
            switch (inventoryType)
            {
                case InventoryType.Weight:
                    EditorGUILayout.PropertyField(_settings.FindPropertyRelative("maxWeight"));
                    break;

                case InventoryType.MaxSlot:
                    EditorGUILayout.PropertyField(_settings.FindPropertyRelative("maxSlots"));
                    break;

                case InventoryType.Grid:
                    var columns = _settings.FindPropertyRelative("columns");
                    var rows = _settings.FindPropertyRelative("rows");
                    EditorGUILayout.PropertyField(columns);
                    EditorGUILayout.PropertyField(rows);
                    using (new EditorGUI.DisabledScope(true))
                    {
                        EditorGUILayout.IntField("Max Slots", columns.intValue * rows.intValue);
                    }
                    break;
            }

            EditorGUILayout.HelpBox(GetModeInfo(inventoryType), MessageType.Info);
            EditorGUILayout.EndVertical();
        }

        private static string GetModeInfo(InventoryType type) => type switch
        {
            InventoryType.Weight =>
                "Unlimited slots, but the total weight can't go over Max Weight. Every item gets a Weight field.",
            InventoryType.MaxSlot =>
                "A fixed number of slots. Stackable items fill their stacks up to Max Stack before taking a new slot; " +
                "everything else takes a new slot every time.",
            InventoryType.Grid =>
                "A Columns x Rows grid, like an attaché case. Every item covers a Width x Height rectangle of cells " +
                "and, with Can Rotate, may be turned 90 degrees to fit. Every item gets Size and Can Rotate fields.",
            _ => "No limit: slots are added as long as items keep coming."
        };

        private void DrawItems()
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField("ITEMS", HeaderStyle, GUILayout.Height(HeaderHeight));
            EditorGUILayout.HelpBox(ItemsInfo, MessageType.Info);
            EditorGUILayout.Space(6);

            // Lists address their group by index, so rebuild them whenever groups are added or removed.
            if (_groupLists.Count != _groups.arraySize)
            {
                _groupLists.Clear();
                for (var i = 0; i < _groups.arraySize; i++)
                {
                    _groupLists.Add(CreateItemList(_groups.GetArrayElementAtIndex(i).FindPropertyRelative("items")));
                }
            }

            var groupPendingRemoval = -1;
            _groupReorder.Begin();
            for (var i = 0; i < _groups.arraySize; i++)
            {
                if (DrawGroup(i))
                {
                    groupPendingRemoval = i;
                }

                _groupReorder.RecordGroupRect();
                EditorGUILayout.Space(6);
            }

            if (_groupReorder.End(out var from, out var to))
            {
                _groups.MoveArrayElement(from, to);
                _groupLists.Clear();
            }
            else if (groupPendingRemoval >= 0)
            {
                _groups.DeleteArrayElementAtIndex(groupPendingRemoval);
                _groupLists.Clear();
            }

            if (GUILayout.Button("+ Add Group", GUILayout.Height(28)))
            {
                // A new array element copies the last one, so clear it.
                _groups.arraySize++;
                var group = _groups.GetArrayElementAtIndex(_groups.arraySize - 1);
                group.FindPropertyRelative("header").stringValue = "New Group";
                group.FindPropertyRelative("items").arraySize = 0;
                _groupLists.Clear();
            }

            EditorGUILayout.EndVertical();
        }

        /// <summary>Draws one group; returns true if its remove button was clicked.</summary>
        private bool DrawGroup(int index)
        {
            var header = _groups.GetArrayElementAtIndex(index).FindPropertyRelative("header");

            EditorGUILayout.BeginVertical(EditorStyles.helpBox);

            EditorGUILayout.BeginHorizontal();
            _groupReorder.DrawHandle(index, GroupHeaderHeight);
            var headerRect = EditorGUILayout.GetControlRect(GUILayout.Height(GroupHeaderHeight));
            header.stringValue = EditorGUI.TextField(headerRect, header.stringValue, GroupHeaderStyle);
            if (string.IsNullOrEmpty(header.stringValue))
            {
                EditorGUI.LabelField(headerRect, "Group name", HintStyle);
            }

            var remove = GUILayout.Button("✕", GUILayout.Width(GroupHeaderHeight), GUILayout.Height(GroupHeaderHeight));
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space(4);
            _groupLists[index].DoLayoutList();

            EditorGUILayout.EndVertical();
            return remove;
        }

        private ReorderableList CreateItemList(SerializedProperty items)
        {
            return new ReorderableList(serializedObject, items, true, false, true, true)
            {
                elementHeightCallback = index =>
                    EditorGUI.GetPropertyHeight(items.GetArrayElementAtIndex(index)) + 8f,
                drawElementCallback = (rect, index, isActive, isFocused) =>
                {
                    rect.y += 4f;
                    rect.height -= 8f;
                    EditorGUI.PropertyField(rect, items.GetArrayElementAtIndex(index), GUIContent.none);
                },
                onAddCallback = list =>
                {
                    // A new array element copies the last one - start from a blank item instead. Its id
                    // is left at 0 so InventoryData.OnValidate hands it a fresh one.
                    items.arraySize++;
                    var item = items.GetArrayElementAtIndex(items.arraySize - 1);
                    item.FindPropertyRelative("id").intValue = 0;
                    item.FindPropertyRelative("displayName").stringValue = string.Empty;
                    item.FindPropertyRelative("icon").objectReferenceValue = null;
                    item.FindPropertyRelative("prefab").objectReferenceValue = null;
                    item.FindPropertyRelative("stackable").boolValue = false;
                    item.FindPropertyRelative("maxStack").intValue = 10;
                    item.FindPropertyRelative("description").stringValue = string.Empty;
                    item.FindPropertyRelative("weight").floatValue = 1f;
                    item.FindPropertyRelative("size").vector2IntValue = Vector2Int.one;
                    item.FindPropertyRelative("canRotate").boolValue = true;
                    list.index = items.arraySize - 1;
                }
            };
        }
    }

    /// <summary>
    /// Draws an <see cref="ItemDefinition"/> as a 50x50 icon on the left and, on the right: Name, Prefab,
    /// Stackable with its Max Stack slider, a two-line Description, plus the one extra line the inventory's
    /// mode uses - Weight, or Size and Can Rotate (with a cell preview under the icon, red if it can't fit the grid).
    /// </summary>
    [CustomPropertyDrawer(typeof(ItemDefinition))]
    internal sealed class ItemDefinitionDrawer : PropertyDrawer
    {
        private const float IconSize = 50f;
        private const float IconGap = 8f;
        private const float LabelWidth = 76f;
        private const int DescriptionLines = 2;
        private const float ToggleWidth = 88f;
        private const float PreviewGap = 2f;
        private const float PreviewLabelHeight = 14f;
        private const float PreviewPadding = 4f;
        private const float MaxPreviewCell = 12f;

        private static GUIStyle _descriptionStyle;

        private static GUIStyle DescriptionStyle => _descriptionStyle ??= new GUIStyle(EditorStyles.textArea) { wordWrap = true };

        private static GUIStyle _centeredLabel;
        private static GUIStyle _previewLabelStyle;

        private static GUIStyle PreviewLabelStyle => _previewLabelStyle ??= new GUIStyle(EditorStyles.miniLabel)
        {
            alignment = TextAnchor.LowerCenter,
            padding = new RectOffset(0, 0, 0, 1)
        };

        private static GUIStyle CenteredLabel => _centeredLabel ??= new GUIStyle(EditorStyles.label) { alignment = TextAnchor.MiddleCenter };

        private static Color ShapeCellColor =>
            EditorGUIUtility.isProSkin ? new Color(0.35f, 0.6f, 0.95f) : new Color(0.2f, 0.45f, 0.85f);

        private static readonly Color TooBigColor = new(0.9f, 0.3f, 0.3f);

        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            var lines = 3 + DescriptionLines + (HasModeLine(property) ? 1 : 0);
            var height = EditorGUIUtility.singleLineHeight * lines + EditorGUIUtility.standardVerticalSpacing * (lines - 1);

            // Grid items show their size preview under the icon, as big as the icon.
            var left = GetInventoryType(property) == InventoryType.Grid ? IconSize * 2f + PreviewGap + PreviewLabelHeight : IconSize;
            return Mathf.Max(left, height);
        }

        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            var icon = property.FindPropertyRelative("icon");
            var iconRect = new Rect(position.x, position.y, IconSize, IconSize);
            icon.objectReferenceValue = EditorGUI.ObjectField(iconRect, icon.objectReferenceValue, typeof(Sprite), false);

            var fields = new Rect(position.x + IconSize + IconGap, position.y, position.width - IconSize - IconGap, position.height);

            var previousLabelWidth = EditorGUIUtility.labelWidth;
            EditorGUIUtility.labelWidth = LabelWidth;

            var nameProperty = property.FindPropertyRelative("displayName");
            EditorGUI.PropertyField(LineRect(fields, 0), nameProperty, new GUIContent("Name", nameProperty.tooltip));

            var prefab = property.FindPropertyRelative("prefab");
            EditorGUI.PropertyField(LineRect(fields, 1), prefab, new GUIContent("Prefab", prefab.tooltip));

            DrawStackLine(LineRect(fields, 2), property);

            var description = property.FindPropertyRelative("description");
            var descriptionRect = EditorGUI.PrefixLabel(LineRect(fields, 3, DescriptionLines), new GUIContent("Description", description.tooltip));
            description.stringValue = EditorGUI.TextArea(descriptionRect, description.stringValue, DescriptionStyle);

            const int modeLine = 3 + DescriptionLines;
            switch (GetInventoryType(property))
            {
                case InventoryType.Weight:
                    var weight = property.FindPropertyRelative("weight");
                    EditorGUI.PropertyField(LineRect(fields, modeLine), weight, new GUIContent("Weight", weight.tooltip));
                    break;

                case InventoryType.Grid:
                    var sizeLine = LineRect(fields, modeLine);
                    DrawShapeLine(sizeLine, property);

                    // As big as the icon, its bottom level with the Size line, titled "Preview".
                    var previewRect = new Rect(position.x, sizeLine.yMax - IconSize, IconSize, IconSize);
                    EditorGUI.LabelField(new Rect(position.x, previewRect.y - PreviewLabelHeight, IconSize, PreviewLabelHeight),
                        "Preview", PreviewLabelStyle);
                    DrawSizePreview(previewRect, property);
                    break;
            }

            EditorGUIUtility.labelWidth = previousLabelWidth;
        }

        private static void DrawStackLine(Rect rect, SerializedProperty property)
        {
            var stackable = property.FindPropertyRelative("stackable");
            var maxStack = property.FindPropertyRelative("maxStack");

            var toggleRect = new Rect(rect.x, rect.y, ToggleWidth, rect.height);
            var sliderRect = new Rect(toggleRect.xMax + 6f, rect.y, rect.width - ToggleWidth - 6f, rect.height);

            EditorGUI.PropertyField(toggleRect, stackable, new GUIContent("Stackable", stackable.tooltip));
            using (new EditorGUI.DisabledScope(!stackable.boolValue))
            {
                EditorGUI.PropertyField(sliderRect, maxStack, new GUIContent("Max Stack", maxStack.tooltip));
            }
        }

        private static void DrawShapeLine(Rect rect, SerializedProperty property)
        {
            var size = property.FindPropertyRelative("size");
            var canRotate = property.FindPropertyRelative("canRotate");

            var toggleRect = new Rect(rect.xMax - ToggleWidth, rect.y, ToggleWidth, rect.height);
            var sizeRect = new Rect(rect.x, rect.y, toggleRect.x - rect.x - 8f, rect.height);

            // Width x Height, each at least 1 and at most the largest grid.
            var fieldRect = EditorGUI.PrefixLabel(sizeRect, new GUIContent("Size", size.tooltip));
            var half = (fieldRect.width - 14f) * 0.5f;
            var value = size.vector2IntValue;
            EditorGUI.BeginChangeCheck();
            var width = EditorGUI.IntField(new Rect(fieldRect.x, fieldRect.y, half, fieldRect.height), value.x);
            EditorGUI.LabelField(new Rect(fieldRect.x + half, fieldRect.y, 14f, fieldRect.height), "x", CenteredLabel);
            var height = EditorGUI.IntField(new Rect(fieldRect.xMax - half, fieldRect.y, half, fieldRect.height), value.y);
            if (EditorGUI.EndChangeCheck())
            {
                size.vector2IntValue = new Vector2Int(
                    Mathf.Clamp(width, 1, InventorySettings.MaxGridSize),
                    Mathf.Clamp(height, 1, InventorySettings.MaxGridSize));
            }

            EditorGUI.PropertyField(toggleRect, canRotate, new GUIContent("Can Rotate", canRotate.tooltip));
        }

        // Under the icon, in a frame like the icon's: the item's rectangle as grid cells, so it reads as a picture of
        // how the item sits in the grid. Red if it can't fit the grid in any rotation.
        private static void DrawSizePreview(Rect rect, SerializedProperty property)
        {
            var size = Vector2Int.Max(property.FindPropertyRelative("size").vector2IntValue, Vector2Int.one);
            var fits = FitsGrid(property, size, property.FindPropertyRelative("canRotate").boolValue);
            var tooltip = fits
                ? $"How the item sits in the grid: {size.x} x {size.y} cells."
                : $"{size.x} x {size.y} cells: too big for the grid in any rotation.";
            EditorGUI.LabelField(rect, new GUIContent(string.Empty, tooltip));

            if (Event.current.type != EventType.Repaint)
            {
                return;
            }

            // The same frame as the icon's empty sprite field above it.
            EditorStyles.objectFieldThumb.Draw(rect, GUIContent.none, false, false, false, false);
            var inner = new Rect(rect.x + 1f, rect.y + 1f, rect.width - 2f, rect.height - 2f);

            // Cells stay small enough to read as cells, even for a 1 x 1 item.
            var area = new Rect(inner.x + PreviewPadding, inner.y + PreviewPadding,
                inner.width - PreviewPadding * 2f, inner.height - PreviewPadding * 2f);
            var cell = Mathf.Clamp(Mathf.Floor(Mathf.Min(area.width / size.x, area.height / size.y)), 1f, MaxPreviewCell);
            var gap = cell >= 4f ? 1f : 0f;
            var originX = Mathf.Round(area.x + (area.width - size.x * cell) * 0.5f);
            var originY = Mathf.Round(area.y + (area.height - size.y * cell) * 0.5f);
            var color = fits ? ShapeCellColor : TooBigColor;

            for (var y = 0; y < size.y; y++)
            {
                for (var x = 0; x < size.x; x++)
                {
                    EditorGUI.DrawRect(new Rect(originX + x * cell, originY + y * cell, cell - gap, cell - gap), color);
                }
            }
        }

        // Whether the item fits the InventoryData's grid as set or, with Can Rotate, turned.
        private static bool FitsGrid(SerializedProperty property, Vector2Int size, bool canRotate)
        {
            var columns = property.serializedObject.FindProperty("settings.columns");
            var rows = property.serializedObject.FindProperty("settings.rows");
            if (columns == null || rows == null)
            {
                return true;
            }

            return (size.x <= columns.intValue && size.y <= rows.intValue)
                   || (canRotate && size.y <= columns.intValue && size.x <= rows.intValue);
        }

        private static bool HasModeLine(SerializedProperty property)
        {
            var type = GetInventoryType(property);
            return type == InventoryType.Weight || type == InventoryType.Grid;
        }

        // Reads the mode from the InventoryData the item belongs to; items drawn anywhere else get no mode line.
        private static InventoryType GetInventoryType(SerializedProperty property)
        {
            var type = property.serializedObject.FindProperty("settings.inventoryType");
            return type != null ? (InventoryType)type.intValue : InventoryType.Unlimited;
        }

        // The rect of line <index>, spanning <count> lines.
        private static Rect LineRect(Rect area, int index, int count = 1)
        {
            var line = EditorGUIUtility.singleLineHeight;
            var spacing = EditorGUIUtility.standardVerticalSpacing;
            return new Rect(area.x, area.y + index * (line + spacing), area.width, count * line + (count - 1) * spacing);
        }
    }
}

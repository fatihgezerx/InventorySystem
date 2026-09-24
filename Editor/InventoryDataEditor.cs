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
                "A Columns x Rows grid. Every item covers the cells of its Shape and, with Can Rotate, may be turned " +
                "90 degrees to fit. Every item gets Shape and Can Rotate fields.",
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
                    item.FindPropertyRelative("weight").floatValue = 1f;
                    item.FindPropertyRelative("shape").intValue = (int)ItemShape.Square1x1;
                    item.FindPropertyRelative("canRotate").boolValue = true;
                    list.index = items.arraySize - 1;
                }
            };
        }
    }

    /// <summary>
    /// Draws an <see cref="ItemDefinition"/> as a 50x50 icon on the left and, on the right: Name, Prefab,
    /// Stackable with its Max Stack slider, plus the one extra line the inventory's mode uses - Weight, or
    /// Shape (with a small cell preview) and Can Rotate.
    /// </summary>
    [CustomPropertyDrawer(typeof(ItemDefinition))]
    internal sealed class ItemDefinitionDrawer : PropertyDrawer
    {
        private const float IconSize = 50f;
        private const float IconGap = 8f;
        private const float LabelWidth = 64f;
        private const float ToggleWidth = 88f;
        private const float ShapePreviewSize = 18f;

        private static Color ShapeCellColor =>
            EditorGUIUtility.isProSkin ? new Color(0.35f, 0.6f, 0.95f) : new Color(0.2f, 0.45f, 0.85f);

        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            var lines = HasModeLine(property) ? 4 : 3;
            var height = EditorGUIUtility.singleLineHeight * lines + EditorGUIUtility.standardVerticalSpacing * (lines - 1);
            return Mathf.Max(IconSize, height);
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

            switch (GetInventoryType(property))
            {
                case InventoryType.Weight:
                    var weight = property.FindPropertyRelative("weight");
                    EditorGUI.PropertyField(LineRect(fields, 3), weight, new GUIContent("Weight", weight.tooltip));
                    break;

                case InventoryType.Grid:
                    DrawShapeLine(LineRect(fields, 3), property);
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
            var shape = property.FindPropertyRelative("shape");
            var canRotate = property.FindPropertyRelative("canRotate");

            var toggleRect = new Rect(rect.xMax - ToggleWidth, rect.y, ToggleWidth, rect.height);
            var previewRect = new Rect(toggleRect.x - ShapePreviewSize - 8f, rect.y, ShapePreviewSize, ShapePreviewSize);
            var shapeRect = new Rect(rect.x, rect.y, previewRect.x - rect.x - 6f, rect.height);

            EditorGUI.PropertyField(shapeRect, shape, new GUIContent("Shape", shape.tooltip));
            DrawShapePreview(previewRect, (ItemShape)shape.intValue);
            EditorGUI.PropertyField(toggleRect, canRotate, new GUIContent("Can Rotate", canRotate.tooltip));
        }

        private static void DrawShapePreview(Rect rect, ItemShape shape)
        {
            if (Event.current.type != EventType.Repaint)
            {
                return;
            }

            // Every shape fits in 3x3 cells.
            var cell = Mathf.Floor(Mathf.Min(rect.width, rect.height) / 3f);
            var size = ItemShapes.GetSize(shape, 0);
            var originX = rect.x + (rect.width - size.x * cell) * 0.5f;
            var originY = rect.y + (rect.height - size.y * cell) * 0.5f;

            foreach (var offset in ItemShapes.GetCells(shape, 0))
            {
                EditorGUI.DrawRect(new Rect(originX + offset.x * cell, originY + offset.y * cell, cell - 1f, cell - 1f), ShapeCellColor);
            }
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

        private static Rect LineRect(Rect area, int index)
        {
            var line = EditorGUIUtility.singleLineHeight;
            return new Rect(area.x, area.y + index * (line + EditorGUIUtility.standardVerticalSpacing), area.width, line);
        }
    }
}

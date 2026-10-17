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
    /// <summary>What the panes of the InventoryData Inspector share: the serialized object, the brand and the validation report.</summary>
    internal sealed class InventoryStudioContext
    {
        public InventoryStudioContext(SerializedObject so, VeneerBrand brand, Func<InventoryDataCheck.Report> getReport)
        {
            So = so;
            Brand = brand;
            GetReport = getReport;
        }

        /// <summary>The serialized InventoryData.</summary>
        public SerializedObject So { get; }

        /// <summary>The Inventory System brand.</summary>
        public VeneerBrand Brand { get; }

        /// <summary>Returns the current validation report (cached until the data changes).</summary>
        public Func<InventoryDataCheck.Report> GetReport { get; }
    }

    /// <summary>Small helpers for the Inventory Inspectors: change tracking, the icon slot, blank new entries and one-line descriptions.</summary>
    internal static class InventoryStudioHelpers
    {
        /// <summary>Icon per <see cref="InventoryType"/>, in enum order.</summary>
        public static readonly string[] TypeIcons = { "list", "scale", "hash", "grid" };

        /// <summary>Name per <see cref="InventoryType"/>, in enum order.</summary>
        public static readonly string[] TypeNames = { "Unlimited", "Weight", "Max slots", "Grid" };

        /// <summary>
        /// Adds an invisible element under <paramref name="parent"/> that calls <paramref name="changed"/> whenever anything in
        /// <paramref name="so"/> changes (edits, Undo, Redo). It stops with the parent, so nothing leaks.
        /// </summary>
        public static void Tracker(VisualElement parent, SerializedObject so, Action changed)
        {
            var host = new VisualElement();
            host.AddToClassList("inv-tracker");
            host.pickingMode = PickingMode.Ignore;
            host.TrackSerializedObjectValue(so, changedObject => changed());
            parent.Add(host);
        }

        /// <summary>Shows <paramref name="sprite"/> in an icon slot, or its placeholder glyph when there is none.</summary>
        public static void SetSprite(VisualElement thumb, VisualElement placeholder, Sprite sprite)
        {
            if (sprite != null)
            {
                thumb.style.backgroundImage = new StyleBackground(sprite);
            }
            else
            {
                thumb.style.backgroundImage = StyleKeyword.None;
            }

            VeneerStyle.SetDisplay(placeholder, sprite == null);
        }

        /// <summary>Lets a sprite (or a texture asset that has one) be dropped on <paramref name="target"/> to fill the icon property.</summary>
        public static void EnableSpriteDrop(VisualElement target, Func<SerializedProperty> getIcon)
        {
            target.RegisterCallback<DragUpdatedEvent>(evt =>
            {
                if (FindDraggedSprite() == null)
                {
                    return;
                }

                DragAndDrop.visualMode = DragAndDropVisualMode.Copy;
                target.AddToClassList("inv-thumb--drop");
                evt.StopPropagation();
            });
            target.RegisterCallback<DragLeaveEvent>(evt => target.RemoveFromClassList("inv-thumb--drop"));
            target.RegisterCallback<DragExitedEvent>(evt => target.RemoveFromClassList("inv-thumb--drop"));
            target.RegisterCallback<DragPerformEvent>(evt =>
            {
                target.RemoveFromClassList("inv-thumb--drop");
                Sprite sprite = FindDraggedSprite();
                SerializedProperty icon = getIcon();
                if (sprite == null || icon == null)
                {
                    return;
                }

                DragAndDrop.AcceptDrag();
                icon.objectReferenceValue = sprite;
                icon.serializedObject.ApplyModifiedProperties();
                evt.StopPropagation();
            });
        }

        private static Sprite FindDraggedSprite()
        {
            UnityEngine.Object[] dragged = DragAndDrop.objectReferences;
            for (int i = 0; i < dragged.Length; i++)
            {
                if (dragged[i] is Sprite sprite)
                {
                    return sprite;
                }

                if (dragged[i] is Texture2D)
                {
                    string path = AssetDatabase.GetAssetPath(dragged[i]);
                    Sprite fromTexture = string.IsNullOrEmpty(path) ? null : AssetDatabase.LoadAssetAtPath<Sprite>(path);
                    if (fromTexture != null)
                    {
                        return fromTexture;
                    }
                }
            }

            return null;
        }

        /// <summary>Makes the integer field inside a kit number row refuse values outside [min, max] (the kit only knows [Min] / [Range] attributes).</summary>
        public static void ClampInt(VisualElement row, SerializedProperty property, int min, int max)
        {
            IntegerField field = row.Q<IntegerField>();
            if (field == null)
            {
                return;
            }

            SerializedProperty tracked = property.Copy();
            field.RegisterValueChangedCallback(evt =>
            {
                int clamped = Mathf.Clamp(evt.newValue, min, max);
                if (clamped != evt.newValue)
                {
                    tracked.intValue = clamped;
                    tracked.serializedObject.ApplyModifiedProperties();
                }
            });
        }

        /// <summary>Turns a freshly added (copied) item element into a blank item. Its id stays 0 so InventoryData.OnValidate hands it a fresh one.</summary>
        public static void ResetItem(SerializedProperty item)
        {
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
        }

        /// <summary>Turns a freshly added (copied) group element into a new empty group; the capacity settings of the copied one stay.</summary>
        public static void ResetGroup(SerializedProperty group)
        {
            group.FindPropertyRelative("id").intValue = 0;
            group.FindPropertyRelative("header").stringValue = "New Group";
            group.FindPropertyRelative("items").arraySize = 0;
        }

        /// <summary>The inventory type of group <paramref name="group"/>.</summary>
        public static InventoryType ReadType(SerializedObject so, int group)
        {
            SerializedProperty type = so.FindProperty("groups.Array.data[" + group + "].settings.inventoryType");
            return type == null ? InventoryType.Unlimited : (InventoryType)type.intValue;
        }

        /// <summary>"Grid 8 x 5", "20 slots", "Weight 50" or "Unlimited" for a group's settings property.</summary>
        public static string DescribeCapacity(SerializedProperty settings)
        {
            switch ((InventoryType)settings.FindPropertyRelative("inventoryType").intValue)
            {
                case InventoryType.Weight:
                    return "Weight " + settings.FindPropertyRelative("maxWeight").floatValue.ToString("0.##");
                case InventoryType.MaxSlot:
                    return settings.FindPropertyRelative("maxSlots").intValue + " slots";
                case InventoryType.Grid:
                    return "Grid " + settings.FindPropertyRelative("columns").intValue + " x " + settings.FindPropertyRelative("rows").intValue;
                default:
                    return "Unlimited";
            }
        }

        /// <summary>The key numbers of an item for its row: stack size and the one extra the inventory's mode uses.</summary>
        public static string DescribeItem(SerializedProperty item, InventoryType type)
        {
            var text = new StringBuilder(48);
            text.Append(item.FindPropertyRelative("stackable").boolValue
                ? "Stack of " + item.FindPropertyRelative("maxStack").intValue
                : "Single");

            if (type == InventoryType.Weight)
            {
                text.Append("  |  Weight ").Append(item.FindPropertyRelative("weight").floatValue.ToString("0.##"));
            }
            else if (type == InventoryType.Grid)
            {
                Vector2Int size = item.FindPropertyRelative("size").vector2IntValue;
                text.Append("  |  ").Append(Mathf.Max(1, size.x)).Append(" x ").Append(Mathf.Max(1, size.y)).Append(" cells");
                if (item.FindPropertyRelative("canRotate").boolValue && size.x != size.y)
                {
                    text.Append(", rotates");
                }
            }

            return text.ToString();
        }

        /// <summary>
        /// Whether an item fits its group's grid (as set or turned). Returns true for groups that are not grids; the size and the grid
        /// are returned for the message.
        /// </summary>
        public static bool FitsGrid(SerializedObject so, int group, int item, out Vector2Int size, out int columns, out int rows)
        {
            string groupPath = "groups.Array.data[" + group + "]";
            SerializedProperty element = so.FindProperty(groupPath + ".items.Array.data[" + item + "]");
            SerializedProperty type = so.FindProperty(groupPath + ".settings.inventoryType");
            size = Vector2Int.one;
            columns = 0;
            rows = 0;
            if (element == null || type == null)
            {
                return true;
            }

            size = Vector2Int.Max(element.FindPropertyRelative("size").vector2IntValue, Vector2Int.one);
            columns = so.FindProperty(groupPath + ".settings.columns").intValue;
            rows = so.FindProperty(groupPath + ".settings.rows").intValue;
            if ((InventoryType)type.intValue != InventoryType.Grid)
            {
                return true;
            }

            bool canRotate = element.FindPropertyRelative("canRotate").boolValue;
            return (size.x <= columns && size.y <= rows) || (canRotate && size.y <= columns && size.x <= rows);
        }

        /// <summary>A key that changes when groups are added, removed or reordered (their ids, in order).</summary>
        public static string GroupIdKey(SerializedProperty groups)
        {
            var key = new StringBuilder(32);
            for (int i = 0; i < groups.arraySize; i++)
            {
                key.Append(groups.GetArrayElementAtIndex(i).FindPropertyRelative("id").intValue).Append(';');
            }

            return key.ToString();
        }
    }

    /// <summary>
    /// The picture of an item's footprint in a grid inventory: its width x height as cells, blue-green while it fits the grid and red
    /// when it can't fit in any rotation. Drawn with one painter call per cell; redrawn only when the size or the verdict changes.
    /// </summary>
    internal sealed class InventoryCellPreview : VisualElement
    {
        private const float MaxCell = 12f;
        private const float Padding = 4f;

        private Vector2Int size = Vector2Int.one;
        private bool fits = true;

        public InventoryCellPreview()
        {
            AddToClassList("inv-cells");
            pickingMode = PickingMode.Position;
            generateVisualContent += Draw;
        }

        /// <summary>Sets the footprint to show and whether it fits.</summary>
        public void Set(Vector2Int newSize, bool newFits, string tooltipText)
        {
            tooltip = tooltipText;
            newSize = Vector2Int.Max(newSize, Vector2Int.one);
            if (newSize == size && newFits == fits)
            {
                return;
            }

            size = newSize;
            fits = newFits;
            MarkDirtyRepaint();
        }

        private void Draw(MeshGenerationContext context)
        {
            Rect area = contentRect;
            if (area.width < 2f || area.height < 2f)
            {
                return;
            }

            area = new Rect(area.x + Padding, area.y + Padding, area.width - Padding * 2f, area.height - Padding * 2f);
            float cell = Mathf.Clamp(Mathf.Floor(Mathf.Min(area.width / size.x, area.height / size.y)), 1f, MaxCell);
            float gap = cell >= 4f ? 1f : 0f;
            float originX = Mathf.Round(area.x + (area.width - size.x * cell) * 0.5f);
            float originY = Mathf.Round(area.y + (area.height - size.y * cell) * 0.5f);

            Painter2D painter = context.painter2D;
            painter.fillColor = fits ? new Color(0.19f, 0.64f, 0.42f, 1f) : new Color(0.9f, 0.3f, 0.3f, 1f);
            for (int y = 0; y < size.y; y++)
            {
                for (int x = 0; x < size.x; x++)
                {
                    float left = originX + x * cell;
                    float top = originY + y * cell;
                    float right = left + cell - gap;
                    float bottom = top + cell - gap;
                    painter.BeginPath();
                    painter.MoveTo(new Vector2(left, top));
                    painter.LineTo(new Vector2(right, top));
                    painter.LineTo(new Vector2(right, bottom));
                    painter.LineTo(new Vector2(left, bottom));
                    painter.ClosePath();
                    painter.Fill();
                }
            }
        }
    }
}
#endif

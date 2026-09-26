#if HAS_EASYUI && HAS_UNIMVC && HAS_INPUT_SYSTEM
using EasyUI;
using UnityEditor;
using UnityEngine;

namespace InventorySystem
{
    // The UI SETTINGS box of the InventoryData Inspector, shown while Easy UI, UniMVC and the Input System are in the
    // project: the Easy UI panel the inventory UI is built from, a checklist of what that panel has (read from its
    // elements' Inventory roles, so nothing is ticked by hand), how the UI behaves, and Build UI.
    internal sealed partial class InventoryDataEditor
    {
        private const string RolesInfo =
            "Pick the Easy UI panel the inventory UI is built from. In Easy UI, mark its elements with Inventory roles " +
            "(the Role menu at the top of an element's inspector): Build UI finds them by role and adds the inventory's views.";

        private const string MissingBuilderInfo =
            "Build UI needs the inventory's UI scripts in your MVC folder. They are added with UniMVC; if you removed " +
            "Panels/Editor/InventoryUIBinder, import Inventory System again.";

        private static readonly Color FoundColor = new(0.45f, 0.8f, 0.45f);
        private static readonly Color MissingColor = new(0.95f, 0.4f, 0.35f);
        private static readonly Color AbsentColor = new(0.55f, 0.55f, 0.55f);

        private GUIStyle _checkStyle;

        private GUIStyle CheckStyle => _checkStyle ??= new GUIStyle(EditorStyles.label) { richText = false };

        partial void DrawUISettings()
        {
            var ui = serializedObject.FindProperty("ui");
            var guid = ui.FindPropertyRelative("panelGuid");

            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField("UI SETTINGS", HeaderStyle, GUILayout.Height(HeaderHeight));
            EditorGUILayout.Space(4);

            var path = string.IsNullOrEmpty(guid.stringValue) ? null : AssetDatabase.GUIDToAssetPath(guid.stringValue);
            var panel = string.IsNullOrEmpty(path) ? null : AssetDatabase.LoadAssetAtPath<EasyUIPanel>(path);

            EditorGUI.BeginChangeCheck();
            panel = (EasyUIPanel)EditorGUILayout.ObjectField(new GUIContent("Panel", "The Easy UI panel the inventory UI is built from."),
                panel, typeof(EasyUIPanel), false);
            if (EditorGUI.EndChangeCheck())
            {
                guid.stringValue = panel != null ? AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(panel)) : string.Empty;
            }

            if (panel == null)
            {
                EditorGUILayout.HelpBox(RolesInfo, MessageType.Info);
                EditorGUILayout.EndVertical();
                EditorGUILayout.Space(10);
                return;
            }

            var type = (InventoryType)serializedObject.FindProperty("settings.inventoryType").intValue;
            var ready = DrawChecklist(panel.Document, type, out var hasDetails);

            EditorGUILayout.Space(6);
            EditorGUILayout.PropertyField(ui.FindPropertyRelative("canDrag"));
            using (new EditorGUI.DisabledScope(!hasDetails))
            {
                var canClick = ui.FindPropertyRelative("canClick");
                EditorGUILayout.PropertyField(canClick,
                    new GUIContent(canClick.displayName, hasDetails ? canClick.tooltip
                        : "Needs something to show the selected item: Item Name, Item Description, Item Icon or Examine View."));
            }

            EditorGUILayout.PropertyField(ui.FindPropertyRelative("canDropToWorld"));

            EditorGUILayout.Space(6);
            var builder = InventoryUIBuild.Builder;
            if (builder == null)
            {
                EditorGUILayout.HelpBox(MissingBuilderInfo, MessageType.Warning);
            }

            using (new EditorGUI.DisabledScope(builder == null || !ready))
            {
                var label = new GUIContent("Build UI", ready
                    ? "Builds the panel into the scene's canvas and adds the inventory's views to its elements."
                    : "The panel is missing a required role.");
                if (GUILayout.Button(label, GUILayout.Height(28)))
                {
                    serializedObject.ApplyModifiedProperties();
                    builder((InventoryData)target, panel);

                    // Build UI shows dialogs: end this event before the layout they interrupted.
                    GUIUtility.ExitGUI();
                }
            }

            EditorGUILayout.EndVertical();
            EditorGUILayout.Space(10);
        }

        // One line per feature that matters for this inventory type: green when the panel has it, red when a
        // required one is missing, grey when an optional one is. Returns whether every required one is there.
        private bool DrawChecklist(EasyUIDocument document, InventoryType type, out bool hasDetails)
        {
            var ready = true;
            hasDetails = InventoryRoles.HasDetails(document);
            var color = GUI.contentColor;

            foreach (var check in InventoryRoles.Checklist(type))
            {
                if (check.Need == InventoryRoles.Need.Unused)
                {
                    continue;
                }

                // A feature is there when any of its roles is (e.g. Details: a name, a description, an icon...).
                var found = check.IsIn(document);

                string text;
                if (found)
                {
                    GUI.contentColor = FoundColor;
                    text = "✓  " + check.Label;
                }
                else if (check.Need == InventoryRoles.Need.Required)
                {
                    GUI.contentColor = MissingColor;
                    text = "✗  " + check.Label + " (required)";
                    ready = false;
                }
                else
                {
                    GUI.contentColor = AbsentColor;
                    text = "–  " + check.Label;
                }

                EditorGUILayout.LabelField(text, CheckStyle);
            }

            GUI.contentColor = color;
            return ready;
        }
    }
}
#endif

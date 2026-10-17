#if !HAS_VENEER
using UnityEditor;
using UnityEngine;

namespace InventorySystem
{
    /// <summary>
    /// Fallback Inspector of <see cref="InventoryData"/> used while Veneer is not in the project: Unity's default
    /// Inspector plus the Compile button, so the Compile step stays reachable. With the kit, InventoryDataStudioEditor
    /// (Editor/Studio) replaces it. The Collectible simply uses Unity's default Inspector without the kit.
    /// </summary>
    [CustomEditor(typeof(InventoryData))]
    internal sealed class InventoryDataEditor : Editor
    {
        // Same green as PoolData's and InteractData's Compile buttons.
        private static readonly Color CompileButtonColor = new(0.4f, 0.75f, 0.4f);

        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();
            EditorGUILayout.Space(14);

            var buttonColor = GUI.backgroundColor;
            GUI.backgroundColor = CompileButtonColor;
            if (GUILayout.Button("Compile", GUILayout.Height(34)))
            {
                InventoryCompiler.Compile((InventoryData)target);
            }

            GUI.backgroundColor = buttonColor;
        }
    }
}
#endif

using UnityEditor;
using UnityEngine;

namespace InventorySystem
{
    /// <summary>
    /// Inspector for <see cref="Collectible"/>: the item it gives, read-only (InventoryData is where it is
    /// edited), then its settings, with Collector Tag as a tag dropdown enabled only while Collect On
    /// Trigger is on, and the On Take / On Release / On Inventory Full events.
    /// </summary>
    [CustomEditor(typeof(Collectible))]
    [CanEditMultipleObjects]
    internal sealed class CollectibleEditor : Editor
    {
        private SerializedProperty _itemType;
        private SerializedProperty _amount;
        private SerializedProperty _collectOnTrigger;
        private SerializedProperty _collectorTag;
        private SerializedProperty _onTake;
        private SerializedProperty _onRelease;
        private SerializedProperty _onInventoryFull;

        private void OnEnable()
        {
            _itemType = serializedObject.FindProperty("itemType");
            _amount = serializedObject.FindProperty("amount");
            _collectOnTrigger = serializedObject.FindProperty("collectOnTrigger");
            _collectorTag = serializedObject.FindProperty("collectorTag");
            _onTake = serializedObject.FindProperty("onTake");
            _onRelease = serializedObject.FindProperty("onRelease");
            _onInventoryFull = serializedObject.FindProperty("onInventoryFull");
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            using (new EditorGUI.DisabledScope(true))
            {
                EditorGUILayout.PropertyField(_itemType, new GUIContent("Item", "Assigned by InventoryData > Compile."));
            }

            EditorGUILayout.PropertyField(_amount);
            EditorGUILayout.PropertyField(_collectOnTrigger);

            using (new EditorGUI.DisabledScope(!_collectOnTrigger.boolValue && !_collectOnTrigger.hasMultipleDifferentValues))
            {
                EditorGUI.showMixedValue = _collectorTag.hasMultipleDifferentValues;
                EditorGUI.BeginChangeCheck();
                var tag = EditorGUILayout.TagField(new GUIContent("Collector Tag", _collectorTag.tooltip), _collectorTag.stringValue);
                if (EditorGUI.EndChangeCheck())
                {
                    _collectorTag.stringValue = tag;
                }
                EditorGUI.showMixedValue = false;
            }

            EditorGUILayout.Space(4);
            EditorGUILayout.PropertyField(_onTake);
            EditorGUILayout.PropertyField(_onRelease);
            EditorGUILayout.PropertyField(_onInventoryFull);

            serializedObject.ApplyModifiedProperties();
        }
    }
}

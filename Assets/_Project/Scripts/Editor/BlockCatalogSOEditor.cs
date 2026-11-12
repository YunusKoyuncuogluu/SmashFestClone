using SmashFast.Data;
using UnityEditor;
using UnityEngine;

namespace SmashFast.LevelEditor
{
    [CustomEditor(typeof(BlockCatalogSO))]
    public class BlockCatalogSOEditor : UnityEditor.Editor
    {
        private SerializedProperty familiesProperty;
        private SerializedProperty standaloneBlocksProperty;

        private void OnEnable()
        {
            familiesProperty = serializedObject.FindProperty("families");
            standaloneBlocksProperty = serializedObject.FindProperty("standaloneBlocks");
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            EditorGUILayout.PropertyField(familiesProperty, true);
            EditorGUILayout.PropertyField(standaloneBlocksProperty, true);
            serializedObject.ApplyModifiedProperties();

            BlockCatalogSO catalog = (BlockCatalogSO)target;
            if (GUI.changed)
            {
                catalog.InvalidateRuntimeBlocks();
                EditorUtility.SetDirty(catalog);
            }

            EditorGUILayout.Space(6f);
            EditorGUILayout.HelpBox(
                "Runtime blocks are built on demand from Families + Standalone Blocks (" + catalog.BlockCount.ToString() + " total).",
                MessageType.None);
        }
    }
}

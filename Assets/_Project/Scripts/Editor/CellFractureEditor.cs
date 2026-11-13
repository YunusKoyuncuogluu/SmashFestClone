using SmashFest.Fracture;
using UnityEditor;
using UnityEngine;

namespace SmashFest.LevelEditor
{
    [CustomEditor(typeof(CellFractureController))]
    public class CellFractureEditor : UnityEditor.Editor
    {
        private readonly FractureAssetBaker fractureBaker = new FractureAssetBaker();

        private SerializedProperty cellCountProperty;
        private SerializedProperty randomSeedProperty;
        private SerializedProperty interiorMaterialProperty;
        private SerializedProperty fallbackMaterialProperty;
        private SerializedProperty interiorUVScaleProperty;
        private SerializedProperty previewExplosionOffsetProperty;
        private SerializedProperty totalMassProperty;
        private SerializedProperty convexCollidersProperty;
        private SerializedProperty physicMaterialProperty;
        private SerializedProperty editSeedsManuallyProperty;
        private SerializedProperty voronoiSeedsProperty;

        private void OnEnable()
        {
            cellCountProperty = serializedObject.FindProperty("cellCount");
            randomSeedProperty = serializedObject.FindProperty("randomSeed");
            interiorMaterialProperty = serializedObject.FindProperty("interiorMaterial");
            fallbackMaterialProperty = serializedObject.FindProperty("fallbackMaterial");
            interiorUVScaleProperty = serializedObject.FindProperty("interiorUVScale");
            previewExplosionOffsetProperty = serializedObject.FindProperty("previewExplosionOffset");
            totalMassProperty = serializedObject.FindProperty("totalMass");
            convexCollidersProperty = serializedObject.FindProperty("convexColliders");
            physicMaterialProperty = serializedObject.FindProperty("physicMaterial");
            editSeedsManuallyProperty = serializedObject.FindProperty("editSeedsManually");
            voronoiSeedsProperty = serializedObject.FindProperty("voronoiSeeds");
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            CellFractureController controller = (CellFractureController)target;

            EditorGUILayout.LabelField("Voronoi Fracture", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(cellCountProperty);
            EditorGUILayout.PropertyField(randomSeedProperty);
            EditorGUILayout.PropertyField(interiorMaterialProperty);
            EditorGUILayout.PropertyField(fallbackMaterialProperty);
            EditorGUILayout.PropertyField(interiorUVScaleProperty);
            EditorGUILayout.PropertyField(previewExplosionOffsetProperty);

            EditorGUILayout.Space(6f);
            EditorGUILayout.LabelField("Physics (Bake)", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(totalMassProperty);
            EditorGUILayout.PropertyField(convexCollidersProperty);
            EditorGUILayout.PropertyField(physicMaterialProperty);

            EditorGUILayout.Space(6f);
            EditorGUILayout.LabelField("Seed Editing", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(editSeedsManuallyProperty, new GUIContent("Edit Seeds Manually"));

            bool editSeeds = editSeedsManuallyProperty.boolValue;
            using (new EditorGUI.DisabledScope(!editSeeds))
            {
                EditorGUILayout.PropertyField(voronoiSeedsProperty, new GUIContent("Voronoi Seeds"), true);
            }

            if (!editSeeds)
                EditorGUILayout.HelpBox("Seeds are auto-generated from Cell Count and Random Seed. Enable Edit Seeds Manually to place impact-focused seeds in the Scene View.", MessageType.Info);
            else
                EditorGUILayout.HelpBox("Drag seed handles in the Scene View to concentrate fractures near an impact point.", MessageType.Info);

            bool previewOffsetChanged = serializedObject.ApplyModifiedProperties();
            if (previewOffsetChanged && controller.HasPreview)
            {
                controller.RefreshPreviewTransforms();
                SceneView.RepaintAll();
            }

            EditorGUILayout.Space(10f);
            DrawWorkflowButtons(controller);
        }

        private void DrawWorkflowButtons(CellFractureController controller)
        {
            EditorGUILayout.LabelField("Workflow", EditorStyles.boldLabel);

            Color previous = GUI.backgroundColor;
            GUI.backgroundColor = new Color(0.45f, 0.85f, 1f, 1f);
            if (GUILayout.Button("Generate / Update Preview", GUILayout.Height(28f)))
            {
                Undo.RecordObject(controller, "Generate Fracture Preview");
                controller.GeneratePreview();
                EditorUtility.SetDirty(controller);
                SceneView.RepaintAll();
            }

            GUI.backgroundColor = previous;
            if (GUILayout.Button("Clear Preview", GUILayout.Height(24f)))
            {
                Undo.RecordObject(controller, "Clear Fracture Preview");
                controller.ClearPreview();
                EditorUtility.SetDirty(controller);
                SceneView.RepaintAll();
            }

            GUI.backgroundColor = new Color(0.55f, 1f, 0.55f, 1f);
            if (GUILayout.Button("Bake Fracture to Prefab/Scene", GUILayout.Height(30f)))
                BakeFracture(controller);

            GUI.backgroundColor = previous;

            if (controller.HasPreview)
                EditorGUILayout.HelpBox("Preview active. Scrub Preview Explosion Offset to inspect interior cuts and UVs.", MessageType.None);
        }

        private void OnSceneGUI()
        {
            CellFractureController controller = (CellFractureController)target;
            if (!controller.EditSeedsManually)
                return;

            serializedObject.Update();
            controller.EnsureSeedListSize();

            Transform owner = controller.transform;
            for (int i = 0; i < controller.VoronoiSeeds.Count; i++)
            {
                Vector3 localSeed = controller.GetSeedPosition(i);
                Vector3 worldSeed = owner.TransformPoint(localSeed);

                EditorGUI.BeginChangeCheck();
                Handles.color = Color.yellow;
                Vector3 newWorld = Handles.PositionHandle(worldSeed, owner.rotation);
                Handles.Label(newWorld + Vector3.up * 0.05f, "Seed " + i.ToString());
                if (EditorGUI.EndChangeCheck())
                {
                    Undo.RecordObject(controller, "Move Voronoi Seed");
                    Vector3 newLocal = owner.InverseTransformPoint(newWorld);
                    controller.SetSeedPosition(i, newLocal);
                    EditorUtility.SetDirty(controller);
                    serializedObject.Update();
                }
            }

            serializedObject.ApplyModifiedProperties();
        }

        private void BakeFracture(CellFractureController controller)
        {
            FractureBakeRequest request = new FractureBakeRequest();
            request.Controller = controller;
            request.AssetName = controller.gameObject.name + "_Fracture_" + System.DateTime.Now.ToString("yyyyMMdd_HHmmss");
            request.RegisterUndo = true;

            FractureBakeResult result = fractureBaker.Bake(request);
            if (!result.Succeeded)
            {
                EditorUtility.DisplayDialog("Bake Fracture", result.Error, "OK");
                return;
            }

            AssetDatabase.Refresh();
            controller.ClearPreview();
            Selection.activeGameObject = result.Root;

            EditorGUIUtility.PingObject(result.PrefabAsset != null ? (Object)result.PrefabAsset : result.Root);
        }
    }
}

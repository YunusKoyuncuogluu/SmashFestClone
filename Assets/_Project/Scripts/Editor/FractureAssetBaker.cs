using SmashFast.Fracture;
using UnityEditor;
using UnityEngine;

namespace SmashFast.LevelEditor
{
    internal sealed class FractureBakeRequest
    {
        public CellFractureController Controller { get; set; }
        public string AssetName { get; set; }
        public int PieceLayer { get; set; } = -1;
        public bool RegisterUndo { get; set; }
        public bool ReplaceExistingAssets { get; set; }
    }

    internal sealed class FractureBakeResult
    {
        public GameObject Root { get; set; }
        public GameObject PrefabAsset { get; set; }
        public string MeshAssetPath { get; set; }
        public string PrefabPath { get; set; }
        public int PieceCount { get; set; }
        public string Error { get; set; }

        public bool Succeeded
        {
            get { return Root != null; }
        }
    }

    internal sealed class FractureAssetBaker
    {
        public const string BakedMeshFolder = "Assets/_Project/FracturedMeshes";

        public FractureBakeResult Bake(FractureBakeRequest request)
        {
            FractureBakeResult result = new FractureBakeResult();
            if (request == null || request.Controller == null)
            {
                result.Error = "A CellFractureController is required.";
                return result;
            }

            CellFractureController controller = request.Controller;
            MeshFilter meshFilter = controller.GetComponent<MeshFilter>();
            if (meshFilter == null || meshFilter.sharedMesh == null)
            {
                result.Error = "A MeshFilter with a shared mesh is required.";
                return result;
            }

            FracturedPiece[] pieces = controller.BakeFracturePieces();
            if (pieces == null || pieces.Length == 0)
            {
                result.Error = "Fracture produced no pieces.";
                return result;
            }

            EnsureFolderExists(BakedMeshFolder);
            string assetName = string.IsNullOrEmpty(request.AssetName) ? controller.gameObject.name + "_Fracture" : request.AssetName;
            string meshAssetPath = BakedMeshFolder + "/" + assetName + ".asset";
            string prefabPath = BakedMeshFolder + "/" + assetName + ".prefab";
            if (request.ReplaceExistingAssets)
                AssetDatabase.DeleteAsset(meshAssetPath);

            Mesh firstMesh = pieces[0].Mesh;
            AssetDatabase.CreateAsset(firstMesh, meshAssetPath);
            for (int i = 1; i < pieces.Length; i++)
                AssetDatabase.AddObjectToAsset(pieces[i].Mesh, firstMesh);

            AssetDatabase.SaveAssets();

            Material[] materials = controller.BuildPieceMaterials();
            float[] masses = controller.ComputeMassDistribution(pieces);

            GameObject root = new GameObject(controller.gameObject.name + "_Fractured");
            if (request.RegisterUndo)
                Undo.RegisterCreatedObjectUndo(root, "Bake Fracture Hierarchy");

            root.transform.SetParent(controller.transform.parent, false);
            root.transform.position = controller.transform.position;
            root.transform.rotation = controller.transform.rotation;
            root.transform.localScale = controller.transform.localScale;
            if (request.PieceLayer >= 0)
                root.layer = request.PieceLayer;

            for (int i = 0; i < pieces.Length; i++)
            {
                FracturedPiece piece = pieces[i];
                GameObject pieceObject = new GameObject("Cell_" + piece.SeedIndex.ToString());
                if (request.RegisterUndo)
                    Undo.RegisterCreatedObjectUndo(pieceObject, "Bake Fracture Piece");

                pieceObject.transform.SetParent(root.transform, false);
                pieceObject.transform.localPosition = piece.LocalCentroid;
                pieceObject.transform.localRotation = Quaternion.identity;
                pieceObject.transform.localScale = Vector3.one;
                if (request.PieceLayer >= 0)
                    pieceObject.layer = request.PieceLayer;

                MeshFilter pieceFilter = pieceObject.AddComponent<MeshFilter>();
                pieceFilter.sharedMesh = piece.Mesh;

                MeshRenderer pieceRenderer = pieceObject.AddComponent<MeshRenderer>();
                pieceRenderer.sharedMaterials = materials;

                MeshCollider pieceCollider = pieceObject.AddComponent<MeshCollider>();
                pieceCollider.sharedMesh = piece.Mesh;
                pieceCollider.convex = controller.ConvexColliders;
                if (controller.PhysicMaterial != null)
                    pieceCollider.sharedMaterial = controller.PhysicMaterial;

                Rigidbody body = pieceObject.AddComponent<Rigidbody>();
                body.mass = masses[i];
                body.collisionDetectionMode = CollisionDetectionMode.Discrete;
                body.interpolation = RigidbodyInterpolation.Interpolate;
            }

            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
            AssetDatabase.SaveAssets();

            result.Root = root;
            result.PrefabAsset = prefab;
            result.MeshAssetPath = meshAssetPath;
            result.PrefabPath = prefabPath;
            result.PieceCount = pieces.Length;
            return result;
        }

        private void EnsureFolderExists(string folderPath)
        {
            if (AssetDatabase.IsValidFolder(folderPath))
                return;

            string[] parts = folderPath.Split('/');
            string current = parts[0];
            for (int i = 1; i < parts.Length; i++)
            {
                string next = current + "/" + parts[i];
                if (!AssetDatabase.IsValidFolder(next))
                    AssetDatabase.CreateFolder(current, parts[i]);

                current = next;
            }
        }
    }
}

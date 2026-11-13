using System;
using System.Collections.Generic;
using UnityEngine;

namespace SmashFest.Fracture
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(MeshFilter))]
    [RequireComponent(typeof(MeshRenderer))]
    public sealed class CellFractureController : MonoBehaviour
    {
        [SerializeField, Range(2, 100)] private int cellCount = 8;
        [SerializeField] private int randomSeed = 1;
        [SerializeField] private Material interiorMaterial;
        [SerializeField] private Material fallbackMaterial;
        [SerializeField] private float interiorUVScale = 1f;
        [SerializeField, Range(0f, 2f)] private float previewExplosionOffset;
        [SerializeField] private float totalMass = 10f;
        [SerializeField] private bool convexColliders = true;
        [SerializeField] private PhysicsMaterial physicMaterial;
        [SerializeField] private bool editSeedsManually;
        [SerializeField] private List<Vector3> voronoiSeeds = new List<Vector3>();
        [SerializeField, HideInInspector] private Transform previewRoot;

        private const string PreviewRootName = "__CellFracturePreview";
        private const float Epsilon = MeshFracturer.Epsilon;
        private const float EpsilonSqr = Epsilon * Epsilon;

        private readonly MeshFracturer fracturer = new MeshFracturer();
        private readonly List<Transform> previewPieceTransforms = new List<Transform>(32);
        private MeshFilter meshFilter;
        private MeshRenderer meshRenderer;
        private FracturedPiece[] previewPieces;
        private readonly List<Vector3> gizmoPlaneLoop = new List<Vector3>(16);
        private bool hideSourceDuringPreview;
        private bool cachedRendererEnabled = true;
        private float lastAppliedExplosionOffset = float.MinValue;

        public bool ConvexColliders
        {
            get => convexColliders;
            set => convexColliders = value;
        }

        public PhysicsMaterial PhysicMaterial
        {
            get => physicMaterial;
            set => physicMaterial = value;
        }

        public bool EditSeedsManually
        {
            get => editSeedsManually;
            set => editSeedsManually = value;
        }

        public IReadOnlyList<Vector3> VoronoiSeeds => voronoiSeeds;
        public bool HasPreview => previewPieces != null && previewPieces.Length > 0;

        private void Awake()
        {
            CacheComponents();
        }

        private void OnDestroy()
        {
            ClearPreview();
        }

        private void OnValidate()
        {
            cellCount = Mathf.Clamp(cellCount, 2, 100);
            previewExplosionOffset = Mathf.Clamp(previewExplosionOffset, 0f, 2f);
            totalMass = Mathf.Max(totalMass, Epsilon);
            interiorUVScale = Mathf.Max(interiorUVScale, Epsilon);

            if (!editSeedsManually)
                EnsureSeedsMatchSettings(false);
            else
                EnsureSeedListSize();

            if (HasPreview && !Mathf.Approximately(lastAppliedExplosionOffset, previewExplosionOffset))
                RefreshPreviewTransforms();
        }

        private void CacheComponents()
        {
            if (meshFilter == null)
                meshFilter = GetComponent<MeshFilter>();

            if (meshRenderer == null)
                meshRenderer = GetComponent<MeshRenderer>();
        }

        public Bounds GetLocalMeshBounds()
        {
            CacheComponents();
            if (meshFilter == null || meshFilter.sharedMesh == null)
                return new Bounds(Vector3.zero, Vector3.one);

            return meshFilter.sharedMesh.bounds;
        }

        public void EnsureSeedsMatchSettings(bool forceRegenerate)
        {
            CacheComponents();
            Bounds bounds = GetLocalMeshBounds();
            bool countMismatch = voronoiSeeds == null || voronoiSeeds.Count != cellCount;
            if (forceRegenerate || countMismatch || voronoiSeeds.Count == 0)
            {
                Vector3[] generated = fracturer.GenerateSeedPoints(bounds, cellCount, randomSeed);
                voronoiSeeds = new List<Vector3>(generated);
            }
        }

        public void EnsureSeedListSize()
        {
            if (voronoiSeeds == null)
                voronoiSeeds = new List<Vector3>(cellCount);

            Bounds bounds = GetLocalMeshBounds();
            while (voronoiSeeds.Count < cellCount)
            {
                Vector3[] generated = fracturer.GenerateSeedPoints(bounds, cellCount, randomSeed + voronoiSeeds.Count);
                int need = cellCount - voronoiSeeds.Count;
                for (int i = 0; i < need && i < generated.Length; i++)
                    voronoiSeeds.Add(generated[i]);
            }

            while (voronoiSeeds.Count > cellCount)
                voronoiSeeds.RemoveAt(voronoiSeeds.Count - 1);
        }

        public void SetSeedPosition(int index, Vector3 localPosition)
        {
            EnsureSeedListSize();
            if (index < 0 || index >= voronoiSeeds.Count)
                return;

            voronoiSeeds[index] = localPosition;
        }

        public Vector3 GetSeedPosition(int index)
        {
            EnsureSeedListSize();
            if (index < 0 || index >= voronoiSeeds.Count)
                return Vector3.zero;

            return voronoiSeeds[index];
        }

        public bool GeneratePreview()
        {
            CacheComponents();
            if (meshFilter == null || meshFilter.sharedMesh == null)
                return false;

            if (!editSeedsManually)
                EnsureSeedsMatchSettings(true);
            else
                EnsureSeedListSize();

            DestroyPreviewVisuals();
            RestoreSourceRenderer();

            Vector3[] seeds = voronoiSeeds.ToArray();
            previewPieces = fracturer.FractureVoronoi(meshFilter.sharedMesh, seeds, interiorUVScale);
            hideSourceDuringPreview = previewPieces != null && previewPieces.Length > 0;

            if (!HasPreview)
                return false;

            BuildPreviewVisuals();
            HideSourceRenderer();
            return true;
        }

        public void ClearPreview()
        {
            DestroyPreviewVisuals();
            RestoreSourceRenderer();
            hideSourceDuringPreview = false;
            previewPieces = null;
            lastAppliedExplosionOffset = float.MinValue;
        }

        public void RefreshPreviewTransforms()
        {
            if (!HasPreview || previewPieceTransforms.Count == 0)
                return;

            Vector3 meshCenter = GetLocalMeshBounds().center;
            int count = Mathf.Min(previewPieces.Length, previewPieceTransforms.Count);
            for (int i = 0; i < count; i++)
            {
                Transform pieceTransform = previewPieceTransforms[i];
                if (pieceTransform == null)
                    continue;

                pieceTransform.localPosition = ComputeExplodedLocalPosition(previewPieces[i].LocalCentroid, meshCenter);
            }

            lastAppliedExplosionOffset = previewExplosionOffset;
        }

        private void HideSourceRenderer()
        {
            CacheComponents();
            if (meshRenderer == null)
                return;

            cachedRendererEnabled = meshRenderer.enabled;
            meshRenderer.enabled = false;
        }

        private void RestoreSourceRenderer()
        {
            CacheComponents();
            if (meshRenderer == null)
                return;

            if (hideSourceDuringPreview)
                meshRenderer.enabled = cachedRendererEnabled;
        }

        private void BuildPreviewVisuals()
        {
            DestroyPreviewVisuals(false);

            GameObject rootObject = new GameObject(PreviewRootName);
            previewRoot = rootObject.transform;
            previewRoot.SetParent(transform, false);
            previewRoot.localPosition = Vector3.zero;
            previewRoot.localRotation = Quaternion.identity;
            previewRoot.localScale = Vector3.one;
            ApplyPreviewHideFlags(rootObject);

            Material[] materials = BuildPieceMaterials();
            EnsurePreviewMaterials(materials);
            Vector3 meshCenter = GetLocalMeshBounds().center;
            previewPieceTransforms.Clear();

            for (int i = 0; i < previewPieces.Length; i++)
            {
                FracturedPiece piece = previewPieces[i];
                if (piece == null || piece.Mesh == null)
                    continue;

                piece.Mesh.hideFlags = HideFlags.HideAndDontSave;

                GameObject pieceObject = new GameObject("PreviewCell_" + piece.SeedIndex.ToString());
                pieceObject.layer = gameObject.layer;
                Transform pieceTransform = pieceObject.transform;
                pieceTransform.SetParent(previewRoot, false);
                pieceTransform.localRotation = Quaternion.identity;
                pieceTransform.localScale = Vector3.one;
                pieceTransform.localPosition = ComputeExplodedLocalPosition(piece.LocalCentroid, meshCenter);
                ApplyPreviewHideFlags(pieceObject);

                MeshFilter pieceFilter = pieceObject.AddComponent<MeshFilter>();
                pieceFilter.sharedMesh = piece.Mesh;

                MeshRenderer pieceRenderer = pieceObject.AddComponent<MeshRenderer>();
                pieceRenderer.sharedMaterials = materials;
                pieceRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
                pieceRenderer.receiveShadows = true;
                pieceRenderer.lightProbeUsage = UnityEngine.Rendering.LightProbeUsage.Off;
                pieceRenderer.reflectionProbeUsage = UnityEngine.Rendering.ReflectionProbeUsage.Off;

                previewPieceTransforms.Add(pieceTransform);
            }

            lastAppliedExplosionOffset = previewExplosionOffset;
        }

        private void DestroyPreviewVisuals(bool destroyMeshes = true)
        {
            previewPieceTransforms.Clear();

            if (previewRoot != null)
            {
                DestroyPreviewObject(previewRoot.gameObject);
                previewRoot = null;
            }

            if (!destroyMeshes || previewPieces == null)
                return;

            for (int i = 0; i < previewPieces.Length; i++)
            {
                FracturedPiece piece = previewPieces[i];
                if (piece == null || piece.Mesh == null)
                    continue;

                DestroyPreviewObject(piece.Mesh);
                piece.Mesh = null;
            }
        }

        private Vector3 ComputeExplodedLocalPosition(Vector3 localCentroid, Vector3 meshCenter)
        {
            Vector3 fromCenter = localCentroid - meshCenter;
            if (fromCenter.sqrMagnitude <= EpsilonSqr)
                fromCenter = Vector3.up * Epsilon;

            return localCentroid + fromCenter.normalized * previewExplosionOffset;
        }

        private void ApplyPreviewHideFlags(GameObject target)
        {
            target.hideFlags = HideFlags.DontSaveInEditor | HideFlags.DontSaveInBuild | HideFlags.NotEditable;
        }

        private void DestroyPreviewObject(UnityEngine.Object target)
        {
            if (target == null)
                return;

            if (Application.isPlaying)
                Destroy(target);
            else
                DestroyImmediate(target);
        }

        private void EnsurePreviewMaterials(Material[] materials)
        {
            if (materials == null)
                return;

            for (int i = 0; i < materials.Length; i++)
            {
                if (materials[i] == null)
                    materials[i] = fallbackMaterial;
            }
        }

        public FracturedPiece[] BakeFracturePieces()
        {
            CacheComponents();
            if (meshFilter == null || meshFilter.sharedMesh == null)
                return Array.Empty<FracturedPiece>();

            if (!editSeedsManually)
                EnsureSeedsMatchSettings(true);
            else
                EnsureSeedListSize();

            return fracturer.FractureVoronoi(meshFilter.sharedMesh, voronoiSeeds.ToArray(), interiorUVScale);
        }

        public Material[] BuildPieceMaterials()
        {
            CacheComponents();
            Material exterior = null;
            if (meshRenderer != null && meshRenderer.sharedMaterials != null && meshRenderer.sharedMaterials.Length > 0)
                exterior = meshRenderer.sharedMaterials[0];

            Material interior = interiorMaterial != null ? interiorMaterial : exterior;
            return new Material[] { exterior, interior };
        }

        public float[] ComputeMassDistribution(FracturedPiece[] pieces)
        {
            if (pieces == null || pieces.Length == 0)
                return Array.Empty<float>();

            float totalVolume = 0f;
            for (int i = 0; i < pieces.Length; i++)
                totalVolume += Mathf.Max(pieces[i].VolumeEstimate, Epsilon);

            float[] masses = new float[pieces.Length];
            float safeTotalMass = Mathf.Max(totalMass, Epsilon);
            for (int i = 0; i < pieces.Length; i++)
            {
                float ratio = pieces[i].VolumeEstimate / totalVolume;
                masses[i] = Mathf.Max(safeTotalMass * ratio, Epsilon);
            }

            return masses;
        }

        private void OnDrawGizmosSelected()
        {
            CacheComponents();
            Bounds localBounds = GetLocalMeshBounds();

            if (!editSeedsManually)
            {
                if (voronoiSeeds == null || voronoiSeeds.Count != cellCount)
                    EnsureSeedsMatchSettings(false);
            }
            else
                EnsureSeedListSize();

            Matrix4x4 localToWorld = transform.localToWorldMatrix;
            Gizmos.matrix = localToWorld;

            Gizmos.color = new Color(0.2f, 0.8f, 1f, 0.35f);
            Gizmos.DrawWireCube(localBounds.center, localBounds.size);

            if (voronoiSeeds != null)
            {
                for (int i = 0; i < voronoiSeeds.Count; i++)
                {
                    Gizmos.color = Color.yellow;
                    Gizmos.DrawSphere(voronoiSeeds[i], localBounds.size.magnitude * 0.015f);
                }

                DrawBisectorPlanes(localBounds);
            }
        }

        private void DrawBisectorPlanes(Bounds localBounds)
        {
            if (voronoiSeeds == null || voronoiSeeds.Count < 2)
                return;

            Gizmos.color = new Color(1f, 0.35f, 0.1f, 0.65f);
            int seedCount = voronoiSeeds.Count;
            for (int i = 0; i < seedCount; i++)
            {
                for (int j = i + 1; j < seedCount; j++)
                {
                    fracturer.GetBisectorPlane(voronoiSeeds[i], voronoiSeeds[j], out Vector3 planePoint, out Vector3 planeNormal);
                    if (!fracturer.TryIntersectPlaneWithBounds(localBounds, planePoint, planeNormal, gizmoPlaneLoop))
                        continue;

                    for (int p = 0; p < gizmoPlaneLoop.Count; p++)
                    {
                        Vector3 a = gizmoPlaneLoop[p];
                        Vector3 b = gizmoPlaneLoop[(p + 1) % gizmoPlaneLoop.Count];
                        Gizmos.DrawLine(a, b);
                    }
                }
            }
        }
    }
}

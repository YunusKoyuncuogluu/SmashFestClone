using System;
using System.Collections.Generic;
using UnityEngine;

namespace SmashFast.Fracture
{
    public class MeshFracturer
    {
        public const float Epsilon = 1e-5f;

        private const float EpsilonSqr = Epsilon * Epsilon;

        private readonly MeshHalfSpaceClipper clipper = new MeshHalfSpaceClipper();
        private readonly FractureMeshBuffer clipBuffer = new FractureMeshBuffer(256, 512);
        private readonly List<Vector3> planeHits = new List<Vector3>(12);
        private readonly Vector3[] boxCorners = new Vector3[8];
        private readonly int[,] boxEdges =
        {
            { 0, 1 }, { 1, 2 }, { 2, 3 }, { 3, 0 },
            { 4, 5 }, { 5, 6 }, { 6, 7 }, { 7, 4 },
            { 0, 4 }, { 1, 5 }, { 2, 6 }, { 3, 7 }
        };

        public Vector3[] GenerateSeedPoints(Bounds localBounds, int cellCount, int randomSeed)
        {
            Vector3[] seeds = new Vector3[Mathf.Clamp(cellCount, 2, 100)];
            System.Random random = new System.Random(randomSeed);

            Vector3 size = localBounds.size;
            float inset = Mathf.Min(size.x, Mathf.Min(size.y, size.z)) * 0.02f;
            if (inset < Epsilon)
                inset = 0f;

            Vector3 min = localBounds.min + Vector3.one * inset;
            Vector3 max = localBounds.max - Vector3.one * inset;

            for (int i = 0; i < seeds.Length; i++)
            {
                seeds[i] = new Vector3(
                    Lerp(min.x, max.x, (float)random.NextDouble()),
                    Lerp(min.y, max.y, (float)random.NextDouble()),
                    Lerp(min.z, max.z, (float)random.NextDouble()));
            }

            return seeds;
        }

        public FracturedPiece[] FractureVoronoi(Mesh sourceMesh, Vector3[] seeds, float interiorUVScale)
        {
            if (sourceMesh == null || seeds == null || seeds.Length < 2)
                return Array.Empty<FracturedPiece>();

            FractureMeshBuffer source = BuildSourceBuffer(sourceMesh);
            if (!source.HasTriangles())
                return Array.Empty<FracturedPiece>();

            float uvScale = interiorUVScale <= Epsilon ? 1f : interiorUVScale;
            List<FracturedPiece> pieces = new List<FracturedPiece>(seeds.Length);

            for (int seedIndex = 0; seedIndex < seeds.Length; seedIndex++)
            {
                FractureMeshBuffer cell = CutCell(source, seeds, seedIndex, uvScale);
                if (cell == null)
                    continue;

                cell.ShiftToCentroid(out Vector3 centroid);
                Bounds bounds = cell.ComputeBounds();

                pieces.Add(new FracturedPiece
                {
                    Mesh = cell.BuildUnityMesh($"{sourceMesh.name}_Cell_{seedIndex}"),
                    LocalCentroid = centroid,
                    SeedIndex = seedIndex,
                    VolumeEstimate = Mathf.Max(bounds.size.x * bounds.size.y * bounds.size.z, Epsilon)
                });
            }

            return pieces.ToArray();
        }

        public void GetBisectorPlane(Vector3 seedA, Vector3 seedB, out Vector3 planePoint, out Vector3 planeNormal)
        {
            Vector3 delta = seedA - seedB;
            float deltaSqr = delta.sqrMagnitude;
            if (deltaSqr <= EpsilonSqr)
            {
                planePoint = seedA;
                planeNormal = Vector3.up;
                return;
            }

            planeNormal = delta / Mathf.Sqrt(deltaSqr);
            planePoint = (seedA + seedB) * 0.5f;
        }

        public bool TryIntersectPlaneWithBounds(Bounds bounds, Vector3 planePoint, Vector3 planeNormal, List<Vector3> loopOut)
        {
            loopOut.Clear();
            Vector3 normal = planeNormal.normalized;
            if (normal.sqrMagnitude < EpsilonSqr)
                return false;

            FillBoxCorners(bounds);
            planeHits.Clear();
            for (int i = 0; i < 12; i++)
            {
                Vector3 a = boxCorners[boxEdges[i, 0]];
                Vector3 b = boxCorners[boxEdges[i, 1]];
                float da = Vector3.Dot(a - planePoint, normal);
                float db = Vector3.Dot(b - planePoint, normal);
                if (da * db > 0f || Mathf.Abs(da - db) <= Epsilon)
                    continue;

                float t = da / (da - db);
                if (t < -Epsilon || t > 1f + Epsilon)
                    continue;

                Vector3 hit = Vector3.LerpUnclamped(a, b, Mathf.Clamp01(t));
                if (!planeHits.Exists(existing => (existing - hit).sqrMagnitude <= EpsilonSqr * 100f))
                    planeHits.Add(hit);
            }

            if (planeHits.Count < 3)
                return false;

            clipper.BuildOrthonormalBasis(normal, out Vector3 tangent, out Vector3 bitangent);
            Vector3 centroid = Vector3.zero;
            foreach (Vector3 hit in planeHits)
                centroid += hit;

            centroid /= planeHits.Count;

            planeHits.Sort((a, b) =>
            {
                float angleA = Mathf.Atan2(Vector3.Dot(a - centroid, bitangent), Vector3.Dot(a - centroid, tangent));
                float angleB = Mathf.Atan2(Vector3.Dot(b - centroid, bitangent), Vector3.Dot(b - centroid, tangent));
                return angleA.CompareTo(angleB);
            });

            loopOut.AddRange(planeHits);
            return true;
        }

        private FractureMeshBuffer CutCell(FractureMeshBuffer source, Vector3[] seeds, int seedIndex, float uvScale)
        {
            FractureMeshBuffer cell = source.Clone();
            Vector3 seed = seeds[seedIndex];

            for (int otherIndex = 0; otherIndex < seeds.Length; otherIndex++)
            {
                if (otherIndex == seedIndex)
                    continue;

                Vector3 other = seeds[otherIndex];
                Vector3 delta = seed - other;
                float deltaSqr = delta.sqrMagnitude;
                if (deltaSqr <= EpsilonSqr)
                    continue;

                Vector3 planeNormal = delta / Mathf.Sqrt(deltaSqr);
                Vector3 planePoint = (seed + other) * 0.5f;

                clipper.Clip(cell, clipBuffer, planePoint, planeNormal, uvScale);
                cell.CopyFrom(clipBuffer);

                if (!cell.HasTriangles())
                    return null;
            }

            return cell;
        }

        private FractureMeshBuffer BuildSourceBuffer(Mesh sourceMesh)
        {
            Vector3[] positions = sourceMesh.vertices;
            Vector3[] normals = sourceMesh.normals;
            Vector4[] tangents = sourceMesh.tangents;
            Vector2[] uvs = sourceMesh.uv;
            int[] triangles = sourceMesh.triangles;

            bool hasNormals = normals != null && normals.Length == positions.Length;
            bool hasTangents = tangents != null && tangents.Length == positions.Length;
            bool hasUvs = uvs != null && uvs.Length == positions.Length;

            FractureMeshBuffer buffer = new FractureMeshBuffer(Mathf.Max(positions.Length, 32), Mathf.Max(triangles.Length, 96));
            int[] remap = new int[positions.Length];
            for (int i = 0; i < positions.Length; i++)
            {
                FractureVertex vertex;
                vertex.Position = positions[i];
                vertex.Normal = hasNormals ? normals[i] : Vector3.up;
                vertex.Tangent = hasTangents ? tangents[i] : new Vector4(1f, 0f, 0f, 1f);
                vertex.Uv0 = hasUvs ? uvs[i] : Vector2.zero;
                remap[i] = buffer.AddVertex(vertex);
            }

            for (int i = 0; i < triangles.Length; i += 3)
            {
                int a = remap[triangles[i]];
                int b = remap[triangles[i + 1]];
                int c = remap[triangles[i + 2]];

                Vector3 ab = buffer.Vertices[b].Position - buffer.Vertices[a].Position;
                Vector3 ac = buffer.Vertices[c].Position - buffer.Vertices[a].Position;
                if (Vector3.Cross(ab, ac).sqrMagnitude > EpsilonSqr)
                    buffer.AddTriangle(a, b, c, false);
            }

            if (!hasNormals)
                RecalculateNormals(buffer);

            return buffer;
        }

        private void RecalculateNormals(FractureMeshBuffer buffer)
        {
            Vector3[] accumulated = new Vector3[buffer.Vertices.Count];
            List<int> triangles = buffer.ExteriorTriangles;
            for (int i = 0; i < triangles.Count; i += 3)
            {
                int ia = triangles[i];
                int ib = triangles[i + 1];
                int ic = triangles[i + 2];
                Vector3 a = buffer.Vertices[ia].Position;
                Vector3 normal = Vector3.Cross(buffer.Vertices[ib].Position - a, buffer.Vertices[ic].Position - a);
                accumulated[ia] += normal;
                accumulated[ib] += normal;
                accumulated[ic] += normal;
            }

            for (int i = 0; i < buffer.Vertices.Count; i++)
            {
                FractureVertex vertex = buffer.Vertices[i];
                vertex.Normal = accumulated[i].sqrMagnitude > EpsilonSqr ? accumulated[i].normalized : Vector3.up;
                buffer.Vertices[i] = vertex;
            }
        }

        private void FillBoxCorners(Bounds bounds)
        {
            Vector3 min = bounds.min;
            Vector3 max = bounds.max;
            boxCorners[0] = new Vector3(min.x, min.y, min.z);
            boxCorners[1] = new Vector3(max.x, min.y, min.z);
            boxCorners[2] = new Vector3(max.x, max.y, min.z);
            boxCorners[3] = new Vector3(min.x, max.y, min.z);
            boxCorners[4] = new Vector3(min.x, min.y, max.z);
            boxCorners[5] = new Vector3(max.x, min.y, max.z);
            boxCorners[6] = new Vector3(max.x, max.y, max.z);
            boxCorners[7] = new Vector3(min.x, max.y, max.z);
        }

        private float Lerp(float a, float b, float t) => a + (b - a) * t;
    }
}

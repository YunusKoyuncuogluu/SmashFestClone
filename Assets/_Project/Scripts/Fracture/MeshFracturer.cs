using System.Collections.Generic;
using UnityEngine;

namespace SmashFast.Fracture
{
    public static class MeshFracturer
    {
        public const float Epsilon = 1e-5f;

        private static readonly MeshHalfSpaceClipper SharedClipper = new MeshHalfSpaceClipper();
        private static readonly FractureMeshBuffer ClipDestination = new FractureMeshBuffer(256, 512);

        public static Vector3[] GenerateSeedPoints(Bounds localBounds, int cellCount, int randomSeed)
        {
            int count = Mathf.Clamp(cellCount, 2, 100);
            Vector3[] seeds = new Vector3[count];
            System.Random rng = new System.Random(randomSeed);

            Vector3 min = localBounds.min;
            Vector3 max = localBounds.max;
            Vector3 size = localBounds.size;
            float inset = Mathf.Min(size.x, Mathf.Min(size.y, size.z)) * 0.02f;
            if (inset < Epsilon)
            {
                inset = 0f;
            }

            min += Vector3.one * inset;
            max -= Vector3.one * inset;

            for (int i = 0; i < count; i++)
            {
                seeds[i] = new Vector3(
                    LerpFloat(min.x, max.x, (float)rng.NextDouble()),
                    LerpFloat(min.y, max.y, (float)rng.NextDouble()),
                    LerpFloat(min.z, max.z, (float)rng.NextDouble()));
            }

            return seeds;
        }

        public static FracturedPiece[] FractureVoronoi(Mesh sourceMesh, Vector3[] seeds, float interiorUVScale)
        {
            if (sourceMesh == null)
            {
                return System.Array.Empty<FracturedPiece>();
            }

            if (seeds == null || seeds.Length < 2)
            {
                return System.Array.Empty<FracturedPiece>();
            }

            FractureMeshBuffer sourceBuffer = BuildSourceBuffer(sourceMesh);
            if (!sourceBuffer.HasTriangles())
            {
                return System.Array.Empty<FracturedPiece>();
            }

            float uvScale = interiorUVScale <= Epsilon ? 1f : interiorUVScale;
            List<FracturedPiece> pieces = new List<FracturedPiece>(seeds.Length);

            for (int seedIndex = 0; seedIndex < seeds.Length; seedIndex++)
            {
                FractureMeshBuffer cellBuffer = sourceBuffer.Clone();
                bool valid = true;

                for (int otherIndex = 0; otherIndex < seeds.Length; otherIndex++)
                {
                    if (otherIndex == seedIndex)
                    {
                        continue;
                    }

                    Vector3 seedA = seeds[seedIndex];
                    Vector3 seedB = seeds[otherIndex];
                    Vector3 delta = seedA - seedB;
                    float deltaSqr = delta.sqrMagnitude;
                    if (deltaSqr <= Epsilon * Epsilon)
                    {
                        continue;
                    }

                    Vector3 planeNormal = delta / Mathf.Sqrt(deltaSqr);
                    Vector3 planePoint = (seedA + seedB) * 0.5f;

                    SharedClipper.Clip(cellBuffer, ClipDestination, planePoint, planeNormal, uvScale);
                    CopyClippedInto(cellBuffer, ClipDestination);

                    if (!cellBuffer.HasTriangles())
                    {
                        valid = false;
                        break;
                    }
                }

                if (!valid)
                {
                    continue;
                }

                cellBuffer.ShiftToCentroid(out Vector3 centroid);
                Bounds pieceBounds = cellBuffer.ComputeBounds();
                float volume = Mathf.Max(pieceBounds.size.x * pieceBounds.size.y * pieceBounds.size.z, Epsilon);

                Mesh pieceMesh = cellBuffer.BuildUnityMesh(sourceMesh.name + "_Cell_" + seedIndex.ToString());
                FracturedPiece piece = new FracturedPiece
                {
                    Mesh = pieceMesh,
                    LocalCentroid = centroid,
                    SeedIndex = seedIndex,
                    VolumeEstimate = volume
                };
                pieces.Add(piece);
            }

            return pieces.ToArray();
        }

        public static void GetBisectorPlane(Vector3 seedA, Vector3 seedB, out Vector3 planePoint, out Vector3 planeNormal)
        {
            Vector3 delta = seedA - seedB;
            float deltaSqr = delta.sqrMagnitude;
            if (deltaSqr <= Epsilon * Epsilon)
            {
                planePoint = seedA;
                planeNormal = Vector3.up;
                return;
            }

            planeNormal = delta / Mathf.Sqrt(deltaSqr);
            planePoint = (seedA + seedB) * 0.5f;
        }

        public static bool TryIntersectPlaneWithBounds(Bounds bounds, Vector3 planePoint, Vector3 planeNormal, List<Vector3> loopOut)
        {
            loopOut.Clear();
            Vector3 n = planeNormal.normalized;
            if (n.sqrMagnitude < Epsilon * Epsilon)
            {
                return false;
            }

            Vector3[] corners = new Vector3[8];
            Vector3 min = bounds.min;
            Vector3 max = bounds.max;
            corners[0] = new Vector3(min.x, min.y, min.z);
            corners[1] = new Vector3(max.x, min.y, min.z);
            corners[2] = new Vector3(max.x, max.y, min.z);
            corners[3] = new Vector3(min.x, max.y, min.z);
            corners[4] = new Vector3(min.x, min.y, max.z);
            corners[5] = new Vector3(max.x, min.y, max.z);
            corners[6] = new Vector3(max.x, max.y, max.z);
            corners[7] = new Vector3(min.x, max.y, max.z);

            int[,] edges =
            {
                { 0, 1 }, { 1, 2 }, { 2, 3 }, { 3, 0 },
                { 4, 5 }, { 5, 6 }, { 6, 7 }, { 7, 4 },
                { 0, 4 }, { 1, 5 }, { 2, 6 }, { 3, 7 }
            };

            List<Vector3> hits = new List<Vector3>(12);
            for (int i = 0; i < 12; i++)
            {
                Vector3 a = corners[edges[i, 0]];
                Vector3 b = corners[edges[i, 1]];
                float da = Vector3.Dot(a - planePoint, n);
                float db = Vector3.Dot(b - planePoint, n);
                if (da * db > 0f)
                {
                    continue;
                }

                if (Mathf.Abs(da - db) <= Epsilon)
                {
                    continue;
                }

                float t = da / (da - db);
                if (t < -Epsilon || t > 1f + Epsilon)
                {
                    continue;
                }

                Vector3 hit = Vector3.LerpUnclamped(a, b, Mathf.Clamp01(t));
                bool unique = true;
                for (int h = 0; h < hits.Count; h++)
                {
                    if ((hits[h] - hit).sqrMagnitude <= Epsilon * Epsilon * 100f)
                    {
                        unique = false;
                        break;
                    }
                }

                if (unique)
                {
                    hits.Add(hit);
                }
            }

            if (hits.Count < 3)
            {
                return false;
            }

            MeshHalfSpaceClipper.BuildOrthonormalBasis(n, out Vector3 tangent, out Vector3 bitangent);
            Vector3 centroid = Vector3.zero;
            for (int i = 0; i < hits.Count; i++)
            {
                centroid += hits[i];
            }

            centroid /= hits.Count;

            hits.Sort((a, b) =>
            {
                float angleA = Mathf.Atan2(Vector3.Dot(a - centroid, bitangent), Vector3.Dot(a - centroid, tangent));
                float angleB = Mathf.Atan2(Vector3.Dot(b - centroid, bitangent), Vector3.Dot(b - centroid, tangent));
                return angleA.CompareTo(angleB);
            });

            loopOut.AddRange(hits);
            return true;
        }

        private static FractureMeshBuffer BuildSourceBuffer(Mesh sourceMesh)
        {
            Vector3[] positions = sourceMesh.vertices;
            Vector3[] normals = sourceMesh.normals;
            Vector4[] tangents = sourceMesh.tangents;
            Vector2[] uvs = sourceMesh.uv;
            int[] triangles = sourceMesh.triangles;

            bool hasNormals = normals != null && normals.Length == positions.Length;
            bool hasTangents = tangents != null && tangents.Length == positions.Length;
            bool hasUvs = uvs != null && uvs.Length == positions.Length;

            FractureMeshBuffer buffer = new FractureMeshBuffer(
                Mathf.Max(positions.Length, 32),
                Mathf.Max(triangles.Length, 96));

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
                if (Vector3.Cross(ab, ac).sqrMagnitude <= Epsilon * Epsilon)
                {
                    continue;
                }

                buffer.AddTriangle(a, b, c, false);
            }

            if (!hasNormals)
            {
                RecalculateNormals(buffer);
            }

            return buffer;
        }

        private static void RecalculateNormals(FractureMeshBuffer buffer)
        {
            Vector3[] accumulated = new Vector3[buffer.Vertices.Count];
            for (int i = 0; i < buffer.ExteriorTriangles.Count; i += 3)
            {
                int ia = buffer.ExteriorTriangles[i];
                int ib = buffer.ExteriorTriangles[i + 1];
                int ic = buffer.ExteriorTriangles[i + 2];
                Vector3 a = buffer.Vertices[ia].Position;
                Vector3 b = buffer.Vertices[ib].Position;
                Vector3 c = buffer.Vertices[ic].Position;
                Vector3 normal = Vector3.Cross(b - a, c - a);
                accumulated[ia] += normal;
                accumulated[ib] += normal;
                accumulated[ic] += normal;
            }

            for (int i = 0; i < buffer.Vertices.Count; i++)
            {
                FractureVertex vertex = buffer.Vertices[i];
                vertex.Normal = accumulated[i].sqrMagnitude > Epsilon * Epsilon
                    ? accumulated[i].normalized
                    : Vector3.up;
                buffer.Vertices[i] = vertex;
            }
        }

        private static void CopyClippedInto(FractureMeshBuffer cellBuffer, FractureMeshBuffer clipped)
        {
            cellBuffer.Clear();
            cellBuffer.Vertices.AddRange(clipped.Vertices);
            cellBuffer.ExteriorTriangles.AddRange(clipped.ExteriorTriangles);
            cellBuffer.InteriorTriangles.AddRange(clipped.InteriorTriangles);
        }

        private static float LerpFloat(float a, float b, float t)
        {
            return a + (b - a) * t;
        }
    }
}

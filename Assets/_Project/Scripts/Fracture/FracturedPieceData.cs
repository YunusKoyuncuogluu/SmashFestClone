using System;
using UnityEngine;

namespace SmashFast.Fracture
{
    [Serializable]
    public struct FractureVertex
    {
        public Vector3 Position;
        public Vector3 Normal;
        public Vector4 Tangent;
        public Vector2 Uv0;

        public static FractureVertex Lerp(in FractureVertex a, in FractureVertex b, float t)
        {
            FractureVertex result;
            result.Position = Vector3.LerpUnclamped(a.Position, b.Position, t);
            result.Normal = Vector3.Normalize(Vector3.LerpUnclamped(a.Normal, b.Normal, t));
            result.Tangent = Vector4.LerpUnclamped(a.Tangent, b.Tangent, t);
            float tangentW = a.Tangent.w >= 0f ? 1f : -1f;
            if (b.Tangent.w < 0f)
            {
                tangentW = -1f;
            }

            result.Tangent.w = tangentW;
            result.Uv0 = Vector2.LerpUnclamped(a.Uv0, b.Uv0, t);
            return result;
        }
    }

    public sealed class FracturedPiece
    {
        public Mesh Mesh;
        public Vector3 LocalCentroid;
        public int SeedIndex;
        public float VolumeEstimate;
    }

    public sealed class FractureMeshBuffer
    {
        public readonly System.Collections.Generic.List<FractureVertex> Vertices;
        public readonly System.Collections.Generic.List<int> ExteriorTriangles;
        public readonly System.Collections.Generic.List<int> InteriorTriangles;

        public FractureMeshBuffer(int vertexCapacity, int triangleCapacity)
        {
            Vertices = new System.Collections.Generic.List<FractureVertex>(vertexCapacity);
            ExteriorTriangles = new System.Collections.Generic.List<int>(triangleCapacity);
            InteriorTriangles = new System.Collections.Generic.List<int>(triangleCapacity / 2);
        }

        public void Clear()
        {
            Vertices.Clear();
            ExteriorTriangles.Clear();
            InteriorTriangles.Clear();
        }

        public FractureMeshBuffer Clone()
        {
            FractureMeshBuffer clone = new FractureMeshBuffer(Vertices.Capacity, ExteriorTriangles.Capacity);
            clone.Vertices.AddRange(Vertices);
            clone.ExteriorTriangles.AddRange(ExteriorTriangles);
            clone.InteriorTriangles.AddRange(InteriorTriangles);
            return clone;
        }

        public int AddVertex(in FractureVertex vertex)
        {
            int index = Vertices.Count;
            Vertices.Add(vertex);
            return index;
        }

        public void AddTriangle(int a, int b, int c, bool interior)
        {
            if (interior)
            {
                InteriorTriangles.Add(a);
                InteriorTriangles.Add(b);
                InteriorTriangles.Add(c);
            }
            else
            {
                ExteriorTriangles.Add(a);
                ExteriorTriangles.Add(b);
                ExteriorTriangles.Add(c);
            }
        }

        public bool HasTriangles()
        {
            return ExteriorTriangles.Count >= 3 || InteriorTriangles.Count >= 3;
        }

        public Bounds ComputeBounds()
        {
            if (Vertices.Count == 0)
            {
                return new Bounds(Vector3.zero, Vector3.zero);
            }

            Vector3 min = Vertices[0].Position;
            Vector3 max = Vertices[0].Position;
            for (int i = 1; i < Vertices.Count; i++)
            {
                Vector3 p = Vertices[i].Position;
                min = Vector3.Min(min, p);
                max = Vector3.Max(max, p);
            }

            Bounds bounds = new Bounds();
            bounds.SetMinMax(min, max);
            return bounds;
        }

        public void ShiftToCentroid(out Vector3 centroid)
        {
            Bounds bounds = ComputeBounds();
            centroid = bounds.center;
            if (centroid.sqrMagnitude <= MeshFracturer.Epsilon * MeshFracturer.Epsilon)
            {
                return;
            }

            for (int i = 0; i < Vertices.Count; i++)
            {
                FractureVertex vertex = Vertices[i];
                vertex.Position -= centroid;
                Vertices[i] = vertex;
            }
        }

        public Mesh BuildUnityMesh(string meshName)
        {
            Mesh mesh = new Mesh();
            mesh.name = meshName;
            mesh.indexFormat = Vertices.Count > 65535
                ? UnityEngine.Rendering.IndexFormat.UInt32
                : UnityEngine.Rendering.IndexFormat.UInt16;

            int vertexCount = Vertices.Count;
            Vector3[] positions = new Vector3[vertexCount];
            Vector3[] normals = new Vector3[vertexCount];
            Vector4[] tangents = new Vector4[vertexCount];
            Vector2[] uvs = new Vector2[vertexCount];

            for (int i = 0; i < vertexCount; i++)
            {
                FractureVertex vertex = Vertices[i];
                positions[i] = vertex.Position;
                normals[i] = vertex.Normal;
                tangents[i] = vertex.Tangent;
                uvs[i] = vertex.Uv0;
            }

            mesh.SetVertices(positions);
            mesh.SetNormals(normals);
            mesh.SetTangents(tangents);
            mesh.SetUVs(0, uvs);
            mesh.subMeshCount = 2;
            mesh.SetTriangles(ExteriorTriangles, 0, true);
            mesh.SetTriangles(InteriorTriangles, 1, true);
            mesh.RecalculateBounds();
            return mesh;
        }
    }
}

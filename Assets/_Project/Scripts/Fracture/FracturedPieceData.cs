using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace SmashFast.Fracture
{
    [Serializable]
    public struct FractureVertex
    {
        public Vector3 Position;
        public Vector3 Normal;
        public Vector4 Tangent;
        public Vector2 Uv0;

        public FractureVertex Lerp(in FractureVertex target, float t)
        {
            FractureVertex result;
            result.Position = Vector3.LerpUnclamped(Position, target.Position, t);
            result.Normal = Vector3.Normalize(Vector3.LerpUnclamped(Normal, target.Normal, t));
            result.Tangent = Vector4.LerpUnclamped(Tangent, target.Tangent, t);
            result.Tangent.w = Tangent.w < 0f || target.Tangent.w < 0f ? -1f : 1f;
            result.Uv0 = Vector2.LerpUnclamped(Uv0, target.Uv0, t);
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
        public readonly List<FractureVertex> Vertices;
        public readonly List<int> ExteriorTriangles;
        public readonly List<int> InteriorTriangles;

        public FractureMeshBuffer(int vertexCapacity, int triangleCapacity)
        {
            Vertices = new List<FractureVertex>(vertexCapacity);
            ExteriorTriangles = new List<int>(triangleCapacity);
            InteriorTriangles = new List<int>(triangleCapacity / 2);
        }

        public bool HasTriangles() => ExteriorTriangles.Count >= 3 || InteriorTriangles.Count >= 3;

        public void Clear()
        {
            Vertices.Clear();
            ExteriorTriangles.Clear();
            InteriorTriangles.Clear();
        }

        public void CopyFrom(FractureMeshBuffer source)
        {
            Clear();
            Vertices.AddRange(source.Vertices);
            ExteriorTriangles.AddRange(source.ExteriorTriangles);
            InteriorTriangles.AddRange(source.InteriorTriangles);
        }

        public FractureMeshBuffer Clone()
        {
            FractureMeshBuffer clone = new FractureMeshBuffer(Vertices.Capacity, ExteriorTriangles.Capacity);
            clone.CopyFrom(this);
            return clone;
        }

        public int AddVertex(in FractureVertex vertex)
        {
            Vertices.Add(vertex);
            return Vertices.Count - 1;
        }

        public void AddTriangle(int a, int b, int c, bool interior)
        {
            List<int> triangles = interior ? InteriorTriangles : ExteriorTriangles;
            triangles.Add(a);
            triangles.Add(b);
            triangles.Add(c);
        }

        public Bounds ComputeBounds()
        {
            if (Vertices.Count == 0)
                return new Bounds(Vector3.zero, Vector3.zero);

            Vector3 min = Vertices[0].Position;
            Vector3 max = min;
            for (int i = 1; i < Vertices.Count; i++)
            {
                min = Vector3.Min(min, Vertices[i].Position);
                max = Vector3.Max(max, Vertices[i].Position);
            }

            Bounds bounds = new Bounds();
            bounds.SetMinMax(min, max);
            return bounds;
        }

        public void ShiftToCentroid(out Vector3 centroid)
        {
            centroid = ComputeBounds().center;
            if (centroid.sqrMagnitude <= MeshFracturer.Epsilon * MeshFracturer.Epsilon)
                return;

            for (int i = 0; i < Vertices.Count; i++)
            {
                FractureVertex vertex = Vertices[i];
                vertex.Position -= centroid;
                Vertices[i] = vertex;
            }
        }

        public Mesh BuildUnityMesh(string meshName)
        {
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

            Mesh mesh = new Mesh
            {
                name = meshName,
                indexFormat = vertexCount > 65535 ? IndexFormat.UInt32 : IndexFormat.UInt16
            };

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

using System.Collections.Generic;
using UnityEngine;

namespace SmashFast.Fracture
{
    public sealed class MeshHalfSpaceClipper
    {
        private const float Epsilon = MeshFracturer.Epsilon;
        private const float EpsilonSqr = Epsilon * Epsilon;

        private readonly List<FractureVertex> polygonInput = new List<FractureVertex>(8);
        private readonly List<FractureVertex> polygonOutput = new List<FractureVertex>(8);
        private readonly List<FractureVertex> clipScratch = new List<FractureVertex>(8);
        private readonly List<Vector3> capPoints = new List<Vector3>(64);
        private readonly List<int> capPointIndices = new List<int>(64);
        private readonly List<Vector3> loopScratch = new List<Vector3>(64);
        private readonly List<bool> edgeUsed = new List<bool>(64);
        private readonly List<Vector3> edgeA = new List<Vector3>(64);
        private readonly List<Vector3> edgeB = new List<Vector3>(64);
        private readonly List<float> distances = new List<float>(8);
        private readonly List<bool> insideFlags = new List<bool>(8);

        public void Clip(FractureMeshBuffer source, FractureMeshBuffer destination, Vector3 planePoint, Vector3 planeNormal, float interiorUVScale)
        {
            destination.Clear();
            ClearCapEdges();

            Vector3 normal = planeNormal.normalized;
            if (normal.sqrMagnitude < EpsilonSqr)
            {
                destination.CopyFrom(source);
                return;
            }

            ClipTriangleList(source, destination, source.ExteriorTriangles, false, planePoint, normal);
            ClipTriangleList(source, destination, source.InteriorTriangles, true, planePoint, normal);
            BuildCap(destination, planePoint, normal, interiorUVScale);
        }

        private void ClipTriangleList(
            FractureMeshBuffer source,
            FractureMeshBuffer destination,
            List<int> triangles,
            bool sourceIsInterior,
            Vector3 planePoint,
            Vector3 planeNormal)
        {
            int triCount = triangles.Count / 3;
            for (int t = 0; t < triCount; t++)
            {
                int i0 = triangles[t * 3];
                int i1 = triangles[t * 3 + 1];
                int i2 = triangles[t * 3 + 2];

                FractureVertex v0 = source.Vertices[i0];
                FractureVertex v1 = source.Vertices[i1];
                FractureVertex v2 = source.Vertices[i2];

                ClipTriangle(v0, v1, v2, destination, sourceIsInterior, planePoint, planeNormal);
            }
        }

        private void ClipTriangle(
            in FractureVertex v0,
            in FractureVertex v1,
            in FractureVertex v2,
            FractureMeshBuffer destination,
            bool interior,
            Vector3 planePoint,
            Vector3 planeNormal)
        {
            polygonInput.Clear();
            polygonInput.Add(v0);
            polygonInput.Add(v1);
            polygonInput.Add(v2);

            SutherlandHodgmanClip(polygonInput, polygonOutput, planePoint, planeNormal);

            if (polygonOutput.Count < 3)
                return;

            CollectCapEdgeFromClip(polygonInput, planePoint, planeNormal);

            int baseIndex = destination.Vertices.Count;
            for (int i = 0; i < polygonOutput.Count; i++)
                destination.AddVertex(polygonOutput[i]);

            for (int i = 1; i < polygonOutput.Count - 1; i++)
                destination.AddTriangle(baseIndex, baseIndex + i, baseIndex + i + 1, interior);
        }

        private void SutherlandHodgmanClip(
            List<FractureVertex> input,
            List<FractureVertex> output,
            Vector3 planePoint,
            Vector3 planeNormal)
        {
            output.Clear();
            if (input.Count == 0)
                return;

            clipScratch.Clear();
            clipScratch.AddRange(input);

            int count = clipScratch.Count;
            FillDistances(clipScratch, planePoint, planeNormal);

            for (int i = 0; i < count; i++)
            {
                int next = (i + 1) % count;
                FractureVertex current = clipScratch[i];
                FractureVertex nextVertex = clipScratch[next];
                float distCurrent = distances[i];
                float distNext = distances[next];
                bool currentInside = insideFlags[i];
                bool nextInside = insideFlags[next];

                if (currentInside)
                {
                    output.Add(current);
                    if (!nextInside)
                    {
                        float t = IntersectionT(distCurrent, distNext);
                        output.Add(current.Lerp(nextVertex, t));
                    }
                }
                else if (nextInside)
                {
                    float t = IntersectionT(distCurrent, distNext);
                    output.Add(current.Lerp(nextVertex, t));
                }
            }
        }

        private void CollectCapEdgeFromClip(List<FractureVertex> originalTriangle, Vector3 planePoint, Vector3 planeNormal)
        {
            int count = originalTriangle.Count;
            FillDistances(originalTriangle, planePoint, planeNormal);

            FractureVertex? enterPoint = null;
            FractureVertex? exitPoint = null;

            for (int i = 0; i < count; i++)
            {
                int next = (i + 1) % count;
                bool currentInside = insideFlags[i];
                bool nextInside = insideFlags[next];

                if (currentInside == nextInside)
                    continue;

                float t = IntersectionT(distances[i], distances[next]);
                FractureVertex intersection = originalTriangle[i].Lerp(originalTriangle[next], t);

                if (currentInside && !nextInside)
                    exitPoint = intersection;
                else if (!currentInside && nextInside)
                    enterPoint = intersection;
            }

            if (enterPoint.HasValue && exitPoint.HasValue)
            {
                Vector3 a = exitPoint.Value.Position;
                Vector3 b = enterPoint.Value.Position;
                if ((a - b).sqrMagnitude > EpsilonSqr)
                {
                    edgeA.Add(a);
                    edgeB.Add(b);
                }
            }
        }

        private void BuildCap(FractureMeshBuffer destination, Vector3 planePoint, Vector3 planeNormal, float interiorUVScale)
        {
            if (edgeA.Count == 0)
                return;

            Vector3 outwardNormal = -planeNormal;
            BuildOrthonormalBasis(outwardNormal, out Vector3 tangent, out Vector3 bitangent);

            if (!TryBuildClosedLoops(destination, planePoint, outwardNormal, tangent, bitangent, interiorUVScale))
                BuildCapFromAngularSort(destination, planePoint, outwardNormal, tangent, bitangent, interiorUVScale);
        }

        private bool TryBuildClosedLoops(
            FractureMeshBuffer destination,
            Vector3 planePoint,
            Vector3 outwardNormal,
            Vector3 tangent,
            Vector3 bitangent,
            float interiorUVScale)
        {
            int edgeCount = edgeA.Count;
            edgeUsed.Clear();
            for (int i = 0; i < edgeCount; i++)
                edgeUsed.Add(false);

            bool builtAny = false;
            for (int startEdge = 0; startEdge < edgeCount; startEdge++)
            {
                if (edgeUsed[startEdge])
                    continue;

                loopScratch.Clear();
                Vector3 start = edgeA[startEdge];
                Vector3 current = edgeB[startEdge];
                edgeUsed[startEdge] = true;
                loopScratch.Add(start);
                loopScratch.Add(current);

                bool closed = false;
                for (int safety = 0; safety < edgeCount; safety++)
                {
                    if ((current - start).sqrMagnitude <= EpsilonSqr * 100f)
                    {
                        if (loopScratch.Count > 1)
                            loopScratch.RemoveAt(loopScratch.Count - 1);

                        closed = loopScratch.Count >= 3;
                        break;
                    }

                    int nextEdge = FindEdgeStartingNear(current, edgeUsed);
                    if (nextEdge < 0)
                    {
                        closed = false;
                        break;
                    }

                    edgeUsed[nextEdge] = true;
                    current = edgeB[nextEdge];
                    loopScratch.Add(current);
                }

                if (!closed || loopScratch.Count < 3)
                    continue;

                EmitCapFan(destination, loopScratch, planePoint, outwardNormal, tangent, bitangent, interiorUVScale);
                builtAny = true;
            }

            return builtAny;
        }

        private int FindEdgeStartingNear(Vector3 point, List<bool> used)
        {
            float bestDist = Epsilon * 100f;
            int bestIndex = -1;
            for (int i = 0; i < edgeA.Count; i++)
            {
                if (used[i])
                    continue;

                float dist = (edgeA[i] - point).sqrMagnitude;
                if (dist < bestDist)
                {
                    bestDist = dist;
                    bestIndex = i;
                }
            }

            return bestIndex;
        }

        private void BuildCapFromAngularSort(
            FractureMeshBuffer destination,
            Vector3 planePoint,
            Vector3 outwardNormal,
            Vector3 tangent,
            Vector3 bitangent,
            float interiorUVScale)
        {
            capPoints.Clear();
            for (int i = 0; i < edgeA.Count; i++)
            {
                AddUniquePoint(capPoints, edgeA[i]);
                AddUniquePoint(capPoints, edgeB[i]);
            }

            if (capPoints.Count < 3)
                return;

            Vector3 centroid = Vector3.zero;
            for (int i = 0; i < capPoints.Count; i++)
                centroid += capPoints[i];

            centroid /= capPoints.Count;

            capPoints.Sort((a, b) =>
            {
                float angleA = Mathf.Atan2(Vector3.Dot(a - centroid, bitangent), Vector3.Dot(a - centroid, tangent));
                float angleB = Mathf.Atan2(Vector3.Dot(b - centroid, bitangent), Vector3.Dot(b - centroid, tangent));
                return angleA.CompareTo(angleB);
            });

            EmitCapFan(destination, capPoints, planePoint, outwardNormal, tangent, bitangent, interiorUVScale);
        }

        private void EmitCapFan(
            FractureMeshBuffer destination,
            List<Vector3> loop,
            Vector3 planePoint,
            Vector3 outwardNormal,
            Vector3 tangent,
            Vector3 bitangent,
            float interiorUVScale)
        {
            if (loop.Count < 3)
                return;

            Vector3 centroid = Vector3.zero;
            for (int i = 0; i < loop.Count; i++)
                centroid += loop[i];

            centroid /= loop.Count;

            Vector3 projectedCentroid = ProjectOntoPlane(centroid, planePoint, outwardNormal);
            int centroidIndex = destination.AddVertex(CreateCapVertex(projectedCentroid, planePoint, outwardNormal, tangent, bitangent, interiorUVScale));

            capPointIndices.Clear();
            for (int i = 0; i < loop.Count; i++)
            {
                Vector3 projected = ProjectOntoPlane(loop[i], planePoint, outwardNormal);
                capPointIndices.Add(destination.AddVertex(CreateCapVertex(projected, planePoint, outwardNormal, tangent, bitangent, interiorUVScale)));
            }

            for (int i = 0; i < capPointIndices.Count; i++)
            {
                int next = (i + 1) % capPointIndices.Count;
                Vector3 a = destination.Vertices[centroidIndex].Position;
                Vector3 b = destination.Vertices[capPointIndices[i]].Position;
                Vector3 c = destination.Vertices[capPointIndices[next]].Position;
                Vector3 cross = Vector3.Cross(b - a, c - a);
                if (Vector3.Dot(cross, outwardNormal) >= 0f)
                    destination.AddTriangle(centroidIndex, capPointIndices[i], capPointIndices[next], true);
                else
                    destination.AddTriangle(centroidIndex, capPointIndices[next], capPointIndices[i], true);
            }
        }

        private FractureVertex CreateCapVertex(
            Vector3 position,
            Vector3 planePoint,
            Vector3 outwardNormal,
            Vector3 tangent,
            Vector3 bitangent,
            float interiorUVScale)
        {
            Vector3 relative = position - planePoint;
            FractureVertex vertex;
            vertex.Position = position;
            vertex.Normal = outwardNormal;
            vertex.Uv0 = new Vector2(
                Vector3.Dot(relative, tangent) * interiorUVScale,
                Vector3.Dot(relative, bitangent) * interiorUVScale);
            vertex.Tangent = new Vector4(tangent.x, tangent.y, tangent.z, 1f);
            return vertex;
        }

        private void ClearCapEdges()
        {
            edgeA.Clear();
            edgeB.Clear();
            edgeUsed.Clear();
            capPoints.Clear();
            capPointIndices.Clear();
            loopScratch.Clear();
        }

        private void FillDistances(List<FractureVertex> vertices, Vector3 planePoint, Vector3 planeNormal)
        {
            distances.Clear();
            insideFlags.Clear();
            for (int i = 0; i < vertices.Count; i++)
            {
                float distance = SignedDistance(vertices[i].Position, planePoint, planeNormal);
                distances.Add(distance);
                insideFlags.Add(distance >= -Epsilon);
            }
        }

        private float SignedDistance(Vector3 point, Vector3 planePoint, Vector3 planeNormal)
        {
            return Vector3.Dot(point - planePoint, planeNormal);
        }

        private float IntersectionT(float distA, float distB)
        {
            float denom = distA - distB;
            if (Mathf.Abs(denom) <= Epsilon)
                return 0.5f;

            return Mathf.Clamp01(distA / denom);
        }

        private Vector3 ProjectOntoPlane(Vector3 point, Vector3 planePoint, Vector3 planeNormal)
        {
            float distance = Vector3.Dot(point - planePoint, planeNormal);
            return point - planeNormal * distance;
        }

        private void AddUniquePoint(List<Vector3> points, Vector3 candidate)
        {
            for (int i = 0; i < points.Count; i++)
            {
                if ((points[i] - candidate).sqrMagnitude <= EpsilonSqr * 100f)
                    return;
            }

            points.Add(candidate);
        }

        public void BuildOrthonormalBasis(Vector3 normal, out Vector3 tangent, out Vector3 bitangent)
        {
            Vector3 n = normal.normalized;
            Vector3 up = Mathf.Abs(n.y) < 0.999f ? Vector3.up : Vector3.right;
            tangent = Vector3.Normalize(Vector3.Cross(up, n));
            if (tangent.sqrMagnitude < EpsilonSqr)
                tangent = Vector3.Normalize(Vector3.Cross(Vector3.forward, n));

            bitangent = Vector3.Normalize(Vector3.Cross(n, tangent));
        }
    }
}

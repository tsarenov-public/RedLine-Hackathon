using System;
using System.Collections.Generic;
using System.Linq;
using Unity.Collections;
using UnityEditor;
using UnityEngine;

/// <summary>Editor-only geometry. Clipping uses triangles, so sparse meshes also intersect the window/clearance planes.</summary>
internal static class NFSCarColliderGeometry
{
    internal static Bounds BoundsOf(Transform node, Transform frame)
    {
        bool found = false; var result = new Bounds();
        foreach (var renderer in node.GetComponentsInChildren<Renderer>(true))
        {
            if (!(renderer is MeshRenderer) && !(renderer is SkinnedMeshRenderer)) continue;
            var bounds = renderer.localBounds;
            for (int i = 0; i < 8; i++)
            {
                var point = frame.InverseTransformPoint(renderer.transform.TransformPoint(bounds.center + Vector3.Scale(bounds.extents,
                    new Vector3((i & 1) == 0 ? -1 : 1, (i & 2) == 0 ? -1 : 1, (i & 4) == 0 ? -1 : 1))));
                if (!found) { result = new Bounds(point, Vector3.zero); found = true; } else result.Encapsulate(point);
            }
        }
        if (!found) throw new InvalidOperationException(node.name + " has no wheel/body renderer.");
        return result;
    }

    internal static List<Vector3[]> BodyTriangles(Transform body, Transform frame)
    {
        var triangles = new List<Vector3[]>();
        foreach (var renderer in body.GetComponentsInChildren<Renderer>(true))
        {
            if (IsDetail(renderer.transform, body)) continue;
            Mesh mesh = null; bool baked = false;
            if (renderer is MeshRenderer) mesh = renderer.GetComponent<MeshFilter>()?.sharedMesh;
            if (renderer is SkinnedMeshRenderer skin) { mesh = new Mesh(); skin.BakeMesh(mesh); baked = true; }
            if (!mesh) continue;
            try
            {
                // Editor API works for imported FBX with Read/Write disabled; import settings stay untouched.
                using (var data = MeshUtility.AcquireReadOnlyMeshData(mesh))
                using (var vertices = new NativeArray<Vector3>(data[0].vertexCount, Allocator.Temp))
                {
                    data[0].GetVertices(vertices);
                    var matrix = frame.worldToLocalMatrix * renderer.localToWorldMatrix;
                    for (int s = 0; s < data[0].subMeshCount; s++)
                    {
                        var sub = data[0].GetSubMesh(s);
                        if (sub.topology != MeshTopology.Triangles) continue;
                        using (var indices = new NativeArray<int>(sub.indexCount, Allocator.Temp))
                        {
                            data[0].GetIndices(indices, s);
                            for (int i = 0; i + 2 < indices.Length; i += 3)
                                triangles.Add(new[] { matrix.MultiplyPoint3x4(vertices[indices[i]]),
                                    matrix.MultiplyPoint3x4(vertices[indices[i + 1]]), matrix.MultiplyPoint3x4(vertices[indices[i + 2]]) });
                        }
                    }
                }
            }
            finally { if (baked) UnityEngine.Object.DestroyImmediate(mesh); }
        }
        if (triangles.Count == 0) throw new InvalidOperationException("No body triangles after excluding wheels/mirrors/antennas/spoilers.");
        return triangles;
    }

    private static bool IsDetail(Transform node, Transform stop)
    {
        for (var t = node; t && t != stop; t = t.parent)
        {
            string n = t.name.ToLowerInvariant();
            if (new[] { "mirror", "antenna", "aerial", "spoiler", "rearwing", "wingmount", "splitter", "wiper" }.Any(n.Contains)) return true;
        }
        return false;
    }

    internal static Mesh BuildHull(List<Vector3[]> triangles, float bottom, float top, string name)
    {
        var points = new List<Vector3>();
        foreach (var triangle in triangles)
        {
            var polygon = Clip(triangle.ToList(), bottom, true);
            polygon = Clip(polygon, top, false);
            points.AddRange(polygon);
        }
        // A bounded support sample keeps the convex cooking input below PhysX's 255-face limit.
        // Every retained point lies on the clipped body geometry; no bounding-box corners are introduced.
        var support = new HashSet<Vector3>();
        if (points.Count < 4) throw new InvalidOperationException("Body has insufficient geometry in " + name + ".");
        for (int y = -2; y <= 2; y++) for (int angle = 0; angle < 16; angle++)
        {
            float a = angle * Mathf.PI / 8;
            var direction = new Vector3(Mathf.Cos(a), y * .6f, Mathf.Sin(a));
            Vector3 best = points[0]; float distance = float.NegativeInfinity;
            foreach (var p in points) { float d = Vector3.Dot(p, direction); if (d > distance) { best = p; distance = d; } }
            support.Add(best);
        }
        support.Add(points.OrderBy(p => p.y).First()); support.Add(points.OrderByDescending(p => p.y).First());
        return ConvexHull(support.ToList(), name);
    }

    private static List<Vector3> Clip(List<Vector3> polygon, float height, bool above)
    {
        var result = new List<Vector3>();
        for (int i = 0; i < polygon.Count; i++)
        {
            var a = polygon[i]; var b = polygon[(i + 1) % polygon.Count];
            bool insideA = above ? a.y >= height : a.y <= height;
            bool insideB = above ? b.y >= height : b.y <= height;
            if (insideA) result.Add(a);
            if (insideA != insideB) result.Add(Vector3.LerpUnclamped(a, b, (height - a.y) / (b.y - a.y)));
        }
        return result;
    }

    private struct Face
    {
        internal int A, B, C;
        internal Vector3 Normal;
        internal Face(int a, int b, int c, List<Vector3> points, Vector3 interior)
        {
            A = a; B = b; C = c; Normal = Vector3.Cross(points[b] - points[a], points[c] - points[a]).normalized;
            if (Vector3.Dot(Normal, interior - points[a]) > 0) { B = c; C = b; Normal = -Normal; }
        }
    }

    // Incremental 3-D hull, with consistently outward faces and an interior seed tetrahedron.
    private static Mesh ConvexHull(List<Vector3> points, string name)
    {
        int a = 0;
        int b = Enumerable.Range(1, points.Count - 1).OrderByDescending(i => (points[i] - points[a]).sqrMagnitude).First();
        int c = Enumerable.Range(0, points.Count).OrderByDescending(i => Vector3.Cross(points[b] - points[a], points[i] - points[a]).sqrMagnitude).First();
        var normal = Vector3.Cross(points[b] - points[a], points[c] - points[a]).normalized;
        int d = Enumerable.Range(0, points.Count).OrderByDescending(i => Mathf.Abs(Vector3.Dot(normal, points[i] - points[a]))).First();
        if (Mathf.Abs(Vector3.Dot(normal, points[d] - points[a])) < .00001f)
            throw new InvalidOperationException("Degenerate/flat body hull: " + name);
        var interior = (points[a] + points[b] + points[c] + points[d]) * .25f;
        var faces = new List<Face> { new Face(a, b, c, points, interior), new Face(a, d, b, points, interior),
            new Face(a, c, d, points, interior), new Face(b, d, c, points, interior) };
        for (int i = 0; i < points.Count; i++)
        {
            var visible = faces.Where(f => Vector3.Dot(f.Normal, points[i] - points[f.A]) > .00001f).ToList();
            if (visible.Count == 0) continue;
            var edges = new Dictionary<(int, int), (int, int)>();
            foreach (var face in visible)
            {
                foreach (var edge in new[] { (face.A, face.B), (face.B, face.C), (face.C, face.A) })
                {
                    var key = (Math.Min(edge.Item1, edge.Item2), Math.Max(edge.Item1, edge.Item2));
                    if (edges.ContainsKey(key)) edges.Remove(key); else edges.Add(key, edge);
                }
                faces.Remove(face);
            }
            foreach (var edge in edges.Values) faces.Add(new Face(edge.Item1, edge.Item2, i, points, interior));
        }
        var used = faces.SelectMany(f => new[] { f.A, f.B, f.C }).Distinct().ToList();
        if (faces.Count > 254) throw new InvalidOperationException("Hull exceeds convex face budget.");
        var mesh = new Mesh { name = name };
        mesh.vertices = used.Select(i => points[i]).ToArray();
        mesh.triangles = faces.SelectMany(f => new[] { used.IndexOf(f.A), used.IndexOf(f.B), used.IndexOf(f.C) }).ToArray();
        mesh.RecalculateNormals(); mesh.RecalculateBounds();
        return mesh;
    }
}

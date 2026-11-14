using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

internal static class NfsCityGeometry
{
    internal struct Box
    {
        public Vector3 center, half;
        public Vector2 x, z;
        public Bounds bounds;

        public Box(Vector3 center, Vector3 size, Quaternion rotation)
        {
            this.center = center;
            half = size * .5f;
            Vector3 rx = rotation * Vector3.right, rz = rotation * Vector3.forward;
            x = new Vector2(rx.x, rx.z); z = new Vector2(rz.x, rz.z);
            bounds = new Bounds(center, new Vector3(
                Mathf.Abs(rx.x) * size.x + Mathf.Abs(rz.x) * size.z, size.y,
                Mathf.Abs(rx.z) * size.x + Mathf.Abs(rz.z) * size.z));
        }

        public bool Intersects(Box other, bool ignoreHeight = false)
        {
            if (!ignoreHeight && (bounds.max.y <= other.bounds.min.y || bounds.min.y >= other.bounds.max.y)) return false;
            if (bounds.max.x <= other.bounds.min.x || bounds.min.x >= other.bounds.max.x ||
                bounds.max.z <= other.bounds.min.z || bounds.min.z >= other.bounds.max.z) return false;
            Vector2 d = new Vector2(other.center.x - center.x, other.center.z - center.z);
            return OverlapAxis(d, x, this, other) && OverlapAxis(d, z, this, other) &&
                OverlapAxis(d, other.x, this, other) && OverlapAxis(d, other.z, this, other);
        }

        private static bool OverlapAxis(Vector2 d, Vector2 axis, Box a, Box b)
        {
            float r = Mathf.Abs(Vector2.Dot(a.x, axis)) * a.half.x + Mathf.Abs(Vector2.Dot(a.z, axis)) * a.half.z +
                Mathf.Abs(Vector2.Dot(b.x, axis)) * b.half.x + Mathf.Abs(Vector2.Dot(b.z, axis)) * b.half.z;
            return Mathf.Abs(Vector2.Dot(d, axis)) < r - .001f;
        }

        public Vector3 At(float u, float v)
        {
            return center + new Vector3(x.x, 0, x.y) * (u * half.x) + new Vector3(z.x, 0, z.y) * (v * half.z);
        }
    }

    // Bounds broad phase followed by yaw-only oriented-box SAT. Road queries ignore Y
    // so a footprint can never protrude into asphalt, including between spline points.
    internal sealed class Grid
    {
        private const float CellSize = 32;
        internal readonly List<Box> boxes = new List<Box>();
        private readonly Dictionary<Vector2Int, List<int>> cells = new Dictionary<Vector2Int, List<int>>();
        private readonly HashSet<int> visited = new HashSet<int>();

        public void Add(Box box)
        {
            int index = boxes.Count; boxes.Add(box);
            var lo = Cell(box.bounds.min); var hi = Cell(box.bounds.max);
            for (int x = lo.x; x <= hi.x; x++) for (int y = lo.y; y <= hi.y; y++)
            {
                var key = new Vector2Int(x, y);
                if (!cells.TryGetValue(key, out var list)) cells.Add(key, list = new List<int>());
                list.Add(index);
            }
        }

        public bool Intersects(Box box, bool ignoreHeight = false)
        {
            visited.Clear();
            var lo = Cell(box.bounds.min); var hi = Cell(box.bounds.max);
            for (int x = lo.x; x <= hi.x; x++) for (int y = lo.y; y <= hi.y; y++)
            {
                if (!cells.TryGetValue(new Vector2Int(x, y), out var list)) continue;
                foreach (int index in list)
                    if (visited.Add(index) && box.Intersects(boxes[index], ignoreHeight)) return true;
            }
            return false;
        }

        private static Vector2Int Cell(Vector3 p) => new Vector2Int(Mathf.FloorToInt(p.x / CellSize), Mathf.FloorToInt(p.z / CellSize));
    }

    internal sealed class Path
    {
        public readonly Vector3[] centers, left, right;
        public readonly float[] distances;
        public readonly float length;
        public readonly bool closed;
        public readonly float width;
        public readonly float sampledWidth;

        public Path(Vector3[] points, Vector3[] leftEdge, Vector3[] rightEdge, float width, bool closed)
        {
            if (points == null || points.Length < 2 || leftEdge == null || rightEdge == null ||
                leftEdge.Length != points.Length || rightEdge.Length != points.Length)
                throw new InvalidOperationException("EasyRoads spline/edge samples are missing or inconsistent. No road rebuild was attempted.");
            if (width <= 0 || !Finite(width)) throw new InvalidOperationException("Invalid road width.");
            this.width = width; this.closed = closed;
            var c = new List<Vector3>(); var l = new List<Vector3>(); var r = new List<Vector3>();
            for (int i = 0; i < points.Length; i++)
            {
                if (!Finite(points[i]) || !Finite(leftEdge[i]) || !Finite(rightEdge[i]))
                    throw new InvalidOperationException("Road spline contains non-finite coordinates.");
                if (c.Count > 0 && (points[i] - c[c.Count - 1]).sqrMagnitude < .000001f) continue;
                c.Add(points[i]); l.Add(leftEdge[i]); r.Add(rightEdge[i]);
            }
            if (c.Count < 2) throw new InvalidOperationException("Road has no usable length.");
            if (closed && (c[0] - c[c.Count - 1]).sqrMagnitude > .000001f)
            { c.Add(c[0]); l.Add(l[0]); r.Add(r[0]); }
            centers = c.ToArray(); left = l.ToArray(); right = r.ToArray();
            sampledWidth = HorizontalDistance(left[left.Length / 2], right[right.Length / 2]);
            distances = new float[centers.Length];
            for (int i = 1; i < centers.Length; i++) distances[i] = distances[i - 1] + Vector3.Distance(centers[i - 1], centers[i]);
            length = distances[distances.Length - 1];
        }

        public Pose Sample(float distance, int side, float extra = 0)
        {
            distance = Mathf.Clamp(distance, 0, length);
            int index = Array.BinarySearch(distances, distance);
            if (index < 0) index = ~index - 1;
            index = Mathf.Clamp(index, 0, distances.Length - 2);
            float t = Mathf.InverseLerp(distances[index], distances[index + 1], distance);
            Vector3 center = Vector3.Lerp(centers[index], centers[index + 1], t);
            var edgePoints = side < 0 ? left : right;
            Vector3 edge = Vector3.Lerp(edgePoints[index], edgePoints[index + 1], t);
            Vector3 outward = edge - center; outward.y = 0;
            if (outward.sqrMagnitude < .01f) throw new InvalidOperationException("Road edge coincides with its center.");
            outward.Normalize();
            Vector3 tangent = centers[index + 1] - centers[index]; tangent.y = 0;
            if (tangent.sqrMagnitude < .0001f) throw new InvalidOperationException("Road contains a vertical segment.");
            tangent.Normalize();
            Vector3 yawOutward = Vector3.Cross(Vector3.up, tangent);
            if (Vector3.Dot(yawOutward, outward) < 0) yawOutward = -yawOutward;
            return new Pose { center = center, edge = edge + outward * extra, outward = outward,
                tangent = tangent, rotation = Quaternion.Euler(0, Mathf.Atan2(-yawOutward.x, -yawOutward.z) * Mathf.Rad2Deg, 0) };
        }

        public Grid RoadExclusion(float padding)
        {
            var grid = new Grid();
            for (int i = 1; i < centers.Length; i++)
            {
                Vector3 delta = centers[i] - centers[i - 1]; delta.y = 0;
                float lengthXZ = delta.magnitude;
                if (lengthXZ < .0001f) continue;
                float halfWidth = Mathf.Max(HorizontalDistance(centers[i], left[i]), HorizontalDistance(centers[i], right[i]),
                    HorizontalDistance(centers[i - 1], left[i - 1]), HorizontalDistance(centers[i - 1], right[i - 1])) + padding;
                // Extended ends also contain the round end caps of the swept road width.
                var rotation = Quaternion.Euler(0, Mathf.Atan2(delta.x, delta.z) * Mathf.Rad2Deg, 0);
                grid.Add(new Box((centers[i - 1] + centers[i]) * .5f,
                    new Vector3(halfWidth * 2, 1, lengthXZ + 2 * halfWidth), rotation));
            }
            return grid;
        }
    }

    internal struct Pose { public Vector3 center, edge, outward, tangent; public Quaternion rotation; }
    internal static float HorizontalDistance(Vector3 a, Vector3 b) => new Vector2(a.x - b.x, a.z - b.z).magnitude;
    internal static bool Finite(float f) => !float.IsNaN(f) && !float.IsInfinity(f);
    private static bool Finite(Vector3 v) => Finite(v.x) && Finite(v.y) && Finite(v.z);

    // A closed rectangular extrusion, shared vertices along the length and no
    // per-segment box colliders: the driving-facing collision surface stays continuous.
    internal static Mesh Ribbon(Path path, int side, float offset, float thickness, float height,
        Func<float, bool> includeSegment = null)
    {
        int steps = Mathf.CeilToInt(path.length / 1.5f);
        var vertices = new List<Vector3>(); var triangles = new List<int>(); var uvs = new List<Vector2>();
        bool running = false; int previous = -1;
        for (int i = 0; i < steps; i++)
        {
            float d0 = path.length * i / steps, d1 = path.length * (i + 1) / steps;
            if (includeSegment != null && !includeSegment((d0 + d1) * .5f))
            {
                if (running) Cap(previous, false, triangles);
                running = false; continue;
            }
            if (!running)
            {
                previous = Ring(path.Sample(d0, side, offset), d0, thickness, height, vertices, uvs);
                Cap(previous, true, triangles); running = true;
            }
            int next = Ring(path.Sample(d1, side, offset), d1, thickness, height, vertices, uvs);
            for (int e = 0; e < 4; e++)
            {
                int f = (e + 1) % 4;
                triangles.Add(previous + e); triangles.Add(next + e); triangles.Add(next + f);
                triangles.Add(previous + e); triangles.Add(next + f); triangles.Add(previous + f);
            }
            previous = next;
        }
        if (running) Cap(previous, false, triangles);
        // Mirroring right to left reverses winding. Correct it for exterior faces.
        if (side > 0) for (int i = 0; i < triangles.Count; i += 3)
        { int swap = triangles[i]; triangles[i] = triangles[i + 1]; triangles[i + 1] = swap; }
        var mesh = new Mesh { name = "NFS continuous ribbon", indexFormat = IndexFormat.UInt32 };
        mesh.SetVertices(vertices); mesh.SetTriangles(triangles, 0); mesh.SetUVs(0, uvs);
        mesh.RecalculateBounds(); mesh.RecalculateNormals();
        return mesh;
    }

    private static int Ring(Pose p, float distance, float thickness, float height, List<Vector3> vertices, List<Vector2> uvs)
    {
        int first = vertices.Count;
        vertices.Add(p.edge); vertices.Add(p.edge + p.outward * thickness);
        vertices.Add(p.edge + p.outward * thickness + Vector3.up * height); vertices.Add(p.edge + Vector3.up * height);
        for (int j = 0; j < 4; j++) uvs.Add(new Vector2(distance / 4f, j * .33f));
        return first;
    }

    private static void Cap(int i, bool start, List<int> triangles)
    {
        if (start) { triangles.Add(i); triangles.Add(i + 2); triangles.Add(i + 1); triangles.Add(i); triangles.Add(i + 3); triangles.Add(i + 2); }
        else { triangles.Add(i); triangles.Add(i + 1); triangles.Add(i + 2); triangles.Add(i); triangles.Add(i + 2); triangles.Add(i + 3); }
    }
}

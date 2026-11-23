using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Arcade.Vehicle;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

internal static class RaceCourseAuthoring
{
    private const string MapPath = "Assets/Cars/MinimapHUD/Stage_0_Roads.png";
    private const int MapSize = 1536;

    // Centerline sampled from the drivable Road Network meshes, starting beside the Stage_0 grid.
    private static readonly Vector2[] StageRoute =
    {
        new(628.60f, -414.00f),
        new(628.33f, -388.33f),
        new(628.07f, -362.67f),
        new(627.80f, -337.00f),
        new(627.53f, -311.33f),
        new(627.27f, -285.67f),
        new(627.00f, -260.00f),
        new(627.17f, -234.67f),
        new(627.33f, -209.33f),
        new(627.50f, -184.00f),
        new(627.67f, -158.67f),
        new(627.83f, -133.33f),
        new(628.00f, -108.00f),
        new(651.29f, -108.00f),
        new(674.57f, -108.00f),
        new(697.86f, -108.00f),
        new(721.14f, -108.00f),
        new(744.43f, -108.00f),
        new(767.71f, -108.00f),
        new(791.00f, -108.00f),
        new(807.00f, -109.00f),
        new(823.00f, -110.00f),
        new(839.00f, -114.00f),
        new(853.50f, -128.00f),
        new(868.00f, -142.00f),
        new(870.00f, -166.00f),
        new(866.00f, -175.00f),
        new(851.50f, -189.50f),
        new(837.00f, -204.00f),
        new(822.00f, -210.00f),
        new(801.00f, -215.00f),
        new(780.00f, -220.00f),
        new(768.50f, -231.50f),
        new(757.00f, -243.00f),
        new(754.00f, -252.00f),
        new(752.00f, -266.00f),
        new(750.00f, -280.00f),
        new(749.17f, -301.83f),
        new(748.33f, -323.67f),
        new(747.50f, -345.50f),
        new(746.67f, -367.33f),
        new(745.83f, -389.17f),
        new(745.00f, -411.00f),
        new(742.00f, -417.00f),
        new(745.00f, -424.00f),
        new(744.89f, -448.89f),
        new(744.78f, -473.78f),
        new(744.67f, -498.67f),
        new(744.56f, -523.56f),
        new(744.44f, -548.44f),
        new(744.33f, -573.33f),
        new(744.22f, -598.22f),
        new(744.11f, -623.11f),
        new(744.00f, -648.00f),
        new(722.00f, -647.80f),
        new(700.00f, -647.60f),
        new(678.00f, -647.40f),
        new(656.00f, -647.20f),
        new(634.00f, -647.00f),
        new(631.00f, -645.00f),
        new(630.73f, -619.33f),
        new(630.47f, -593.67f),
        new(630.20f, -568.00f),
        new(629.93f, -542.33f),
        new(629.67f, -516.67f),
        new(629.40f, -491.00f),
        new(629.13f, -465.33f),
        new(628.87f, -439.67f),
    };

    internal static void Bake(Scene scene)
    {
        var network = scene.GetRootGameObjects().Single(root => root.name == "Road Network");
        var triangles = ReadRoads(network);
        if (triangles.Count == 0) throw new InvalidOperationException("No Stage_0 road triangles.");
        var min = new Vector2(float.PositiveInfinity, float.PositiveInfinity);
        var max = new Vector2(float.NegativeInfinity, float.NegativeInfinity);
        foreach (var triangle in triangles)
        {
            min = Vector2.Min(min, triangle.Min);
            max = Vector2.Max(max, triangle.Max);
        }
        Vector2 midpoint = (min + max) * .5f;
        float side = Mathf.Max(max.x - min.x, max.y - min.y) + 30f;
        var bounds = Vector2.one * side;
        min = midpoint - bounds * .5f;

        var roadMap = Rasterize(triangles, min, side);
        var roots = scene.GetRootGameObjects();
        var path = roots.SelectMany(gameObject => gameObject.GetComponentsInChildren<WaypointPath>(true))
            .FirstOrDefault(item => item.name == "Race_Waypoints");
        if (path == null)
        {
            var pathObject = SceneObject(scene, "Race_Waypoints");
            path = pathObject.AddComponent<WaypointPath>();
            for (int i = 0; i < StageRoute.Length; i++)
            {
                var position = StageRoute[i];
                float surface = RoadHeight(triangles, position);
                var waypoint = new GameObject($"WP_{i + 1:D2}").transform;
                waypoint.SetParent(path.transform, false);
                waypoint.position = new Vector3(position.x, surface + .15f, position.y);
            }
        }

        var course = roots.Select(root => root.GetComponent<RaceMinimapCourse>()).FirstOrDefault(item => item != null);
        if (course == null) course = SceneObject(scene, "Race Radar Course").AddComponent<RaceMinimapCourse>();
        course.roadMap = roadMap;
        course.mapMin = min;
        course.mapSize = bounds;
        course.route = path;
        course.closedLoop = true;
        int reclassified = EnableRouteSurfaces(triangles, path.GetWaypoints());
        Debug.Log($"RACE_HUD road map {triangles.Count} triangles / {path.GetWaypoints().Count} route points / {reclassified} road layers fixed.");
    }

    private static GameObject SceneObject(Scene scene, string name)
    {
        var result = new GameObject(name);
        SceneManager.MoveGameObjectToScene(result, scene);
        return result;
    }

    private static List<Triangle> ReadRoads(GameObject network)
    {
        var triangles = new List<Triangle>();
        foreach (var collider in network.GetComponentsInChildren<MeshCollider>())
        {
            string group = collider.transform.parent?.name;
            if (!collider.enabled || collider.sharedMesh == null || group is not ("Road Objects" or "Connection Objects"))
                continue;
            var mesh = collider.sharedMesh;
            var vertices = mesh.vertices;
            for (int i = 0; i < vertices.Length; i++) vertices[i] = collider.transform.TransformPoint(vertices[i]);
            int[] indices = mesh.triangles;
            for (int i = 0; i < indices.Length; i += 3)
            {
                var triangle = new Triangle(vertices[indices[i]], vertices[indices[i + 1]], vertices[indices[i + 2]], collider);
                if (Mathf.Abs(triangle.Area) > .01f) triangles.Add(triangle);
            }
        }
        return triangles;
    }

    private static Texture2D Rasterize(List<Triangle> triangles, Vector2 min, float side)
    {
        var image = new Color32[MapSize * MapSize];
        float scale = MapSize / side;
        foreach (var triangle in triangles)
        {
            var low = (triangle.Min - min) * scale;
            var high = (triangle.Max - min) * scale;
            for (int y = Mathf.Max(0, Mathf.FloorToInt(low.y)); y < Mathf.Min(MapSize, Mathf.CeilToInt(high.y)); y++)
                for (int x = Mathf.Max(0, Mathf.FloorToInt(low.x)); x < Mathf.Min(MapSize, Mathf.CeilToInt(high.x)); x++)
                {
                    var point = new Vector2(x + .5f, y + .5f) / scale + min;
                    if (triangle.TryHeight(point, out _)) image[y * MapSize + x] = new Color32(77, 79, 81, 255);
                }
        }
        var texture = new Texture2D(MapSize, MapSize, TextureFormat.RGBA32, false);
        texture.SetPixels32(image);
        texture.Apply();
        File.WriteAllBytes(MapPath, texture.EncodeToPNG());
        UnityEngine.Object.DestroyImmediate(texture);
        AssetDatabase.ImportAsset(MapPath, ImportAssetOptions.ForceSynchronousImport);
        var importer = (TextureImporter)AssetImporter.GetAtPath(MapPath);
        importer.wrapMode = TextureWrapMode.Clamp;
        importer.alphaIsTransparency = true;
        importer.mipmapEnabled = false;
        importer.npotScale = TextureImporterNPOTScale.None;
        importer.textureCompression = TextureImporterCompression.Uncompressed;
        importer.SaveAndReimport();
        return AssetDatabase.LoadAssetAtPath<Texture2D>(MapPath);
    }

    private static float RoadHeight(List<Triangle> triangles, Vector2 point)
    {
        if (FindSurface(triangles, point, out float height) == null)
            throw new InvalidOperationException($"Waypoint {point} is outside the road mesh.");
        return height;
    }

    private static int EnableRouteSurfaces(List<Triangle> triangles, List<Transform> points)
    {
        int ground = LayerMask.NameToLayer("Ground");
        if (ground < 0) return 0;
        var updated = new HashSet<GameObject>();
        for (int i = 0; i < points.Count; i++)
        {
            var a = Xz(points[i].position);
            var b = Xz(points[(i + 1) % points.Count].position);
            int steps = Mathf.Max(1, Mathf.CeilToInt(Vector2.Distance(a, b)));
            for (int j = 0; j < steps; j++)
            {
                var surface = FindSurface(triangles, Vector2.Lerp(a, b, j / (float)steps), out _);
                if (surface == null || surface.gameObject.layer != 0 || !updated.Add(surface.gameObject)) continue;
                surface.gameObject.layer = ground;
                Debug.Log("RACE_HUD route surface set to Ground " + surface.name);
            }
        }
        return updated.Count;
    }

    private static MeshCollider FindSurface(List<Triangle> triangles, Vector2 point, out float height)
    {
        MeshCollider result = null;
        height = float.NegativeInfinity;
        foreach (var triangle in triangles)
        {
            if (!triangle.TryHeight(point, out float sample) || sample <= height) continue;
            height = sample;
            result = triangle.Surface;
        }
        return result;
    }

    private static Vector2 Xz(Vector3 point) => new(point.x, point.z);

    private readonly struct Triangle
    {
        readonly Vector3 a, b, c;
        readonly Vector2 origin, ab, ac;
        public readonly Vector2 Min, Max;
        public readonly float Area;
        public readonly MeshCollider Surface;

        public Triangle(Vector3 a, Vector3 b, Vector3 c, MeshCollider surface)
        {
            this.a = a; this.b = b; this.c = c;
            Surface = surface;
            origin = Xz(a);
            var p = Xz(b);
            var q = Xz(c);
            ab = p - origin;
            ac = q - origin;
            Min = Vector2.Min(origin, Vector2.Min(p, q));
            Max = Vector2.Max(origin, Vector2.Max(p, q));
            Area = Cross(ab, ac);
        }

        public bool TryHeight(Vector2 point, out float y)
        {
            y = 0;
            if (point.x < Min.x || point.x > Max.x || point.y < Min.y || point.y > Max.y) return false;
            var d = point - origin;
            float u = Cross(d, ac) / Area, v = Cross(ab, d) / Area;
            if (u < -.00001f || v < -.00001f || u + v > 1.00001f) return false;
            y = a.y + (b.y - a.y) * u + (c.y - a.y) * v;
            return true;
        }

        static float Cross(Vector2 x, Vector2 y) => x.x * y.y - x.y * y.x;
    }
}

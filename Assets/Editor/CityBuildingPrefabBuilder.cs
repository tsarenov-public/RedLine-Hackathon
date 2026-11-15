using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Unity.Collections;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

/// <summary>Root-local, half-metre wall footprints. Does not change model import settings.</summary>
public static class CityBuildingPrefabBuilder
{
    public const string PrefabFolder = "Assets/Prefab/buildings/City";
    private const string Menu = "Tools/RacingCity/";
    private const float Cell = 0.5f;
    private const float Epsilon = 0.0001f;
    private const int MaxCells = 1000000;
    private static readonly string[] GeneratedNames = { "Foundations", "ShadowProxies", "Colliders" };

    [MenuItem(Menu + "Setup Foundations, Shadows & Colliders")]
    public static void SetupAll() => Process(AssetDatabase.FindAssets("t:Prefab", new[] { PrefabFolder })
        .Select(AssetDatabase.GUIDToAssetPath));

    [MenuItem(Menu + "Setup Selected Foundations, Shadows & Colliders")]
    public static void SetupSelected() => Process(SelectedPaths());

    [MenuItem(Menu + "Setup Selected Foundations, Shadows & Colliders", true)]
    private static bool ValidateSelected() => !EditorApplication.isPlayingOrWillChangePlaymode && SelectedPaths().Any();

    private static IEnumerable<string> SelectedPaths() => Selection.objects.Select(AssetDatabase.GetAssetPath)
        .Where(p => p.StartsWith(PrefabFolder + "/", StringComparison.Ordinal) &&
                    p.EndsWith(".prefab", StringComparison.OrdinalIgnoreCase)).Distinct();

    private static void Process(IEnumerable<string> paths)
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            Debug.LogWarning("[RacingCity] Exit Play Mode before editing prefab assets.");
            return;
        }
        string[] assets = paths.Distinct().OrderBy(p => p, StringComparer.Ordinal).ToArray();
        if (assets.Length == 0) { Debug.LogWarning("[RacingCity] No City prefab assets found/selected."); return; }
        Material material = FindFoundationMaterial();
        if (!material) { Debug.LogError("[RacingCity] No Concrete/Asphalt material found under Assets/BuildingsAsset. Nothing saved."); return; }
        int saved = 0, failed = 0;
        try
        {
            for (int i = 0; i < assets.Length; i++)
            {
                if (EditorUtility.DisplayCancelableProgressBar("RacingCity", assets[i], (float)i / assets.Length)) break;
                GameObject root = null;
                try
                {
                    if (!AssetDatabase.IsOpenForEdit(assets[i])) throw new InvalidOperationException("Prefab is read-only.");
                    root = PrefabUtility.LoadPrefabContents(assets[i]);
                    int count = Build(root, material);
                    PrefabUtility.SaveAsPrefabAsset(root, assets[i], out bool success);
                    if (!success) throw new IOException("SaveAsPrefabAsset failed.");
                    saved++;
                    Debug.Log($"[RacingCity] {Path.GetFileNameWithoutExtension(assets[i])}: {count} BoxCollider(s), " +
                              $"foundation depth {root.GetComponent<BuildingFoundationController>().foundationDepth:F2} m.",
                        AssetDatabase.LoadAssetAtPath<GameObject>(assets[i]));
                }
                catch (Exception e) { failed++; Debug.LogError($"[RacingCity] {assets[i]}: {e.Message} (not saved)."); }
                finally { if (root) PrefabUtility.UnloadPrefabContents(root); }
            }
        }
        finally { EditorUtility.ClearProgressBar(); }
        Debug.Log($"[RacingCity] Saved {saved}/{assets.Length}; failed {failed}; material: {material.name}.");
    }

    /// <summary>Builds in memory; caller owns saving/unloading. Analysis finishes before cleanup.</summary>
    public static int Build(GameObject root, Material material)
    {
        if (!root || !material) throw new ArgumentNullException(!root ? nameof(root) : nameof(material));
        var renderers = FindLODRenderers(root, 2);
        if (renderers.Count == 0) renderers = FindLODRenderers(root, 0);
        if (renderers.Count == 0) throw new InvalidOperationException("No LOD2 or LOD0 meshes found.");
        var geometry = ReadGeometry(root.transform, renderers);
        Grid collisionGrid = MakeFootprint(geometry, 0.5f, 4.5f);
        List<Rect> collisionRects = RemoveThinParts(collisionGrid, root.name + " colliders");
        Grid foundationGrid = MakeFootprint(geometry, 0f, 3.5f);
        IntersectFootprints(foundationGrid, collisionGrid);
        List<Rect> foundationRects = RemoveThinParts(foundationGrid, root.name + " foundations");
        var shadowRects = new List<Rect>();
        var shadowTops = new List<float>();
        // Use the intersection of the LOD silhouettes so proxies cannot extend past a coarser roof.
        var shadowGeometry = new List<Geometry> { geometry };
        for (int lod = 0; lod <= 2; lod++)
        {
            var other = FindLODRenderers(root, lod);
            if (other.Count > 0 && !other.SetEquals(renderers)) shadowGeometry.Add(ReadGeometry(root.transform, other));
        }
        foundationRects = foundationRects.Select(r => FitToWalls(collisionGrid, FitToWalls(foundationGrid, r, 0.05f), 0.05f)).ToList();
        var shadowGrids = shadowGeometry.Select(g => MakeFootprint(g, 0.5f, 4.5f)).ToList();
        foreach (Rect rect in collisionRects)
        {
            Rect inset = rect;
            foreach (Grid grid in shadowGrids) inset = FitToWalls(grid, inset, 0.15f);
            AddShadowRegions(inset, shadowGeometry, shadowRects, shadowTops, root.name);
        }
        if (shadowRects.Count == 0) throw new InvalidOperationException("No common roof-covered space in the building LODs.");

        // Nested model/prefab children must be unpacked to remove old generated objects/components.
        foreach (Transform child in root.transform.Cast<Transform>().ToArray())
            if (PrefabUtility.IsPartOfPrefabInstance(child.gameObject))
                PrefabUtility.UnpackPrefabInstance(child.gameObject, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
        foreach (Transform child in root.GetComponentsInChildren<Transform>(true).Reverse())
            if (child && child != root.transform && GeneratedNames.Contains(child.name)) Object.DestroyImmediate(child.gameObject);
        foreach (var collider in root.GetComponentsInChildren<Collider>(true)) Object.DestroyImmediate(collider);
        foreach (var renderer in root.GetComponentsInChildren<Renderer>(true)) renderer.shadowCastingMode = ShadowCastingMode.Off;

        Transform foundations = Container(root.transform, "Foundations");
        Transform shadows = Container(root.transform, "ShadowProxies");
        Transform colliders = Container(root.transform, "Colliders");
        var cubes = new List<Transform>();
        var proxies = new List<Transform>();
        var boxes = new List<BoxCollider>();
        for (int i = 0; i < foundationRects.Count; i++)
            cubes.Add(Cube(foundations, "Foundation_" + (i + 1), foundationRects[i],
                0f, BuildingFoundationController.FoundationTop, material, ShadowCastingMode.Off));
        for (int i = 0; i < shadowRects.Count; i++)
            proxies.Add(Cube(shadows, "ShadowProxy_" + (i + 1), shadowRects[i], 0f, shadowTops[i],
                material, ShadowCastingMode.ShadowsOnly));
        foreach (Rect rect in collisionRects)
        {
            var box = colliders.gameObject.AddComponent<BoxCollider>();
            box.center = new Vector3(rect.center.x, 2.5f, rect.center.y);
            box.size = new Vector3(rect.width, 5f, rect.height);
            boxes.Add(box);
        }
        var controller = root.GetComponent<BuildingFoundationController>();
        if (!controller) controller = root.AddComponent<BuildingFoundationController>();
        controller.BindGeneratedObjects(cubes.ToArray(), proxies.ToArray(), boxes.ToArray());
        foreach (var transform in root.GetComponentsInChildren<Transform>(true))
            GameObjectUtility.SetStaticEditorFlags(transform.gameObject, transform == colliders
                ? 0 : (StaticEditorFlags)(-1));
        if (foundationRects.Count > 3 || collisionRects.Count > 3 || proxies.Count > 3)
            Debug.LogWarning($"[RacingCity] {root.name}: complex outline requires {foundationRects.Count} foundations / " +
                             $"{collisionRects.Count} colliders / {proxies.Count} shadow proxies; empty courtyards have been preserved.");
        return boxes.Count;
    }

    private static HashSet<Renderer> FindLODRenderers(GameObject root, int level)
    {
        var result = new HashSet<Renderer>();
        foreach (var group in root.GetComponentsInChildren<LODGroup>(true))
        {
            LOD[] lods = group.GetLODs();
            if (level < lods.Length)
                foreach (var renderer in lods[level].renderers)
                    if (renderer && renderer.transform.IsChildOf(root.transform) && !IsGenerated(renderer.transform, root.transform))
                        result.Add(renderer);
        }
        // Suffix fallback is independent of the prefab's name, and supports a LOD container.
        if (result.Count == 0)
            foreach (var renderer in root.GetComponentsInChildren<MeshRenderer>(true))
                for (Transform t = renderer.transform; t && t != root.transform; t = t.parent)
                    if (t.name.EndsWith("_LOD" + level, StringComparison.OrdinalIgnoreCase) && !IsGenerated(t, root.transform))
                    { result.Add(renderer); break; }
        return result;
    }

    private static bool IsGenerated(Transform t, Transform root)
    {
        for (; t && t != root; t = t.parent) if (GeneratedNames.Contains(t.name)) return true;
        return false;
    }

    private sealed class Geometry
    {
        public readonly List<Vector3> Vertices = new List<Vector3>();
        public readonly List<int> Indices = new List<int>();
    }

    private static Geometry ReadGeometry(Transform root, IEnumerable<Renderer> renderers)
    {
        var result = new Geometry();
        foreach (Renderer renderer in renderers)
        {
            var filter = renderer.GetComponent<MeshFilter>();
            if (!filter || !filter.sharedMesh) throw new InvalidOperationException("LOD renderer has no static mesh: " + renderer.name);
            Mesh mesh = filter.sharedMesh;
            Matrix4x4 matrix = root.worldToLocalMatrix * filter.transform.localToWorldMatrix;
            // Unlike mesh.vertices, this works with imported Read/Write-disabled FBX assets.
            using (var dataArray = MeshUtility.AcquireReadOnlyMeshData(mesh))
            using (var vertices = new NativeArray<Vector3>(mesh.vertexCount, Allocator.Temp))
            {
                Mesh.MeshData data = dataArray[0];
                data.GetVertices(vertices);
                int offset = result.Vertices.Count;
                foreach (Vector3 vertex in vertices) result.Vertices.Add(matrix.MultiplyPoint3x4(vertex));
                for (int sub = 0; sub < data.subMeshCount; sub++)
                {
                    var descriptor = data.GetSubMesh(sub);
                    if (descriptor.topology != MeshTopology.Triangles) continue;
                    using (var indices = new NativeArray<int>(descriptor.indexCount, Allocator.Temp))
                    {
                        data.GetIndices(indices, sub, true);
                        foreach (int index in indices) result.Indices.Add(offset + index);
                    }
                }
            }
        }
        if (result.Indices.Count == 0) throw new InvalidOperationException("No triangles in the selected LOD.");
        return result;
    }

    private struct Segment
    {
        public Vector2 A, B;
        public int Direction, OtherDirection;
        public Segment(Vector3 a, Vector3 b, Vector3 normal)
        {
            A = new Vector2(a.x, a.z); B = new Vector2(b.x, b.z);
            Direction = Math.Sign(normal.x); OtherDirection = Math.Sign(normal.z);
        }
    }

    private sealed class Grid
    {
        public bool[,] Cells;
        public List<List<Segment>> Sections;
        public float X, Z;
        public int Width => Cells.GetLength(0);
        public int Height => Cells.GetLength(1);
    }

    private static Grid MakeFootprint(Geometry geometry, float minY, float maxY)
    {
        // Intersect triangle edges with planes INSIDE the permitted slab. This also handles
        // sparse walls having vertices only at y=0 and the roof, without projecting upper floors.
        var sections = new List<List<Segment>>();
        for (int sample = 0; sample <= 8; sample++)
        {
            float y = Mathf.Lerp(minY + 0.001f, maxY - 0.001f, sample / 8f);
            var section = new List<Segment>();
            for (int i = 0; i < geometry.Indices.Count; i += 3)
            {
                Vector3 a = geometry.Vertices[geometry.Indices[i]], b = geometry.Vertices[geometry.Indices[i + 1]],
                    c = geometry.Vertices[geometry.Indices[i + 2]];
                var hits = new List<Vector3>(3);
                IntersectEdge(a, b, y, hits); IntersectEdge(b, c, y, hits); IntersectEdge(c, a, y, hits);
                if (hits.Count == 2 && (hits[0] - hits[1]).sqrMagnitude > Epsilon * Epsilon)
                    section.Add(new Segment(hits[0], hits[1], Vector3.Cross(b - a, c - a)));
            }
            if (section.Count > 0) sections.Add(section);
        }
        var points = sections.SelectMany(s => s).SelectMany(s => new[] { s.A, s.B }).ToArray();
        if (points.Length == 0) throw new InvalidOperationException($"No wall cross-section at Y={minY}..{maxY}.");
        float originX = Mathf.Floor(points.Min(p => p.x) / Cell) * Cell;
        float originZ = Mathf.Floor(points.Min(p => p.y) / Cell) * Cell;
        int width = Mathf.CeilToInt((points.Max(p => p.x) - originX) / Cell);
        int height = Mathf.CeilToInt((points.Max(p => p.y) - originZ) / Cell);
        if (width <= 0 || height <= 0 || (long)width * height > MaxCells)
            throw new InvalidOperationException("Invalid/oversized footprint; check model units and root scale.");
        var grid = new Grid { Cells = new bool[width, height], X = originX, Z = originZ, Sections = sections };
        var votes = new int[width, height];
        // Nonzero winding fills overlapping closed mesh parts and preserves courtyards. Rasterizing
        // vertices alone would leave almost every cell of a plain rectangular building empty.
        foreach (var section in sections)
        {
            var occupied = new bool[width, height];
            for (int z = 0; z < height; z++)
            {
                float worldZ = originZ + (z + 0.5f) * Cell;
                var crossings = new List<Vector2>();
                foreach (Segment segment in section)
                {
                    if ((segment.A.y > worldZ) == (segment.B.y > worldZ)) continue;
                    crossings.Add(new Vector2(Mathf.Lerp(segment.A.x, segment.B.x,
                        (worldZ - segment.A.y) / (segment.B.y - segment.A.y)), segment.Direction));
                }
                crossings.Sort((a, b) => a.x.CompareTo(b.x));
                // Unbalanced winding indicates an open/non-manifold facade. Never substitute a
                // bounding box, which would silently block an entire street/courtyard.
                if (Mathf.Abs(crossings.Sum(c => c.y)) > 0.1f) continue;
                int winding = 0;
                float left = 0f;
                for (int i = 0; i < crossings.Count;)
                {
                    float right = crossings[i].x;
                    int delta = 0;
                    while (i < crossings.Count && crossings[i].x - right < 0.002f) delta += Mathf.RoundToInt(crossings[i++].y);
                    int previous = winding;
                    winding += delta;
                    if (previous == 0 && winding != 0) left = right;
                    if (previous == 0 || winding != 0 || right - left < Cell - Epsilon) continue;
                    int start = Mathf.Max(0, Mathf.CeilToInt((left - originX) / Cell - 0.5f));
                    int end = Mathf.Min(width - 1, Mathf.FloorToInt((right - originX) / Cell - 0.5f));
                    for (int x = start; x <= end; x++) occupied[x, z] = true;
                }
            }
            for (int x = 0; x < width; x++) for (int z = 0; z < height; z++) if (occupied[x, z]) votes[x, z]++;
        }
        for (int x = 0; x < width; x++) for (int z = 0; z < height; z++)
            grid.Cells[x, z] = votes[x, z] > 0;
        // Opening with a 2x2-cell kernel removes thin rails, isolated cells and spurs.
        var clean = new bool[width, height];
        for (int x = 0; x < width - 1; x++)
            for (int z = 0; z < height - 1; z++)
                if (grid.Cells[x, z] && grid.Cells[x + 1, z] && grid.Cells[x, z + 1] && grid.Cells[x + 1, z + 1])
                    clean[x, z] = clean[x + 1, z] = clean[x, z + 1] = clean[x + 1, z + 1] = true;
        grid.Cells = clean;
        if (!clean.Cast<bool>().Any(v => v)) throw new InvalidOperationException("Wall footprint is empty after removing thin details.");
        return grid;
    }

    private static void IntersectFootprints(Grid foundation, Grid walls)
    {
        // Ground-level sampling supplies the foundation contour; the first-floor wall mask
        // rejects steps/paving that occur only in that lower slab. Union of closed sections
        // fills window/door openings without the holes produced by a majority vote.
        int offsetX = Mathf.RoundToInt((foundation.X - walls.X) / Cell);
        int offsetZ = Mathf.RoundToInt((foundation.Z - walls.Z) / Cell);
        for (int x = 0; x < foundation.Width; x++) for (int z = 0; z < foundation.Height; z++)
        {
            int wx = x + offsetX, wz = z + offsetZ;
            foundation.Cells[x, z] &= wx >= 0 && wz >= 0 && wx < walls.Width && wz < walls.Height && walls.Cells[wx, wz];
        }
        if (!foundation.Cells.Cast<bool>().Any(v => v)) throw new InvalidOperationException("Ground and first-floor contours do not overlap.");
    }

    private static List<Rect> RemoveThinParts(Grid grid, string label)
    {
        int discarded = 0;
        for (int pass = 0; pass < 8; pass++)
        {
            List<Rect> rectangles = Merge(grid, label);
            bool changed = false;
            foreach (Rect rect in rectangles)
            {
                bool thin;
                try
                {
                    Rect fitted = FitToWalls(grid, rect, 0f);
                    thin = fitted.width < Cell - Epsilon || fitted.height < Cell - Epsilon;
                }
                catch (InvalidOperationException) { thin = true; }
                if (!thin) continue;
                int xMin = Mathf.RoundToInt((rect.xMin - grid.X) / Cell), xMax = Mathf.RoundToInt((rect.xMax - grid.X) / Cell);
                int zMin = Mathf.RoundToInt((rect.yMin - grid.Z) / Cell), zMax = Mathf.RoundToInt((rect.yMax - grid.Z) / Cell);
                for (int x = xMin; x < xMax; x++) for (int z = zMin; z < zMax; z++) grid.Cells[x, z] = false;
                discarded++; changed = true;
            }
            if (!changed)
            {
                if (rectangles.Count == 0) throw new InvalidOperationException("Only narrow outline fragments remain.");
                if (discarded > 0) Debug.Log($"[RacingCity] {label}: ignored {discarded} outline fragments thinner than 0.5 m.");
                return rectangles;
            }
        }
        throw new InvalidOperationException("Contour did not stabilize after removing narrow fragments.");
    }

    private static Rect FitToWalls(Grid grid, Rect rect, float inset)
    {
        // Grid merging is quantized; recover actual facade limits before applying a metric
        // inset. Otherwise a 0.15 m inset could still protrude from a cell by 0.10 m.
        for (int axis = 0; axis < 2; axis++)
        {
            float lower = axis == 0 ? rect.xMin : rect.yMin;
            float upper = axis == 0 ? rect.xMax : rect.yMax;
            float alongMin = axis == 0 ? rect.yMin : rect.xMin;
            float alongMax = axis == 0 ? rect.yMax : rect.xMax;
            int count = Mathf.Max(1, Mathf.CeilToInt((alongMax - alongMin) / Cell));
            for (int sample = 0; sample <= count; sample++)
            {
                float along = Mathf.Lerp(alongMin + 0.001f, alongMax - 0.001f, (float)sample / count);
                var lefts = new List<float>(); var rights = new List<float>();
                foreach (var section in grid.Sections)
                {
                    var crossings = new List<Vector2>();
                    foreach (Segment s in section)
                    {
                        Vector2 a = axis == 0 ? s.A : new Vector2(s.A.y, s.A.x);
                        Vector2 b = axis == 0 ? s.B : new Vector2(s.B.y, s.B.x);
                        if ((a.y > along) == (b.y > along)) continue;
                        float direction = axis == 0 ? s.Direction : s.OtherDirection;
                        crossings.Add(new Vector2(Mathf.Lerp(a.x, b.x, (along - a.y) / (b.y - a.y)), direction));
                    }
                    crossings.Sort((a, b) => a.x.CompareTo(b.x));
                    int winding = 0; float start = 0;
                    for (int i = 0; i < crossings.Count;)
                    {
                        float end = crossings[i].x; int delta = 0;
                        while (i < crossings.Count && crossings[i].x - end < 0.002f) delta += Mathf.RoundToInt(crossings[i++].y);
                        int previous = winding; winding += delta;
                        if (previous == 0 && winding != 0) start = end;
                        if (previous != 0 && winding == 0 && (lower + upper) / 2f >= start - Epsilon && (lower + upper) / 2f <= end + Epsilon)
                        { lefts.Add(start); rights.Add(end); break; }
                    }
                }
                if (lefts.Count == 0) continue; // Plane may pass through a window/open doorway.
                lefts.Sort(); rights.Sort();
                lower = Mathf.Max(lower, lefts[0] + inset);
                upper = Mathf.Min(upper, rights[rights.Count - 1] - inset);
            }
            if (upper - lower < 0.05f) throw new InvalidOperationException($"Cannot fit a box inside the wall contour: {rect}, inset={inset}, axis={axis}, remaining={upper - lower:F3} m.");
            if (axis == 0) rect = Rect.MinMaxRect(lower, rect.yMin, upper, rect.yMax);
            else rect = Rect.MinMaxRect(rect.xMin, lower, rect.xMax, upper);
        }
        return rect;
    }

    private static void IntersectEdge(Vector3 a, Vector3 b, float y, List<Vector3> hits)
    {
        if ((a.y > y) == (b.y > y)) return;
        Vector3 hit = Vector3.LerpUnclamped(a, b, (y - a.y) / (b.y - a.y));
        if (!hits.Any(h => (h - hit).sqrMagnitude < Epsilon * Epsilon)) hits.Add(hit);
    }

    private static List<Rect> Merge(Grid grid, string label)
    {
        // Compress identical adjacent columns/rows. A 100x100 rectangle becomes a 1x1
        // partition problem, while all rectilinear outline corners remain available.
        var xs = new List<int> { 0 };
        var zs = new List<int> { 0 };
        for (int x = 1; x < grid.Width; x++)
            if (Enumerable.Range(0, grid.Height).Any(z => grid.Cells[x, z] != grid.Cells[x - 1, z])) xs.Add(x);
        for (int z = 1; z < grid.Height; z++)
            if (Enumerable.Range(0, grid.Width).Any(x => grid.Cells[x, z] != grid.Cells[x, z - 1])) zs.Add(z);
        xs.Add(grid.Width); zs.Add(grid.Height);
        var mask = new bool[xs.Count - 1, zs.Count - 1];
        for (int x = 0; x < mask.GetLength(0); x++)
            for (int z = 0; z < mask.GetLength(1); z++) mask[x, z] = grid.Cells[xs[x], zs[z]];
        List<RectInt> best = Greedy(mask, false), vertical = Greedy(mask, true);
        if (vertical.Count < best.Count) best = vertical;
        // Greedy alone is not a global-minimum guarantee. Bounded exact partition search
        // improves it (and proves the usual rectangle/L/T cases), without freezing the editor.
        int budget = mask.Length <= 256 ? 10000 : 0;
        Search(mask, new List<RectInt>(), ref best, ref budget);
        if (budget <= 0) Debug.LogWarning($"[RacingCity] {label}: search budget reached; using {best.Count} rectangles (minimum not proven).");
        return best.Select(r => Rect.MinMaxRect(grid.X + xs[r.xMin] * Cell, grid.Z + zs[r.yMin] * Cell,
            grid.X + xs[r.xMax] * Cell, grid.Z + zs[r.yMax] * Cell)).ToList();
    }

    private static List<RectInt> Greedy(bool[,] source, bool vertical)
    {
        bool[,] remaining = (bool[,])source.Clone();
        var result = new List<RectInt>();
        while (First(remaining, out int x, out int z))
        {
            List<RectInt> candidates = Candidates(remaining, x, z);
            RectInt rect = vertical ? candidates.OrderByDescending(r => r.height).ThenByDescending(r => r.width).First()
                : candidates.OrderByDescending(r => r.width).ThenByDescending(r => r.height).First();
            Paint(remaining, rect, false); result.Add(rect);
        }
        return result;
    }

    private static void Search(bool[,] mask, List<RectInt> current, ref List<RectInt> best, ref int budget)
    {
        if (--budget <= 0 || current.Count >= best.Count) return;
        if (!First(mask, out int x, out int z)) { best = new List<RectInt>(current); return; }
        if (current.Count + 1 >= best.Count) return;
        foreach (RectInt rect in Candidates(mask, x, z).OrderByDescending(r => r.width * r.height))
        {
            Paint(mask, rect, false); current.Add(rect);
            Search(mask, current, ref best, ref budget);
            current.RemoveAt(current.Count - 1); Paint(mask, rect, true);
            if (budget <= 0) break;
        }
    }

    private static bool First(bool[,] mask, out int x, out int z)
    {
        for (z = 0; z < mask.GetLength(1); z++)
            for (x = 0; x < mask.GetLength(0); x++) if (mask[x, z]) return true;
        x = z = 0; return false;
    }

    private static List<RectInt> Candidates(bool[,] mask, int x, int z)
    {
        var result = new List<RectInt>();
        int width = mask.GetLength(0) - x;
        for (int row = z; row < mask.GetLength(1); row++)
        {
            int run = 0;
            while (run < width && mask[x + run, row]) run++;
            width = Mathf.Min(width, run);
            if (width == 0) break;
            for (int w = 1; w <= width; w++) result.Add(new RectInt(x, z, w, row - z + 1));
        }
        return result;
    }

    private static void Paint(bool[,] mask, RectInt r, bool value)
    {
        for (int x = r.xMin; x < r.xMax; x++) for (int z = r.yMin; z < r.yMax; z++) mask[x, z] = value;
    }

    private static float RoofLimit(Geometry geometry, Rect rect)
    {
        int nx = Mathf.Max(1, Mathf.CeilToInt(rect.width / Cell)), nz = Mathf.Max(1, Mathf.CeilToInt(rect.height / Cell));
        float[,] highest = RasterRoofs(geometry, rect, nx, nz);
        float top = float.PositiveInfinity;
        foreach (float y in highest)
        {
            if (float.IsNegativeInfinity(y)) throw new InvalidOperationException("Open roof or footprint outside a LOD silhouette.");
            top = Mathf.Min(top, y);
        }
        return top;
    }

    private static float[,] RasterRoofs(Geometry geometry, Rect rect, int nx, int nz)
    {
        // Rasterize roof triangles into the lattice, rather than testing every triangle at
        // every point. Vertical facade triangles have zero projected area and are skipped.
        float dx = rect.width / nx, dz = rect.height / nz;
        var highest = new float[nx + 1, nz + 1];
        for (int x = 0; x <= nx; x++) for (int z = 0; z <= nz; z++) highest[x, z] = float.NegativeInfinity;
        for (int i = 0; i < geometry.Indices.Count; i += 3)
        {
            Vector3 a = geometry.Vertices[geometry.Indices[i]], b = geometry.Vertices[geometry.Indices[i + 1]], c = geometry.Vertices[geometry.Indices[i + 2]];
            float denominator = (b.z - c.z) * (a.x - c.x) + (c.x - b.x) * (a.z - c.z);
            if (Mathf.Abs(denominator) < Epsilon) continue;
            int minX = Mathf.Max(0, Mathf.CeilToInt((Mathf.Min(a.x, b.x, c.x) - rect.xMin - Epsilon) / dx));
            int maxX = Mathf.Min(nx, Mathf.FloorToInt((Mathf.Max(a.x, b.x, c.x) - rect.xMin + Epsilon) / dx));
            int minZ = Mathf.Max(0, Mathf.CeilToInt((Mathf.Min(a.z, b.z, c.z) - rect.yMin - Epsilon) / dz));
            int maxZ = Mathf.Min(nz, Mathf.FloorToInt((Mathf.Max(a.z, b.z, c.z) - rect.yMin + Epsilon) / dz));
            for (int x = minX; x <= maxX; x++)
                for (int z = minZ; z <= maxZ; z++)
                {
                    var p = new Vector2(rect.xMin + x * dx, rect.yMin + z * dz);
                    float u = ((b.z - c.z) * (p.x - c.x) + (c.x - b.x) * (p.y - c.z)) / denominator;
                    float v = ((c.z - a.z) * (p.x - c.x) + (a.x - c.x) * (p.y - c.z)) / denominator;
                    if (u < -Epsilon || v < -Epsilon || u + v > 1f + Epsilon) continue;
                    highest[x, z] = Mathf.Max(highest[x, z], u * a.y + v * b.y + (1 - u - v) * c.y);
                }
        }
        return highest;
    }

    private static void AddShadowRegions(Rect rect, List<Geometry> geometry, List<Rect> rectangles, List<float> tops, string label)
    {
        try
        {
            float top = geometry.Min(g => RoofLimit(g, rect)) - 0.15f;
            if (top > BuildingFoundationController.FoundationTop) { rectangles.Add(rect); tops.Add(top); return; }
        }
        catch (InvalidOperationException) { /* Open roof/courtyard: partition the covered area below. */ }

        int width = Mathf.FloorToInt(rect.width / Cell), height = Mathf.FloorToInt(rect.height / Cell);
        if (width < 1 || height < 1) throw new InvalidOperationException("No roof-covered space for a shadow proxy.");
        var covered = new Grid { X = rect.xMin, Z = rect.yMin, Cells = new bool[width, height] };
        for (int x = 0; x < width; x++) for (int z = 0; z < height; z++) covered.Cells[x, z] = true;
        var bounds = new Rect(covered.X, covered.Z, width * Cell, height * Cell);
        foreach (Geometry lod in geometry)
        {
            float[,] roof = RasterRoofs(lod, bounds, width * 2, height * 2);
            for (int x = 0; x < width; x++) for (int z = 0; z < height; z++)
                for (int sx = 0; sx <= 2; sx++) for (int sz = 0; sz <= 2; sz++)
                    covered.Cells[x, z] &= roof[x * 2 + sx, z * 2 + sz] > BuildingFoundationController.FoundationTop + 0.15f;
        }
        int before = rectangles.Count;
        foreach (Rect region in Merge(covered, label + " roof coverage"))
        {
            // Keep away from the newly discovered courtyard/roof edge as well as the facade.
            Rect inset = Rect.MinMaxRect(region.xMin + 0.15f, region.yMin + 0.15f, region.xMax - 0.15f, region.yMax - 0.15f);
            try
            {
                float top = geometry.Min(g => RoofLimit(g, inset)) - 0.15f;
                if (top <= BuildingFoundationController.FoundationTop) continue;
                rectangles.Add(inset); tops.Add(top);
            }
            catch (InvalidOperationException) { /* Reject a remaining sub-cell opening. */ }
        }
        if (rectangles.Count == before)
        {
            Debug.LogWarning($"[RacingCity] {label}: omitted a shadow fragment without common roof coverage in all LODs.");
            return;
        }
        Debug.LogWarning($"[RacingCity] {label}: roof openings/LOD differences required {rectangles.Count - before} interior shadow regions.");
    }

    private static Transform Container(Transform parent, string name)
    {
        var go = new GameObject(name); go.transform.SetParent(parent, false); go.layer = parent.gameObject.layer;
        return go.transform;
    }

    private static Transform Cube(Transform parent, string name, Rect footprint, float bottom, float top, Material material, ShadowCastingMode casting)
    {
        var cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
        cube.name = name; cube.layer = parent.gameObject.layer; cube.transform.SetParent(parent, false);
        Object.DestroyImmediate(cube.GetComponent<BoxCollider>());
        cube.transform.localPosition = new Vector3(footprint.center.x, (top + bottom) / 2f, footprint.center.y);
        cube.transform.localScale = new Vector3(footprint.width, top - bottom, footprint.height);
        var renderer = cube.GetComponent<MeshRenderer>(); renderer.sharedMaterial = material;
        renderer.shadowCastingMode = casting; renderer.receiveShadows = false;
        return cube.transform;
    }

    private static Material FindFoundationMaterial() => AssetDatabase.FindAssets("t:Material", new[] { "Assets/BuildingsAsset" })
        .Select(AssetDatabase.GUIDToAssetPath)
        .Where(p => Path.GetFileNameWithoutExtension(p).IndexOf("Concrete", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    Path.GetFileNameWithoutExtension(p).IndexOf("Asphalt", StringComparison.OrdinalIgnoreCase) >= 0)
        .Select(AssetDatabase.LoadAssetAtPath<Material>).Where(m => m && m.shader && m.shader.isSupported)
        .OrderBy(m => MaterialScore(m)).ThenBy(m => AssetDatabase.GetAssetPath(m), StringComparer.Ordinal).FirstOrDefault();

    private static float MaterialScore(Material material)
    {
        string name = material.name.ToLowerInvariant();
        float score = name.Contains("asphalt") ? -10f : 0f;
        if (name.Contains("line") || name.Contains("cross") || name.Contains("parking")) score += 20f;
        if (name.Contains("white") || name.Contains("beige")) score += 5f;
        if (material.HasProperty("_BaseColor")) score += material.GetColor("_BaseColor").grayscale;
        return score;
    }
}

[CustomEditor(typeof(BuildingFoundationController)), CanEditMultipleObjects]
public sealed class BuildingFoundationControllerEditor : Editor
{
    public override void OnInspectorGUI()
    {
        serializedObject.Update();
        EditorGUI.BeginChangeCheck();
        EditorGUILayout.PropertyField(serializedObject.FindProperty("foundationDepth"));
        if (!EditorGUI.EndChangeCheck()) return;
        // Record the driven properties in the SAME Undo group as the Inspector slider.
        Object[] driven = targets.Cast<BuildingFoundationController>().SelectMany(c => c.GetDrivenObjects()).Distinct().ToArray();
        Undo.RecordObjects(driven.Concat(targets).ToArray(), "Change building foundation depth");
        serializedObject.ApplyModifiedProperties();
        foreach (BuildingFoundationController controller in targets) controller.ApplyDepth();
    }
}

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using EasyRoads3Dv3;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;
using Box = NfsCityGeometry.Box;
using Grid = NfsCityGeometry.Grid;
using RoadPath = NfsCityGeometry.Path;

public sealed class NfsCityGenerator : EditorWindow
{
    [Serializable]
    public sealed class Settings
    {
        public int seed = 179466;
        public float buildingSpacing = .5f;
        public float skylineDensity = .8f;
        public float curbOffset = .25f;
    }

    [SerializeField] private Settings settings = new Settings();
    [SerializeField] private string status = "Open FistMission, then scan or generate. The existing road is read-only.";
    private Vector2 scroll;
    private const string RootName = "Generated_City_Root";
    private const string AssetRoot = "Assets/GeneratedLevelPrefabs";
    private static readonly string[] BuildingFolders = {
        "Assets/Prefab/buildings/BrooklynBuildingsPrefab", "Assets/Prefab/buildings/AmericaBuildings" };

    [MenuItem("Tools/NFS City Generator")]
    public static void Open() => GetWindow<NfsCityGenerator>("NFS City Generator");

    private void OnGUI()
    {
        EditorGUILayout.LabelField("NFS City Generator", EditorStyles.boldLabel);
        EditorGUILayout.LabelField("Scene", SceneManager.GetActiveScene().name);
        settings.seed = EditorGUILayout.IntField("Seed", settings.seed);
        settings.buildingSpacing = EditorGUILayout.Slider("Building Spacing (m)", settings.buildingSpacing, .2f, 1);
        settings.skylineDensity = EditorGUILayout.Slider("Skyline Density", settings.skylineDensity, 0, 1);
        settings.curbOffset = EditorGUILayout.Slider("Curb Offset (m)", settings.curbOffset, 0, 2);
        using (new EditorGUI.DisabledScope(EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling))
        {
            if (GUILayout.Button("Scan Scene & Prefabs")) RunUI(() => Describe(Preflight(settings)));
            if (GUILayout.Button("Generate Full Map")) RunUI(() => {
                var result = Generate(settings);
                return $"Generated {result.frontBuildings} street-front buildings, {result.skylineBuildings} skyline buildings, " +
                    $"{result.streetProps} props. Save the scene to keep the result. Undo is available.";
            });
            if (GUILayout.Button("Clear Generated City")) RunUI(() => {
                Clear(); return "Generated city cleared; original terrain appearance restored. Undo is available.";
            });
            if (GUILayout.Button("Validate Generated City")) RunUI(() => ValidateCurrent());
        }
        EditorGUILayout.HelpBox("Road geometry, Main's heightmap, camera, light and the two example houses are preserved. " +
            "Terrain appearance uses a separate copy. Generated assets are retained for Undo and saved-scene references.", MessageType.Info);
        scroll = EditorGUILayout.BeginScrollView(scroll);
        EditorGUILayout.SelectableLabel(status, EditorStyles.wordWrappedLabel, GUILayout.MinHeight(160));
        EditorGUILayout.EndScrollView();
    }

    private void RunUI(Func<string> action)
    {
        try { status = action(); Debug.Log("[NFS City] " + status); }
        catch (OperationCanceledException) { status = "Generation canceled. Previous scene content restored."; }
        catch (Exception ex) { status = ex.Message; Debug.LogException(ex); }
    }

    internal sealed class Profile
    {
        public string path;
        public GameObject prefab;
        public Bounds bounds;
    }

    internal sealed class Context
    {
        public Scene scene;
        public Transform network;
        public Terrain terrain;
        public RoadPath road;
        public readonly List<Profile> buildings = new List<Profile>();
        public readonly List<Profile> lamps = new List<Profile>();
        public readonly List<Profile> props = new List<Profile>();
        public readonly Grid occupied = new Grid();
        public Grid roadExclusion;
        public string fingerprint;
        public string protectedFingerprint;
        public Settings settings;
        public readonly List<Vector2>[] frontage = { new List<Vector2>(), new List<Vector2>() };
        public int rejectedRoad, rejectedSlope, rejectedOverlap, rejectedTerrain;
        public int unavailableBuildings;
        public Material foundationMaterial;
    }

    internal static Context Preflight(Settings input)
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Exit Play mode before generating.");
        if (!NfsCityGeometry.Finite(input.buildingSpacing) || !NfsCityGeometry.Finite(input.skylineDensity) || !NfsCityGeometry.Finite(input.curbOffset))
            throw new InvalidOperationException("Generator settings must be finite numbers.");
        var c = new Context { scene = SceneManager.GetActiveScene(), settings = new Settings {
            seed = input.seed, buildingSpacing = Mathf.Clamp(input.buildingSpacing, .2f, 1),
            skylineDensity = Mathf.Clamp01(input.skylineDensity), curbOffset = Mathf.Clamp(input.curbOffset, 0, 2) } };
        if (!c.scene.IsValid() || !c.scene.isLoaded || EditorSceneManager.IsPreviewScene(c.scene))
            throw new InvalidOperationException("Open the target scene first.");
        var networks = c.scene.GetRootGameObjects().Where(g => g.name == "MainRoad").ToArray();
        if (networks.Length != 1 || !networks[0].GetComponent<ERModularBase>())
            throw new InvalidOperationException("Expected one existing MainRoad with ERModularBase. No road was created.");
        c.network = networks[0].transform;
        var roadTransform = c.network.Find("Road Objects/road_0001");
        if (!roadTransform || !roadTransform.GetComponent<ERModularRoad>())
            throw new InvalidOperationException("MainRoad/Road Objects/road_0001 with ERModularRoad was not found.");
        FindOwnedRoot(c.scene); // Reject an unowned object with the reserved name before doing any work.
        var terrains = All<Terrain>(c.scene).Where(t => t.name == "Main").ToArray();
        if (terrains.Length != 1 || !terrains[0].terrainData) throw new InvalidOperationException("Expected one terrain named Main with TerrainData.");
        c.terrain = terrains[0];
        if (Quaternion.Angle(c.terrain.transform.rotation, Quaternion.identity) > .01f ||
            (c.terrain.transform.lossyScale - Vector3.one).sqrMagnitude > .0001f)
            throw new InvalidOperationException("Main must have an unrotated, unit-scale transform for Terrain.SampleHeight.");
        c.fingerprint = Fingerprint(c.network);
        c.protectedFingerprint = ProtectedFingerprint(c.scene);
        var script = roadTransform.GetComponent<ERModularRoad>();
        // ERRoadNetwork construction initializes terrain caches. This existing-road
        // wrapper performs only getters and never calls any EasyRoads mutator.
        var road = script.road ?? new ERRoad(script);
        c.road = ReadPath(roadTransform, road);
        c.roadExclusion = c.road.RoadExclusion(c.settings.curbOffset + .4f);
        foreach (string folder in BuildingFolders)
        {
            if (!AssetDatabase.IsValidFolder(folder)) throw new InvalidOperationException("Missing prefab folder: " + folder);
        }
        foreach (string path in AssetDatabase.FindAssets("t:Prefab", BuildingFolders).Select(AssetDatabase.GUIDToAssetPath)
            .Distinct().OrderBy(p => p, StringComparer.Ordinal))
        {
            string name = Path.GetFileNameWithoutExtension(path);
            if (name.IndexOf("Bldg", StringComparison.OrdinalIgnoreCase) < 0 ||
                name.IndexOf("WaterTower", StringComparison.OrdinalIgnoreCase) >= 0 ||
                name.IndexOf("Camper", StringComparison.OrdinalIgnoreCase) >= 0 ||
                name.IndexOf("Trailer", StringComparison.OrdinalIgnoreCase) >= 0) continue;
            var p = Measure(path);
            if (p == null) { c.unavailableBuildings++; continue; }
            if (p != null && p.bounds.size.y >= 5 && p.bounds.size.x >= 4 && p.bounds.size.z >= 4) c.buildings.Add(p);
        }
        if (c.buildings.Count < 2) throw new InvalidOperationException("At least two renderable building prefabs are required.");
        // Search the requested folders for actual lamps/signs/bins. Decorative trees,
        // wires, fences and isolated facades never enter the building catalogue.
        foreach (string path in AssetDatabase.FindAssets("t:Prefab", new[] { "Assets/Prefab", "Assets/BuildingsAsset" })
            .Select(AssetDatabase.GUIDToAssetPath).OrderBy(p => p, StringComparer.Ordinal))
        {
            string name = Path.GetFileNameWithoutExtension(path).ToLowerInvariant();
            bool lamp = name.Contains("streetlight") || name.Contains("streetlamp") ||
                path.IndexOf("/street_lights/", StringComparison.OrdinalIgnoreCase) >= 0;
            bool prop = name.Contains("roadsign") || name.Contains("streetsign") || name.Contains("trashbin") || name.Contains("trashcan");
            if (!lamp && !prop) continue;
            var p = Measure(path);
            if (p == null) continue;
            if (lamp && p.bounds.size.y >= 3 && p.bounds.size.y <= 16 && p.bounds.size.x <= 12 && p.bounds.size.z <= 12)
                c.lamps.Add(p);
            else if (prop && p.bounds.size.y <= 5 && p.bounds.size.x <= 3 && p.bounds.size.z <= 3) c.props.Add(p);
        }
        // Protect all existing building prefab roots, including both named examples.
        var oldRoot = FindOwnedRoot(c.scene);
        foreach (var t in All<Transform>(c.scene))
        {
            if (oldRoot && t.IsChildOf(oldRoot.transform)) continue;
            if (!t.name.StartsWith("KB3D_", StringComparison.Ordinal) || !t.name.Contains("Bldg")) continue;
            if (t.parent && t.parent.name.StartsWith("KB3D_", StringComparison.Ordinal)) continue;
            if (TryBounds(t.gameObject, out var b)) c.occupied.Add(new Box(b.center, b.size, Quaternion.identity));
        }
        RequirePreserved(c);
        return c;
    }

    internal static Profile Measure(string path)
    {
        var root = PrefabUtility.LoadPrefabContents(path);
        try
        {
            root.transform.position = Vector3.zero;
            // Some shipped prefab files contain proxy shadow cubes but their source
            // FBX is absent. Do not mistake proxy bounds for a renderable building.
            if (root.GetComponentsInChildren<MeshFilter>(true).Any(f => f.gameObject.activeInHierarchy &&
                f.TryGetComponent<Renderer>(out var renderer) && renderer.enabled && !f.sharedMesh)) return null;
            if (!TryBounds(root, out var b) || b.size.sqrMagnitude < .01f) return null;
            return new Profile { path = path, prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path), bounds = b };
        }
        finally { PrefabUtility.UnloadPrefabContents(root); }
    }

    internal static bool TryBounds(GameObject root, out Bounds bounds)
    {
        var renderers = root.GetComponentsInChildren<Renderer>(true)
            .Where(r => r.enabled && r.gameObject.activeInHierarchy && !(r is ParticleSystemRenderer)).ToArray();
        bounds = new Bounds();
        if (renderers.Length == 0) return false;
        bounds = renderers[0].bounds;
        foreach (var renderer in renderers.Skip(1)) bounds.Encapsulate(renderer.bounds);
        return NfsCityGeometry.Finite(bounds.size.x) && NfsCityGeometry.Finite(bounds.size.y) && NfsCityGeometry.Finite(bounds.size.z);
    }

    private static string Describe(Context c)
    {
        return $"Scene: {c.scene.path}\nRoad: {c.road.length:F1} m long; actual sampled width {c.road.sampledWidth:F1} m, configured width {c.road.width:F1} m; " +
            $"{c.road.centers.Length} spline samples; {(c.road.closed ? "closed" : "open")}.\n" +
            $"Buildings: {c.buildings.Count}; missing-mesh prefabs skipped: {c.unavailableBuildings}; usable lamps: {c.lamps.Count}; small props: {c.props.Count}.\n" +
            $"Curb inner edge: {c.road.sampledWidth * .5f + c.settings.curbOffset:F2} m from center on a constant-width section.\n" +
            "Front footprint: 2.5 m behind that curb; building center adds half its measured depth.\n" +
            "Skyline center offsets: 45 / 80 m from the road edge, with 5 m variation.\n" +
            "MainRoad, Main heightmap, camera, light and existing buildings are protected.";
    }

    internal static RoadPath ReadPath(Transform transform, ERRoad road)
    {
        var centers = road.GetSplinePointsCenter(); var left = road.GetSplinePointsLeftSide(); var right = road.GetSplinePointsRightSide();
        var path = new RoadPath(centers, left, right, road.GetWidth(), road.IsClosedTrack());
        if (VerifyRenderedEdges(transform, path)) return path;
        // Existing EasyRoads meshes may have their points stored in mesh-local
        // coordinates under a translated Road Objects parent (0.02 m here).
        // Accept a coordinate convention only after checking every edge against the actual mesh.
        path = new RoadPath(centers.Select(transform.TransformPoint).ToArray(), left.Select(transform.TransformPoint).ToArray(),
            right.Select(transform.TransformPoint).ToArray(), road.GetWidth(), road.IsClosedTrack());
        if (!VerifyRenderedEdges(transform, path))
            throw new InvalidOperationException("Cached EasyRoads edges disagree with the existing rendered road mesh. Generation stopped without rebuilding the road.");
        return path;
    }

    private static bool VerifyRenderedEdges(Transform road, RoadPath path)
    {
        var filter = road.GetComponent<MeshFilter>();
        if (!filter || !filter.sharedMesh || !filter.sharedMesh.isReadable)
            throw new InvalidOperationException("The existing road mesh must be readable to verify cached spline edges. No road or importer was changed.");
        var vertices = new Dictionary<Vector3Int, List<Vector3>>();
        Func<Vector3, Vector3Int> key = p => new Vector3Int(Mathf.FloorToInt(p.x * 10), Mathf.FloorToInt(p.y * 10), Mathf.FloorToInt(p.z * 10));
        foreach (var vertex in filter.sharedMesh.vertices)
        {
            var point = filter.transform.TransformPoint(vertex); var cell = key(point);
            if (!vertices.TryGetValue(cell, out var list)) vertices.Add(cell, list = new List<Vector3>());
            list.Add(point);
        }
        foreach (var point in path.left.Concat(path.right))
        {
            var cell = key(point); bool found = false;
            for (int x = -1; x <= 1 && !found; x++) for (int y = -1; y <= 1 && !found; y++) for (int z = -1; z <= 1 && !found; z++)
                if (vertices.TryGetValue(cell + new Vector3Int(x, y, z), out var list))
                    found = list.Any(vertex => (vertex - point).sqrMagnitude < .0001f);
            if (!found) return false;
        }
        return true;
    }

    public static NfsGeneratedCity Generate(Settings settings)
    {
        var c = Preflight(settings); // Finish all asset/scene validation before removing a previous generation.
        Debug.Log("[NFS City] " + Describe(c));
        Undo.IncrementCurrentGroup(); int undoGroup = Undo.GetCurrentGroup(); Undo.SetCurrentGroupName("Generate Full NFS City");
        GameObject root = null; bool rootRegistered = false;
        try
        {
            ClearOwned(c.scene);
            root = new GameObject(RootName); SceneManager.MoveGameObjectToScene(root, c.scene);
            var state = root.AddComponent<NfsGeneratedCity>();
            state.seed = c.settings.seed; state.roadFingerprint = c.fingerprint;
            state.assetFolder = AssetRoot + "/City_" + Guid.NewGuid().ToString("N");
            EnsureFolder(state.assetFolder);
            c.foundationMaterial = Material(state.assetFolder, "Foundation", new Color(.25f, .26f, .25f), .1f);
            var front = Child(root.transform, "01_Buildings_FrontLine");
            var skyline = Child(root.transform, "02_Buildings_Skyline");
            var curbs = Child(root.transform, "03_Curbs_And_Barriers");
            var walls = Child(root.transform, "04_Smooth_WallColliders");
            var props = Child(root.transform, "05_Street_Props");
            var random = new System.Random(c.settings.seed);
            var frontPool = c.buildings.Where(p => p.bounds.size.y <= 65 && p.bounds.size.x <= 65 && p.bounds.size.z <= 65).ToList();
            var skyPool = c.buildings.Where(p => p.bounds.size.y >= 35).ToList();
            if (frontPool.Count < 2 || skyPool.Count < 2) throw new InvalidOperationException("Not enough suitable frontage/skyline prefabs.");
            for (int side = -1; side <= 1; side += 2) BuildFront(c, state, front, frontPool, side, random);
            if (c.settings.skylineDensity > 0)
                for (int row = 1; row <= 2; row++) for (int side = -1; side <= 1; side += 2)
                    BuildSkyline(c, state, skyline, skyPool, side, row, random);
            Progress("Extruding curbs and smooth walls", .72f);
            BuildRoadFurniture(c, state, curbs, walls);
            BuildProps(c, state, props, random);
            ApplyTerrainAppearance(c.terrain, state);
            RequirePreserved(c);
            Validate(state, c.road, c.roadExclusion);
            // Register the completed hierarchy: Undo/Redo snapshots include all prefab children and mesh references.
            Undo.RegisterCreatedObjectUndo(root, "Generate Full NFS City"); rootRegistered = true;
            Undo.CollapseUndoOperations(undoGroup);
            AssetDatabase.SaveAssets(); EditorSceneManager.MarkSceneDirty(c.scene);
            Selection.activeGameObject = root;
            Debug.Log($"[NFS City] Complete: front={state.frontBuildings}, skyline={state.skylineBuildings}, props={state.streetProps}. " +
                $"Rejected attempts: road={c.rejectedRoad}, overlap={c.rejectedOverlap}, terrain={c.rejectedTerrain}, slope={c.rejectedSlope}. " +
                "Road/protected-object fingerprints unchanged. Scene has not been automatically saved.");
            return state;
        }
        catch
        {
            if (root && !rootRegistered) Object.DestroyImmediate(root);
            Undo.RevertAllDownToGroup(undoGroup);
            throw;
        }
        finally { EditorUtility.ClearProgressBar(); }
    }

    private static void BuildFront(Context c, NfsGeneratedCity state, Transform parent, List<Profile> pool, int side, System.Random random)
    {
        float cursor = 1; string previous = null;
        while (cursor < c.road.length - 5)
        {
            Progress("Placing street front " + (side < 0 ? "left" : "right"), .08f + .29f * (side < 0 ? 0 : 1) + .29f * cursor / c.road.length);
            bool accepted = false;
            foreach (var p in Shuffled(pool, random).Where(p => p.path != previous))
            {
                float station = cursor + p.bounds.size.x * .5f;
                if (station + p.bounds.size.x * .5f > c.road.length - 1) continue;
                var pose = c.road.Sample(station, side, c.settings.curbOffset);
                Vector3 center = pose.edge + pose.outward * (2.5f + p.bounds.size.z * .5f);
                if (!TryPlace(c, state, parent, p, center, pose.rotation, pose.center.y, side, 0, station, random)) continue;
                c.frontage[side < 0 ? 0 : 1].Add(new Vector2(cursor, cursor + p.bounds.size.x));
                cursor += p.bounds.size.x + c.settings.buildingSpacing; previous = p.path;
                state.frontBuildings++; accepted = true; break;
            }
            if (!accepted) cursor += 2; // Narrow alternatives were already tried; leave a safe opening on the inside bend.
        }
    }

    private static void BuildSkyline(Context c, NfsGeneratedCity state, Transform parent, List<Profile> pool, int side, int row, System.Random random)
    {
        float step = 46 / c.settings.skylineDensity;
        string previous = null;
        for (float station = row * 11 + (side > 0 ? step * .4f : 0); station < c.road.length; station += step)
        {
            Progress("Placing skyline rows", .68f);
            foreach (var p in Shuffled(pool, random).Where(p => p.path != previous))
            {
                var pose = c.road.Sample(station, side);
                float offset = (row == 1 ? 45 : 80) + (float)(random.NextDouble() * 10 - 5);
                Vector3 center = pose.edge + pose.outward * offset;
                if (!TryPlace(c, state, parent, p, center, pose.rotation, float.PositiveInfinity, side, row, station, random)) continue;
                state.skylineBuildings++; previous = p.path; break;
            }
        }
    }

    private static bool TryPlace(Context c, NfsGeneratedCity state, Transform parent, Profile p, Vector3 center,
        Quaternion rotation, float roadHeight, int side, int row, float station, System.Random random)
    {
        var footprint = new Box(center, p.bounds.size, rotation);
        if (c.roadExclusion.Intersects(footprint, true)) { c.rejectedRoad++; return false; }
        if (!SampleFootprint(c.terrain, footprint, row > 0, out float minY, out float maxY))
        { if (minY == float.PositiveInfinity) c.rejectedTerrain++; else c.rejectedSlope++; return false; }
        float bottom = Mathf.Min(minY, roadHeight) - .75f - (float)random.NextDouble() * .5f;
        float supportTop = Mathf.Max(maxY, float.IsPositiveInfinity(roadHeight) ? maxY : roadHeight);
        float foundationDepth = 0;
        // A low store should not disappear into an embankment. On larger height
        // differences use the concrete-foundation alternative specified for this tool.
        if (supportTop - Mathf.Min(minY, roadHeight) > Mathf.Max(2.5f, p.bounds.size.y * .25f))
        {
            bottom = supportTop - .75f;
            foundationDepth = bottom - (Mathf.Min(minY, roadHeight) - 1);
        }
        var occupiedSize = p.bounds.size + new Vector3(0, foundationDepth, 0);
        center.y = bottom + (p.bounds.size.y - foundationDepth) * .5f;
        var box = new Box(center, occupiedSize + new Vector3(c.settings.buildingSpacing, 0, c.settings.buildingSpacing), rotation);
        if (c.occupied.Intersects(box)) { c.rejectedOverlap++; return false; }
        var wrapper = PlacePrefab(p, parent, new Vector3(center.x, bottom, center.z), rotation);
        if (foundationDepth > 0)
            Cube(wrapper, "Concrete_Foundation", new Vector3(0, -foundationDepth * .5f, 0),
                new Vector3(p.bounds.size.x, foundationDepth, p.bounds.size.z), c.foundationMaterial);
        state.placements.Add(new NfsGeneratedCity.Placement { transform = wrapper, prefabPath = p.path, station = station,
            side = side, row = row, localBounds = new Bounds(Vector3.up * ((p.bounds.size.y - foundationDepth) * .5f), occupiedSize) });
        c.occupied.Add(box); return true;
    }

    private static Transform PlacePrefab(Profile p, Transform parent, Vector3 bottomCenter, Quaternion rotation)
    {
        var wrapper = Child(parent, Path.GetFileNameWithoutExtension(p.path));
        wrapper.SetPositionAndRotation(bottomCenter, rotation);
        var instance = (GameObject)PrefabUtility.InstantiatePrefab(p.prefab, wrapper);
        // Preserve authored import corrections and scale inside the yaw-only placement root.
        instance.transform.localPosition = new Vector3(-p.bounds.center.x, -p.bounds.min.y, -p.bounds.center.z);
        instance.transform.localRotation = p.prefab.transform.localRotation;
        instance.transform.localScale = p.prefab.transform.localScale;
        return wrapper;
    }

    internal static bool SampleFootprint(Terrain terrain, Box box, bool limitSlope, out float minY)
        => SampleFootprint(terrain, box, limitSlope, out minY, out _);

    private static bool SampleFootprint(Terrain terrain, Box box, bool limitSlope, out float minY, out float maxY)
    {
        minY = float.PositiveInfinity;
        maxY = float.NegativeInfinity;
        Vector3 origin = terrain.transform.position, size = terrain.terrainData.size;
        int nx = Mathf.Clamp(Mathf.CeilToInt(box.half.x * 2 / 5), 2, 20);
        int nz = Mathf.Clamp(Mathf.CeilToInt(box.half.z * 2 / 5), 2, 20);
        for (int x = 0; x <= nx; x++) for (int z = 0; z <= nz; z++)
        {
            Vector3 point = box.At((float)x / nx * 2 - 1, (float)z / nz * 2 - 1);
            float u = (point.x - origin.x) / size.x, v = (point.z - origin.z) / size.z;
            if (u < 0 || u > 1 || v < 0 || v > 1 || terrain.terrainData.IsHole(
                Mathf.Clamp((int)(u * terrain.terrainData.holesResolution), 0, terrain.terrainData.holesResolution - 1),
                Mathf.Clamp((int)(v * terrain.terrainData.holesResolution), 0, terrain.terrainData.holesResolution - 1)))
            { minY = float.PositiveInfinity; return false; }
            float y = terrain.SampleHeight(point) + origin.y;
            minY = Mathf.Min(minY, y);
            maxY = Mathf.Max(maxY, y);
            if (limitSlope && terrain.terrainData.GetSteepness(u, v) > 35) return false;
        }
        return true;
    }

    private static void BuildRoadFurniture(Context c, NfsGeneratedCity state, Transform curbs, Transform walls)
    {
        var concrete = Material(state.assetFolder, "Concrete", new Color(.24f, .25f, .24f), .15f);
        var barrier = Material(state.assetFolder, "Barrier", new Color(.42f, .43f, .4f), .3f);
        var physics = new PhysicsMaterial("NFS Zero Friction") { dynamicFriction = 0, staticFriction = 0, bounciness = 0,
            frictionCombine = PhysicsMaterialCombine.Minimum, bounceCombine = PhysicsMaterialCombine.Minimum };
        AssetDatabase.CreateAsset(physics, state.assetFolder + "/NFS_ZeroFriction.physicMaterial");
        for (int side = -1; side <= 1; side += 2)
        {
            string prefix = side < 0 ? "Left" : "Right";
            MeshObject(curbs, prefix + "_ContinuousCurb", NfsCityGeometry.Ribbon(c.road, side, c.settings.curbOffset, .35f, .22f), concrete, null, state.assetFolder);
            MeshObject(walls, prefix + "_SmoothWall", NfsCityGeometry.Ribbon(c.road, side, c.settings.curbOffset, .12f, 3), null, physics, state.assetFolder);
            var intervals = c.frontage[side < 0 ? 0 : 1];
            Func<float, bool> openOrBend = d => !intervals.Any(i => d >= i.x && d <= i.y) ||
                Vector3.Angle(c.road.Sample(Mathf.Max(0, d - 5), side).tangent,
                    c.road.Sample(Mathf.Min(c.road.length, d + 5), side).tangent) > 16;
            var mesh = NfsCityGeometry.Ribbon(c.road, side, c.settings.curbOffset + .14f, .26f, .85f, openOrBend);
            if (mesh.vertexCount > 0) MeshObject(curbs, prefix + "_OpenSectionBarriers", mesh, barrier, null, state.assetFolder);
            else Object.DestroyImmediate(mesh);
        }
    }

    private static void MeshObject(Transform parent, string name, Mesh mesh, Material visual, PhysicsMaterial physics, string folder)
    {
        mesh.name = name; AssetDatabase.CreateAsset(mesh, folder + "/" + name + ".asset");
        var go = Child(parent, name).gameObject; go.isStatic = true;
        if (visual)
        {
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            go.AddComponent<MeshRenderer>().sharedMaterial = visual;
        }
        if (physics)
        {
            var collider = go.AddComponent<MeshCollider>(); collider.convex = false;
            collider.cookingOptions = MeshColliderCookingOptions.CookForFasterSimulation |
                MeshColliderCookingOptions.EnableMeshCleaning | MeshColliderCookingOptions.WeldColocatedVertices | MeshColliderCookingOptions.UseFastMidphase;
            collider.sharedMaterial = physics; collider.sharedMesh = mesh;
        }
    }

    private static void BuildProps(Context c, NfsGeneratedCity state, Transform parent, System.Random random)
    {
        if (c.lamps.Count == 0) c.lamps.Add(CreateLamp(state.assetFolder));
        var sign = CreateSign(state.assetFolder);
        for (int side = -1; side <= 1; side += 2)
        {
            for (float d = 12 + (side > 0 ? 15 : 0); d < c.road.length - 2; d += 30)
            {
                Progress("Placing street lights and props", .83f);
                var lamp = c.lamps[random.Next(c.lamps.Count)];
                PlaceProp(c, state, parent, lamp, d, side);
                if (((int)(d / 30) % 3) == 0) PlaceProp(c, state, parent, sign, d + 6, side);
                if (c.props.Count > 0 && ((int)(d / 30) % 2) == 0)
                    PlaceProp(c, state, parent, c.props[random.Next(c.props.Count)], d + 9, side);
            }
        }
    }

    private static void PlaceProp(Context c, NfsGeneratedCity state, Transform parent, Profile p, float station, int side)
    {
        if (station >= c.road.length) return;
        var pose = c.road.Sample(station, side, c.settings.curbOffset);
        Vector3 center = pose.edge + pose.outward * (.55f + p.bounds.size.z * .5f);
        var box = new Box(center, p.bounds.size + new Vector3(.15f, 0, .15f), pose.rotation);
        if (c.roadExclusion.Intersects(box, true) || !SampleFootprint(c.terrain, box, false, out float minY)) return;
        float bottom = Mathf.Min(minY, pose.edge.y) - .12f;
        center.y = bottom + p.bounds.size.y * .5f;
        box = new Box(center, p.bounds.size + new Vector3(.15f, 0, .15f), pose.rotation);
        if (c.occupied.Intersects(box)) return;
        PlacePrefab(p, parent, new Vector3(center.x, bottom, center.z), pose.rotation);
        c.occupied.Add(box); state.streetProps++;
    }

    private static Profile CreateLamp(string folder)
    {
        var metal = Material(folder, "LampMetal", new Color(.09f, .1f, .105f), .5f);
        var glow = Material(folder, "LampGlass", new Color(.84f, .74f, .45f), .35f);
        glow.EnableKeyword("_EMISSION"); glow.SetColor("_EmissionColor", new Color(.85f, .65f, .28f) * 2);
        var root = new GameObject("NFS_StreetLamp");
        try
        {
            Cube(root.transform, "Base", new Vector3(0, .2f, 0), new Vector3(.5f, .4f, .5f), metal);
            Cube(root.transform, "Pole", new Vector3(0, 4, 0), new Vector3(.16f, 8, .16f), metal);
            Cube(root.transform, "Arm", new Vector3(0, 7.9f, 1.25f), new Vector3(.16f, .16f, 2.5f), metal);
            Cube(root.transform, "Luminaire", new Vector3(0, 7.75f, 2.35f), new Vector3(.6f, .2f, 1.1f), glow);
            string path = folder + "/NFS_StreetLamp.prefab";
            PrefabUtility.SaveAsPrefabAsset(root, path); return Measure(path);
        }
        finally { Object.DestroyImmediate(root); }
    }

    private static Profile CreateSign(string folder)
    {
        var post = Material(folder, "SignPost", new Color(.19f, .2f, .2f), .5f);
        var face = Material(folder, "SignAmber", new Color(.8f, .57f, .12f), .25f);
        var dark = Material(folder, "SignArrow", new Color(.035f, .04f, .035f), .1f);
        var root = new GameObject("NFS_ChevronSign");
        try
        {
            Cube(root.transform, "Post", new Vector3(0, 1.2f, 0), new Vector3(.075f, 2.4f, .075f), post);
            Cube(root.transform, "Panel", new Vector3(0, 2.2f, 0), new Vector3(.9f, .65f, .08f), face);
            var upper = Cube(root.transform, "ChevronTop", new Vector3(0, 2.32f, .05f), new Vector3(.42f, .09f, .025f), dark);
            var lower = Cube(root.transform, "ChevronBottom", new Vector3(0, 2.08f, .05f), new Vector3(.42f, .09f, .025f), dark);
            upper.localRotation = Quaternion.Euler(0, 0, -35); lower.localRotation = Quaternion.Euler(0, 0, 35);
            string path = folder + "/NFS_ChevronSign.prefab";
            PrefabUtility.SaveAsPrefabAsset(root, path); return Measure(path);
        }
        finally { Object.DestroyImmediate(root); }
    }

    private static Transform Cube(Transform parent, string name, Vector3 position, Vector3 scale, Material material)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Cube); go.name = name; go.transform.SetParent(parent, false);
        go.transform.localPosition = position; go.transform.localScale = scale;
        go.GetComponent<Renderer>().sharedMaterial = material;
        Object.DestroyImmediate(go.GetComponent<Collider>()); return go.transform;
    }

    private static void ApplyTerrainAppearance(Terrain terrain, NfsGeneratedCity state)
    {
        var layers = terrain.terrainData.terrainLayers;
        bool testLayers = layers.Length == 0 || layers.Any(l => !l || !l.diffuseTexture || IsTestName(l.name) || IsTestName(l.diffuseTexture.name));
        bool testMaterial = !terrain.materialTemplate || IsTestName(terrain.materialTemplate.name);
        if (!testLayers && !testMaterial) return;
        state.terrain = terrain; state.originalTerrainData = terrain.terrainData;
        state.originalTerrainMaterial = terrain.materialTemplate;
        var terrainCollider = terrain.GetComponent<TerrainCollider>();
        if (terrainCollider) state.originalColliderData = terrainCollider.terrainData;
        Undo.RecordObject(terrain, "Set city terrain appearance");
        if (terrainCollider) Undo.RecordObject(terrainCollider, "Set city terrain collision data");
        var copy = Object.Instantiate(terrain.terrainData); copy.name = "Main_CityAppearance";
        AssetDatabase.CreateAsset(copy, state.assetFolder + "/Main_CityAppearance.asset");
        state.generatedTerrainData = copy;
        if (testLayers)
        {
            var texture = new Texture2D(64, 64, TextureFormat.RGB24, true) { name = "CityGround", wrapMode = TextureWrapMode.Repeat };
            var pixels = new Color[64 * 64]; var random = new System.Random(83);
            for (int i = 0; i < pixels.Length; i++)
            {
                float value = .155f + (float)random.NextDouble() * .045f;
                pixels[i] = new Color(value * .96f, value, value * .92f);
            }
            texture.SetPixels(pixels); texture.Apply();
            AssetDatabase.CreateAsset(texture, state.assetFolder + "/CityGround.asset");
            var layer = new TerrainLayer { name = "City dark concrete and soil", diffuseTexture = texture,
                tileSize = new Vector2(9, 9), metallic = 0, smoothness = .08f };
            AssetDatabase.CreateAsset(layer, state.assetFolder + "/CityGround.terrainlayer");
            if (layers.Length == 0)
            {
                copy.terrainLayers = new[] { layer };
                var weights = new float[copy.alphamapHeight, copy.alphamapWidth, 1];
                for (int z = 0; z < copy.alphamapHeight; z++) for (int x = 0; x < copy.alphamapWidth; x++) weights[z, x, 0] = 1;
                copy.SetAlphamaps(0, 0, weights);
            }
            else
            {
                var replacements = (TerrainLayer[])layers.Clone();
                for (int i = 0; i < replacements.Length; i++)
                    if (!layers[i] || !layers[i].diffuseTexture || IsTestName(layers[i].name) || IsTestName(layers[i].diffuseTexture.name)) replacements[i] = layer;
                copy.terrainLayers = replacements;
            }
        }
        terrain.terrainData = copy;
        if (terrainCollider) terrainCollider.terrainData = copy;
        var shader = Shader.Find("Universal Render Pipeline/Terrain/Lit");
        if (!shader) throw new InvalidOperationException("URP Terrain/Lit shader is missing.");
        var material = new Material(shader) { name = "Main_CityTerrain_URP" };
        AssetDatabase.CreateAsset(material, state.assetFolder + "/Main_CityTerrain_URP.mat");
        state.generatedTerrainMaterial = material; terrain.materialTemplate = material;
        EditorUtility.SetDirty(copy); terrain.Flush();
    }

    private static bool IsTestName(string name)
    {
        name = name.ToLowerInvariant();
        return name.Contains("checker") || name.Contains("grid") || name.Contains("default") || name.Contains("prototype");
    }

    public static void Clear()
    {
        Undo.IncrementCurrentGroup(); int group = Undo.GetCurrentGroup(); Undo.SetCurrentGroupName("Clear Generated NFS City");
        ClearOwned(SceneManager.GetActiveScene()); Undo.CollapseUndoOperations(group);
    }

    private static void ClearOwned(Scene scene)
    {
        var state = FindOwnedRoot(scene);
        if (!state) return;
        if (state.terrain)
        {
            var terrain = state.terrain;
            Undo.RecordObject(terrain, "Restore original terrain appearance");
            if (terrain.terrainData == state.generatedTerrainData) terrain.terrainData = state.originalTerrainData;
            if (terrain.materialTemplate == state.generatedTerrainMaterial) terrain.materialTemplate = state.originalTerrainMaterial;
            var collider = terrain.GetComponent<TerrainCollider>();
            if (collider && collider.terrainData == state.generatedTerrainData)
            { Undo.RecordObject(collider, "Restore original terrain collider data"); collider.terrainData = state.originalColliderData; }
            terrain.Flush();
        }
        Undo.DestroyObjectImmediate(state.gameObject); EditorSceneManager.MarkSceneDirty(scene);
    }

    internal static NfsGeneratedCity FindOwnedRoot(Scene scene)
    {
        var roots = scene.GetRootGameObjects().Where(g => g.name == RootName).ToArray();
        if (roots.Length == 0) return null;
        if (roots.Length != 1 || !roots[0].TryGetComponent<NfsGeneratedCity>(out var state) || state.owner != NfsGeneratedCity.Owner)
            throw new InvalidOperationException("Generated_City_Root already exists without a unique generator ownership marker. It was not modified.");
        return state;
    }

    public static string ValidateCurrent()
    {
        var state = FindOwnedRoot(SceneManager.GetActiveScene());
        if (!state) throw new InvalidOperationException("Generate a city first.");
        var root = SceneManager.GetActiveScene().GetRootGameObjects().Single(g => g.name == "MainRoad").transform;
        var script = root.Find("Road Objects/road_0001").GetComponent<ERModularRoad>();
        var road = script.road ?? new ERRoad(script);
        var path = ReadPath(script.transform, road);
        // The generator fingerprint includes all serialized MainRoad components and meshes.
        if (Fingerprint(root) != state.roadFingerprint) throw new InvalidOperationException("Road fingerprint differs from generation time.");
        Validate(state, path, path.RoadExclusion(.35f));
        return $"Validated {state.placements.Count} buildings: no building overlap or asphalt intrusion; " +
            "yaw-only placement; two continuous 3 m zero-friction walls; road unchanged.";
    }

    internal static void Validate(NfsGeneratedCity state, RoadPath path, Grid roadExclusion)
    {
        var occupied = new Grid();
        foreach (var p in state.placements)
        {
            if (!p.transform || Quaternion.Angle(p.transform.rotation, Quaternion.Euler(0, p.transform.eulerAngles.y, 0)) > .02f)
                throw new InvalidOperationException("A generated building is missing or tilted.");
            var b = new Box(p.transform.TransformPoint(p.localBounds.center), p.localBounds.size, p.transform.rotation);
            if (roadExclusion.Intersects(b, true)) throw new InvalidOperationException("A generated building intrudes into the road: " + p.transform.name);
            if (occupied.Intersects(b)) throw new InvalidOperationException("Generated building overlap: " + p.transform.name);
            occupied.Add(b);
        }
        var walls = state.transform.Find("04_Smooth_WallColliders").GetComponentsInChildren<MeshCollider>();
        if (walls.Length != 2) throw new InvalidOperationException("Expected exactly two smooth wall colliders.");
        foreach (var wall in walls)
        {
            var material = wall.sharedMaterial;
            if (wall.GetComponent<MeshRenderer>() || !wall.sharedMesh || wall.convex || !material ||
                material.dynamicFriction != 0 || material.staticFriction != 0 || material.bounciness != 0 ||
                material.frictionCombine != PhysicsMaterialCombine.Minimum)
                throw new InvalidOperationException("Invalid wall collider or physics material.");
            var vertices = wall.sharedMesh.vertices;
            if (vertices.Length < 8 || Mathf.Abs((vertices[3] - vertices[0]).y - 3) > .001f)
                throw new InvalidOperationException("Wall height is not 3 m.");
        }
    }

    internal static string Fingerprint(Transform root)
    {
        var text = new StringBuilder();
        foreach (var transform in root.GetComponentsInChildren<Transform>(true))
            foreach (var component in transform.GetComponents<Component>())
                if (component) text.Append(component.GetType().FullName).Append(StableJson(component));
        foreach (var filter in root.GetComponentsInChildren<MeshFilter>(true))
            if (filter.sharedMesh) text.Append(StableJson(filter.sharedMesh));
        return Hash128.Compute(text.ToString()).ToString();
    }

    private static string StableJson(Object obj)
    {
        return Regex.Replace(EditorJsonUtility.ToJson(obj), "\\\"(?:instanceID|entityId)\\\":\\s*([0-9]+)", match => {
            var id = EntityId.FromULong(ulong.Parse(match.Groups[1].Value));
            var referenced = EditorUtility.EntityIdToObject(id);
            return "\"stableReference\":\"" + (referenced ? GlobalObjectId.GetGlobalObjectIdSlow(referenced).ToString() : "null") + "\"";
        });
    }

    private static string ProtectedFingerprint(Scene scene)
    {
        var text = new StringBuilder();
        foreach (var t in All<Transform>(scene).Where(t => t.name == "Main Camera" || t.name == "Directional Light" ||
            t.name == "KB3D_BRK_BldgSM_G_grp" || t.name == "KB3D_BRK_BldgSM_G_grp (1)"))
            if (!t.GetComponentInParent<NfsGeneratedCity>()) text.Append(Fingerprint(t));
        foreach (var t in All<Terrain>(scene).Where(t => t.name == "Main")) text.Append(StableJson(t.transform));
        return Hash128.Compute(text.ToString()).ToString();
    }

    private static void RequirePreserved(Context c)
    {
        if (Fingerprint(c.network) != c.fingerprint || ProtectedFingerprint(c.scene) != c.protectedFingerprint)
            throw new InvalidOperationException("A protected scene object changed. Generation was rolled back.");
    }

    private static Material Material(string folder, string name, Color color, float smoothness)
    {
        var shader = Shader.Find("Universal Render Pipeline/Lit");
        if (!shader) throw new InvalidOperationException("URP/Lit shader is missing.");
        var material = new Material(shader) { name = "NFS_" + name };
        material.SetColor("_BaseColor", color); material.SetFloat("_Smoothness", smoothness);
        AssetDatabase.CreateAsset(material, folder + "/" + name + ".mat"); return material;
    }

    private static void EnsureFolder(string path)
    {
        string parent = "Assets";
        foreach (var part in path.Split('/').Skip(1))
        { string next = parent + "/" + part; if (!AssetDatabase.IsValidFolder(next)) AssetDatabase.CreateFolder(parent, part); parent = next; }
    }

    private static Transform Child(Transform parent, string name)
    {
        var go = new GameObject(name); go.transform.SetParent(parent, false); return go.transform;
    }

    private static IEnumerable<Profile> Shuffled(List<Profile> source, System.Random random)
    {
        var copy = source.ToArray();
        for (int i = copy.Length - 1; i > 0; i--) { int j = random.Next(i + 1); var t = copy[i]; copy[i] = copy[j]; copy[j] = t; }
        return copy;
    }

    private static IEnumerable<T> All<T>(Scene scene) where T : Component => scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<T>(true));
    private static void Progress(string message, float progress)
    {
        if (!Application.isBatchMode && EditorUtility.DisplayCancelableProgressBar("NFS City Generator", message, progress))
            throw new OperationCanceledException();
    }
}

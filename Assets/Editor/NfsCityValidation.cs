using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Box = NfsCityGeometry.Box;
using RoadPath = NfsCityGeometry.Path;

/// <summary>Integration checks against the actual scene, also callable with -executeMethod.</summary>
public static class NfsCityValidation
{
    [Serializable] private sealed class Report
    {
        public string scene;
        public string roadBefore;
        public string roadAfter;
        public string terrainHeightHash;
        public string generatedAssets;
        public int frontBuildings, skylineBuildings, streetProps;
        public int[] skylineByRow;
        public int wallRaycasts;
        public bool geometryChecks, undoGeneration, redoGeneration, undoClear, reloadValidation;
        public string[] errors;
    }

    public static void GenerateAndVerify()
    {
        var errors = new List<string>();
        Application.LogCallback callback = (condition, stack, type) => {
            if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert) errors.Add(condition);
        };
        Application.logMessageReceived += callback;
        try
        {
            GeometryChecks();
            const string scenePath = "Assets/Scenes/FistMission.unity";
            Directory.CreateDirectory("Logs/NfsCity");
            if (!File.Exists("Logs/NfsCity/FistMission.BeforeGeneration.unity"))
                File.Copy(scenePath, "Logs/NfsCity/FistMission.BeforeGeneration.unity");
            var scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
            if (NfsCityGenerator.FindOwnedRoot(scene))
            {
                NfsCityGenerator.ValidateCurrent(); // Also checks fingerprint stability across editor processes.
                NfsCityGenerator.Clear(); Undo.ClearAll();
            }
            var network = scene.GetRootGameObjects().Single(g => g.name == "MainRoad").transform;
            var terrain = scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<Terrain>(true)).Single(t => t.name == "Main");
            var originalData = terrain.terrainData;
            var originalMaterial = terrain.materialTemplate;
            string roadBefore = NfsCityGenerator.Fingerprint(network);
            string heightHash = HeightHash(originalData);
            var city = NfsCityGenerator.Generate(new NfsCityGenerator.Settings());
            var report = new Report { scene = scenePath, roadBefore = roadBefore, terrainHeightHash = heightHash,
                generatedAssets = city.assetFolder, frontBuildings = city.frontBuildings, skylineBuildings = city.skylineBuildings,
                streetProps = city.streetProps, skylineByRow = new[] { city.placements.Count(p => p.row == 1), city.placements.Count(p => p.row == 2) },
                geometryChecks = true };
            Require(city.frontBuildings >= 100, "Frontage is unexpectedly sparse.");
            Require(city.skylineBuildings >= 30 && report.skylineByRow.All(n => n > 0), "Both skyline rows must contain buildings.");
            Require(city.streetProps >= 50, "Street furniture is unexpectedly sparse.");
            Require(HeightHash(terrain.terrainData) == heightHash, "Terrain heights changed.");
            Require(terrain.terrainData != originalData && terrain.terrainData.terrainLayers.Length > 0, "Test terrain appearance was not replaced on a copy.");
            Require(terrain.GetComponent<TerrainCollider>().terrainData == terrain.terrainData, "Terrain visual/collision data differ.");
            foreach (var group in city.placements.Where(p => p.row == 0).GroupBy(p => p.side))
            {
                string previous = null;
                foreach (var p in group.OrderBy(p => p.station))
                { Require(p.prefabPath != previous, "Consecutive frontage prefab repeated."); previous = p.prefabPath; }
            }
            NfsCityGenerator.ValidateCurrent();
            report.wallRaycasts = WallRaycasts(city);
            Undo.FlushUndoRecordObjects(); Undo.PerformUndo();
            Require(!NfsCityGenerator.FindOwnedRoot(scene), "Undo generation did not remove the city.");
            Require(terrain.terrainData == originalData && terrain.materialTemplate == originalMaterial, "Undo did not restore terrain appearance.");
            report.undoGeneration = true;
            Undo.PerformRedo(); city = NfsCityGenerator.FindOwnedRoot(scene);
            Require(city && city.frontBuildings == report.frontBuildings, "Redo generation lost the hierarchy.");
            NfsCityGenerator.ValidateCurrent(); report.redoGeneration = true;
            NfsCityGenerator.Clear(); Undo.FlushUndoRecordObjects();
            Require(!NfsCityGenerator.FindOwnedRoot(scene) && terrain.terrainData == originalData, "Clear did not restore the original terrain.");
            Undo.PerformUndo(); city = NfsCityGenerator.FindOwnedRoot(scene);
            Require(city && terrain.terrainData == city.generatedTerrainData, "Undo Clear did not restore the generated terrain.");
            NfsCityGenerator.ValidateCurrent(); report.undoClear = true;
            report.roadAfter = NfsCityGenerator.Fingerprint(network);
            Require(report.roadBefore == report.roadAfter, "Protected MainRoad changed.");
            Require(errors.Count == 0, "Unity logged errors: " + string.Join("; ", errors));
            AssetDatabase.SaveAssets();
            Require(EditorSceneManager.SaveScene(scene), "Unable to save FistMission.");
            scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
            NfsCityGenerator.ValidateCurrent(); report.reloadValidation = true;
            city = NfsCityGenerator.FindOwnedRoot(scene);
            Require(HeightHash(city.terrain.terrainData) == heightHash, "Terrain heights changed after scene reload.");
            Require(errors.Count == 0, "Unity logged errors after reload: " + string.Join("; ", errors));
            report.errors = errors.ToArray();
            File.WriteAllText("Logs/NfsCity/verification.json", JsonUtility.ToJson(report, true));
            Debug.Log("[NFS City] INTEGRATION PASSED: " + JsonUtility.ToJson(report));
        }
        finally { Application.logMessageReceived -= callback; }
    }

    public static void VerifySavedScene()
    {
        EditorSceneManager.OpenScene("Assets/Scenes/FistMission.unity", OpenSceneMode.Single);
        Debug.Log("[NFS City] SAVED SCENE: " + NfsCityGenerator.ValidateCurrent());
    }

    private static int WallRaycasts(NfsGeneratedCity city)
    {
        Physics.SyncTransforms();
        var walls = city.transform.Find("04_Smooth_WallColliders").GetComponentsInChildren<MeshCollider>();
        int count = 0;
        foreach (var wall in walls)
        {
            var mesh = wall.sharedMesh;
            var vertices = mesh.vertices;
            for (int ring = 8; ring < vertices.Length / 4 - 2; ring += 97)
            {
                int i = ring * 4;
                Vector3 a = wall.transform.TransformPoint((vertices[i] + vertices[i + 4]) * .5f);
                Vector3 outward = ((vertices[i + 1] - vertices[i]) + (vertices[i + 5] - vertices[i + 4])).normalized;
                var ray = new Ray(a - outward * 2 + Vector3.up, outward);
                Require(wall.Raycast(ray, out var hit, 4), "Wall has an inward-facing gap at ring " + ring + ": " + wall.name);
                Require(hit.normal.y < .2f && Vector3.Dot(hit.normal, outward) < -.8f, "Wall collision normal points away from the driving surface.");
                count++;
            }
        }
        Require(count > 20, "Insufficient wall collision samples."); return count;
    }

    private static void GeometryChecks()
    {
        var a = new Box(Vector3.zero, new Vector3(10, 20, 2), Quaternion.Euler(0, 45, 0));
        var b = new Box(new Vector3(0, 0, .5f), new Vector3(1, 5, 1), Quaternion.identity);
        Require(a.Intersects(b), "Rotated building overlap was missed.");
        b = new Box(new Vector3(0, 30, .5f), new Vector3(1, 5, 1), Quaternion.identity);
        Require(!a.Intersects(b), "Separated heights should not collide.");
        b = new Box(new Vector3(0, 0, 8), new Vector3(1, 5, 1), Quaternion.identity);
        Require(!a.Intersects(b), "Rotated, separated buildings reported an overlap.");
        var center = new[] { Vector3.zero, new Vector3(0, 0, 100) };
        var left = center.Select(p => p - Vector3.right * 10).ToArray();
        var right = center.Select(p => p + Vector3.right * 10).ToArray();
        var path = new RoadPath(center, left, right, 20, false);
        var grid = path.RoadExclusion(.5f);
        Require(grid.Intersects(new Box(new Vector3(0, 0, 50), Vector3.one, Quaternion.identity), true), "Road exclusion missed the space between spline points.");
        Require(!grid.Intersects(new Box(new Vector3(20, 0, 50), Vector3.one, Quaternion.identity), true), "Road exclusion is too wide.");
        Require(Mathf.Abs(path.Sample(50, 1).edge.x - 10) < .001f, "Road edge sampling failed.");
        for (int side = -1; side <= 1; side += 2)
        {
            var mesh = NfsCityGeometry.Ribbon(path, side, .25f, .12f, 3);
            try
            {
                Require(mesh.vertexCount > 8, "Wall extrusion failed.");
                var vertices = mesh.vertices; var indices = mesh.triangles;
                // Ignore end caps and check the driving-facing side's actual geometric normal.
                int t = 6 + 3 * 6;
                Vector3 normal = Vector3.Cross(vertices[indices[t + 1]] - vertices[indices[t]], vertices[indices[t + 2]] - vertices[indices[t]]).normalized;
                Require(Vector3.Dot(normal, Vector3.right * side) < -.99f, "Extruded wall winding is inverted.");
            }
            finally { UnityEngine.Object.DestroyImmediate(mesh); }
        }
    }

    private static string HeightHash(TerrainData data)
    {
        var heights = data.GetHeights(0, 0, data.heightmapResolution, data.heightmapResolution);
        var bytes = new byte[heights.Length * sizeof(float)]; Buffer.BlockCopy(heights, 0, bytes, 0, bytes.Length);
        using (var sha = SHA256.Create()) return BitConverter.ToString(sha.ComputeHash(bytes)).Replace("-", "").ToLowerInvariant();
    }

    private static void Require(bool condition, string message)
    { if (!condition) throw new InvalidOperationException("[NFS City verification] " + message); }
}

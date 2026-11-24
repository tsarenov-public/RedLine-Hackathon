using System;
using System.Collections.Generic;
using System.Linq;
using Arcade.Vehicle;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

internal static class LamborghiniRaceAuthoring
{
    private const string Folder = "Assets/Cars/race cars/lamborgini/";
    private const string Decals = Folder + "Decals/";
    private const string CarPath = Folder + "FINAL_MODEL_SV97_Car 1.prefab";
    private const string EquipmentPath = Folder + "Diablo Race Equipment.prefab";
    private const string Stage = "Assets/Scenes/Stage_0.unity";
    private const string Fx = "Assets/Cars/SmokeAndSkidmarks/";
    private const float VinylOffset = .004f;

    [MenuItem("Tools/Arcade Vehicle/Build Diablo race equipment")]
    public static void Build()
    {
        var car = PrefabUtility.LoadPrefabContents(CarPath);
        GameObject equipment;
        try
        {
            equipment = BuildEquipment(car.transform);
            Fit(car.GetComponent<ArcadeVehicle>(), equipment);
            if (PrefabUtility.SaveAsPrefabAsset(car, CarPath) == null)
                throw new InvalidOperationException("Could not save Diablo prefab.");
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(car);
        }

        var stage = SceneManager.GetSceneByPath(Stage);
        bool openedHere = !stage.isLoaded;
        if (openedHere) stage = EditorSceneManager.OpenScene(Stage, OpenSceneMode.Additive);
        try
        {
            bool changed = false;
            foreach (var vehicle in stage.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<ArcadeVehicle>(true)))
            {
                if (!vehicle.name.StartsWith("FINAL_MODEL_SV97", StringComparison.Ordinal)) continue;
                Fit(vehicle, equipment);
                changed = true;
            }
            if (changed && !EditorSceneManager.SaveScene(stage))
                throw new InvalidOperationException("Could not save Stage_0 Diablo equipment.");
        }
        finally
        {
            if (openedHere) EditorSceneManager.CloseScene(stage, true);
        }
    }

    private static GameObject BuildEquipment(Transform car)
    {
        var paint = car.GetComponentsInChildren<MeshFilter>(true).Single(f => f.name.Contains(":Paint_Geo"));
        var hood = BuildOverlay(car, paint, true);
        var sides = BuildOverlay(car, paint, false);
        var root = new GameObject("Diablo Race Equipment") { layer = car.gameObject.layer };
        try
        {
            AddVinyl(root.transform, "Hood Stripes", hood, VinylMaterial("Diablo_Hood"));
            AddVinyl(root.transform, "Door Numbers and Accents", sides, VinylMaterial("Diablo_Quarter_97"));
            var flameMaterial = FlameMaterial();
            // The Diablo mesh has two pairs of round chrome tips below the rear fascia.
            AddFlame(root.transform, "Left Outer NOS Flame", new Vector3(-.352f, .284f, -2.090f), flameMaterial);
            AddFlame(root.transform, "Left Inner NOS Flame", new Vector3(-.250f, .284f, -2.090f), flameMaterial);
            AddFlame(root.transform, "Right Inner NOS Flame", new Vector3(.250f, .284f, -2.090f), flameMaterial);
            AddFlame(root.transform, "Right Outer NOS Flame", new Vector3(.352f, .284f, -2.090f), flameMaterial);
            AddBrakeLight(root.transform, "Left Brake Glow", new Vector3(-.763f, .730f, -2.079f));
            AddBrakeLight(root.transform, "Right Brake Glow", new Vector3(.763f, .730f, -2.079f));
            return PrefabUtility.SaveAsPrefabAsset(root, EquipmentPath)
                ?? throw new InvalidOperationException("Could not save Diablo equipment.");
        }
        finally
        {
            Object.DestroyImmediate(root);
        }
    }

    private static void Fit(ArcadeVehicle car, GameObject equipment)
    {
        var child = car.transform.Find(equipment.name);
        if (child == null) child = ((GameObject)PrefabUtility.InstantiatePrefab(equipment, car.transform)).transform;
        var effects = car.GetComponent<VehicleEffects>();
        if (effects == null) effects = car.gameObject.AddComponent<VehicleEffects>();
        effects.vehicle = car;
        effects.smokePrefab = AssetDatabase.LoadAssetAtPath<GameObject>(Fx +
            "Prefabs/Prefabs/Particles/P_WheelSmokeAsphalt.prefab").GetComponent<ParticleSystem>();
        effects.skidmarksPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(Fx +
            "UnitySkidmarks/SkidmarksController.prefab").GetComponent<Skidmarks>();
        effects.nosFlames = child.GetComponentsInChildren<ParticleSystem>(true);
        effects.brakeLights = child.GetComponentsInChildren<Light>(true);
        Debug.Log($"RACE_HUD Diablo {car.name}: {car.wheelRigs.Length} wheel contacts, {effects.nosFlames.Length} flames.");
    }

    private static Mesh BuildOverlay(Transform car, MeshFilter paint, bool hood)
    {
        var source = paint.sharedMesh;
        var positionTransform = car.worldToLocalMatrix * paint.transform.localToWorldMatrix;
        var normalTransform = positionTransform.inverse.transpose;
        var positions = source.vertices;
        var normals = source.normals;
        for (int i = 0; i < positions.Length; i++)
        {
            positions[i] = positionTransform.MultiplyPoint3x4(positions[i]);
            normals[i] = normalTransform.MultiplyVector(normals[i]).normalized;
        }

        var vertices = new List<Vector3>();
        var uv = new List<Vector2>();
        var surfaceNormals = new List<Vector3>();
        var outputIndices = new List<int>();
        var projected = new Dictionary<int, int>();
        int[] indices = source.triangles;
        for (int i = 0; i < indices.Length; i += 3)
        {
            var a = positions[indices[i]];
            var b = positions[indices[i + 1]];
            var c = positions[indices[i + 2]];
            var normal = (normals[indices[i]] + normals[indices[i + 1]] + normals[indices[i + 2]]).normalized;
            if (hood
                ? normal.y < .50f || !Overlaps(a.z, b.z, c.z, .61f, 2.10f) || !Overlaps(a.x, b.x, c.x, -.57f, .57f)
                : Mathf.Abs(normal.x) < .57f || !Overlaps(a.z, b.z, c.z, -1.31f, 1.14f) ||
                    !Overlaps(a.y, b.y, c.y, .30f, .78f)) continue;

            for (int corner = i; corner < i + 3; corner++)
            {
                int index = indices[corner];
                int chart = hood || normal.x > 0f ? 0 : 1;
                int key = index * 2 + chart;
                if (projected.TryGetValue(key, out int existing))
                {
                    outputIndices.Add(existing);
                    continue;
                }

                var p = positions[index];
                projected.Add(key, vertices.Count);
                outputIndices.Add(vertices.Count);
                vertices.Add(p + normals[index] * VinylOffset);
                surfaceNormals.Add(normals[index]);
                if (hood)
                    uv.Add(new Vector2((p.x + .57f) / 1.14f, (p.z - .61f) / 1.49f));
                else
                {
                    float across = (p.z + 1.31f) / 2.45f;
                    uv.Add(new Vector2(chart == 0 ? across : 1f - across, (p.y - .30f) / .48f));
                }
            }
        }

        if (vertices.Count == 0) throw new InvalidOperationException("No paint triangles for Diablo vinyl.");
        var mesh = new Mesh { name = hood ? "Diablo Hood Overlay" : "Diablo Side Overlays" };
        mesh.SetVertices(vertices);
        mesh.SetNormals(surfaceNormals);
        mesh.SetUVs(0, uv);
        mesh.SetTriangles(outputIndices, 0);
        mesh.RecalculateTangents();
        mesh.RecalculateBounds();
        Debug.Log($"RACE_HUD livery {mesh.name}: {outputIndices.Count / 3} triangles.");
        return SaveAsset(mesh, Decals + mesh.name + ".asset");
    }

    private static bool Overlaps(float a, float b, float c, float min, float max) =>
        Mathf.Max(a, Mathf.Max(b, c)) >= min && Mathf.Min(a, Mathf.Min(b, c)) <= max;

    private static void AddVinyl(Transform parent, string name, Mesh mesh, Material material)
    {
        var vinyl = new GameObject(name, typeof(MeshFilter), typeof(MeshRenderer)) { layer = parent.gameObject.layer };
        vinyl.transform.SetParent(parent, false);
        vinyl.GetComponent<MeshFilter>().sharedMesh = mesh;
        var renderer = vinyl.GetComponent<MeshRenderer>();
        renderer.sharedMaterial = material;
        renderer.shadowCastingMode = ShadowCastingMode.Off;
    }

    private static Material VinylMaterial(string artwork)
    {
        string path = Decals + artwork + ".png";
        var importer = (TextureImporter)AssetImporter.GetAtPath(path);
        importer.alphaIsTransparency = true;
        importer.wrapMode = TextureWrapMode.Clamp;
        importer.textureCompression = TextureImporterCompression.Uncompressed;
        importer.maxTextureSize = 2048;
        importer.SaveAndReimport();
        var material = new Material(Shader.Find("Universal Render Pipeline/Lit")) { name = artwork };
        material.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>(path));
        material.SetFloat("_AlphaClip", 1f);
        material.SetFloat("_Cutoff", .48f);
        material.SetFloat("_Smoothness", .62f);
        material.SetFloat("_Metallic", .12f);
        material.EnableKeyword("_ALPHATEST_ON");
        material.SetOverrideTag("RenderType", "TransparentCutout");
        material.renderQueue = (int)RenderQueue.AlphaTest + 5;
        return SaveAsset(material, Decals + artwork + ".mat");
    }

    private static Material FlameMaterial()
    {
        var material = new Material(Shader.Find("Universal Render Pipeline/Particles/Unlit")) { name = "Diablo NOS Flame" };
        material.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>(Fx +
            "Textures/Textures/Particles/ParticleFlare.png"));
        material.SetFloat("_Surface", 1f);
        material.SetFloat("_Blend", 2f);
        material.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
        material.SetFloat("_DstBlend", (float)BlendMode.One);
        material.SetFloat("_ZWrite", 0f);
        material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        material.SetOverrideTag("RenderType", "Transparent");
        material.renderQueue = (int)RenderQueue.Transparent + 9;
        return SaveAsset(material, Folder + "Diablo NOS Flame.mat");
    }

    private static void AddFlame(Transform parent, string name, Vector3 position, Material material)
    {
        var outlet = new GameObject(name, typeof(ParticleSystem)) { layer = parent.gameObject.layer };
        var particles = outlet.GetComponent<ParticleSystem>();
        var main = particles.main;
        main.playOnAwake = false;
        main.loop = true;
        main.startLifetime = new ParticleSystem.MinMaxCurve(.075f, .14f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(4.8f, 7.5f);
        main.startSize = new ParticleSystem.MinMaxCurve(.095f, .16f);
        main.simulationSpace = ParticleSystemSimulationSpace.Local;
        main.maxParticles = 48;
        particles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        outlet.transform.SetParent(parent, false);
        outlet.transform.SetLocalPositionAndRotation(position, Quaternion.LookRotation(Vector3.back));
        var shape = particles.shape;
        shape.shapeType = ParticleSystemShapeType.Cone;
        shape.angle = 7f;
        shape.radius = .028f;
        var emission = particles.emission;
        emission.rateOverTime = 0f;

        var tint = particles.colorOverLifetime;
        tint.enabled = true;
        var gradient = new Gradient();
        gradient.SetKeys(new[]
        {
            new GradientColorKey(new Color(1f, .96f, .72f), 0),
            new GradientColorKey(new Color(.37f, .56f, 1f), .25f),
            new GradientColorKey(new Color(1f, .30f, .035f), .76f)
        }, new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(.92f, .30f), new GradientAlphaKey(0f, 1f) });
        tint.color = gradient;
        var renderer = outlet.GetComponent<ParticleSystemRenderer>();
        renderer.sharedMaterial = material;
        renderer.renderMode = ParticleSystemRenderMode.Stretch;
        renderer.lengthScale = 2.6f;
        renderer.velocityScale = .10f;
    }

    private static void AddBrakeLight(Transform parent, string name, Vector3 position)
    {
        var lamp = new GameObject(name, typeof(Light)) { layer = parent.gameObject.layer };
        lamp.transform.SetParent(parent, false);
        lamp.transform.SetLocalPositionAndRotation(position, Quaternion.LookRotation(Vector3.back));
        var light = lamp.GetComponent<Light>();
        light.type = LightType.Spot;
        light.color = new Color(1f, .075f, .034f);
        light.intensity = 2.5f;
        light.range = 4f;
        light.spotAngle = 77f;
        light.shadows = LightShadows.None;
        light.enabled = false;
    }

    private static T SaveAsset<T>(T asset, string path) where T : Object
    {
        var existing = AssetDatabase.LoadAssetAtPath<T>(path);
        if (existing == null)
        {
            AssetDatabase.CreateAsset(asset, path);
            return asset;
        }

        EditorUtility.CopySerialized(asset, existing);
        Object.DestroyImmediate(asset);
        EditorUtility.SetDirty(existing);
        AssetDatabase.SaveAssetIfDirty(existing);
        return existing;
    }
}

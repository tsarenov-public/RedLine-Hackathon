using System;
using System.Linq;
using Arcade.Vehicle;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

internal static class RaceCameraAuthoring
{
    private const string CameraFolder = "Assets/Cars/Camera/";
    private const string DebrisFolder = "Assets/Cars/Effects/SpeedDebris/";
    private const string DebrisPrefab = DebrisFolder + "WindDebrisParticles.prefab";
    private const string StagePath = "Assets/Scenes/Stage_0.unity";
    private const string WheelEffectsFolder = "Assets/Cars/SmokeAndSkidmarks/";

    [MenuItem("Tools/Arcade Vehicle/Build race camera and wind effects")]
    public static void Build()
    {
        var windMaterial = ParticleMaterial(CameraFolder + "Speed Lines.mat", CameraFolder + "Speed_Line.png", true);
        var paperMaterial = ParticleMaterial(DebrisFolder + "Wind Paper.mat", DebrisFolder + "Speed_Debris_Paper.png", false);
        var debris = BuildDebrisPrefab(paperMaterial);
        var smoke = AssetDatabase.LoadAssetAtPath<GameObject>(WheelEffectsFolder +
            "Prefabs/Prefabs/Particles/P_WheelSmokeAsphalt.prefab").GetComponent<ParticleSystem>();
        var marks = AssetDatabase.LoadAssetAtPath<GameObject>(WheelEffectsFolder +
            "UnitySkidmarks/SkidmarksController.prefab").GetComponent<Skidmarks>();
        foreach (string guid in AssetDatabase.FindAssets("t:Prefab", new[] { "Assets/Cars" }))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            var asset = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (asset == null || asset.GetComponentInChildren<ArcadeVehicle>(true) == null) continue;
            var root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                foreach (var car in root.GetComponentsInChildren<ArcadeVehicle>(true))
                {
                    BindDebris(car, debris);
                    BindWheelEffects(car, smoke, marks);
                }
                if (PrefabUtility.SaveAsPrefabAsset(root, path) == null)
                    throw new InvalidOperationException("Could not save " + path);
                Debug.Log("RACE_CAMERA prefab " + path);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        var stage = SceneManager.GetSceneByPath(StagePath);
        bool openedHere = !stage.isLoaded;
        if (openedHere) stage = EditorSceneManager.OpenScene(StagePath, OpenSceneMode.Additive);
        try
        {
            var roots = stage.GetRootGameObjects();
            var camera = roots.SelectMany(root => root.GetComponentsInChildren<Camera>(true))
                .Single(cam => cam.CompareTag("MainCamera"));
            BuildStageCamera(camera, roots, windMaterial);
            foreach (var car in roots.SelectMany(root => root.GetComponentsInChildren<ArcadeVehicle>(true)))
            {
                BindDebris(car, debris);
                BindWheelEffects(car, smoke, marks);
                Debug.Log("RACE_CAMERA Stage_0 " + car.name);
            }
            if (!EditorSceneManager.SaveScene(stage))
                throw new InvalidOperationException("Could not save " + StagePath);
        }
        finally
        {
            if (openedHere) EditorSceneManager.CloseScene(stage, true);
        }
    }

    private static void BuildStageCamera(Camera camera, GameObject[] roots, Material material)
    {
        if (camera.TryGetComponent<VehicleChaseCamera>(out var legacy)) legacy.enabled = false;
        var follow = GetOrAdd<NFSCameraController>(camera.gameObject);
        follow.baseDistance = 3f;
        follow.heightOffset = 1.86f;
        follow.lookAtOffset = new Vector3(0f, .54f, 1.30f);
        follow.pitchAngleOffset = 14.4f;
        follow.positionDamping = .16f;
        follow.rotationDamping = .115f;
        follow.cornerOffset = .29f;
        var effects = GetOrAdd<CameraSpeedEffects>(camera.gameObject);
        camera.fieldOfView = effects.baseFov;
        camera.nearClipPlane = .15f;

        var flow = Child(camera.transform, "Peripheral Speed Lines");
        flow.transform.SetLocalPositionAndRotation(new Vector3(0, 0, 12f), Quaternion.Euler(0, 180f, 0));
        flow.layer = camera.gameObject.layer;
        var particles = GetOrAdd<ParticleSystem>(flow);
        var main = particles.main;
        main.playOnAwake = false;
        main.loop = true;
        main.startLifetime = new ParticleSystem.MinMaxCurve(.17f, .25f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(45f, 70f);
        main.startSize = new ParticleSystem.MinMaxCurve(.10f, .22f);
        main.startColor = new Color(.82f, .91f, 1f, .57f);
        main.simulationSpace = ParticleSystemSimulationSpace.Local;
        main.cullingMode = ParticleSystemCullingMode.AlwaysSimulate;
        main.maxParticles = 96;
        var shape = particles.shape;
        shape.enabled = true;
        shape.shapeType = ParticleSystemShapeType.Donut;
        shape.radius = 1f;
        shape.donutRadius = .035f;
        shape.scale = new Vector3(12.1f, 6.44f, .25f);
        var velocity = particles.velocityOverLifetime;
        velocity.enabled = false;
        SetFade(particles, .88f, .12f, .69f);
        var renderer = particles.GetComponent<ParticleSystemRenderer>();
        renderer.renderMode = ParticleSystemRenderMode.Stretch;
        renderer.lengthScale = 17f;
        renderer.velocityScale = .34f;
        ConfigureRenderer(renderer, material);
        SetIdle(particles);

        var baseVolume = roots.SelectMany(root => root.GetComponentsInChildren<Volume>(true))
            .Where(volume => volume.isGlobal && volume.sharedProfile != null && volume.name != "Speed Post FX")
            .OrderByDescending(volume => volume.priority).FirstOrDefault();
        Volume speedVolume = null;
        if (baseVolume != null)
        {
            var host = Child(camera.transform, "Speed Post FX");
            host.layer = baseVolume.gameObject.layer;
            speedVolume = GetOrAdd<Volume>(host);
            speedVolume.isGlobal = true;
            speedVolume.priority = baseVolume.priority + 1f;
            speedVolume.weight = 0f;
            speedVolume.sharedProfile = BuildVolumeProfile();
            var cameraData = GetOrAdd<UniversalAdditionalCameraData>(camera.gameObject);
            cameraData.renderPostProcessing = true;
            cameraData.volumeLayerMask |= 1 << host.layer;
        }

        var binding = new SerializedObject(effects);
        binding.FindProperty("speedLines").objectReferenceValue = particles;
        binding.FindProperty("speedVolume").objectReferenceValue = speedVolume;
        binding.ApplyModifiedPropertiesWithoutUndo();
        Debug.Log($"RACE_CAMERA Main Camera base {follow.baseDistance} m / {effects.baseFov} degrees, URP Volume {speedVolume != null}");
    }

    private static VolumeProfile BuildVolumeProfile()
    {
        string path = CameraFolder + "Speed Post FX.asset";
        var profile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(path);
        if (profile == null)
        {
            profile = ScriptableObject.CreateInstance<VolumeProfile>();
            AssetDatabase.CreateAsset(profile, path);
        }

        var chromatic = GetOrAddOverride<ChromaticAberration>(profile);
        chromatic.intensity.Override(.16f);
        var vignette = GetOrAddOverride<Vignette>(profile);
        vignette.intensity.Override(.38f);
        vignette.smoothness.Override(.44f);
        var lens = GetOrAddOverride<LensDistortion>(profile);
        lens.intensity.Override(-.12f);
        EditorUtility.SetDirty(chromatic);
        EditorUtility.SetDirty(vignette);
        EditorUtility.SetDirty(lens);
        EditorUtility.SetDirty(profile);
        AssetDatabase.SaveAssets();
        return profile;
    }

    private static T GetOrAddOverride<T>(VolumeProfile profile) where T : VolumeComponent
    {
        if (profile.TryGet<T>(out var component)) return component;
        component = profile.Add<T>();
        component.name = typeof(T).Name;
        AssetDatabase.AddObjectToAsset(component, profile);
        return component;
    }

    private static GameObject BuildDebrisPrefab(Material material)
    {
        var root = new GameObject("WindDebrisParticles", typeof(SpeedDebrisController));
        try
        {
            var binding = new SerializedObject(root.GetComponent<SpeedDebrisController>());
            binding.FindProperty("left").objectReferenceValue = CreatePaperEmitter(root.transform, "Left", -7.4f, material);
            binding.FindProperty("right").objectReferenceValue = CreatePaperEmitter(root.transform, "Right", 7.4f, material);
            binding.ApplyModifiedPropertiesWithoutUndo();
            return PrefabUtility.SaveAsPrefabAsset(root, DebrisPrefab)
                ?? throw new InvalidOperationException("Could not save wind particles prefab.");
        }
        finally
        {
            Object.DestroyImmediate(root);
        }
    }

    private static ParticleSystem CreatePaperEmitter(Transform parent, string name, float side, Material material)
    {
        var emitter = new GameObject(name, typeof(ParticleSystem));
        emitter.transform.SetParent(parent, false);
        emitter.transform.SetLocalPositionAndRotation(new Vector3(side, 1.6f, 12.5f), Quaternion.Euler(0, 180f, 0));
        var particles = emitter.GetComponent<ParticleSystem>();
        var main = particles.main;
        main.playOnAwake = false;
        main.loop = true;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.scalingMode = ParticleSystemScalingMode.Local;
        main.maxParticles = 70;
        main.startLifetime = new ParticleSystem.MinMaxCurve(1.5f, 2f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(24f, 38f);
        main.gravityModifier = .25f;
        main.startSize3D = true;
        main.startSizeX = new ParticleSystem.MinMaxCurve(.32f, .38f);
        main.startSizeY = new ParticleSystem.MinMaxCurve(.46f, .54f);
        main.startSizeZ = 1f;
        main.startRotation3D = true;
        main.startRotationX = RandomAngle();
        main.startRotationY = RandomAngle();
        main.startRotationZ = RandomAngle();
        var shape = particles.shape;
        shape.shapeType = ParticleSystemShapeType.Box;
        shape.scale = new Vector3(1.2f, 2.7f, 1.25f);

        var noise = particles.noise;
        noise.enabled = true;
        noise.strength = 3.5f;
        noise.frequency = 1.2f;
        noise.rotationAmount = 2f;
        var spin = particles.rotationOverLifetime;
        spin.enabled = true;
        spin.separateAxes = true;
        spin.x = new ParticleSystem.MinMaxCurve(-2.5f, 3.7f);
        spin.y = new ParticleSystem.MinMaxCurve(-3.9f, 2.9f);
        spin.z = new ParticleSystem.MinMaxCurve(-3.6f, 3.6f);

        var sheet = particles.textureSheetAnimation;
        sheet.enabled = true;
        sheet.numTilesX = sheet.numTilesY = 2;
        sheet.frameOverTime = 0f;
        sheet.startFrame = new ParticleSystem.MinMaxCurve(0f, .999f);
        SetFade(particles, .98f, .04f, .73f);
        var renderer = particles.GetComponent<ParticleSystemRenderer>();
        renderer.renderMode = ParticleSystemRenderMode.Mesh;
        renderer.mesh = PaperQuad();
        renderer.enableGPUInstancing = false;
        ConfigureRenderer(renderer, material);
        SetIdle(particles);
        return particles;
    }

    private static void BindDebris(ArcadeVehicle car, GameObject prefab)
    {
        var emitter = car.transform.Find(prefab.name);
        if (emitter == null)
            emitter = ((GameObject)PrefabUtility.InstantiatePrefab(prefab, car.transform)).transform;
        emitter.SetLocalPositionAndRotation(Vector3.zero, Quaternion.identity);
        var controller = emitter.GetComponent<SpeedDebrisController>();
        var binding = new SerializedObject(controller);
        binding.FindProperty("vehicle").objectReferenceValue = car;
        binding.ApplyModifiedPropertiesWithoutUndo();
        PrefabUtility.RecordPrefabInstancePropertyModifications(emitter);
        PrefabUtility.RecordPrefabInstancePropertyModifications(controller);
    }

    private static void BindWheelEffects(ArcadeVehicle car, ParticleSystem smoke, Skidmarks marks)
    {
        var effects = GetOrAdd<VehicleEffects>(car.gameObject);
        effects.vehicle = car;
        if (effects.smokePrefab == null) effects.smokePrefab = smoke;
        if (effects.skidmarksPrefab == null && effects.skidmarks == null) effects.skidmarksPrefab = marks;
        PrefabUtility.RecordPrefabInstancePropertyModifications(effects);
    }

    private static void SetIdle(ParticleSystem particles)
    {
        particles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        var emission = particles.emission;
        emission.rateOverTime = 0f;
        emission.rateOverDistance = 0f;
        emission.SetBursts(Array.Empty<ParticleSystem.Burst>());
    }

    private static void SetFade(ParticleSystem particles, float peak, float rise, float fall)
    {
        var curve = new Gradient();
        curve.SetKeys(new[] { new GradientColorKey(Color.white, 0), new GradientColorKey(Color.white, 1) },
            new[] { new GradientAlphaKey(0, 0), new GradientAlphaKey(peak, rise),
                new GradientAlphaKey(peak, fall), new GradientAlphaKey(0, 1) });
        var tint = particles.colorOverLifetime;
        tint.enabled = true;
        tint.color = curve;
    }

    private static void ConfigureRenderer(ParticleSystemRenderer renderer, Material material)
    {
        renderer.sharedMaterial = material;
        renderer.shadowCastingMode = ShadowCastingMode.Off;
        renderer.receiveShadows = false;
    }

    private static Material ParticleMaterial(string path, string texturePath, bool lines)
    {
        var importer = (TextureImporter)AssetImporter.GetAtPath(texturePath);
        if (!importer.alphaIsTransparency || importer.wrapMode != TextureWrapMode.Clamp)
        {
            importer.alphaIsTransparency = true;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.SaveAndReimport();
        }

        var shader = Shader.Find("Arcade/Peripheral Particles");
        var material = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (material == null)
        {
            material = new Material(shader);
            AssetDatabase.CreateAsset(material, path);
        }

        int queue = material.renderQueue;
        var tint = material.HasProperty("_BaseColor") ? material.GetColor("_BaseColor") : Color.white;
        material.shader = shader;
        material.shaderKeywords = Array.Empty<string>();
        material.renderQueue = queue;
        material.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>(texturePath));
        material.SetColor("_BaseColor", tint);
        material.SetFloat("_SideStart", lines ? .70f : .68f);
        material.SetFloat("_TopStart", .76f);
        material.SetFloat("_Feather", .09f);
        material.SetFloat("_TopWeight", lines ? 1f : 0f);
        material.SetFloat("_Brightness", lines ? 1f : 1.3f);
        material.SetFloat("_Cull", (float)CullMode.Off);
        EditorUtility.SetDirty(material);
        AssetDatabase.SaveAssetIfDirty(material);
        return material;
    }

    private static Mesh PaperQuad()
    {
        string path = DebrisFolder + "Wind Paper Quad.asset";
        var asset = AssetDatabase.LoadAssetAtPath<Mesh>(path);
        if (asset != null) return asset;
        var mesh = new Mesh { name = "Wind Paper Quad" };
        mesh.vertices = new[] { new Vector3(-.5f, -.5f), new Vector3(.5f, -.5f),
            new Vector3(.5f, .5f), new Vector3(-.5f, .5f) };
        mesh.uv = new[] { new Vector2(0, 0), new Vector2(1, 0), new Vector2(1, 1), new Vector2(0, 1) };
        mesh.triangles = new[] { 0, 1, 2, 0, 2, 3 };
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
        AssetDatabase.CreateAsset(mesh, path);
        return mesh;
    }

    private static ParticleSystem.MinMaxCurve RandomAngle() => new(0f, Mathf.PI * 2f);

    private static GameObject Child(Transform parent, string name)
    {
        var current = parent.Find(name);
        if (current != null) return current.gameObject;
        var child = new GameObject(name);
        child.transform.SetParent(parent, false);
        return child;
    }

    private static T GetOrAdd<T>(GameObject target) where T : Component =>
        target.TryGetComponent<T>(out var existing) ? existing : target.AddComponent<T>();
}

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

    [MenuItem("Tools/Arcade Vehicle/Build race camera and wind effects")]
    public static void Build()
    {
        var windMaterial = ParticleMaterial(CameraFolder + "Speed Lines.mat", CameraFolder + "Speed_Line.png");
        var paperMaterial = ParticleMaterial(DebrisFolder + "Wind Paper.mat", DebrisFolder + "Speed_Debris_Paper.png");
        var debris = BuildDebrisPrefab(paperMaterial);
        foreach (string guid in AssetDatabase.FindAssets("t:Prefab", new[] { "Assets/Cars" }))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            var asset = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (asset == null || asset.GetComponentInChildren<ArcadeVehicle>(true) == null) continue;
            var root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                foreach (var car in root.GetComponentsInChildren<ArcadeVehicle>(true)) BindDebris(car, debris);
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
        main.startLifetime = new ParticleSystem.MinMaxCurve(.22f, .32f);
        main.startSpeed = 0f;
        main.startSize = new ParticleSystem.MinMaxCurve(.055f, .11f);
        main.startColor = new Color(.82f, .91f, 1f, .64f);
        main.simulationSpace = ParticleSystemSimulationSpace.Local;
        main.maxParticles = 64;
        var shape = particles.shape;
        shape.enabled = true;
        shape.shapeType = ParticleSystemShapeType.Circle;
        shape.radius = 1f;
        shape.radiusThickness = .035f;
        shape.scale = new Vector3(11.5f, 6.3f, 1f);
        var velocity = particles.velocityOverLifetime;
        velocity.enabled = true;
        velocity.space = ParticleSystemSimulationSpace.Local;
        velocity.z = 35f;
        SetFade(particles, .78f, .16f, .62f);
        var renderer = particles.GetComponent<ParticleSystemRenderer>();
        renderer.renderMode = ParticleSystemRenderMode.Stretch;
        renderer.lengthScale = 5.5f;
        renderer.velocityScale = .095f;
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
        var root = new GameObject("WindDebrisParticles", typeof(ParticleSystem), typeof(SpeedDebrisController));
        try
        {
            var particles = root.GetComponent<ParticleSystem>();
            var main = particles.main;
            main.playOnAwake = false;
            main.loop = true;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.scalingMode = ParticleSystemScalingMode.Local;
            main.maxParticles = 88;
            main.startLifetime = new ParticleSystem.MinMaxCurve(1.5f, 2f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(4.2f, 8.2f);
            main.gravityModifier = .25f;
            main.startSize3D = true;
            main.startSizeX = new ParticleSystem.MinMaxCurve(.19f, .28f);
            main.startSizeY = new ParticleSystem.MinMaxCurve(.27f, .38f);
            main.startSizeZ = 1f;
            main.startRotation3D = true;
            main.startRotationX = RandomAngle();
            main.startRotationY = RandomAngle();
            main.startRotationZ = RandomAngle();
            var shape = particles.shape;
            shape.shapeType = ParticleSystemShapeType.Box;
            shape.scale = new Vector3(2.8f, .15f, 1f);
            var lift = particles.velocityOverLifetime;
            lift.enabled = true;
            lift.space = ParticleSystemSimulationSpace.World;
            lift.y = 3.1f;

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
            SetFade(particles, .94f, .08f, .73f);
            var renderer = particles.GetComponent<ParticleSystemRenderer>();
            renderer.renderMode = ParticleSystemRenderMode.Mesh;
            renderer.mesh = PaperQuad();
            renderer.enableGPUInstancing = false;
            ConfigureRenderer(renderer, material);
            SetIdle(particles);
            return PrefabUtility.SaveAsPrefabAsset(root, DebrisPrefab)
                ?? throw new InvalidOperationException("Could not save wind particles prefab.");
        }
        finally
        {
            Object.DestroyImmediate(root);
        }
    }

    private static void BindDebris(ArcadeVehicle car, GameObject prefab)
    {
        var emitter = car.transform.Find(prefab.name);
        if (emitter == null)
            emitter = ((GameObject)PrefabUtility.InstantiatePrefab(prefab, car.transform)).transform;
        float nose = car.GetComponents<BoxCollider>()
            .Where(box => box.enabled && !box.isTrigger)
            .Select(box => box.center.z + box.size.z * .5f).DefaultIfEmpty(2.1f).Max();
        emitter.SetLocalPositionAndRotation(new Vector3(0, .15f, nose + .35f), Quaternion.Euler(-12f, 180f, 0f));
        var controller = emitter.GetComponent<SpeedDebrisController>();
        var binding = new SerializedObject(controller);
        binding.FindProperty("vehicle").objectReferenceValue = car;
        binding.ApplyModifiedPropertiesWithoutUndo();
        PrefabUtility.RecordPrefabInstancePropertyModifications(emitter);
        PrefabUtility.RecordPrefabInstancePropertyModifications(controller);
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

    private static Material ParticleMaterial(string path, string texturePath)
    {
        var importer = (TextureImporter)AssetImporter.GetAtPath(texturePath);
        importer.alphaIsTransparency = true;
        importer.wrapMode = TextureWrapMode.Clamp;
        importer.SaveAndReimport();
        var material = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (material == null)
        {
            material = new Material(Shader.Find("Universal Render Pipeline/Particles/Unlit"));
            AssetDatabase.CreateAsset(material, path);
        }
        material.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>(texturePath));
        material.SetFloat("_Surface", 1f);
        material.SetFloat("_Cull", (float)CullMode.Off);
        material.SetFloat("_ZWrite", 0f);
        material.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
        material.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
        material.SetFloat("_SrcBlendAlpha", (float)BlendMode.One);
        material.SetFloat("_DstBlendAlpha", (float)BlendMode.OneMinusSrcAlpha);
        material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        material.SetOverrideTag("RenderType", "Transparent");
        material.renderQueue = (int)RenderQueue.Transparent + 5;
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

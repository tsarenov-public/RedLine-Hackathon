using System;
using System.Linq;
using Arcade.Vehicle;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

internal static class RaceCameraAuthoring
{
    private const string CameraFolder = "Assets/Cars/Camera/";
    private const string StagePath = "Assets/Scenes/Stage_0.unity";

    [MenuItem("Tools/Arcade Vehicle/Configure Stage_0 race camera")]
    public static void Build()
    {
        ConfigurePostProcessData();
        var stage = SceneManager.GetSceneByPath(StagePath);
        bool openedHere = !stage.isLoaded;
        if (openedHere) stage = EditorSceneManager.OpenScene(StagePath, OpenSceneMode.Additive);
        try
        {
            var roots = stage.GetRootGameObjects();
            var camera = roots.SelectMany(root => root.GetComponentsInChildren<Camera>(true))
                .Single(cam => cam.CompareTag("MainCamera"));
            var cameraData = GetOrAdd<UniversalAdditionalCameraData>(camera.gameObject);
            var effects = GetOrAdd<CameraSpeedEffects>(camera.gameObject);
            if (!camera.TryGetComponent<NFSCameraController>(out _)) camera.gameObject.AddComponent<NFSCameraController>();
            if (camera.TryGetComponent<VehicleChaseCamera>(out var legacy)) legacy.enabled = false;

            var host = camera.transform.Find("Speed Post FX");
            if (host == null)
            {
                host = new GameObject("Speed Post FX").transform;
                host.SetParent(camera.transform, false);
            }
            var volumes = roots.SelectMany(root => root.GetComponentsInChildren<Volume>(true))
                .Where(volume => volume.isGlobal && volume.sharedProfile != null && volume.transform != host);
            var baseVolume = volumes.OrderByDescending(volume => volume.priority).FirstOrDefault();
            if (baseVolume != null) host.gameObject.layer = baseVolume.gameObject.layer;
            var speedVolume = GetOrAdd<Volume>(host.gameObject);
            speedVolume.isGlobal = true;
            speedVolume.priority = baseVolume != null ? baseVolume.priority + 1f : 1f;
            speedVolume.weight = 0f;
            speedVolume.sharedProfile = BuildVolumeProfile();
            cameraData.renderPostProcessing = true;
            cameraData.requiresDepthOption = CameraOverrideOption.On;
            cameraData.volumeLayerMask |= 1 << host.gameObject.layer;

            var binding = new SerializedObject(effects);
            binding.FindProperty("speedVolume").objectReferenceValue = speedVolume;
            binding.ApplyModifiedPropertiesWithoutUndo();

            if (!EditorSceneManager.SaveScene(stage))
                throw new InvalidOperationException("Could not save " + StagePath);
            AssetDatabase.SaveAssets();
            Debug.Log("RACE_CAMERA configured peripheral motion blur on Stage_0.");
        }
        finally
        {
            if (openedHere) EditorSceneManager.CloseScene(stage, true);
        }
    }

    private static void ConfigurePostProcessData()
    {
        const string defaultPath = "Packages/com.unity.render-pipelines.universal/Runtime/Data/PostProcessData.asset";
        const string path = CameraFolder + "Race Post Process Data.asset";
        var data = AssetDatabase.LoadAssetAtPath<PostProcessData>(path);
        if (data == null)
        {
            var defaults = AssetDatabase.LoadAssetAtPath<PostProcessData>(defaultPath);
            data = ScriptableObject.CreateInstance<PostProcessData>();
            EditorUtility.CopySerialized(defaults, data);
            AssetDatabase.CreateAsset(data, path);
        }

        // Replace only the stock blur shader; URP still owns the volume blend and render pass.
        data.shaders.cameraMotionBlurPS = AssetDatabase.LoadAssetAtPath<Shader>(CameraFolder + "PeripheralMotionBlur.shader");
        EditorUtility.SetDirty(data);

        foreach (string rendererName in new[] { "PC_Renderer", "Mobile_Renderer" })
        {
            var renderer = AssetDatabase.LoadAssetAtPath<UniversalRendererData>($"Assets/Settings/{rendererName}.asset");
            renderer.postProcessData = data;
            EditorUtility.SetDirty(renderer);
        }
    }

    private static VolumeProfile BuildVolumeProfile()
    {
        const string path = CameraFolder + "Speed Post FX.asset";
        var profile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(path);
        if (profile == null)
        {
            profile = ScriptableObject.CreateInstance<VolumeProfile>();
            AssetDatabase.CreateAsset(profile, path);
        }

        var blur = GetOrAddOverride<MotionBlur>(profile);
        blur.mode.Override(MotionBlurMode.CameraOnly);
        blur.quality.Override(MotionBlurQuality.High);
        blur.clamp.Override(.022f);
        blur.intensity.Override(0f);
        EditorUtility.SetDirty(blur);

        var chromatic = GetOrAddOverride<ChromaticAberration>(profile);
        chromatic.intensity.Override(.085f);
        EditorUtility.SetDirty(chromatic);
        var vignette = GetOrAddOverride<Vignette>(profile);
        vignette.intensity.Override(.24f);
        vignette.smoothness.Override(.49f);
        EditorUtility.SetDirty(vignette);
        var lens = GetOrAddOverride<LensDistortion>(profile);
        lens.intensity.Override(-.055f);
        EditorUtility.SetDirty(lens);
        EditorUtility.SetDirty(profile);
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

    private static T GetOrAdd<T>(GameObject gameObject) where T : Component =>
        gameObject.TryGetComponent<T>(out var component) ? component : gameObject.AddComponent<T>();
}

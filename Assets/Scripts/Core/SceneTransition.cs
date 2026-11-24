using System;
using System.Collections;
using System.IO;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public sealed class SceneTransition : MonoBehaviour
{
    private static readonly Color LoadingBackground = new(.04f, .045f, .05f);
    private static SceneTransition active;

    public static bool IsLoading => active != null;

    public static int BuildIndexOf(string scene)
    {
        if (string.IsNullOrWhiteSpace(scene)) return -1;
        scene = scene.Trim().Replace('\\', '/');
        if (scene.Contains('/'))
            return SceneUtility.GetBuildIndexByScenePath(scene.EndsWith(".unity", StringComparison.OrdinalIgnoreCase)
                ? scene : scene + ".unity");

        string name = Path.GetFileNameWithoutExtension(scene);
        int found = -1;
        for (int i = 0; i < SceneManager.sceneCountInBuildSettings; i++)
        {
            if (!string.Equals(Path.GetFileNameWithoutExtension(SceneUtility.GetScenePathByBuildIndex(i)), name,
                    StringComparison.OrdinalIgnoreCase)) continue;
            if (found >= 0) return -1;
            found = i;
        }
        return found;
    }

    public static bool TryLoad(int buildIndex, Font font = null, Action onFailure = null)
    {
        if (IsLoading || buildIndex < 0 || buildIndex >= SceneManager.sceneCountInBuildSettings) return false;

        var root = new GameObject("Scene Transition", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler));
        root.GetComponent<Canvas>().sortingOrder = short.MaxValue;
        DontDestroyOnLoad(root);
        active = root.AddComponent<SceneTransition>();
        active.ShowLoading(font);
        active.StartCoroutine(active.Load(buildIndex, onFailure));
        return true;
    }

    private IEnumerator Load(int buildIndex, Action onFailure)
    {
        yield return null;
        var source = SceneManager.GetActiveScene();
        int sourceIndex = source.buildIndex;
        bool leavingMap = source.isLoaded && sourceIndex >= 0 && sourceIndex != buildIndex &&
            source.name == GameProgressionManager.MainMenuScene;
        if (leavingMap)
        {
            // Keep the loading camera alive while Unity releases the map before allocating race meshes.
            var loadingScene = SceneManager.CreateScene("RedLine Loading");
            SceneManager.SetActiveScene(loadingScene);
            yield return SceneManager.UnloadSceneAsync(source);
            yield return Resources.UnloadUnusedAssets();
        }

        var operation = StartLoad(buildIndex);
        if (operation != null) yield return operation;

        bool loaded = operation != null && SceneManager.GetActiveScene().buildIndex == buildIndex;
        if (!loaded && leavingMap && !source.isLoaded)
        {
            var recovery = StartLoad(sourceIndex);
            if (recovery != null) yield return recovery;
        }

        gameObject.SetActive(false);
        active = null;
        Destroy(gameObject);
        if (loaded) yield break;

        Debug.LogError($"Не удалось загрузить сцену «{SceneUtility.GetScenePathByBuildIndex(buildIndex)}».");
        onFailure?.Invoke();
    }

    private static AsyncOperation StartLoad(int buildIndex)
    {
        try
        {
            return SceneManager.LoadSceneAsync(buildIndex, LoadSceneMode.Single);
        }
        catch (Exception exception)
        {
            Debug.LogException(exception);
            return null;
        }
    }

    private void ShowLoading(Font font)
    {
        // Overlay UI alone does not render a display while the scene cameras are being swapped.
        var camera = new GameObject("Loading Camera", typeof(Camera)).GetComponent<Camera>();
        camera.transform.SetParent(transform, false);
        camera.clearFlags = CameraClearFlags.SolidColor;
        camera.backgroundColor = LoadingBackground;
        camera.cullingMask = 0;
        camera.depth = -100f;
        camera.allowHDR = false;
        camera.allowMSAA = false;
        camera.useOcclusionCulling = false;
        var cameraData = camera.GetUniversalAdditionalCameraData();
        cameraData.renderShadows = false;
        cameraData.requiresDepthOption = CameraOverrideOption.Off;
        cameraData.requiresColorOption = CameraOverrideOption.Off;

        var canvas = GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        var scaler = GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 1f;

        var background = new GameObject("Background", typeof(RectTransform), typeof(Image));
        Stretch(background, transform);
        var image = background.GetComponent<Image>();
        image.color = LoadingBackground;
        image.raycastTarget = false;

        var message = new GameObject("Loading Label", typeof(RectTransform), typeof(Text));
        Stretch(message, background.transform);
        var text = message.GetComponent<Text>();
        text.font = font != null ? font : Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        text.text = "ЗАГРУЗКА…";
        text.fontSize = 30;
        text.alignment = TextAnchor.MiddleCenter;
        text.raycastTarget = false;
    }

    private static void Stretch(GameObject element, Transform parent)
    {
        var rect = (RectTransform)element.transform;
        rect.SetParent(parent, false);
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = rect.offsetMax = Vector2.zero;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() => active = null;
}

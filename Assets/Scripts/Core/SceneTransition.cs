using System;
using System.Collections;
using System.IO;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public sealed class SceneTransition : MonoBehaviour
{
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

    public static bool TryLoad(int buildIndex, Font font = null)
    {
        if (IsLoading || buildIndex < 0 || buildIndex >= SceneManager.sceneCountInBuildSettings) return false;

        var source = SceneManager.GetActiveScene();
        var root = new GameObject("Scene Transition", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler));
        root.GetComponent<Canvas>().sortingOrder = short.MaxValue;
        DontDestroyOnLoad(root);
        active = root.AddComponent<SceneTransition>();
        active.ShowLoading(font);
        active.StartCoroutine(active.Load(source, buildIndex));
        return true;
    }

    private IEnumerator Load(Scene source, int buildIndex)
    {
        // Let the UI complete its click before destroying its scene or starting another render pipeline.
        yield return null;
        if (source.IsValid() && source.isLoaded)
        {
            foreach (var root in source.GetRootGameObjects())
                foreach (var camera in root.GetComponentsInChildren<Camera>())
                    camera.gameObject.SetActive(false);

            yield return null;
            var handoff = SceneManager.CreateScene("RedLine Loading");
            SceneManager.SetActiveScene(handoff);
            yield return SceneManager.UnloadSceneAsync(source);
        }

        // Release the old world's render objects and textures before loading the next world.
        yield return Resources.UnloadUnusedAssets();
        yield return null;
        yield return SceneManager.LoadSceneAsync(buildIndex, LoadSceneMode.Single);
        Destroy(gameObject);
    }

    private void ShowLoading(Font font)
    {
        var canvas = GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        var scaler = GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 1f;

        var background = new GameObject("Background", typeof(RectTransform), typeof(Image));
        Stretch(background, transform);
        var image = background.GetComponent<Image>();
        image.color = new Color(.04f, .045f, .05f);
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

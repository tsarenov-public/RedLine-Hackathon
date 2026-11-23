using System;
using System.Linq;
using Arcade.Vehicle;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Object = UnityEngine.Object;

public static class SpeedometerHUDAuthoring
{
    private const string Folder = "Assets/Cars/SpeedometerHUD";
    private const string HudPath = Folder + "/SpeedometerHUD.prefab";
    private const string StagePath = "Assets/Scenes/Stage_0.unity";
    private const float DialSize = 304f;
    private const float DesignSize = 1024f;
    private const int UiLayer = 5;

    [MenuItem("Tools/Arcade Vehicle/Build and bind speedometer HUD")]
    public static void BuildAndBind()
    {
        var prefab = BuildPrefab();
        foreach (string guid in AssetDatabase.FindAssets("t:Prefab", new[] { "Assets/Cars" }))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            var asset = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (asset == null || asset.GetComponentInChildren<ArcadeVehicle>(true) == null) continue;

            var contents = PrefabUtility.LoadPrefabContents(path);
            try
            {
                bool changed = false;
                foreach (var vehicle in contents.GetComponentsInChildren<ArcadeVehicle>(true))
                    changed |= Bind(vehicle, prefab);
                if (changed && PrefabUtility.SaveAsPrefabAsset(contents, path) == null)
                    throw new InvalidOperationException("Could not save " + path);
                Debug.Log("SPEED_HUD prefab " + path);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(contents);
            }
        }

        BindStage(prefab);
    }

    private static GameObject BuildPrefab()
    {
        var root = new GameObject("SpeedometerHUD", typeof(RectTransform), typeof(ArcadeSpeedometerHUD))
            { layer = UiLayer };
        try
        {
            var display = new GameObject("Speedometer Canvas", typeof(RectTransform), typeof(Canvas),
                typeof(CanvasScaler)) { layer = UiLayer };
            display.transform.SetParent(root.transform, false);
            var canvas = display.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 10;
            canvas.enabled = false;
            var scaler = display.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = 1f;

            var dial = AddImage(display.transform, "Dial", "Speedometer_Dial", 512, 512, 1024, 1024);
            var face = dial.rectTransform;
            face.anchorMin = face.anchorMax = face.pivot = new Vector2(1f, 0f);
            face.anchoredPosition = new Vector2(-36f, 32f);

            AddImage(face, "NOS Track", "Nitro_Bar_Bg", 512, 827, 432, 62);
            var fill = AddImage(face, "NOS Charge", "Nitro_Bar_Fill", 512, 827, 432, 62);
            fill.type = Image.Type.Filled;
            fill.fillMethod = Image.FillMethod.Horizontal;
            fill.fillOrigin = (int)Image.OriginHorizontal.Left;
            fill.fillAmount = 0.5f;
            fill.color = new Color(0.27f, 0.94f, 0.88f);

            AddImage(face, "Digital Speed Panel", "Digital_Speed_Panel", 481, 660, 230, 106);
            var speed = AddText(face, "Digital Speed", "000", 453, 659, 130, 96, 19,
                new Color(0.93f, 0.99f, 1f));
            AddText(face, "Digital Speed Units", "MPH", 548, 662, 67, 90, 9,
                new Color(0.72f, 0.88f, 0.95f));
            AddImage(face, "Gear Panel", "Gear_Panel", 660, 660, 88, 106);
            var gear = AddText(face, "Gear", "N", 660, 659, 80, 96, 17, new Color(0.64f, 0.96f, 1f));

            var needle = AddImage(face, "Needle", "Speedometer_Needle", 512, 512, 1024, 1024);
            needle.rectTransform.localRotation = Quaternion.Euler(0f, 0f, 135f);

            var settings = new SerializedObject(root.GetComponent<ArcadeSpeedometerHUD>());
            settings.FindProperty("display").objectReferenceValue = canvas;
            settings.FindProperty("needle").objectReferenceValue = needle.rectTransform;
            settings.FindProperty("nitroFill").objectReferenceValue = fill;
            settings.FindProperty("digitalSpeed").objectReferenceValue = speed;
            settings.FindProperty("gearLabel").objectReferenceValue = gear;
            settings.ApplyModifiedPropertiesWithoutUndo();

            return PrefabUtility.SaveAsPrefabAsset(root, HudPath)
                ?? throw new InvalidOperationException("Could not save " + HudPath);
        }
        finally
        {
            Object.DestroyImmediate(root);
        }
    }

    private static Image AddImage(Transform parent, string name, string sprite, float x, float y,
        float width, float height)
    {
        var element = new GameObject(name, typeof(RectTransform), typeof(Image)) { layer = UiLayer };
        Place(element, parent, x, y, width, height);
        var image = element.GetComponent<Image>();
        image.sprite = ImportSprite(sprite);
        image.raycastTarget = false;
        return image;
    }

    private static Sprite ImportSprite(string name)
    {
        string path = Folder + "/" + name + ".png";
        var importer = AssetImporter.GetAtPath(path) as TextureImporter
            ?? throw new InvalidOperationException("Missing HUD texture " + path);
        if (importer.textureType != TextureImporterType.Sprite ||
            importer.spriteImportMode != SpriteImportMode.Single || !importer.alphaIsTransparency ||
            importer.mipmapEnabled || importer.textureCompression != TextureImporterCompression.Uncompressed)
        {
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = false;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.filterMode = FilterMode.Bilinear;
            var sprite = new TextureImporterSettings();
            importer.ReadTextureSettings(sprite);
            sprite.spriteAlignment = (int)SpriteAlignment.Center;
            sprite.spritePivot = new Vector2(0.5f, 0.5f);
            sprite.spriteMeshType = SpriteMeshType.FullRect;
            importer.SetTextureSettings(sprite);
            importer.SaveAndReimport();
        }

        return AssetDatabase.LoadAssetAtPath<Sprite>(path)
            ?? throw new InvalidOperationException("Missing imported sprite " + path);
    }

    private static Text AddText(Transform parent, string name, string content, float x, float y,
        float width, float height, int size, Color color)
    {
        var element = new GameObject(name, typeof(RectTransform), typeof(Text)) { layer = UiLayer };
        Place(element, parent, x, y, width, height);
        var label = element.GetComponent<Text>();
        label.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        label.fontStyle = FontStyle.Bold;
        label.fontSize = size;
        label.alignment = TextAnchor.MiddleCenter;
        label.horizontalOverflow = HorizontalWrapMode.Overflow;
        label.verticalOverflow = VerticalWrapMode.Overflow;
        label.raycastTarget = false;
        label.text = content;
        label.color = color;
        return label;
    }

    private static void Place(GameObject element, Transform parent, float x, float y, float width, float height)
    {
        var rect = (RectTransform)element.transform;
        rect.SetParent(parent, false);
        rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 0.5f);
        const float scale = DialSize / DesignSize;
        rect.sizeDelta = new Vector2(width, height) * scale;
        rect.anchoredPosition = new Vector2(x - DesignSize / 2, DesignSize / 2 - y) * scale;
    }

    private static bool Bind(ArcadeVehicle vehicle, GameObject prefab)
    {
        var existing = vehicle.GetComponentsInChildren<ArcadeSpeedometerHUD>(true)
            .Where(hud => hud.GetComponentInParent<ArcadeVehicle>(true) == vehicle).ToArray();
        if (existing.Length > 1)
            throw new InvalidOperationException("Multiple speedometer HUDs on " + vehicle.name);

        var hud = existing.Length == 1 ? existing[0] : ((GameObject)PrefabUtility.InstantiatePrefab(
            prefab, vehicle.transform)).GetComponent<ArcadeSpeedometerHUD>();
        var serialized = new SerializedObject(hud);
        var binding = serialized.FindProperty("vehicle");
        if (existing.Length == 1 && binding.objectReferenceValue == vehicle) return false;

        binding.objectReferenceValue = vehicle;
        serialized.ApplyModifiedPropertiesWithoutUndo();
        PrefabUtility.RecordPrefabInstancePropertyModifications(hud);
        return true;
    }

    private static void BindStage(GameObject prefab)
    {
        var stage = SceneManager.GetSceneByPath(StagePath);
        bool openedHere = !stage.isLoaded;
        if (openedHere) stage = EditorSceneManager.OpenScene(StagePath, OpenSceneMode.Additive);
        try
        {
            bool changed = false;
            foreach (var vehicle in stage.GetRootGameObjects()
                         .SelectMany(root => root.GetComponentsInChildren<ArcadeVehicle>(true)))
            {
                changed |= Bind(vehicle, prefab);
                Debug.Log("SPEED_HUD Stage_0 " + vehicle.name);
            }

            if (changed && !EditorSceneManager.SaveScene(stage))
                throw new InvalidOperationException("Could not save " + StagePath);
        }
        finally
        {
            if (openedHere) EditorSceneManager.CloseScene(stage, true);
        }
    }
}

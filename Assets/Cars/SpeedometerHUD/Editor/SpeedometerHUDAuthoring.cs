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
        RaceHudSprites.Generate();
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

            var track = AddImage(face, "NOS Track", "Nitro_Bar_Bg", 512, 512, 1024, 1024);
            var fill = AddImage(face, "NOS Charge", "Nitro_Bar_Fill", 512, 512, 1024, 1024);
            track.rectTransform.localRotation = fill.rectTransform.localRotation = Quaternion.Euler(0f, 0f, -22f);
            fill.type = Image.Type.Filled;
            fill.fillMethod = Image.FillMethod.Radial360;
            fill.fillOrigin = (int)Image.Origin360.Bottom;
            fill.fillClockwise = true;
            fill.fillAmount = ArcadeSpeedometerHUD.NosSweepDegrees / 720f;
            fill.color = new Color(.69f, 1f, .16f);
            AddImage(face, "NOS Bottle", "Nitro_Bottle", 374, 927, 76, 76);

            var ink = new Color(.17f, .11f, .051f);
            AddImage(face, "Gear Panel", "Gear_Panel", 515, 634, 171, 78);
            var gear = AddLcd(face, "Gear", "N", 545, 634, 79, 60, ink);
            var shift = AddImage(face, "Upshift", "Upshift", 472, 634, 33, 31);
            shift.color = new Color(ink.r, ink.g, ink.b, .20f);
            AddImage(face, "Digital Speed Panel", "Digital_Speed_Panel", 512, 729, 252, 79);
            var speed = AddLcd(face, "Digital Speed", "000", 512, 729, 202, 60, ink);
            AddText(face, "Digital Speed Units", "MPH", 512, 793, 130, 36, 11, Color.white);
            AddText(face, "RPM Units", "×1000 RPM", 512, 404, 238, 42, 10, new Color(.80f, .79f, .72f));

            var needle = AddImage(face, "Needle", "Speedometer_Needle", 512, 512, 1024, 1024);
            needle.rectTransform.localRotation = Quaternion.Euler(0f, 0f, 135f);

            var minimap = MinimapHUDAuthoring.AddTo(display.transform);
            var settings = new SerializedObject(root.GetComponent<ArcadeSpeedometerHUD>());
            settings.FindProperty("display").objectReferenceValue = canvas;
            settings.FindProperty("needle").objectReferenceValue = needle.rectTransform;
            settings.FindProperty("nitroFill").objectReferenceValue = fill;
            settings.FindProperty("digitalSpeed").objectReferenceValue = speed;
            settings.FindProperty("gearLabel").objectReferenceValue = gear;
            settings.FindProperty("upshift").objectReferenceValue = shift;
            settings.FindProperty("minimap").objectReferenceValue = minimap;
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
        image.sprite = RaceHudSprites.SpriteAt(Folder + "/" + sprite + ".png");
        image.raycastTarget = false;
        return image;
    }

    private static LcdDisplay AddLcd(Transform parent, string name, string value, float x, float y,
        float width, float height, Color color)
    {
        var element = new GameObject(name, typeof(RectTransform), typeof(LcdDisplay)) { layer = UiLayer };
        Place(element, parent, x, y, width, height);
        var label = element.GetComponent<LcdDisplay>();
        label.Value = value;
        label.color = color;
        label.raycastTarget = false;
        return label;
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
            foreach (var vehicle in stage.GetRootGameObjects()
                         .SelectMany(root => root.GetComponentsInChildren<ArcadeVehicle>(true)))
            {
                Bind(vehicle, prefab);
                Debug.Log("SPEED_HUD Stage_0 " + vehicle.name);
            }

            RaceCourseAuthoring.Bake(stage);
            if (!EditorSceneManager.SaveScene(stage))
                throw new InvalidOperationException("Could not save " + StagePath);
        }
        finally
        {
            if (openedHere) EditorSceneManager.CloseScene(stage, true);
        }
    }
}

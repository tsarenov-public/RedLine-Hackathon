using System;
using System.Collections.Generic;
using System.Linq;
using Arcade.Vehicle;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Object = UnityEngine.Object;

public static class RedLineGameSetup
{
    private const string MenuScenePath = "Assets/Scenes/MainMenu.unity";
    private const string ProgressionPath = "Assets/Resources/GameProgression.asset";
    private const string MenuFolder = "Assets/UI/Menu";
    private const string SpriteFolder = MenuFolder + "/Sprites/";
    private const string MapTexturePath = MenuFolder + "/MenuMap.png";
    private const string SettingsPrefabPath = MenuFolder + "/TopSettingsMenu.prefab";
    private const string RaceUIFolder = "Assets/UI/Race";
    private const string CountdownPrefabPath = RaceUIFolder + "/RaceStartCountdown.prefab";
    private const string FinishPrefabPath = "Assets/UI/Finish/RaceFinishCanvas.prefab";
    private const string FontPath = "Assets/UI/Finish/DejaVuSansMono.ttf";

    private static readonly Color Accent = new(.84f, .95f, .23f);
    private static readonly Color AccentHover = new(.93f, 1f, .5f);
    private static readonly Color AccentPressed = new(.66f, .76f, .16f);
    private static readonly Color Ink = new(.05f, .06f, .07f);
    private static readonly Color PanelColor = new(.075f, .08f, .09f, .96f);
    private static readonly Color DarkButton = new(.15f, .16f, .18f);
    private static readonly Color Muted = new(.63f, .66f, .69f);
    private static readonly Color Danger = new(.9f, .27f, .2f);
    private static readonly Color PaperInk = new(.17f, .11f, .075f);
    private static readonly Color PaperAccent = new(.55f, .3f, .14f);

    private sealed class RaceSetup
    {
        public string Scene;
        public string Player;
        public string Rival;
        public string Key;
        public RaceMode Mode;
    }

    private static readonly RaceSetup[] Races =
    {
        new()
        {
            Scene = "Assets/Scenes/1.unity",
            Player = "Assets/Cars/Police/police_Car 1 1.prefab",
            Rival = "Assets/Cars/race cars/honda nsx/FINAL_MODEL_R92_Car 1.prefab",
            Key = "Assets/Cars/race cars/honda nsx/FinishKey.png",
            Mode = RaceMode.Chase
        },
        new()
        {
            Scene = "Assets/Scenes/2.unity",
            Player = "Assets/Cars/race cars/Plymouth Barracuda/FINAL_CUDA_440 1 1 1.prefab",
            Rival = "Assets/Cars/race cars/gtr/FINAL_MODEL_RF_Car 1 1.prefab",
            Key = "Assets/Cars/race cars/gtr/FinishKey.png",
            Mode = RaceMode.Race
        },
        new()
        {
            Scene = "Assets/Scenes/3.unity",
            Player = "Assets/Cars/race cars/gtr/FINAL_MODEL_RF_Car 1 1.prefab",
            Rival = "Assets/Cars/race cars/mclaren/Final_Model_Car 1.prefab",
            Key = "Assets/Cars/race cars/mclaren/FinishKey.png",
            Mode = RaceMode.Race
        },
        new()
        {
            Scene = "Assets/Scenes/4.unity",
            Player = "Assets/Cars/race cars/mclaren/Final_Model_Car 1.prefab",
            Rival = "Assets/Cars/race cars/lamborgini/FINAL_MODEL_SV97_Car 1.prefab",
            Key = "Assets/Cars/race cars/lamborgini/FinishKey.png",
            Mode = RaceMode.Race
        }
    };

    private static Font font;

    [MenuItem("Tools/RedLine/Собрать игровой цикл (меню, гонки, настройки)")]
    private static void BuildFromMenu()
    {
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        Run();
        EditorSceneManager.OpenScene(MenuScenePath);
    }

    [MenuItem("Tools/RedLine/Сбросить прогресс игрока")]
    private static void ResetProgressFromMenu()
    {
        GameProgressionManager.ResetProgress();
        Debug.Log("[RedLine] Прогресс сброшен: открыта только гонка 1.");
    }

    public static void RunBatch() => Run();

    private static void Run()
    {
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
        EnsureFolder(RaceUIFolder);
        EnsureFolder("Assets/Resources");
        ConfigureArt();
        font = Load<Font>(FontPath);

        EnsureProgression();
        BuildSettingsPrefab();
        BuildCountdownPrefab();
        UpgradeFinishPrefab();

        var mapPositions = new Dictionary<int, Vector2>();
        for (int i = 0; i < Races.Length; i++)
            if (SetupRace(Races[i]) is { } position) mapPositions[i + 1] = position;

        var data = Load<GameProgressionManager>(ProgressionPath);
        foreach (var (stage, position) in mapPositions) ApplyMapPosition(data.Stages[stage - 1], position);
        EditorUtility.SetDirty(data);
        AssetDatabase.SaveAssets();

        BuildMainMenu();

        EditorBuildSettings.scenes = new[] { MenuScenePath }
            .Concat(Races.Select(race => race.Scene))
            .Select(path => new EditorBuildSettingsScene(path, true))
            .ToArray();
        AssetDatabase.SaveAssets();
        Debug.Log("[RedLine] Игровой цикл собран: MainMenu -> 1 -> 2 -> 3 -> 4.");
    }

    private static void ConfigureArt()
    {
        foreach (var name in new[] { "Circle", "Ring", "Glow", "Lock", "Check", "Close", "Gear", "Vignette" })
            ImportSprite(SpriteFolder + name + ".png", Vector4.zero);
        ImportSprite(SpriteFolder + "Panel.png", Vector4.one * 24f);
        ImportSprite(SpriteFolder + "PanelOutline.png", Vector4.one * 24f);
        ImportTexture(SpriteFolder + "Scanlines.png", TextureWrapMode.Repeat, FilterMode.Point, false);
        ImportTexture(MapTexturePath, TextureWrapMode.Clamp, FilterMode.Trilinear, true);
    }

    private static void ImportSprite(string path, Vector4 border)
    {
        var importer = Importer(path);
        var settings = new TextureImporterSettings();
        importer.ReadTextureSettings(settings);
        settings.textureType = TextureImporterType.Sprite;
        settings.spriteMode = (int)SpriteImportMode.Single;
        settings.spriteMeshType = SpriteMeshType.FullRect;
        settings.spriteBorder = border;
        settings.alphaIsTransparency = true;
        settings.mipmapEnabled = false;
        settings.wrapMode = TextureWrapMode.Clamp;
        importer.SetTextureSettings(settings);
        importer.textureCompression = TextureImporterCompression.Uncompressed;
        importer.SaveAndReimport();
    }

    private static void ImportTexture(string path, TextureWrapMode wrap, FilterMode filter, bool mipmaps)
    {
        var importer = Importer(path);
        importer.textureType = TextureImporterType.Default;
        importer.wrapMode = wrap;
        importer.filterMode = filter;
        importer.mipmapEnabled = mipmaps;
        importer.npotScale = TextureImporterNPOTScale.None;
        importer.maxTextureSize = 2048;
        importer.alphaIsTransparency = true;
        importer.textureCompression = mipmaps ? TextureImporterCompression.CompressedHQ : TextureImporterCompression.Uncompressed;
        importer.SaveAndReimport();
    }

    private static TextureImporter Importer(string path) =>
        AssetImporter.GetAtPath(path) as TextureImporter ??
        throw new InvalidOperationException($"Нет текстуры {path}. Запусти Tools/MainMenu/generate_menu_art.py");

    private static GameProgressionManager EnsureProgression()
    {
        var data = AssetDatabase.LoadAssetAtPath<GameProgressionManager>(ProgressionPath);
        if (data == null)
        {
            data = ScriptableObject.CreateInstance<GameProgressionManager>();
            AssetDatabase.CreateAsset(data, ProgressionPath);
        }

        var defaults = GameProgressionManager.CreateDefaultStages();
        var stages = data.Stages ?? Array.Empty<GameProgressionManager.StageConfig>();
        if (stages.Length != defaults.Length) Array.Resize(ref stages, defaults.Length);
        for (int i = 0; i < defaults.Length; i++)
        {
            var stage = stages[i] ??= defaults[i];
            var fallback = defaults[i];
            if (string.IsNullOrEmpty(stage.sceneName)) stage.sceneName = fallback.sceneName;
            if (string.IsNullOrEmpty(stage.missionName)) stage.missionName = fallback.missionName;
            if (string.IsNullOrEmpty(stage.chapterTitle)) stage.chapterTitle = fallback.chapterTitle;
            if (string.IsNullOrEmpty(stage.rivalName)) stage.rivalName = fallback.rivalName;
            if (string.IsNullOrEmpty(stage.carName)) stage.carName = fallback.carName;
            if (string.IsNullOrEmpty(stage.briefing)) stage.briefing = fallback.briefing;
            if (string.IsNullOrEmpty(stage.victoryStory)) stage.victoryStory = fallback.victoryStory;
            if (stage.mapPosition == Vector2.zero) stage.mapPosition = fallback.mapPosition;
            if (stage.keySprite == null) stage.keySprite = Load<Sprite>(Races[i].Key);
        }

        data.Stages = stages;
        EditorUtility.SetDirty(data);
        AssetDatabase.SaveAssets();
        return data;
    }

    private static GameObject BuildSettingsPrefab()
    {
        var existing = AssetDatabase.LoadAssetAtPath<GameObject>(SettingsPrefabPath);
        if (existing != null) return existing;
        var canvas = NewCanvas("TopSettingsMenu", 100);
        var root = (RectTransform)canvas.transform;
        var menu = canvas.gameObject.AddComponent<TopSettingsMenu>();

        var open = Node("Settings Button", root, Vector2.one, Vector2.one, new Vector2(-28f, -28f), new Vector2(76f, 76f));
        Picture(open, Art("Panel"), new Color(.06f, .065f, .075f, .88f), true, 12f);
        var gear = Node("Gear", open, Half, Half, Vector2.zero, new Vector2(44f, 44f));
        var gearImage = Picture(gear, Art("Gear"), Color.white);
        var openButton = Tinted(open, gearImage, Color.white, Accent, AccentPressed);

        var window = Stretch("Window", root);
        Picture(Stretch("Dim", window), null, new Color(0f, 0f, 0f, .62f), true);
        var panel = Node("Panel", window, Half, Half, Vector2.zero, new Vector2(620f, 0f));
        Picture(panel, Art("Panel"), PanelColor, true, 12f);
        var layout = panel.gameObject.AddComponent<VerticalLayoutGroup>();
        layout.padding = new RectOffset(48, 48, 46, 46);
        layout.spacing = 16f;
        layout.childAlignment = TextAnchor.UpperCenter;
        layout.childControlWidth = layout.childControlHeight = true;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = false;
        panel.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        var bar = Node("Accent Bar", panel, new Vector2(.5f, 1f), new Vector2(.5f, 1f), Vector2.zero, new Vector2(620f, 6f));
        bar.gameObject.AddComponent<LayoutElement>().ignoreLayout = true;
        bar.anchorMin = new Vector2(0f, 1f);
        bar.anchorMax = Vector2.one;
        bar.sizeDelta = new Vector2(0f, 6f);
        Picture(bar, null, Accent);

        var title = Label(Row("Title", panel, 56f), "НАСТРОЙКИ", 42, Color.white, TextAnchor.MiddleLeft, FontStyle.Bold);

        var close = Node("Close", panel, Vector2.one, Vector2.one, new Vector2(-22f, -24f), new Vector2(56f, 56f));
        close.gameObject.AddComponent<LayoutElement>().ignoreLayout = true;
        Picture(close, Art("Panel"), DarkButton, true, 10f);
        var closeIcon = Picture(Node("Icon", close, Half, Half, Vector2.zero, new Vector2(26f, 26f)), Art("Close"), Color.white);
        var closeButton = Tinted(close, closeIcon, Color.white, Accent, AccentPressed);

        Row("Spacer", panel, 6f);
        var volumeRow = Row("Volume Row", panel, 34f);
        Label(Stretch("Label", volumeRow), "ГРОМКОСТЬ ЗВУКА", 24, Muted, TextAnchor.MiddleLeft, FontStyle.Bold);
        var volumeValue = Label(Stretch("Value", volumeRow), "100%", 24, Color.white, TextAnchor.MiddleRight, FontStyle.Bold);
        var slider = BuildSlider(Row("Volume Slider", panel, 40f));
        Row("Spacer", panel, 14f);

        var resume = FilledButton(Row("Resume", panel, 76f), "ПРОДОЛЖИТЬ", Accent, Ink, 30, out var resumeLabel);
        var reset = OutlineButton(Row("Reset Progress", panel, 64f), "СБРОСИТЬ ПРОГРЕСС", Danger, 24, out var resetLabel);
        var exit = FilledButton(Row("Exit", panel, 76f), "ВЫЙТИ В МЕНЮ", DarkButton, Color.white, 28, out var exitLabel);

        Bind(menu,
            ("openButton", openButton), ("window", window.gameObject), ("titleText", title),
            ("volumeSlider", slider), ("volumeValueText", volumeValue), ("closeButton", closeButton),
            ("resumeButton", resume), ("resumeLabel", resumeLabel), ("resetProgressButton", reset),
            ("resetProgressLabel", resetLabel), ("exitButton", exit), ("exitLabel", exitLabel));
        window.gameObject.SetActive(false);
        return SavePrefab(canvas.gameObject, SettingsPrefabPath);
    }

    private static Slider BuildSlider(RectTransform rect)
    {
        var track = Node("Background", rect, new Vector2(0f, .5f), Half, Vector2.zero, new Vector2(0f, 12f));
        track.anchorMax = new Vector2(1f, .5f);
        Picture(track, Art("Panel"), new Color(.19f, .2f, .22f), false, 3f);

        var fillArea = Node("Fill Area", rect, new Vector2(0f, .5f), Half, Vector2.zero, new Vector2(0f, 12f));
        fillArea.anchorMax = new Vector2(1f, .5f);
        var fill = Node("Fill", fillArea, Vector2.zero, Half, Vector2.zero, Vector2.zero);
        fill.anchorMax = new Vector2(0f, 1f);
        Picture(fill, Art("Panel"), Accent, false, 3f);

        var handleArea = Stretch("Handle Slide Area", rect, 16f, 0f, 16f, 0f);
        var handle = Node("Handle", handleArea, Vector2.zero, Half, Vector2.zero, new Vector2(34f, 0f));
        handle.anchorMax = new Vector2(0f, 1f);
        var knob = Picture(handle, Art("Circle"), Color.white, true);
        knob.preserveAspect = true;

        var slider = rect.gameObject.AddComponent<Slider>();
        slider.fillRect = fill;
        slider.handleRect = handle;
        slider.targetGraphic = knob;
        slider.direction = Slider.Direction.LeftToRight;
        slider.minValue = 0f;
        slider.maxValue = 1f;
        slider.value = 1f;
        var colors = ColorBlock.defaultColorBlock;
        colors.highlightedColor = AccentHover;
        colors.pressedColor = Accent;
        colors.selectedColor = Color.white;
        slider.colors = colors;
        return slider;
    }

    private static GameObject BuildCountdownPrefab()
    {
        var existing = AssetDatabase.LoadAssetAtPath<GameObject>(CountdownPrefabPath);
        if (existing != null) return existing;
        var canvas = NewCanvas("RaceStartCountdown", 30);
        var root = (RectTransform)canvas.transform;
        var ui = canvas.gameObject.AddComponent<RaceCountdownUI>();

        var count = Label(Node("Count", root, Half, Half, new Vector2(0f, 60f), new Vector2(1200f, 280f)),
            "3", 220, Color.white, TextAnchor.MiddleCenter, FontStyle.Bold);
        count.horizontalOverflow = HorizontalWrapMode.Overflow;
        var outline = count.gameObject.AddComponent<Outline>();
        outline.effectColor = new Color(0f, 0f, 0f, .75f);
        outline.effectDistance = new Vector2(4f, -4f);

        var plate = Node("Caption Plate", root, Half, Half, new Vector2(0f, -150f), new Vector2(980f, 72f));
        Picture(plate, Art("Panel"), new Color(0f, 0f, 0f, .62f), false, 10f);
        Picture(Node("Accent", plate, new Vector2(0f, .5f), new Vector2(0f, .5f), Vector2.zero, new Vector2(8f, 72f)), null, Accent);
        var caption = Label(Stretch("Caption", plate, 24f, 0f, 24f, 0f), "ПРИГОТОВЬСЯ", 34, Accent,
            TextAnchor.MiddleCenter, FontStyle.Bold, 20);

        Bind(ui, ("countText", count), ("captionPlate", plate.gameObject), ("captionText", caption));
        canvas.enabled = false;
        return SavePrefab(canvas.gameObject, CountdownPrefabPath);
    }

    private static void UpgradeFinishPrefab()
    {
        var root = PrefabUtility.LoadPrefabContents(FinishPrefabPath);
        try
        {
            if (root.transform.Find("Defeat Panel") != null) return;
            var ui = root.GetComponent<RaceFinishUI>();
            var paper = root.transform.Find("Story Paper").gameObject;
            var card = root.transform.Find("Key Reward Card").gameObject;
            var continueLabel = paper.transform.Find("Continue/Continue Label").GetComponent<Text>();
            continueLabel.text = "Далее";

            var defeat = Stretch("Defeat Panel", root.transform);
            Picture(Stretch("Dim", defeat), null, new Color(0f, 0f, 0f, .55f), true);
            var panel = Node("Card", defeat, Half, Half, Vector2.zero, new Vector2(900f, 480f));
            Picture(panel, Art("Panel"), PanelColor, true, 12f);
            var bar = Node("Accent Bar", panel, new Vector2(0f, 1f), new Vector2(.5f, 1f), Vector2.zero, new Vector2(0f, 8f));
            bar.anchorMax = Vector2.one;
            Picture(bar, null, Danger);
            var title = Label(Node("Title", panel, new Vector2(.5f, 1f), new Vector2(.5f, 1f), new Vector2(0f, -56f),
                new Vector2(820f, 80f)), "ВЫ ПРИШЛИ ВТОРЫМ!", 56, Color.white, TextAnchor.MiddleCenter, FontStyle.Bold, 30);
            var message = Label(Node("Message", panel, new Vector2(.5f, 1f), new Vector2(.5f, 1f), new Vector2(0f, -156f),
                new Vector2(800f, 130f)), "", 26, Muted, TextAnchor.UpperCenter, FontStyle.Normal, 16);
            message.lineSpacing = 1.2f;

            var retry = FilledButton(Node("Retry", panel, new Vector2(.5f, 0f), new Vector2(.5f, 0f), new Vector2(-200f, 52f),
                new Vector2(370f, 86f)), "ЗАНОВО", Accent, Ink, 32, out _);
            var menu = FilledButton(Node("Menu", panel, new Vector2(.5f, 0f), new Vector2(.5f, 0f), new Vector2(200f, 52f),
                new Vector2(370f, 86f)), "ВЫЙТИ В МЕНЮ", DarkButton, Color.white, 28, out _);
            defeat.gameObject.SetActive(false);

            var binding = new SerializedObject(ui);
            var elements = binding.FindProperty("victoryElements");
            elements.arraySize = 2;
            elements.GetArrayElementAtIndex(0).objectReferenceValue = paper;
            elements.GetArrayElementAtIndex(1).objectReferenceValue = card;
            binding.ApplyModifiedPropertiesWithoutUndo();
            Bind(ui, ("defeatPanel", defeat.gameObject), ("defeatTitleText", title), ("defeatMessageText", message),
                ("retryButton", retry), ("menuButton", menu));

            PrefabUtility.SaveAsPrefabAsset(root, FinishPrefabPath);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    private static Vector2? SetupRace(RaceSetup setup)
    {
        var scene = EditorSceneManager.OpenScene(setup.Scene, OpenSceneMode.Single);
        Physics.SyncTransforms();
        var roots = scene.GetRootGameObjects();
        var path = All<WaypointPath>(roots).FirstOrDefault() ??
                   throw new InvalidOperationException($"{setup.Scene}: нет WaypointPath");
        var waypoints = path.GetWaypoints();
        var vehicles = All<ArcadeVehicle>(roots).ToList();
        var playerPrefab = Load<GameObject>(setup.Player);
        var rivalPrefab = Load<GameObject>(setup.Rival);

        var existing = All<RaceDirector>(roots).FirstOrDefault();
        var player = existing != null ? existing.Player : null;
        var rival = existing != null ? existing.Rival : null;
        if (player == null) player = vehicles.FirstOrDefault(vehicle => vehicle != rival && IsCar(vehicle, playerPrefab, setup.Player));
        if (rival == null) rival = vehicles.FirstOrDefault(vehicle => vehicle != player && IsCar(vehicle, rivalPrefab, setup.Rival));
        var template = vehicles.Select(vehicle => vehicle.GetComponent<AIRacerController>()).FirstOrDefault(ai => ai != null);
        if (player == null && rival == null)
            throw new InvalidOperationException($"{setup.Scene}: не найдена ни машина игрока, ни машина соперника");

        if (player == null)
        {
            var placeholder = setup.Mode == RaceMode.Race
                ? vehicles.FirstOrDefault(vehicle => vehicle != rival && !HasEnabledAI(vehicle) &&
                                                     Vector3.Distance(vehicle.transform.position, rival.transform.position) < 12f)
                : null;
            var pose = placeholder != null
                ? new Pose(placeholder.transform.position, placeholder.transform.rotation)
                : setup.Mode == RaceMode.Chase ? ChaseSlot(rival, waypoints) : SideSlot(rival);
            if (placeholder != null)
            {
                placeholder.gameObject.SetActive(false);
                vehicles.Remove(placeholder);
                Debug.LogWarning($"[RedLine] {setup.Scene}: заготовка {placeholder.name} у старта выключена, " +
                                 "на её место поставлена машина игрока", placeholder);
            }
            player = Spawn(playerPrefab, scene, pose, "игрок");
        }
        if (rival == null) rival = Spawn(rivalPrefab, scene, SideSlot(player), "соперник");

        foreach (var other in vehicles.Where(vehicle => vehicle != player && vehicle != rival))
            Debug.LogWarning($"[RedLine] {setup.Scene}: лишняя машина {other.name} не участвует в гонке " +
                             "(во время заезда RaceDirector держит её на месте)", other);

        if (player.TryGetComponent(out AIRacerController playerAI)) playerAI.enabled = false;
        if (player.TryGetComponent(out VehicleInputSource playerInput)) playerInput.enabled = true;

        var rivalAI = rival.GetComponent<AIRacerController>();
        if (rivalAI == null)
        {
            rivalAI = rival.gameObject.AddComponent<AIRacerController>();
            if (template != null) EditorJsonUtility.FromJsonOverwrite(EditorJsonUtility.ToJson(template), rivalAI);
        }
        rivalAI.enabled = true;
        rivalAI.path = path;
        if (rival.TryGetComponent(out VehicleInputSource rivalInput)) rivalInput.enabled = true;

        var cameras = All<Camera>(roots).ToList();
        var mainCamera = cameras.FirstOrDefault(camera => camera.GetComponent<VehicleChaseCamera>() != null) ??
                         cameras.FirstOrDefault(camera => camera.CompareTag("MainCamera"));
        if (mainCamera != null && mainCamera.TryGetComponent(out VehicleChaseCamera chase)) chase.vehicle = player;
        if (mainCamera != null && mainCamera.TryGetComponent(out NFSCameraController nfs)) Bind(nfs, ("manualTarget", player));
        var showcase = All<FinishShowcaseCamera>(roots).FirstOrDefault();

        var finish = All<RaceFinishUI>(roots).FirstOrDefault() ??
                     ((GameObject)PrefabUtility.InstantiatePrefab(Load<GameObject>(FinishPrefabPath), scene))
                     .GetComponent<RaceFinishUI>();
        Bind(finish, ("gameplayCamera", mainCamera), ("showcaseCamera", showcase));

        var countdown = Ensure<RaceCountdownUI>(scene, CountdownPrefabPath);
        Ensure<TopSettingsMenu>(scene, SettingsPrefabPath);

        var director = All<RaceDirector>(roots).FirstOrDefault() ?? new GameObject("Race Director").AddComponent<RaceDirector>();
        var binding = new SerializedObject(director);
        binding.FindProperty("mode").enumValueIndex = (int)setup.Mode;
        binding.ApplyModifiedPropertiesWithoutUndo();
        Bind(director, ("player", player), ("rival", rival), ("path", path), ("finishScreen", finish), ("countdown", countdown));

        var triggers = All<FinishLineTrigger>(roots).ToList();
        if (triggers.Count == 0) Debug.LogError($"[RedLine] {setup.Scene}: нет FinishLineTrigger");
        foreach (var trigger in triggers) Bind(trigger, ("race", director));

        var course = All<RaceMinimapCourse>(roots).FirstOrDefault();

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Debug.Log($"[RedLine] Сцена {setup.Scene}: игрок {player.name} {player.transform.position}, " +
                  $"соперник {rival.name} {rival.transform.position}, режим {setup.Mode}, финиш-триггеров {triggers.Count}");

        if (course == null || waypoints.Count == 0 || course.mapSize.x <= 0f || course.mapSize.y <= 0f) return null;
        var start = waypoints[0].position;
        return new Vector2(Mathf.Clamp01((start.x - course.mapMin.x) / course.mapSize.x),
            Mathf.Clamp01((start.z - course.mapMin.y) / course.mapSize.y));
    }

    private static void ApplyMapPosition(GameProgressionManager.StageConfig stage, Vector2 position)
    {
        var fallback = GameProgressionManager.CreateDefaultStages().FirstOrDefault(item => item.sceneName == stage.sceneName);
        if (stage.mapPosition == Vector2.zero || (fallback != null && Vector2.Distance(stage.mapPosition, fallback.mapPosition) < .02f))
            stage.mapPosition = position;
    }

    private static bool HasEnabledAI(ArcadeVehicle vehicle) =>
        vehicle.TryGetComponent(out AIRacerController ai) && ai.enabled;

    private static bool IsCar(ArcadeVehicle vehicle, GameObject prefab, string prefabPath) =>
        PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(vehicle.gameObject) == prefabPath ||
        vehicle.gameObject.name == prefab.name;

    private static ArcadeVehicle Spawn(GameObject prefab, Scene scene, Pose pose, string role)
    {
        var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene);
        instance.transform.SetPositionAndRotation(pose.position, pose.rotation);
        SnapToGround(instance);
        Debug.Log($"[RedLine] {scene.name}: добавлена машина ({role}) {prefab.name} в {instance.transform.position}");
        return instance.GetComponent<ArcadeVehicle>();
    }

    private static void SnapToGround(GameObject car)
    {
        Physics.SyncTransforms();
        var bounds = car.GetComponentsInChildren<Renderer>(true)
            .Where(renderer => renderer is MeshRenderer or SkinnedMeshRenderer && renderer.enabled)
            .Select(renderer => renderer.bounds)
            .Concat(car.GetComponentsInChildren<Collider>(true)
                .Where(collider => collider.enabled && !collider.isTrigger)
                .Select(collider => collider.bounds))
            .ToList();
        if (bounds.Count == 0 || !Ground(car.transform.position + Vector3.up * 1.5f, out var hit, car.transform)) return;
        float bottom = bounds.Min(item => item.min.y);
        car.transform.position += Vector3.up * (hit.point.y + .04f - bottom);
        Debug.Log($"[RedLine] {car.name}: низ модели на {bottom - car.transform.position.y:F2} м от пивота, " +
                  $"поверхность {hit.collider.name} (слой {LayerMask.LayerToName(hit.collider.gameObject.layer)})");
    }

    private static Pose SideSlot(ArcadeVehicle anchor)
    {
        var origin = anchor.transform;
        var forward = Flat(origin.forward).normalized;
        var right = Vector3.Cross(Vector3.up, forward);
        var rotation = Quaternion.LookRotation(forward, Vector3.up);
        if (!Ground(origin.position, out var ground))
            return new Pose(origin.position + right * 3.4f, rotation);

        float clearance = Mathf.Clamp(origin.position.y - ground.point.y, 0f, 1.5f);
        int layer = ground.collider.gameObject.layer;
        foreach (float along in new[] { 0f, -2f })
            foreach (float side in new[] { 3.4f, -3.4f, 3f, -3f, 3.8f, -3.8f, 2.6f, -2.6f })
                if (TrySlot(origin.position + forward * along + right * side, rotation, clearance, layer, ground.point.y, out var pose))
                    return pose;
        if (TrySlot(origin.position - forward * 9f, rotation, clearance, layer, ground.point.y, out var behind)) return behind;

        Debug.LogWarning($"[RedLine] Не нашёл свободное место рядом с {anchor.name}, ставлю справа без проверки", anchor);
        return new Pose(origin.position + right * 3.4f, rotation);
    }

    private static Pose ChaseSlot(ArcadeVehicle rival, List<Transform> waypoints)
    {
        var origin = rival.transform;
        float clearance = Ground(origin.position, out var startGround) ? Mathf.Clamp(origin.position.y - startGround.point.y, 0f, 1.5f) : .5f;
        int layer = startGround.collider != null ? startGround.collider.gameObject.layer : 0;
        foreach (float distance in new[] { 90f, 110f, 70f, 130f, 55f })
        {
            if (!PointAlong(waypoints, origin.position, distance, out var point, out var direction) ||
                !Ground(point, out var ground)) continue;
            var rotation = Quaternion.LookRotation(direction, Vector3.up);
            var right = Vector3.Cross(Vector3.up, direction);
            foreach (float side in new[] { 5f, 4.5f, 4f, 3.5f, -5f, -4.5f, -4f, -3.5f })
                if (TrySlot(point + right * side, rotation, clearance, layer, ground.point.y, out var pose))
                    return pose;
        }

        Debug.LogWarning("[RedLine] Не нашёл обочину для засады полиции, ставлю на маршрут в 90 м", rival);
        PointAlong(waypoints, origin.position, 90f, out var fallback, out var heading);
        return new Pose(fallback + Vector3.Cross(Vector3.up, heading) * 4.5f, Quaternion.LookRotation(heading, Vector3.up));
    }

    private static bool PointAlong(List<Transform> waypoints, Vector3 start, float distance, out Vector3 point, out Vector3 direction)
    {
        point = start;
        direction = Vector3.forward;
        if (waypoints.Count < 2) return false;
        int first = 0;
        float best = float.PositiveInfinity;
        for (int i = 0; i < Mathf.Min(12, waypoints.Count); i++)
        {
            float d = Flat(waypoints[i].position - start).sqrMagnitude;
            if (d < best) { best = d; first = i; }
        }

        var from = waypoints[first].position;
        for (int i = first + 1; i < waypoints.Count; i++)
        {
            var to = waypoints[i].position;
            float length = Flat(to - from).magnitude;
            if (length > .01f)
            {
                direction = Flat(to - from).normalized;
                if (distance <= length)
                {
                    point = Vector3.Lerp(from, to, distance / length);
                    return true;
                }
                distance -= length;
            }
            from = to;
        }
        return false;
    }

    private static bool TrySlot(Vector3 candidate, Quaternion rotation, float clearance, int roadLayer, float height, out Pose pose)
    {
        pose = default;
        if (!Ground(candidate, out var hit) || hit.collider.gameObject.layer != roadLayer ||
            Mathf.Abs(hit.point.y - height) > 1.5f) return false;

        int mask = ~((1 << 2) | (1 << 5) | (1 << roadLayer));
        var center = hit.point + Vector3.up * 1.25f;
        if (Physics.CheckBox(center, new Vector3(1.2f, .6f, 2.7f), rotation, mask, QueryTriggerInteraction.Ignore)) return false;
        pose = new Pose(hit.point + Vector3.up * clearance, rotation);
        return true;
    }

    private static bool Ground(Vector3 point, out RaycastHit hit, Transform ignore = null)
    {
        int vehicle = LayerMask.NameToLayer("Vehicle");
        int mask = ~((1 << 2) | (1 << 5) | (vehicle >= 0 ? 1 << vehicle : 0));
        var hits = Physics.RaycastAll(point + Vector3.up * 4f, Vector3.down, 14f, mask, QueryTriggerInteraction.Ignore)
            .Where(item => ignore == null || !item.collider.transform.IsChildOf(ignore))
            .OrderBy(item => item.distance)
            .ToList();
        hit = hits.FirstOrDefault();
        return hits.Count > 0;
    }

    private static T Ensure<T>(Scene scene, string prefabPath) where T : Component =>
        All<T>(scene.GetRootGameObjects()).FirstOrDefault() ??
        ((GameObject)PrefabUtility.InstantiatePrefab(Load<GameObject>(prefabPath), scene)).GetComponent<T>();

    private static void BuildMainMenu()
    {
        if (AssetDatabase.LoadAssetAtPath<SceneAsset>(MenuScenePath) != null)
        {
            var existing = EditorSceneManager.OpenScene(MenuScenePath);
            var menu = All<MainMenuController>(existing.GetRootGameObjects()).Single();
            if (new SerializedObject(menu).FindProperty("intro").objectReferenceValue == null)
            {
                BuildIntroduction(menu);
                EditorSceneManager.SaveScene(existing);
            }
            return;
        }
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        var data = Load<GameProgressionManager>(ProgressionPath);
        var settingsPrefab = Load<GameObject>(SettingsPrefabPath);

        var cameraObject = new GameObject("Main Camera") { tag = "MainCamera" };
        var camera = cameraObject.AddComponent<Camera>();
        camera.clearFlags = CameraClearFlags.SolidColor;
        camera.backgroundColor = new Color(.13f, .14f, .15f);
        camera.cullingMask = 0;
        cameraObject.AddComponent<AudioListener>();

        var events = new GameObject("EventSystem");
        events.AddComponent<EventSystem>();
        events.AddComponent<InputSystemUIInputModule>();

        var canvas = NewCanvas("Main Menu", 0);
        var root = (RectTransform)canvas.transform;
        var controller = canvas.gameObject.AddComponent<MainMenuController>();

        Picture(Stretch("Backdrop", root), null, new Color(.2f, .21f, .22f));
        var map = Node("City Map", root, Half, Half, Vector2.zero, new Vector2(1920f, 1920f));
        var mapImage = map.gameObject.AddComponent<RawImage>();
        mapImage.texture = Load<Texture2D>(MapTexturePath);
        mapImage.raycastTarget = false;
        Envelope(map);

        var lines = Stretch("Scanlines", root).gameObject.AddComponent<RawImage>();
        lines.texture = Load<Texture2D>(SpriteFolder + "Scanlines.png");
        lines.uvRect = new Rect(0f, 0f, 1f, 270f);
        lines.color = new Color(1f, 1f, 1f, .07f);
        lines.raycastTarget = false;
        Picture(Stretch("Vignette", root), Art("Vignette"), Color.white);

        var layer = Node("Race Markers", root, Half, Half, Vector2.zero, new Vector2(1920f, 1920f));
        Envelope(layer);
        var markers = new List<RaceMapMarker>();
        for (int i = 0; i < data.Stages.Length; i++) markers.Add(BuildMarker(layer, i + 1, data.Stages[i].mapPosition));

        BuildHeader(root);
        var progress = BuildFooter(root);
        var toast = BuildToast(root, out var toastText);
        var briefing = BuildBriefing(root, controller);

        var binding = new SerializedObject(controller);
        var list = binding.FindProperty("markers");
        list.arraySize = markers.Count;
        for (int i = 0; i < markers.Count; i++) list.GetArrayElementAtIndex(i).objectReferenceValue = markers[i];
        binding.ApplyModifiedPropertiesWithoutUndo();
        Bind(controller, ("progressText", progress), ("toast", toast), ("toastText", toastText), ("briefing", briefing));
        BuildIntroduction(controller);

        PrefabUtility.InstantiatePrefab(settingsPrefab, scene);
        EditorSceneManager.SaveScene(scene, MenuScenePath);
        Debug.Log("[RedLine] Создана сцена " + MenuScenePath);
    }

    private static void BuildIntroduction(MainMenuController controller)
    {
        font ??= Load<Font>(FontPath);
        var intro = Stretch("City Introduction", controller.transform);
        Picture(Stretch("Backdrop", intro), null, new Color(0f, 0f, 0f, .72f), true);

        var paper = Node("Introduction Paper", intro, Half, Half, Vector2.zero, new Vector2(940f, 680f));
        paper.localRotation = Quaternion.Euler(0f, 0f, 1.2f);
        Picture(paper, Load<Sprite>(FinishSpriteAuthoring.Paper), Color.white, true);
        var top = new Vector2(.5f, 1f);
        var bottom = new Vector2(.5f, 0f);
        Label(Node("Title", paper, top, top, new Vector2(0f, -66f), new Vector2(776f, 54f)),
            "ПРОЛОГ", 42, PaperInk, TextAnchor.MiddleLeft, FontStyle.Bold);
        Picture(Node("Rule", paper, top, top, new Vector2(0f, -134f), new Vector2(776f, 3f)), null, PaperAccent);
        var story = Label(Node("Story", paper, top, top, new Vector2(0f, -162f), new Vector2(776f, 362f)),
            "", 27, PaperInk, TextAnchor.UpperLeft, FontStyle.Normal, 18);
        story.lineSpacing = 1.16f;
        var next = FilledButton(Node("Continue", paper, bottom, bottom, new Vector2(0f, 70f), new Vector2(286f, 62f)),
            "К КАРТЕ", PaperInk, Color.white, 25, out _);
        Bind(controller, ("intro", intro.gameObject), ("introStoryText", story), ("introContinueButton", next));
        intro.gameObject.SetActive(false);
    }

    private static RaceMapMarker BuildMarker(RectTransform layer, int stage, Vector2 position)
    {
        var rect = Node($"Race Marker {stage}", layer, position, Half, Vector2.zero, new Vector2(110f, 110f));
        var marker = rect.gameObject.AddComponent<RaceMapMarker>();
        var body = Node("Body", rect, Half, Half, Vector2.zero, new Vector2(84f, 84f));
        var glow = Picture(Node("Glow", body, Half, Half, Vector2.zero, new Vector2(150f, 150f)), Art("Glow"), Accent);
        var ring = Picture(Node("Ring", body, Half, Half, Vector2.zero, new Vector2(84f, 84f)), Art("Circle"), Ink, true);
        var core = Picture(Node("Core", body, Half, Half, Vector2.zero, new Vector2(66f, 66f)), Art("Circle"), Accent, true);
        var number = Label(Node("Number", body, Half, Half, new Vector2(0f, 1f), new Vector2(66f, 66f)),
            stage.ToString(), 34, Ink, TextAnchor.MiddleCenter, FontStyle.Bold);
        var lockIcon = Picture(Node("Lock", body, Half, Half, Vector2.zero, new Vector2(34f, 34f)), Art("Lock"),
            new Color(.56f, .58f, .6f));
        var badge = Node("Check", body, Half, Half, new Vector2(30f, 30f), new Vector2(34f, 34f));
        var badgeImage = Picture(badge, Art("Circle"), Accent);
        Picture(Node("Icon", badge, Half, Half, Vector2.zero, new Vector2(20f, 20f)), Art("Check"), Ink);

        bool left = position.x > .9f;
        var plate = Node("Label", rect, Half, left ? new Vector2(1f, .5f) : new Vector2(.5f, 1f),
            left ? new Vector2(-54f, 0f) : new Vector2(0f, -50f), new Vector2(150f, 36f));
        Picture(plate, Art("Panel"), new Color(.04f, .045f, .05f, .82f), false, 8f);
        var label = Label(Stretch("Text", plate), $"ГОНКА {stage}", 20, Color.white, TextAnchor.MiddleCenter, FontStyle.Bold);

        var button = rect.gameObject.AddComponent<Button>();
        button.transition = Selectable.Transition.None;
        button.targetGraphic = core;
        var navigation = button.navigation;
        navigation.mode = Navigation.Mode.None;
        button.navigation = navigation;

        var binding = new SerializedObject(marker);
        binding.FindProperty("stage").intValue = stage;
        binding.ApplyModifiedPropertiesWithoutUndo();
        Bind(marker, ("button", button), ("body", body), ("glow", glow), ("ring", ring), ("core", core), ("number", number),
            ("lockIcon", lockIcon), ("checkBadge", badgeImage), ("label", label));
        return marker;
    }

    private static void BuildHeader(RectTransform root)
    {
        var header = Node("Header", root, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(64f, -40f), new Vector2(1000f, 190f));
        var title = Label(Node("Title", header, new Vector2(0f, 1f), new Vector2(0f, 1f), Vector2.zero, new Vector2(1000f, 112f)),
            "REDLINE", 104, Color.white, TextAnchor.MiddleLeft, FontStyle.Bold);
        Shadowed(title);
        Picture(Node("Accent Bar", header, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(4f, -118f), new Vector2(260f, 7f)),
            null, Accent);
        Shadowed(Label(Node("Subtitle", header, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(4f, -134f), new Vector2(1000f, 44f)),
            "НОЧНАЯ ЛИГА  ·  КАРТА ГОРОДА", 28, new Color(.86f, .88f, .9f), TextAnchor.MiddleLeft, FontStyle.Bold));
    }

    private static Text BuildFooter(RectTransform root)
    {
        var footer = Node("Footer", root, Vector2.zero, Vector2.zero, new Vector2(64f, 44f), new Vector2(760f, 120f));
        var plate = Node("Progress Plate", footer, new Vector2(0f, 1f), new Vector2(0f, 1f), Vector2.zero, new Vector2(440f, 58f));
        Picture(plate, Art("Panel"), new Color(.04f, .045f, .05f, .86f), false, 10f);
        Picture(Node("Accent", plate, new Vector2(0f, .5f), new Vector2(0f, .5f), Vector2.zero, new Vector2(7f, 58f)), null, Accent);
        var progress = Label(Stretch("Text", plate, 26f, 0f, 16f, 0f), "ПРОЙДЕНО 0 / 4", 25, Color.white,
            TextAnchor.MiddleLeft, FontStyle.Bold, 16);

        var legend = Node("Legend", footer, Vector2.zero, Vector2.zero, new Vector2(0f, 6f), new Vector2(760f, 34f));
        LegendItem(legend, 0f, Art("Circle"), Accent, "ДОСТУПНА");
        LegendItem(legend, 200f, Art("Check"), Color.white, "ПРОЙДЕНА");
        LegendItem(legend, 400f, Art("Lock"), new Color(.6f, .62f, .64f), "ЗАКРЫТА");

        Shadowed(Label(Node("Hint", root, new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(-64f, 50f), new Vector2(700f, 34f)),
            "ESC — НАСТРОЙКИ", 20, new Color(.85f, .87f, .89f), TextAnchor.MiddleRight, FontStyle.Bold));
        return progress;
    }

    private static void LegendItem(RectTransform parent, float x, Sprite icon, Color color, string text)
    {
        var item = Node(text, parent, Vector2.zero, Vector2.zero, new Vector2(x, 0f), new Vector2(190f, 34f));
        Picture(Node("Icon", item, new Vector2(0f, .5f), new Vector2(0f, .5f), Vector2.zero, new Vector2(22f, 22f)), icon, color);
        Shadowed(Label(Node("Text", item, new Vector2(0f, .5f), new Vector2(0f, .5f), new Vector2(32f, 0f), new Vector2(160f, 34f)),
            text, 19, new Color(.88f, .9f, .92f), TextAnchor.MiddleLeft, FontStyle.Bold));
    }

    private static CanvasGroup BuildToast(RectTransform root, out Text text)
    {
        var toast = Node("Toast", root, new Vector2(.5f, 0f), new Vector2(.5f, 0f), new Vector2(0f, 150f), new Vector2(780f, 70f));
        var group = toast.gameObject.AddComponent<CanvasGroup>();
        group.alpha = 0f;
        group.blocksRaycasts = false;
        group.interactable = false;
        Picture(toast, Art("Panel"), new Color(.05f, .055f, .06f, .94f), false, 10f);
        Picture(Node("Accent", toast, new Vector2(0f, .5f), new Vector2(0f, .5f), Vector2.zero, new Vector2(8f, 70f)), null, Danger);
        Picture(Node("Icon", toast, new Vector2(0f, .5f), new Vector2(0f, .5f), new Vector2(30f, 0f), new Vector2(30f, 30f)),
            Art("Lock"), Danger);
        text = Label(Stretch("Text", toast, 80f, 0f, 24f, 0f), "Сначала пройдите предыдущую гонку", 26, Color.white,
            TextAnchor.MiddleLeft, FontStyle.Bold, 16);
        return group;
    }

    private static GameObject BuildBriefing(RectTransform root, MainMenuController controller)
    {
        var briefing = Stretch("Briefing", root);
        var backdrop = Stretch("Backdrop", briefing);
        var backdropImage = Picture(backdrop, null, new Color(0f, 0f, 0f, .64f), true);
        var backdropButton = backdrop.gameObject.AddComponent<Button>();
        backdropButton.transition = Selectable.Transition.None;
        backdropButton.targetGraphic = backdropImage;

        var card = Node("Card", briefing, Half, Half, new Vector2(0f, -10f), new Vector2(1420f, 780f));

        var paper = Node("Dossier", card, new Vector2(0f, .5f), new Vector2(0f, .5f), new Vector2(10f, 0f), new Vector2(720f, 780f));
        paper.localRotation = Quaternion.Euler(0f, 0f, 1.4f);
        Picture(paper, Load<Sprite>(FinishSpriteAuthoring.Paper), Color.white, true);
        var stage = Label(Node("Stage", paper, new Vector2(.5f, 1f), new Vector2(.5f, 1f), new Vector2(0f, -78f), new Vector2(580f, 34f)),
            "ГОНКА 1  ·  ГЛАВА I", 22, PaperAccent, TextAnchor.MiddleLeft, FontStyle.Bold);
        var chapter = Label(Node("Chapter Title", paper, new Vector2(.5f, 1f), new Vector2(.5f, 1f), new Vector2(0f, -118f),
            new Vector2(580f, 96f)), "ГЛАВА I", 36, PaperInk, TextAnchor.UpperLeft, FontStyle.Bold, 22);
        Picture(Node("Rule", paper, new Vector2(.5f, 1f), new Vector2(.5f, 1f), new Vector2(0f, -226f), new Vector2(580f, 3f)),
            null, new Color(.39f, .26f, .145f, .56f));
        var story = Label(Node("Story", paper, new Vector2(.5f, 1f), new Vector2(.5f, 1f), new Vector2(0f, -250f),
            new Vector2(580f, 420f)), "", 26, PaperInk, TextAnchor.UpperLeft, FontStyle.Normal, 16);
        story.lineSpacing = 1.25f;
        var stamp = Label(Node("Stamp", paper, new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(-70f, 66f), new Vector2(260f, 70f)),
            "ДОСЬЕ", 44, new Color(.7f, .12f, .08f, .5f), TextAnchor.MiddleCenter, FontStyle.Bold);
        stamp.rectTransform.localRotation = Quaternion.Euler(0f, 0f, 12f);
        var stampLine = stamp.gameObject.AddComponent<Outline>();
        stampLine.effectColor = new Color(.7f, .12f, .08f, .35f);
        stampLine.effectDistance = new Vector2(2f, -2f);

        var info = Node("Rival Card", card, new Vector2(1f, .5f), new Vector2(1f, .5f), Vector2.zero, new Vector2(680f, 720f));
        Picture(info, Art("Panel"), PanelColor, true, 12f);
        var bar = Node("Accent Bar", info, new Vector2(0f, 1f), new Vector2(.5f, 1f), Vector2.zero, new Vector2(0f, 7f));
        bar.anchorMax = Vector2.one;
        Picture(bar, null, Accent);

        TopLeftLabel(info, "Rival Label", "СОПЕРНИК", 44f, -46f, 20, Muted);
        var rival = TopLeftLabel(info, "Rival", "—", 44f, -74f, 44, Color.white, FontStyle.Bold, 320f, 60f);
        TopLeftLabel(info, "Car Label", "МАШИНА", 44f, -156f, 20, Muted);
        var car = TopLeftLabel(info, "Car", "—", 44f, -184f, 32, Accent, FontStyle.Bold, 320f, 48f);
        TopLeftLabel(info, "Status Label", "СТАТУС", 44f, -262f, 20, Muted);
        var status = TopLeftLabel(info, "Status", "ДОСТУПНА", 44f, -290f, 22, Color.white, FontStyle.Bold, 320f, 64f);

        var keyFrame = Node("Key Frame", info, Vector2.one, Vector2.one, new Vector2(-40f, -46f), new Vector2(270f, 370f));
        Picture(keyFrame, Art("Panel"), new Color(.03f, .035f, .04f, .9f), false, 10f);
        var key = Picture(Node("Key", keyFrame, new Vector2(.5f, 1f), new Vector2(.5f, 1f), new Vector2(0f, -16f), new Vector2(250f, 290f)),
            null, Color.white);
        key.preserveAspect = true;
        var reward = Label(Node("Reward", keyFrame, new Vector2(.5f, 0f), new Vector2(.5f, 0f), new Vector2(0f, 8f), new Vector2(236f, 50f)),
            "НАГРАДА", 16, Muted, TextAnchor.MiddleCenter, FontStyle.Bold, 11);

        var play = Node("Play", info, new Vector2(.5f, 0f), new Vector2(.5f, 0f), new Vector2(0f, 44f), new Vector2(592f, 112f));
        var playButton = FilledButton(play, "PLAY", Accent, Ink, 60, out var playLabel);
        Label(Node("Hint", info, new Vector2(.5f, 0f), new Vector2(.5f, 0f), new Vector2(0f, 172f), new Vector2(592f, 30f)),
            "Победи соперника, чтобы открыть следующую гонку", 18, Muted, TextAnchor.MiddleCenter, FontStyle.Normal, 12);

        var close = Node("Close", card, Vector2.one, Half, new Vector2(-6f, -30f), new Vector2(64f, 64f));
        Picture(close, Art("Circle"), DarkButton, true);
        var closeIcon = Picture(Node("Icon", close, Half, Half, Vector2.zero, new Vector2(28f, 28f)), Art("Close"), Color.white);
        var closeButton = Tinted(close, closeIcon, Color.white, Accent, AccentPressed);

        Bind(controller, ("backdropButton", backdropButton), ("closeButton", closeButton), ("stageText", stage),
            ("chapterTitleText", chapter), ("storyText", story), ("rivalText", rival), ("carText", car), ("keyImage", key),
            ("rewardText", reward), ("statusText", status), ("playButton", playButton), ("playLabel", playLabel));
        briefing.gameObject.SetActive(false);
        return briefing.gameObject;
    }

    private static Text TopLeftLabel(RectTransform parent, string name, string value, float x, float y, int size, Color color,
        FontStyle style = FontStyle.Bold, float width = 320f, float height = 30f) =>
        Label(Node(name, parent, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(x, y), new Vector2(width, height)),
            value, size, color, TextAnchor.UpperLeft, style, Mathf.Max(12, size / 2));

    private static Canvas NewCanvas(string name, int order)
    {
        var rect = Node(name, null);
        var canvas = rect.gameObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = order;
        var scaler = rect.gameObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 1f;
        rect.gameObject.AddComponent<GraphicRaycaster>();
        return canvas;
    }

    private static GameObject SavePrefab(GameObject root, string path)
    {
        var prefab = PrefabUtility.SaveAsPrefabAsset(root, path);
        Object.DestroyImmediate(root);
        return prefab;
    }

    private static readonly Vector2 Half = new(.5f, .5f);

    private static RectTransform Node(string name, Transform parent)
    {
        var child = new GameObject(name, typeof(RectTransform)) { layer = 5 };
        var rect = (RectTransform)child.transform;
        if (parent != null) rect.SetParent(parent, false);
        return rect;
    }

    private static RectTransform Node(string name, Transform parent, Vector2 anchor, Vector2 pivot, Vector2 position, Vector2 size)
    {
        var rect = Node(name, parent);
        rect.anchorMin = rect.anchorMax = anchor;
        rect.pivot = pivot;
        rect.anchoredPosition = position;
        rect.sizeDelta = size;
        return rect;
    }

    private static RectTransform Stretch(string name, Transform parent, float left = 0f, float bottom = 0f, float right = 0f,
        float top = 0f)
    {
        var rect = Node(name, parent);
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.pivot = Half;
        rect.offsetMin = new Vector2(left, bottom);
        rect.offsetMax = new Vector2(-right, -top);
        return rect;
    }

    private static RectTransform Row(string name, RectTransform parent, float height)
    {
        var rect = Node(name, parent);
        var element = rect.gameObject.AddComponent<LayoutElement>();
        element.minHeight = element.preferredHeight = height;
        return rect;
    }

    private static void Envelope(RectTransform rect)
    {
        var fitter = rect.gameObject.AddComponent<AspectRatioFitter>();
        fitter.aspectMode = AspectRatioFitter.AspectMode.EnvelopeParent;
        fitter.aspectRatio = 1f;
    }

    private static Image Picture(RectTransform rect, Sprite sprite, Color color, bool raycast = false, float corner = 0f)
    {
        var image = rect.gameObject.AddComponent<Image>();
        image.sprite = sprite;
        image.color = color;
        image.raycastTarget = raycast;
        if (sprite != null && sprite.border != Vector4.zero)
        {
            image.type = Image.Type.Sliced;
            if (corner > 0f) image.pixelsPerUnitMultiplier = 24f / corner;
        }
        return image;
    }

    private static Text Label(RectTransform rect, string value, int size, Color color, TextAnchor alignment,
        FontStyle style = FontStyle.Normal, int minSize = 0)
    {
        var text = rect.gameObject.AddComponent<Text>();
        text.font = font;
        text.text = value;
        text.fontSize = size;
        text.fontStyle = style;
        text.color = color;
        text.alignment = alignment;
        text.raycastTarget = false;
        text.supportRichText = true;
        text.horizontalOverflow = HorizontalWrapMode.Wrap;
        text.verticalOverflow = minSize > 0 ? VerticalWrapMode.Truncate : VerticalWrapMode.Overflow;
        if (minSize > 0)
        {
            text.resizeTextForBestFit = true;
            text.resizeTextMinSize = minSize;
            text.resizeTextMaxSize = size;
        }
        return text;
    }

    private static void Shadowed(Graphic graphic)
    {
        var shadow = graphic.gameObject.AddComponent<Shadow>();
        shadow.effectColor = new Color(0f, 0f, 0f, .7f);
        shadow.effectDistance = new Vector2(2f, -3f);
    }

    private static Button Tinted(RectTransform rect, Graphic target, Color normal, Color highlighted, Color pressed)
    {
        var button = rect.gameObject.AddComponent<Button>();
        button.targetGraphic = target;
        var colors = ColorBlock.defaultColorBlock;
        colors.normalColor = normal;
        colors.highlightedColor = highlighted;
        colors.pressedColor = pressed;
        colors.selectedColor = normal;
        colors.disabledColor = new Color(.45f, .47f, .5f, .7f);
        colors.fadeDuration = .08f;
        button.colors = colors;
        var navigation = button.navigation;
        navigation.mode = Navigation.Mode.None;
        button.navigation = navigation;
        return button;
    }

    private static Button FilledButton(RectTransform rect, string text, Color fill, Color ink, int size, out Text label)
    {
        var background = Picture(rect, Art("Panel"), Color.white, true, 10f);
        var hover = fill == Accent ? AccentHover : Color.Lerp(fill, Color.white, .18f);
        var pressed = fill == Accent ? AccentPressed : Color.Lerp(fill, Color.black, .25f);
        var button = Tinted(rect, background, fill, hover, pressed);
        label = Label(Stretch("Label", rect, 16f, 0f, 16f, 0f), text, size, ink, TextAnchor.MiddleCenter, FontStyle.Bold, 14);
        return button;
    }

    private static Button OutlineButton(RectTransform rect, string text, Color color, int size, out Text label)
    {
        var background = Picture(rect, Art("PanelOutline"), Color.white, true, 10f);
        var button = Tinted(rect, background, color, Color.Lerp(color, Color.white, .35f), Color.Lerp(color, Color.black, .25f));
        label = Label(Stretch("Label", rect, 16f, 0f, 16f, 0f), text, size, color, TextAnchor.MiddleCenter, FontStyle.Bold, 14);
        return button;
    }

    private static void Bind(Object target, params (string name, Object value)[] fields)
    {
        var binding = new SerializedObject(target);
        foreach (var (name, value) in fields)
        {
            var property = binding.FindProperty(name) ??
                           throw new InvalidOperationException($"{target.GetType().Name}: нет поля {name}");
            property.objectReferenceValue = value;
        }
        binding.ApplyModifiedPropertiesWithoutUndo();
    }

    private static IEnumerable<T> All<T>(GameObject[] roots) where T : Component =>
        roots.SelectMany(root => root.GetComponentsInChildren<T>(true));

    private static Sprite Art(string name) => Load<Sprite>(SpriteFolder + name + ".png");

    private static T Load<T>(string path) where T : Object =>
        AssetDatabase.LoadAssetAtPath<T>(path) ?? throw new InvalidOperationException($"Не найден ассет {path}");

    private static Vector3 Flat(Vector3 value) => new(value.x, 0f, value.z);

    private static void EnsureFolder(string path)
    {
        if (AssetDatabase.IsValidFolder(path)) return;
        int slash = path.LastIndexOf('/');
        EnsureFolder(path.Substring(0, slash));
        AssetDatabase.CreateFolder(path.Substring(0, slash), path.Substring(slash + 1));
    }
}

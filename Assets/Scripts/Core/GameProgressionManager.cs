using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.SceneManagement;

[CreateAssetMenu(menuName = "RedLine/Game Progression", fileName = "GameProgression")]
public sealed class GameProgressionManager : ScriptableObject
{
    public const string ResourcePath = "GameProgression";
    public const string MainMenuScene = "MainMenu";
    public const int StageCount = 4;

    private const string UnlockedKey = "RedLine.UnlockedStage";
    private const string CompletedKey = "RedLine.CompletedStages";

    [Serializable]
    public sealed class StageConfig
    {
        [InspectorName("Сцена")] public string sceneName;
        [InspectorName("Название главы")] public string chapterTitle;
        [InspectorName("Соперник")] public string rivalName;
        [InspectorName("Машина (ключ)")] public string carName;
        [InspectorName("Спрайт ключа")] public Sprite keySprite;
        [InspectorName("Предыстория (до гонки)"), TextArea(4, 10)] public string briefing;
        [InspectorName("Финал (после победы)"), TextArea(4, 10)] public string victoryStory;
        [InspectorName("Позиция на карте меню"), Tooltip("Нормализованные координаты иконки на карте главного меню (0..1)")]
        public Vector2 mapPosition;
    }

    [SerializeField] private StageConfig[] stages = CreateDefaultStages();

    public StageConfig[] Stages
    {
        get => stages;
        set => stages = value;
    }

    public static event Action ProgressChanged;

    private static GameProgressionManager instance;

    public static GameProgressionManager Instance =>
        instance != null ? instance : instance = Resources.Load<GameProgressionManager>(ResourcePath);

    public static int UnlockedStage => Mathf.Clamp(PlayerPrefs.GetInt(UnlockedKey, 1), 1, StageCount);

    public static IReadOnlyList<int> CompletedStages => ReadCompleted();

    public static int CurrentStage => StageOfScene(SceneManager.GetActiveScene().name);

    public static bool IsStageUnlocked(int stageIndex) => stageIndex >= 1 && stageIndex <= UnlockedStage;

    public static bool IsStageCompleted(int stageIndex) => ReadCompleted().Contains(stageIndex);

    public static void CompleteStage(int stageIndex)
    {
        if (stageIndex < 1 || stageIndex > StageCount) return;
        var completed = ReadCompleted();
        if (!completed.Contains(stageIndex)) completed.Add(stageIndex);
        completed.Sort();
        PlayerPrefs.SetString(CompletedKey, string.Join(",", completed));
        PlayerPrefs.SetInt(UnlockedKey, Mathf.Max(UnlockedStage, Mathf.Min(stageIndex + 1, StageCount)));
        PlayerPrefs.Save();
        ProgressChanged?.Invoke();
    }

    public static void ResetProgress()
    {
        PlayerPrefs.SetInt(UnlockedKey, 1);
        PlayerPrefs.DeleteKey(CompletedKey);
        PlayerPrefs.Save();
        ProgressChanged?.Invoke();
    }

    public static StageConfig GetStage(int stageIndex)
    {
        var data = Instance;
        return data != null && data.stages != null && stageIndex >= 1 && stageIndex <= data.stages.Length
            ? data.stages[stageIndex - 1]
            : null;
    }

    public static int StageOfScene(string sceneName)
    {
        var data = Instance;
        if (data != null && data.stages != null)
            for (int i = 0; i < data.stages.Length; i++)
                if (data.stages[i] != null && data.stages[i].sceneName == sceneName)
                    return i + 1;
        return int.TryParse(sceneName, out int stage) && stage >= 1 && stage <= StageCount ? stage : 0;
    }

    private static List<int> ReadCompleted() =>
        PlayerPrefs.GetString(CompletedKey, string.Empty)
            .Split(',', StringSplitOptions.RemoveEmptyEntries)
            .Select(value => int.TryParse(value, out int stage) ? stage : 0)
            .Where(stage => stage >= 1 && stage <= StageCount)
            .Distinct()
            .ToList();

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        instance = null;
        ProgressChanged = null;
    }

    public static StageConfig[] CreateDefaultStages() => new[]
    {
        new StageConfig
        {
            sceneName = "1",
            chapterTitle = "ГЛАВА I: ВОЗВРАЩЕНИЕ НА УЛИЦЫ",
            rivalName = "Кай",
            carName = "HONDA NSX",
            briefing = "Город не забывает тех, кто ушёл без боя. Чтобы вернуться в ночную лигу, придётся начать с самых низов. Первый соперник — Кай на Honda NSX. Обойди его на трассе, чтобы забрать ключи и заявить о себе.",
            victoryStory = "Кай: «Думал, ты просто залётный... Ладно, уговор есть уговор. Держи ключи от NSX. Но дальше начинаются районы Синдиката — там за ошибки платят не машинами, а свободой».",
            mapPosition = new Vector2(.9f, .525f)
        },
        new StageConfig
        {
            sceneName = "2",
            chapterTitle = "ГЛАВА II: СЛЕД СИНДИКАТА",
            rivalName = "Гонщик Синдиката",
            carName = "NISSAN GT-R",
            briefing = "Кай предупреждал о районах Синдиката — ставки взлетели вдвое. Теперь против тебя выставили заряженный Nissan GT-R. Победи в этом заезде, чтобы забрать GT-R и выйти на след того, кто подставил тебя в прошлом.",
            victoryStory = "Рэй (механик): «Красиво прошёл! С этим GT-R ты теперь в высшей лиге. Я пробил старые записи — твой прошлый провал подстроил Рейвен, и он уже ждёт тебя на верхнем ярусе».",
            mapPosition = new Vector2(.68f, .529f)
        },
        new StageConfig
        {
            sceneName = "3",
            chapterTitle = "ГЛАВА III: ПОСЛЕДНИЙ РУБЕЖ",
            rivalName = "Виктория",
            carName = "LAMBORGHINI",
            briefing = "Теперь ты знаешь правду: за твоим прошлым провалом стоит лидер Синдиката — Рейвен. На пути к нему стоит его правая рука — Виктория на Lamborghini. Выиграй гонку на скоростных развязках, чтобы забрать ключи и открыть дорогу в финал.",
            victoryStory = "Виктория: «Забирай ключи от Lamborghini, ты их заслужил. Рейвен уже перекрыл центр города для финального заезда. Если выиграешь там — весь город будет знать, кто здесь номер один».",
            mapPosition = new Vector2(.875f, .39f)
        },
        new StageConfig
        {
            sceneName = "4",
            chapterTitle = "ГЛАВА IV: КОРОЛЬ УЛИЦ",
            rivalName = "Рейвен",
            carName = "MCLAREN",
            briefing = "Финал. Рейвен перекрыл центр города и вывел на старт свой главный McLaren. Весь город следит за этой дуэлью — выиграй заезд, чтобы забрать его ключи и вернуть себе корону ночных улиц.",
            victoryStory = "Рейвен: «Невероятно... Ты действительно прошёл весь путь до конца. Вот ключи от McLaren. С этой ночи улицы города принадлежат тебе».",
            mapPosition = new Vector2(.962f, .659f)
        }
    };
}

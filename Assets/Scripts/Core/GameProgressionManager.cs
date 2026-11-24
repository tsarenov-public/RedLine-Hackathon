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
    private const string IntroSeenKey = "RedLine.CityIntroSeen";

    private const string DefaultIntroduction = "Твой отец — начальник крупнейшей портовой компании города — отказался подписывать грязный контракт на провоз контрабанды через свои терминалы. В ту же ночь его похитила уличная банда гонщиков, крышующая нелегальные грузы. Полиция бездействует, а все следы ведут на ночные трассы. Из всего, что у тебя осталось, — только машина отца в пустом гараже. Чтобы люди банды согласились выйти с тобой на старт и заговорили, ты ставишь на кон свою тачку, шаг за шагом пробиваясь к главарю банды, чтобы освободить отца и узнать имя заказчика.";

    [Serializable]
    public sealed class StageConfig
    {
        [InspectorName("Сцена")] public string sceneName;
        [InspectorName("Название миссии")] public string missionName;
        [InspectorName("Название главы")] public string chapterTitle;
        [InspectorName("Соперник")] public string rivalName;
        [InspectorName("Машина (ключ)")] public string carName;
        [InspectorName("Спрайт ключа")] public Sprite keySprite;
        [InspectorName("Предыстория (до гонки)"), TextArea(4, 10)] public string briefing;
        [InspectorName("Финал (после победы)"), TextArea(4, 10)] public string victoryStory;
        [InspectorName("Позиция на карте меню"), Tooltip("Нормализованные координаты иконки на карте главного меню (0..1)")]
        public Vector2 mapPosition;
    }

    [SerializeField, TextArea(8, 15)] private string introduction = DefaultIntroduction;
    [SerializeField] private StageConfig[] stages = CreateDefaultStages();

    public StageConfig[] Stages
    {
        get => stages;
        set => stages = value;
    }

    public string Introduction => introduction;
    public static bool HasSeenIntroduction => PlayerPrefs.GetInt(IntroSeenKey, 0) != 0;

    public static void AcknowledgeIntroduction()
    {
        PlayerPrefs.SetInt(IntroSeenKey, 1);
        PlayerPrefs.Save();
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
        PlayerPrefs.DeleteKey(IntroSeenKey);
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
            missionName = "Погоня",
            chapterTitle = "ГЛАВА I. СЛЕДЫ",
            rivalName = "Кай",
            carName = "HONDA NSX",
            briefing = "У ворот порта замечен Кай. Начни погоню на полицейской машине, забери ключи от Honda NSX и выясни, куда увезли твоего отца.",
            victoryStory = "Я только стоял на шухере у ворот порта, а твоего отца увезли в промзону. Ищи Маркуса.",
            mapPosition = new Vector2(.9f, .525f)
        },
        new StageConfig
        {
            sceneName = "2",
            missionName = "Кольцевая гонка",
            chapterTitle = "ГЛАВА II. Первый заезд",
            rivalName = "Маркус",
            carName = "NISSAN GT-R",
            briefing = "Ниточка от Кая приводит тебя  к Маркусу — гонщику. На старой машине отца против его заряженного зверя, Одержи победу, забери ключи от Nissan GT-R и заставь Маркуса раскрыть, где держат начальника порта.",
            victoryStory = "Маркус глушит мотор и протягивает ключи от GT-R:\n«Держи ключи, ты честно забрал эту тачку. Мы всего лишь перевозчики. Твоего отца держат на закрытой базе в центре города — он до сих пор отказывается подписывать бумаги. Прорвёшься через Викторию на развязках — выйдешь прямо на нашего главаря».",
            mapPosition = new Vector2(.68f, .529f)
        },
        new StageConfig
        {
            sceneName = "3",
            missionName = "Скоростные развязки",
            chapterTitle = "ГЛАВА III. Шпильки",
            rivalName = "Виктория",
            carName = "MCLAREN",
            briefing = "До базы в деловом квартале остался один шаг, но все подъезды туда перекрыла Виктория — правая рука главаря банды. Она отвечает за безопасность верхушки и не собирается пускать тебя в центр. Чтобы принять её вызов на скоростных развязках, ты садишься за руль отвоёванного у Маркуса Nissan GT-R и ставишь его против её суперкара. Обойди Викторию",
            victoryStory = "Виктория смотрит на финишное табло и молча отдаёт ключи от McLaren:\n«Этот McLaren теперь твой, и проезд в центр открыт. Рейвен сам ждёт тебя на главной прямой — выиграешь у него, и он отпустит твоего отца».",
            mapPosition = new Vector2(.875f, .39f)
        },
        new StageConfig
        {
            sceneName = "4",
            missionName = "Центр города",
            chapterTitle = "ГЛАВА IV. ИМЯ ЗАКАЗЧИКА",
            rivalName = "Рейвен",
            carName = "LAMBORGHINI",
            briefing = "Финальная дуэль. Главарь банды гонщиков Рейвен перекрыл центральный проспект и вывел на старт свой флагманский Lamborghini. Ты выходишь против него на выигранном у Виктории McLaren — на кону обе машины, свобода твоего отца и правда о том, кому понадобились портовые терминалы для контрабанды. Приди к финишу первым, забери ключи и заставь Рейвена назвать имя настоящего заказчика.",
            victoryStory = "Рейвен сдержал слово: твоего отца отпустили. Перед тем как уйти, Рейвен назвал имя заказчика и передал тебе ключи от Lamborghini.",
            mapPosition = new Vector2(.962f, .659f)
        }
    };
}

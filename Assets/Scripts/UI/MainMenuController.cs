using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

[DisallowMultipleComponent]
public sealed class MainMenuController : MonoBehaviour
{
    private static readonly string[] Roman = { "I", "II", "III", "IV", "V", "VI" };

    [SerializeField] private RaceMapMarker[] markers;

    [Header("Прогресс")]
    [SerializeField] private Text progressText;

    [Header("Подсказка")]
    [SerializeField] private CanvasGroup toast;
    [SerializeField] private Text toastText;
    [SerializeField, Min(.5f)] private float toastDuration = 2.2f;

    [Header("Брифинг")]
    [SerializeField] private GameObject briefing;
    [SerializeField] private Button backdropButton;
    [SerializeField] private Button closeButton;
    [SerializeField] private Text stageText;
    [SerializeField] private Text chapterTitleText;
    [SerializeField] private Text storyText;
    [SerializeField] private Text rivalText;
    [SerializeField] private Text carText;
    [SerializeField] private Image keyImage;
    [SerializeField] private Text rewardText;
    [SerializeField] private Text statusText;
    [SerializeField] private Button playButton;
    [SerializeField] private Text playLabel;

    private int selectedStage;
    private float toastHideAt = float.NegativeInfinity;
    private bool loading;

    private void Awake()
    {
        GamePause.SetPaused(false);
        foreach (var marker in markers) marker.Clicked += OnMarkerClicked;
        backdropButton.onClick.AddListener(CloseBriefing);
        closeButton.onClick.AddListener(CloseBriefing);
        playButton.onClick.AddListener(Play);
        briefing.SetActive(false);
        toast.alpha = 0f;
    }

    private void OnEnable()
    {
        GameProgressionManager.ProgressChanged += Refresh;
        Refresh();
    }

    private void OnDisable() => GameProgressionManager.ProgressChanged -= Refresh;

    private void Update()
    {
        float target = Time.unscaledTime < toastHideAt ? 1f : 0f;
        toast.alpha = Mathf.MoveTowards(toast.alpha, target, Time.unscaledDeltaTime * 5f);
    }

    private void Refresh()
    {
        int unlocked = GameProgressionManager.UnlockedStage;
        int completed = 0;
        foreach (var marker in markers)
        {
            int stage = marker.Stage;
            var data = GameProgressionManager.GetStage(stage);
            if (data != null && data.mapPosition != Vector2.zero) marker.SetMapPosition(data.mapPosition);

            bool done = GameProgressionManager.IsStageCompleted(stage) || stage < unlocked;
            if (done) completed++;
            marker.SetState(done ? RaceMarkerState.Completed :
                stage == unlocked ? RaceMarkerState.Current : RaceMarkerState.Locked);
        }

        progressText.text = completed >= markers.Length
            ? $"ПРОЙДЕНО {completed} / {markers.Length}  ·  УЛИЦЫ ТВОИ"
            : $"ПРОЙДЕНО {completed} / {markers.Length}";
        if (briefing.activeSelf && !GameProgressionManager.IsStageUnlocked(selectedStage)) CloseBriefing();
    }

    private void OnMarkerClicked(int stage)
    {
        if (!GameProgressionManager.IsStageUnlocked(stage))
        {
            ShowToast("Сначала пройдите предыдущую гонку");
            return;
        }
        OpenBriefing(stage);
    }

    private void OpenBriefing(int stage)
    {
        var data = GameProgressionManager.GetStage(stage);
        if (data == null)
        {
            ShowToast("Нет данных для этой гонки");
            return;
        }

        selectedStage = stage;
        bool done = GameProgressionManager.IsStageCompleted(stage) || stage < GameProgressionManager.UnlockedStage;
        stageText.text = $"ГОНКА {stage}  ·  ГЛАВА {Roman[Mathf.Clamp(stage - 1, 0, Roman.Length - 1)]}";
        chapterTitleText.text = data.chapterTitle;
        storyText.text = data.briefing;
        rivalText.text = data.rivalName;
        carText.text = data.carName;
        keyImage.sprite = data.keySprite;
        keyImage.enabled = data.keySprite != null;
        rewardText.text = $"НАГРАДА: КЛЮЧИ ОТ {data.carName}";
        statusText.text = done ? "ГОНКА ПРОЙДЕНА · МОЖНО ПЕРЕПРОЙТИ" : "ДОСТУПНА";
        playLabel.text = "PLAY";
        playButton.interactable = true;
        briefing.SetActive(true);
    }

    private void CloseBriefing()
    {
        if (!loading) briefing.SetActive(false);
    }

    private void Play()
    {
        var data = GameProgressionManager.GetStage(selectedStage);
        if (loading || data == null) return;
        if (!Application.CanStreamedLevelBeLoaded(data.sceneName))
        {
            ShowToast($"Сцена «{data.sceneName}» не добавлена в Build Settings");
            return;
        }

        loading = true;
        playButton.interactable = false;
        playLabel.text = "ЗАГРУЗКА...";
        SceneManager.LoadSceneAsync(data.sceneName, LoadSceneMode.Single);
    }

    private void ShowToast(string message)
    {
        toastText.text = message;
        toastHideAt = Time.unscaledTime + toastDuration;
    }
}

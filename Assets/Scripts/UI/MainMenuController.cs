using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
public sealed class MainMenuController : MonoBehaviour
{
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

    [Header("Пролог")]
    [SerializeField] private GameObject intro;
    [SerializeField] private Text introStoryText;
    [SerializeField] private Button introContinueButton;

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
        if (introContinueButton != null) introContinueButton.onClick.AddListener(CloseIntro);
        ShowIntroIfNeeded();
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
        if (loading || (intro != null && intro.activeSelf)) return;
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
        stageText.text = string.IsNullOrEmpty(data.missionName) ? $"МИССИЯ {stage}" :
            $"{stage:D2}  ·  {data.missionName.ToUpperInvariant()}";
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
        if (loading || data == null || !GameProgressionManager.IsStageUnlocked(selectedStage)) return;
        int next = SceneTransition.BuildIndexOf(data.sceneName);
        if (next < 0)
        {
            ShowToast($"Сцена «{data.sceneName}» не добавлена в Build Settings");
            return;
        }

        if (!SceneTransition.TryLoad(next, playLabel.font)) return;
        loading = true;
        playButton.interactable = false;
        playLabel.text = "ЗАГРУЗКА...";
    }

    private void ShowIntroIfNeeded()
    {
        if (intro == null) return;
        var progression = GameProgressionManager.Instance;
        bool show = progression != null && !GameProgressionManager.HasSeenIntroduction &&
            !string.IsNullOrWhiteSpace(progression.Introduction);
        if (show) introStoryText.text = progression.Introduction;
        intro.SetActive(show);
    }

    private void CloseIntro()
    {
        GameProgressionManager.AcknowledgeIntroduction();
        intro.SetActive(false);
    }

    private void ShowToast(string message)
    {
        toast.transform.SetAsLastSibling();
        toastText.text = message;
        toastHideAt = Time.unscaledTime + toastDuration;
    }
}

using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

[DisallowMultipleComponent]
public sealed class TopSettingsMenu : MonoBehaviour
{
    private const string VolumeKey = "RedLine.Settings.Volume";
    private const string ResetLabel = "СБРОСИТЬ ПРОГРЕСС";

    [SerializeField] private Button openButton;
    [SerializeField] private GameObject window;
    [SerializeField] private Text titleText;
    [SerializeField] private Slider volumeSlider;
    [SerializeField] private Text volumeValueText;
    [SerializeField] private Button closeButton;
    [SerializeField] private Button resumeButton;
    [SerializeField] private Text resumeLabel;
    [SerializeField] private Button resetProgressButton;
    [SerializeField] private Text resetProgressLabel;
    [SerializeField] private Button exitButton;
    [SerializeField] private Text exitLabel;

    private bool inMainMenu;
    private bool resetArmed;
    private float resetLabelUntil = float.NegativeInfinity;

    public static bool IsOpen { get; private set; }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() => IsOpen = false;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void ApplySavedVolume() => AudioListener.volume = PlayerPrefs.GetFloat(VolumeKey, 1f);

    private void Awake()
    {
        inMainMenu = SceneManager.GetActiveScene().name == GameProgressionManager.MainMenuScene;

        openButton.onClick.AddListener(() => SetOpen(true));
        closeButton.onClick.AddListener(() => SetOpen(false));
        resumeButton.onClick.AddListener(() => SetOpen(false));
        resetProgressButton.onClick.AddListener(ResetProgress);
        exitButton.onClick.AddListener(Exit);

        volumeSlider.minValue = 0f;
        volumeSlider.maxValue = 1f;
        volumeSlider.SetValueWithoutNotify(PlayerPrefs.GetFloat(VolumeKey, AudioListener.volume));
        volumeSlider.onValueChanged.AddListener(SetVolume);
        SetVolume(volumeSlider.value);

        titleText.text = inMainMenu ? "НАСТРОЙКИ" : "ПАУЗА";
        resumeLabel.text = inMainMenu ? "ЗАКРЫТЬ" : "ПРОДОЛЖИТЬ";
        exitLabel.text = inMainMenu ? "ВЫЙТИ ИЗ ИГРЫ" : "ВЫЙТИ В МЕНЮ";
        resetProgressButton.gameObject.SetActive(inMainMenu);
        resetProgressLabel.text = ResetLabel;
        window.SetActive(false);
    }

    private void Update()
    {
        if (EscapePressed()) SetOpen(!window.activeSelf);
        if (Time.unscaledTime <= resetLabelUntil || resetProgressLabel.text == ResetLabel) return;
        resetProgressLabel.text = ResetLabel;
        resetArmed = false;
    }

    private void OnDestroy()
    {
        if (!window.activeSelf) return;
        IsOpen = false;
        if (!inMainMenu) GamePause.SetPaused(false);
    }

    public void SetOpen(bool open)
    {
        if (window.activeSelf == open) return;
        window.SetActive(open);
        IsOpen = open;
        resetArmed = false;
        resetLabelUntil = float.NegativeInfinity;
        if (!inMainMenu) GamePause.SetPaused(open);
    }

    private void SetVolume(float value)
    {
        AudioListener.volume = value;
        PlayerPrefs.SetFloat(VolumeKey, value);
        volumeValueText.text = Mathf.RoundToInt(value * 100f) + "%";
    }

    private void ResetProgress()
    {
        if (resetArmed && Time.unscaledTime <= resetLabelUntil)
        {
            GameProgressionManager.ResetProgress();
            resetArmed = false;
            resetProgressLabel.text = "ПРОГРЕСС СБРОШЕН";
            resetLabelUntil = Time.unscaledTime + 1.5f;
            return;
        }

        resetArmed = true;
        resetLabelUntil = Time.unscaledTime + 3f;
        resetProgressLabel.text = "ТОЧНО? НАЖМИ ЕЩЁ РАЗ";
    }

    private void Exit()
    {
        PlayerPrefs.Save();
        window.SetActive(false);
        IsOpen = false;
        GamePause.SetPaused(false);
        if (!inMainMenu)
        {
            SceneManager.LoadScene(GameProgressionManager.MainMenuScene);
            return;
        }
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    private static bool EscapePressed()
    {
#if ENABLE_INPUT_SYSTEM
        return (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame) ||
               (Gamepad.current != null && Gamepad.current.startButton.wasPressedThisFrame);
#else
        return Input.GetKeyDown(KeyCode.Escape);
#endif
    }
}

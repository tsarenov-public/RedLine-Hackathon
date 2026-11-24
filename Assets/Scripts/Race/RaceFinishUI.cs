using Arcade.Vehicle;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

[DisallowMultipleComponent]
[RequireComponent(typeof(Canvas), typeof(GraphicRaycaster))]
public sealed class RaceFinishUI : MonoBehaviour
{
    [Header("Победа")]
    [SerializeField] private GameObject[] victoryElements;
    [SerializeField] private Text chapterTitleText;
    [SerializeField] private Text chapterStoryText;
    [SerializeField] private Text carNameText;
    [SerializeField] private Image keyImage;
    [SerializeField] private Button continueButton;

    [Header("Поражение")]
    [SerializeField] private GameObject defeatPanel;
    [SerializeField] private Text defeatTitleText;
    [SerializeField] private Text defeatMessageText;
    [SerializeField] private Button retryButton;
    [SerializeField] private Button menuButton;

    [Header("Камеры")]
    [SerializeField] private Camera gameplayCamera;
    [SerializeField] private FinishShowcaseCamera showcaseCamera;

    private Canvas canvas;
    private GraphicRaycaster raycaster;
    private bool presented;
    private bool leaving;

    public bool IsPresented => presented;

    private void Awake()
    {
        canvas = GetComponent<Canvas>();
        raycaster = GetComponent<GraphicRaycaster>();
        canvas.enabled = raycaster.enabled = false;
        if (defeatPanel != null) defeatPanel.SetActive(false);
        Listen(continueButton, () => Leave(GameProgressionManager.MainMenuScene));
        Listen(retryButton, () => Leave(SceneManager.GetActiveScene().path));
        Listen(menuButton, () => Leave(GameProgressionManager.MainMenuScene));
    }

    public void PresentVictory(ArcadeVehicle car, GameProgressionManager.StageConfig stage)
    {
        if (presented) return;
        presented = true;

        if (car != null && showcaseCamera != null)
        {
            if (gameplayCamera != null)
            {
                gameplayCamera.enabled = false;
                gameplayCamera.gameObject.SetActive(false);
            }
            showcaseCamera.Show(car);
        }

        SetText(chapterTitleText, stage?.chapterTitle);
        SetText(chapterStoryText, stage?.victoryStory);
        SetText(carNameText, stage?.carName);
        if (keyImage != null)
        {
            keyImage.sprite = stage?.keySprite;
            keyImage.enabled = keyImage.sprite != null;
        }

        SetVictoryVisible(true);
        if (defeatPanel != null) defeatPanel.SetActive(false);
        Show();
    }

    public void PresentDefeat(GameProgressionManager.StageConfig stage)
    {
        if (presented) return;
        presented = true;

        SetText(defeatTitleText, "ВЫ ПРИШЛИ ВТОРЫМ!");
        SetText(defeatMessageText, stage == null
            ? "Соперник первым пересёк финишную черту."
            : $"{stage.rivalName} первым пересёк финишную черту.\nКлючи от {stage.carName} остаются у соперника.");

        SetVictoryVisible(false);
        if (defeatPanel != null) defeatPanel.SetActive(true);
        Show();
    }

    private void Show() => canvas.enabled = raycaster.enabled = true;

    private void SetVictoryVisible(bool visible)
    {
        if (victoryElements == null) return;
        foreach (var element in victoryElements)
            if (element != null) element.SetActive(visible);
    }

    private void Leave(string sceneName)
    {
        if (!presented || leaving) return;
        int next = SceneTransition.BuildIndexOf(sceneName);
        if (next < 0)
        {
            Debug.LogError($"Сцена «{sceneName}» недоступна в Build Settings.", this);
            return;
        }

        leaving = SceneTransition.TryLoad(next, chapterTitleText != null ? chapterTitleText.font : null,
            () => leaving = false);
        if (!leaving) return;
        GamePause.SetPaused(false);
    }

    private static void Listen(Button button, UnityEngine.Events.UnityAction action)
    {
        if (button != null) button.onClick.AddListener(action);
    }

    private static void SetText(Text target, string value)
    {
        if (target != null) target.text = value ?? string.Empty;
    }
}

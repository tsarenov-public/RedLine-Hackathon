using System.Collections;
using Arcade.Vehicle;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

[DisallowMultipleComponent]
[RequireComponent(typeof(Canvas), typeof(GraphicRaycaster))]
public sealed class RaceFinishUI : MonoBehaviour
{
    [Header("Chapter")]
    [SerializeField] private string chapterTitle;
    [SerializeField, TextArea(8, 14)] private string chapterStory;
    [SerializeField] private string carName;
    [SerializeField] private Sprite carKey;
    [SerializeField] private string nextScenePath;

    [Header("View")]
    [SerializeField] private Text chapterTitleText;
    [SerializeField] private Text chapterStoryText;
    [SerializeField] private Text carNameText;
    [SerializeField] private Image keyImage;
    [SerializeField] private Button continueButton;
    [SerializeField] private Camera gameplayCamera;
    [SerializeField] private FinishShowcaseCamera showcaseCamera;

    private Canvas canvas;
    private GraphicRaycaster raycaster;
    private bool presented;

    public bool IsPresented => presented;

    private void Awake()
    {
        canvas = GetComponent<Canvas>();
        raycaster = GetComponent<GraphicRaycaster>();
        canvas.enabled = raycaster.enabled = false;
        continueButton.onClick.AddListener(Continue);
    }

    private void OnDestroy()
    {
        if (continueButton != null) continueButton.onClick.RemoveListener(Continue);
    }

    public void Present(ArcadeVehicle car)
    {
        if (presented) return;
        presented = true;

        if (gameplayCamera != null)
        {
            gameplayCamera.enabled = false;
            gameplayCamera.gameObject.SetActive(false);
        }
        showcaseCamera.Show(car);

        chapterTitleText.text = chapterTitle;
        chapterStoryText.text = chapterStory;
        carNameText.text = carName;
        keyImage.sprite = carKey;
        canvas.enabled = raycaster.enabled = true;
    }

    private void Continue()
    {
        if (!presented || !continueButton.interactable) return;
        int nextScene = SceneUtility.GetBuildIndexByScenePath(nextScenePath);
        if (nextScene < 0)
        {
            Debug.LogError($"Finish screen scene '{nextScenePath}' must be enabled in build settings.", this);
            return;
        }

        continueButton.interactable = false;
        StartCoroutine(LoadNextScene(nextScene));
    }

    private IEnumerator LoadNextScene(int nextScene)
    {
        showcaseCamera.gameObject.SetActive(false);
        yield return null;
        SceneManager.LoadSceneAsync(nextScene, LoadSceneMode.Single);
    }
}

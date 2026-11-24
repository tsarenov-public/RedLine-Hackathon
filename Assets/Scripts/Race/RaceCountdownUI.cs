using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
[RequireComponent(typeof(Canvas))]
public sealed class RaceCountdownUI : MonoBehaviour
{
    [SerializeField] private Text countText;
    [SerializeField] private GameObject captionPlate;
    [SerializeField] private Text captionText;
    [SerializeField] private Color countColor = Color.white;
    [SerializeField] private Color accentColor = new(.84f, .95f, .23f);
    [SerializeField, Min(.05f)] private float popDuration = .35f;
    [SerializeField, Min(1f)] private float popScale = 1.6f;

    private Canvas canvas;
    private float popStarted = float.NegativeInfinity;

    private void Awake()
    {
        canvas = GetComponent<Canvas>();
        Hide();
    }

    public void ShowCount(string value, bool accent)
    {
        canvas.enabled = true;
        countText.gameObject.SetActive(true);
        countText.text = value;
        countText.color = accent ? accentColor : countColor;
        popStarted = Time.unscaledTime;
        Animate();
    }

    public void ShowCaption(string value)
    {
        bool visible = !string.IsNullOrEmpty(value);
        if (visible) canvas.enabled = true;
        captionPlate.SetActive(visible);
        captionText.text = value ?? string.Empty;
    }

    public void Hide()
    {
        canvas.enabled = false;
        countText.gameObject.SetActive(false);
        captionPlate.SetActive(false);
    }

    private void Update()
    {
        if (canvas.enabled && countText.gameObject.activeSelf) Animate();
    }

    private void Animate()
    {
        float t = Mathf.Clamp01((Time.unscaledTime - popStarted) / popDuration);
        float eased = 1f - (1f - t) * (1f - t) * (1f - t);
        countText.rectTransform.localScale = Vector3.one * Mathf.Lerp(popScale, 1f, eased);
        var color = countText.color;
        color.a = Mathf.Lerp(.2f, 1f, eased);
        countText.color = color;
    }
}

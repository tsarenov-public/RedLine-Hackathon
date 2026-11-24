using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public enum RaceMarkerState
{
    Locked,
    Current,
    Completed
}

[DisallowMultipleComponent]
public sealed class RaceMapMarker : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    [SerializeField, Min(1)] private int stage = 1;
    [SerializeField] private Button button;
    [SerializeField] private RectTransform body;
    [SerializeField] private Image glow;
    [SerializeField] private Image ring;
    [SerializeField] private Image core;
    [SerializeField] private Text number;
    [SerializeField] private Image lockIcon;
    [SerializeField] private Image checkBadge;
    [SerializeField] private Text label;

    [Header("Цвета")]
    [SerializeField] private Color currentColor = new(.84f, .95f, .23f);
    [SerializeField] private Color completedColor = new(.9f, .92f, .93f);
    [SerializeField] private Color lockedColor = new(.17f, .18f, .19f);
    [SerializeField, Min(.1f)] private float pulseSpeed = 2.4f;

    private RaceMarkerState state;
    private bool hovered;

    public int Stage => stage;
    public event Action<int> Clicked;

    private void Awake() => button.onClick.AddListener(() => Clicked?.Invoke(stage));

    public void SetMapPosition(Vector2 normalized)
    {
        var rect = (RectTransform)transform;
        rect.anchorMin = rect.anchorMax = normalized;
        rect.anchoredPosition = Vector2.zero;
    }

    public void SetState(RaceMarkerState value)
    {
        state = value;
        bool locked = value == RaceMarkerState.Locked;
        core.color = value switch
        {
            RaceMarkerState.Current => currentColor,
            RaceMarkerState.Completed => completedColor,
            _ => lockedColor
        };
        ring.color = locked ? new Color(.45f, .47f, .49f, .9f) : new Color(.05f, .06f, .07f, 1f);
        number.gameObject.SetActive(!locked);
        number.color = new Color(.06f, .07f, .08f);
        lockIcon.gameObject.SetActive(locked);
        checkBadge.gameObject.SetActive(value == RaceMarkerState.Completed);
        glow.gameObject.SetActive(value == RaceMarkerState.Current);
        glow.color = currentColor;
        label.color = locked ? new Color(.62f, .64f, .66f) : Color.white;
    }

    public void OnPointerEnter(PointerEventData eventData) => hovered = true;
    public void OnPointerExit(PointerEventData eventData) => hovered = false;

    private void OnDisable() => hovered = false;

    private void Update()
    {
        float dt = Time.unscaledDeltaTime;
        float target = hovered ? 1.14f : 1f;
        body.localScale = Vector3.one * Mathf.Lerp(body.localScale.x, target, 1f - Mathf.Exp(-dt * 14f));
        if (state != RaceMarkerState.Current) return;

        float wave = .5f + .5f * Mathf.Sin(Time.unscaledTime * pulseSpeed * Mathf.PI);
        glow.rectTransform.localScale = Vector3.one * Mathf.Lerp(1f, 1.75f, wave);
        var color = currentColor;
        color.a = Mathf.Lerp(.75f, .1f, wave);
        glow.color = color;
    }
}

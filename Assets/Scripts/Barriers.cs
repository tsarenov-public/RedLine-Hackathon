using UnityEngine;
using UnityEngine.UI;

[ExecuteAlways]
[DisallowMultipleComponent]
public class Barriers : MonoBehaviour
{
    public enum TurnDirection
    {
        Right,
        Left
    }

    [Header("Направление и размеры")]
    public TurnDirection direction = TurnDirection.Right;
    [Min(1f)] public float width = 18f;
    [Min(0.5f)] public float height = 4f;

    [Header("Визуал и анимация стрелок")]
    public Color arrowColor = new Color(1f, 0.72f, 0.05f, 1f);
    [Range(0f, 0.3f)] public float backgroundAlpha = 0.06f;
    public float scrollSpeed = 1.8f;
    [Min(1f)] public float arrowTiling = 5f;

    [Header("Физика отбойника")]
    public bool solidWall = true;

    private const string LegacyVisualName = "Barrier_Visual";
    private const string CanvasChildName = "Barrier_Canvas";
    private const string ArrowsChildName = "Barrier_Arrows";

    private BoxCollider _boxCollider;
    private RectTransform _canvasRect;
    private RawImage _rawImage;
    private Texture2D _arrowTexture;
    private PhysicsMaterial _slickMaterial;
    private float _uvOffset;
    private bool _dirty = true;
    private TurnDirection _lastDirection;

    private void OnEnable()
    {
        _dirty = true;
        EnsureComponents();
        RebuildVisuals(forceTexture: true);
    }

    private void OnValidate()
    {
        _dirty = true;
    }

    private void Update()
    {
        if (_dirty || _lastDirection != direction)
        {
            bool dirChanged = _lastDirection != direction;
            EnsureComponents();
            RebuildVisuals(forceTexture: dirChanged || _dirty);
        }

        if (_rawImage == null)
            return;

        float dt = Application.isPlaying ? Time.deltaTime : 0.016f;
        float dirSign = direction == TurnDirection.Right ? -1f : 1f;
        _uvOffset = Mathf.Repeat(_uvOffset + dirSign * scrollSpeed * dt, 1f);

        float repeatCount = Mathf.Max(1f, (width / Mathf.Max(0.5f, height)) * (arrowTiling * 0.22f));
        _rawImage.uvRect = new Rect(_uvOffset, 0f, repeatCount, 1f);
    }

    private void EnsureComponents()
    {
        // Удаляем старый 3D Quad, если он остался от прошлой версии скрипта
        Transform legacy = transform.Find(LegacyVisualName);
        if (legacy != null)
        {
            if (Application.isPlaying) Destroy(legacy.gameObject);
            else DestroyImmediate(legacy.gameObject);
        }

        _boxCollider = GetComponent<BoxCollider>();
        if (_boxCollider == null)
            _boxCollider = gameObject.AddComponent<BoxCollider>();

        _boxCollider.enabled = solidWall;
        _boxCollider.isTrigger = false;
        _boxCollider.size = new Vector3(width, height, 0.6f);
        _boxCollider.center = new Vector3(0f, height * 0.5f, 0f);

        if (Application.isPlaying && _boxCollider.sharedMaterial == null)
        {
            _slickMaterial = new PhysicsMaterial("BarrierSlick")
            {
                dynamicFriction = 0.05f,
                staticFriction = 0.05f,
                bounciness = 0.15f,
                frictionCombine = PhysicsMaterialCombine.Minimum,
                bounceCombine = PhysicsMaterialCombine.Average
            };
            _boxCollider.sharedMaterial = _slickMaterial;
        }

        Transform canvasTr = transform.Find(CanvasChildName);
        if (canvasTr == null)
        {
            GameObject canvasGo = new GameObject(CanvasChildName);
            canvasGo.transform.SetParent(transform, false);
            Canvas canvas = canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            canvasTr = canvasGo.transform;
        }

        _canvasRect = canvasTr.GetComponent<RectTransform>();
        if (_canvasRect == null)
            _canvasRect = canvasTr.gameObject.AddComponent<RectTransform>();

        _canvasRect.localPosition = new Vector3(0f, height * 0.5f, 0f);
        _canvasRect.localRotation = Quaternion.identity;
        _canvasRect.sizeDelta = new Vector2(width * 100f, height * 100f);
        _canvasRect.localScale = new Vector3(0.01f, 0.01f, 0.01f);

        Transform arrowsTr = canvasTr.Find(ArrowsChildName);
        if (arrowsTr == null)
        {
            GameObject imgGo = new GameObject(ArrowsChildName);
            imgGo.transform.SetParent(canvasTr, false);
            _rawImage = imgGo.AddComponent<RawImage>();
            arrowsTr = imgGo.transform;
        }
        else
        {
            _rawImage = arrowsTr.GetComponent<RawImage>();
            if (_rawImage == null)
                _rawImage = arrowsTr.gameObject.AddComponent<RawImage>();
        }

        RectTransform imgRect = _rawImage.rectTransform;
        imgRect.anchorMin = Vector2.zero;
        imgRect.anchorMax = Vector2.one;
        imgRect.offsetMin = Vector2.zero;
        imgRect.offsetMax = Vector2.zero;
        _rawImage.raycastTarget = false;
    }

    private void RebuildVisuals(bool forceTexture)
    {
        _dirty = false;
        _lastDirection = direction;

        if (_rawImage == null)
            return;

        if (_arrowTexture == null || forceTexture)
        {
            if (_arrowTexture != null)
            {
                if (Application.isPlaying) Destroy(_arrowTexture);
                else DestroyImmediate(_arrowTexture);
            }
            _arrowTexture = GenerateChevronTexture(256, 128);
        }

        float repeatCount = Mathf.Max(1f, (width / Mathf.Max(0.5f, height)) * (arrowTiling * 0.22f));
        _rawImage.texture = _arrowTexture;
        _rawImage.color = arrowColor;
        _rawImage.uvRect = new Rect(_uvOffset, 0f, repeatCount, 1f);
    }

    private Texture2D GenerateChevronTexture(int texWidth, int texHeight)
    {
        Texture2D tex = new Texture2D(texWidth, texHeight, TextureFormat.RGBA32, false)
        {
            wrapMode = TextureWrapMode.Repeat,
            filterMode = FilterMode.Bilinear,
            hideFlags = HideFlags.DontSave
        };

        Color[] pixels = new Color[texWidth * texHeight];

        for (int y = 0; y < texHeight; y++)
        {
            float v = (y + 0.5f) / texHeight;
            bool isBorder = v < 0.06f || v > 0.94f;
            bool isArrowZone = v >= 0.16f && v <= 0.84f;
            float dy = Mathf.Abs(v - 0.5f) / 0.34f;

            for (int x = 0; x < texWidth; x++)
            {
                float u = (x + 0.5f) / texWidth;
                float evalU = direction == TurnDirection.Right ? u : 1f - u;

                float alpha = backgroundAlpha;

                if (isBorder)
                {
                    alpha = 0.95f;
                }
                else if (isArrowZone)
                {
                    float tipU = 0.72f - dy * 0.38f;
                    float dist = Mathf.Abs(evalU - tipU);
                    float arrowMask = Mathf.Clamp01((0.12f - dist) / 0.025f);
                    alpha = Mathf.Lerp(backgroundAlpha, 1f, arrowMask);
                }

                pixels[y * texWidth + x] = new Color(1f, 1f, 1f, alpha);
            }
        }

        tex.SetPixels(pixels);
        tex.Apply();
        return tex;
    }

    private void OnDestroy()
    {
        if (_slickMaterial != null)
        {
            if (Application.isPlaying) Destroy(_slickMaterial);
            else DestroyImmediate(_slickMaterial);
        }
        if (_arrowTexture != null)
        {
            if (Application.isPlaying) Destroy(_arrowTexture);
            else DestroyImmediate(_arrowTexture);
        }
    }
}

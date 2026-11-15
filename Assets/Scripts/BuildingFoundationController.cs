using System.Collections.Generic;
using UnityEngine;

/// <summary>Controls only the vertical extent of the builder's ordinary, editable cubes.</summary>
[ExecuteAlways, DisallowMultipleComponent]
public sealed class BuildingFoundationController : MonoBehaviour
{
    public const float FoundationTop = 0.05f;
    public const float ColliderTop = 5f;

    [Range(0f, 20f)] public float foundationDepth = 6.0f;

    [SerializeField, HideInInspector] private Transform[] foundations = new Transform[0];
    [SerializeField, HideInInspector] private Transform[] shadowProxies = new Transform[0];
    [SerializeField, HideInInspector] private BoxCollider[] wallColliders = new BoxCollider[0];
    [SerializeField, HideInInspector] private float appliedDepth = -1f;
    private bool pending;

    public void BindGeneratedObjects(Transform[] cubes, Transform[] shadows, BoxCollider[] colliders)
    {
        foundations = cubes;
        shadowProxies = shadows;
        wallColliders = colliders;
        appliedDepth = -1f;
        ApplyDepth();
    }

    private void OnEnable() => pending = true;

    // OnValidate can run on Unity's loading thread. Actual object changes happen in Update
    // or explicitly from the custom Inspector, on the main thread.
    private void OnValidate()
    {
        foundationDepth = float.IsNaN(foundationDepth) || float.IsInfinity(foundationDepth)
            ? 6f : Mathf.Clamp(foundationDepth, 0f, 20f);
        pending = true;
    }

    private void Update()
    {
        if (pending || !Mathf.Approximately(appliedDepth, foundationDepth)) ApplyDepth();
    }

    public IEnumerable<Object> GetDrivenObjects()
    {
        foreach (var cube in foundations) if (cube) yield return cube;
        foreach (var proxy in shadowProxies) if (proxy) yield return proxy;
        foreach (var box in wallColliders) if (box) yield return box;
    }

    public void ApplyDepth()
    {
        pending = false;
#if UNITY_EDITOR
        // Project assets are updated by SaveAsPrefabAsset, never by an editor repaint.
        if (UnityEditor.EditorUtility.IsPersistent(this)) return;
#endif
        foundationDepth = float.IsNaN(foundationDepth) || float.IsInfinity(foundationDepth)
            ? 6f : Mathf.Clamp(foundationDepth, 0f, 20f);
        if (Mathf.Approximately(appliedDepth, foundationDepth)) return;

        foreach (var cube in foundations)
            if (cube) SetVerticalExtent(cube, -foundationDepth, FoundationTop);

        foreach (var proxy in shadowProxies)
        {
            if (!proxy) continue;
            // Preserve the roof limit, including manual edits, while moving only the bottom.
            float top = proxy.localPosition.y + proxy.localScale.y * 0.5f;
            SetVerticalExtent(proxy, -foundationDepth, top);
        }

        foreach (var box in wallColliders)
        {
            if (!box) continue;
            Vector3 center = box.center, size = box.size;
            center.y = (ColliderTop - foundationDepth) * 0.5f;
            size.y = ColliderTop + foundationDepth;
            box.center = center;
            box.size = size;
            MarkChanged(box);
        }
        appliedDepth = foundationDepth;
        MarkChanged(this);
    }

    private static void SetVerticalExtent(Transform target, float bottom, float top)
    {
        Vector3 position = target.localPosition, scale = target.localScale;
        position.y = (top + bottom) * 0.5f;
        scale.y = Mathf.Max(0f, top - bottom);
        target.localPosition = position;
        target.localScale = scale;
        MarkChanged(target);
    }

    private static void MarkChanged(Object target)
    {
#if UNITY_EDITOR
        if (Application.isPlaying) return;
        UnityEditor.EditorUtility.SetDirty(target);
        if (UnityEditor.PrefabUtility.IsPartOfPrefabInstance(target))
            UnityEditor.PrefabUtility.RecordPrefabInstancePropertyModifications(target);
#endif
    }
}

using System;
using UnityEngine;
using UnityEngine.Rendering;

// Skidmarks controller. Call AddSkidMark with the wheel's ground contact.
// Copyright 2017 Nition, BSD licence (see LICENCE file). http://nition.co
[RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
public class Skidmarks : MonoBehaviour
{
    [SerializeField] private Material skidmarksMaterial;

    [Header("Road surface projection")]
    [SerializeField] private Collider roadSurface;
    [SerializeField, Min(0f)] private float surfaceOffset = 0.018f;
    [SerializeField, Min(0.1f)] private float projectionStartHeight = 0.75f;
    [SerializeField, Min(0.1f)] private float projectionDistance = 1.5f;

    private const int MaxMarks = 2048;
    private const float HalfWidth = 0.175f;
    private const float MinDistanceSquared = 0.25f * 0.25f;
    private const float MaxGapSquared = 2.5f * 2.5f;
    private static readonly int OpacityGainId = Shader.PropertyToID("_OpacityGain");

    private struct MarkSection
    {
        public Vector3 Position;
        public Vector3 Normal;
        public Vector3 Left;
        public Vector3 Right;
        public Vector4 Tangent;
        public Color32 Color;
        public bool StartsStrip;
    }

    private readonly MarkSection[] sections = new MarkSection[MaxMarks];
    private readonly Vector3[] vertices = new Vector3[MaxMarks * 4];
    private readonly Vector3[] normals = new Vector3[MaxMarks * 4];
    private readonly Vector4[] tangents = new Vector4[MaxMarks * 4];
    private readonly Color32[] colors = new Color32[MaxMarks * 4];
    private readonly Vector2[] uvs = new Vector2[MaxMarks * 4];
    private readonly int[] triangles = new int[MaxMarks * 6];

    private int nextIndex;
    private Mesh marksMesh;
    private Material runtimeMaterial;
    private bool meshUpdated;

    private void Awake()
    {
        // Mark vertices are world-space even when the prefab starts as a child.
        if (transform.parent != null) transform.SetParent(null, true);
        transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
        transform.localScale = Vector3.one;

        marksMesh = new Mesh { name = "Runtime Skidmarks", hideFlags = HideFlags.DontSave };
        marksMesh.MarkDynamic();
        GetComponent<MeshFilter>().sharedMesh = marksMesh;

        var renderer = GetComponent<MeshRenderer>();
        renderer.shadowCastingMode = ShadowCastingMode.Off;
        renderer.receiveShadows = false;
        renderer.sortingOrder = 10;
        renderer.lightProbeUsage = LightProbeUsage.Off;
        if (skidmarksMaterial != null)
        {
            runtimeMaterial = new Material(skidmarksMaterial)
            {
                name = skidmarksMaterial.name + " (Runtime Skidmarks)",
                hideFlags = HideFlags.DontSave,
                renderQueue = 3100
            };
            renderer.sharedMaterial = runtimeMaterial;
        }
    }

    private void LateUpdate()
    {
        if (!meshUpdated) return;
        meshUpdated = false;
        marksMesh.SetVertices(vertices);
        marksMesh.SetNormals(normals);
        marksMesh.SetTangents(tangents);
        marksMesh.SetColors(colors);
        marksMesh.SetUVs(0, uvs);
        marksMesh.SetTriangles(triangles, 0, false);
        marksMesh.bounds = new Bounds(Vector3.zero, Vector3.one * 10000f);
    }

    private void OnDestroy()
    {
        if (marksMesh != null) Destroy(marksMesh);
        if (runtimeMaterial != null) Destroy(runtimeMaterial);
    }

    public void BindRoadSurface(Collider surface)
    {
        roadSurface = surface;
    }

    public void SetOpacityGain(float gain)
    {
        if (runtimeMaterial != null && runtimeMaterial.HasProperty(OpacityGainId))
            runtimeMaterial.SetFloat(OpacityGainId, Mathf.Clamp(gain, .2f, 3f));
    }

    public int AddSkidMark(Vector3 position, Vector3 normal, float opacity, int lastIndex)
    {
        var color = new Color32(0, 0, 0, (byte)Mathf.RoundToInt(Mathf.Clamp01(opacity) * 255f));
        return AddSkidMark(position, normal, color, lastIndex);
    }

    public int AddSkidMark(Vector3 position, Vector3 normal, Color32 color, int lastIndex)
    {
        if (color.a == 0 || marksMesh == null || !isActiveAndEnabled) return -1;

        ProjectOntoRoad(ref position, ref normal);
        position += normal * surfaceOffset;
        int slot = nextIndex % MaxMarks;
        var section = new MarkSection { Position = position, Normal = normal, Color = color, StartsStrip = true };

        // Track sequence numbers so another wheel cannot connect to an overwritten slot.
        if (lastIndex >= Mathf.Max(0, nextIndex - MaxMarks + 1) && lastIndex < nextIndex)
        {
            var previous = sections[lastIndex % MaxMarks];
            Vector3 delta = position - previous.Position;
            float distanceSquared = delta.sqrMagnitude;
            if (distanceSquared < MinDistanceSquared) return lastIndex;
            if (distanceSquared <= MaxGapSquared)
            {
                Vector3 width = Vector3.Cross(delta, normal).normalized;
                section.StartsStrip = false;
                SetEdges(ref section, width);
                if (previous.StartsStrip) SetEdges(ref previous, width);
                WriteSegment(slot, previous, section);
            }
        }

        if (section.StartsStrip && triangles[slot * 6 + 1] != 0)
        {
            Array.Clear(triangles, slot * 6, 6);
            meshUpdated = true;
        }

        sections[slot] = section;
        return nextIndex++;
    }

    private void ProjectOntoRoad(ref Vector3 position, ref Vector3 normal)
    {
        if (roadSurface == null) return;
        var ray = new Ray(position + Vector3.up * projectionStartHeight, Vector3.down);
        if (!roadSurface.Raycast(ray, out var hit, Mathf.Max(projectionDistance, projectionStartHeight))) return;
        position = hit.point;
        normal = hit.normal;
    }

    private static void SetEdges(ref MarkSection section, Vector3 width)
    {
        section.Left = section.Position + width * HalfWidth;
        section.Right = section.Position - width * HalfWidth;
        section.Tangent = new Vector4(width.x, width.y, width.z, 1f);
    }

    private void WriteSegment(int slot, MarkSection previous, MarkSection current)
    {
        int vertex = slot * 4;
        WriteVertex(vertex, previous.Left, previous, new Vector2(0, 0));
        WriteVertex(vertex + 1, previous.Right, previous, new Vector2(1, 0));
        WriteVertex(vertex + 2, current.Left, current, new Vector2(0, 1));
        WriteVertex(vertex + 3, current.Right, current, new Vector2(1, 1));

        int index = slot * 6;
        triangles[index] = vertex;
        triangles[index + 1] = vertex + 2;
        triangles[index + 2] = vertex + 1;
        triangles[index + 3] = vertex + 2;
        triangles[index + 4] = vertex + 3;
        triangles[index + 5] = vertex + 1;
        meshUpdated = true;
    }

    private void WriteVertex(int index, Vector3 position, MarkSection section, Vector2 uv)
    {
        vertices[index] = position;
        normals[index] = section.Normal;
        tangents[index] = section.Tangent;
        colors[index] = section.Color;
        uvs[index] = uv;
    }
}

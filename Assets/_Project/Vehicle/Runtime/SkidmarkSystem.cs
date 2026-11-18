using UnityEngine;
using UnityEngine.Rendering;

namespace Arcade.Vehicle
{
    [RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
    public sealed class SkidmarkSystem : MonoBehaviour
    {
        [Header("Свой материал: URP/Particles/Unlit, Transparent, Vertex Color")]
        public Material markMaterial;
        public int maxSegments = 1500;
        public float width = 0.24f;
        public float lifetime = 12f;
        public float minDistance = 0.12f;
        public float maxGap = 2.0f;
        [Range(0f, 1f)] public float minIntensity = 0.28f;
        public float groundOffset = 0.02f;
        public float uvTiling = 0.5f;

        Mesh _mesh;
        Vector3[] _v; Color[] _col; Vector2[] _uv; int[] _tri;
        float[] _birth, _alpha;
        int _head;
        bool _dirty;
        float _fadeTimer;

        struct Edge { public Vector3 l, r, mid; public float v; public bool valid; }
        Edge[] _last;

        public void Init(int wheelCount) { _last = new Edge[wheelCount]; }

        void Awake()
        {
            _v = new Vector3[maxSegments * 4];
            _col = new Color[maxSegments * 4];
            _uv = new Vector2[maxSegments * 4];
            _tri = new int[maxSegments * 6];
            _birth = new float[maxSegments];
            _alpha = new float[maxSegments];

            for (int i = 0; i < maxSegments; i++)
            {
                int b = i * 4, t = i * 6;
                _tri[t] = b; _tri[t + 1] = b + 1; _tri[t + 2] = b + 2;
                _tri[t + 3] = b; _tri[t + 4] = b + 2; _tri[t + 5] = b + 3;
            }

            _mesh = new Mesh { name = "Skidmarks", bounds = new Bounds(Vector3.zero, Vector3.one * 8000f) };
            _mesh.MarkDynamic();
            _mesh.SetVertices(_v); _mesh.SetColors(_col); _mesh.SetUVs(0, _uv);
            _mesh.SetTriangles(_tri, 0);
            GetComponent<MeshFilter>().sharedMesh = _mesh;

            var mr = GetComponent<MeshRenderer>();
            mr.sharedMaterial = markMaterial;
            mr.shadowCastingMode = ShadowCastingMode.Off;
            mr.receiveShadows = false;
        }

        public void AddMark(int wheel, Vector3 pos, Vector3 normal, Vector3 right, float intensity)
        {
            if (_last == null || wheel >= _last.Length) return;
            if (intensity < minIntensity) { _last[wheel].valid = false; return; }

            Vector3 p = pos + normal * groundOffset;
            Vector3 side = right.normalized * (width * 0.5f);
            var e = _last[wheel];

            if (e.valid)
            {
                float d = Vector3.Distance(p, e.mid);
                if (d < minDistance) return;
                if (d < maxGap) Write(e.l, e.r, p - side, p + side, intensity, e.v + d * uvTiling);
                _last[wheel] = new Edge { l = p - side, r = p + side, mid = p, v = e.v + d * uvTiling, valid = true };
            }
            else _last[wheel] = new Edge { l = p - side, r = p + side, mid = p, v = 0f, valid = true };
        }

        public void BreakStrip(int wheel) { if (_last != null && wheel < _last.Length) _last[wheel].valid = false; }

        void Write(Vector3 pl, Vector3 pr, Vector3 cl, Vector3 cr, float alpha, float v)
        {
            int i = _head, b = i * 4;
            _v[b] = pl; _v[b + 1] = cl; _v[b + 2] = cr; _v[b + 3] = pr;
            _uv[b] = new Vector2(0, v - uvTiling); _uv[b + 1] = new Vector2(0, v);
            _uv[b + 2] = new Vector2(1, v); _uv[b + 3] = new Vector2(1, v - uvTiling);
            var col = new Color(1f, 1f, 1f, alpha);
            for (int k = 0; k < 4; k++) _col[b + k] = col;
            _birth[i] = Time.time; _alpha[i] = alpha;
            _head = (_head + 1) % maxSegments;
            _dirty = true;
        }

        void Update()
        {
            _fadeTimer += Time.deltaTime;
            bool fade = _fadeTimer > 0.08f;
            if (fade) _fadeTimer = 0f;

            if (fade)
            {
                for (int i = 0; i < maxSegments; i++)
                {
                    if (_alpha[i] <= 0f) continue;
                    float a = _alpha[i] * (1f - (Time.time - _birth[i]) / lifetime);
                    int b = i * 4;
                    if (a <= 0f)
                    {
                        _alpha[i] = 0f;
                        for (int k = 0; k < 4; k++) { _v[b + k] = Vector3.zero; _col[b + k] = Color.clear; }
                    }
                    else
                    {
                        var col = new Color(1f, 1f, 1f, a);
                        for (int k = 0; k < 4; k++) _col[b + k] = col;
                    }
                    _dirty = true;
                }
            }

            if (!_dirty) return;
            _mesh.SetVertices(_v);
            _mesh.SetColors(_col);
            _mesh.SetUVs(0, _uv);
            _dirty = false;
        }
    }
}
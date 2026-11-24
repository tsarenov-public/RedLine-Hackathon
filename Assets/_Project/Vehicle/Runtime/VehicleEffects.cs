using System.Collections.Generic;
using UnityEngine;

namespace Arcade.Vehicle
{
    public sealed class VehicleEffects : MonoBehaviour
    {
        const float SkidmarkSlipThreshold = 0.28f;

        [Header("Ссылки (можно оставить пустыми — найдётся само)")]
        public ArcadeVehicle vehicle;
        public ParticleSystem smokePrefab;
        public ParticleSystem sparksPrefab;
        public SkidmarkSystem skidmarks;
        public Skidmarks skidmarksPrefab;
        public ParticleSystem[] nosFlames;
        public Light[] brakeLights;

        [Header("Интенсивность")]
        public float smokeMaxRate = 70f;
        [Range(.2f, 3f), Tooltip("Яркость, прозрачность, размер и плотность дыма.")]
        public float smokeBrightness = 1.6f;
        [Range(.2f, 3f), Tooltip("Контраст следов шин. Порог пробуксовки сохраняется.")]
        public float skidMarkBrightness = 1.5f;
        public float sparksMinImpulse = 4000f;
        [Min(0f)] public float nosFlameRate = 48f;

        ParticleSystem[] _smoke;
        readonly List<SmokeStyle> _smokeStyles = new();
        Skidmarks _wheelMarks;
        int[] _lastWheelMark;
        bool _ready;
        float _appliedSmoke, _appliedMarks, _smokeGain = 1f;

        void OnValidate()
        {
            smokeBrightness = Mathf.Clamp(smokeBrightness, .2f, 3f);
            skidMarkBrightness = Mathf.Clamp(skidMarkBrightness, .2f, 3f);
        }

        void Start()
        {
            if (vehicle == null) vehicle = GetComponentInParent<ArcadeVehicle>();
            if (vehicle == null) vehicle = Object.FindFirstObjectByType<ArcadeVehicle>();
            if (vehicle == null)
            {
                Debug.LogWarning("[VehicleEffects] Машина не найдена — эффекты выключены.", this);
                return;
            }

            var wheels = vehicle.Ctx?.wheels;
            if (wheels == null) return;
            if (skidmarksPrefab != null)
            {
                _wheelMarks = Instantiate(skidmarksPrefab);
                _lastWheelMark = new int[wheels.Length];
                System.Array.Fill(_lastWheelMark, -1);
            }
            else if (skidmarks == null)
            {
                skidmarks = Object.FindFirstObjectByType<SkidmarkSystem>();
            }

            if (smokePrefab != null)
            {
                _smoke = new ParticleSystem[wheels.Length];
                for (int i = 0; i < wheels.Length; i++)
                {
                    _smoke[i] = Instantiate(smokePrefab, transform);
                    foreach (var particles in _smoke[i].GetComponentsInChildren<ParticleSystem>(true))
                        _smokeStyles.Add(new SmokeStyle(particles));
                    var em = _smoke[i].emission;
                    em.rateOverTime = 0f;
                    _smoke[i].Play();
                }
            }

            if (_wheelMarks == null && skidmarks != null) skidmarks.Init(wheels.Length);
            if (nosFlames != null)
                foreach (var flame in nosFlames)
                    if (flame != null)
                    {
                        SetRate(flame, 0f);
                        flame.Play();
                    }
            vehicle.WallHit += OnWallHit;
            _ready = true;
            ApplyVisualTuning();
        }

        void OnDisable()
        {
            if (_lastWheelMark != null) System.Array.Fill(_lastWheelMark, -1);
            if (_smoke != null)
                foreach (var smoke in _smoke)
                    if (smoke != null) SetRate(smoke, 0f);
            UpdateBodyEffects(0f, false);
        }

        void OnDestroy()
        {
            if (vehicle != null) vehicle.WallHit -= OnWallHit;
            if (_wheelMarks != null) Destroy(_wheelMarks.gameObject);
        }

        void Update()
        {
            if (!_ready || vehicle == null || vehicle.Ctx == null) return;
            ApplyVisualTuning();
            UpdateBodyEffects(vehicle.State.nosActive, vehicle.State.brake > 0.08f && !vehicle.State.reversing);
            var wheels = vehicle.Wheels;
            if (wheels == null) return;

            for (int i = 0; i < wheels.Length; i++)
            {
                var w = wheels[i];
                float slip = w.grounded ? w.slipIntensity : 0f;

                if (_smoke != null && i < _smoke.Length && _smoke[i] != null)
                {
                    _smoke[i].transform.SetPositionAndRotation(
                        w.contactPoint + w.contactNormal * 0.06f,
                        Quaternion.LookRotation(Vector3.up));
                    SetRate(_smoke[i], slip * smokeMaxRate * _smokeGain);
                }

                if (_wheelMarks != null)
                {
                    _lastWheelMark[i] = slip >= SkidmarkSlipThreshold
                        ? _wheelMarks.AddSkidMark(w.contactPoint, w.contactNormal, slip, _lastWheelMark[i])
                        : -1;
                }
                else if (skidmarks != null)
                {
                    if (w.grounded)
                        skidmarks.AddMark(i, w.contactPoint, w.contactNormal, w.right, w.slipIntensity, _appliedMarks);
                    else skidmarks.BreakStrip(i);
                }
            }
        }

        void ApplyVisualTuning()
        {
            float smoke = Mathf.Clamp(smokeBrightness, .2f, 3f);
            if (!Mathf.Approximately(smoke, _appliedSmoke))
            {
                _appliedSmoke = smoke;
                _smokeGain = Mathf.Sqrt(smoke);
                foreach (var style in _smokeStyles) style.Apply(smoke, _smokeGain);
            }

            float marks = Mathf.Clamp(skidMarkBrightness, .2f, 3f);
            if (!Mathf.Approximately(marks, _appliedMarks))
            {
                _appliedMarks = marks;
                if (_wheelMarks != null) _wheelMarks.SetOpacityGain(marks);
            }
        }

        private readonly struct SmokeStyle
        {
            private readonly ParticleSystem particles;
            private readonly ParticleSystem.MinMaxGradient colors;
            private readonly ParticleSystem.MinMaxCurve size, sizeY, sizeZ;
            private readonly bool size3D;

            public SmokeStyle(ParticleSystem particles)
            {
                this.particles = particles;
                var main = particles.main;
                colors = main.startColor;
                size3D = main.startSize3D;
                size = main.startSizeX;
                sizeY = main.startSizeY;
                sizeZ = main.startSizeZ;
            }

            public void Apply(float brightness, float sizeGain)
            {
                if (particles == null) return;
                var main = particles.main;
                main.startColor = ScaleGradient(colors, sizeGain, brightness);
                main.startSizeX = ScaleSize(size, sizeGain);
                if (size3D)
                {
                    main.startSizeY = ScaleSize(sizeY, sizeGain);
                    main.startSizeZ = ScaleSize(sizeZ, sizeGain);
                }
            }
        }

        private static ParticleSystem.MinMaxCurve ScaleSize(ParticleSystem.MinMaxCurve size, float gain)
        {
            if (size.mode is ParticleSystemCurveMode.Constant or ParticleSystemCurveMode.TwoConstants)
            {
                size.constantMin *= gain;
                size.constantMax *= gain;
            }
            else size.curveMultiplier *= gain;
            return size;
        }

        private static ParticleSystem.MinMaxGradient ScaleGradient(ParticleSystem.MinMaxGradient colors,
            float rgb, float alpha)
        {
            switch (colors.mode)
            {
                case ParticleSystemGradientMode.Color:
                    colors.color = ScaleColor(colors.color, rgb, alpha);
                    break;
                case ParticleSystemGradientMode.TwoColors:
                    colors.colorMin = ScaleColor(colors.colorMin, rgb, alpha);
                    colors.colorMax = ScaleColor(colors.colorMax, rgb, alpha);
                    break;
                case ParticleSystemGradientMode.TwoGradients:
                    colors.gradientMin = ScaleKeys(colors.gradientMin, rgb, alpha);
                    goto case ParticleSystemGradientMode.Gradient;
                case ParticleSystemGradientMode.Gradient:
                case ParticleSystemGradientMode.RandomColor:
                    colors.gradientMax = ScaleKeys(colors.gradientMax, rgb, alpha);
                    break;
            }
            return colors;
        }

        private static Gradient ScaleKeys(Gradient source, float rgb, float alpha)
        {
            if (source == null) return null;
            var colors = source.colorKeys;
            var opacity = source.alphaKeys;
            for (int i = 0; i < colors.Length; i++)
                colors[i].color = ScaleColor(colors[i].color, rgb, 1f);
            for (int i = 0; i < opacity.Length; i++) opacity[i].alpha = Mathf.Clamp01(opacity[i].alpha * alpha);
            var result = new Gradient { mode = source.mode, colorSpace = source.colorSpace };
            result.SetKeys(colors, opacity);
            return result;
        }

        private static Color ScaleColor(Color color, float rgb, float alpha) =>
            new(Mathf.Clamp01(color.r * rgb), Mathf.Clamp01(color.g * rgb), Mathf.Clamp01(color.b * rgb),
                Mathf.Clamp01(color.a * alpha));

        void UpdateBodyEffects(float nos, bool brake)
        {
            if (nosFlames != null)
                foreach (var flame in nosFlames)
                    if (flame != null) SetRate(flame, nos * nosFlameRate);
            if (brakeLights != null)
                foreach (var lamp in brakeLights)
                    if (lamp != null) lamp.enabled = brake;
        }

        static void SetRate(ParticleSystem particles, float rate)
        {
            var emission = particles.emission;
            emission.rateOverTime = rate;
        }

        void OnWallHit(Vector3 point, Vector3 normal, float impulse)
        {
            if (sparksPrefab == null || impulse < sparksMinImpulse) return;
            var ps = Instantiate(sparksPrefab, point + normal * 0.05f, Quaternion.LookRotation(normal));
            var main = ps.main;
            main.startSpeedMultiplier = Mathf.Clamp(impulse / sparksMinImpulse, 1f, 4f);
            ps.Play();
            Destroy(ps.gameObject, 2f);
        }
    }
}

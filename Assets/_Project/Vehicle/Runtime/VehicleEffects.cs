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
        public float sparksMinImpulse = 4000f;
        [Min(0f)] public float nosFlameRate = 48f;

        ParticleSystem[] _smoke;
        Skidmarks _wheelMarks;
        int[] _lastWheelMark;
        bool _ready;

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
            if (!_ready || vehicle == null) return;
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
                    SetRate(_smoke[i], slip * smokeMaxRate);
                }

                if (_wheelMarks != null)
                {
                    _lastWheelMark[i] = slip >= SkidmarkSlipThreshold
                        ? _wheelMarks.AddSkidMark(w.contactPoint, w.contactNormal, slip, _lastWheelMark[i])
                        : -1;
                }
                else if (skidmarks != null)
                {
                    if (w.grounded) skidmarks.AddMark(i, w.contactPoint, w.contactNormal, w.right, w.slipIntensity);
                    else skidmarks.BreakStrip(i);
                }
            }
        }

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

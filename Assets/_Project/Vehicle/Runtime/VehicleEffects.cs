using UnityEngine;

namespace Arcade.Vehicle
{
    public sealed class VehicleEffects : MonoBehaviour
    {
        [Header("Ссылки (можно оставить пустыми — найдётся само)")]
        public ArcadeVehicle vehicle;
        public ParticleSystem smokePrefab;
        public ParticleSystem sparksPrefab;
        public SkidmarkSystem skidmarks;

        [Header("Интенсивность")]
        public float smokeMaxRate = 70f;
        public float sparksMinImpulse = 4000f;

        ParticleSystem[] _smoke;
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

            if (skidmarks == null) skidmarks = Object.FindFirstObjectByType<SkidmarkSystem>();

            var wheels = vehicle.Wheels;
            if (smokePrefab != null && wheels != null)
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

            if (skidmarks != null && wheels != null) skidmarks.Init(wheels.Length);
            vehicle.WallHit += OnWallHit;
            _ready = true;
        }

        void OnDestroy()
        {
            if (vehicle != null) vehicle.WallHit -= OnWallHit;
        }

        void Update()
        {
            if (!_ready || vehicle == null) return;
            var wheels = vehicle.Wheels;
            if (wheels == null) return;

            for (int i = 0; i < wheels.Length; i++)
            {
                var w = wheels[i];

                if (_smoke != null && i < _smoke.Length && _smoke[i] != null)
                {
                    float slip = w.grounded ? w.slipIntensity : 0f;
                    _smoke[i].transform.SetPositionAndRotation(
                        w.contactPoint + w.contactNormal * 0.06f,
                        Quaternion.LookRotation(Vector3.up));
                    var em = _smoke[i].emission;
                    em.rateOverTime = slip * smokeMaxRate;
                }

                if (skidmarks != null)
                {
                    if (w.grounded) skidmarks.AddMark(i, w.contactPoint, w.contactNormal, w.right, w.slipIntensity);
                    else skidmarks.BreakStrip(i);
                }
            }
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
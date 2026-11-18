using UnityEngine;

namespace Arcade.Vehicle
{
    public sealed class VehicleEffects : MonoBehaviour
    {
        public ArcadeVehicle vehicle;
        public ParticleSystem smokePrefab;      // Simulation Space = World!
        public ParticleSystem sparksPrefab;
        public SkidmarkSystem skidmarks;
        public float smokeMaxRate = 70f;
        public float sparksMinImpulse = 4000f;

        ParticleSystem[] _smoke;

        void Start()
        {
            var wheels = vehicle.Wheels;
            _smoke = new ParticleSystem[wheels.Length];
            for (int i = 0; i < wheels.Length; i++)
            {
                if (smokePrefab == null) break;
                _smoke[i] = Instantiate(smokePrefab, transform);
                var em = _smoke[i].emission; em.rateOverTime = 0f;
                _smoke[i].Play();
            }
            if (skidmarks != null) skidmarks.Init(wheels.Length);
            vehicle.WallHit += OnWallHit;
        }

        void OnDestroy() { if (vehicle != null) vehicle.WallHit -= OnWallHit; }

        void Update()
        {
            var wheels = vehicle.Wheels;
            for (int i = 0; i < wheels.Length; i++)
            {
                var w = wheels[i];
                float slip = w.grounded ? w.slipIntensity : 0f;

                if (_smoke != null && _smoke[i] != null)
                {
                    _smoke[i].transform.SetPositionAndRotation(
                        w.contactPoint + w.contactNormal * 0.06f,
                        Quaternion.LookRotation(Vector3.up));
                    var em = _smoke[i].emission;
                    em.rateOverTime = slip * smokeMaxRate;
                }

                if (skidmarks != null)
                {
                    if (w.grounded) skidmarks.AddMark(i, w.contactPoint, w.contactNormal, w.right, slip);
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
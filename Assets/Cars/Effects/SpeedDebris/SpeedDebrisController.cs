using UnityEngine;

namespace Arcade.Vehicle
{
    [DefaultExecutionOrder(80)]
    public sealed class SpeedDebrisController : MonoBehaviour
    {
        [SerializeField] private ArcadeVehicle vehicle;
        [SerializeField] private ParticleSystem left;
        [SerializeField] private ParticleSystem right;
        [Min(0f)] public float startKmh = 100f;
        [Min(0f)] public float fullKmh = 230f;
        [Min(0f)] public float maximumRate = 30f;
        [Range(1f, 2f)] public float nitroMultiplier = 1.6f;
        [Range(3f, 7.5f), Tooltip("Lateral distance from the camera's road heading, in metres.")]
        public float sideDistance = 7.4f;
        [Range(12f, 24f), Tooltip("Distance ahead of the camera along the road, in metres.")]
        public float forwardDistance = 12.5f;

        private void Awake()
        {
            if (vehicle == null) vehicle = GetComponentInParent<ArcadeVehicle>();
        }

        private void OnEnable()
        {
            SetRate(left, 0f);
            SetRate(right, 0f);
            if (left != null) left.Play(false);
            if (right != null) right.Play(false);
        }

        private void OnDisable()
        {
            if (left != null) left.Stop(false, ParticleSystemStopBehavior.StopEmittingAndClear);
            if (right != null) right.Stop(false, ParticleSystemStopBehavior.StopEmittingAndClear);
        }

        private void LateUpdate()
        {
            float rate = 0f;
            if (vehicle != null && vehicle.isActiveAndEnabled && vehicle.Ctx != null)
            {
                var state = vehicle.State;
                var camera = Camera.main;
                if (!state.reversing && state.speedKmh > startKmh && FollowsVehicle(camera))
                {
                    var forward = Vector3.ProjectOnPlane(camera.transform.forward, Vector3.up).normalized;
                    if (forward.sqrMagnitude < .01f)
                        forward = Vector3.ProjectOnPlane(vehicle.transform.forward, Vector3.up).normalized;
                    var side = Vector3.Cross(Vector3.up, forward);
                    var center = camera.transform.position + forward * forwardDistance - Vector3.up * .27f;
                    AimEmitter(left, center, forward, -side);
                    AimEmitter(right, center, forward, side);
                    float speed = Mathf.InverseLerp(startKmh, fullKmh, state.speedKmh);
                    rate = .5f * maximumRate * speed * Mathf.Lerp(1f, nitroMultiplier, Mathf.Clamp01(state.nosActive));
                }
            }
            SetRate(left, rate);
            SetRate(right, rate);
        }

        private bool FollowsVehicle(Camera camera)
        {
            if (camera == null) return false;
            if (camera.TryGetComponent<NFSCameraController>(out var follow) && follow.isActiveAndEnabled)
                return follow.Target == vehicle;
            if (camera.TryGetComponent<VehicleChaseCamera>(out var legacy) && legacy.isActiveAndEnabled)
                return legacy.vehicle == vehicle;
            return VehicleSpawner.Current == vehicle;
        }

        private void AimEmitter(ParticleSystem particles, Vector3 center, Vector3 forward, Vector3 outward)
        {
            if (particles == null) return;
            var direction = -forward + outward * .12f + Vector3.up * .022f;
            particles.transform.SetPositionAndRotation(center + outward * sideDistance,
                Quaternion.LookRotation(direction, Vector3.up));
        }

        private static void SetRate(ParticleSystem particles, float rate)
        {
            if (particles == null) return;
            var emission = particles.emission;
            emission.rateOverTime = rate;
        }
    }
}

using UnityEngine;

namespace Arcade.Vehicle
{
    [RequireComponent(typeof(ParticleSystem))]
    public sealed class SpeedDebrisController : MonoBehaviour
    {
        [SerializeField] private ArcadeVehicle vehicle;
        [Min(0f)] public float startKmh = 100f;
        [Min(0f)] public float fullKmh = 230f;
        [Min(0f)] public float maximumRate = 20f;
        [Range(1f, 2f)] public float nitroMultiplier = 1.6f;
        [Min(0f)] public float groundClearance = .15f;

        private ParticleSystem paper;
        private Vector3 bumperOffset;

        private void Awake()
        {
            paper = GetComponent<ParticleSystem>();
            if (vehicle == null) vehicle = GetComponentInParent<ArcadeVehicle>();
            bumperOffset = vehicle != null ? vehicle.transform.InverseTransformPoint(transform.position) : transform.localPosition;
            SetRate(0f);
        }

        private void OnEnable()
        {
            SetRate(0f);
            paper.Play();
        }

        private void OnDisable()
        {
            if (paper != null) paper.Stop(false, ParticleSystemStopBehavior.StopEmittingAndClear);
        }

        private void LateUpdate()
        {
            if (vehicle == null || !vehicle.isActiveAndEnabled || vehicle.Ctx == null)
            {
                SetRate(0f);
                return;
            }

            var state = vehicle.State;
            float speed = state.reversing ? 0f : Mathf.InverseLerp(startKmh, fullKmh, state.speedKmh);
            SetRate(maximumRate * speed * Mathf.Lerp(1f, nitroMultiplier, Mathf.Clamp01(state.nosActive)));
            var position = vehicle.transform.TransformPoint(bumperOffset);
            float surface = 0f;
            int count = 0;
            foreach (var wheel in vehicle.Wheels)
            {
                if (!wheel.grounded || wheel.contactNormal.y < .4f) continue;
                var normal = wheel.contactNormal;
                var offset = position - wheel.contactPoint;
                surface += wheel.contactPoint.y - (normal.x * offset.x + normal.z * offset.z) / normal.y;
                count++;
            }
            if (count > 0) position.y = surface / count + groundClearance;
            transform.position = position;
        }

        private void SetRate(float rate)
        {
            var emission = paper.emission;
            emission.rateOverTime = rate;
        }
    }
}

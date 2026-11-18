using UnityEngine;

namespace Arcade.Vehicle
{
    [RequireComponent(typeof(Camera))]
    public sealed class VehicleChaseCamera : MonoBehaviour
    {
        public ArcadeVehicle vehicle;
        public float height = 1.45f, distance = 3.8f, distanceAddAtSpeed = 0.3f;
        public float lookAhead = 2.0f, lookHeight = 1.0f;
        public float posSmooth = 0.08f, rotSmooth = 0.10f, fovSmooth = 0.15f;
        public float fovBase = 60f, fovMax = 72f, fovNosKick = 6f;
        [Range(0f, 1f)] public float driftFollow = 0.55f;
        public LayerMask obstacleMask = 1;

        Camera _cam; Vector3 _posVel, _look;

        void Awake() { _cam = GetComponent<Camera>(); }

        public void Bind(ArcadeVehicle v)
        {
            vehicle = v;
            if (v != null) { _look = v.transform.position; _posVel = Vector3.zero; }
        }

        void LateUpdate()
        {
            if (vehicle == null) return;
            float dt = Time.unscaledDeltaTime;
            if (dt <= 0f) return;

            var t = vehicle.transform;
            var s = vehicle.State;

            Vector3 flatVel = Vector3.ProjectOnPlane(vehicle.Rb.linearVelocity, Vector3.up);
            Vector3 fwd = Vector3.ProjectOnPlane(t.forward, Vector3.up).normalized;
            Vector3 dir = fwd;
            if (flatVel.magnitude > 4f)
                dir = Vector3.Slerp(fwd, flatVel.normalized, driftFollow * Mathf.Clamp01(s.speed / 18f)).normalized;

            float speedT = Mathf.Clamp01(s.speedKmh / 220f);
            Vector3 desired = t.position - dir * (distance + distanceAddAtSpeed * speedT) + Vector3.up * height;

            Vector3 from = t.position + Vector3.up * lookHeight;
            Vector3 delta = desired - from;
            if (delta.sqrMagnitude > 0.01f &&
                Physics.SphereCast(from, 0.25f, delta.normalized, out var hit, delta.magnitude,
                                   obstacleMask, QueryTriggerInteraction.Ignore))
                desired = from + delta.normalized * Mathf.Max(0.5f, hit.distance - 0.1f);

            transform.position = Vector3.SmoothDamp(transform.position, desired, ref _posVel, posSmooth, Mathf.Infinity, dt);

            Vector3 lookTarget = t.position + Vector3.up * lookHeight + dir * lookAhead * speedT;
            _look = Vector3.Lerp(_look, lookTarget, 1f - Mathf.Exp(-dt / rotSmooth));
            Vector3 toLook = _look - transform.position;
            if (toLook.sqrMagnitude > 0.01f)
                transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(toLook, Vector3.up),
                                                      1f - Mathf.Exp(-dt / rotSmooth));

            float fovTarget = Mathf.Lerp(fovBase, fovMax, speedT) + fovNosKick * s.nosActive;
            _cam.fieldOfView = Mathf.Lerp(_cam.fieldOfView, fovTarget, 1f - Mathf.Exp(-dt / fovSmooth));
        }
    }
}
using UnityEngine;

namespace RedLine.NFS
{
    [DisallowMultipleComponent, RequireComponent(typeof(Camera))]
    public sealed class NFSArcadeCamera : MonoBehaviour
    {
        [Header("Target and spring follow")]
        [Tooltip("Vehicle to follow. Bind in the setup wizard or assign manually.")] public NFSRaycastCarController Target;
        [Min(1), Tooltip("Distance behind the car, metres.")] public float FollowDistance = 6.5f;
        [Min(.2f), Tooltip("Height above the chassis root, metres.")] public float FollowHeight = 2.5f;
        [Range(.3f, 1.5f), Tooltip("Scales follow distance and height; 0.6 brings the camera 40% closer, including existing scene settings.")] public float FollowDistanceMultiplier = .6f;
        [Range(.02f, 1), Tooltip("Critical-damping follow time, seconds.")] public float PositionSmoothTime = .16f;
        [Range(1, 25), Tooltip("Rotation response, 1/s.")] public float RotationResponse = 9;
        [Tooltip("Local look target offset.")] public Vector3 LookOffset = new Vector3(0, .7f, 0);
        [Range(0, .1f), Tooltip("Acceleration-induced spring lag, metres per m/s².")] public float AccelerationLag = .035f;
        [Header("Drift framing")]
        [Range(0, 8), Tooltip("Look ahead in the direction of travel, metres.")] public float DriftLookAhead = 3.5f;
        [Range(0, 1), Tooltip("Blends the follow direction toward velocity while drifting.")] public float DriftFollowWeight = .25f;
        [Header("Dynamic field of view")]
        [Range(40, 90), Tooltip("Field of view at low speed, degrees.")] public float BaseFOV = 60;
        [Range(50, 110), Tooltip("Field of view at maximum speed, degrees.")] public float MaxSpeedFOV = 78;
        [Range(60, 120), Tooltip("Sustained field of view with active NOS, degrees.")] public float NitroFOV = 92;
        [Range(1, 20), Tooltip("Normal FOV response, 1/s; NOS responds twice as quickly.")] public float FOVResponse = 5;
        [Header("Nitro ignition impulse")]
        [Range(0, 20), Tooltip("Extra FOV at ignition, fading into the sustained NOS FOV.")] public float NitroImpulseFOV = 10;
        [Range(.1f, 1), Tooltip("Duration of the ignition impulse, seconds. Retriggers on every NOS activation.")] public float NitroImpulseDuration = .5f;
        [Range(5, 40), Tooltip("Fast FOV response during ignition, 1/s.")] public float NitroImpulseResponse = 24;
        [Range(0, 1), Tooltip("Short backward camera kick at ignition, metres.")] public float NitroImpulseKick = .45f;
        [Range(0, .3f), Tooltip("Additional ignition positional shake, metres.")] public float NitroImpulseShake = .12f;
        [Range(0, 3), Tooltip("Additional ignition rotational shake, degrees.")] public float NitroImpulseRotation = 1.5f;
        [Header("High-speed micro shake")]
        [Min(0), Tooltip("Shake begins above this speed, km/h.")] public float ShakeStartKmh = 200;
        [Range(0, .15f), Tooltip("Maximum positional shake, metres.")] public float ShakeAmount = .035f;
        [Range(1, 50), Tooltip("Procedural shake frequency, Hz.")] public float ShakeFrequency = 27;
        [Header("Camera obstruction")]
        [Tooltip("Walls/terrain to avoid; the target's layer is excluded automatically.")] public LayerMask ObstructionLayers = Physics.DefaultRaycastLayers;
        [Range(.05f, .8f), Tooltip("Camera clearance radius, metres.")] public float ObstructionRadius = .25f;

        private Camera cameraComponent;
        private Vector3 followPosition, followVelocity, previousTargetPosition;
        private Vector3 previousVelocity;
        private bool initialized;
        private Quaternion followRotation;
        private bool wasNitroActive;
        private float nitroImpulse, nitroBlend, nitroKick;
        private NFSRaycastCarController previousTarget;
        private readonly RaycastHit[] hits = new RaycastHit[16];

        private void Awake() { cameraComponent = GetComponent<Camera>(); cameraComponent.fieldOfView = BaseFOV; }
        private void OnEnable() => initialized = false;
        public void SnapToTarget() => initialized = false;

        private void LateUpdate()
        {
            if (!Target || !Target.Body) { initialized = false; return; }
            if (!cameraComponent) cameraComponent = GetComponent<Camera>();
            float dt = Mathf.Max(.0001f, Time.deltaTime);
            Vector3 position = Target.transform.position;
            bool snap = !initialized || Target != previousTarget || (position - previousTargetPosition).sqrMagnitude > 50 * 50;
            Vector3 forward = Vector3.ProjectOnPlane(Target.transform.forward, Vector3.up).normalized;
            if (forward.sqrMagnitude < .01f) forward = Vector3.forward;
            Vector3 velocity = Target.Body.linearVelocity;
            Vector3 travel = Vector3.ProjectOnPlane(velocity, Vector3.up).normalized;
            if (travel.sqrMagnitude < .01f || Target.CurrentGear < 0) travel = forward;
            float drift = Mathf.InverseLerp(5, 45, Mathf.Abs(Target.SlipAngle));
            Vector3 heading = Vector3.Slerp(forward, travel, drift * DriftFollowWeight).normalized;
            float acceleration = !snap ? Mathf.Clamp(Vector3.Dot(velocity - previousVelocity, forward) / dt, -20, 20) : 0;
            Vector3 desired = position + (-heading * (FollowDistance + acceleration * AccelerationLag) + Vector3.up * FollowHeight) * FollowDistanceMultiplier;
            Vector3 look = position + Target.transform.TransformDirection(LookOffset) + travel * (DriftLookAhead * drift + 1.5f);
            if (snap) { wasNitroActive = false; nitroImpulse = nitroBlend = nitroKick = 0; }
            if (Target.IsNitroActive && !wasNitroActive) nitroImpulse = 1;
            else nitroImpulse = Mathf.MoveTowards(nitroImpulse, 0, dt / Mathf.Max(.1f, NitroImpulseDuration));
            if (!Target.IsNitroActive) nitroImpulse = Mathf.MoveTowards(nitroImpulse, 0, dt * 8);
            nitroBlend = Mathf.MoveTowards(nitroBlend, Target.IsNitroActive ? 1 : 0, dt * 8);
            nitroKick = Mathf.Lerp(nitroKick, nitroImpulse, 1 - Mathf.Exp(-NitroImpulseResponse * dt));
            if (snap) { followPosition = desired; followVelocity = Vector3.zero; }
            else followPosition = Vector3.SmoothDamp(followPosition, desired, ref followVelocity, PositionSmoothTime, Mathf.Infinity, dt);
            followPosition = ResolveObstruction(look, followPosition);
            float intensity = Mathf.InverseLerp(ShakeStartKmh, Target.MaxSpeedKmh + Target.NitroMaxSpeedBonus, Target.SpeedKmh);
            intensity = Mathf.Max(intensity, nitroBlend);
            float time = Time.time * ShakeFrequency;
            Vector3 noise = new Vector3(Mathf.PerlinNoise(time, 3.17f) - .5f, Mathf.PerlinNoise(9.41f, time) - .5f, 0);
            Quaternion rotation = Quaternion.LookRotation(look - followPosition, Vector3.up);
            Vector3 cameraPosition = followPosition - heading * (NitroImpulseKick * nitroKick)
                + rotation * noise * (ShakeAmount * intensity + NitroImpulseShake * nitroImpulse);
            // Check the final pose as well, so the ignition kick/shake cannot cross a wall.
            transform.position = ResolveObstruction(look, cameraPosition);
            followRotation = snap ? rotation : Quaternion.Slerp(followRotation, rotation, 1 - Mathf.Exp(-RotationResponse * dt));
            float rotationShake = intensity + NitroImpulseRotation * nitroImpulse;
            transform.rotation = followRotation * Quaternion.Euler(noise.y * rotationShake, noise.x * rotationShake, -Target.SteerInput * Mathf.Clamp01(Target.SpeedKmh / 200) * 1.5f);
            float normalFOV = Mathf.Lerp(BaseFOV, MaxSpeedFOV, Mathf.Clamp01(Target.SpeedKmh / Target.MaxSpeedKmh));
            float fov = (Target.IsNitroActive ? Mathf.Max(normalFOV, NitroFOV) : normalFOV) + NitroImpulseFOV * nitroImpulse;
            float response = Target.IsNitroActive ? Mathf.Lerp(FOVResponse * 2, NitroImpulseResponse, nitroImpulse) : FOVResponse;
            cameraComponent.fieldOfView = Mathf.Lerp(snap ? BaseFOV : cameraComponent.fieldOfView, Mathf.Clamp(fov, 40, 120), 1 - Mathf.Exp(-response * dt));
            previousVelocity = velocity; previousTargetPosition = position; previousTarget = Target;
            wasNitroActive = Target.IsNitroActive; initialized = true;
        }

        private Vector3 ResolveObstruction(Vector3 look, Vector3 desired)
        {
            Vector3 offset = desired - look;
            float distance = offset.magnitude;
            if (distance <= .01f) return desired;
            int count = Physics.SphereCastNonAlloc(look, ObstructionRadius, offset / distance, hits, distance,
                ObstructionLayers.value & ~(1 << Target.VehicleLayer), QueryTriggerInteraction.Ignore);
            float nearest = distance;
            for (int i = 0; i < count; i++)
                if (hits[i].collider.attachedRigidbody != Target.Body && !hits[i].collider.transform.IsChildOf(Target.transform))
                    nearest = Mathf.Min(nearest, hits[i].distance);
            return nearest < distance ? look + offset.normalized * Mathf.Max(.02f, nearest - .08f) : desired;
        }
    }
}

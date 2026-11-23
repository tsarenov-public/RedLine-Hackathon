using UnityEngine;

namespace Arcade.Vehicle
{
    [RequireComponent(typeof(Camera))]
    [DefaultExecutionOrder(60)]
    public sealed class NFSCameraController : MonoBehaviour
    {
        [SerializeField] private ArcadeVehicle manualTarget;
        [Min(1f)] public float baseDistance = 3.28f;
        [Min(.4f)] public float heightOffset = 1.34f;
        public Vector3 lookAtOffset = new(0f, .5f, .55f);
        [Min(.01f)] public float positionDamping = .12f;
        [Min(.01f)] public float rotationDamping = .095f;
        [Range(0f, 1f)] public float driftFollow = .34f;
        public LayerMask obstacleMask = 201;
        [Min(.05f)] public float collisionRadius = .27f;
        [Min(0f)] public float highSpeedPullback = 1.1f;
        [Min(0f)] public float nitroPullback = .9f;
        [Min(.01f)] public float pullbackDamping = .22f;
        [Min(0f)] public float shakeFrequency = 24f;
        public Vector2 maximumShake = new(.052f, .037f);
        [Min(0f)] public float maximumRoll = .85f;

        public ArcadeVehicle Target { get; private set; }
        public float FollowDistance => Mathf.Max(.5f, baseDistance + chassisDistanceOffset) + speedOffset + nosOffset;
        public float ShakeStrength { get; private set; }

        private readonly RaycastHit[] hits = new RaycastHit[24];
        private float nextSearch;
        private float orbitYaw, orbitVelocity, followHeight, heightVelocity;
        private float chassisDistanceOffset, chassisRoof;
        private float speedOffset, speedVelocity, nosOffset, nosVelocity;
        private float obstructionDistance, obstructionVelocity;
        private Vector3 lastCarPosition;
        private Quaternion stableRotation;
        private bool snapped;

        private void Start() => UpdateTarget();

        private void Update() => UpdateTarget();

        public void Bind(ArcadeVehicle vehicle)
        {
            manualTarget = vehicle;
            nextSearch = 0f;
            UpdateTarget();
        }

        private void UpdateTarget()
        {
            if (IsPlayer(manualTarget)) { SetTarget(manualTarget); return; }
            if (IsPlayer(VehicleSpawner.Current)) { SetTarget(VehicleSpawner.Current); return; }
            if (IsPlayer(Target)) return;

            SetTarget(null);
            if (Time.unscaledTime < nextSearch) return;
            nextSearch = Time.unscaledTime + .35f;
            foreach (var candidate in FindObjectsByType<ArcadeVehicle>())
            {
                if (!IsPlayer(candidate)) continue;
                SetTarget(candidate);
                break;
            }
        }

        private static bool IsPlayer(ArcadeVehicle vehicle) =>
            vehicle != null && vehicle.isActiveAndEnabled && vehicle.Ctx != null &&
            vehicle.TryGetComponent<VehicleInputSource>(out var input) && input.isActiveAndEnabled &&
            (!vehicle.TryGetComponent<AIRacerController>(out var ai) || !ai.isActiveAndEnabled);

        private void SetTarget(ArcadeVehicle vehicle)
        {
            if (vehicle == Target) return;
            Target = vehicle;
            snapped = false;
            speedOffset = nosOffset = speedVelocity = nosVelocity = 0f;
            MeasureChassis(vehicle);
        }

        private void MeasureChassis(ArcadeVehicle vehicle)
        {
            chassisDistanceOffset = chassisRoof = 0f;
            if (vehicle == null) return;

            var scale = vehicle.transform.lossyScale;
            float longest = 0f, rear = 0f, width = 0f;
            foreach (var box in vehicle.GetComponents<BoxCollider>())
            {
                if (!box.enabled || box.isTrigger) continue;
                chassisRoof = Mathf.Max(chassisRoof, (box.center.y + .5f * box.size.y) * Mathf.Abs(scale.y));
                if (box.size.z <= longest) continue;
                longest = box.size.z;
                rear = (.5f * box.size.z - box.center.z) * Mathf.Abs(scale.z);
                width = box.size.x * Mathf.Abs(scale.x);
            }
            // Keep the same rear clearance and framing on longer or narrower chassis.
            if (longest > 0f)
                chassisDistanceOffset = Mathf.Clamp(rear - 2.15f + .7f * (width - 2f), -1.4f, 1.6f);
        }

        private void LateUpdate()
        {
            if (Target == null) { ShakeStrength = 0f; return; }
            float dt = Time.unscaledDeltaTime;
            if (dt <= 0f) return;

            var car = Target.transform;
            var state = Target.State;
            Vector3 position = car.position;
            Vector3 forward = Planar(car.forward).normalized;
            if (forward.sqrMagnitude < .1f) forward = Vector3.forward;
            if (state.reversing) forward = -forward;
            Vector3 velocity = Planar(Target.Rb.linearVelocity);
            if (velocity.sqrMagnitude > 16f && Vector3.Dot(velocity, forward) > 0f)
                forward = Vector3.Slerp(forward, velocity.normalized, driftFollow * Mathf.Clamp01(velocity.magnitude / 20f));

            float targetYaw = Mathf.Atan2(forward.x, forward.z) * Mathf.Rad2Deg;
            float targetHeight = position.y + Mathf.Max(heightOffset, chassisRoof + .22f);
            bool reset = !snapped || (position - lastCarPosition).sqrMagnitude > 18f * 18f;
            if (reset)
            {
                orbitYaw = targetYaw;
                orbitVelocity = heightVelocity = obstructionVelocity = 0f;
                followHeight = targetHeight;
                obstructionDistance = float.PositiveInfinity;
            }
            orbitYaw = Mathf.SmoothDampAngle(orbitYaw, targetYaw, ref orbitVelocity, positionDamping, Mathf.Infinity, dt);
            followHeight = Mathf.SmoothDamp(followHeight, targetHeight, ref heightVelocity,
                positionDamping, Mathf.Infinity, dt);
            speedOffset = Mathf.SmoothDamp(speedOffset, state.speedKmh > 150f ? highSpeedPullback : 0f,
                ref speedVelocity, pullbackDamping, Mathf.Infinity, dt);
            nosOffset = Mathf.SmoothDamp(nosOffset, state.nosActive > .01f ? nitroPullback : 0f,
                ref nosVelocity, pullbackDamping, Mathf.Infinity, dt);

            var orbit = Quaternion.Euler(0f, orbitYaw, 0f);
            var lookOffset = lookAtOffset;
            lookOffset.y = Mathf.Max(lookOffset.y, chassisRoof * .43f);
            var aim = position + orbit * lookOffset;
            var pivot = position + Vector3.up * Mathf.Max(.65f, lookOffset.y);
            var intended = position - orbit * Vector3.forward * FollowDistance;
            intended.y = Mathf.Max(followHeight, position.y + .45f);
            var arm = intended - pivot;
            float blocked = ClearDistance(pivot, arm);
            if (blocked < obstructionDistance)
            {
                obstructionDistance = blocked;
                obstructionVelocity = 0f;
            }
            else obstructionDistance = Mathf.SmoothDamp(obstructionDistance, blocked, ref obstructionVelocity,
                .14f, Mathf.Infinity, dt);
            Vector3 cameraPosition = pivot + arm.normalized * obstructionDistance;
            var aimDirection = aim - cameraPosition;
            Quaternion look = aimDirection.sqrMagnitude > .0001f ? Quaternion.LookRotation(aimDirection, Vector3.up) : orbit;
            stableRotation = reset ? look : Quaternion.Slerp(stableRotation, look, 1f - Mathf.Exp(-dt / rotationDamping));

            ShakeStrength = Mathf.InverseLerp(100f, 280f, state.speedKmh);
            if (ShakeStrength > 0f)
            {
                float phase = Time.unscaledTime * shakeFrequency;
                cameraPosition += stableRotation * new Vector3(Noise(phase, 1.7f) * maximumShake.x,
                    Noise(phase, 4.2f) * maximumShake.y, 0f) * ShakeStrength;
                var shakenArm = cameraPosition - pivot;
                cameraPosition = pivot + shakenArm.normalized * ClearDistance(pivot, shakenArm);
                look = stableRotation * Quaternion.Euler(0f, 0f, Noise(phase, 7.8f) * maximumRoll * ShakeStrength);
            }
            else look = stableRotation;

            transform.SetPositionAndRotation(cameraPosition, look);
            lastCarPosition = position;
            snapped = true;
        }

        private float ClearDistance(Vector3 origin, Vector3 arm)
        {
            float length = arm.magnitude;
            var ray = new Ray(origin, arm / Mathf.Max(length, .001f));
            int count = Physics.SphereCastNonAlloc(ray, collisionRadius, hits, length, obstacleMask, QueryTriggerInteraction.Ignore);
            var results = count == hits.Length
                ? Physics.SphereCastAll(ray, collisionRadius, length, obstacleMask, QueryTriggerInteraction.Ignore)
                : hits;
            if (results != hits) count = results.Length;
            for (int i = 0; i < count; i++)
            {
                var hit = results[i];
                if (hit.collider == null || hit.collider.transform.IsChildOf(Target.transform)) continue;
                length = Mathf.Min(length, Mathf.Max(0f, hit.distance - .035f));
            }
            return length;
        }

        private static Vector3 Planar(Vector3 vector) => new(vector.x, 0f, vector.z);
        private static float Noise(float phase, float channel) => Mathf.PerlinNoise(phase, channel) * 2f - 1f;
    }
}

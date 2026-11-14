using System;
using UnityEngine;
using UnityEngine.InputSystem;

namespace RedLine.NFS
{
    [DisallowMultipleComponent, RequireComponent(typeof(Rigidbody), typeof(BoxCollider))]
    public sealed class NFSRaycastCarController : MonoBehaviour
    {
        [Serializable]
        public sealed class Wheel
        {
            [Tooltip("Optional suspension mount. Its position defines the ray origin; chassis up defines its axis.")]
            public Transform SuspensionPoint;
            [Tooltip("Wheel spin/steer pivot. Keep the model's import correction on a child of this pivot.")]
            public Transform Visual;
            [Tooltip("Chassis-local mount used when no SuspensionPoint is assigned.")]
            public Vector3 LocalAnchor;
            [Tooltip("Front steering wheel.")] public bool Steers;
            [Tooltip("Receives engine torque; the default preset is rear-wheel drive.")] public bool Driven;
            [Min(.05f), Tooltip("Rolling radius in metres.")] public float Radius = .34f;
            [NonSerialized] internal Quaternion visualBindRotation;
            [NonSerialized] internal float spin;
            [NonSerialized] internal WheelContact contact;
        }

        public struct WheelContact
        {
            public bool Grounded;
            public Vector3 Point, Normal;
            public float SpringLength, Compression, NormalLoad;
            public Collider Collider;
        }

        public struct DriverInput
        {
            public float Throttle, BrakeReverse, Steer;
            public bool Handbrake, Nitro;
            public DriverInput(float throttle, float brakeReverse, float steer, bool handbrake = false, bool nitro = false)
            { Throttle = throttle; BrakeReverse = brakeReverse; Steer = steer; Handbrake = handbrake; Nitro = nitro; }
        }

        [Header("Input — keyboard and gamepad, no action asset required")]
        [Tooltip("W / Up Arrow / right trigger.")] public InputAction Throttle = new InputAction("Throttle", InputActionType.Value, expectedControlType: "Axis");
        [Tooltip("S / Down Arrow / left trigger. Brake first, then reverse after stopping.")] public InputAction BrakeReverse = new InputAction("BrakeReverse", InputActionType.Value, expectedControlType: "Axis");
        [Tooltip("A/D, arrows or left stick X.")] public InputAction Steer = new InputAction("Steer", InputActionType.Value, expectedControlType: "Axis");
        [Tooltip("Space / gamepad south button.")] public InputAction Handbrake = new InputAction("Handbrake", InputActionType.Button);
        [Tooltip("Left Shift / gamepad east button.")] public InputAction Nitro = new InputAction("Nitro", InputActionType.Button);

        [Header("Chassis — M3 GTR inspired arcade preset")]
        [Min(100), Tooltip("Body mass in kg.")] public float Mass = 1400;
        [Tooltip("Low chassis-local centre of mass; metres relative to the physics root.")] public Vector3 CenterOfMass = new Vector3(0, -.25f, .05f);
        [Range(0, 31), Tooltip("Layer assigned to own colliders. Default: built-in Ignore Raycast; no ProjectSettings changes.")]
        public int VehicleLayer = 2;
        [Tooltip("Drivable layers; the vehicle's own layer is always excluded.")] public LayerMask GroundLayers = Physics.DefaultRaycastLayers;
        [Min(0), Tooltip("Quadratic aerodynamic resistance, N / (m/s)^2.")] public float AeroDrag = .42f;
        [Min(0), Tooltip("Rolling deceleration in m/s², active on the ground.")] public float RollingResistance = .12f;

        [Header("Four raycast wheels — FL, FR, RL, RR")]
        [Tooltip("Exactly four wheels, ordered front left, front right, rear left, rear right.")]
        public Wheel[] Wheels = DefaultWheels();
        [Min(.05f), Tooltip("Unloaded spring length in metres.")] public float SuspensionRestLength = .36f;
        [Min(.02f), Tooltip("Maximum compression and extra droop travel in metres.")] public float SuspensionTravel = .22f;
        [Min(1000), Tooltip("Hooke spring stiffness per wheel, N/m.")] public float SpringStiffness = 38000;
        [Min(0), Tooltip("Suspension damping per wheel, N/(m/s).")] public float DamperStiffness = 4200;
        [Range(0, 1), Tooltip("Sphere radius for suspension casts; zero selects a raycast.")] public float CastRadius = .1f;
        [Range(0, 1), Tooltip("Couples the left/right suspension to resist body roll.")] public float AntiRoll = .45f;

        [Header("Engine and automatic transmission")]
        [Min(10), Tooltip("Forward speed limit without nitro, km/h.")] public float MaxSpeedKmh = 320;
        [Min(1), Tooltip("Peak crankshaft torque, N·m.")] public float EngineTorque = 540;
        [Tooltip("Torque multiplier over normalized engine RPM.")]
        public AnimationCurve TorqueCurve = new AnimationCurve(new Keyframe(0, .65f), new Keyframe(.45f, 1), new Keyframe(.8f, .95f), new Keyframe(1, .55f));
        [Tooltip("Index 0 is a negative reverse ratio; indices 1–6 are forward ratios.")]
        public float[] GearRatios = { -3.4f, 3.65f, 2.15f, 1.55f, 1.18f, .96f, .78f };
        [Min(.1f), Tooltip("Final drive reduction.")] public float FinalDrive = 3.8f;
        [Min(100), Tooltip("Upshift threshold, RPM.")] public float ShiftUpRPM = 7200;
        [Min(100), Tooltip("Downshift threshold, RPM.")] public float ShiftDownRPM = 3200;
        [Min(100), Tooltip("Minimum running engine speed, RPM.")] public float IdleRPM = 950;
        [Min(1000), Tooltip("Engine redline, RPM.")] public float MaxRPM = 8000;
        [Range(.05f, 1), Tooltip("Torque interruption duration per shift, seconds.")] public float ShiftDelay = .18f;
        [Range(.1f, 1), Tooltip("Hold the opposite pedal at rest for this long before changing direction.")] public float DirectionChangeDelay = .22f;
        [Min(1), Tooltip("Maximum reverse speed, km/h.")] public float ReverseMaxSpeedKmh = 40;
        [Min(1), Tooltip("Service brake deceleration, m/s².")] public float BrakeAcceleration = 15;

        [Header("Steering and tyre grip")]
        [Range(5, 50), Tooltip("Low-speed front steering angle, degrees.")] public float MaxSteerAngle = 32;
        [Tooltip("Steering angle multiplier over SpeedKmh / MaxSpeedKmh.")]
        public AnimationCurve SteeringBySpeed = new AnimationCurve(new Keyframe(0, 1), new Keyframe(.3f, .48f), new Keyframe(.7f, .2f), new Keyframe(1, .13f));
        [Range(1, 15), Tooltip("Front lateral velocity damping, 1/s.")] public float FrontGrip = 9;
        [Range(1, 15), Tooltip("Rear lateral velocity damping, 1/s.")] public float RearGrip = 7.5f;
        [Range(.1f, 6), Tooltip("Rear lateral grip with handbrake or lift-off oversteer.")] public float DriftRearGrip = 1.65f;
        [Range(.5f, 3), Tooltip("Tyre force limit relative to suspension normal load.")] public float TyreFriction = 1.65f;
        [Range(1, 15), Tooltip("Smooth steering/input response, 1/s.")] public float SteeringResponse = 7;

        [Header("Drift assist")]
        [Range(3, 35), Tooltip("Minimum slip angle for drift assist, degrees.")] public float DriftThreshold = 10;
        [Range(15, 70), Tooltip("Target controllable drift angle at full steering input.")] public float TargetDriftAngle = 34;
        [Range(35, 85), Tooltip("Extra counter-torque beyond this slip angle.")] public float MaxDriftAngle = 58;
        [Range(0, 15), Tooltip("Yaw-rate correction strength during a drift.")] public float DriftAssistStrength = 5;
        [Range(.3f, 3), Tooltip("Maximum grounded yaw rate, radians per second.")] public float MaxYawRate = 1.55f;
        [Range(0, 4), Tooltip("Small forward acceleration retained during a drift, m/s².")] public float DriftForwardBoost = 1.2f;

        [Header("NOS")]
        [Min(1), Tooltip("Maximum nitro charge.")] public float NitroCapacity = 100;
        [Min(.1f), Tooltip("Charge consumed per second.")] public float NitroBurnRate = 25;
        [Min(0), Tooltip("Charge regenerated per second above 50 km/h.")] public float NitroRegenRate = 6;
        [Min(1), Tooltip("Regeneration multiplier during a grounded drift.")] public float NitroDriftRegenMultiplier = 2.5f;
        [Range(1, 3), Tooltip("Additional engine thrust multiplier while nitro is active.")] public float NitroForceMultiplier = 1.7f;
        [Min(0), Tooltip("Extra forward speed allowance with NOS, km/h.")] public float NitroMaxSpeedBonus = 50;

        [Header("Road holding, air control and wall sliding")]
        [Min(0), Tooltip("Downforce in newtons per m/s, applied only while grounded.")] public float DownforceFactor = 75;
        [Range(0, 40), Tooltip("Airborne pitch/roll levelling angular acceleration.")] public float AirLevelStrength = 16;
        [Range(0, 15), Tooltip("Airborne pitch/roll angular damping.")] public float AirAngularDamping = 5;
        [Range(0, 1), Tooltip("Restores tangential speed lost in a shallow wall collision, without adding energy.")] public float WallSlideAssist = 1;

        public Rigidbody Body { get; private set; }
        public float SpeedKmh { get; private set; }
        public float ForwardSpeed { get; private set; }
        public float CurrentRPM { get; private set; }
        public int CurrentGear { get; private set; } = 1;
        public float SlipAngle { get; private set; }
        public bool IsDrifting { get; private set; }
        public bool IsShifting => shiftTimer > 0;
        public bool IsNitroActive { get; private set; }
        public float NitroAmount { get; private set; }
        public float NitroNormalized => NitroAmount / Mathf.Max(1, NitroCapacity);
        public int GroundedWheelCount { get; private set; }
        public float ThrottleInput { get; private set; }
        public float BrakeInput { get; private set; }
        public float SteerInput { get; private set; }
        public bool HandbrakeInput { get; private set; }
        public bool IsBurningOut => GroundedWheelCount >= 2 && CurrentGear > 0 && ThrottleInput > .7f && SpeedKmh < 35;
        public bool IsWallSliding => Time.time - wallTime < .12f && SpeedKmh > 30;
        public Vector3 WallPoint { get; private set; }
        public Vector3 WallNormal { get; private set; }

        private readonly RaycastHit[] castHits = new RaycastHit[24];
        private DriverInput externalInput;
        private bool useExternalInput;
        private float shiftTimer, shiftCooldown, directionTimer, rearDriftBlend, liftOffTimer, previousThrottle;
        private float wheelSteerAngle, wallTime = -100;
        private Vector3 groundNormal = Vector3.up, preStepVelocity, wallIncoming;
        private bool pendingWall;
        private PhysicsMaterial bodyMaterial;

        public static Wheel[] DefaultWheels() => new[] {
            new Wheel { LocalAnchor = new Vector3(-.86f, .1f, 1.35f), Steers = true },
            new Wheel { LocalAnchor = new Vector3(.86f, .1f, 1.35f), Steers = true },
            new Wheel { LocalAnchor = new Vector3(-.86f, .1f, -1.35f), Driven = true },
            new Wheel { LocalAnchor = new Vector3(.86f, .1f, -1.35f), Driven = true }
        };

        private void Reset()
        {
            Wheels = DefaultWheels();
            var box = GetComponent<BoxCollider>();
            if (box) { box.size = new Vector3(1.8f, .55f, 4.2f); box.center = Vector3.zero; }
            EnsureBindings();
        }

        private void OnValidate()
        {
            Mass = Mathf.Max(100, Mass); MaxSpeedKmh = Mathf.Max(10, MaxSpeedKmh);
            SuspensionRestLength = Mathf.Max(.05f, SuspensionRestLength); SuspensionTravel = Mathf.Max(.02f, SuspensionTravel);
            SpringStiffness = Mathf.Max(1000, SpringStiffness); DamperStiffness = Mathf.Max(0, DamperStiffness);
            IdleRPM = Mathf.Max(100, IdleRPM); MaxRPM = Mathf.Max(IdleRPM + 1000, MaxRPM);
            ShiftUpRPM = Mathf.Clamp(ShiftUpRPM, IdleRPM + 500, MaxRPM - 100);
            ShiftDownRPM = Mathf.Clamp(ShiftDownRPM, IdleRPM + 100, ShiftUpRPM - 500);
            NitroCapacity = Mathf.Max(1, NitroCapacity); NitroBurnRate = Mathf.Max(.1f, NitroBurnRate);
            if (GearRatios == null || GearRatios.Length < 2) GearRatios = new[] { -3.4f, 3.65f, 2.15f, 1.55f, 1.18f, .96f, .78f };
            GearRatios[0] = -Mathf.Max(.1f, Mathf.Abs(GearRatios[0]));
            for (int i = 1; i < GearRatios.Length; i++) GearRatios[i] = Mathf.Max(.1f, Mathf.Abs(GearRatios[i]));
            if (Wheels == null || Wheels.Length != 4) Wheels = DefaultWheels();
            var defaults = DefaultWheels();
            for (int i = 0; i < 4; i++) { if (Wheels[i] == null) Wheels[i] = defaults[i]; Wheels[i].Radius = Mathf.Max(.05f, Wheels[i].Radius); }
            EnsureBindings();
        }

        private void Awake()
        {
            OnValidate(); Body = GetComponent<Rigidbody>();
            Body.mass = Mass; Body.centerOfMass = CenterOfMass;
            Body.interpolation = RigidbodyInterpolation.Interpolate;
            Body.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            Body.linearDamping = 0; Body.angularDamping = .12f; Body.maxAngularVelocity = 5;
            Body.maxDepenetrationVelocity = 5;
            Body.solverIterations = 12; Body.solverVelocityIterations = 4;
            bodyMaterial = new PhysicsMaterial("NFS chassis — zero friction") { dynamicFriction = 0, staticFriction = 0,
                bounciness = 0, frictionCombine = PhysicsMaterialCombine.Minimum, bounceCombine = PhysicsMaterialCombine.Minimum };
            foreach (var collider in GetComponentsInChildren<Collider>())
            {
                collider.gameObject.layer = VehicleLayer; collider.sharedMaterial = bodyMaterial; collider.contactOffset = .025f;
            }
            foreach (var wheel in Wheels) if (wheel.Visual) wheel.visualBindRotation = wheel.Visual.localRotation;
            NitroAmount = NitroCapacity; CurrentRPM = IdleRPM;
        }

        private void OnEnable() { EnsureBindings(); Throttle.Enable(); BrakeReverse.Enable(); Steer.Enable(); Handbrake.Enable(); Nitro.Enable(); }
        private void OnDisable() { Throttle?.Disable(); BrakeReverse?.Disable(); Steer?.Disable(); Handbrake?.Disable(); Nitro?.Disable(); IsNitroActive = false; }
        private void OnDestroy()
        {
            Throttle?.Dispose(); BrakeReverse?.Dispose(); Steer?.Dispose(); Handbrake?.Dispose(); Nitro?.Dispose();
            if (bodyMaterial) Destroy(bodyMaterial);
        }

        private void EnsureBindings()
        {
            if (Throttle == null) Throttle = new InputAction("Throttle", InputActionType.Value);
            if (BrakeReverse == null) BrakeReverse = new InputAction("BrakeReverse", InputActionType.Value);
            if (Steer == null) Steer = new InputAction("Steer", InputActionType.Value);
            if (Handbrake == null) Handbrake = new InputAction("Handbrake", InputActionType.Button);
            if (Nitro == null) Nitro = new InputAction("Nitro", InputActionType.Button);
            if (Throttle.bindings.Count == 0) { Throttle.AddBinding("<Keyboard>/w"); Throttle.AddBinding("<Keyboard>/upArrow"); Throttle.AddBinding("<Gamepad>/rightTrigger"); }
            if (BrakeReverse.bindings.Count == 0) { BrakeReverse.AddBinding("<Keyboard>/s"); BrakeReverse.AddBinding("<Keyboard>/downArrow"); BrakeReverse.AddBinding("<Gamepad>/leftTrigger"); }
            if (Steer.bindings.Count == 0)
            {
                Steer.AddCompositeBinding("1DAxis").With("Negative", "<Keyboard>/a").With("Positive", "<Keyboard>/d");
                Steer.AddCompositeBinding("1DAxis").With("Negative", "<Keyboard>/leftArrow").With("Positive", "<Keyboard>/rightArrow");
                Steer.AddBinding("<Gamepad>/leftStick/x");
            }
            if (Handbrake.bindings.Count == 0) { Handbrake.AddBinding("<Keyboard>/space"); Handbrake.AddBinding("<Gamepad>/buttonSouth"); }
            if (Nitro.bindings.Count == 0) { Nitro.AddBinding("<Keyboard>/leftShift"); Nitro.AddBinding("<Gamepad>/buttonEast"); }
        }

        /// <summary>Optional AI/replay/test input, using the same physics path as player input.</summary>
        public void SetExternalInput(DriverInput input) { externalInput = input; useExternalInput = true; }
        public void UsePlayerInput() => useExternalInput = false;
        public WheelContact GetWheelContact(int index) => index >= 0 && index < Wheels.Length ? Wheels[index].contact : default;
        private Vector3 Mount(Wheel wheel) => wheel.SuspensionPoint ? wheel.SuspensionPoint.position : transform.TransformPoint(wheel.LocalAnchor);

        private void FixedUpdate()
        {
            float dt = Time.fixedDeltaTime;
            ApplyWallSlide(); preStepVelocity = Body.linearVelocity;
            SpeedKmh = Body.linearVelocity.magnitude * 3.6f;
            ForwardSpeed = Vector3.Dot(Body.linearVelocity, transform.forward);
            var input = useExternalInput ? externalInput : new DriverInput(Throttle.ReadValue<float>(), BrakeReverse.ReadValue<float>(),
                Steer.ReadValue<float>(), Handbrake.IsPressed(), Nitro.IsPressed());
            ThrottleInput = Mathf.Clamp01(input.Throttle); BrakeInput = Mathf.Clamp01(input.BrakeReverse);
            SteerInput = Mathf.MoveTowards(SteerInput, Mathf.Clamp(input.Steer, -1, 1), SteeringResponse * dt);
            HandbrakeInput = input.Handbrake;
            if (previousThrottle > .6f && ThrottleInput < .2f && Mathf.Abs(SteerInput) > .3f && SpeedKmh > 50) liftOffTimer = .7f;
            previousThrottle = ThrottleInput; liftOffTimer = Mathf.Max(0, liftOffTimer - dt);
            rearDriftBlend = Mathf.MoveTowards(rearDriftBlend, HandbrakeInput || liftOffTimer > 0 ? 1 : 0, dt * 4);
            wheelSteerAngle = SteerInput * MaxSteerAngle * Mathf.Clamp(SteeringBySpeed.Evaluate(SpeedKmh / MaxSpeedKmh), .05f, 1);
            CastSuspension();
            var planarVelocity = Vector3.ProjectOnPlane(Body.linearVelocity, groundNormal);
            SlipAngle = planarVelocity.sqrMagnitude > 1 ? Vector3.SignedAngle(CurrentGear < 0 ? -transform.forward : transform.forward, planarVelocity, groundNormal) : 0;
            IsDrifting = CurrentGear > 0 && GroundedWheelCount >= 2 && SpeedKmh > 30 && Mathf.Abs(SlipAngle) > DriftThreshold;
            UpdateTransmission(dt);
            IsNitroActive = input.Nitro && ThrottleInput > .1f && CurrentGear > 0 && GroundedWheelCount >= 2 && NitroAmount > NitroBurnRate * dt;
            if (IsNitroActive) NitroAmount = Mathf.Max(0, NitroAmount - NitroBurnRate * dt);
            else if (!input.Nitro && GroundedWheelCount >= 2 && SpeedKmh > 50)
                NitroAmount = Mathf.Min(NitroCapacity, NitroAmount + NitroRegenRate * (IsDrifting ? NitroDriftRegenMultiplier : 1) * dt);
            float driveInput = CurrentGear < 0 ? BrakeInput : ThrottleInput;
            bool oppositePedal = CurrentGear < 0 ? ThrottleInput > .01f : BrakeInput > .01f;
            float brake = oppositePedal ? (CurrentGear < 0 ? ThrottleInput : BrakeInput) : 0;
            float ratio = GearRatios[CurrentGear < 0 ? 0 : CurrentGear];
            float radius = (Wheels[2].Radius + Wheels[3].Radius) * .5f;
            float force = driveInput * EngineTorque * Mathf.Max(0, TorqueCurve.Evaluate(CurrentRPM / MaxRPM)) * ratio * FinalDrive / radius;
            if (oppositePedal || IsShifting) force *= IsShifting && !oppositePedal ? .15f : 0;
            float limit = (CurrentGear < 0 ? ReverseMaxSpeedKmh : MaxSpeedKmh + (IsNitroActive ? NitroMaxSpeedBonus : 0)) / 3.6f;
            force *= 1 - Mathf.SmoothStep(0, 1, Mathf.InverseLerp(limit * .97f, limit + 1, Mathf.Abs(ForwardSpeed)));
            ApplyTyres(force, brake, dt);
            if (IsNitroActive) Body.AddForce(transform.forward * Mathf.Abs(force) * (NitroForceMultiplier - 1));
            if (GroundedWheelCount > 0)
            {
                Body.AddForce(-groundNormal * Body.linearVelocity.magnitude * DownforceFactor);
                if (planarVelocity.sqrMagnitude > .01f) Body.AddForce(-planarVelocity.normalized * Mass * RollingResistance);
                StabilizeDrift();
                if (IsDrifting && ThrottleInput > .1f && SpeedKmh < MaxSpeedKmh)
                    Body.AddForce(Vector3.ProjectOnPlane(transform.forward, groundNormal).normalized * (Mass * DriftForwardBoost * ThrottleInput));
            }
            else
            {
                Vector3 tiltVelocity = Vector3.ProjectOnPlane(Body.angularVelocity, Vector3.up);
                // Angle-axis also works when the car is exactly upside down, where
                // Cross(up, worldUp) would be zero and could never right the body.
                Quaternion.FromToRotation(transform.up, Vector3.up).ToAngleAxis(out float angle, out Vector3 axis);
                if (angle > 180) angle -= 360;
                Body.AddTorque(axis * (angle * Mathf.Deg2Rad * AirLevelStrength) - tiltVelocity * AirAngularDamping, ForceMode.Acceleration);
            }
            Body.AddForce(-Body.linearVelocity * (Body.linearVelocity.magnitude * AeroDrag));
            if (Mathf.Abs(ForwardSpeed) > limit)
                Body.AddForce(-transform.forward * Mathf.Sign(ForwardSpeed) * ((Mathf.Abs(ForwardSpeed) - limit) * Mass * 8));
        }

        private void CastSuspension()
        {
            GroundedWheelCount = 0; Vector3 normals = Vector3.zero;
            int mask = GroundLayers.value & ~(1 << VehicleLayer);
            foreach (var wheel in Wheels)
            {
                Vector3 origin = Mount(wheel), up = transform.up;
                float radius = Mathf.Min(CastRadius, wheel.Radius * .8f);
                float length = SuspensionRestLength + SuspensionTravel + wheel.Radius;
                int count = radius > .001f ? Physics.SphereCastNonAlloc(origin, radius, -up, castHits, length - radius, mask, QueryTriggerInteraction.Ignore) :
                    Physics.RaycastNonAlloc(origin, -up, castHits, length, mask, QueryTriggerInteraction.Ignore);
                float nearest = float.PositiveInfinity; RaycastHit hit = default; bool found = false;
                for (int i = 0; i < count; i++)
                {
                    var candidate = castHits[i];
                    if (candidate.collider.attachedRigidbody == Body || candidate.collider.transform.IsChildOf(transform) ||
                        Vector3.Dot(candidate.normal, up) < .4f || candidate.distance >= nearest) continue;
                    nearest = candidate.distance; hit = candidate; found = true;
                }
                wheel.contact = new WheelContact { SpringLength = SuspensionRestLength + SuspensionTravel, Normal = up };
                if (!found) continue;
                float springLength = Mathf.Clamp(hit.distance + radius - wheel.Radius, SuspensionRestLength - SuspensionTravel, SuspensionRestLength + SuspensionTravel);
                float compression = Mathf.Clamp(SuspensionRestLength - springLength, 0, SuspensionTravel);
                Vector3 relative = Body.GetPointVelocity(origin);
                if (hit.rigidbody) relative -= hit.rigidbody.GetPointVelocity(hit.point);
                float verticalVelocity = Vector3.Dot(relative, up);
                float springForce = Mathf.Clamp(SpringStiffness * compression - DamperStiffness * verticalVelocity, 0, SpringStiffness * SuspensionTravel * 3);
                Body.AddForceAtPosition(up * springForce, origin);
                if (hit.rigidbody && !hit.rigidbody.isKinematic) hit.rigidbody.AddForceAtPosition(-up * springForce, hit.point);
                wheel.contact = new WheelContact { Grounded = true, Point = hit.point, Normal = hit.normal, Collider = hit.collider,
                    SpringLength = springLength, Compression = compression, NormalLoad = springForce * Mathf.Max(0, Vector3.Dot(up, hit.normal)) };
                GroundedWheelCount++; normals += hit.normal;
            }
            groundNormal = GroundedWheelCount > 0 ? normals.normalized : Vector3.up;
            ApplyAntiRoll(0, 1); ApplyAntiRoll(2, 3);
        }

        private void ApplyAntiRoll(int left, int right)
        {
            if (!Wheels[left].contact.Grounded || !Wheels[right].contact.Grounded) return;
            float force = (Wheels[left].contact.Compression - Wheels[right].contact.Compression) * SpringStiffness * AntiRoll;
            Body.AddForceAtPosition(transform.up * force, Mount(Wheels[left]));
            Body.AddForceAtPosition(-transform.up * force, Mount(Wheels[right]));
        }

        private void ApplyTyres(float driveForce, float brake, float dt)
        {
            int driven = 0; foreach (var wheel in Wheels) if (wheel.Driven && wheel.contact.Grounded) driven++;
            foreach (var wheel in Wheels)
            {
                var contact = wheel.contact; if (!contact.Grounded) continue;
                Vector3 forward = Vector3.ProjectOnPlane(Quaternion.AngleAxis(wheel.Steers ? wheelSteerAngle : 0, transform.up) * transform.forward, contact.Normal).normalized;
                Vector3 right = Vector3.Cross(contact.Normal, forward).normalized;
                Vector3 velocity = Body.GetPointVelocity(contact.Point);
                if (contact.Collider.attachedRigidbody) velocity -= contact.Collider.attachedRigidbody.GetPointVelocity(contact.Point);
                float grip = wheel.Steers ? FrontGrip : Mathf.Lerp(RearGrip, DriftRearGrip, rearDriftBlend);
                float lateral = -Vector3.Dot(velocity, right) * grip * Mass * .25f;
                float normalLimit = Mathf.Max(0, contact.NormalLoad) * TyreFriction;
                float lateralLimit = normalLimit * (wheel.Steers ? 1 : Mathf.Lerp(1, Mathf.Clamp01(DriftRearGrip / Mathf.Max(.1f, RearGrip)), rearDriftBlend));
                lateral = Mathf.Clamp(lateral, -lateralLimit, lateralLimit);
                float longitudinal = wheel.Driven && driven > 0 ? driveForce / driven : 0;
                float wheelBrake = Mathf.Max(brake, !wheel.Steers && HandbrakeInput ? 1 : 0);
                if (wheelBrake > 0)
                {
                    float v = Vector3.Dot(velocity, forward);
                    longitudinal -= Mathf.Sign(v) * Mathf.Min(Mass * BrakeAcceleration * wheelBrake * .25f, Mathf.Abs(v) * Mass * .25f / dt);
                }
                longitudinal = Mathf.Clamp(longitudinal, -normalLimit, normalLimit);
                Vector3 tyreForce = Vector3.ClampMagnitude(forward * longitudinal + right * lateral, normalLimit);
                Body.AddForceAtPosition(tyreForce, contact.Point);
                if (contact.Collider.attachedRigidbody && !contact.Collider.attachedRigidbody.isKinematic)
                    contact.Collider.attachedRigidbody.AddForceAtPosition(-tyreForce, contact.Point);
            }
        }

        private void UpdateTransmission(float dt)
        {
            shiftTimer = Mathf.Max(0, shiftTimer - dt); shiftCooldown = Mathf.Max(0, shiftCooldown - dt);
            bool wantsOpposite = CurrentGear > 0 ? BrakeInput > .15f && ThrottleInput < .1f : ThrottleInput > .15f && BrakeInput < .1f;
            if (wantsOpposite && SpeedKmh < 2.2f) directionTimer += dt; else directionTimer = 0;
            if (directionTimer >= DirectionChangeDelay) { ChangeGear(CurrentGear > 0 ? -1 : 1); directionTimer = 0; }
            float radius = (Wheels[2].Radius + Wheels[3].Radius) * .5f;
            float ratio = Mathf.Abs(GearRatios[CurrentGear < 0 ? 0 : CurrentGear]);
            float wheelRPM = Mathf.Abs(ForwardSpeed) / (2 * Mathf.PI * radius) * 60 * ratio * FinalDrive;
            float pedal = CurrentGear < 0 ? BrakeInput : ThrottleInput;
            float targetRPM = Mathf.Clamp(Mathf.Max(wheelRPM, IdleRPM + pedal * 1700), IdleRPM, MaxRPM);
            CurrentRPM = Mathf.MoveTowards(CurrentRPM, targetRPM, dt * (targetRPM < CurrentRPM ? 22000 : 15000));
            if (CurrentGear <= 0 || IsShifting || shiftCooldown > 0) return;
            if (CurrentRPM >= ShiftUpRPM && CurrentGear < GearRatios.Length - 1) ChangeGear(CurrentGear + 1);
            else if (CurrentRPM < ShiftDownRPM && CurrentGear > 1)
            {
                float lowerRPM = wheelRPM * GearRatios[CurrentGear - 1] / ratio;
                if (lowerRPM < ShiftUpRPM - 500) ChangeGear(CurrentGear - 1);
            }
        }

        private void ChangeGear(int gear) { CurrentGear = gear; shiftTimer = ShiftDelay; shiftCooldown = ShiftDelay + .25f; }

        private void StabilizeDrift()
        {
            float yaw = Vector3.Dot(Body.angularVelocity, groundNormal);
            if (IsDrifting)
            {
                float targetSlip = -SteerInput * TargetDriftAngle;
                float desiredYaw = Mathf.Clamp((SlipAngle - targetSlip) * Mathf.Deg2Rad * 2.2f + SteerInput * .35f, -MaxYawRate, MaxYawRate);
                if (Mathf.Abs(SlipAngle) > MaxDriftAngle) desiredYaw = Mathf.Sign(SlipAngle) * MaxYawRate;
                Body.AddTorque(groundNormal * ((desiredYaw - yaw) * DriftAssistStrength), ForceMode.Acceleration);
            }
            float excess = yaw - Mathf.Clamp(yaw, -MaxYawRate, MaxYawRate);
            if (Mathf.Abs(excess) > .001f) Body.AddTorque(-groundNormal * excess, ForceMode.VelocityChange);
        }

        private void OnCollisionEnter(Collision collision) => RecordWall(collision);
        private void OnCollisionStay(Collision collision) => RecordWall(collision);
        private void RecordWall(Collision collision)
        {
            for (int i = 0; i < collision.contactCount; i++)
            {
                var contact = collision.GetContact(i);
                if (Mathf.Abs(Vector3.Dot(contact.normal, Vector3.up)) > .35f || preStepVelocity.magnitude < 8.33f) continue;
                float normalSpeed = Vector3.Dot(preStepVelocity, contact.normal);
                if (Mathf.Abs(normalSpeed) > preStepVelocity.magnitude * .65f) continue;
                WallPoint = contact.point; WallNormal = contact.normal; wallTime = Time.time;
                wallIncoming = preStepVelocity; pendingWall = true; break;
            }
        }

        private void ApplyWallSlide()
        {
            if (!pendingWall) return; pendingWall = false;
            Vector3 tangent = Vector3.ProjectOnPlane(wallIncoming, WallNormal);
            if (tangent.sqrMagnitude < 1) return;
            float lostAlongWall = Mathf.Clamp(tangent.magnitude - Vector3.Dot(Body.linearVelocity, tangent.normalized), 0, wallIncoming.magnitude);
            float inward = Mathf.Min(0, Vector3.Dot(Body.linearVelocity, WallNormal));
            Body.AddForce((tangent.normalized * lostAlongWall * WallSlideAssist - WallNormal * inward) * Mass, ForceMode.Impulse);
        }

        private void LateUpdate()
        {
            if (!Body) return;
            foreach (var wheel in Wheels)
            {
                if (!wheel.Visual) continue;
                wheel.spin = Mathf.Repeat(wheel.spin + ForwardSpeed / wheel.Radius * Mathf.Rad2Deg * Time.deltaTime +
                    (wheel.Driven && IsBurningOut ? ThrottleInput * 600 * Time.deltaTime : 0), 360);
                wheel.Visual.position = Mount(wheel) - transform.up * wheel.contact.SpringLength;
                wheel.Visual.localRotation = wheel.visualBindRotation * Quaternion.Euler(0, wheel.Steers ? wheelSteerAngle : 0, 0) * Quaternion.Euler(wheel.spin, 0, 0);
            }
        }

        private void OnDrawGizmosSelected()
        {
            if (Wheels == null) return;
            foreach (var wheel in Wheels)
            {
                if (wheel == null) continue;
                Vector3 origin = Mount(wheel);
                Gizmos.color = !Application.isPlaying || !wheel.contact.Grounded ? Color.blue : wheel.contact.Compression >= SuspensionTravel * .95f ? Color.red : Color.green;
                Gizmos.DrawLine(origin, origin - transform.up * (SuspensionRestLength + SuspensionTravel + wheel.Radius));
                Gizmos.DrawWireSphere(origin, .05f);
                if (Application.isPlaying && wheel.contact.Grounded) Gizmos.DrawSphere(wheel.contact.Point, .065f);
            }
            if (Application.isPlaying && Body) { Gizmos.color = Color.cyan; Gizmos.DrawRay(Body.worldCenterOfMass, Body.linearVelocity * .15f); }
        }
    }
}

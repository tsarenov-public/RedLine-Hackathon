using System;
using UnityEngine;

namespace Arcade.Vehicle
{
    [RequireComponent(typeof(Rigidbody))]
    public sealed class ArcadeVehicle : MonoBehaviour
    {
        [Header("Конфиг")]
        public VehicleTuning tuning;
        public WheelRig[] wheelRigs = new WheelRig[4];
        public LayerMask groundMask = 1;

        [Header("Визуал")]
        [Range(1f, 2.5f)] public float visualSteerMultiplier = 1.6f;

        public VehicleContext Ctx { get; private set; }
        public VehicleState State => Ctx.s;
        public Wheel[] Wheels => Ctx.wheels;
        public Rigidbody Rb { get; private set; }

        public event Action<Vector3, Vector3, float> WallHit;
        public event Action<bool> NosChanged;
        public event Action<bool> SpeedbreakerChanged;
        public event Action<int> GearChanged;

        IInputSource _input;
        IVehicleModule[] _modules;
        IRealtimeModule[] _realtime;

        void Awake()
        {
            if (tuning == null) { Debug.LogError("Не назначен Tuning!", this); enabled = false; return; }
            tuning.Recalculate();

            Rb = GetComponent<Rigidbody>();
            var ch = tuning.chassis;
            Rb.mass = ch.mass;
            Rb.centerOfMass = ch.centerOfMass;
            Rb.inertiaTensor = Vector3.Scale(Rb.inertiaTensor, ch.inertiaScale);
            Rb.inertiaTensorRotation = Quaternion.identity;
            Rb.linearDamping = 0f;
            Rb.angularDamping = 0.05f;
            Rb.interpolation = RigidbodyInterpolation.Interpolate;
            Rb.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
            Rb.maxAngularVelocity = 12f;
            Rb.solverIterations = 8;

            var wheels = new Wheel[wheelRigs.Length];
            for (int i = 0; i < wheels.Length; i++)
            {
                var r = wheelRigs[i];
                if (r.mount == null) { Debug.LogError($"WheelRig {i}: не назначен mount", this); enabled = false; return; }
                wheels[i] = new Wheel
                {
                    rig = r,
                    mountLocal = transform.InverseTransformPoint(r.mount.position),
                    visualRestLocal = r.visual != null ? r.visual.localPosition : Vector3.zero,
                    suspLength = tuning.suspension.restLength
                };
            }

            Ctx = new VehicleContext
            {
                vehicle = this, rb = Rb, t = transform, cfg = tuning,
                wheels = wheels, groundMask = groundMask
            };

            foreach (var mb in GetComponents<MonoBehaviour>())
                if (mb is IInputSource src) { _input = src; break; }

            var sb = new SpeedbreakerModule();
            _modules = new IVehicleModule[]
            {
                new SteeringModule(), new SuspensionModule(), new EngineModule(),
                new GearboxModule(), new TireModule(), new DriftModule(),
                new StabilityModule(), new NosModule(), sb
            };
            _realtime = new IRealtimeModule[] { sb };
            foreach (var m in _modules) m.Init(Ctx);
        }

        void FixedUpdate()
        {
            float dt = Time.fixedDeltaTime;
            Ctx.dt = dt;
            Ctx.input = _input != null ? _input.Sample(dt) : default;
            if (Ctx.input.BrakeOnly) Ctx.s.reversing = false;
            UpdateKinematics();
            for (int i = 0; i < _modules.Length; i++) _modules[i].FixedTick(dt);
        }

        void UpdateKinematics()
        {
            var s = Ctx.s;
            Vector3 v = Rb.linearVelocity;
            Vector3 flat = Vector3.ProjectOnPlane(v, Vector3.up);
            Vector3 fwdFlat = Vector3.ProjectOnPlane(transform.forward, Vector3.up).normalized;

            s.velocity = v;
            s.speed = flat.magnitude;
            s.speedKmh = s.speed * 3.6f;
            s.forwardSpeed = Vector3.Dot(v, transform.forward);
            s.lateralSpeed = Vector3.Dot(v, transform.right);
            s.yawRate = Vector3.Dot(Rb.angularVelocity, transform.up);
            s.bodySlipDeg = s.speed > 1f ? Vector3.SignedAngle(fwdFlat, flat.normalized, Vector3.up) : 0f;

            float th = Ctx.input.Throttle, br = Ctx.input.Brake;
            if (s.reversing) { th = Ctx.input.Brake; br = Ctx.input.Throttle; }
            s.throttle = Mathf.Clamp01(th);
            s.brake = Mathf.Clamp01(br);
        }

        void Update()
        {
            float udt = Time.unscaledDeltaTime;
            for (int i = 0; i < _realtime.Length; i++) _realtime[i].RealtimeTick(udt);
            UpdateWheelVisuals(Time.deltaTime);
        }

        void UpdateWheelVisuals(float dt)
        {
            foreach (var w in Ctx.wheels)
            {
                if (w.rig.visual == null) continue;
                w.spinAngle = Mathf.Repeat(w.spinAngle + w.spinRate * dt * Mathf.Rad2Deg, 360f);
                w.rig.visual.localRotation =
                    Quaternion.Euler(0f, w.steerAngle * visualSteerMultiplier, 0f) *
                    Quaternion.Euler(w.spinAngle, 0f, 0f) *
                    w.rig.modelOffset;
                w.rig.visual.localPosition = new Vector3(w.visualRestLocal.x, -w.suspLength, w.visualRestLocal.z);
            }
        }

        void OnCollisionEnter(Collision c)
        {
            float imp = c.impulse.magnitude;
            if (imp < tuning.chassis.wallHitMinImpulse) return;
            var contact = c.GetContact(0);
            Vector3 n = contact.normal;
            float headOn = Mathf.Clamp01(-Vector3.Dot(Rb.linearVelocity.normalized, n));
            float loss = Mathf.Lerp(tuning.chassis.scrapeSpeedLoss, tuning.chassis.impactSpeedLoss, headOn);
            Rb.linearVelocity *= 1f - loss;
            Rb.angularVelocity *= 0.6f;
            WallHit?.Invoke(contact.point, n, imp);
        }

        public void AddNos(float amount) => State.nos = Mathf.Clamp01(State.nos + amount);
        internal void NotifyNos(bool on) => NosChanged?.Invoke(on);
        internal void NotifySpeedbreaker(bool on) => SpeedbreakerChanged?.Invoke(on);
        internal void NotifyGear(int g) => GearChanged?.Invoke(g);

        void OnDisable()
        {
            if (_realtime != null) foreach (var m in _realtime) m.Restore();
        }
    }
}

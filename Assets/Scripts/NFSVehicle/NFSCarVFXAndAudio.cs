using UnityEngine;

namespace RedLine.NFS
{
    [DisallowMultipleComponent, RequireComponent(typeof(NFSRaycastCarController))]
    public sealed class NFSCarVFXAndAudio : MonoBehaviour
    {
        [Header("Vehicle")]
        [Tooltip("Automatically resolved on this object if omitted.")] public NFSRaycastCarController Car;
        [Header("Rear tyres — array order: rear left, rear right")]
        [Tooltip("Optional world-space smoke emitters.")] public ParticleSystem[] RearSmoke = new ParticleSystem[2];
        [Tooltip("Optional skid trails. Their points follow actual ground contacts.")] public TrailRenderer[] RearSkids = new TrailRenderer[2];
        [Range(3, 45), Tooltip("Minimum sideways slip for tyre effects, degrees.")] public float SmokeSlipAngle = 12;
        [Min(0), Tooltip("Minimum road speed for handbrake smoke/trails, km/h.")] public float MinimumSkidSpeed = 12;
        [Range(.005f, .1f), Tooltip("Distance of tyre marks above the contact surface, metres.")] public float SurfaceOffset = .02f;
        [Header("Wall sparks and NOS flames")]
        [Tooltip("Optional emitter for shallow wall collisions.")] public ParticleSystem WallSparks;
        [Tooltip("Optional exhaust flame emitters.")] public ParticleSystem[] NitroFlames = new ParticleSystem[0];
        [Min(0), Tooltip("Minimum wall-sliding speed, km/h.")] public float SparkMinimumSpeed = 30;
        [Header("Audio — every source and clip is optional")]
        [Tooltip("Looping engine source.")] public AudioSource EngineAudio;
        [Tooltip("Looping tyre-squeal source.")] public AudioSource TyreAudio;
        [Range(.1f, 2), Tooltip("Engine pitch at idle.")] public float IdlePitch = .7f;
        [Range(1, 4), Tooltip("Engine pitch at redline.")] public float RedlinePitch = 2.2f;
        [Range(1, 25), Tooltip("Audio smoothing speed, 1/s.")] public float AudioResponse = 10;

        private float sparkTime = -100;
        private Vector3 sparkPoint, sparkNormal;

        private void Awake()
        {
            if (!Car) Car = GetComponent<NFSRaycastCarController>();
            StartLoop(EngineAudio); StartLoop(TyreAudio);
        }

        private void OnEnable()
        {
            if (!Car) Car = GetComponent<NFSRaycastCarController>();
            StartLoop(EngineAudio); StartLoop(TyreAudio);
        }

        private static void StartLoop(AudioSource source)
        { if (source && source.clip) { source.loop = true; if (!source.isPlaying) source.Play(); } }

        private void LateUpdate()
        {
            if (!Car || !Car.Body) return;
            bool sliding = Car.SpeedKmh > MinimumSkidSpeed && (Mathf.Abs(Car.SlipAngle) > SmokeSlipAngle || Car.HandbrakeInput);
            bool braking = Car.SpeedKmh > MinimumSkidSpeed && Car.BrakeInput > .35f && Car.CurrentGear > 0;
            for (int i = 0; i < 2; i++)
            {
                var contact = Car.GetWheelContact(i + 2);
                bool smoke = contact.Grounded && (sliding || Car.IsBurningOut);
                if (RearSmoke != null && i < RearSmoke.Length && RearSmoke[i])
                {
                    if (contact.Grounded) RearSmoke[i].transform.position = contact.Point + contact.Normal * .07f;
                    SetEmission(RearSmoke[i], smoke);
                }
                if (RearSkids != null && i < RearSkids.Length && RearSkids[i])
                {
                    var trail = RearSkids[i]; bool emit = contact.Grounded && (sliding || braking || Car.IsBurningOut);
                    if (contact.Grounded)
                    {
                        trail.transform.SetPositionAndRotation(contact.Point + contact.Normal * SurfaceOffset,
                            Quaternion.LookRotation(contact.Normal, Vector3.ProjectOnPlane(transform.forward, contact.Normal).normalized));
                    }
                    if (emit && !trail.emitting) trail.Clear(); // Do not bridge separate contact patches after a jump.
                    trail.emitting = emit;
                }
            }
            if (NitroFlames != null) foreach (var flame in NitroFlames) SetEmission(flame, Car.IsNitroActive);
            bool sparks = Time.time - sparkTime < .1f && Car.SpeedKmh > SparkMinimumSpeed;
            if (WallSparks && sparks)
            {
                Vector3 direction = -Car.Body.linearVelocity.normalized + sparkNormal * .4f;
                WallSparks.transform.SetPositionAndRotation(sparkPoint + sparkNormal * .03f, Quaternion.LookRotation(direction, Vector3.up));
            }
            SetEmission(WallSparks, sparks);
            float blend = 1 - Mathf.Exp(-AudioResponse * Time.deltaTime);
            if (EngineAudio)
            {
                float rpm = Mathf.InverseLerp(Car.IdleRPM, Car.MaxRPM, Car.CurrentRPM);
                float pitch = Mathf.Lerp(IdlePitch, RedlinePitch, rpm) * (Car.IsShifting ? .82f : 1);
                EngineAudio.pitch = Mathf.Lerp(EngineAudio.pitch, pitch, blend);
                EngineAudio.volume = Mathf.Lerp(EngineAudio.volume, .3f + Car.ThrottleInput * .45f, blend);
            }
            if (TyreAudio)
            {
                float volume = Car.GroundedWheelCount > 0 ? Mathf.InverseLerp(6, 45, Mathf.Abs(Car.SlipAngle)) : 0;
                if (Car.GroundedWheelCount > 0 && (Car.HandbrakeInput || Car.IsBurningOut)) volume = Mathf.Max(volume, .65f);
                volume *= Mathf.InverseLerp(3, 35, Car.SpeedKmh);
                TyreAudio.volume = Mathf.Lerp(TyreAudio.volume, volume * .7f, blend);
                TyreAudio.pitch = Mathf.Lerp(.9f, 1.3f, Mathf.Clamp01(Car.SpeedKmh / 180));
            }
        }

        private static void SetEmission(ParticleSystem system, bool active)
        {
            if (!system) return;
            var emission = system.emission; emission.enabled = active;
            if (active && !system.isPlaying) system.Play();
            else if (!active && system.isPlaying) system.Stop(true, ParticleSystemStopBehavior.StopEmitting);
        }

        private void OnCollisionStay(Collision collision)
        {
            if (!Car || Car.SpeedKmh < SparkMinimumSpeed) return;
            for (int i = 0; i < collision.contactCount; i++)
            {
                var contact = collision.GetContact(i);
                if (Mathf.Abs(Vector3.Dot(contact.normal, Vector3.up)) > .35f) continue;
                sparkPoint = contact.point; sparkNormal = contact.normal; sparkTime = Time.time; break;
            }
        }

        private void OnDisable()
        {
            if (RearSmoke != null) foreach (var smoke in RearSmoke) SetEmission(smoke, false);
            if (NitroFlames != null) foreach (var flame in NitroFlames) SetEmission(flame, false);
            if (RearSkids != null) foreach (var trail in RearSkids) if (trail) trail.emitting = false;
            SetEmission(WallSparks, false);
            if (EngineAudio) EngineAudio.Stop(); if (TyreAudio) TyreAudio.Stop();
        }
    }
}

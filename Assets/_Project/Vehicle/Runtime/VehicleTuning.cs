using System;
using UnityEngine;

namespace Arcade.Vehicle
{
    [CreateAssetMenu(menuName = "Arcade Vehicle/Tuning", fileName = "Tuning_TEMPLATE")]
    public sealed class VehicleTuning : ScriptableObject
    {
        [Tooltip("Выключи, если хочешь править все параметры вручную")]
        public bool autoCalculate = true;

        public CharacterSection character = new();
        public ChassisSection chassis = new();
        public SuspensionSection suspension = new();
        public TireSection tires = new();
        public SteeringSection steering = new();
        public EngineSection engine = new();
        public GearboxSection gearbox = new();
        public AssistSection assists = new();
        public AeroSection aero = new();
        public NosSection nos = new();
        public SpeedbreakerSection speedbreaker = new();

        static AnimationCurve C(params (float t, float v)[] p)
        {
            var k = new Keyframe[p.Length];
            for (int i = 0; i < p.Length; i++) k[i] = new Keyframe(p[i].t, p[i].v);
            var c = new AnimationCurve(k);
            for (int i = 0; i < p.Length; i++) c.SmoothTangents(i, 0f);
            return c;
        }

        void OnValidate() => Recalculate();

        // Идемпотентно: считается из "характера" и геометрии, можно звать каждый кадр
        public void Recalculate()
        {
            if (!autoCalculate) return;

            const float G = 9.81f;
            float m = Mathf.Max(200f, chassis.mass);
            float L = Mathf.Max(1f, chassis.wheelbase);
            float r = Mathf.Max(0.15f, chassis.wheelRadius);
            float b = Mathf.Clamp(suspension.frontWeightBias, 0.35f, 0.65f);
            float travel = Mathf.Max(0.08f, suspension.restLength);

            chassis.centerOfMass = new Vector3(0f, r * character.cgHeightPerRadius, L * (b - 0.5f));
            chassis.rideHeight = r + travel * 0.5f;

            float w = 2f * Mathf.PI * character.rideFrequencyHz;
            float kF = w * w * (m * b * 0.5f);
            float kR = w * w * (m * (1f - b) * 0.5f);
            float kAvg = (kF + kR) * 0.5f;
            float mAvg = m * 0.25f;

            suspension.stiffness = kAvg * travel;
            suspension.bumpDamping = 2f * character.bumpDampingRatio * Mathf.Sqrt(kAvg * mAvg);
            suspension.reboundDamping = 2f * character.reboundDampingRatio * Mathf.Sqrt(kAvg * mAvg);
            suspension.antiRoll = suspension.stiffness * character.antiRollRatio;
            suspension.antiRollMax = suspension.antiRoll;
            suspension.stickExtraTravel = travel * 0.4f;

            tires.brakeForce = m * G * character.brakeDecelG;

            // ---- л.с. -> момент: ищем обороты пиковой мощности ----
            float bestRpm = engine.idleRpm, bestProd = 0f;
            for (float rpm = engine.idleRpm; rpm <= engine.redlineRpm; rpm += 25f)
            {
                float p = engine.torqueShape.Evaluate(rpm) * rpm;
                if (p > bestProd) { bestProd = p; bestRpm = rpm; }
            }
            float omega = bestRpm * 2f * Mathf.PI / 60f;
            float shape = Mathf.Max(0.05f, engine.torqueShape.Evaluate(bestRpm));
            engine.peakTorque = engine.horsePower * 735.5f / Mathf.Max(1f, shape * omega);
            engine.powerPeakRpm = bestRpm;

            // ---- аэро ----
            if (chassis.bodyWidth > 0.1f && chassis.bodyHeight > 0.1f)
                aero.frontalArea = chassis.bodyWidth * chassis.bodyHeight * 0.85f;
            aero.dragCoeff = 0.6f * aero.dragCd * Mathf.Max(0.5f, aero.frontalArea);
            aero.downforceCoeff = m * character.downforcePerKg;
            aero.staticDownforce = m * 0.2f;

            // ---- максималка из мощности, КПП под неё ----
            float powerW = engine.horsePower * 735.5f * gearbox.efficiency;
            float vMax = Mathf.Pow(powerW / Mathf.Max(0.05f, aero.dragCoeff), 1f / 3f);
            vMax *= Mathf.Clamp(character.topSpeedFactor, 0.6f, 1.4f);
            engine.topSpeedKmh = vMax * 3.6f;

            int n = gearbox.forwardRatios != null ? gearbox.forwardRatios.Length : 0;
            float topGear = n > 0 ? Mathf.Max(0.2f, gearbox.forwardRatios[n - 1]) : 0.8f;
            gearbox.finalDrive = Mathf.Clamp(
                bestRpm * 2f * Mathf.PI * r / (60f * Mathf.Max(5f, vMax) * topGear), 1.5f, 9f);
        }

        [Serializable] public sealed class CharacterSection
        {
            public string archetypeName = "Muscle RWD";
            [Range(1.0f, 3.0f)] public float rideFrequencyHz = 1.7f;
            [Range(0.15f, 0.7f)] public float bumpDampingRatio = 0.30f;
            [Range(0.2f, 0.9f)] public float reboundDampingRatio = 0.45f;
            [Range(0.2f, 0.8f)] public float antiRollRatio = 0.45f;
            public float cgHeightPerRadius = 1.05f;
            public float brakeDecelG = 1.30f;
            public float downforcePerKg = 0.0006f;
            [Range(0.6f, 1.4f)] public float topSpeedFactor = 1.0f;
            [Header("Эталон для масштаба руля")]
            public float refWheelbase = 2.91f;
        }

        [Serializable] public sealed class ChassisSection
        {
            public float mass = 1600f;
            [Header("Геометрия — заполняет визард")]
            public float wheelbase = 2.91f;
            public float track = 1.65f;
            public float wheelRadius = 0.345f;
            public float bodyWidth = 2.24f;
            public float bodyHeight = 1.64f;
            public float bodyLength = 5.21f;
            [Header("Считается автоматически")]
            public Vector3 centerOfMass = new(0f, 0.36f, 0.06f);
            public float rideHeight = 0.5f;
            [Header("Вручную")]
            public Vector3 inertiaScale = new(1.15f, 1.6f, 1.15f);
            [Range(0f, 0.5f)] public float scrapeSpeedLoss = 0.10f;
            [Range(0f, 0.8f)] public float impactSpeedLoss = 0.45f;
            public float wallHitMinImpulse = 2500f;
        }

        [Serializable] public sealed class SuspensionSection
        {
            [Header("Вручную")]
            public float restLength = 0.30f;
            [Range(0.4f, 0.6f)] public float frontWeightBias = 0.52f;
            public float maxLoadMult = 3.5f;
            [Range(0f, 0.5f)] public float stickForce = 0.25f;
            [Header("Считается автоматически")]
            public float stiffness = 15000f;
            public float bumpDamping = 2800f;
            public float reboundDamping = 4200f;
            public float antiRoll = 6700f;
            public float antiRollMax = 6700f;
            public float stickExtraTravel = 0.12f;
        }

        [Serializable] public sealed class TireSection
        {
            [Header("Сцепление")]
            public float baseGrip = 1.55f;
            [Range(0f, 0.3f)] public float loadSensitivity = 0.12f;
            public AnimationCurve lateralGrip = C((0,0),(3,0.62f),(7,1f),(14,0.94f),(30,0.92f),(60,0.90f),(90,0.88f));
            public float lateralScale = 1f;
            public float frontGripBias = 1.05f;
            public float rearGripBias = 1.00f;

            [Header("Сохранение скорости в заносе")]
            [Range(0f, 0.95f)] public float driftScrubRetention = 0.70f;
            [Range(1f, 3f)] public float driftEllipseBoost = 1.70f;
            [Range(0f, 1f)] public float driftThrottleBoost = 0.35f;

            [Header("Ручник")]
            [Range(0.1f, 1f)] public float handbrakeLateralGrip = 0.32f;
            [Range(0f, 1f)] public float handbrakeLock = 0.60f;
            public float handbrakeFadeTime = 0.5f;

            [Header("Тормоза и сопротивление")]
            public float brakeForce = 20400f;   // считается
            [Range(0.4f, 0.8f)] public float brakeBias = 0.62f;
            public float rollingResistance = 0.015f;
            public float engineBrake = 260f;
            [Range(0.5f, 1f)] public float tractionLimit = 0.95f;
            [Range(1.5f, 4f)] public float ellipsePower = 2.2f;

            [Header("Стабильность")]
            public float slipEpsilon = 1.6f;
            public float relaxLateral = 0.07f;
            public float relaxLongitudinal = 0.04f;
            [Range(0.3f, 1f)] public float impulseClamp = 0.9f;

            [Header("FX")]
            public float slipFxStartDeg = 6f;
            public float slipFxFullDeg = 22f;
            public float wheelSpinExtra = 45f;
        }

        [Serializable] public sealed class SteeringSection
        {
            public AnimationCurve maxAngleByKmh = C((0,32),(40,20),(80,13),(120,9.5f),(180,7),(280,5));
            [Range(1f, 2.5f)] public float inputExponent = 1.35f;
            public float inputSmooth = 0.09f;
            public float inputReturnSmooth = 0.06f;
            public float rateLowSpeed = 200f;
            public float rateHighSpeed = 90f;
            [Range(0f, 1.5f)] public float counterSteer = 0.9f;
            public float counterSteerGain = 0.55f;
            public float counterSteerLimit = 18f;
            [Range(0f, 1f)] public float ackermann = 0.6f;
        }

        [Serializable] public sealed class EngineSection
        {
            [Header("ГЛАВНОЕ — мощность")]
            public float horsePower = 250f;
            [Tooltip("Форма кривой момента, пик = 1.0. Масштаб считается из л.с.")]
            public AnimationCurve torqueShape = C((0,0.25f),(1000,0.55f),(2500,0.80f),(4200,1f),(6000,0.93f),(7500,0.70f),(8200,0.25f));
            public float idleRpm = 900f;
            public float redlineRpm = 7600f;
            public float rpmResponse = 0.08f;
            [Header("Считается автоматически (не менять)")]
            public float peakTorque = 315f;
            public float powerPeakRpm = 6000f;
            public float topSpeedKmh = 236f;
        }

        [Serializable] public sealed class GearboxSection
        {
            public float[] forwardRatios = { 3.1f, 2.05f, 1.5f, 1.15f, 0.9f, 0.72f };
            public float reverseRatio = 3.0f;
            [Range(0.7f, 1f)] public float efficiency = 0.92f;
            public float upshiftRpm = 6900f;
            public float downshiftRpm = 3000f;
            public float shiftTime = 0.22f;
            public bool awd = false;
            [Range(0.1f, 0.9f)] public float frontTorqueSplit = 0.4f;
            [Header("Считается автоматически")]
            public float finalDrive = 4.6f;
        }

        [Serializable] public sealed class AssistSection
        {
            public float yawDamping = 1.8f;
            public float spinThreshold = 50f;
            public float spinSaveGain = 0.18f;
            public float alignGain = 0.03f;
            public float handbrakeKick = 4.0f;
            public float handbrakeKickTime = 0.25f;
            public float driftMinAngle = 8f;
            public float driftFullAngle = 45f;
            public float driftRampTime = 0.2f;
        }

        [Serializable] public sealed class AeroSection
        {
            [Header("Вручную")]
            public float dragCd = 0.42f;
            public float rollDamping = 3.5f;
            public float pitchDamping = 2.5f;
            public float groundAlignGain = 6f;
            public float stickTime = 0.25f;
            public float stickDistance = 0.9f;
            public float stickAccel = 12f;
            public float airAlignDelay = 0.12f;
            public float airAlignGain = 5f;
            public float airAlignSmooth = 0.15f;
            public float airYawControl = 1.6f;
            public float airPitchControl = 2.2f;
            [Range(-0.5f, 0.5f)] public float flipDot = 0.15f;
            public float flipResetTime = 1.5f;
            [Header("Считается автоматически")]
            public float frontalArea = 3.1f;
            public float dragCoeff = 0.78f;
            public float downforceCoeff = 0.96f;
            public float staticDownforce = 320f;
        }

        [Serializable] public sealed class NosSection
        {
            public float duration = 3f;
            [Range(0f, 0.5f)] public float minToActivate = 0.15f;
            public float torqueMult = 1.7f;
            public float dragMult = 0.85f;
            public float driftFill = 0.10f;
            public float airFill = 0.12f;
            public float passiveFill = 0.012f;
            public bool requireHold = true;
            public float rampTime = 0.15f;
        }

        [Serializable] public sealed class SpeedbreakerSection
        {
            [Range(0.15f, 0.8f)] public float timeScale = 0.32f;
            public float duration = 4f;
            public float refillRate = 0.06f;
            public float blend = 0.08f;
            public float steerMult = 1.5f;
            public float gripMult = 1.08f;
        }
    }
}
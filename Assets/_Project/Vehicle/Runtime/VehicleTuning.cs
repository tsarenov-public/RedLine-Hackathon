using System;
using UnityEngine;

namespace Arcade.Vehicle
{
    [CreateAssetMenu(menuName = "Arcade Vehicle/Tuning", fileName = "Tuning_Car")]
    public sealed class VehicleTuning : ScriptableObject
    {
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
            for (int i =  0; i < p.Length; i++) c.SmoothTangents(i, 0f);
            return c;
        }

        [Serializable] public sealed class ChassisSection
        {
            public float mass = 1300f;
            public Vector3 centerOfMass = new(0f, 0.35f, 0.05f);   // root на уровне земли!
            public Vector3 inertiaScale = new(1.15f, 1.6f, 1.15f);  // Y = yaw, тяжелее крутиться
            [Range(0f, 0.5f)] public float scrapeSpeedLoss = 0.10f; // скользящий удар о стену
            [Range(0f, 0.8f)] public float impactSpeedLoss = 0.45f; // лобовой удар
            public float wallHitMinImpulse = 2500f;
            public float rideHeight = 0.45f;                        // для авто-сброса
        }

        [Serializable] public sealed class SuspensionSection
        {
            public float restLength = 0.28f;
            public float stiffness = 12000f;      // Н на полный ход
            public float bumpDamping = 2400f;
            public float reboundDamping = 3400f;
            public float antiRoll = 6000f;
            public float antiRollMax = 6000f;
            public float maxLoadMult = 3.5f;
            [Range(0.4f, 0.6f)] public float frontWeightBias = 0.52f;
            public float stickExtraTravel = 0.12f; // «дотягивание» стойки после гребня
            [Range(0f, 0.5f)] public float stickForce = 0.25f;
        }

        [Serializable] public sealed class TireSection
        {
            public float baseGrip = 1.5f;
            [Range(0f, 0.3f)] public float loadSensitivity = 0.12f;
            // x = |slip angle| в градусах, y = нормированная сила (0..1)
            public AnimationCurve lateralGrip = C((0,0),(3,0.62f),(7,1f),(14,0.93f),(30,0.87f),(60,0.83f),(90,0.80f));
            public float lateralScale = 1f;
            public float frontGripBias = 1.05f;
            public float rearGripBias = 1.00f;
            [Range(0.1f, 1f)] public float handbrakeLateralGrip = 0.35f;
            [Range(0f, 1f)] public float handbrakeLock = 0.9f;
            public float brakeForce = 18000f;
            [Range(0.4f, 0.8f)] public float brakeBias = 0.62f;
            public float rollingResistance = 0.015f;
            public float engineBrake = 260f;
            [Range(0.5f, 1f)] public float tractionLimit = 0.95f;
            [Range(1.5f, 4f)] public float ellipsePower = 2.2f;
            public float slipEpsilon = 1.6f;
            public float relaxLateral = 0.07f;      // ← главный анти-джиттер
            public float relaxLongitudinal = 0.04f;
            [Range(0.3f, 1f)] public float impulseClamp = 0.9f;
            public float slipFxStartDeg = 6f;
            public float slipFxFullDeg = 22f;
            public float wheelSpinExtra = 45f;
        }

        [Serializable] public sealed class SteeringSection
        {
            // x = км/ч, y = максимальный угол колёс в градусах
            public AnimationCurve maxAngleByKmh = C((0,32),(40,20),(80,13),(120,9.5f),(180,7),(280,5));
            [Range(1f, 2.5f)] public float inputExponent = 1.35f;
            public float inputSmooth = 0.09f;
            public float inputReturnSmooth = 0.06f;
            public float rateLowSpeed = 200f;
            public float rateHighSpeed = 90f;
            [Range(0f, 1.5f)] public float counterSteer = 0.7f;
            public float counterSteerGain = 0.55f;
            public float counterSteerLimit = 18f;
            [Range(0f, 1f)] public float ackermann = 0.6f;
        }

        [Serializable] public sealed class EngineSection
        {
            public AnimationCurve torqueByRpm = C((0,120),(1000,260),(2500,380),(4200,430),(6000,400),(7500,300),(8200,120));
            public float idleRpm = 900f;
            public float redlineRpm = 7600f;
            public float rpmResponse = 0.08f;
        }

        [Serializable] public sealed class GearboxSection
        {
            public float[] forwardRatios = { 3.1f, 2.05f, 1.5f, 1.15f, 0.9f, 0.72f };
            public float reverseRatio = 3.0f;
            public float finalDrive = 3.4f;
            [Range(0.7f, 1f)] public float efficiency = 0.92f;
            public float upshiftRpm = 6900f;
            public float downshiftRpm = 3000f;
            public float shiftTime = 0.22f;
            public bool awd = false;
            [Range(0.1f, 0.9f)] public float frontTorqueSplit = 0.4f;
        }

        [Serializable] public sealed class AssistSection
        {
            public float yawDamping = 2.2f;
            public float spinThreshold = 45f;      // градуса body slip
            public float spinSaveGain = 0.22f;     // рад/с² на градус превышения
            public float alignGain = 0.06f;
            public float handbrakeKick = 3.5f;
            public float handbrakeKickTime = 0.25f;
            public float driftMinAngle = 8f;
            public float driftFullAngle = 45f;
        }

        [Serializable] public sealed class AeroSection
        {
            public float dragCoeff = 0.62f;
            public float downforceCoeff = 1.0f;
            public float staticDownforce = 300f;
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
        }

        [Serializable] public sealed class NosSection
        {
            public float duration = 3f;
            [Range(0f, 0.5f)] public float minToActivate = 0.15f;
            public float torqueMult = 1.65f;
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
using System;
using UnityEngine;

namespace Arcade.Vehicle
{
    public struct VehicleInputState
    {
        public float Throttle, Brake, Steer;
        public bool Handbrake, Nos, Speedbreaker, Reset, BrakeOnly;
    }

    public interface IInputSource { VehicleInputState Sample(float dt); }

    public interface IVehicleModule
    {
        void Init(VehicleContext ctx);
        void FixedTick(float dt);
    }

    public interface IRealtimeModule { void RealtimeTick(float unscaledDt); void Restore(); }

    [Serializable]
    public sealed class WheelRig
    {
        public Transform mount;
        public Transform visual;
        public float radius = 0.345f;
        public bool steered;
        public bool driven = true;
        public bool handbraked;
        [Range(0.6f, 1.4f)] public float gripScale = 1f;
        public Quaternion modelOffset = Quaternion.identity;
    }

    public sealed class Wheel
    {
        public WheelRig rig;
        public Vector3 mountLocal, visualRestLocal;
        public bool grounded, wasGrounded;
        public Vector3 contactPoint, contactNormal = Vector3.up, axis, forward, right;
        public float suspLength, compression, load, staticLoad;
        public float steerAngle, vLong, vLat, slipAngleDeg, slipIntensity, longSlip;
        public float driveRequest;
        public Vector2 force;
        public float spinAngle, spinRate, lastGroundTime;
        public float Radius => rig.radius;
    }

    public sealed class VehicleState
    {
        public Vector3 velocity, groundNormal = Vector3.up;
        public float speed, speedKmh, forwardSpeed, lateralSpeed, yawRate, bodySlipDeg;
        public int groundedWheels;
        public float airTime, groundedTime, handbrakeTime;
        public bool drifting; public float driftAmount, driftScore, driftTime;
        public float rpm, engineTorque, throttle, brake;
        public int gear; public float gearRatio; public bool shifting, reversing;
        public float nos = 0.5f, nosActive;
        public bool speedbreaker; public float speedbreakerMeter = 1f;
        public float steerDeg, gripMult = 1f, steerMult = 1f;
    }

    public sealed class VehicleContext
    {
        public ArcadeVehicle vehicle;
        public Rigidbody rb;
        public Transform t;
        public VehicleTuning cfg;
        public Wheel[] wheels;
        public LayerMask groundMask;
        public VehicleInputState input;
        public readonly VehicleState s = new();
        public float dt;
    }
}

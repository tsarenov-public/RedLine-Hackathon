using UnityEngine;

namespace Arcade.Vehicle
{
    public sealed class DriftModule : IVehicleModule
    {
        VehicleContext c;
        public void Init(VehicleContext ctx) => c = ctx;

        public void FixedTick(float dt)
        {
            var a = c.cfg.assists;
            var s = c.s;
            float speedF = Mathf.Clamp01(s.speedKmh / 70f);
            bool hb = c.input.Handbrake;
            s.handbrakeTime = hb ? s.handbrakeTime + dt : 0f;

            float slip = s.bodySlipDeg;
            float abs = Mathf.Abs(slip);
            float sign = Mathf.Sign(slip);

            float yawAcc = -s.yawRate * a.yawDamping * speedF;

            if (hb && s.handbrakeTime < a.handbrakeKickTime && s.speed > 8f)
                yawAcc += c.input.Steer * a.handbrakeKick * speedF;

            float excess = Mathf.Max(0f, abs - a.spinThreshold);
            if (excess > 0f) yawAcc += sign * excess * a.spinSaveGain * speedF;

            if (!hb)
                yawAcc += sign * Mathf.Min(abs, 30f) * a.alignGain
                        * (1f - 0.7f * Mathf.Abs(c.input.Steer)) * speedF;

            c.rb.AddTorque(c.t.up * yawAcc, ForceMode.Acceleration);

            bool raw = s.speed > 8f && abs > a.driftMinAngle && s.groundedWheels >= 2;
            float target = raw ? Mathf.Clamp01(abs / a.driftFullAngle) : 0f;
            s.drifting = target > 0.05f;
            s.driftAmount = Mathf.MoveTowards(s.driftAmount, target, dt / Mathf.Max(0.02f, a.driftRampTime));

            if (s.drifting)
            {
                s.driftTime += dt;
                s.driftScore += abs * s.speed * dt * 0.1f;
                s.nos = Mathf.Clamp01(s.nos + c.cfg.nos.driftFill * dt * s.driftAmount);
            }
            else s.driftTime = 0f;

            if (s.airTime > 0.3f) s.nos = Mathf.Clamp01(s.nos + c.cfg.nos.airFill * dt);
        }
    }
}
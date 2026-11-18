using UnityEngine;

namespace Arcade.Vehicle
{
    public sealed class DriftModule : IVehicleModule
    {
        VehicleContext c;
        float _hbTime;
        public void Init(VehicleContext ctx) => c = ctx;

        public void FixedTick(float dt)
        {
            var a = c.cfg.assists;
            var s = c.s;
            float speedF = Mathf.Clamp01(s.speedKmh / 70f);
            bool hb = c.input.Handbrake;
            _hbTime = hb ? _hbTime + dt : 0f;

            float slip = s.bodySlipDeg;
            float abs = Mathf.Abs(slip);
            float sign = Mathf.Sign(slip);

            // демпфер рыскания — база стабильности
            float yawAcc = -s.yawRate * a.yawDamping * speedF;

            // кик от ручника: инициируем занос
            if (hb && _hbTime < a.handbrakeKickTime && s.speed > 8f)
                yawAcc += c.input.Steer * a.handbrakeKick * speedF;

            // анти-спин: мёртвая зона до порога, дальше P-регулятор
            float excess = Mathf.Max(0f, abs - a.spinThreshold);
            if (excess > 0f) yawAcc += sign * excess * a.spinSaveGain * speedF;

            // авто-выравнивание на выходе (не мешает держать дрифт)
            if (!hb)
                yawAcc += sign * Mathf.Min(abs, 30f) * a.alignGain
                        * (1f - 0.7f * Mathf.Abs(c.input.Steer)) * speedF;

            c.rb.AddTorque(c.t.up * yawAcc, ForceMode.Acceleration);

            // состояние дрифта +充填 NOS
            bool drifting = s.speed > 8f && abs > a.driftMinAngle && s.groundedWheels >= 2;
            s.drifting = drifting;
            s.driftAmount = Mathf.Clamp01(abs / a.driftFullAngle);
            if (drifting)
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
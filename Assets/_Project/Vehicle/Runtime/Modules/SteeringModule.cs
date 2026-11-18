using UnityEngine;

namespace Arcade.Vehicle
{
    public sealed class SteeringModule : IVehicleModule
    {
        VehicleContext c;
        float _raw, _rawVel, _angle;

        public void Init(VehicleContext ctx) => c = ctx;

        public void FixedTick(float dt)
        {
            var st = c.cfg.steering;
            var s = c.s;

            float target = Mathf.Clamp(c.input.Steer, -1f, 1f);
            target = Mathf.Sign(target) * Mathf.Pow(Mathf.Abs(target), st.inputExponent);
            float smooth = Mathf.Abs(target) > Mathf.Abs(_raw) ? st.inputSmooth : st.inputReturnSmooth;
            _raw = Mathf.SmoothDamp(_raw, target, ref _rawVel, smooth, Mathf.Infinity, dt);

            float maxAngle = st.maxAngleByKmh.Evaluate(s.speedKmh) * s.steerMult;
            float desired = _raw * maxAngle;

            // авто-контрруль: рулим в сторону скольжения, но игрок всегда главнее
            if (st.counterSteer > 0f && s.speed > 4f)
            {
                float assist = Mathf.Clamp(s.bodySlipDeg * st.counterSteerGain,
                                           -st.counterSteerLimit, st.counterSteerLimit);
                assist *= st.counterSteer * (1f - 0.6f * Mathf.Abs(_raw)) * Mathf.Clamp01(s.speedKmh / 60f);
                desired += assist;
            }

            desired = Mathf.Clamp(desired, -maxAngle * 1.4f, maxAngle * 1.4f);
            float rate = Mathf.Lerp(st.rateLowSpeed, st.rateHighSpeed, Mathf.Clamp01(s.speedKmh / 160f)) * s.steerMult;
            _angle = Mathf.MoveTowards(_angle, desired, rate * dt);
            s.steerDeg = _angle;

            float ackBase = st.ackermann * Mathf.Abs(_raw) * 0.12f;
            foreach (var w in c.wheels)
            {
                if (!w.rig.steered) { w.steerAngle = 0f; continue; }
                bool inner = Mathf.Sign(_angle) == Mathf.Sign(w.mountLocal.x) && Mathf.Abs(_angle) > 0.01f;
                w.steerAngle = _angle * (1f + (inner ? ackBase : -ackBase));
            }
        }
    }
}
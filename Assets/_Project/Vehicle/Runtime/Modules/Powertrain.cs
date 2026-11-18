using UnityEngine;

namespace Arcade.Vehicle
{
    public sealed class EngineModule : IVehicleModule
    {
        VehicleContext c;
        float _rpm;

        public void Init(VehicleContext ctx) { c = ctx; _rpm = ctx.cfg.engine.idleRpm; }

        public void FixedTick(float dt)
        {
            var e = c.cfg.engine;
            var s = c.s;

            float rps = 0f; int n = 0;
            foreach (var w in c.wheels)
                if (w.rig.driven) { rps += Mathf.Abs(w.vLong) / (2f * Mathf.PI * w.Radius); n++; }
            if (n > 0) rps /= n;

            float ratio = Mathf.Abs(s.gearRatio) * c.cfg.gearbox.finalDrive;
            float targetRpm = rps * 60f * ratio;
            if (s.shifting || (c.input.Handbrake && s.speed < 3f))
                targetRpm = Mathf.Lerp(e.idleRpm, e.redlineRpm * 0.8f, s.throttle);
            targetRpm = Mathf.Clamp(targetRpm, e.idleRpm, e.redlineRpm);

            _rpm = Mathf.Lerp(_rpm, targetRpm, 1f - Mathf.Exp(-dt / e.rpmResponse));
            s.rpm = _rpm;

            float nos = Mathf.Lerp(1f, c.cfg.nos.torqueMult, s.nosActive);
            float torque = e.torqueShape.Evaluate(_rpm) * e.peakTorque * s.throttle * nos;
            if (_rpm > e.redlineRpm * 0.985f) torque *= 0.15f;
            s.engineTorque = torque;
        }
    }

    public sealed class GearboxModule : IVehicleModule
    {
        VehicleContext c;
        float _shiftTimer, _sinceShift;

        public void Init(VehicleContext ctx)
        {
            c = ctx;
            c.s.gear = 1;
            c.s.gearRatio = ctx.cfg.gearbox.forwardRatios[0];
        }

        public void FixedTick(float dt)
        {
            var g = c.cfg.gearbox;
            var s = c.s;
            _sinceShift += dt;

            if (!s.reversing && c.input.Brake > 0.2f && s.forwardSpeed < 0.6f && c.input.Throttle < 0.1f)
                s.reversing = true;
            if (s.reversing && c.input.Throttle > 0.2f && s.forwardSpeed > -0.6f)
                s.reversing = false;

            if (_shiftTimer > 0f)
            {
                _shiftTimer -= dt;
                s.shifting = true;
                if (_shiftTimer <= 0f) { s.shifting = false; _sinceShift = 0f; }
            }
            else if (!s.reversing && _sinceShift > 0.35f)
            {
                if (s.rpm > g.upshiftRpm && s.gear < g.forwardRatios.Length && s.throttle > 0.1f) Shift(s.gear + 1);
                else if (s.rpm < g.downshiftRpm && s.gear > 1) Shift(s.gear - 1);
            }

            s.gearRatio = s.reversing ? -g.reverseRatio : g.forwardRatios[s.gear - 1];

            float torque = s.shifting ? 0f : s.engineTorque;
            float wheelTorque = torque * s.gearRatio * g.finalDrive * g.efficiency;

            float avgR = 0f; int driven = 0, drivenFront = 0;
            foreach (var w in c.wheels)
            {
                if (!w.rig.driven) continue;
                avgR += w.Radius; driven++; if (w.rig.steered) drivenFront++;
            }
            if (driven == 0) return;
            avgR /= driven;
            float totalForce = wheelTorque / avgR;

            foreach (var w in c.wheels)
            {
                if (!w.rig.driven) { w.driveRequest = 0f; continue; }
                float share;
                if (g.awd && drivenFront > 0 && drivenFront < driven)
                    share = w.rig.steered ? g.frontTorqueSplit / drivenFront
                                          : (1f - g.frontTorqueSplit) / (driven - drivenFront);
                else share = 1f / driven;
                w.driveRequest = totalForce * share;
            }
        }

        void Shift(int gear)
        {
            c.s.gear = gear;
            _shiftTimer = c.cfg.gearbox.shiftTime;
            c.vehicle.NotifyGear(gear);
        }
    }
}
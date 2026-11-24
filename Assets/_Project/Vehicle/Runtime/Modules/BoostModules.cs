using UnityEngine;

namespace Arcade.Vehicle
{
    public sealed class NosModule : IVehicleModule
    {
        VehicleContext c;
        bool _active;
        public void Init(VehicleContext ctx) => c = ctx;

        public void FixedTick(float dt)
        {
            var n = c.cfg.nos;
            var s = c.s;

            if (!_active && c.input.Nos && s.nos >= n.minToActivate)
            { _active = true; c.vehicle.NotifyNos(true); }

            if (_active)
            {
                s.nos -= dt / n.duration;
                if (s.nos <= 0f || (n.requireHold && !c.input.Nos))
                { _active = false; s.nos = Mathf.Max(0f, s.nos); c.vehicle.NotifyNos(false); }
            }
            else s.nos = Mathf.Clamp01(s.nos + n.passiveFill * dt);

            s.nosActive = Mathf.MoveTowards(s.nosActive, _active ? 1f : 0f, dt / Mathf.Max(0.01f, n.rampTime));
        }
    }

    public sealed class SpeedbreakerModule : IVehicleModule, IRealtimeModule
    {
        VehicleContext c;
        bool _active, _ownsTime;
        float _scale = 1f, _baseFixed;

        public void Init(VehicleContext ctx) { c = ctx; _baseFixed = VehiclePhysicsBootstrap.BaseFixedDelta; }
        public void FixedTick(float dt) { }

        public void RealtimeTick(float udt)
        {
            if (GamePause.IsPaused) return;
            var sb = c.cfg.speedbreaker;
            var s = c.s;

            bool want = c.input.Speedbreaker && s.speedbreakerMeter > 0.02f && s.speed > 3f;
            if (want != _active) { _active = want; c.vehicle.NotifySpeedbreaker(want); }

            s.speedbreakerMeter = _active
                ? Mathf.Max(0f, s.speedbreakerMeter - udt / sb.duration)
                : Mathf.Min(1f, s.speedbreakerMeter + udt * sb.refillRate);
            s.speedbreaker = _active;

            _scale = Mathf.Lerp(_scale, _active ? sb.timeScale : 1f, 1f - Mathf.Exp(-udt / sb.blend));
            if (!_active && Mathf.Abs(_scale - 1f) < 0.002f) _scale = 1f;

            if (_active || _scale < 1f || _ownsTime)
            {
                Time.timeScale = _scale;
                Time.fixedDeltaTime = _baseFixed * _scale;
                _ownsTime = _scale < 1f;
            }

            float k = Mathf.InverseLerp(1f, sb.timeScale, _scale);
            s.steerMult = Mathf.Lerp(1f, sb.steerMult, k);
            s.gripMult = Mathf.Lerp(1f, sb.gripMult, k);
        }

        public void Restore()
        {
            if (!_ownsTime) return;
            _ownsTime = false;
            _scale = 1f;
            Time.timeScale = GamePause.IsPaused ? 0f : 1f;
            Time.fixedDeltaTime = _baseFixed;
        }
    }
}
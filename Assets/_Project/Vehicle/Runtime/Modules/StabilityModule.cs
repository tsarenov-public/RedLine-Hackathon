using UnityEngine;

namespace Arcade.Vehicle
{
    public sealed class StabilityModule : IVehicleModule
    {
        VehicleContext c;
        float _flipTimer;
        public void Init(VehicleContext ctx) => c = ctx;

        public void FixedTick(float dt)
        {
            var cfg = c.cfg.aero;
            var s = c.s;
            var rb = c.rb;
            var t = c.t;

            Vector3 v = rb.linearVelocity;
            float sp = v.magnitude;

            if (sp > 0.1f)
            {
                float drag = cfg.dragCoeff * sp * sp * Mathf.Lerp(1f, c.cfg.nos.dragMult, s.nosActive);
                rb.AddForce(-v.normalized * drag);
            }
            rb.AddForce(-Vector3.up * (cfg.downforceCoeff * s.speed * s.speed + cfg.staticDownforce));

            if (s.groundedWheels > 0)
            {
                s.groundedTime += dt; s.airTime = 0f;

                float rollRate = Vector3.Dot(rb.angularVelocity, t.forward);
                float pitchRate = Vector3.Dot(rb.angularVelocity, t.right);
                rb.AddTorque(-t.forward * rollRate * cfg.rollDamping
                             - t.right * pitchRate * cfg.pitchDamping, ForceMode.Acceleration);

                var tilt = Quaternion.FromToRotation(t.up, s.groundNormal);
                tilt.ToAngleAxis(out float ang, out Vector3 axis);
                if (ang > 180f) ang -= 360f;
                if (axis.sqrMagnitude > 0.001f)
                    rb.AddTorque(axis.normalized * (ang * Mathf.Deg2Rad) * cfg.groundAlignGain, ForceMode.Acceleration);
            }
            else
            {
                s.airTime += dt; s.groundedTime = 0f;

                // прилипание после гребня
                if (s.airTime < cfg.stickTime &&
                    Physics.Raycast(t.position, -Vector3.up, out _, cfg.stickDistance, c.groundMask, QueryTriggerInteraction.Ignore))
                    rb.AddForce(-Vector3.up * (cfg.stickAccel * rb.mass));

                // авто-посадка на колёса
                if (s.airTime > cfg.airAlignDelay)
                {
                    Vector3 targetUp = Physics.Raycast(t.position, -Vector3.up, out var h, 14f,
                                        c.groundMask, QueryTriggerInteraction.Ignore) ? h.normal : Vector3.up;
                    var rot = Quaternion.FromToRotation(t.up, targetUp);
                    rot.ToAngleAxis(out float ang, out Vector3 axis);
                    if (ang > 180f) ang -= 360f;

                    Vector3 angVel = rb.angularVelocity;
                    Vector3 yaw = Vector3.Project(angVel, Vector3.up);
                    Vector3 desired = (axis.sqrMagnitude > 0.001f ? axis.normalized * (ang * Mathf.Deg2Rad) * cfg.airAlignGain : Vector3.zero) + yaw * 0.9f;
                    rb.angularVelocity = Vector3.Lerp(angVel, desired, 1f - Mathf.Exp(-dt / cfg.airAlignSmooth));

                    rb.AddTorque(Vector3.up * (c.input.Steer * cfg.airYawControl), ForceMode.Acceleration);
                    rb.AddTorque(-t.right * ((c.input.Throttle - c.input.Brake) * cfg.airPitchControl), ForceMode.Acceleration);
                }
            }

            // анти-переворот / сброс
            if (Vector3.Dot(t.up, Vector3.up) < cfg.flipDot && s.speed < 3f) _flipTimer += dt;
            else _flipTimer = 0f;
            if (_flipTimer > cfg.flipResetTime || c.input.Reset) ResetUpright();
        }

        void ResetUpright()
        {
            var t = c.t; var rb = c.rb;
            Vector3 from = t.position + Vector3.up * 3f;
            Vector3 pos = Physics.Raycast(from, -Vector3.up, out var h, 30f, c.groundMask, QueryTriggerInteraction.Ignore)
                ? h.point + Vector3.up * c.cfg.chassis.rideHeight
                : from;
            Vector3 fwd = Vector3.ProjectOnPlane(t.forward, Vector3.up);
            if (fwd.sqrMagnitude < 0.01f) fwd = Vector3.forward;

            rb.position = pos;
            rb.rotation = Quaternion.LookRotation(fwd.normalized, Vector3.up);
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
            foreach (var w in c.wheels) w.force = Vector2.zero;
            _flipTimer = 0f;
        }
    }
}
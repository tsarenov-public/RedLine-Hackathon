using System.Collections.Generic;
using UnityEngine;

namespace Arcade.Vehicle
{
    public sealed class SuspensionModule : IVehicleModule
    {
        VehicleContext c;
        readonly List<(Wheel l, Wheel r)> _axles = new();

        public void Init(VehicleContext ctx)
        {
            c = ctx;
            var front = new List<Wheel>();
            var rear = new List<Wheel>();
            foreach (var w in c.wheels) (w.rig.steered ? front : rear).Add(w);
            AddAxle(front); AddAxle(rear);
        }

        void AddAxle(List<Wheel> list)
        {
            Wheel l = null, r = null;
            foreach (var w in list) { if (w.mountLocal.x < 0f) l = w; else r = w; }
            if (l != null && r != null) _axles.Add((l, r));
        }

        public void FixedTick(float dt)
        {
            var cfg = c.cfg.suspension;
            var s = c.s;
            var rb = c.rb;
            float rest = cfg.restLength;
            int grounded = 0;
            Vector3 nsum = Vector3.zero;

            foreach (var w in c.wheels)
            {
                w.wasGrounded = w.grounded;
                w.axis = -c.t.up;
                Vector3 origin = c.t.TransformPoint(w.mountLocal);
                w.staticLoad = rb.mass * 9.81f * (w.rig.steered ? cfg.frontWeightBias : 1f - cfg.frontWeightBias) * 0.5f;

                bool recent = Time.time - w.lastGroundTime < 0.25f;
                float extra = recent ? cfg.stickExtraTravel : 0f;

                if (Physics.Raycast(origin, w.axis, out var hit, rest + w.Radius + extra,
                                    c.groundMask, QueryTriggerInteraction.Ignore))
                {
                    float len = Mathf.Clamp(hit.distance - w.Radius, -extra, rest);
                    w.suspLength = len;
                    w.compression = (rest - len) / rest;
                    w.contactPoint = hit.point;
                    w.contactNormal = hit.normal;

                    float compVel = Vector3.Dot(rb.GetPointVelocity(origin), w.axis);
                    float spring = cfg.stiffness * w.compression;
                    float damp = (compVel > 0f ? cfg.bumpDamping : cfg.reboundDamping) * compVel;
                    float load = spring + damp;
                    if (load < 0f) load = Mathf.Max(load, -cfg.stickForce * w.staticLoad); // натяг = прилипание
                    load = Mathf.Min(load, cfg.maxLoadMult * w.staticLoad);
                    w.load = load;

                    w.grounded = w.compression > 0.001f;
                    if (w.grounded)
                    {
                        grounded++; nsum += hit.normal; w.lastGroundTime = Time.time;
                    }
                    rb.AddForceAtPosition(-w.axis * load, origin, ForceMode.Force);
                }
                else
                {
                    w.grounded = false; w.load = 0f; w.compression = 0f;
                    w.suspLength = rest; w.contactNormal = Vector3.up;
                }
            }

            foreach (var (l, r) in _axles)
            {
                float diff = l.compression - r.compression;
                float f = Mathf.Clamp(diff * cfg.antiRoll, -cfg.antiRollMax, cfg.antiRollMax);
                if (l.grounded || l.wasGrounded)
                    rb.AddForceAtPosition(-l.axis * f, c.t.TransformPoint(l.mountLocal), ForceMode.Force);
                if (r.grounded || r.wasGrounded)
                    rb.AddForceAtPosition(r.axis * f, c.t.TransformPoint(r.mountLocal), ForceMode.Force);
            }

            s.groundedWheels = grounded;
            s.groundNormal = grounded > 0 ? (nsum / grounded).normalized : Vector3.up;
        }
    }
}
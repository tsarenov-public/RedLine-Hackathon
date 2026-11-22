using UnityEngine;

namespace Arcade.Vehicle
{
    public sealed class TireModule : IVehicleModule
    {
        VehicleContext c;
        public void Init(VehicleContext ctx) => c = ctx;

        public void FixedTick(float dt)
        {
            var t = c.cfg.tires;
            var s = c.s;
            var rb = c.rb;
            float perWheelMass = rb.mass / Mathf.Max(1, s.groundedWheels);

            float drift = s.driftAmount;
            float retention = t.driftScrubRetention * drift;
            float ellipsePow = t.ellipsePower * Mathf.Lerp(1f, t.driftEllipseBoost, drift);
            float driveBoost = 1f + t.driftThrottleBoost * drift * s.throttle;

            Vector3 vDir = Vector3.ProjectOnPlane(s.velocity, Vector3.up);
            vDir = vDir.sqrMagnitude > 0.01f ? vDir.normalized : Vector3.zero;

            foreach (var w in c.wheels)
            {
                if (!w.grounded || w.load <= 1f)
                {
                    float k = 1f - Mathf.Exp(-dt / 0.05f);
                    w.force = Vector2.Lerp(w.force, Vector2.zero, k);
                    w.slipIntensity = Mathf.Lerp(w.slipIntensity, 0f, k);
                    w.spinRate = Mathf.Lerp(w.spinRate, w.vLong / w.Radius, k);
                    w.longSlip = 0f; w.slipAngleDeg = 0f;
                    continue;
                }

                Vector3 n = w.contactNormal;
                Vector3 fwd = Vector3.ProjectOnPlane(Quaternion.AngleAxis(w.steerAngle, n) * c.t.forward, n).normalized;
                Vector3 right = Vector3.Cross(n, fwd).normalized;
                w.forward = fwd; w.right = right;

                Vector3 v = Vector3.ProjectOnPlane(rb.GetPointVelocity(w.contactPoint), n);
                float vLong = Vector3.Dot(v, fwd);
                float vLat = Vector3.Dot(v, right);
                w.vLong = vLong; w.vLat = vLat;

                float loadSens = 1f - t.loadSensitivity * Mathf.Clamp(w.load / w.staticLoad - 1f, -1f, 2f);
                float maxF = Mathf.Max(0f, t.baseGrip * w.rig.gripScale * loadSens * s.gripMult * w.load);
                float safeF = Mathf.Max(1f, maxF);

                // ---- боковая ----
                float alpha = Mathf.Atan2(vLat, Mathf.Abs(vLong) + t.slipEpsilon) * Mathf.Rad2Deg;
                w.slipAngleDeg = alpha;
                bool hb = c.input.Handbrake && w.rig.handbraked && s.speed > 2f;

                float latScale = t.lateralScale * (w.rig.steered ? t.frontGripBias : t.rearGripBias);
                if (hb) latScale *= t.handbrakeLateralGrip;
                float latTarget = -Mathf.Sign(vLat) * t.lateralGrip.Evaluate(Mathf.Abs(alpha)) * maxF * latScale;

                // ---- продольная ----
                float driveForce = w.driveRequest * driveBoost;
                float brakeF = s.brake * t.brakeForce * (w.rig.steered ? t.brakeBias : 1f - t.brakeBias) * 0.5f;
                float resist = brakeF + t.rollingResistance * w.load
                             + (w.rig.driven ? t.engineBrake * (1f - s.throttle) : 0f);

                if (hb)
                {
                    // долгое удержание ручника не должно останавливать машину
                    float fade = Mathf.Clamp01(s.handbrakeTime / Mathf.Max(0.05f, t.handbrakeFadeTime));
                    float lockF = Mathf.Lerp(t.handbrakeLock, t.handbrakeLock * 0.35f, fade);
                    driveForce = 0f;
                    resist = maxF * lockF;
                }

                float motionSign = Mathf.Sign(vLong);
                float absLongSpeed = Mathf.Abs(vLong);
                if (absLongSpeed < 3f)
                {
                    // Торможение может погасить движение и попутную тягу. Тяга при смене направления сохраняется.
                    float forceToStop = perWheelMass * absLongSpeed / dt;
                    float driveWithMotion = Mathf.Max(0f, motionSign * driveForce);
                    resist = Mathf.Min(resist, forceToStop + driveWithMotion);
                }
                float longTarget = driveForce - motionSign * resist;

                float limit = maxF * t.tractionLimit;
                if (Mathf.Abs(longTarget) > limit)
                {
                    w.longSlip = Mathf.Clamp01((Mathf.Abs(longTarget) - limit) / safeF);
                    longTarget = Mathf.Sign(longTarget) * limit;
                }
                else w.longSlip = Mathf.Lerp(w.longSlip, 0f, 1f - Mathf.Exp(-dt / 0.08f));

                // ---- friction ellipse (в заносе "квадратнее") ----
                float lu = Mathf.Clamp01(Mathf.Abs(longTarget) / safeF);
                float latCap = maxF * Mathf.Sqrt(Mathf.Max(0f, 1f - Mathf.Pow(lu, ellipsePow)));
                latTarget = Mathf.Clamp(latTarget, -latCap, latCap);
                float au = Mathf.Clamp01(Mathf.Abs(latTarget) / safeF);
                float longCap = maxF * Mathf.Sqrt(Mathf.Max(0f, 1f - Mathf.Pow(au, ellipsePow)));
                longTarget = Mathf.Clamp(longTarget, -longCap, longCap);

                // ---- анти-джиттер ----
                float maxLatImp = perWheelMass * Mathf.Abs(vLat) / dt * t.impulseClamp;
                latTarget = Mathf.Clamp(latTarget, -maxLatImp, maxLatImp);
                if (s.speed < 0.5f && s.throttle < 0.02f && !hb)
                {
                    longTarget = -vLong * perWheelMass / dt * 0.5f;
                    latTarget = -vLat * perWheelMass / dt * 0.5f;
                }

                // ---- relaxation ----
                float kLat = 1f - Mathf.Exp(-dt / t.relaxLateral);
                float kLong = 1f - Mathf.Exp(-dt / t.relaxLongitudinal);
                w.force = new Vector2(Mathf.Lerp(w.force.x, longTarget, kLong),
                                      Mathf.Lerp(w.force.y, latTarget, kLat));

                Vector3 fLong = fwd * w.force.x;
                Vector3 fLat = right * w.force.y;

                // ---- ГЛАВНОЕ: убираем тормозящую часть боковой силы в заносе ----
                if (retention > 0f && vDir != Vector3.zero && s.speed > 5f)
                {
                    float along = Vector3.Dot(fLat, vDir);
                    if (along < 0f) fLat -= vDir * along * retention;
                }

                rb.AddForceAtPosition(fLong + fLat, w.contactPoint, ForceMode.Force);

                // ---- FX ----
                float latSlip01 = Mathf.InverseLerp(t.slipFxStartDeg, t.slipFxFullDeg, Mathf.Abs(alpha));
                float fx = Mathf.Max(latSlip01, w.longSlip) * Mathf.Clamp01(s.speed / 6f);
                w.slipIntensity = Mathf.Lerp(w.slipIntensity, fx, 1f - Mathf.Exp(-dt / 0.08f));
                w.spinRate = vLong / w.Radius + Mathf.Sign(w.driveRequest) * w.longSlip * t.wheelSpinExtra;
            }
        }
    }
}

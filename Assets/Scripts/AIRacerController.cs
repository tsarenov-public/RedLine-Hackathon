using System.Collections.Generic;
using UnityEngine;

public class AIRacerController : MonoBehaviour
{
    [Header("Маршрут")]
    public WaypointPath path;
    public float reachDistance = 6.5f;
    public bool loopTrack = true;
    [Range(0f, 1f)] public float lookAheadBlend = 0.4f;

    [Header("Настройки вождения")]
    [Range(0.2f, 1.5f)] public float maxThrottle = 1f;
    public float cruiseSpeedKmh = 270f;
    public float steerSensitivity = 1.7f;
    public float steerSmoothSpeed = 10f;
    [Range(0f, 1f)] public float cornerBrakeStrength = 0.7f;

    [Header("Физика торможения перед поворотами")]
    [Tooltip("Чем меньше значение, тем раньше бот начинает тормозить перед поворотом (м/с²)")]
    public float brakeDecel = 10.5f;
    [Range(0.1f, 1f)] public float maxBrakeForce = 0.9f;

    public float CurrentThrottle { get; private set; }
    public float CurrentSteer { get; private set; }
    public float CurrentBrake { get; private set; }

    private List<Transform> waypoints;
    private Rigidbody rb;
    private int currentIndex = 0;
    private bool finished = false;

    private void Start()
    {
        rb = GetComponent<Rigidbody>();
        if (path != null)
            waypoints = path.GetWaypoints();
    }

    private void FixedUpdate()
    {
        if (waypoints == null || waypoints.Count == 0 || finished)
        {
            CurrentThrottle = 0f;
            CurrentSteer = Mathf.Lerp(CurrentSteer, 0f, Time.fixedDeltaTime * steerSmoothSpeed);
            CurrentBrake = 1f;
            return;
        }

        Transform currentWp = waypoints[currentIndex];
        int nextIndex = (currentIndex + 1) % waypoints.Count;
        Transform nextWp = (loopTrack || currentIndex + 1 < waypoints.Count) ? waypoints[nextIndex] : currentWp;

        // 1. Руление с упреждением на следующую точку
        Vector3 blendedTarget = Vector3.Lerp(currentWp.position, nextWp.position, lookAheadBlend);
        Vector3 flatTarget = new Vector3(blendedTarget.x, transform.position.y, blendedTarget.z);
        Vector3 direction = (flatTarget - transform.position).normalized;

        float angle = Vector3.SignedAngle(transform.forward, direction, Vector3.up);
        float targetSteer = Mathf.Clamp((angle / 45f) * steerSensitivity, -1f, 1f);
        CurrentSteer = Mathf.Lerp(CurrentSteer, targetSteer, Time.fixedDeltaTime * steerSmoothSpeed);

        // 2. Расчёт необходимой дистанции торможения до всех поворотов впереди
        float speedMs = rb != null ? rb.linearVelocity.magnitude : 0f;
        float speedKmh = speedMs * 3.6f;
        float cruiseMs = cruiseSpeedKmh / 3.6f;
        float minCornerMs = Mathf.Lerp(145f, 65f, cornerBrakeStrength) / 3.6f;

        float targetAllowedMs = cruiseMs;

        Vector3 flatWp = new Vector3(currentWp.position.x, transform.position.y, currentWp.position.z);
        float scannedDist = Vector3.Distance(transform.position, flatWp);
        Vector3 prevPos = currentWp.position;
        Vector3 prevDir = (flatWp - transform.position).sqrMagnitude > 0.01f
            ? (flatWp - transform.position).normalized
            : transform.forward;

        // Проверяем текущий угол входа
        float immediateSeverity = Mathf.Clamp01(Mathf.Max(0f, Mathf.Abs(angle) - 12f) / 70f);
        if (immediateSeverity > 0.05f)
            targetAllowedMs = Mathf.Min(targetAllowedMs, Mathf.Lerp(cruiseMs, minCornerMs, immediateSeverity));

        // Сканируем до 220 метров вперёд по точкам маршрута
        for (int i = 1; i <= 12; i++)
        {
            int idx = currentIndex + i;
            if (idx >= waypoints.Count)
            {
                if (loopTrack) idx %= waypoints.Count;
                else break;
            }

            Vector3 wpPos = waypoints[idx].position;
            Vector3 segVec = new Vector3(wpPos.x - prevPos.x, 0f, wpPos.z - prevPos.z);
            if (segVec.sqrMagnitude > 0.01f)
            {
                Vector3 segDir = segVec.normalized;
                float angleFromCar = Vector3.Angle(transform.forward, segDir);
                float angleFromPrev = Vector3.Angle(prevDir, segDir);
                float bendAngle = Mathf.Max(angleFromCar, angleFromPrev);

                float severity = Mathf.Clamp01(Mathf.Max(0f, bendAngle - 12f) / 70f);
                if (severity > 0.05f)
                {
                    float cornerSpeedMs = Mathf.Lerp(cruiseMs, minCornerMs, severity);
                    float decel = Mathf.Max(4f, brakeDecel);
                    float requiredBrakeDist = Mathf.Max(0f, (speedMs * speedMs - cornerSpeedMs * cornerSpeedMs) / (2f * decel)) + 8f;

                    if (scannedDist <= requiredBrakeDist)
                        targetAllowedMs = Mathf.Min(targetAllowedMs, cornerSpeedMs);
                }

                prevDir = segDir;
            }

            scannedDist += Vector3.Distance(prevPos, wpPos);
            prevPos = wpPos;
            if (scannedDist >= 220f) break;
        }

        // 3. Управление газом и тормозом (с ослаблением тормоза при повороте руля, чтобы не терять зацеп)
        float speedExcessMs = speedMs - targetAllowedMs;
        if (speedExcessMs > 1.2f)
        {
            CurrentThrottle = 0f;
            float brakeIntensity = Mathf.Clamp01(speedExcessMs / 7f) * maxBrakeForce;
            float steerGripShare = 1f - Mathf.Abs(CurrentSteer) * 0.65f;
            CurrentBrake = brakeIntensity * steerGripShare;
        }
        else
        {
            CurrentBrake = 0f;
            float throttle = maxThrottle;
            if (cruiseSpeedKmh > 0f && speedKmh > cruiseSpeedKmh)
            {
                float overSpeed = Mathf.Clamp01((speedKmh - cruiseSpeedKmh) / 15f);
                throttle *= (1f - overSpeed);
            }
            CurrentThrottle = Mathf.Clamp01(throttle);
        }

        // 4. Переключение точек маршрута
        if (Vector3.Distance(transform.position, flatWp) < reachDistance)
        {
            currentIndex++;
            if (currentIndex >= waypoints.Count)
            {
                if (loopTrack)
                    currentIndex = 0;
                else
                    finished = true;
            }
        }
    }

    private void OnDrawGizmos()
    {
        if (waypoints != null && currentIndex < waypoints.Count && !finished)
        {
            Gizmos.color = Color.green;
            Gizmos.DrawLine(transform.position + Vector3.up, waypoints[currentIndex].position);
        }
    }
}
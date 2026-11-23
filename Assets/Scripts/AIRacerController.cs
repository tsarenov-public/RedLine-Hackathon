using System.Collections.Generic;
using UnityEngine;

public class AIRacerController : MonoBehaviour
{
    [Header("Маршрут")]
    [InspectorName("Путь (Waypoints)")]
    public WaypointPath path;

    [InspectorName("Радиус захвата точки (м)")]
    public float reachDistance = 6.5f;

    [InspectorName("Зациклить трассу")]
    public bool loopTrack = true;

    [InspectorName("Упреждение взгляда")]
    [Range(0f, 1f)] public float lookAheadBlend = 0.4f;

    [Header("Настройки вождения")]
    [InspectorName("Максимальный газ")]
    [Range(0.2f, 1.5f)] public float maxThrottle = 1f;

    [InspectorName("Макс. скорость (км/ч)")]
    public float cruiseSpeedKmh = 270f;

    [InspectorName("Чувствительность руля")]
    public float steerSensitivity = 1.7f;

    [InspectorName("Плавность поворота руля")]
    public float steerSmoothSpeed = 10f;

    [InspectorName("Сброс скорости в поворотах")]
    [Range(0f, 1f)] public float cornerBrakeStrength = 0.7f;

    [Header("Торможение перед поворотами")]
    [InspectorName("Интенсивность замедления")]
    [Tooltip("Чем меньше значение, тем раньше бот начинает тормозить перед поворотом (м/с²)")]
    public float brakeDecel = 10.5f;

    [InspectorName("Максимальное усилие тормоза")]
    [Range(0.1f, 1f)] public float maxBrakeForce = 0.9f;

    [Header("Возврат на трассу и анти-застревание")]
    [InspectorName("Упорство возврата на маршрут")]
    [Tooltip("Усиливает подруливание, когда бота выбили с траектории")]
    [Range(0.5f, 2.5f)] public float routeRecoveryStrength = 1.45f;

    [InspectorName("Дистанция перезахвата точек (м)")]
    [Tooltip("Если бота отбросило дальше этой дистанции, он найдёт ближайшую точку впереди")]
    [Range(15f, 100f)] public float realignSearchRadius = 35f;

    [InspectorName("Стабилизация при развороте")]
    [Tooltip("Насколько сильно бот гасит газ при развороте боком после тарана")]
    [Range(0f, 1f)] public float spinRecoveryBrake = 0.75f;

    [InspectorName("Время до заднего хода (сек)")]
    [Range(0.5f, 4f)] public float stuckTimeout = 1.3f;

    [InspectorName("Длительность выезда назад (сек)")]
    [Range(0.4f, 2.5f)] public float reverseDuration = 1.0f;

    public float CurrentThrottle { get; private set; }
    public float CurrentSteer { get; private set; }
    public float CurrentBrake { get; private set; }

    private List<Transform> waypoints;
    private Rigidbody rb;
    private int currentIndex = 0;
    private bool finished = false;

    private float stuckTimer = 0f;
    private float reverseTimer = 0f;
    private float impactRecoveryTimer = 0f;

    private void Start()
    {
        rb = GetComponent<Rigidbody>();
        if (path != null)
            waypoints = path.GetWaypoints();
    }

    private void FixedUpdate()
    {
        float dt = Time.fixedDeltaTime;

        if (waypoints == null || waypoints.Count == 0 || finished)
        {
            CurrentThrottle = 0f;
            CurrentSteer = Mathf.Lerp(CurrentSteer, 0f, dt * steerSmoothSpeed);
            CurrentBrake = 1f;
            return;
        }

        if (impactRecoveryTimer > 0f)
            impactRecoveryTimer -= dt;

        // 1. Проверка: не проскочил ли бот текущую точку после удара
        UpdateWaypointProgress();

        Transform currentWp = waypoints[currentIndex];
        int nextIndex = (currentIndex + 1) % waypoints.Count;
        Transform nextWp = (loopTrack || currentIndex + 1 < waypoints.Count) ? waypoints[nextIndex] : currentWp;

        Vector3 flatWp = new Vector3(currentWp.position.x, transform.position.y, currentWp.position.z);
        Vector3 flatNextWp = new Vector3(nextWp.position.x, transform.position.y, nextWp.position.z);

        // Определяем, насколько сильно бота выбили с траектории
        float lateralOffset = CalculateLateralOffset(flatWp, flatNextWp);
        bool offCourse = lateralOffset > 4.5f || impactRecoveryTimer > 0f;

        // Если бота выбили с трассы — уменьшаем упреждение, чтобы он сначала вернулся на полосу
        float effectiveBlend = offCourse ? Mathf.Min(lookAheadBlend, 0.15f) : lookAheadBlend;
        Vector3 blendedTarget = Vector3.Lerp(flatWp, flatNextWp, effectiveBlend);
        Vector3 direction = (blendedTarget - transform.position).normalized;
        if (direction.sqrMagnitude < 0.001f) direction = transform.forward;

        float angle = Vector3.SignedAngle(transform.forward, direction, Vector3.up);
        float absAngle = Mathf.Abs(angle);

        // 2. Режим выезда задним ходом при застревании у стены
        if (reverseTimer > 0f)
        {
            reverseTimer -= dt;
            CurrentThrottle = 0f;
            CurrentBrake = 1f;
            // Выворачиваем руль в обратную сторону, чтобы корма увела нос к маршруту
            float reverseSteer = -Mathf.Sign(angle) * Mathf.Clamp01(absAngle / 30f);
            CurrentSteer = Mathf.Lerp(CurrentSteer, reverseSteer, dt * steerSmoothSpeed);
            return;
        }

        // 3. Руление с усиленным возвратом на маршрут после столкновения
        float recoveryMult = offCourse ? routeRecoveryStrength : 1f;
        float targetSteer = Mathf.Clamp((angle / 45f) * steerSensitivity * recoveryMult, -1f, 1f);
        CurrentSteer = Mathf.Lerp(CurrentSteer, targetSteer, dt * steerSmoothSpeed);

        // 4. Расчёт безопасной скорости и торможения перед поворотами
        float speedMs = rb != null ? rb.linearVelocity.magnitude : 0f;
        float speedKmh = speedMs * 3.6f;
        float cruiseMs = cruiseSpeedKmh / 3.6f;
        float minCornerMs = Mathf.Lerp(145f, 65f, cornerBrakeStrength) / 3.6f;

        float targetAllowedMs = cruiseMs;
        float scannedDist = Vector3.Distance(transform.position, flatWp);
        Vector3 prevPos = currentWp.position;
        Vector3 prevDir = (flatWp - transform.position).sqrMagnitude > 0.01f
            ? (flatWp - transform.position).normalized
            : transform.forward;

        float immediateSeverity = Mathf.Clamp01(Mathf.Max(0f, absAngle - 12f) / 70f);
        if (immediateSeverity > 0.05f)
            targetAllowedMs = Mathf.Min(targetAllowedMs, Mathf.Lerp(cruiseMs, minCornerMs, immediateSeverity));

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

        // 5. Управление газом и тормозом + стабилизация при развороте боком
        float speedExcessMs = speedMs - targetAllowedMs;
        if (absAngle > 55f && speedKmh > 18f && spinRecoveryBrake > 0.05f)
        {
            // Бота развернуло после удара — сбрасываем газ и стабилизируем курс
            float spinFactor = Mathf.Clamp01((absAngle - 55f) / 75f) * spinRecoveryBrake;
            CurrentThrottle = Mathf.Clamp01(maxThrottle * (1f - spinFactor));
            CurrentBrake = spinFactor * 0.65f;
        }
        else if (speedExcessMs > 1.2f)
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

        // 6. Детектор застревания (если упёрся в стену после столкновения)
        if (CurrentThrottle > 0.25f && speedKmh < 5f)
        {
            stuckTimer += dt;
            if (stuckTimer >= stuckTimeout)
            {
                reverseTimer = reverseDuration;
                stuckTimer = 0f;
            }
        }
        else
        {
            stuckTimer = Mathf.Max(0f, stuckTimer - dt * 2f);
        }
    }

    private void UpdateWaypointProgress()
    {
        Transform currentWp = waypoints[currentIndex];
        Vector3 flatWp = new Vector3(currentWp.position.x, transform.position.y, currentWp.position.z);
        float distToCurrent = Vector3.Distance(transform.position, flatWp);

        // Обычный захват точки
        if (distToCurrent < reachDistance)
        {
            AdvanceWaypoint();
            return;
        }

        // Если после тарана бот оказался далеко от текущей точки — ищем ближайшую впереди
        if (distToCurrent > realignSearchRadius)
        {
            int bestIdx = FindBestRecoveryWaypoint();
            if (bestIdx != -1)
            {
                currentIndex = bestIdx;
                return;
            }
        }

        // Если бота протащили мимо текущей точки по ходу трассы — не разворачиваемся назад
        int nextIdx = (currentIndex + 1) % waypoints.Count;
        if (loopTrack || currentIndex + 1 < waypoints.Count)
        {
            Vector3 flatNext = new Vector3(waypoints[nextIdx].position.x, transform.position.y, waypoints[nextIdx].position.z);
            Vector3 seg = flatNext - flatWp;
            float segLen = seg.magnitude;
            if (segLen > 0.5f)
            {
                Vector3 segDir = seg / segLen;
                float along = Vector3.Dot(transform.position - flatWp, segDir);
                float distToNext = Vector3.Distance(transform.position, flatNext);
                if (along > 0f && distToNext < segLen && distToNext < distToCurrent)
                {
                    AdvanceWaypoint();
                }
            }
        }
    }

    private int FindBestRecoveryWaypoint()
    {
        int bestIndex = -1;
        float bestScore = float.MaxValue;

        for (int i = 0; i < waypoints.Count; i++)
        {
            Vector3 wp = new Vector3(waypoints[i].position.x, transform.position.y, waypoints[i].position.z);
            Vector3 toWp = wp - transform.position;
            float dist = toWp.magnitude;
            if (dist < 1f) continue;

            // Предпочитаем точки впереди машины, чтобы не разворачивать бота против движения
            float forwardDot = Vector3.Dot(transform.forward, toWp / dist);
            float penalty = forwardDot < 0f ? 45f : 0f;
            float score = dist + penalty;

            if (score < bestScore)
            {
                bestScore = score;
                bestIndex = i;
            }
        }

        return bestIndex;
    }

    private float CalculateLateralOffset(Vector3 flatWp, Vector3 flatNextWp)
    {
        int prevIdx = (currentIndex - 1 + waypoints.Count) % waypoints.Count;
        Vector3 flatPrev = (loopTrack || currentIndex > 0)
            ? new Vector3(waypoints[prevIdx].position.x, transform.position.y, waypoints[prevIdx].position.z)
            : flatWp;

        Vector3 lineDir = (flatWp - flatPrev).sqrMagnitude > 0.1f
            ? (flatWp - flatPrev).normalized
            : (flatNextWp - flatWp).normalized;

        if (lineDir.sqrMagnitude < 0.01f) return 0f;
        Vector3 toCar = transform.position - flatPrev;
        Vector3 projected = Vector3.Project(toCar, lineDir);
        return (toCar - projected).magnitude;
    }

    private void AdvanceWaypoint()
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

    private void OnCollisionEnter(Collision collision)
    {
        // Реагируем на сильный удар (например, таран игроком)
        if (collision.impulse.magnitude > 1500f)
        {
            impactRecoveryTimer = 1.5f;
        }
    }

    private void OnDrawGizmos()
    {
        if (waypoints != null && currentIndex < waypoints.Count && !finished)
        {
            Gizmos.color = reverseTimer > 0f ? Color.red : (impactRecoveryTimer > 0f ? Color.yellow : Color.green);
            Gizmos.DrawLine(transform.position + Vector3.up, waypoints[currentIndex].position);
        }
    }
}
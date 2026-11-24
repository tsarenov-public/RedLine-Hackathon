using System.Collections.Generic;
using UnityEngine;

[DefaultExecutionOrder(-20)]
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
    [Tooltip("Если бот оказался дальше этой дистанции от маршрута, он целится в текущую точку")]
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
    public bool HoldPosition { get; set; }

    private List<Transform> waypoints;
    private float[] speedLimits;
    private Rigidbody rb;
    private int currentIndex;
    private bool finished;
    private float stuckTimer;
    private float reverseTimer;
    private float impactRecoveryTimer;
    private bool useFinishGate;
    private bool keepPaceThroughFinish;

    private bool Loops => loopTrack && !useFinishGate;
    public bool IsApproachingFinish => waypoints != null && waypoints.Count > 0 && currentIndex == waypoints.Count - 1;

    // RaceDirector arms this before Start builds the speed profile.
    public void UseFinishGate(bool keepRacePace = false)
    {
        useFinishGate = true;
        keepPaceThroughFinish = keepRacePace;
    }

    private float PlanningDeceleration => Mathf.Max(1f, brakeDecel * .65f);

    private void Start()
    {
        rb = GetComponent<Rigidbody>();
        if (path == null || rb == null) return;

        waypoints = path.GetWaypoints();
        speedLimits = BuildSpeedLimits();
        currentIndex = FindStartIndex();
    }

    private int FindStartIndex()
    {
        if (waypoints.Count == 0) return 0;
        var position = rb.position;
        int best = 0;
        float bestDistance = float.PositiveInfinity;
        for (int i = 0; i < Mathf.Min(waypoints.Count, 12); i++)
        {
            float distance = FlatDistance(position, waypoints[i].position);
            if (distance >= bestDistance) continue;
            bestDistance = distance;
            best = i;
        }

        var forward = Flatten(rb.rotation * Vector3.forward);
        bool behind = Vector3.Dot(forward, Flatten(waypoints[best].position - position)) < 1f;
        return behind && best < waypoints.Count - 1 ? best + 1 : best;
    }

    private void FixedUpdate()
    {
        float dt = Time.fixedDeltaTime;
        if (waypoints == null || waypoints.Count == 0 || finished || HoldPosition)
        {
            CurrentThrottle = 0f;
            CurrentSteer = Mathf.MoveTowards(CurrentSteer, 0f, dt * steerSmoothSpeed);
            CurrentBrake = Mathf.MoveTowards(CurrentBrake, 1f, dt * 4f);
            return;
        }

        var position = rb.position;
        var forward = Flatten(rb.rotation * Vector3.forward).normalized;
        float speed = Flatten(rb.linearVelocity).magnitude;
        AdvanceIfReached(position, speed);
        if (finished)
        {
            CurrentThrottle = 0f;
            return;
        }

        var target = waypoints[currentIndex].position;
        var onPath = ProjectOntoApproach(position, target);
        float offset = FlatDistance(position, onPath);
        impactRecoveryTimer = Mathf.Max(0f, impactRecoveryTimer - dt);
        bool recovering = offset > 4.5f || impactRecoveryTimer > 0f;

        float fast = Mathf.InverseLerp(160f, 255f, speed * 3.6f);
        float lookAhead = Mathf.Lerp(5f, 24f, Mathf.InverseLerp(0f, 68f, speed)) *
            Mathf.Lerp(.80f, 1.30f, lookAheadBlend);
        if (recovering) lookAhead *= .6f;
        var aim = offset < realignSearchRadius ? LookAhead(onPath, lookAhead) : target;
        var direction = Flatten(aim - position);
        float headingError = Vector3.SignedAngle(forward, direction, Vector3.up);
        float steering = Mathf.Clamp(headingError / 34f * steerSensitivity *
            (recovering ? routeRecoveryStrength : 1f) * Mathf.Lerp(1f, .76f, fast), -1f, 1f);
        float steerBlend = 1f - Mathf.Exp(-dt * steerSmoothSpeed);

        if (reverseTimer > 0f)
        {
            reverseTimer -= dt;
            CurrentThrottle = 0f;
            CurrentBrake = 1f;
            CurrentSteer = Mathf.Lerp(CurrentSteer, -steering, steerBlend);
            return;
        }

        CurrentSteer = Mathf.Lerp(CurrentSteer, steering, steerBlend);
        float speedLimit = ScanSpeedLimit(onPath, speed);
        float steeringDemand = Mathf.Max(Mathf.Abs(steering), Mathf.Abs(CurrentSteer));

        if (recovering)
            speedLimit = Mathf.Min(speedLimit, Mathf.Lerp(14f, 7f, Mathf.InverseLerp(4.5f, 12f, offset)));

        float spin = Mathf.InverseLerp(52f, 115f, Mathf.Abs(headingError)) * spinRecoveryBrake;
        float brake = Mathf.Clamp01((speed - speedLimit + .25f) / 3.5f) * maxBrakeForce;
        if (speed > 5f) brake = Mathf.Max(brake, spin * .75f);
        float throttle = Mathf.Clamp01(maxThrottle * Mathf.Clamp01((speedLimit - speed) / 3f) *
            (1f - .80f * steeringDemand * steeringDemand) * (1f - spin));
        CurrentBrake = Mathf.MoveTowards(CurrentBrake, brake, dt * (brake > CurrentBrake ? 7f : 3.5f));
        CurrentThrottle = CurrentBrake > .04f ? 0f :
            Mathf.MoveTowards(CurrentThrottle, throttle, dt * (throttle > CurrentThrottle ? 3f : 9f));

        if (!Loops && currentIndex == waypoints.Count - 1 && speedLimit < 1f)
            stuckTimer = 0f;
        else
            UpdateStuckRecovery(speed, dt);
    }

    private void AdvanceIfReached(Vector3 position, float speed)
    {
        var target = waypoints[currentIndex].position;
        var delta = position - target;
        if (Mathf.Abs(delta.y) > 3f) return;

        if (!Loops && currentIndex == waypoints.Count - 1)
        {
            if (!useFinishGate && Flatten(delta).sqrMagnitude < reachDistance * reachDistance && speed < .45f)
            {
                finished = true;
                currentIndex++;
            }
            return;
        }

        // Keep the next target in sequence, including when nearby lanes cross at different elevations.
        if (Flatten(delta).sqrMagnitude < reachDistance * reachDistance)
            currentIndex = (currentIndex + 1) % waypoints.Count;
    }

    private Vector3 ProjectOntoApproach(Vector3 position, Vector3 target)
    {
        if (currentIndex == 0 && !Loops) return position;

        int previous = (currentIndex + waypoints.Count - 1) % waypoints.Count;
        var start = waypoints[previous].position;
        var segment = Flatten(target - start);
        float progress = Vector3.Dot(Flatten(position - start), segment) / Mathf.Max(.001f, segment.sqrMagnitude);
        return Vector3.Lerp(start, target, progress);
    }

    private Vector3 LookAhead(Vector3 start, float distance)
    {
        for (int step = 0; step < waypoints.Count; step++)
        {
            int index = currentIndex + step;
            if (!Loops && index == waypoints.Count) break;
            var end = waypoints[index % waypoints.Count].position;
            float length = FlatDistance(start, end);
            if (length >= distance) return Vector3.Lerp(start, end, distance / length);
            distance -= length;
            start = end;
        }
        return start;
    }

    private float ScanSpeedLimit(Vector3 start, float speed)
    {
        float scanDistance = Mathf.Lerp(90f, 100f, Mathf.InverseLerp(0f, 70f, speed));
        float reactionDistance = Mathf.Lerp(2f, 7f, Mathf.InverseLerp(0f, 70f, speed));
        float distance = 0f;
        float limit = Mathf.Max(0f, cruiseSpeedKmh / 3.6f);
        for (int step = 0; step < waypoints.Count; step++)
        {
            int index = currentIndex + step;
            if (!Loops && index == waypoints.Count) break;
            index %= waypoints.Count;
            var end = waypoints[index].position;
            distance += FlatDistance(start, end);
            if (step > 0 && distance > scanDistance) break;
            float brakingDistance = Mathf.Max(0f, distance - reactionDistance);
            limit = Mathf.Min(limit, ApproachSpeed(speedLimits[index], brakingDistance));
            start = end;
        }
        return limit;
    }

    private float[] BuildSpeedLimits()
    {
        int count = waypoints.Count;
        var limits = new float[count];
        float cruise = Mathf.Max(0f, cruiseSpeedKmh / 3.6f);
        float lateralAcceleration = Mathf.Lerp(13f, 4.8f, cornerBrakeStrength);
        for (int i = 0; i < count; i++)
        {
            limits[i] = cruise;
            if (!Loops && (i == 0 || i == count - 1)) continue;

            var point = waypoints[i].position;
            var incoming = point - waypoints[(i + count - 1) % count].position;
            var outgoing = waypoints[(i + 1) % count].position - point;
            float a = Flatten(incoming).magnitude;
            float b = Flatten(outgoing).magnitude;
            if (a < .1f || b < .1f) continue;

            float twiceArea = Vector3.Cross(Flatten(incoming), Flatten(outgoing)).magnitude;
            if (twiceArea > .01f)
            {
                float radius = a * b * Flatten(incoming + outgoing).magnitude / (2f * twiceArea);
                limits[i] = Mathf.Min(limits[i], Mathf.Sqrt(lateralAcceleration * radius));
            }

            float crest = (incoming.y / a - outgoing.y / b) / ((a + b) * .5f);
            if (crest > .001f)
                limits[i] = Mathf.Min(limits[i], Mathf.Sqrt(4.5f / crest));
        }

        if (count == 0) return limits;
        if (!Loops) limits[count - 1] = useFinishGate ? (keepPaceThroughFinish ? cruise : 4f) : 0f;

        // Propagate limits farther than the live scan when a straight needs more braking room.
        int first = Loops ? count * 2 - 1 : count - 2;
        for (int i = first; i >= 0; i--)
        {
            int index = i % count;
            int next = (index + 1) % count;
            float length = FlatDistance(waypoints[index].position, waypoints[next].position);
            limits[index] = Mathf.Min(limits[index], ApproachSpeed(limits[next], length));
        }

        return limits;
    }

    private float ApproachSpeed(float speed, float distance) =>
        Mathf.Sqrt(speed * speed + 2f * PlanningDeceleration * distance);

    private void UpdateStuckRecovery(float speed, float dt)
    {
        if (CurrentThrottle <= .25f || speed >= 5f / 3.6f)
        {
            stuckTimer = Mathf.Max(0f, stuckTimer - dt * 2f);
            return;
        }

        stuckTimer += dt;
        if (stuckTimer < stuckTimeout) return;

        reverseTimer = reverseDuration;
        stuckTimer = 0f;
    }

    private static Vector3 Flatten(Vector3 value) => new(value.x, 0f, value.z);
    private static float FlatDistance(Vector3 a, Vector3 b) => Flatten(a - b).magnitude;

    private void OnCollisionEnter(Collision collision)
    {
        if (collision.impulse.magnitude > 1500f) impactRecoveryTimer = 1.5f;
    }

    private void OnDrawGizmos()
    {
        if (waypoints == null || currentIndex >= waypoints.Count || finished) return;

        Gizmos.color = reverseTimer > 0f ? Color.red : impactRecoveryTimer > 0f ? Color.yellow : Color.green;
        Gizmos.DrawLine(transform.position + Vector3.up, waypoints[currentIndex].position);
    }
}

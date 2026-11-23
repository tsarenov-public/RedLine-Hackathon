using System.Collections.Generic;
using UnityEngine;

[DefaultExecutionOrder(-10)]
[RequireComponent(typeof(Rigidbody))]
public sealed class AIRacerController : MonoBehaviour
{
    private const float CornerDeceleration = 4f;
    private const float CornerPreviewDistance = 60f;
    private const float LateralAcceleration = 5f;

    [Header("Маршрут")]
    public WaypointPath path;
    [Min(0.5f)] public float reachDistance = 3f;
    public bool loopTrack;

    [Header("Настройки вождения")]
    [Range(0f, 1f)] public float maxThrottle = 0.85f;
    [Min(10f)] public float cruiseSpeedKmh = 72f;
    [Min(0f)] public float steerSensitivity = 1.6f;
    [Range(0f, 1f)] public float cornerBrakeStrength = 0.75f;

    public float CurrentThrottle { get; private set; }
    public float CurrentSteer { get; private set; }
    public float CurrentBrake { get; private set; }

    private Rigidbody vehicleBody;
    private List<Transform> waypoints;
    private Vector3 segmentStart;
    private int currentIndex;
    private bool finished;

    private void Awake() => vehicleBody = GetComponent<Rigidbody>();

    private void OnEnable()
    {
        waypoints = path != null ? path.GetWaypoints() : null;
        segmentStart = Flat(transform.position);
        currentIndex = 0;
        finished = false;
        HoldBrake();
    }

    private void FixedUpdate()
    {
        Vector3 position = Flat(vehicleBody.position);
        if (waypoints == null || waypoints.Count == 0 || finished || !AdvanceWaypoints(position))
        {
            HoldBrake();
            return;
        }

        Vector3 target = Flat(waypoints[currentIndex].position);
        Vector3 segment = target - segmentStart;
        float along = Mathf.Clamp(Vector3.Dot(position - segmentStart, segment.normalized), 0f, segment.magnitude);
        Vector3 projected = segmentStart + segment.normalized * along;

        float speed = Flat(vehicleBody.linearVelocity).magnitude;
        float lookAhead = Mathf.Clamp(3f + speed * 0.32f, 3f, 10f);
        Vector3 aim = LookAlongRoute(projected, lookAhead);
        Vector3 forward = Flat(transform.forward);
        float angle = Vector3.SignedAngle(forward, aim - position, Vector3.up);
        CurrentSteer = Mathf.Clamp(angle * steerSensitivity / 45f, -1f, 1f);

        float cruise = cruiseSpeedKmh / 3.6f;
        float targetSpeed = GetTargetSpeed(projected, segment, cruise);
        float steeringSlowdown = Mathf.Lerp(cruise, 4f, Mathf.InverseLerp(20f, 75f, Mathf.Abs(angle)));
        float speedError = Mathf.Min(targetSpeed, steeringSlowdown) - speed;
        CurrentThrottle = maxThrottle * Mathf.Clamp01(speedError / 3f);
        CurrentBrake = cornerBrakeStrength * Mathf.Clamp01(-speedError / 2f);
    }

    private bool AdvanceWaypoints(Vector3 position)
    {
        for (int count = 0; count < waypoints.Count; count++)
        {
            Vector3 target = Flat(waypoints[currentIndex].position);
            Vector3 offset = position - target;
            Vector3 segment = target - segmentStart;
            bool passed = Vector3.Dot(offset, segment) > 0f &&
                Vector3.ProjectOnPlane(offset, segment).sqrMagnitude <= reachDistance * reachDistance;
            if (offset.sqrMagnitude > reachDistance * reachDistance && !passed) return true;

            if (!TryGetNext(currentIndex, out int next))
            {
                finished = true;
                return false;
            }

            segmentStart = target;
            currentIndex = next;
        }

        return true;
    }

    private Vector3 LookAlongRoute(Vector3 from, float distance)
    {
        int index = currentIndex;
        for (int count = 0; count < waypoints.Count; count++)
        {
            Vector3 to = Flat(waypoints[index].position);
            float length = Vector3.Distance(from, to);
            if (length >= distance || !TryGetNext(index, out int next))
                return Vector3.MoveTowards(from, to, distance);

            distance -= length;
            from = to;
            index = next;
        }

        return from;
    }

    private float GetTargetSpeed(Vector3 from, Vector3 incoming, float cruise)
    {
        int index = currentIndex;
        float speed = cruise;
        float distance = Vector3.Distance(from, Flat(waypoints[index].position));
        for (int count = 0; count < waypoints.Count && distance <= CornerPreviewDistance; count++)
        {
            if (!TryGetNext(index, out int next))
                return Mathf.Min(speed, Mathf.Sqrt(2f * CornerDeceleration * distance));

            Vector3 outgoing = Flat(waypoints[next].position - waypoints[index].position);
            float angle = Vector3.Angle(incoming, outgoing);
            if (angle > 4f)
            {
                float radius = Mathf.Min(incoming.magnitude, outgoing.magnitude) /
                    (2f * Mathf.Tan(angle * Mathf.Deg2Rad * 0.5f));
                float cornerSpeed = Mathf.Clamp(Mathf.Sqrt(Mathf.Max(0f, radius) * LateralAcceleration), 4f, cruise);
                float approachSpeed = Mathf.Sqrt(cornerSpeed * cornerSpeed + 2f * CornerDeceleration * distance);
                speed = Mathf.Min(speed, approachSpeed);
            }

            distance += outgoing.magnitude;
            incoming = outgoing;
            index = next;
        }

        return speed;
    }

    private bool TryGetNext(int index, out int next)
    {
        next = index + 1;
        if (next < waypoints.Count) return true;
        next = 0;
        return loopTrack;
    }

    private void HoldBrake()
    {
        CurrentThrottle = 0f;
        CurrentSteer = 0f;
        CurrentBrake = 1f;
    }

    private static Vector3 Flat(Vector3 vector) => new Vector3(vector.x, 0f, vector.z);

    private void OnDrawGizmos()
    {
        if (waypoints == null || currentIndex >= waypoints.Count || finished) return;
        Gizmos.color = Color.green;
        Gizmos.DrawLine(transform.position + Vector3.up, waypoints[currentIndex].position);
    }
}
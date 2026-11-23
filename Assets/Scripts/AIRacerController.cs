using System.Collections.Generic;
using UnityEngine;

public class AIRacerController : MonoBehaviour
{
    [Header("Маршрут")]
    public WaypointPath path;
    public float reachDistance = 5f;
    public bool loopTrack = false;

    [Header("Настройки вождения")]
    [Range(0.2f, 1.5f)] public float maxThrottle = 0.85f;
    public float steerSensitivity = 2.2f;
    [Range(0f, 1f)] public float cornerBrakeStrength = 0.5f;

    // Выходные команды для контроллера машины
    public float CurrentThrottle { get; private set; }
    public float CurrentSteer { get; private set; }
    public float CurrentBrake { get; private set; }

    private List<Transform> waypoints;
    private int currentIndex = 0;
    private bool finished = false;

    private void Start()
    {
        if (path != null)
            waypoints = path.GetWaypoints();
    }

    private void FixedUpdate()
    {
        if (waypoints == null || waypoints.Count == 0 || finished)
        {
            CurrentThrottle = 0f;
            CurrentSteer = 0f;
            CurrentBrake = 1f;
            return;
        }

        Transform target = waypoints[currentIndex];
        Vector3 flatTarget = new Vector3(target.position.x, transform.position.y, target.position.z);
        Vector3 direction = (flatTarget - transform.position).normalized;

        // Угол до следующей точки (-180..180)
        float angle = Vector3.SignedAngle(transform.forward, direction, Vector3.up);
        CurrentSteer = Mathf.Clamp((angle / 45f) * steerSensitivity, -1f, 1f);

        // Сброс газа на крутых поворотах
        float turnFactor = Mathf.Clamp01(Mathf.Abs(angle) / 60f);
        CurrentThrottle = Mathf.Clamp01((1f - turnFactor * cornerBrakeStrength) * maxThrottle);
        CurrentBrake = 0f;

        // Переключение на следующую точку
        if (Vector3.Distance(transform.position, flatTarget) < reachDistance)
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
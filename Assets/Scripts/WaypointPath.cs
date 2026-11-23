using System.Collections.Generic;
using UnityEngine;

public class WaypointPath : MonoBehaviour
{
    public Color lineColor = Color.yellow;
    public float waypointRadius = 0.6f;

    public List<Transform> GetWaypoints()
    {
        List<Transform> points = new List<Transform>();
        foreach (Transform child in transform)
        {
            if (child.gameObject.activeInHierarchy)
                points.Add(child);
        }
        return points;
    }

    private void OnDrawGizmos()
    {
        Gizmos.color = lineColor;
        List<Transform> points = GetWaypoints();

        for (int i = 0; i < points.Count; i++)
        {
            Vector3 current = points[i].position;
            Gizmos.DrawSphere(current, waypointRadius);

            if (i + 1 < points.Count)
                Gizmos.DrawLine(current, points[i + 1].position);
        }
    }
}
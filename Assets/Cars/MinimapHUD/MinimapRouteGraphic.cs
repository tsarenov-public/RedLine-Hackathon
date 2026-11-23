using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Arcade.Vehicle
{
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class MinimapRouteGraphic : MaskableGraphic
    {
        private readonly List<Vector2> points = new();
        private bool loop;

        public void SetPath(WaypointPath path, float scale, bool closedLoop)
        {
            loop = closedLoop;
            points.Clear();
            if (path != null)
                foreach (var point in path.GetWaypoints())
                    points.Add(new Vector2(point.position.x, point.position.z) * scale);
            SetVerticesDirty();
        }

        protected override void OnPopulateMesh(VertexHelper mesh)
        {
            mesh.Clear();
            if (points.Count < 2) return;
            DrawRibbon(mesh, 4f, new Color32(10, 10, 12, 255));
            DrawRibbon(mesh, 2.35f, color);
        }

        private void DrawRibbon(VertexHelper mesh, float radius, Color32 ink)
        {
            int count = loop ? points.Count : points.Count - 1;
            for (int i = 0; i < count; i++)
            {
                var a = points[i];
                var b = points[(i + 1) % points.Count];
                var difference = b - a;
                if (difference.sqrMagnitude < .001f) continue;
                var side = new Vector2(-difference.y, difference.x).normalized * radius;
                int j = mesh.currentVertCount;
                mesh.AddVert(a + side, ink, Vector2.zero);
                mesh.AddVert(b + side, ink, Vector2.zero);
                mesh.AddVert(b - side, ink, Vector2.zero);
                mesh.AddVert(a - side, ink, Vector2.zero);
                mesh.AddTriangle(j, j + 1, j + 2);
                mesh.AddTriangle(j, j + 2, j + 3);
            }

            foreach (var p in points)
            {
                int center = mesh.currentVertCount;
                mesh.AddVert(p, ink, Vector2.zero);
                for (int i = 0; i < 8; i++)
                {
                    float angle = i * Mathf.PI / 4f;
                    mesh.AddVert(p + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius, ink, Vector2.zero);
                }
                for (int i = 0; i < 8; i++)
                    mesh.AddTriangle(center, center + (i + 1) % 8 + 1, center + i + 1);
            }
        }
    }
}

using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Arcade.Vehicle
{
    public sealed class ArcadeMinimapHUD : MonoBehaviour
    {
        [SerializeField] private RectTransform mapRotation;
        [SerializeField] private RectTransform mapOffset;
        [SerializeField] private RawImage roadMap;
        [SerializeField] private MinimapRouteGraphic route;
        [SerializeField] private Image playerMarker;
        [SerializeField, Min(10f)] private float range = 135f;

        private readonly List<Image> markers = new();
        private ArcadeVehicle player;
        private RaceMinimapCourse course;
        private ArcadeVehicle[] racers;
        private float nextCourseCheck;
        private float nextScan;
        private float radius;
        private float metersToPixels;

        public void Refresh(ArcadeVehicle vehicle)
        {
            if (player != vehicle || (course != null && course.gameObject.scene != vehicle.gameObject.scene) ||
                (course == null && Time.unscaledTime >= nextCourseCheck)) Bind(vehicle);
            if (course == null) return;
            Vector3 forward = vehicle.transform.forward;
            float heading = Mathf.Atan2(forward.x, forward.z) * Mathf.Rad2Deg;
            var rotation = Quaternion.Euler(0f, 0f, heading);
            var position = Flatten(vehicle.transform.position);
            mapRotation.localRotation = rotation;
            mapOffset.anchoredPosition = -position * metersToPixels;

            if (Time.unscaledTime >= nextScan)
            {
                nextScan = Time.unscaledTime + .5f;
                racers = FindObjectsByType<ArcadeVehicle>(FindObjectsSortMode.None);
            }

            int visible = 0;
            foreach (var racer in racers)
            {
                if (racer == null || racer == player || !racer.isActiveAndEnabled ||
                    racer.gameObject.scene != player.gameObject.scene) continue;
                var marker = GetMarker(visible++);
                var relative = Flatten(racer.transform.position) - position;
                marker.rectTransform.anchoredPosition = Vector2.ClampMagnitude(rotation * relative * metersToPixels,
                    radius - 13f);
                Vector3 rivalForward = racer.transform.forward;
                float rivalHeading = Mathf.Atan2(rivalForward.x, rivalForward.z) * Mathf.Rad2Deg;
                marker.rectTransform.localRotation = Quaternion.Euler(0f, 0f, heading - rivalHeading);
            }
            for (int i = visible; i < markers.Count; i++) markers[i].gameObject.SetActive(false);
        }

        private void Bind(ArcadeVehicle vehicle)
        {
            player = vehicle;
            nextScan = float.NegativeInfinity;
            nextCourseCheck = Time.unscaledTime + 1f;
            course = null;
            foreach (var candidate in FindObjectsByType<RaceMinimapCourse>(FindObjectsSortMode.None))
                if (candidate.gameObject.scene == vehicle.gameObject.scene)
                {
                    course = candidate;
                    break;
                }

            gameObject.SetActive(course != null);
            if (course == null) return;
            radius = ((RectTransform)transform).rect.width * .47f;
            metersToPixels = radius / range;
            roadMap.texture = course.roadMap;
            roadMap.rectTransform.sizeDelta = course.mapSize * metersToPixels;
            roadMap.rectTransform.anchoredPosition = (course.mapMin + course.mapSize * .5f) * metersToPixels;
            route.rectTransform.sizeDelta = roadMap.rectTransform.sizeDelta;
            route.SetPath(course.route, metersToPixels, course.closedLoop);
        }

        private Image GetMarker(int index)
        {
            if (index == markers.Count)
            {
                var marker = Instantiate(playerMarker, playerMarker.transform.parent);
                marker.name = "Rival Chevron";
                marker.color = new Color32(255, 106, 59, 255);
                marker.rectTransform.sizeDelta *= .78f;
                marker.transform.SetSiblingIndex(playerMarker.transform.GetSiblingIndex());
                markers.Add(marker);
            }

            var result = markers[index];
            result.gameObject.SetActive(true);
            return result;
        }

        private static Vector2 Flatten(Vector3 world) => new(world.x, world.z);
    }
}

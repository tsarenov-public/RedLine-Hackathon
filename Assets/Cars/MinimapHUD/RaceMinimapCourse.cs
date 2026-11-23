using UnityEngine;

namespace Arcade.Vehicle
{
    public sealed class RaceMinimapCourse : MonoBehaviour
    {
        public Texture2D roadMap;
        public Vector2 mapMin;
        public Vector2 mapSize;
        public WaypointPath route;
        public bool closedLoop;
    }
}

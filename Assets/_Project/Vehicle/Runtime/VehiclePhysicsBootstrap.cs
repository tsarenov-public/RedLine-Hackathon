using UnityEngine;

namespace Arcade.Vehicle
{
    public static class VehiclePhysicsBootstrap
    {
        public const float BaseFixedDelta = 0.01f;
        static bool DisableVehicleVsVehicle = false;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Apply()
        {
            Time.fixedDeltaTime = BaseFixedDelta;
            Time.maximumDeltaTime = 0.1f;
            Physics.gravity = new Vector3(0f, -9.81f, 0f);
            Physics.defaultSolverIterations = 8;

            int ground = LayerMask.NameToLayer("Ground");
            int vehicle = LayerMask.NameToLayer("Vehicle");
            if (ground < 0 || vehicle < 0)
            {
                Debug.LogError("[VehiclePhysics] Не найдены слои Ground / Vehicle!");
                return;
            }
            if (Physics.GetIgnoreLayerCollision(ground, vehicle))
            {
                Physics.IgnoreLayerCollision(ground, vehicle, false);
                Debug.LogWarning("[VehiclePhysics] Пара Ground×Vehicle была отключена — включил.");
            }
            if (DisableVehicleVsVehicle) Physics.IgnoreLayerCollision(vehicle, vehicle, true);

            Debug.Log($"[VehiclePhysics] OK | fixedDt={Time.fixedDeltaTime} | Ground={ground} | Vehicle={vehicle}");
        }
    }
}
using UnityEngine;

namespace Arcade.Vehicle
{
    // Применяет настройки физики автоматически при запуске — не зависит от версии UI Unity.
    public static class VehiclePhysicsBootstrap
    {
        static bool DisableVehicleVsVehicle = false; // включи, если появится трафик и машины не должны толкаться

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Apply()
        {
            Time.fixedDeltaTime = 0.01f;          // 100 Hz
            Time.maximumDeltaTime = 0.1f;
            Physics.gravity = new Vector3(0f, -9.81f, 0f);
            Physics.defaultSolverIterations = 8;

            int ground = LayerMask.NameToLayer("Ground");
            int vehicle = LayerMask.NameToLayer("Vehicle");

            if (ground < 0 || vehicle < 0)
            {
                Debug.LogError("[VehiclePhysics] Не найдены слои Ground / Vehicle! Вернись к шагу 1.1.");
                return;
            }

            // гарантируем, что машина сталкивается с землёй
            if (Physics.GetIgnoreLayerCollision(ground, vehicle))
            {
                Physics.IgnoreLayerCollision(ground, vehicle, false);
                Debug.LogWarning("[VehiclePhysics] Пара Ground Vehicle была отключена — включил.");
            }

            if (DisableVehicleVsVehicle)
                Physics.IgnoreLayerCollision(vehicle, vehicle, true);

            Debug.Log($"[VehiclePhysics] OK | gravity={Physics.gravity} | fixedDt={Time.fixedDeltaTime} " +
                      $"| Ground=layer {ground} | Vehicle=layer {vehicle}");
        }
    }
}
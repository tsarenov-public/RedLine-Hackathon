using System;
using Arcade.Vehicle;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class GamePause
{
    public static bool IsPaused { get; private set; }

    public static event Action<bool> Changed;

    public static void SetPaused(bool paused)
    {
        Time.timeScale = paused ? 0f : 1f;
        if (IsPaused == paused) return;
        IsPaused = paused;
        Changed?.Invoke(paused);
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetState()
    {
        IsPaused = false;
        Changed = null;
        SceneManager.sceneLoaded -= OnSceneLoaded;
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (mode != LoadSceneMode.Single) return;
        IsPaused = false;
        Time.timeScale = 1f;
        Time.fixedDeltaTime = VehiclePhysicsBootstrap.BaseFixedDelta;
    }
}

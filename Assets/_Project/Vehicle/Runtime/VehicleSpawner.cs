using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace Arcade.Vehicle
{
    public sealed class VehicleSpawner : MonoBehaviour
    {
        public GameObject[] carPrefabs;
        public Transform spawnPoint;
        public VehicleChaseCamera chaseCamera;
        public bool allowDebugSwitch = true;
        public int startIndex = 0;

        public static ArcadeVehicle Current { get; private set; }
        GameObject _instance;

        void Start() => Spawn(startIndex);

        void Update()
        {
            if (!allowDebugSwitch) return;
#if ENABLE_INPUT_SYSTEM
            var kb = Keyboard.current;
            if (kb == null) return;
            if (kb.digit1Key.wasPressedThisFrame) Spawn(0);
            if (kb.digit2Key.wasPressedThisFrame) Spawn(1);
            if (kb.digit3Key.wasPressedThisFrame) Spawn(2);
            if (kb.digit4Key.wasPressedThisFrame) Spawn(3);
            if (kb.digit5Key.wasPressedThisFrame) Spawn(4);
#else
            for (int i = 0; i < 5; i++)
                if (Input.GetKeyDown(KeyCode.Alpha1 + i)) Spawn(i);
#endif
        }

        public GameObject Spawn(int index)
        {
            if (carPrefabs == null || carPrefabs.Length == 0) return null;
            index = Mathf.Clamp(index, 0, carPrefabs.Length - 1);

            if (_instance != null) Destroy(_instance);
            Time.timeScale = 1f;
            Time.fixedDeltaTime = VehiclePhysicsBootstrap.BaseFixedDelta;

            var p = spawnPoint != null ? spawnPoint : transform;
            _instance = Instantiate(carPrefabs[index], p.position, p.rotation);
            Current = _instance.GetComponent<ArcadeVehicle>();
            if (chaseCamera != null) chaseCamera.Bind(Current);
            return _instance;
        }
    }
}
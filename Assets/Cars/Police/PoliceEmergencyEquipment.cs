using Arcade.Vehicle;
using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

[DisallowMultipleComponent]
public sealed class PoliceEmergencyEquipment : MonoBehaviour
{
    [SerializeField] private AudioSource siren;
    [SerializeField] private GameObject redBeacons;
    [SerializeField] private GameObject blueBeacons;

    private VehicleInputSource playerInput;
    private bool wasActive;
    private float flashStartedAt;
#if ENABLE_INPUT_SYSTEM
    private InputAction toggleSiren;
#endif

    public bool IsPursuitActive => siren && siren.isPlaying;

    private void Awake()
    {
        playerInput = GetComponentInParent<VehicleInputSource>();
#if ENABLE_INPUT_SYSTEM
        if (!playerInput) return;
        toggleSiren = new InputAction("Police siren", InputActionType.Button, "<Keyboard>/b");
        toggleSiren.AddBinding("<Gamepad>/dpad/up");
        toggleSiren.performed += OnToggleSiren;
#endif
    }

#if ENABLE_INPUT_SYSTEM
    private void OnEnable() => toggleSiren?.Enable();

    private void OnDestroy() => toggleSiren?.Dispose();

    private void OnToggleSiren(InputAction.CallbackContext context)
    {
        if (playerInput && playerInput.isActiveAndEnabled) SetPursuitActive(!IsPursuitActive);
    }
#endif

    public void SetPursuitActive(bool active)
    {
        if (!siren || active == IsPursuitActive) return;
        if (active) siren.Play();
        else siren.Stop();
        RefreshBeacons();
    }

    private void LateUpdate()
    {
#if !ENABLE_INPUT_SYSTEM
        if (playerInput && playerInput.isActiveAndEnabled && Input.GetKeyDown(KeyCode.B))
            SetPursuitActive(!IsPursuitActive);
#endif

        RefreshBeacons();
    }

    private void OnDisable()
    {
#if ENABLE_INPUT_SYSTEM
        toggleSiren?.Disable();
#endif
        if (siren) siren.Stop();
        RefreshBeacons();
    }

    private void RefreshBeacons()
    {
        bool active = IsPursuitActive;
        float now = Time.unscaledTime;
        if (active && !wasActive) flashStartedAt = now;
        wasActive = active;

        const float cycle = .36f;
        const float pulse = .16f;
        float phase = Mathf.Repeat(now - flashStartedAt, cycle);
        redBeacons.SetActive(active && phase < pulse);
        blueBeacons.SetActive(active && phase >= cycle * .5f && phase < cycle * .5f + pulse);
    }
}

using System.Globalization;
using UnityEngine;
using UnityEngine.UI;

namespace Arcade.Vehicle
{
    public sealed class ArcadeSpeedometerHUD : MonoBehaviour
    {
        private const float MphPerKmh = 0.621371f;
        private const float MaxDialMph = 200f;
        private static readonly Color NosIdle = new(0.27f, 0.94f, 0.88f);
        private static readonly Color NosActive = new(0.32f, 0.66f, 1f);
        private static ArcadeSpeedometerHUD visibleHud;

        [SerializeField] private ArcadeVehicle vehicle;
        [SerializeField] private Canvas display;
        [SerializeField] private RectTransform needle;
        [SerializeField] private Image nitroFill;
        [SerializeField] private Text digitalSpeed;
        [SerializeField] private Text gearLabel;

        private VehicleInputSource inputSource;
        private AIRacerController aiController;
        private float displayedMph;
        private int lastSpeed = -1;
        private int lastGear = int.MinValue;

        private bool IsPlayerControlled => isActiveAndEnabled && display != null &&
            vehicle != null && vehicle.isActiveAndEnabled && vehicle.Ctx != null &&
            inputSource != null && inputSource.isActiveAndEnabled &&
            (aiController == null || !aiController.isActiveAndEnabled);

        private void Awake()
        {
            if (vehicle == null) vehicle = GetComponentInParent<ArcadeVehicle>();
            if (vehicle == null) return;
            inputSource = vehicle.GetComponent<VehicleInputSource>();
            aiController = vehicle.GetComponent<AIRacerController>();
        }

        private void OnEnable()
        {
            if (display != null) display.enabled = false;
            if (vehicle != null) vehicle.GearChanged += OnGearChanged;
        }

        private void OnDisable()
        {
            if (vehicle != null) vehicle.GearChanged -= OnGearChanged;
            if (visibleHud == this) visibleHud = null;
            if (display != null) display.enabled = false;
        }

        private void OnDestroy()
        {
            if (display != null && display.transform.parent == null) Destroy(display.gameObject);
        }

        private void LateUpdate()
        {
            if (!IsPlayerControlled)
            {
                if (visibleHud == this) visibleHud = null;
                if (display != null) display.enabled = false;
                return;
            }

            if (visibleHud != this)
            {
                if (visibleHud != null && visibleHud.IsPlayerControlled && !HasDisplayPriority())
                    return;

                if (visibleHud != null) visibleHud.display.enabled = false;
                visibleHud = this;
            }

            var state = vehicle.State;
            float speed = Mathf.Max(0f, state.speedKmh * MphPerKmh);
            float dialMph = Mathf.Min(speed, MaxDialMph);
            displayedMph = display.enabled
                ? Mathf.Lerp(displayedMph, dialMph, 1f - Mathf.Exp(-12f * Time.unscaledDeltaTime))
                : dialMph;
            needle.localRotation = Quaternion.Euler(0f, 0f, Mathf.Lerp(135f, -135f, displayedMph / MaxDialMph));

            int roundedSpeed = Mathf.RoundToInt(Mathf.Min(speed, 999f));
            if (lastSpeed != roundedSpeed)
            {
                lastSpeed = roundedSpeed;
                digitalSpeed.text = roundedSpeed.ToString("D3", CultureInfo.InvariantCulture);
            }

            nitroFill.fillAmount = Mathf.Clamp01(state.nos);
            float pulse = 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * 18f);
            nitroFill.color = state.nosActive > 0.01f ? Color.Lerp(NosActive, Color.white, pulse * 0.55f) : NosIdle;
            SetGear(state.reversing ? -1 : state.gear);
            if (!display.enabled)
            {
                // Overlay canvases render at the scene root. The binding stays with the car.
                if (display.transform.parent != null) display.transform.SetParent(null, false);
                display.enabled = true;
            }
        }

        private bool HasDisplayPriority()
        {
            if (visibleHud.vehicle == vehicle) return false;
            if (vehicle == VehicleSpawner.Current) return true;
            if (visibleHud.vehicle == VehicleSpawner.Current) return false;

            // Scene-authored cars can be selected by the chase camera without a spawner.
            var camera = Camera.main;
            return camera != null && camera.TryGetComponent<VehicleChaseCamera>(out var chaseCamera) &&
                chaseCamera.isActiveAndEnabled && chaseCamera.vehicle == vehicle;
        }

        private void OnGearChanged(int gear)
        {
            if (visibleHud == this) SetGear(vehicle.State.reversing ? -1 : gear);
        }

        private void SetGear(int gear)
        {
            if (lastGear == gear) return;
            lastGear = gear;
            gearLabel.text = gear < 0 ? "R" : gear == 0 ? "N" : gear.ToString(CultureInfo.InvariantCulture);
        }
    }
}

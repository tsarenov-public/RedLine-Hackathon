using System.Globalization;
using UnityEngine;
using UnityEngine.UI;

namespace Arcade.Vehicle
{
    public sealed class ArcadeSpeedometerHUD : MonoBehaviour
    {
        private const float MphPerKmh = 0.621371f;
        private const float MaxDialRpm = 10000f;
        public const float NosSweepDegrees = 53f;
        private static readonly Color NosIdle = new(.69f, 1f, .16f);
        private static readonly Color NosActive = new(.89f, 1f, .54f);
        private static ArcadeSpeedometerHUD visibleHud;

        [SerializeField] private ArcadeVehicle vehicle;
        [SerializeField] private Canvas display;
        [SerializeField] private RectTransform needle;
        [SerializeField] private Image nitroFill;
        [SerializeField] private LcdDisplay digitalSpeed;
        [SerializeField] private LcdDisplay gearLabel;
        [SerializeField] private Image upshift;
        [SerializeField] private ArcadeMinimapHUD minimap;
        [SerializeField] private bool showForAI;

        private VehicleInputSource inputSource;
        private AIRacerController aiController;
        private float displayedRpm;
        private int lastSpeed = -1;
        private int lastGear = int.MinValue;

        private bool IsPlayerControlled => isActiveAndEnabled && display != null &&
            vehicle != null && vehicle.isActiveAndEnabled && vehicle.Ctx != null && !vehicle.IsFinishing &&
            inputSource != null && inputSource.isActiveAndEnabled &&
            (showForAI || aiController == null || !aiController.isActiveAndEnabled);

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
            float rpm = Mathf.Clamp(state.rpm, 0f, MaxDialRpm);
            displayedRpm = display.enabled
                ? Mathf.Lerp(displayedRpm, rpm, 1f - Mathf.Exp(-12f * Time.unscaledDeltaTime))
                : rpm;
            needle.localRotation = Quaternion.Euler(0f, 0f, Mathf.Lerp(135f, -135f, displayedRpm / MaxDialRpm));

            int roundedSpeed = Mathf.RoundToInt(Mathf.Min(speed, 999f));
            if (lastSpeed != roundedSpeed)
            {
                lastSpeed = roundedSpeed;
                digitalSpeed.Value = roundedSpeed.ToString("D3", CultureInfo.InvariantCulture);
            }

            nitroFill.fillAmount = Mathf.Clamp01(state.nos) * (NosSweepDegrees / 360f);
            float pulse = 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * 18f);
            nitroFill.color = state.nosActive > 0.01f ? Color.Lerp(NosActive, Color.white, pulse * 0.55f) : NosIdle;
            SetGear(state.reversing ? -1 : state.gear);
            float shiftRpm = Mathf.Clamp(vehicle.tuning.gearbox.upshiftRpm * .97f, 1500f, 9000f);
            bool shiftNow = !state.reversing && state.gear > 0 &&
                state.gear < vehicle.tuning.gearbox.forwardRatios.Length && state.rpm >= shiftRpm;
            upshift.color = new Color(.19f, .128f, .065f, shiftNow ? 1f : .20f);
            if (minimap != null) minimap.Refresh(vehicle);
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
            var camera = Camera.main;
            if (camera != null && camera.TryGetComponent<NFSCameraController>(out var raceCamera) &&
                raceCamera.isActiveAndEnabled && raceCamera.Target != null)
                return raceCamera.Target == vehicle;

            if (vehicle == VehicleSpawner.Current) return true;
            if (visibleHud.vehicle == VehicleSpawner.Current) return false;

            // Preserve camera-based selection in older scenes.
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
            gearLabel.Value = gear < 0 ? "R" : gear == 0 ? "N" : gear.ToString(CultureInfo.InvariantCulture);
        }
    }
}

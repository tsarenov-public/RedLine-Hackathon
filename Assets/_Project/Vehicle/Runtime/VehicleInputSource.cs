using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace Arcade.Vehicle
{
    public sealed class VehicleInputSource : MonoBehaviour, IInputSource
    {
        [Range(0f, 0.4f)] public float stickDeadzone = 0.12f;
        public bool debugInput;

        public bool HoldPosition { get; set; }

        private AIRacerController _aiController;
        private float _nextLogTime;

#if ENABLE_INPUT_SYSTEM
        InputAction _steer, _throttle, _brake, _hb, _nos, _sb, _reset;
#endif

        void Awake()
        {
            _aiController = GetComponent<AIRacerController>();

#if ENABLE_INPUT_SYSTEM
            _steer = new InputAction("Steer");
            _steer.AddCompositeBinding("1DAxis").With("Negative", "<Keyboard>/a").With("Positive", "<Keyboard>/d");
            _steer.AddCompositeBinding("1DAxis").With("Negative", "<Keyboard>/leftArrow").With("Positive", "<Keyboard>/rightArrow");
            _steer.AddBinding("<Gamepad>/leftStick/x");

            _throttle = new InputAction("Throttle", binding: "<Keyboard>/w");
            _throttle.AddBinding("<Keyboard>/upArrow");
            _throttle.AddBinding("<Gamepad>/rightTrigger");

            _brake = new InputAction("Brake", binding: "<Keyboard>/s");
            _brake.AddBinding("<Keyboard>/downArrow");
            _brake.AddBinding("<Gamepad>/leftTrigger");

            _hb = new InputAction("Handbrake", binding: "<Keyboard>/space");
            _hb.AddBinding("<Gamepad>/buttonSouth");

            _nos = new InputAction("Nos", binding: "<Keyboard>/leftShift");
            _nos.AddBinding("<Gamepad>/buttonWest");

            _sb = new InputAction("Speedbreaker", binding: "<Keyboard>/leftCtrl");
            _sb.AddBinding("<Gamepad>/buttonEast");

            _reset = new InputAction("Reset", binding: "<Keyboard>/r");
            _reset.AddBinding("<Gamepad>/selectButton");
#endif
        }

        void OnEnable()
        {
            if (_aiController == null)
                _aiController = GetComponent<AIRacerController>();

#if ENABLE_INPUT_SYSTEM
            _steer.Enable();
            _throttle.Enable();
            _brake.Enable();
            _hb.Enable();
            _nos.Enable();
            _sb.Enable();
            _reset.Enable();
#endif
        }

#if ENABLE_INPUT_SYSTEM
        void OnDisable()
        {
            _steer.Disable();
            _throttle.Disable();
            _brake.Disable();
            _hb.Disable();
            _nos.Disable();
            _sb.Disable();
            _reset.Disable();
        }
#endif

        public VehicleInputState Sample(float dt)
        {
            if (_aiController != null && _aiController.isActiveAndEnabled)
            {
                var aiInput = new VehicleInputState
                {
                    Steer = _aiController.CurrentSteer,
                    Throttle = _aiController.CurrentThrottle,
                    Brake = _aiController.CurrentBrake,
                    BrakeOnly = true
                };
                LogState("AI", aiInput);
                return aiInput;
            }

            if (HoldPosition) return new VehicleInputState { Brake = 1f, BrakeOnly = true };

#if ENABLE_INPUT_SYSTEM
            float steer = _steer.ReadValue<float>();
            float a = Mathf.Abs(steer);
            steer = a < stickDeadzone ? 0f : Mathf.Sign(steer) * Mathf.InverseLerp(stickDeadzone, 1f, a);
            float throttle = Mathf.Clamp01(_throttle.ReadValue<float>());
            float brake = Mathf.Clamp01(_brake.ReadValue<float>());

            var playerInput = new VehicleInputState
            {
                Steer = steer,
                Throttle = throttle,
                Brake = brake,
                Handbrake = _hb.IsPressed(),
                Nos = _nos.IsPressed(),
                Speedbreaker = _sb.IsPressed(),
                Reset = _reset.WasPressedThisFrame()
            };
#else
            float steer = Input.GetAxisRaw("Horizontal");
            float throttle = Input.GetKey(KeyCode.W) || Input.GetKey(KeyCode.UpArrow) ? 1f : 0f;
            float brake = Input.GetKey(KeyCode.S) || Input.GetKey(KeyCode.DownArrow) ? 1f : 0f;

            var playerInput = new VehicleInputState
            {
                Steer = steer,
                Throttle = throttle,
                Brake = brake,
                Handbrake = Input.GetKey(KeyCode.Space),
                Nos = Input.GetKey(KeyCode.LeftShift),
                Speedbreaker = Input.GetKey(KeyCode.LeftControl),
                Reset = Input.GetKeyDown(KeyCode.R)
            };
#endif
            LogState("ИГРОК", playerInput);
            return playerInput;
        }

        private void LogState(string source, VehicleInputState input)
        {
            if (debugInput && Time.unscaledTime >= _nextLogTime)
            {
                _nextLogTime = Time.unscaledTime + 0.5f;
                Debug.Log($"[{gameObject.name}] {source} -> Газ: {input.Throttle:F2}, Руль: {input.Steer:F2}, Тормоз: {input.Brake:F2}");
            }
        }
    }
}

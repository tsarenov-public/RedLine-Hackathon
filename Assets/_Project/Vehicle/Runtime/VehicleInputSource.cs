using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace Arcade.Vehicle
{
    public sealed class VehicleInputSource : MonoBehaviour, IInputSource
    {
        [Range(0f, 0.4f)] public float stickDeadzone = 0.12f;

#if ENABLE_INPUT_SYSTEM
        InputAction _steer, _throttle, _brake, _hb, _nos, _sb, _reset;

        void Awake()
        {
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
        }

        void OnEnable() { _steer.Enable(); _throttle.Enable(); _brake.Enable(); _hb.Enable(); _nos.Enable(); _sb.Enable(); _reset.Enable(); }
        void OnDisable() { _steer.Disable(); _throttle.Disable(); _brake.Disable(); _hb.Disable(); _nos.Disable(); _sb.Disable(); _reset.Disable(); }

        public VehicleInputState Sample(float dt)
        {
            float steer = _steer.ReadValue<float>();
            float a = Mathf.Abs(steer);
            steer = a < stickDeadzone ? 0f : Mathf.Sign(steer) * Mathf.InverseLerp(stickDeadzone, 1f, a);

            return new VehicleInputState
            {
                Steer = steer,
                Throttle = Mathf.Clamp01(_throttle.ReadValue<float>()),
                Brake = Mathf.Clamp01(_brake.ReadValue<float>()),
                Handbrake = _hb.IsPressed(),
                Nos = _nos.IsPressed(),
                Speedbreaker = _sb.IsPressed(),
                Reset = _reset.WasPressedThisFrame()
            };
        }
#else
        public VehicleInputState Sample(float dt) => new VehicleInputState
        {
            Steer = Input.GetAxisRaw("Horizontal"),
            Throttle = Input.GetKey(KeyCode.W) || Input.GetKey(KeyCode.UpArrow) ? 1f : 0f,
            Brake = Input.GetKey(KeyCode.S) || Input.GetKey(KeyCode.DownArrow) ? 1f : 0f,
            Handbrake = Input.GetKey(KeyCode.Space),
            Nos = Input.GetKey(KeyCode.LeftShift),
            Speedbreaker = Input.GetKey(KeyCode.LeftControl),
            Reset = Input.GetKeyDown(KeyCode.R)
        };
#endif
    }
}
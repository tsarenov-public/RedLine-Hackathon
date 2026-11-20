using SkrilStudio;
using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

public class RES2Connector : MonoBehaviour
{
    [Header("Настройки коробки для звука")]
    public float maxSpeed = 270f;     // Максимальная скорость из твоего Tuning_police (271 км/ч)
    public int gearsCount = 5;        // 5 передач из Tuning_police
    public float smoothSpeed = 7f;    // Плавность переключения (меньше = мягче переход)

    private RealisticEngineSound res2;
    private Rigidbody carRigidbody;
    private float currentRpm = 900f;

    void Start()
    {
        res2 = GetComponentInChildren<RealisticEngineSound>();
        carRigidbody = GetComponent<Rigidbody>();
    }

    void Update()
    {
        if (res2 == null || carRigidbody == null) return;

        // 1. Скорость в км/ч
        float speedKmh = carRigidbody.linearVelocity.magnitude * 3.6f;

        // 2. Плавный расчет передачи без резкого обрыва
        float gearStep = maxSpeed / gearsCount;
        int currentGear = Mathf.Min(Mathf.FloorToInt(speedKmh / gearStep), gearsCount - 1);
        float speedInGear = Mathf.Clamp01((speedKmh - currentGear * gearStep) / gearStep);

        // На каждой следующей передаче минимальные обороты после переключения не падают в самый низ (900),
        // а остаются в рабочей зоне (2200–3000), поэтому нет резкого обрыва звука
        float gearFloorRpm = Mathf.Lerp(900f, 3000f, (float)currentGear / gearsCount);
        float targetRpm = Mathf.Lerp(gearFloorRpm, res2.maxRPMLimit - 50f, speedInGear);

        // 3. Мягко сглаживаем переход между передачами
        currentRpm = Mathf.Lerp(currentRpm, targetRpm, Time.deltaTime * smoothSpeed);
        res2.engineCurrentRPM = Mathf.Clamp(currentRpm, 900f, res2.maxRPMLimit - 50f);

        // 4. Педаль газа
        bool isGasPressed = false;
#if ENABLE_INPUT_SYSTEM
        if (Keyboard.current != null)
            isGasPressed = Keyboard.current.wKey.isPressed || Keyboard.current.upArrowKey.isPressed;
#else
        isGasPressed = Input.GetAxis("Vertical") > 0.1f;
#endif
        res2.gasPedalPressing = isGasPressed;
    }
}

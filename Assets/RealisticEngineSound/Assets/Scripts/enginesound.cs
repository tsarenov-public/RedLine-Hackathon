using UnityEngine;

[RequireComponent(typeof(AudioSource))]
public class EngineSound : MonoBehaviour
{
    [Header("Настройки оборотов")]
    public float minPitch = 0.85f;     // Холостые обороты
    public float maxPitch = 2.1f;      // Максимум строго 2.1
    public float maxSpeed = 120f;      // Максимальная скорость (км/ч)
    public int gearsCount = 5;         // Количество передач

    [Header("Громкость")]
    public float minVolume = 0.45f;    // Громкость на холостых
    public float maxVolume = 0.52f;    // Громкость на максимуме

    [Header("Плавность")]
    public float pitchSmoothSpeed = 5f;

    private AudioSource engineAudio;
    private Rigidbody carRigidbody;

    void Start()
    {
        engineAudio = GetComponent<AudioSource>();
        carRigidbody = GetComponent<Rigidbody>();
    }

    void Update()
    {
        if (carRigidbody == null) return;

        float currentSpeed = carRigidbody.linearVelocity.magnitude * 3.6f;
        float totalSpeedRatio = Mathf.Clamp01(currentSpeed / maxSpeed);

        // Расчет передач
        float gearStep = maxSpeed / gearsCount;
        int currentGear = Mathf.Min(Mathf.FloorToInt(currentSpeed / gearStep), gearsCount - 1);

        float gearMinSpeed = currentGear * gearStep;
        float speedInGear = Mathf.Clamp01((currentSpeed - gearMinSpeed) / gearStep);

        // С каждой передачей нижний порог чуть поднимается, а верхний упирается в 2.1
        float gearBasePitch = Mathf.Lerp(minPitch, minPitch + 0.35f, (float)currentGear / gearsCount);
        float targetPitch = Mathf.Lerp(gearBasePitch, maxPitch, speedInGear);

        // Жестко ограничиваем потолок на 2.1
        targetPitch = Mathf.Clamp(targetPitch, minPitch, maxPitch);

        engineAudio.pitch = Mathf.Lerp(engineAudio.pitch, targetPitch, Time.deltaTime * pitchSmoothSpeed);
        engineAudio.volume = Mathf.Lerp(minVolume, maxVolume, totalSpeedRatio);
    }
}

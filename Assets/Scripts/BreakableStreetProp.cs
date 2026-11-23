using Arcade.Vehicle;
using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(Rigidbody))]
public sealed class BreakableStreetProp : MonoBehaviour
{
    [SerializeField] private BoxCollider approachTrigger;

    [Header("Настройка веса")]
    [Tooltip("Меньше 1 — лёгкий объект (мусорка), 1 — стандарт (лавочка), больше 1 — тяжёлый (столб)")]
    [Range(0.2f, 5f)]
    [SerializeField] private float heaviness = 1f;

    [Header("Настройка вращения")]
    [Tooltip("Максимальная угловая скорость (рад/с). Защита от бешеного вращения.")]
    [SerializeField] private float maxAngularVelocity = 15f;
    
    [Tooltip("Насколько сильно объект кувыркается при ударе.")]
    [SerializeField] private float spinMultiplier = 0.3f;

    private Rigidbody propBody;
    private Collider[] solidColliders;
    private bool launched;

    private void Awake()
    {
        propBody = GetComponent<Rigidbody>();
        solidColliders = GetComponentsInChildren<Collider>();
        propBody.mass *= heaviness;
        
        // На всякий случай ограничиваем максимальную угловую скорость сразу при старте
        propBody.maxAngularVelocity = maxAngularVelocity;
    }

    private void OnTriggerEnter(Collider other)
    {
        if (launched || other.isTrigger)
            return;

        Rigidbody carBody = other.attachedRigidbody;
        if (carBody == null || !carBody.TryGetComponent<ArcadeVehicle>(out _))
            return;

        Vector3 velocity = Vector3.ProjectOnPlane(carBody.linearVelocity, Vector3.up);
        if (velocity.sqrMagnitude < 0.25f ||
            Vector3.Dot(velocity, propBody.worldCenterOfMass - carBody.worldCenterOfMass) <= 0f)
            return;

        launched = true;
        if (approachTrigger != null) approachTrigger.enabled = false;

        // Keep debris solid against the map without feeding the launch impulse back into this car.
        foreach (Collider carCollider in carBody.GetComponentsInChildren<Collider>())
        {
            if (carCollider.isTrigger || carCollider.attachedRigidbody != carBody)
                continue;

            foreach (Collider propCollider in solidColliders)
                if (!propCollider.isTrigger)
                    Physics.IgnoreCollision(propCollider, carCollider);
        }

        propBody.isKinematic = false;

        float lightness = 1f / Mathf.Max(0.1f, heaviness);
        Vector3 lift = Vector3.up * (Mathf.Clamp(velocity.magnitude * 0.14f, 0.6f, 4f) * lightness);
        Vector3 launchVelocity = Vector3.ClampMagnitude(velocity * 1.05f, 24f) * lightness + lift;

        propBody.AddForce(launchVelocity * propBody.mass, ForceMode.Impulse);

        // --- ИСПРАВЛЕННОЕ ВРАЩЕНИЕ ---

        // 1. Создаем ось вращения перпендикулярно направлению удара (эффект кувырка)
        Vector3 hitDirection = velocity.normalized;
        Vector3 spinAxis = Vector3.Cross(Vector3.up, hitDirection).normalized; 
        
        // Добавляем немного случайности, чтобы каждый раз было чуть по-разному
        spinAxis += Random.insideUnitSphere * 0.3f; 
        spinAxis.Normalize();

        // 2. Вычисляем силу вращения БЕЗ учета массы (чтобы тяжелые не вертелись как юла)
        float spinForce = Mathf.Clamp(velocity.magnitude * spinMultiplier, 2f, 10f) * lightness;

        // 3. Применяем импульс вращения
        propBody.AddTorque(spinAxis * spinForce, ForceMode.Impulse);

        // 4. Ограничиваем максимальную угловую скорость (защита от "бешеного" вращения)
        if (propBody.angularVelocity.magnitude > maxAngularVelocity)
        {
            propBody.angularVelocity = propBody.angularVelocity.normalized * maxAngularVelocity;
        }

        Destroy(gameObject, 3.5f);
    }
}
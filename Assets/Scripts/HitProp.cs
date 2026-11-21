using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
public class HitProp : MonoBehaviour
{
    [Header("Порог срабатывания")]
    [Tooltip("Минимальная скорость машины (м/с), чтобы объект отлетел")]
    public float minSpeed = 3f;

    [Header("Сила отлёта")]
    [Tooltip("Множитель горизонтальной силы (зависит от скорости машины)")]
    public float horizontalForce = 1.5f;

    [Tooltip("Подброс вверх — доля от скорости удара")]
    public float upwardForce = 0.4f;

    [Tooltip("Случайное вращение при отлёте")]
    public float spinForce = 4f;

    [Header("Прочее")]
    [Tooltip("Уничтожить объект через N сек (0 = не уничтожать)")]
    public float destroyAfter = 10f;

    private Rigidbody rb;
    private bool launched;

    void Awake()
    {
        rb = GetComponent<Rigidbody>();
    }

    void OnCollisionEnter(Collision collision)
    {
        if (launched) return;
        if (!collision.collider.CompareTag("Vehicle")) return; // ← тег машины

        // Скорость машины
        Rigidbody carRb = collision.collider.attachedRigidbody;
        Vector3 carVelocity = carRb != null
            ? carRb.linearVelocity           // Unity 6+
            : collision.relativeVelocity;    // fallback

        // Для Unity 2022 и старше используй: carRb.velocity

        float speed = carVelocity.magnitude;
        if (speed < minSpeed) return;

        // Направление отлёта — от машины к объекту (по горизонтали)
        Vector3 dir = (transform.position - collision.collider.transform.position);
        dir.y = 0f;
        if (dir.sqrMagnitude < 0.001f) dir = -collision.GetContact(0).normal;
        dir.Normalize();

        // Импульс = масса × скорость × множитель
        Vector3 impulse = dir * speed * horizontalForce * rb.mass;
        impulse.y = speed * upwardForce * rb.mass;

        rb.AddForce(impulse, ForceMode.Impulse);

        // Случайное вращение
        Vector3 spin = new Vector3(
            Random.Range(-1f, 1f),
            Random.Range(-1f, 1f),
            Random.Range(-1f, 1f)) * spinForce * rb.mass;

        rb.AddTorque(spin, ForceMode.Impulse);

        launched = true;

        if (destroyAfter > 0f)
            Destroy(gameObject, destroyAfter);
    }
}
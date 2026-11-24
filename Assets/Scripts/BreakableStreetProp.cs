using System.Collections;
using Arcade.Vehicle;
using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(Rigidbody))]
public sealed class BreakableStreetProp : MonoBehaviour
{
    [SerializeField] private BoxCollider approachTrigger;

    [Header("Настройка веса и разлёта")]
    [Tooltip("0.3 — лёгкий (мусорка, конус), 1.0 — средний (лавочка), 3.0–5.0 — тяжёлый (фонарный столб)")]
    [Range(0.2f, 5f)]
    [SerializeField] private float heaviness = 1f;

    [Tooltip("Минимальная скорость машины (км/ч), на которой объект сбивается")]
    [SerializeField] private float minBreakSpeedKmh = 12f;

    [Tooltip("Через сколько секунд обломок исчезнет со сцены")]
    [SerializeField] private float destroyDelay = 3.5f;

    [Header("Ощущение импакта (отдача на машину)")]
    [Tooltip("Потеря скорости машины (%) при ударе о тяжёлый объект (для лёгких автоматически меньше)")]
    [Range(0f, 0.35f)]
    [SerializeField] private float maxSpeedLoss = 0.14f;

    [Tooltip("Сила физического толчка/встряски кузова машины при ударе")]
    [Range(0f, 3f)]
    [SerializeField] private float chassisJoltStrength = 1.2f;

    [Header("Звук и частицы удара (необязательно)")]
    [SerializeField] private AudioClip impactSound;
    [Range(0f, 1f)]
    [SerializeField] private float impactVolume = 0.85f;
    [SerializeField] private GameObject impactVfxPrefab;

    private Rigidbody propBody;
    private Collider[] solidColliders;
    private bool launched;

    private void Awake()
    {
        propBody = GetComponent<Rigidbody>();
        solidColliders = GetComponentsInChildren<Collider>(true);

        if (approachTrigger == null)
        {
            foreach (Collider col in solidColliders)
            {
                if (col is BoxCollider box && box.isTrigger)
                {
                    approachTrigger = box;
                    break;
                }
            }
        }

        propBody.isKinematic = true;
        propBody.mass = Mathf.Max(15f, propBody.mass * heaviness);
    }

    private void OnTriggerEnter(Collider other)
    {
        if (launched || other.isTrigger)
            return;

        TryLaunchFromCollider(other);
    }

    private void OnCollisionEnter(Collision collision)
    {
        if (launched || collision.collider == null)
            return;

        TryLaunchFromCollider(collision.collider);
    }

    private void TryLaunchFromCollider(Collider other)
    {
        Rigidbody carBody = other.attachedRigidbody;
        if (carBody == null || !carBody.TryGetComponent<ArcadeVehicle>(out _))
            return;

        Vector3 carVel = Vector3.ProjectOnPlane(carBody.linearVelocity, Vector3.up);
        float speedMs = carVel.magnitude;
        float minSpeedMs = minBreakSpeedKmh / 3.6f;

        if (speedMs < minSpeedMs)
            return;

        launched = true;
        if (approachTrigger != null)
            approachTrigger.enabled = false;

        propBody.isKinematic = false;
        propBody.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;

        StartCoroutine(DisableCarCollisionAfterImpact(carBody, 0.06f));

        Vector3 forwardDir = carVel / speedMs;

        Vector3 toProp = Vector3.ProjectOnPlane(propBody.worldCenterOfMass - carBody.worldCenterOfMass, Vector3.up);
        Vector3 lateralOffset = Vector3.ProjectOnPlane(toProp, forwardDir);
        Vector3 sideDir = lateralOffset.sqrMagnitude > 0.01f
            ? lateralOffset.normalized
            : (Random.value > 0.5f ? carBody.transform.right : -carBody.transform.right);

        float weight01 = Mathf.InverseLerp(0.2f, 5f, heaviness);
        float lightness = 1f / Mathf.Max(0.25f, heaviness);

        float forwardLaunchSpeed = Mathf.Clamp(speedMs * Mathf.Lerp(1.22f, 1.06f, weight01), 8f, 42f);
        float sideLaunchSpeed = Mathf.Clamp(speedMs * 0.35f, 3.5f, 14f) * Mathf.Lerp(1.1f, 0.65f, weight01);
        float upLaunchSpeed = Mathf.Clamp(speedMs * 0.16f, 1.0f, 6.5f) * lightness;

        Vector3 launchVelocity = forwardDir * forwardLaunchSpeed + sideDir * sideLaunchSpeed + Vector3.up * upLaunchSpeed;
        propBody.linearVelocity = launchVelocity;

        Vector3 toppleAxis = Vector3.Cross(Vector3.up, forwardDir + sideDir * 0.4f).normalized;
        Vector3 randomSpin = Random.onUnitSphere * (7f * lightness);
        propBody.angularVelocity = toppleAxis * Mathf.Lerp(5f, 2.2f, weight01) + randomSpin;

        ApplyCarImpact(carBody, forwardDir, sideDir, speedMs, weight01);
        PlayImpactFeedback(propBody.worldCenterOfMass, speedMs);

        Destroy(gameObject, destroyDelay);
    }

    private void ApplyCarImpact(Rigidbody carBody, Vector3 forwardDir, Vector3 sideDir, float speedMs, float weight01)
    {
        float speedFactor = Mathf.Clamp01(speedMs / 45f);

        float lossRatio = Mathf.Lerp(0.015f, maxSpeedLoss, weight01) * Mathf.Lerp(0.5f, 1f, speedFactor);
        carBody.linearVelocity *= (1f - lossRatio);

        if (chassisJoltStrength > 0.01f)
        {
            float joltMag = carBody.mass * Mathf.Lerp(0.35f, 2.6f, weight01) * chassisJoltStrength * speedFactor;
            Vector3 hitPoint = carBody.worldCenterOfMass + forwardDir * 1.4f + sideDir * 0.65f;
            Vector3 joltImpulse = (-forwardDir * 0.65f + Vector3.down * 0.55f - sideDir * 0.25f) * joltMag;
            carBody.AddForceAtPosition(joltImpulse, hitPoint, ForceMode.Impulse);
        }
    }

    private void PlayImpactFeedback(Vector3 hitPosition, float speedMs)
    {
        if (impactSound != null)
        {
            float vol = impactVolume * Mathf.Lerp(0.45f, 1f, Mathf.Clamp01(speedMs / 35f));
            AudioSource.PlayClipAtPoint(impactSound, hitPosition, vol);
        }

        if (impactVfxPrefab != null)
        {
            GameObject vfx = Instantiate(impactVfxPrefab, hitPosition, Quaternion.identity);
            Destroy(vfx, 2.5f);
        }
    }

    private IEnumerator DisableCarCollisionAfterImpact(Rigidbody carBody, float delay)
    {
        float elapsed = 0f;
        while (elapsed < delay)
        {
            yield return new WaitForFixedUpdate();
            elapsed += Time.fixedDeltaTime;
        }

        if (this == null || carBody == null)
            yield break;

        foreach (Collider carCollider in carBody.GetComponentsInChildren<Collider>())
        {
            if (carCollider == null || carCollider.isTrigger || carCollider.attachedRigidbody != carBody)
                continue;

            foreach (Collider propCollider in solidColliders)
            {
                if (propCollider != null && !propCollider.isTrigger)
                    Physics.IgnoreCollision(propCollider, carCollider, true);
            }
        }
    }
}
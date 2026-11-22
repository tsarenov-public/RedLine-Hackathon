using Arcade.Vehicle;
using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(Rigidbody))]
public sealed class BreakableStreetProp : MonoBehaviour
{
    [SerializeField] private BoxCollider approachTrigger;

    private Rigidbody propBody;
    private Collider[] solidColliders;
    private bool launched;

    private void Awake()
    {
        propBody = GetComponent<Rigidbody>();
        solidColliders = GetComponentsInChildren<Collider>();
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
        Vector3 lift = Vector3.up * Mathf.Clamp(velocity.magnitude * 0.14f, 0.6f, 4f);
        Vector3 launchVelocity = Vector3.ClampMagnitude(velocity * 1.05f, 24f) + lift;
        propBody.AddForce(launchVelocity * propBody.mass, ForceMode.Impulse);
        propBody.AddTorque(Random.onUnitSphere * (propBody.mass * 1.6f), ForceMode.Impulse);
        Destroy(gameObject, 3.5f);
    }
}
using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(BoxCollider))]
public sealed class FinishLineTrigger : MonoBehaviour
{
    [SerializeField] private RaceDirector race;

    private void Awake()
    {
        GetComponent<BoxCollider>().isTrigger = true;
        if (race == null) race = FindAnyObjectByType<RaceDirector>();
    }

    private void OnTriggerEnter(Collider other) => TryFinish(other);
    private void OnTriggerStay(Collider other) => TryFinish(other);

    private void TryFinish(Collider other)
    {
        var body = other.attachedRigidbody;
        if (race == null || body == null) return;

        var offset = body.worldCenterOfMass - transform.position;
        if (Vector3.Dot(offset, transform.forward) < -.8f ||
            Vector3.Dot(body.linearVelocity, transform.forward) <= .1f) return;

        race.NotifyFinishCrossed(body);
    }

    private void Reset()
    {
        var box = GetComponent<BoxCollider>();
        box.isTrigger = true;
        box.center = new Vector3(0f, 1.6f, 0f);
        box.size = new Vector3(17f, 4f, 2f);
    }
}

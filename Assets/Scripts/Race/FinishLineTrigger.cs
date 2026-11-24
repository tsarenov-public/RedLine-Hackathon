using Arcade.Vehicle;
using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(BoxCollider))]
public sealed class FinishLineTrigger : MonoBehaviour
{
    [SerializeField] private ArcadeVehicle raceCar;
    [SerializeField] private AIRacerController driver;
    [SerializeField] private RaceFinishUI finishScreen;

    private bool crossed;

    private void Awake()
    {
        GetComponent<BoxCollider>().isTrigger = true;
        if (driver != null) driver.UseFinishGate();
    }

    private void OnTriggerEnter(Collider other) => TryFinish(other);
    private void OnTriggerStay(Collider other) => TryFinish(other);

    private void TryFinish(Collider other)
    {
        if (crossed || raceCar == null || finishScreen == null || other.attachedRigidbody != raceCar.Rb) return;
        if (driver != null && !driver.IsApproachingFinish) return;

        var offset = raceCar.Rb.worldCenterOfMass - transform.position;
        if (Vector3.Dot(offset, transform.forward) < -.8f ||
            Vector3.Dot(raceCar.Rb.linearVelocity, transform.forward) <= .1f) return;

        crossed = true;
        raceCar.BeginFinishBraking();
        if (driver != null) driver.enabled = false;
        finishScreen.Present(raceCar);
    }

    private void Reset()
    {
        var box = GetComponent<BoxCollider>();
        box.isTrigger = true;
        box.center = new Vector3(0f, 1.6f, 0f);
        box.size = new Vector3(17f, 4f, 2f);
    }
}

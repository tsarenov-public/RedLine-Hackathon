using Arcade.Vehicle;
using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(Camera))]
public sealed class FinishShowcaseCamera : MonoBehaviour
{
    [SerializeField] private Vector3 offset = new(4.7f, 2f, 6.8f);
    [SerializeField] private float aimHeight = .72f;
    [SerializeField] private LayerMask obstructionMask = 1;

    private ArcadeVehicle target;
    private Vector3 viewOffset;
    private Camera view;

    private void Awake()
    {
        view = GetComponent<Camera>();
        view.enabled = false;
    }

    public void Show(ArcadeVehicle car)
    {
        target = car;
        var forward = Vector3.ProjectOnPlane(car.transform.forward, Vector3.up).normalized;
        var heading = Quaternion.LookRotation(forward);
        var alternateOffset = new Vector3(-offset.x, offset.y, offset.z);
        var aim = AimPoint();
        var preferred = heading * (offset - Vector3.up * aimHeight);
        var alternate = heading * (alternateOffset - Vector3.up * aimHeight);
        viewOffset = ClearDistance(aim, preferred) + .25f >= ClearDistance(aim, alternate) ? preferred : alternate;
        PositionCamera();
        view.enabled = true;
        if (TryGetComponent<AudioListener>(out var listener)) listener.enabled = true;
    }

    private void LateUpdate()
    {
        if (target != null) PositionCamera();
    }

    private Vector3 AimPoint() => target.transform.position + Vector3.up * aimHeight;

    private void PositionCamera()
    {
        var aim = AimPoint();
        float distance = ClearDistance(aim, viewOffset);
        var direction = viewOffset.normalized;
        transform.SetPositionAndRotation(aim + direction * distance, Quaternion.LookRotation(-direction));
    }

    private float ClearDistance(Vector3 aim, Vector3 displacement) =>
        Physics.SphereCast(aim, .2f, displacement.normalized, out var hit, displacement.magnitude,
            obstructionMask, QueryTriggerInteraction.Ignore)
            ? Mathf.Max(1f, hit.distance - .15f) : displacement.magnitude;
}

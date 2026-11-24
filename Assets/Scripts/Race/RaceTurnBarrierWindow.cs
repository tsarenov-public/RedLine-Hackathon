using System.Linq;
using Arcade.Vehicle;
using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(Barriers), typeof(BoxCollider))]
public sealed class RaceTurnBarrierWindow : MonoBehaviour
{
    [SerializeField] private RaceDirector race;
    [SerializeField, Min(0), Tooltip("Первый индекс следующей точки маршрута, при котором этот выезд закрыт.")]
    private int firstWaypoint;
    [SerializeField, Min(0), Tooltip("Последний индекс следующей точки маршрута, при котором этот выезд закрыт.")]
    private int lastWaypoint;

    private BoxCollider wall;
    private Canvas sign;
    private Collider[] playerColliders;
    private Collider[] rivalColliders;
    private bool? playerBlocked;
    private bool? rivalBlocked;

    private void Start()
    {
        if (race == null)
        {
            enabled = false;
            return;
        }
        wall = GetComponent<BoxCollider>();
        sign = GetComponentInChildren<Canvas>();
        playerColliders = CarColliders(race.Player);
        rivalColliders = CarColliders(race.Rival);
        UpdateWall();
    }

    private void FixedUpdate() => UpdateWall();

    private void UpdateWall()
    {
        bool blockPlayer = InWindow(race.PlayerNextWaypoint);
        bool blockRival = InWindow(race.RivalNextWaypoint);
        if (playerBlocked == blockPlayer && rivalBlocked == blockRival) return;

        playerBlocked = blockPlayer;
        rivalBlocked = blockRival;
        sign.enabled = blockPlayer;
        wall.enabled = blockPlayer || blockRival;
        if (!wall.enabled) return;

        // Racers can arrive on different visits to the same crossing.
        SetCollision(playerColliders, blockPlayer);
        SetCollision(rivalColliders, blockRival);
    }

    private bool InWindow(int next) => next >= firstWaypoint && next <= lastWaypoint;

    private static Collider[] CarColliders(ArcadeVehicle car) =>
        car != null ? car.GetComponentsInChildren<Collider>().Where(collider => !collider.isTrigger).ToArray()
            : System.Array.Empty<Collider>();

    private void SetCollision(Collider[] colliders, bool blocked)
    {
        foreach (var collider in colliders)
            if (collider != null) Physics.IgnoreCollision(wall, collider, !blocked);
    }
}

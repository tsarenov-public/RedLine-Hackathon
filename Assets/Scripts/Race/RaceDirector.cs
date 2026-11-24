using System.Collections;
using System.Collections.Generic;
using Arcade.Vehicle;
using UnityEngine;

public enum RaceMode
{
    [InspectorName("Гонка (кто первый на финише)")] Race,
    [InspectorName("Погоня (соперник проезжает мимо и останавливается на финише)")] Chase
}

[DisallowMultipleComponent]
public sealed class RaceDirector : MonoBehaviour
{
    private const float ReachRadius = 14f;
    private const int LookAheadPoints = 20;
    private const int FinishWindow = 3;

    [Header("Участники")]
    [SerializeField] private ArcadeVehicle player;
    [SerializeField] private ArcadeVehicle rival;
    [SerializeField] private WaypointPath path;

    [Header("Правила")]
    [SerializeField] private RaceMode mode = RaceMode.Race;
    [SerializeField, Min(1)] private int countdownFrom = 3;
    [SerializeField, Min(5f)] private float chaseStartTimeout = 40f;
    [SerializeField, Min(3f)] private float chaseCatchDistance = 14f;

    [Header("Интерфейс")]
    [SerializeField] private RaceFinishUI finishScreen;
    [SerializeField] private RaceCountdownUI countdown;

    private sealed class Racer
    {
        public ArcadeVehicle Car;
        public int Next;
        public bool Finished;
    }

    private readonly List<Vector3> points = new();
    private Racer playerRacer;
    private Racer rivalRacer;
    private bool resultDecided;
    private int stage;

    public bool RaceStarted { get; private set; }
    public ArcadeVehicle Player => player;
    public ArcadeVehicle Rival => rival;

    private void Awake()
    {
        if (path != null)
            foreach (var point in path.GetWaypoints()) points.Add(point.position);
        playerRacer = new Racer { Car = player };
        rivalRacer = new Racer { Car = rival };
        if (rival != null && rival.TryGetComponent(out AIRacerController ai)) ai.UseFinishGate(mode == RaceMode.Race);
    }

    private IEnumerator Start()
    {
        stage = GameProgressionManager.CurrentStage;
        ParkBystanders();
        Begin(playerRacer);
        Begin(rivalRacer);
        yield return mode == RaceMode.Chase ? ChaseStart() : Countdown();
    }

    private IEnumerator Countdown()
    {
        SetHold(playerRacer, true);
        SetHold(rivalRacer, true);
        Caption("ПРИГОТОВЬСЯ");
        yield return new WaitForSeconds(1f);
        for (int i = countdownFrom; i > 0; i--)
        {
            Count(i.ToString(), false);
            yield return new WaitForSeconds(1f);
        }

        SetHold(playerRacer, false);
        SetHold(rivalRacer, false);
        RaceStarted = true;
        Count("GO!", true);
        Caption(null);
        yield return new WaitForSeconds(1f);
        HideCountdown();
    }

    private IEnumerator ChaseStart()
    {
        SetHold(playerRacer, true);
        Caption("НАРУШИТЕЛЬ ПРИБЛИЖАЕТСЯ...");
        float timeout = Time.time + chaseStartTimeout;
        while (!RivalPassedPlayer() && Time.time < timeout) yield return null;

        SetHold(playerRacer, false);
        RaceStarted = true;
        if (player != null)
            foreach (var equipment in player.GetComponentsInChildren<PoliceEmergencyEquipment>())
                equipment.SetPursuitActive(true);
        Count("ПОГОНЯ!", true);
        Caption("НЕ УПУСТИ НАРУШИТЕЛЯ");
        yield return new WaitForSeconds(1.8f);
        HideCountdown();
    }

    private void FixedUpdate()
    {
        Track(playerRacer);
        Track(rivalRacer);
        if (mode == RaceMode.Chase && !resultDecided && rivalRacer.Finished && player != null && rival != null &&
            (player.transform.position - rival.transform.position).sqrMagnitude < chaseCatchDistance * chaseCatchDistance)
            Win();
    }

    public void NotifyFinishCrossed(Rigidbody body)
    {
        var racer = Match(body);
        if (racer == null || racer.Finished || !RaceStarted) return;
        Track(racer);
        if (racer.Next < points.Count - FinishWindow) return;

        racer.Finished = true;
        racer.Car.BeginFinishBraking();
        if (resultDecided) return;
        if (racer == playerRacer) Win();
        else if (mode == RaceMode.Race) Lose();
    }

    private void Win()
    {
        resultDecided = true;
        if (player != null && !player.IsFinishing) player.BeginFinishBraking();
        if (rival != null && !rival.IsFinishing) rival.BeginFinishBraking();
        GameProgressionManager.CompleteStage(stage);
        if (finishScreen != null) finishScreen.PresentVictory(player, GameProgressionManager.GetStage(stage));
    }

    private void Lose()
    {
        resultDecided = true;
        if (player != null && !player.IsFinishing) player.BeginFinishBraking();
        if (finishScreen != null) finishScreen.PresentDefeat(GameProgressionManager.GetStage(stage));
    }

    private Racer Match(Rigidbody body)
    {
        if (body == null) return null;
        if (player != null && body == player.Rb) return playerRacer;
        return rival != null && body == rival.Rb ? rivalRacer : null;
    }

    private bool RivalPassedPlayer()
    {
        if (player == null || rival == null) return true;
        var offset = rival.transform.position - player.transform.position;
        return Vector3.Dot(offset, player.transform.forward) > 2f && offset.sqrMagnitude < 80f * 80f;
    }

    private void ParkBystanders()
    {
        foreach (var car in FindObjectsByType<ArcadeVehicle>())
        {
            if (car == player || car == rival || !car.TryGetComponent(out VehicleInputSource input)) continue;
            if (car.TryGetComponent(out AIRacerController ai) && ai.enabled) continue;
            input.HoldPosition = true;
            input.enabled = false;
        }
    }

    private void Begin(Racer racer)
    {
        if (racer.Car == null || points.Count == 0) return;
        var position = racer.Car.transform.position;
        float best = float.PositiveInfinity;
        for (int i = 0; i < Mathf.Min(points.Count, 12); i++)
        {
            float distance = Flat(points[i] - position).sqrMagnitude;
            if (distance >= best) continue;
            best = distance;
            racer.Next = i;
        }
        Track(racer);
    }

    private void Track(Racer racer)
    {
        if (racer.Car == null || racer.Finished || points.Count == 0) return;
        var position = racer.Car.Rb != null ? racer.Car.Rb.position : racer.Car.transform.position;
        int last = Mathf.Min(points.Count - 1, racer.Next + LookAheadPoints);
        for (int i = last; i >= racer.Next; i--)
        {
            var delta = points[i] - position;
            if (Mathf.Abs(delta.y) > 8f || Flat(delta).sqrMagnitude > ReachRadius * ReachRadius) continue;
            racer.Next = i + 1;
            return;
        }
    }

    private static void SetHold(Racer racer, bool hold)
    {
        if (racer.Car == null) return;
        if (racer.Car.TryGetComponent(out AIRacerController ai)) ai.HoldPosition = hold;
        if (racer.Car.TryGetComponent(out VehicleInputSource input)) input.HoldPosition = hold;
    }

    private void Count(string value, bool accent)
    {
        if (countdown != null) countdown.ShowCount(value, accent);
    }

    private void Caption(string value)
    {
        if (countdown != null) countdown.ShowCaption(value);
    }

    private void HideCountdown()
    {
        if (countdown != null) countdown.Hide();
    }

    private static Vector3 Flat(Vector3 value) => new(value.x, 0f, value.z);
}

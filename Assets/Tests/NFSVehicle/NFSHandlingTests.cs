using System.Collections;
using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using RedLine.NFS;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

public sealed class NFSHandlingTests
{
    private readonly List<GameObject> objects = new List<GameObject>();
    private SimulationMode previousSimulationMode;
    private float previousFixedDelta;
    private const float Step = .02f;
    private const float GroundY = 1000;

    [SetUp]
    public void SetUp()
    {
        previousSimulationMode = Physics.simulationMode;
        previousFixedDelta = Time.fixedDeltaTime;
        Physics.simulationMode = SimulationMode.Script;
        Time.fixedDeltaTime = Step;
        var ground = Track(GameObject.CreatePrimitive(PrimitiveType.Cube));
        ground.name = "Handling test ground";
        ground.transform.position = new Vector3(0, GroundY - .5f, 0);
        ground.transform.localScale = new Vector3(1000, 1, 1000);
        Directory.CreateDirectory("Logs/NFSCar");
    }

    [TearDown]
    public void TearDown()
    {
        foreach (var go in objects) if (go) Object.DestroyImmediate(go);
        objects.Clear();
        Physics.simulationMode = previousSimulationMode;
        Time.fixedDeltaTime = previousFixedDelta;
    }

    private GameObject Track(GameObject go) { objects.Add(go); return go; }

    private NFSRaycastCarController CreateCar(float gravity = 3, float brakes = 2)
    {
        var go = Track(new GameObject("Handling test car"));
        go.transform.position = new Vector3(0, GroundY + .6f, 0);
        var box = go.AddComponent<BoxCollider>(); box.size = new Vector3(1.8f, .55f, 4.2f);
        var car = go.AddComponent<NFSRaycastCarController>();
        car.GravityMultiplier = gravity; car.BrakeStrengthMultiplier = brakes;
        car.SetExternalInput(default);
        // Manual physics steps own the clock in these tests.
        car.enabled = false;
        Physics.SyncTransforms();
        return car;
    }

    private static void Simulate(NFSRaycastCarController car, int steps)
    {
        for (int i = 0; i < steps; i++)
        {
            car.SendMessage("FixedUpdate");
            Physics.Simulate(Step);
        }
    }

    [Test]
    public void GravityIsTripledOnlyForCarAndPreservesRideHeight()
    {
        Vector3 gravity = Physics.gravity;
        var car = CreateCar();
        car.transform.position = new Vector3(0, GroundY + 30, 0);
        var other = Track(new GameObject("Ordinary body")).AddComponent<Rigidbody>();
        other.position = new Vector3(10, GroundY + 30, 0);
        Physics.SyncTransforms();
        Simulate(car, 10);
        Assert.That(car.Body.linearVelocity.y / other.linearVelocity.y, Is.InRange(2.98f, 3.02f));
        Assert.That(Physics.gravity, Is.EqualTo(gravity));
        Object.DestroyImmediate(other.gameObject);
        car.Body.position = new Vector3(0, GroundY + .6f, 0);
        car.Body.linearVelocity = Vector3.zero;
        Physics.SyncTransforms();
        Simulate(car, 200);
        float strongHeight = car.Body.position.y - GroundY;
        Assert.That(car.GroundedWheelCount, Is.EqualTo(4));
        Assert.That(Mathf.Abs(car.Body.linearVelocity.y), Is.LessThan(.1f));
        Object.DestroyImmediate(car.gameObject);
        var baseline = CreateCar(1, 1);
        Simulate(baseline, 200);
        Assert.That(strongHeight, Is.EqualTo(baseline.Body.position.y - GroundY).Within(.03f));
        Log("gravity: 3x; ride-height=" + strongHeight);
    }

    [TestCase(60)]
    [TestCase(100)]
    [TestCase(200)]
    public void ServiceBrakesHalveStoppingTimeAndDistance(float speedKmh)
    {
        var baseline = MeasureStop(speedKmh, 1, 1, out float oldTime);
        var tuned = MeasureStop(speedKmh, 3, 2, out float newTime);
        Log($"braking {speedKmh} km/h: {baseline:F2}m/{oldTime:F2}s -> {tuned:F2}m/{newTime:F2}s");
        Assert.That(tuned / baseline, Is.InRange(.4f, .6f));
        Assert.That(newTime / oldTime, Is.InRange(.4f, .6f));
    }

    private float MeasureStop(float speedKmh, float gravity, float brakes, out float time)
    {
        var car = CreateCar(gravity, brakes);
        Simulate(car, 150);
        car.Body.linearVelocity = Vector3.forward * (speedKmh / 3.6f);
        car.SetExternalInput(new NFSRaycastCarController.DriverInput(0, 1, 0));
        float start = car.Body.position.z;
        int frames = 0;
        while (car.Body.linearVelocity.z > .3f && frames < 600) { Simulate(car, 1); frames++; }
        time = frames * Step;
        Assert.That(frames, Is.LessThan(600), "Car failed to stop.");
        Assert.That(Mathf.Abs(car.Body.position.x), Is.LessThan(.15f));
        Assert.That(Vector3.Dot(car.transform.up, Vector3.up), Is.GreaterThan(.98f));
        float distance = car.Body.position.z - start;
        Object.DestroyImmediate(car.gameObject);
        return distance;
    }

    [Test]
    public void StrongerGravityPreservesOrdinaryAcceleration()
    {
        var baseline = CreateCar(1, 1); Simulate(baseline, 150);
        baseline.SetExternalInput(new NFSRaycastCarController.DriverInput(1, 0, 0));
        Simulate(baseline, 200);
        float oldSpeed = baseline.Body.linearVelocity.z;
        Object.DestroyImmediate(baseline.gameObject);
        var tuned = CreateCar(); Simulate(tuned, 150);
        tuned.SetExternalInput(new NFSRaycastCarController.DriverInput(1, 0, 0));
        Simulate(tuned, 200);
        float newSpeed = tuned.Body.linearVelocity.z;
        Log($"ordinary acceleration after 4s: {oldSpeed * 3.6f:F1} -> {newSpeed * 3.6f:F1} km/h");
        Assert.That(newSpeed / oldSpeed, Is.InRange(.95f, 1.05f));
    }

    [Test]
    public void CarSettlesAfterJumpWithAllWheelsAndCanSteerUnderBraking()
    {
        var car = CreateCar();
        car.Body.position = new Vector3(0, GroundY + 5, 0);
        car.Body.linearVelocity = new Vector3(0, 4, 12);
        car.Body.rotation = Quaternion.Euler(15, 0, 10);
        Physics.SyncTransforms();
        Simulate(car, 250);
        Assert.That(car.GroundedWheelCount, Is.EqualTo(4));
        Assert.That(Vector3.Dot(car.transform.up, Vector3.up), Is.GreaterThan(.98f));
        Assert.That(Mathf.Abs(car.Body.linearVelocity.y), Is.LessThan(.15f));
        car.Body.linearVelocity = car.transform.forward * (100 / 3.6f);
        car.SetExternalInput(new NFSRaycastCarController.DriverInput(0, .6f, .35f));
        Simulate(car, 30);
        Assert.That(Mathf.Abs(Vector3.Dot(car.Body.angularVelocity, Vector3.up)), Is.GreaterThan(.05f));
        Assert.That(Vector3.Dot(car.transform.up, Vector3.up), Is.GreaterThan(.9f));
    }

    [UnityTest]
    public IEnumerator TunedCameraRetriggersNitroImpulseAndAvoidsWall()
    {
        var car = CreateCar(); Simulate(car, 150); car.Body.isKinematic = true;
        var cameraObject = Track(new GameObject("Handling test camera", typeof(Camera), typeof(NFSArcadeCamera)));
        var camera = cameraObject.GetComponent<Camera>();
        var chase = cameraObject.GetComponent<NFSArcadeCamera>(); chase.Target = car;
        yield return null;
        Assert.That(cameraObject.transform.position.z - car.transform.position.z, Is.EqualTo(-5.07f).Within(.02f));
        Assert.That(cameraObject.transform.position.y - car.transform.position.y, Is.EqualTo(1.5f).Within(.02f));
        car.SetExternalInput(new NFSRaycastCarController.DriverInput(1, 0, 0, nitro: true));
        car.SendMessage("FixedUpdate");
        yield return new WaitForSeconds(.18f);
        float firstPeak = camera.fieldOfView;
        float sustainedFOV = Mathf.Lerp(chase.BaseFOV, chase.NitroFOV, chase.FOVExpansionMultiplier);
        Assert.That(firstPeak, Is.GreaterThan(sustainedFOV + 2));
        yield return new WaitForSeconds(.7f);
        Assert.That(camera.fieldOfView, Is.EqualTo(sustainedFOV).Within(.5f));
        car.SetExternalInput(default); car.SendMessage("FixedUpdate");
        yield return new WaitForSeconds(.8f);
        Assert.That(camera.fieldOfView, Is.EqualTo(chase.BaseFOV).Within(1));
        car.SetExternalInput(new NFSRaycastCarController.DriverInput(1, 0, 0, nitro: true));
        car.SendMessage("FixedUpdate");
        yield return new WaitForSeconds(.18f);
        Assert.That(camera.fieldOfView, Is.GreaterThan(sustainedFOV + 2));
        var wall = Track(GameObject.CreatePrimitive(PrimitiveType.Cube));
        wall.transform.position = car.transform.position + new Vector3(0, 1, -3);
        wall.transform.localScale = new Vector3(20, 10, .4f);
        Physics.SyncTransforms();
        yield return null;
        Assert.That(cameraObject.transform.position.z, Is.GreaterThan(wall.transform.position.z + .4f));
        Log($"camera: 5.07m / 1.5m; nitro impulse FOV={firstPeak:F1}; repeat and obstruction passed");
    }

    [UnityTest]
    public IEnumerator CameraMaintainsRelativeFramingAtTopSpeedAndRaisesRoadView()
    {
        var car = CreateCar();
        car.Body.useGravity = false; car.Body.interpolation = RigidbodyInterpolation.None;
        var cameraObject = Track(new GameObject("Speed framing test camera", typeof(Camera), typeof(NFSArcadeCamera)));
        var chase = cameraObject.GetComponent<NFSArcadeCamera>();
        chase.Target = car; chase.enabled = false; chase.ShakeAmount = 0;
        var camera = cameraObject.GetComponent<Camera>();
        float previousCaptureDelta = Time.captureDeltaTime;
        try
        {
            foreach (int fps in new[] { 30, 120 })
            {
                Time.captureDeltaTime = 1f / fps;
                yield return null;
                chase.SnapToTarget();
                foreach (float kmh in new[] { 0f, car.MaxSpeedKmh, car.MaxSpeedKmh + car.NitroMaxSpeedBonus, 0f })
                {
                    Vector3 velocity = Vector3.forward * (kmh / 3.6f);
                    // Advance the target every render frame. A stationary-target test would
                    // miss the speed-dependent lag caused by world-space SmoothDamp.
                    for (int frame = 0; frame < fps * 2; frame++)
                    {
                        car.transform.position += velocity * Time.deltaTime;
                        car.Body.linearVelocity = velocity;
                        Physics.SyncTransforms();
                        car.SendMessage("FixedUpdate");
                        chase.SendMessage("LateUpdate");
                        yield return null;
                    }
                    float speed = Mathf.Clamp01(kmh / car.MaxSpeedKmh);
                    float distance = chase.FollowDistance * chase.FollowDistanceMultiplier
                        * (speed >= 1 ? chase.MaxSpeedDistanceScale : chase.LowSpeedDistanceScale);
                    float height = chase.FollowHeight * chase.FollowDistanceMultiplier + (speed >= 1 ? chase.MaxSpeedHeightBoost : 0);
                    Assert.That(car.transform.position.z - cameraObject.transform.position.z, Is.EqualTo(distance).Within(.04f), $"distance at {kmh} km/h, {fps} FPS");
                    Assert.That(cameraObject.transform.position.y - car.transform.position.y, Is.EqualTo(height).Within(.04f));
                    Assert.That(camera.fieldOfView, Is.EqualTo(Mathf.Lerp(chase.BaseFOV, chase.MaxSpeedFOV, speed * chase.FOVExpansionMultiplier)).Within(.2f));
                    if (speed >= 1)
                    {
                        Vector3 road = car.transform.position + Vector3.forward * 25 - Vector3.up * .6f;
                        Vector3 view = camera.WorldToViewportPoint(road);
                        Assert.That(view.z, Is.GreaterThan(0));
                        Assert.That(view.y, Is.InRange(.35f, .95f), "Road ahead is outside the usable camera view.");
                    }
                    Log($"camera {kmh:F0} km/h, {fps} FPS: distance={car.transform.position.z - cameraObject.transform.position.z:F2}m; height={height:F2}m; FOV={camera.fieldOfView:F1}");
                }
            }
        }
        finally { Time.captureDeltaTime = previousCaptureDelta; }
    }

    [TestCase(120, 1)]
    [TestCase(320, .8f)]
    public void HighSpeedSteeringIsReducedWithoutChangingOrdinaryCornering(float kmh, float expectedRatio)
    {
        float original = MeasureTurn(kmh, 1);
        float tuned = MeasureTurn(kmh, .8f);
        Assert.That(original, Is.GreaterThan(.02f), "Baseline failed to turn.");
        Assert.That(tuned / original, Is.EqualTo(expectedRatio).Within(.07f));
        Log($"steering {kmh} km/h: yaw {original:F3} -> {tuned:F3} rad/s");
    }

    private float MeasureTurn(float kmh, float steeringScale)
    {
        var car = CreateCar(); Simulate(car, 150);
        car.HighSpeedSteeringScale = steeringScale;
        car.Body.linearVelocity = Vector3.forward * (kmh / 3.6f);
        car.SetExternalInput(new NFSRaycastCarController.DriverInput(0, 0, .25f));
        Simulate(car, 10);
        float yaw = Mathf.Abs(car.Body.angularVelocity.y);
        Object.DestroyImmediate(car.gameObject);
        return yaw;
    }

    private static void Log(string message)
    {
        Debug.Log("[NFS handling] " + message);
        File.AppendAllText("Logs/NFSCar/handling-metrics.txt", message + "\n");
    }
}

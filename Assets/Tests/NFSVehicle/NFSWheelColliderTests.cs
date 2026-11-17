#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using RedLine.NFS;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

public sealed class NFSWheelColliderTests
{
    private readonly List<GameObject> objects = new List<GameObject>();
    private SimulationMode previousMode;
    private float previousFixedDelta;
    private const string Prefab = "Assets/GeneratedVehicles/AutoSetup/NFS_TestCar_5811e443/NFS_TestCar_Ready.prefab";

    [SetUp]
    public void SetUp()
    {
        previousMode = Physics.simulationMode; previousFixedDelta = Time.fixedDeltaTime;
        Physics.simulationMode = SimulationMode.Script; Time.fixedDeltaTime = .02f;
        var ground = Track(GameObject.CreatePrimitive(PrimitiveType.Cube));
        ground.transform.position = new Vector3(0, 999.5f, 0); ground.transform.localScale = new Vector3(500, 1, 500);
    }

    [TearDown]
    public void TearDown()
    {
        foreach (var go in objects) if (go) Object.DestroyImmediate(go);
        objects.Clear(); Physics.simulationMode = previousMode; Time.fixedDeltaTime = previousFixedDelta;
    }

    private GameObject Track(GameObject go) { objects.Add(go); return go; }
    private static void Simulate(NFSRaycastCarController car, int steps)
    { for (int i = 0; i < steps; i++) { car.SendMessage("FixedUpdate"); Physics.Simulate(.02f); } }

    private NFSRaycastCarController CreateCar()
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(Prefab); Assert.That(prefab, Is.Not.Null);
        var go = Track(Object.Instantiate(prefab)); go.transform.position = new Vector3(0, 1001, 0);
        var car = go.GetComponent<NFSRaycastCarController>(); car.enabled = false;
        go.GetComponent<NFSCarVFXAndAudio>().enabled = false;
        car.SetExternalInput(default); Physics.SyncTransforms(); return car;
    }

    [Test]
    public void GeneratedPrefabSupportsAcceleratesBrakesAndSteersOnNativeWheels()
    {
        var car = CreateCar(); Simulate(car, 200);
        Assert.That(car.GroundedWheelCount, Is.EqualTo(4));
        Assert.That(Mathf.Abs(car.Body.linearVelocity.y), Is.LessThan(.15f));
        Assert.That(car.GetComponent<BoxCollider>(), Is.Null, "A legacy box would bypass the generated silhouette.");
        var material = car.GetComponentInChildren<MeshCollider>().sharedMaterial;
        Assert.That(material.bounciness, Is.EqualTo(.05f).Within(.0001f), "Awake must preserve the assigned material.");
        car.SetExternalInput(new NFSRaycastCarController.DriverInput(1, 0, 0)); Simulate(car, 200);
        float speed = car.SpeedKmh; Assert.That(speed, Is.GreaterThan(45));
        float before = car.Body.linearVelocity.magnitude;
        car.SetExternalInput(new NFSRaycastCarController.DriverInput(0, 1, .15f)); Simulate(car, 35);
        Assert.That(car.Body.linearVelocity.magnitude, Is.LessThan(before * .85f));
        Assert.That(Mathf.Abs(car.Wheels[0].PhysicsCollider.steerAngle), Is.GreaterThan(.1f));
        Assert.That(car.Wheels[2].PhysicsCollider.steerAngle, Is.Zero);
        Assert.That(Vector3.Dot(car.transform.up, Vector3.up), Is.GreaterThan(.85f));
        car.SendMessage("LateUpdate"); car.Wheels[0].PhysicsCollider.GetWorldPose(out var pose, out _);
        Assert.That(Vector3.Distance(car.Wheels[0].Visual.position, pose), Is.LessThan(.001f));
        Directory.CreateDirectory("Logs/NFSCar");
        File.WriteAllText("Logs/NFSCar/auto-setup-physics.txt", $"PlayMode: native four-wheel support; speed after 4 seconds: {speed:F2} km/h; braking: {before:F2} -> {car.Body.linearVelocity.magnitude:F2} m/s.\n");
    }

    [Test]
    public void RaycastModeDisablesNativeSpringsAndDisableClearsNativeTorque()
    {
        var parent = Track(new GameObject("Inactive setup parent")); parent.SetActive(false);
        var go = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(Prefab), parent.transform);
        var car = go.GetComponent<NFSRaycastCarController>(); car.UseWheelColliders = false;
        go.transform.position = new Vector3(0, 1001, 0); parent.SetActive(true);
        foreach (var wheel in car.Wheels) Assert.That(wheel.PhysicsCollider.enabled, Is.False, "Two suspensions must never run together.");
        Object.DestroyImmediate(parent);
        car = CreateCar(); car.enabled = true;
        foreach (var wheel in car.Wheels) { wheel.PhysicsCollider.motorTorque = 500; wheel.PhysicsCollider.brakeTorque = 100; }
        car.enabled = false;
        foreach (var wheel in car.Wheels)
        { Assert.That(wheel.PhysicsCollider.motorTorque, Is.Zero); Assert.That(wheel.PhysicsCollider.brakeTorque, Is.Zero); }
    }
}
#endif

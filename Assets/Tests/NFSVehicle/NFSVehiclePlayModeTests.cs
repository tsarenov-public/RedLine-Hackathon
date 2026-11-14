using System.Collections;
using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using RedLine.NFS;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.TestTools;

public sealed class NFSVehiclePlayModeTests
{
    private NFSRaycastCarController car;
    private readonly List<GameObject> objects = new List<GameObject>();
    private float oldTimeScale, oldFixedStep, oldMaximumDelta;
    private const float Floor = 1000;
    private static readonly WaitForFixedUpdate Step = new WaitForFixedUpdate();

    [UnitySetUp]
    public IEnumerator SetUp()
    {
        oldTimeScale = Time.timeScale; oldFixedStep = Time.fixedDeltaTime; oldMaximumDelta = Time.maximumDeltaTime;
        Time.timeScale = 12; Time.fixedDeltaTime = .02f; Time.maximumDeltaTime = .5f;
        var ground = GameObject.CreatePrimitive(PrimitiveType.Cube); ground.name = "NFS test floor";
        ground.transform.position = new Vector3(0, Floor - .5f, 0); ground.transform.localScale = new Vector3(20000, 1, 20000); objects.Add(ground);
        var go = new GameObject("NFS physics fixture"); go.transform.position = new Vector3(0, Floor + 1, 0); objects.Add(go);
        var box = go.AddComponent<BoxCollider>(); box.size = new Vector3(1.8f, .55f, 4.2f);
        car = go.AddComponent<NFSRaycastCarController>(); car.SetExternalInput(default);
        Physics.SyncTransforms();
        for (int i = 0; i < 100; i++) yield return Step;
    }

    [UnityTearDown]
    public IEnumerator TearDown()
    {
        foreach (var go in objects) if (go) Object.Destroy(go); objects.Clear();
        yield return null;
        Time.timeScale = oldTimeScale; Time.fixedDeltaTime = oldFixedStep; Time.maximumDeltaTime = oldMaximumDelta;
    }

    [UnityTest]
    public IEnumerator SuspensionSettlesAndNullEffectsAreSafe()
    {
        Assert.That(car.GroundedWheelCount, Is.EqualTo(4));
        Assert.That(car.transform.position.y - Floor, Is.InRange(.35f, .8f));
        Assert.That(Mathf.Abs(car.Body.linearVelocity.y), Is.LessThan(.2f));
        Assert.That(Vector3.Dot(car.transform.up, Vector3.up), Is.GreaterThan(.99f));
        for (int i = 0; i < 4; i++)
        {
            var contact = car.GetWheelContact(i);
            Assert.That(contact.Collider.attachedRigidbody, Is.Not.EqualTo(car.Body));
            Assert.That(contact.Compression, Is.InRange(.03f, .15f));
        }
        car.gameObject.AddComponent<NFSCarVFXAndAudio>();
        var camera = new GameObject("Null target camera", typeof(Camera), typeof(NFSArcadeCamera)); objects.Add(camera);
        yield return null; LogAssert.NoUnexpectedReceived();
    }

    [UnityTest]
    public IEnumerator AcceleratesShiftsBrakesAndChangesDirection()
    {
        car.SetExternalInput(new NFSRaycastCarController.DriverInput(1, 0, 0));
        for (int i = 0; i < 450; i++) yield return Step;
        Record("acceleration", $"speed={car.SpeedKmh:F1}, gear={car.CurrentGear}, rpm={car.CurrentRPM:F0}");
        Assert.That(car.SpeedKmh, Is.GreaterThan(100)); Assert.That(car.CurrentGear, Is.GreaterThanOrEqualTo(2));
        Assert.That(car.CurrentRPM, Is.InRange(car.IdleRPM, car.MaxRPM));
        car.SetExternalInput(new NFSRaycastCarController.DriverInput(0, 1, 0));
        for (int i = 0; i < 500 && car.CurrentGear > 0; i++) yield return Step;
        Assert.That(car.CurrentGear, Is.EqualTo(-1), "Brake must reach a stop before engaging reverse.");
        for (int i = 0; i < 150; i++) yield return Step;
        Assert.That(car.ForwardSpeed, Is.LessThan(-3)); Assert.That(car.SpeedKmh, Is.LessThan(44));
        car.SetExternalInput(new NFSRaycastCarController.DriverInput(1, 0, 0));
        for (int i = 0; i < 350; i++) yield return Step;
        Assert.That(car.CurrentGear, Is.GreaterThan(0)); Assert.That(car.ForwardSpeed, Is.GreaterThan(5));
    }

    [UnityTest]
    public IEnumerator NitroConsumesRegeneratesAndAllowsHigherSpeed()
    {
        car.SetExternalInput(new NFSRaycastCarController.DriverInput(1, 0, 0));
        for (int i = 0; i < 600; i++) yield return Step;
        float speedBefore = car.SpeedKmh;
        car.SetExternalInput(new NFSRaycastCarController.DriverInput(1, 0, 0, false, true));
        for (int i = 0; i < 100; i++) yield return Step;
        Assert.That(car.IsNitroActive, Is.True); Assert.That(car.NitroAmount, Is.InRange(40, 55));
        Assert.That(car.SpeedKmh, Is.GreaterThan(speedBefore + 5));
        float charge = car.NitroAmount;
        car.SetExternalInput(new NFSRaycastCarController.DriverInput(1, 0, 0));
        for (int i = 0; i < 75; i++) yield return Step;
        Assert.That(car.NitroAmount, Is.GreaterThan(charge + 5));
        for (int i = 0; i < 2200; i++) yield return Step;
        Record("top-speed", $"speed={car.SpeedKmh:F1}, gear={car.CurrentGear}, rpm={car.CurrentRPM:F0}");
        Assert.That(car.SpeedKmh, Is.InRange(285, car.MaxSpeedKmh + 4));
        car.SetExternalInput(new NFSRaycastCarController.DriverInput(1, 0, 0, false, true));
        for (int i = 0; i < 165; i++) yield return Step;
        Record("nitro-speed", $"speed={car.SpeedKmh:F1}, charge={car.NitroAmount:F1}");
        Assert.That(car.SpeedKmh, Is.GreaterThan(car.MaxSpeedKmh + 4));
        for (int i = 0; i < 150; i++) yield return Step;
        Assert.That(car.NitroAmount, Is.GreaterThanOrEqualTo(0)); Assert.That(car.IsNitroActive, Is.False);
    }

    [UnityTest]
    public IEnumerator HandbrakeDriftRemainsControllable()
    {
        car.Body.linearVelocity = Vector3.forward * 23;
        car.SetExternalInput(new NFSRaycastCarController.DriverInput(.85f, 0, .65f, true));
        bool drifted = false; float maximumSlip = 0, maximumYaw = 0;
        for (int i = 0; i < 260; i++)
        {
            if (i == 30) car.SetExternalInput(new NFSRaycastCarController.DriverInput(.85f, 0, .45f));
            yield return Step;
            drifted |= car.IsDrifting; maximumSlip = Mathf.Max(maximumSlip, Mathf.Abs(car.SlipAngle));
            maximumYaw = Mathf.Max(maximumYaw, Mathf.Abs(Vector3.Dot(car.Body.angularVelocity, Vector3.up)));
        }
        Record("drift", $"slip={maximumSlip:F1}, yaw={maximumYaw:F2}, speed={car.SpeedKmh:F1}");
        Assert.That(drifted, Is.True); Assert.That(maximumSlip, Is.LessThan(95));
        Assert.That(maximumYaw, Is.LessThan(car.MaxYawRate + .3f)); Assert.That(car.SpeedKmh, Is.GreaterThan(25));
        Assert.That(Vector3.Dot(car.transform.up, Vector3.up), Is.GreaterThan(.9f));
    }

    [UnityTest]
    public IEnumerator AirborneCarLevelsAndLandsOnFourWheels()
    {
        car.Body.position += Vector3.up * 4; car.Body.rotation = Quaternion.Euler(22, 35, 28);
        car.Body.linearVelocity = Vector3.zero; car.Body.angularVelocity = Vector3.zero;
        float tiltBefore = Vector3.Angle(car.Body.rotation * Vector3.up, Vector3.up);
        for (int i = 0; i < 22; i++) yield return Step;
        Assert.That(car.GroundedWheelCount, Is.Zero);
        Assert.That(Vector3.Angle(car.Body.rotation * Vector3.up, Vector3.up), Is.LessThan(tiltBefore * .7f));
        for (int i = 0; i < 150; i++) yield return Step;
        Assert.That(car.GroundedWheelCount, Is.EqualTo(4)); Assert.That(Vector3.Dot(car.transform.up, Vector3.up), Is.GreaterThan(.98f));
        car.Body.position += Vector3.up * 8; car.Body.rotation = Quaternion.Euler(0, 35, 180);
        car.Body.linearVelocity = Vector3.zero; car.Body.angularVelocity = Vector3.zero;
        for (int i = 0; i < 55; i++) yield return Step;
        Assert.That(Vector3.Dot(car.Body.rotation * Vector3.up, Vector3.up), Is.GreaterThan(.85f), "An upside-down car must also recover in the air.");
        for (int i = 0; i < 125; i++) yield return Step;
        Assert.That(car.GroundedWheelCount, Is.EqualTo(4));
    }

    [UnityTest]
    public IEnumerator ShallowWallCollisionPreservesTangentialSpeed()
    {
        var wall = GameObject.CreatePrimitive(PrimitiveType.Cube); wall.name = "NFS wall test";
        wall.transform.position = new Vector3(6, Floor + 3, 40); wall.transform.localScale = new Vector3(.4f, 6, 100); objects.Add(wall);
        car.Body.position += Vector3.right * 4.6f;
        Physics.SyncTransforms(); car.Body.linearVelocity = new Vector3(5, 0, 32);
        bool wallSlide = false;
        for (int i = 0; i < 115; i++) { yield return Step; wallSlide |= car.IsWallSliding; }
        Record("wall", $"position={car.Body.position}, velocity={car.Body.linearVelocity}");
        Assert.That(wallSlide, Is.True); Assert.That(car.Body.position.x, Is.LessThan(5.1f));
        Assert.That(car.Body.linearVelocity.z, Is.GreaterThan(24)); Assert.That(Mathf.Abs(car.Body.linearVelocity.y), Is.LessThan(2));
    }

    [UnityTest]
    public IEnumerator DefaultGamepadBindingsAndCameraFovRespond()
    {
        var pad = InputSystem.AddDevice<Gamepad>();
        try
        {
            car.UsePlayerInput(); InputSystem.QueueStateEvent(pad, new GamepadState { rightTrigger = 1 }.WithButton(GamepadButton.East));
            var go = new GameObject("NFS chase test", typeof(Camera)); objects.Add(go);
            var camera = go.GetComponent<Camera>(); var follow = go.AddComponent<NFSArcadeCamera>(); follow.Target = car;
            for (int i = 0; i < 75; i++) yield return Step;
            yield return null;
            Assert.That(car.ThrottleInput, Is.GreaterThan(.9f)); Assert.That(car.IsNitroActive, Is.True);
            Assert.That(camera.fieldOfView, Is.GreaterThan(80)); Assert.That(Vector3.Distance(go.transform.position, car.transform.position), Is.InRange(2, 12));
        }
        finally { InputSystem.RemoveDevice(pad); }
    }

#if UNITY_EDITOR
    [UnityTest]
    public IEnumerator SavedMissionCarDrivesOnActualRoad()
    {
        var load = UnityEditor.SceneManagement.EditorSceneManager.LoadSceneAsyncInPlayMode("Assets/Scenes/FistMission.unity",
            new UnityEngine.SceneManagement.LoadSceneParameters(UnityEngine.SceneManagement.LoadSceneMode.Additive));
        while (!load.isDone) yield return null;
        var scene = UnityEngine.SceneManagement.SceneManager.GetSceneByPath("Assets/Scenes/FistMission.unity");
        try
        {
            var missionCar = System.Array.Find(scene.GetRootGameObjects(), g => g.GetComponent<NFSRaycastCarController>()).GetComponent<NFSRaycastCarController>();
            missionCar.SetExternalInput(new NFSRaycastCarController.DriverInput(1, 0, 0));
            Vector3 start = missionCar.Body.position;
            for (int i = 0; i < 120; i++) yield return Step;
            LogAssert.Expect(LogType.Log, new System.Text.RegularExpressions.Regex(@"^\[NFS physics\] mission-road:"));
            Record("mission-road", $"speed={missionCar.SpeedKmh:F1}, wheels={missionCar.GroundedWheelCount}, distance={Vector3.Distance(start, missionCar.Body.position):F1}");
            Assert.That(missionCar.GroundedWheelCount, Is.GreaterThanOrEqualTo(3));
            Assert.That(missionCar.SpeedKmh, Is.GreaterThan(25)); Assert.That(Vector3.Distance(start, missionCar.Body.position), Is.GreaterThan(8));
            LogAssert.NoUnexpectedReceived();
        }
        finally { UnityEngine.SceneManagement.SceneManager.UnloadSceneAsync(scene); }
        yield return null;
    }

    [UnityTest]
    public IEnumerator GeneratedPrefabRunsWheelVisualsSkidsSmokeAudioAndNitro()
    {
        var prefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/GeneratedVehicles/NFS/NFS_TestCar.prefab");
        Assert.That(prefab, Is.Not.Null, "Run NFSCarSetupValidation.PrepareAndVerify to create the test prefab first.");
        Object.Destroy(car.gameObject); yield return null;
        var go = Object.Instantiate(prefab, new Vector3(0, Floor + 1, 0), Quaternion.identity); objects.Add(go);
        car = go.GetComponent<NFSRaycastCarController>(); car.SetExternalInput(default);
        var cameraObject = new GameObject("Full prefab chase", typeof(Camera), typeof(AudioListener)); objects.Add(cameraObject);
        var follow = cameraObject.AddComponent<NFSArcadeCamera>(); follow.Target = car;
        for (int i = 0; i < 100; i++) yield return Step;
        for (int i = 0; i < 4; i++)
        {
            var contact = car.GetWheelContact(i);
            Assert.That(Vector3.Distance(car.Wheels[i].Visual.position, contact.Point + contact.Normal * car.Wheels[i].Radius), Is.LessThan(.06f));
        }
        car.Body.linearVelocity = new Vector3(5, 0, 18);
        car.SetExternalInput(new NFSRaycastCarController.DriverInput(.9f, 0, .25f, true));
        for (int i = 0; i < 20; i++) yield return Step;
        yield return null;
        var vfx = car.GetComponent<NFSCarVFXAndAudio>();
        Assert.That(vfx.RearSmoke[0].emission.enabled, Is.True); Assert.That(vfx.RearSkids[0].emitting, Is.True);
        Assert.That(vfx.RearSkids[0].transform.position.y, Is.EqualTo(Floor + .02f).Within(.006f));
        Assert.That(vfx.EngineAudio.pitch, Is.GreaterThan(vfx.IdlePitch)); Assert.That(vfx.TyreAudio.volume, Is.GreaterThan(0));
        car.SetExternalInput(new NFSRaycastCarController.DriverInput(1, 0, 0, false, true));
        for (int i = 0; i < 45; i++) yield return Step;
        yield return null;
        Assert.That(car.IsNitroActive, Is.True); Assert.That(vfx.NitroFlames[0].emission.enabled, Is.True);
        Assert.That(cameraObject.GetComponent<Camera>().fieldOfView, Is.GreaterThan(80));
        LogAssert.NoUnexpectedReceived();
    }
#endif

    private static void Record(string name, string data)
    {
        Directory.CreateDirectory("Logs/NFSCar"); File.AppendAllText("Logs/NFSCar/physics-metrics.txt", name + ": " + data + "\n");
        Debug.Log("[NFS physics] " + name + ": " + data);
    }
}

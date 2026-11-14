using System;
using System.IO;
using System.Linq;
using RedLine.NFS;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

/// <summary>Batch integration checks for both setup-wizard buttons and saved prefab references.</summary>
public static class NFSCarSetupValidation
{
    public static void PrepareAndVerify()
    {
        var scene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);
        var car = NFSCarSetupWizard.SpawnReadyTestCar(); ValidateCar(car);
        Undo.FlushUndoRecordObjects(); Undo.PerformUndo();
        Require(Object.FindObjectsByType<NFSRaycastCarController>().Length == 0, "Undo left a generated car behind.");
        Undo.PerformRedo(); car = Object.FindAnyObjectByType<NFSRaycastCarController>(); ValidateCar(car);
        var model = new GameObject("ValidationCustomModel"); model.transform.position = new Vector3(20, 1, 0); model.transform.rotation = Quaternion.Euler(0, 23, 0);
        var body = GameObject.CreatePrimitive(PrimitiveType.Cube); body.transform.SetParent(model.transform, false); body.transform.localScale = new Vector3(1.8f, .55f, 4);
        Object.DestroyImmediate(body.GetComponent<Collider>());
        var wheels = new Transform[4];
        for (int i = 0; i < 4; i++)
        {
            wheels[i] = new GameObject("Wheel_" + i).transform; wheels[i].SetParent(model.transform, false);
            wheels[i].localPosition = new Vector3(i % 2 == 0 ? -.85f : .85f, -.17f, i < 2 ? 1.35f : -1.35f);
            var mesh = GameObject.CreatePrimitive(PrimitiveType.Cylinder); mesh.transform.SetParent(wheels[i], false);
            mesh.transform.localPosition = new Vector3(.05f, 0, 0); // Deliberately offset the model's wheel pivot.
            mesh.transform.localRotation = Quaternion.Euler(0, 0, 90); mesh.transform.localScale = new Vector3(.68f, .14f, .68f);
            Object.DestroyImmediate(mesh.GetComponent<Collider>());
        }
        string modelAsset = AssetDatabase.GenerateUniqueAssetPath("Assets/GeneratedVehicles/NFS/ValidationOnlyModel.prefab");
        PrefabUtility.SaveAsPrefabAsset(model, modelAsset);
        var bound = NFSCarSetupWizard.BindCustomModel(model, wheels); ValidateCar(bound);
        string boundAsset = PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(bound);
        Require(!model.activeSelf, "The original scene model should be hidden, not duplicated visually.");
        Require(bound.Wheels.All(w => w.Visual.parent == bound.transform), "Custom wheels were not isolated from the body.");
        Undo.FlushUndoRecordObjects(); Undo.PerformUndo();
        Require(model.activeSelf, "Undo did not restore the original custom model.");
        var asset = AssetDatabase.LoadAssetAtPath<GameObject>(modelAsset);
        var assetWheels = Enumerable.Range(0, 4).Select(i => asset.transform.Find("Wheel_" + i)).ToArray();
        var boundFromAsset = NFSCarSetupWizard.BindCustomModel(asset, assetWheels); ValidateCar(boundFromAsset);
        string boundFromAssetPath = PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(boundFromAsset);
        Undo.FlushUndoRecordObjects(); Undo.PerformUndo(); Undo.ClearAll();
        Object.DestroyImmediate(model);
        AssetDatabase.DeleteAsset(boundAsset); AssetDatabase.DeleteAsset(boundFromAssetPath); AssetDatabase.DeleteAsset(modelAsset);
        car = Object.FindAnyObjectByType<NFSRaycastCarController>(); ValidateCar(car);
        Require(EditorSceneManager.SaveScene(scene, "Assets/Scenes/NFSCarTest.unity"), "Could not save test scene.");
        scene = EditorSceneManager.OpenScene("Assets/Scenes/NFSCarTest.unity", OpenSceneMode.Single);
        ValidateCar(Object.FindAnyObjectByType<NFSRaycastCarController>());
        Directory.CreateDirectory("Logs/NFSCar");
        File.WriteAllText("Logs/NFSCar/setup-verification.txt", "PASS: spawn; undo/redo; custom scene model; custom prefab asset; offset wheel pivots; camera binding; prefab persistence; scene reload.\n");
        Debug.Log("[NFS setup] ALL WIZARD CHECKS PASSED.");
    }

    public static void InstallOnFistMission()
    {
        var scene = EditorSceneManager.OpenScene("Assets/Scenes/FistMission.unity", OpenSceneMode.Single);
        var road = scene.GetRootGameObjects().Single(g => g.name == "MainRoad"); string before = Fingerprint(road);
        var existing = Object.FindAnyObjectByType<NFSRaycastCarController>();
        var car = existing ? existing : NFSCarSetupWizard.SpawnReadyTestCar(); ValidateCar(car);
        Require(Fingerprint(road) == before, "The setup wizard changed MainRoad.");
        Directory.CreateDirectory("Logs/NFSCar");
        if (!File.Exists("Logs/NFSCar/FistMission.BeforeCar.unity")) File.Copy(scene.path, "Logs/NFSCar/FistMission.BeforeCar.unity");
        Require(EditorSceneManager.SaveScene(scene), "Could not save the playable car in FistMission.");
        EditorSceneManager.OpenScene(scene.path, OpenSceneMode.Single); ValidateCar(Object.FindAnyObjectByType<NFSRaycastCarController>());
        File.WriteAllText("Logs/NFSCar/scene-verification.txt", "PASS: FistMission; saved test car and chase camera; MainRoad fingerprint unchanged: " + before + "\n");
        Debug.Log("[NFS setup] FistMission ready; MainRoad unchanged.");
    }

    public static void RenderPreview()
    {
        var scene = EditorSceneManager.OpenScene("Assets/Scenes/NFSCarTest.unity", OpenSceneMode.Single);
        var car = Object.FindAnyObjectByType<NFSRaycastCarController>(); ValidateCar(car);
        // Give the newly-created test scene useful inspection lighting. Existing
        // mission lighting is never edited by this method.
        var sun = Object.FindAnyObjectByType<Light>();
        sun.transform.rotation = Quaternion.Euler(45, 215, 0); sun.intensity = 2.5f; sun.color = Color.white;
        RenderSettings.sun = sun; RenderSettings.ambientMode = AmbientMode.Flat;
        RenderSettings.ambientLight = new Color(.38f, .4f, .44f);
        EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene);
        car.transform.position = new Vector3(0, .51f, 0);
        foreach (var wheel in car.Wheels) wheel.Visual.localPosition = wheel.LocalAnchor - Vector3.up * .27f;
        var go = new GameObject("Temporary NFS preview camera", typeof(Camera));
        var camera = go.GetComponent<Camera>(); camera.enabled = false; camera.fieldOfView = 48;
        go.AddComponent<UnityEngine.Rendering.Universal.UniversalAdditionalCameraData>().renderPostProcessing = false;
        go.transform.position = new Vector3(5, 2.8f, 6.7f); go.transform.LookAt(car.transform.position + Vector3.up * .2f);
        var render = new RenderTexture(1400, 900, 24); render.Create(); var previous = RenderTexture.active;
        Texture2D image = null;
        bool asyncShaders = ShaderUtil.allowAsyncCompilation; ShaderUtil.allowAsyncCompilation = false;
        try
        {
            for (int frame = 0; frame < 3; frame++) RenderPipeline.SubmitRenderRequest(camera, new RenderPipeline.StandardRequest { destination = render });
            RenderTexture.active = render; image = new Texture2D(1400, 900, TextureFormat.RGB24, false);
            image.ReadPixels(new Rect(0, 0, 1400, 900), 0, 0); image.Apply();
            Directory.CreateDirectory("Logs/NFSCar"); File.WriteAllBytes("Logs/NFSCar/test-car.png", image.EncodeToPNG());
        }
        finally
        {
            ShaderUtil.allowAsyncCompilation = asyncShaders;
            RenderTexture.active = previous; if (image) Object.DestroyImmediate(image);
            render.Release(); Object.DestroyImmediate(render); Object.DestroyImmediate(go);
        }
        Debug.Log("[NFS setup] Preview rendered. Test-scene lighting saved; temporary camera/car poses were not saved.");
    }

    private static void ValidateCar(NFSRaycastCarController car)
    {
        Require(car, "Car is missing."); Require(car.Wheels.Length == 4 && car.Wheels.All(w => w.Visual && w.SuspensionPoint), "Wheel references are missing.");
        Require(car.Throttle.bindings.Count >= 3 && car.Steer.bindings.Count >= 7 && car.Nitro.bindings.Count >= 2, "Default input bindings were not saved.");
        var body = car.GetComponent<Rigidbody>(); var box = car.GetComponent<BoxCollider>();
        Require(body && box && body.collisionDetectionMode == CollisionDetectionMode.ContinuousDynamic, "Chassis physics is incomplete.");
        Require(box.sharedMaterial && box.sharedMaterial.dynamicFriction == 0 && box.sharedMaterial.bounciness == 0, "Chassis material is not frictionless.");
        Require(car.GetComponentsInChildren<WheelCollider>(true).Length == 0, "This must be a raycast vehicle.");
        var vfx = car.GetComponent<NFSCarVFXAndAudio>();
        Require(vfx && vfx.Car == car && vfx.RearSmoke.All(p => p) && vfx.RearSkids.All(t => t) && vfx.WallSparks &&
            vfx.NitroFlames.Length == 2 && vfx.NitroFlames.All(p => p), "VFX wiring is incomplete.");
        Require(vfx.EngineAudio && vfx.EngineAudio.clip && vfx.TyreAudio && vfx.TyreAudio.clip, "Generated sound loops are missing.");
        Require(Object.FindObjectsByType<NFSArcadeCamera>().Any(c => c.Target == car), "Chase camera was not bound.");
        foreach (var system in car.GetComponentsInChildren<ParticleSystem>())
            Require(system.GetComponent<ParticleSystemRenderer>().sharedMaterial.shader.name.StartsWith("Universal Render Pipeline/", StringComparison.Ordinal), "A particle material is not URP.");
    }

    private static string Fingerprint(GameObject root)
    {
        var text = new System.Text.StringBuilder();
        foreach (var component in root.GetComponentsInChildren<Component>(true)) if (component) text.Append(EditorJsonUtility.ToJson(component));
        foreach (var filter in root.GetComponentsInChildren<MeshFilter>(true)) if (filter.sharedMesh) text.Append(EditorJsonUtility.ToJson(filter.sharedMesh));
        return Hash128.Compute(text.ToString()).ToString();
    }
    private static void Require(bool condition, string message) { if (!condition) throw new InvalidOperationException("[NFS setup verification] " + message); }
}

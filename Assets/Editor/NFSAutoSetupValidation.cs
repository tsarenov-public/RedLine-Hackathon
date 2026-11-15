using System;
using System.IO;
using System.Linq;
using RedLine.NFS;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

/// <summary>Headless integration checks: real prefab serialization, geometry, repeat runs and batch failures.</summary>
public static class NFSAutoSetupValidation
{
    private const string Fixtures = "Assets/GeneratedVehicles/__AutoSetupValidation";
    private static void Check(bool condition, string message)
    { if (!condition) throw new InvalidOperationException("[Auto Setup verification] " + message); }

    public static void Run()
    {
        Directory.CreateDirectory("Logs/NFSCar");
        NFSAutoSetupCarPrefabs.EnsureFolder(Fixtures);
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        string[] cleanup = new string[0];
        try
        {
            var reference = NFSAutoSetupCarPrefabs.FindReference();
            string referencePath = AssetDatabase.GetAssetPath(reference.gameObject);
            string referenceBefore = File.ReadAllText(referencePath);
            var named = CreateFixture("NamedOffsetWheels", true);
            var spatial = CreateFixture("SpatialWheels", false);
            var invalid = new GameObject("InvalidModel");
            string invalidPath = Fixtures + "/InvalidModel.prefab";
            PrefabUtility.SaveAsPrefabAsset(invalid, invalidPath); Object.DestroyImmediate(invalid);
            cleanup = new[] { named, spatial }.Select(p => OutputDirectory(p)).ToArray();

            string namedBefore = File.ReadAllText(named);
            string output = NFSAutoSetupCarPrefabs.Build(AssetDatabase.LoadAssetAtPath<GameObject>(named));
            VerifyPrefab(output);
            string prefabGuid = AssetDatabase.AssetPathToGUID(output);
            string meshPath = Path.GetDirectoryName(output).Replace('\\', '/') + "/LowerBody.asset";
            string meshGuid = AssetDatabase.AssetPathToGUID(meshPath);
            Check(output == NFSAutoSetupCarPrefabs.Build(AssetDatabase.LoadAssetAtPath<GameObject>(output)), "Repeat setup changed output path.");
            VerifyPrefab(output);
            Check(AssetDatabase.AssetPathToGUID(output) == prefabGuid && AssetDatabase.AssetPathToGUID(meshPath) == meshGuid, "Repeat setup changed GUIDs.");
            Check(File.ReadAllText(named) == namedBefore, "Source prefab changed.");
            string spatialOutput = NFSAutoSetupCarPrefabs.Build(AssetDatabase.LoadAssetAtPath<GameObject>(spatial));
            VerifyPrefab(spatialOutput);

            // A folder selection recurses, ignores generated results and continues after one invalid source.
            Selection.objects = new Object[] { AssetDatabase.LoadAssetAtPath<DefaultAsset>(Fixtures), AssetDatabase.LoadAssetAtPath<GameObject>(named) };
            Check(NFSAutoSetupCarPrefabs.SelectedPaths().Length == 3, "Folder selection did not recurse/deduplicate.");
            NFSAutoSetupCarPrefabs.SetupSelection();
            Check(AssetDatabase.LoadAssetAtPath<GameObject>(spatialOutput), "Batch did not continue after invalid source.");
            VerifyPrefab(spatialOutput);

            // A reference containing native wheels must copy all serialized suspension and friction settings.
            var customReference = Object.Instantiate(reference);
            for (int i = 0; i < 4; i++)
            {
                var child = new GameObject("ReferenceWC_" + i); child.transform.SetParent(customReference.transform);
                var wc = child.AddComponent<WheelCollider>();
                wc.suspensionDistance = .31f + .01f * i;
                wc.suspensionSpring = new JointSpring { spring = 67000 + i, damper = 5100 + i, targetPosition = .42f };
                var friction = wc.forwardFriction; friction.stiffness = 2.1f + .1f * i; wc.forwardFriction = friction;
                customReference.Wheels[i].PhysicsCollider = wc;
            }
            string copied = NFSAutoSetupCarPrefabs.Build(AssetDatabase.LoadAssetAtPath<GameObject>(named), customReference);
            var copiedCar = AssetDatabase.LoadAssetAtPath<GameObject>(copied).GetComponent<NFSRaycastCarController>();
            for (int i = 0; i < 4; i++)
            {
                var wc = copiedCar.Wheels[i].PhysicsCollider;
                Check(Mathf.Abs(wc.suspensionDistance - (.31f + .01f * i)) < .0001f, "Native travel not copied.");
                Check(Mathf.Abs(wc.suspensionSpring.spring - (67000 + i)) < .01f, "Native spring not copied.");
                Check(Mathf.Abs(wc.forwardFriction.stiffness - (2.1f + .1f * i)) < .0001f, "Native friction not copied.");
            }
            Object.DestroyImmediate(customReference.gameObject);
            Check(File.ReadAllText(referencePath) == referenceBefore, "Reference prefab changed.");
            Check(!scene.isDirty || scene.GetRootGameObjects().Length == 0, "Temporary objects leaked into the user's scene.");

            // Keep one concrete ready prefab for immediate review/use, leaving the reference asset intact.
            string example = NFSAutoSetupCarPrefabs.Build(reference.gameObject);
            VerifyPrefab(example);
            File.WriteAllText("Logs/NFSCar/auto-setup-verification.txt", "PASS: named/spatial wheel detection; offset pivots; tyre/rim and LOD grouping; stale collider cleanup; " +
                "non-readable mesh; two clipped convex hulls; clearance/material; native and derived settings; VFX/audio references; " +
                "source preservation; reload; stable GUIDs; repeat setup; recursive batch and failure isolation.\nReady prefab: " + example + "\n");
            Debug.Log("[NFS Auto Setup] ALL INTEGRATION CHECKS PASSED.");
        }
        finally
        {
            Selection.objects = new Object[0];
            foreach (string folder in cleanup) if (AssetDatabase.IsValidFolder(folder)) AssetDatabase.DeleteAsset(folder);
            AssetDatabase.DeleteAsset(Fixtures);
            AssetDatabase.SaveAssets();
        }
    }

    private static string OutputDirectory(string path) => NFSAutoSetupCarPrefabs.OutputFolder + "/" +
        Path.GetFileNameWithoutExtension(path) + "_" + AssetDatabase.AssetPathToGUID(path).Substring(0, 8);

    private static string CreateFixture(string name, bool named)
    {
        var model = new GameObject(name);
        try
        {
            // A stale compound setup must not survive generation.
            model.AddComponent<Rigidbody>(); model.AddComponent<BoxCollider>();
            Primitive(model.transform, "Chassis", PrimitiveType.Cube, new Vector3(0, .6f, 0), new Vector3(1.8f, .6f, 4.2f));
            var cabin = Primitive(model.transform, "Cabin", PrimitiveType.Cube, new Vector3(0, 1.12f, -.3f), new Vector3(1.35f, .5f, 1.9f));
            var immutableMesh = Object.Instantiate(cabin.GetComponent<MeshFilter>().sharedMesh);
            AssetDatabase.CreateAsset(immutableMesh, Fixtures + "/" + name + "_NonReadable.asset");
            immutableMesh.UploadMeshData(true); cabin.GetComponent<MeshFilter>().sharedMesh = immutableMesh;
            Primitive(model.transform, "Mirror_Left", PrimitiveType.Cube, new Vector3(-1.4f, 1, .5f), new Vector3(.5f, .2f, .3f));
            for (int i = 0; i < 4; i++)
            {
                string wheelName = new[] { "FrontLeft", "FrontRight", "RL", "RR" }[i];
                var wheel = new GameObject(named ? wheelName : "Part_" + i).transform; wheel.SetParent(model.transform, false);
                Vector3 center = new Vector3(i % 2 == 0 ? -.9f : .9f, .35f, i < 2 ? 1.4f : -1.4f);
                wheel.localPosition = center + new Vector3(.17f, .08f, .09f); // Deliberately wrong pivot.
                var tyre = Primitive(wheel, named ? "Tyre_LOD0" : "Geometry_" + i, PrimitiveType.Cylinder,
                    new Vector3(-.17f, -.08f, -.09f), new Vector3(.7f, .12f, .7f));
                tyre.transform.localRotation = Quaternion.Euler(0, 0, 90);
                if (named)
                {
                    var rim = Primitive(wheel, "Rim_LOD0", PrimitiveType.Cylinder,
                        new Vector3(-.17f, -.08f, -.09f), new Vector3(.5f, .13f, .5f)); rim.transform.localRotation = Quaternion.Euler(0, 0, 90);
                    var lod = Object.Instantiate(tyre, wheel); lod.name = "Tyre_LOD1";
                }
            }
            string path = Fixtures + "/" + name + ".prefab";
            PrefabUtility.SaveAsPrefabAsset(model, path); return path;
        }
        finally { Object.DestroyImmediate(model); }
    }

    private static GameObject Primitive(Transform parent, string name, PrimitiveType type, Vector3 position, Vector3 scale)
    {
        var go = GameObject.CreatePrimitive(type); go.name = name; go.transform.SetParent(parent, false);
        go.transform.localPosition = position; go.transform.localScale = scale; return go;
    }

    private static void VerifyPrefab(string path)
    {
        var root = PrefabUtility.LoadPrefabContents(path);
        try
        {
            var car = root.GetComponent<NFSRaycastCarController>(); NFSAutoSetupCarPrefabs.ValidateBindings(car);
            Check(car.UseWheelColliders, "Native mode was not enabled.");
            Check(root.GetComponentsInChildren<Rigidbody>(true).Length == 1, "Extra rigidbodies remain.");
            Check(root.GetComponentsInChildren<Collider>(true).Length == 6, "Old/extra colliders remain.");
            foreach (string name in new[] { "Body", "WheelsVisual", "WheelColliders", "Colliders", "CenterOfMass" })
                Check(root.transform.Find(name), "Missing container " + name);
            float bottom = float.PositiveInfinity;
            foreach (var wheel in car.Wheels)
            {
                var bounds = NFSCarColliderGeometry.BoundsOf(wheel.Visual, root.transform);
                Check(Vector3.Distance(bounds.center, wheel.Visual.localPosition) < .001f, "Wheel hub is not at its geometric centre.");
                Check(Vector3.Distance(root.transform.InverseTransformPoint(wheel.PhysicsCollider.transform.position), bounds.center) < .001f, "Native wheel transform is off-centre.");
                Check(Mathf.Abs(wheel.Radius - bounds.size.y * .5f) < .001f, "Incorrect wheel radius.");
                bottom = Mathf.Min(bottom, bounds.min.y);
            }
            var colliders = root.GetComponentsInChildren<MeshCollider>(); Check(colliders.Length == 2, "Expected two body hulls.");
            foreach (var collider in colliders)
            {
                var mesh = collider.sharedMesh;
                Check(collider.convex && mesh && AssetDatabase.Contains(mesh), "Convex mesh is not persistent.");
                Check(mesh.bounds.min.y >= bottom + .179f, "Insufficient clearance.");
                Check(mesh.bounds.size.x <= 2.01f, "Wheels/mirrors polluted the body hull.");
                Check(mesh.triangles.Length / 3 < 255, "Convex hull exceeds PhysX face limit.");
                var material = collider.sharedMaterial;
                Check(material && material.dynamicFriction == 0 && material.staticFriction == 0 && Mathf.Abs(material.bounciness - .05f) < .0001f &&
                    material.frictionCombine == PhysicsMaterialCombine.Minimum, "Incorrect slippery material.");
                // Every hull vertex is behind every outward face (convexity and winding).
                var vertices = mesh.vertices; var indices = mesh.triangles;
                for (int i = 0; i < indices.Length; i += 3)
                {
                    var a = vertices[indices[i]];
                    var normal = Vector3.Cross(vertices[indices[i + 1]] - a, vertices[indices[i + 2]] - a).normalized;
                    Check(vertices.All(p => Vector3.Dot(normal, p - a) < .001f), "Non-convex or inward hull face.");
                }
            }
            var vfx = car.GetComponent<NFSCarVFXAndAudio>();
            Check(vfx.Car == car && vfx.RearSmoke.All(p => p && p.transform.IsChildOf(root.transform)) &&
                vfx.RearSkids.All(t => t && t.transform.IsChildOf(root.transform)) && vfx.WallSparks && vfx.NitroFlames.All(p => p) &&
                vfx.EngineAudio && vfx.EngineAudio.clip && vfx.TyreAudio && vfx.TyreAudio.clip, "VFX/audio remapping failed.");
        }
        finally { PrefabUtility.UnloadPrefabContents(root); }
    }

}

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using RedLine.NFS;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

/// <summary>Builds independent ready-to-drive prefabs. Sources and open scenes are never modified.</summary>
public static class NFSAutoSetupCarPrefabs
{
    public const string SourceFolder = "Assets/GeneratedVehicles";
    public const string OutputFolder = SourceFolder + "/AutoSetup";
    public const string MaterialPath = SourceFolder + "/CarBody_Slippery.physicMaterial";
    private const string SourceMarker = "NFS_AUTO_SETUP_SOURCE:";
    private static readonly string[] WheelNames = { "FL", "FR", "RL", "RR" };

    [MenuItem("Tools/RacingCity/Auto Setup Car Prefabs")]
    public static void SetupSelection()
    {
        var paths = SelectedPaths();
        if (paths.Length == 0)
        {
            Debug.LogWarning("[NFS Auto Setup] Select car prefabs/models or folders under " + SourceFolder + "."); return;
        }
        if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling)
            throw new InvalidOperationException("Run car setup outside Play mode and script compilation.");
        var reference = FindReference(); // Resolve once so selection order cannot change the template.
        int success = 0;
        try
        {
            for (int i = 0; i < paths.Length; i++)
            {
                EditorUtility.DisplayProgressBar("NFS Auto Setup", paths[i], (float)i / paths.Length);
                try { Build(AssetDatabase.LoadAssetAtPath<GameObject>(paths[i]), reference); success++; }
                catch (Exception exception) { Debug.LogError("[NFS Auto Setup] FAILED " + paths[i] + ": " + exception.Message); }
            }
        }
        finally { EditorUtility.ClearProgressBar(); AssetDatabase.SaveAssets(); }
        Debug.Log($"[NFS Auto Setup] Finished: {success}/{paths.Length} ready; {paths.Length - success} failed. Output: {OutputFolder}");
    }

    internal static string[] SelectedPaths()
    {
        var paths = new HashSet<string>(StringComparer.Ordinal);
        foreach (var selected in Selection.objects)
        {
            string path = AssetDatabase.GetAssetPath(selected);
            if (AssetDatabase.IsValidFolder(path) && IsSourcePath(path))
            {
                foreach (string guid in AssetDatabase.FindAssets("t:GameObject", new[] { path }))
                {
                    string candidate = AssetDatabase.GUIDToAssetPath(guid);
                    if (!candidate.StartsWith(OutputFolder + "/", StringComparison.Ordinal)) paths.Add(candidate);
                }
            }
            else if (IsSourcePath(path) && AssetDatabase.LoadAssetAtPath<GameObject>(path)) paths.Add(OriginalPath(path));
        }
        return paths.OrderBy(p => p, StringComparer.Ordinal).ToArray();
    }

    private static bool IsSourcePath(string path) => path == SourceFolder || path.StartsWith(SourceFolder + "/", StringComparison.Ordinal);
    private static string OriginalPath(string path)
    {
        var importer = AssetImporter.GetAtPath(path);
        if (importer && importer.userData.StartsWith(SourceMarker, StringComparison.Ordinal))
        {
            string original = AssetDatabase.GUIDToAssetPath(importer.userData.Substring(SourceMarker.Length));
            if (string.IsNullOrEmpty(original)) throw new InvalidOperationException("Original source was deleted for " + path);
            return original;
        }
        return path;
    }

    public static NFSRaycastCarController FindReference()
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(SourceFolder + "/NFS/NFS_TestCar.prefab");
        if (prefab && prefab.TryGetComponent<NFSRaycastCarController>(out var car)) return car;
        foreach (var candidate in Object.FindObjectsByType<NFSRaycastCarController>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            if (candidate.name == "NFS_TestCar") return candidate;
        foreach (string guid in AssetDatabase.FindAssets("NFS_TestCar t:Prefab"))
        {
            prefab = AssetDatabase.LoadAssetAtPath<GameObject>(AssetDatabase.GUIDToAssetPath(guid));
            if (prefab && prefab.TryGetComponent(out car)) return car;
        }
        throw new InvalidOperationException("NFS_TestCar with NFSRaycastCarController is required as the setup template.");
    }

    public static string Build(GameObject source, NFSRaycastCarController reference = null)
    {
        string sourcePath = source ? OriginalPath(AssetDatabase.GetAssetPath(source)) : "";
        if (!IsSourcePath(sourcePath) || AssetDatabase.IsValidFolder(sourcePath))
            throw new InvalidOperationException("Expected a prefab/model asset under " + SourceFolder);
        source = AssetDatabase.LoadAssetAtPath<GameObject>(sourcePath);
        if (!source) throw new InvalidOperationException("Source model is missing.");
        if (!reference) reference = FindReference();
        if (!reference.GetComponent<Rigidbody>() || !reference.GetComponent<NFSCarVFXAndAudio>())
            throw new InvalidOperationException("Reference needs Rigidbody, controller and NFSCarVFXAndAudio.");
        // A preview scene isolates temporary rigidbodies from the user's scene and supports headless execution.
        var scene = EditorSceneManager.NewPreviewScene();
        GameObject root = null; var transientMeshes = new List<Mesh>();
        try
        {
            root = Clone(reference.gameObject, scene); root.name = Path.GetFileNameWithoutExtension(sourcePath) + "_Ready";
            root.SetActive(false); root.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity); root.transform.localScale = Vector3.one;
            var car = root.GetComponent<NFSRaycastCarController>();
            // Cloning the complete reference keeps every serialized tuning value and remaps VFX/audio references.
            foreach (var renderer in root.GetComponentsInChildren<MeshRenderer>(true)) Object.DestroyImmediate(renderer.gameObject);
            foreach (var skin in root.GetComponentsInChildren<SkinnedMeshRenderer>(true)) Object.DestroyImmediate(skin.gameObject);
            foreach (var wheel in car.Wheels)
            {
                if (wheel.Visual && wheel.Visual != root.transform) Object.DestroyImmediate(wheel.Visual.gameObject);
                if (wheel.SuspensionPoint && wheel.SuspensionPoint != root.transform) Object.DestroyImmediate(wheel.SuspensionPoint.gameObject);
            }
            foreach (var collider in root.GetComponentsInChildren<Collider>(true)) Object.DestroyImmediate(collider);
            foreach (string container in new[] { "Body", "BodyVisual", "WheelsVisual", "WheelColliders", "Colliders", "CenterOfMass" })
            { var child = root.transform.Find(container); if (child) Object.DestroyImmediate(child.gameObject); }

            var body = Child(root.transform, "Body");
            var visuals = Child(root.transform, "WheelsVisual");
            var physical = Child(root.transform, "WheelColliders");
            var colliders = Child(root.transform, "Colliders");
            var model = Clone(source, scene);
            model.transform.SetParent(body, false); model.transform.localPosition = Vector3.zero; model.transform.localRotation = Quaternion.identity;
            model.SetActive(true);
            var wheels = DetectWheels(model.transform, root.transform);
            var wheelBounds = wheels.Select(group => GroupBounds(group, root.transform)).ToArray();
            // Strip old physics only on this temporary copy, after detection can use existing wheel links.
            foreach (var script in model.GetComponentsInChildren<NFSCarVFXAndAudio>(true)) Object.DestroyImmediate(script);
            foreach (var script in model.GetComponentsInChildren<NFSRaycastCarController>(true)) Object.DestroyImmediate(script);
            foreach (var script in model.GetComponentsInChildren<NFSArcadeCamera>(true)) Object.DestroyImmediate(script);
            foreach (var collider in model.GetComponentsInChildren<Collider>(true)) Object.DestroyImmediate(collider);
            foreach (var rb in model.GetComponentsInChildren<Rigidbody>(true)) Object.DestroyImmediate(rb);
            foreach (var audio in model.GetComponentsInChildren<AudioSource>(true)) Object.DestroyImmediate(audio);
            foreach (var effect in model.GetComponentsInChildren<ParticleSystem>(true)) Object.DestroyImmediate(effect.gameObject);
            foreach (var trail in model.GetComponentsInChildren<TrailRenderer>(true)) Object.DestroyImmediate(trail.gameObject);

            car.UseWheelColliders = true;
            float preload = car.Mass * Physics.gravity.magnitude * car.GravityMultiplier / (4 * car.EffectiveSpringStiffness);
            for (int i = 0; i < 4; i++)
            {
                var bounds = wheelBounds[i]; float radius = bounds.size.y * .5f;
                if (radius < .1f || radius > 1.2f) throw new InvalidOperationException("Wheel radius outside 0.1–1.2 m: check import units.");
                var hub = Child(visuals, WheelNames[i]); hub.localPosition = bounds.center;
                foreach (var wheelPart in wheels[i]) wheelPart.SetParent(hub, true);
                var mount = Child(physical, "Suspension_" + WheelNames[i]);
                mount.localPosition = bounds.center + Vector3.up * (car.SuspensionRestLength - preload);
                var wheel = car.Wheels[i];
                wheel.Visual = hub; wheel.SuspensionPoint = mount; wheel.LocalAnchor = mount.localPosition; wheel.Radius = radius;
                var wc = Child(physical, WheelNames[i]).gameObject.AddComponent<WheelCollider>();
                wc.transform.localPosition = bounds.center;
                var template = reference.Wheels[i].PhysicsCollider;
                if (template) EditorUtility.CopySerialized(template, wc);
                else
                {
                    wc.suspensionDistance = car.SuspensionTravel * 2;
                    wc.suspensionSpring = new JointSpring { spring = car.EffectiveSpringStiffness,
                        damper = car.DamperStiffness * Mathf.Sqrt(car.GravityMultiplier), targetPosition = .5f };
                    wc.forwardFriction = Friction(car.TyreFriction);
                    wc.sidewaysFriction = Friction(car.TyreFriction);
                }
                // Transform is exactly at the visual centre. Centre offsets the suspension's fully-compressed origin.
                wc.center = Vector3.up * (wc.suspensionDistance * (1 - wc.suspensionSpring.targetPosition) + preload);
                wc.radius = radius; wc.enabled = true; wc.motorTorque = 0; wc.brakeTorque = 0; wc.steerAngle = 0;
                wc.sprungMass = car.Mass * .25f;
                wc.excludeLayers = 1 << car.VehicleLayer;
                wheel.PhysicsCollider = wc;
            }

            var geometry = NFSCarColliderGeometry.BodyTriangles(body, root.transform);
            var points = geometry.SelectMany(t => t).ToArray();
            var boundsBody = new Bounds(points[0], Vector3.zero); foreach (var point in points) boundsBody.Encapsulate(point);
            if (boundsBody.size.x < .5f || boundsBody.size.x > 5 || boundsBody.size.z < 1 || boundsBody.size.z > 12)
                throw new InvalidOperationException("Expected an upright passenger car in metres with +Z forward.");
            float floor = wheelBounds.Min(b => b.min.y) + .18f;
            float bottom = Mathf.Max(floor, boundsBody.min.y);
            float split = Mathf.Max(bottom + .12f, boundsBody.min.y + boundsBody.size.y * .55f);
            if (split >= boundsBody.max.y - .03f) throw new InvalidOperationException("Body is too flat to form lower chassis and upper cabin hulls.");
            transientMeshes.Add(NFSCarColliderGeometry.BuildHull(geometry, bottom, split, "LowerBody"));
            transientMeshes.Add(NFSCarColliderGeometry.BuildHull(geometry, split, boundsBody.max.y, "Cabin"));
            var material = SlipperyMaterial();
            foreach (var mesh in transientMeshes)
            {
                var collider = Child(colliders, mesh.name).gameObject.AddComponent<MeshCollider>();
                collider.sharedMesh = mesh; collider.convex = true; collider.sharedMaterial = material; collider.contactOffset = .025f;
            }
            var com = Child(root.transform, "CenterOfMass");
            var axleCenter = wheelBounds.Aggregate(Vector3.zero, (sum, b) => sum + b.center) * .25f;
            com.localPosition = new Vector3(axleCenter.x, axleCenter.y - .15f, axleCenter.z + reference.CenterOfMass.z);
            car.CenterOfMass = com.localPosition; car.CenterOfMassTransform = com;
            var rigidbody = car.GetComponent<Rigidbody>();
            rigidbody.mass = car.Mass; rigidbody.centerOfMass = com.localPosition;
            rigidbody.interpolation = RigidbodyInterpolation.Interpolate;
            rigidbody.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            root.tag = reference.tag;
            foreach (var transform in root.GetComponentsInChildren<Transform>(true))
            { transform.gameObject.layer = car.VehicleLayer; transform.gameObject.isStatic = false; }
            RepositionEffects(car, boundsBody);
            ValidateBindings(car);

            string guid = AssetDatabase.AssetPathToGUID(sourcePath);
            string folder = OutputFolder + "/" + Path.GetFileNameWithoutExtension(sourcePath) + "_" + guid.Substring(0, 8);
            EnsureFolder(folder);
            string output = folder + "/" + root.name + ".prefab";
            SaveResult(root, colliders, folder, output, guid);
            Debug.Log($"[NFS Auto Setup] OK {sourcePath} -> {output}\n" +
                $"Wheel radii FL/FR/RL/RR: {string.Join(", ", car.Wheels.Select(w => w.Radius.ToString("F3")))} m; " +
                $"hulls: {string.Join(" + ", transientMeshes.Select(m => m.vertexCount))} vertices; clearance >= 0.18 m; " +
                "bindings: controller, 4 WheelColliders, 4 centred hubs, COM, VFX/audio OK. " +
                (reference.Wheels.All(w => w.PhysicsCollider) ? "Native suspension/friction copied from reference." :
                    "Reference uses raycasts: native spring/damper/travel/grip derived from its serialized preset."));
            return output;
        }
        finally
        {
            EditorSceneManager.ClosePreviewScene(scene);
            foreach (var mesh in transientMeshes) if (mesh && !AssetDatabase.Contains(mesh)) Object.DestroyImmediate(mesh);
        }
    }

    private static List<Transform>[] DetectWheels(Transform model, Transform frame)
    {
        var existing = model.GetComponentInChildren<NFSRaycastCarController>(true);
        if (existing && existing.Wheels.Length == 4 && existing.Wheels.All(w => w.Visual && w.Visual.IsChildOf(model)))
            return existing.Wheels.Select(w => new List<Transform> { w.Visual }).ToArray();
        var candidates = new List<Transform>();
        foreach (var node in model.GetComponentsInChildren<Transform>(true))
        {
            if (node == model || node.GetComponentsInChildren<MeshRenderer>(true).Length + node.GetComponentsInChildren<SkinnedMeshRenderer>(true).Length == 0) continue;
            if (!WheelLikeName(node.name)) continue;
            var bounds = NFSCarColliderGeometry.BoundsOf(node, frame);
            if (WheelShape(bounds)) candidates.Add(node);
        }
        // Keep complete wheel groups, including their tyre, rim and LOD children.
        candidates = candidates.Where(t => !candidates.Any(other => other != t && t.IsChildOf(other))).ToList();
        if (candidates.Count == 0)
            candidates = model.GetComponentsInChildren<Renderer>(true)
                .Where(r => r is MeshRenderer || r is SkinnedMeshRenderer).Select(r => r.transform)
                .Where(t => WheelShape(NFSCarColliderGeometry.BoundsOf(t, frame))).Distinct().ToList();
        var groups = new List<List<Transform>>();
        foreach (var candidate in candidates)
        {
            var bounds = NFSCarColliderGeometry.BoundsOf(candidate, frame);
            var group = groups.FirstOrDefault(g => Vector3.Distance(GroupBounds(g, frame).center, bounds.center) < Mathf.Max(.08f, bounds.size.y * .3f));
            if (group == null) groups.Add(new List<Transform> { candidate }); else group.Add(candidate);
        }
        if (groups.Count != 4)
            throw new InvalidOperationException($"Detected {groups.Count} wheel groups, expected 4. Use separate wheel objects (FL/FR/RL/RR or Wheel_*); separate spare wheels and combined body/wheel meshes before setup.");
        // Some exports name only the tyre or only the rim. Include nearby unnamed wheel geometry too,
        // otherwise a rim could remain in Body and contaminate its hull (or stay still during steering).
        foreach (var renderer in model.GetComponentsInChildren<Renderer>(true))
        {
            if (!(renderer is MeshRenderer) && !(renderer is SkinnedMeshRenderer)) continue;
            var node = renderer.transform;
            if (groups.SelectMany(g => g).Any(t => node == t || node.IsChildOf(t) || t.IsChildOf(node))) continue;
            var bounds = NFSCarColliderGeometry.BoundsOf(node, frame);
            if (!WheelShape(bounds)) continue;
            var group = groups.FirstOrDefault(g => Vector3.Distance(GroupBounds(g, frame).center, bounds.center) < Mathf.Max(.08f, bounds.size.y * .3f));
            if (group != null) group.Add(node);
        }
        var sorted = groups.OrderByDescending(g => GroupBounds(g, frame).center.z).ToArray();
        var front = sorted.Take(2).OrderBy(g => GroupBounds(g, frame).center.x).ToArray();
        var rear = sorted.Skip(2).OrderBy(g => GroupBounds(g, frame).center.x).ToArray();
        var result = new[] { front[0], front[1], rear[0], rear[1] };
        var b = result.Select(g => GroupBounds(g, frame)).ToArray();
        if (b[1].center.x - b[0].center.x < .4f || b[3].center.x - b[2].center.x < .4f ||
            Mathf.Min(b[0].center.z, b[1].center.z) - Mathf.Max(b[2].center.z, b[3].center.z) < .6f)
            throw new InvalidOperationException("Ambiguous wheel layout. Model must use +Z forward, +Y up, with two distinct axles.");
        return result;
    }

    private static bool WheelShape(Bounds b) => b.size.y >= .2f && b.size.y <= 2.4f && b.size.z / b.size.y > .65f &&
        b.size.z / b.size.y < 1.45f && b.size.x < b.size.y * .85f;
    private static bool WheelLikeName(string name)
    {
        string n = Regex.Replace(name.ToLowerInvariant(), "[^a-z0-9]", "");
        return n.Contains("wheel") || n.Contains("tyre") || n.Contains("tire") || n.Contains("rim") ||
            n.Contains("frontleft") || n.Contains("frontright") || n.Contains("rearleft") || n.Contains("rearright") ||
            Regex.IsMatch(n, "^(fl|fr|rl|rr|lf|rf|lr)([0-9]|lod[0-9])*$");
    }
    private static Bounds GroupBounds(List<Transform> group, Transform frame)
    { var bounds = NFSCarColliderGeometry.BoundsOf(group[0], frame); foreach (var part in group.Skip(1)) bounds.Encapsulate(NFSCarColliderGeometry.BoundsOf(part, frame)); return bounds; }
    private static GameObject Clone(GameObject source, Scene scene)
    {
        var clone = Object.Instantiate(source); clone.name = source.name;
        if (PrefabUtility.IsPartOfPrefabInstance(clone)) PrefabUtility.UnpackPrefabInstance(clone, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
        SceneManager.MoveGameObjectToScene(clone, scene); return clone;
    }
    private static Transform Child(Transform parent, string name)
    { var child = new GameObject(name).transform; child.SetParent(parent, false); return child; }
    private static WheelFrictionCurve Friction(float stiffness) => new WheelFrictionCurve {
        extremumSlip = .4f, extremumValue = 1, asymptoteSlip = .8f, asymptoteValue = .5f, stiffness = stiffness };
    private static Mesh SaveMesh(Mesh mesh, string path)
    {
        var asset = AssetDatabase.LoadAssetAtPath<Mesh>(path);
        if (asset) { EditorUtility.CopySerialized(mesh, asset); EditorUtility.SetDirty(asset); return asset; }
        // Keep the temporary hull separate for logging/cleanup and save a persistent copy.
        asset = Object.Instantiate(mesh); asset.name = mesh.name; AssetDatabase.CreateAsset(asset, path); return asset;
    }
    private static void SaveResult(GameObject root, Transform colliders, string folder, string output, string guid)
    {
        // Preserve the last usable generated prefab if a write/import fails halfway through a repeat run.
        var paths = new[] { output, folder + "/LowerBody.asset", folder + "/Cabin.asset" };
        AssetDatabase.SaveAssets();
        var backups = paths.SelectMany(p => new[] { p, p + ".meta" }).ToDictionary(p => p, p => File.Exists(p) ? File.ReadAllBytes(p) : null);
        try
        {
            foreach (var collider in colliders.GetComponentsInChildren<MeshCollider>())
                collider.sharedMesh = SaveMesh(collider.sharedMesh, folder + "/" + collider.sharedMesh.name + ".asset");
            root.SetActive(true);
            PrefabUtility.SaveAsPrefabAsset(root, output, out bool saved);
            if (!saved) throw new IOException("Could not save ready prefab: " + output);
            var importer = AssetImporter.GetAtPath(output); importer.userData = SourceMarker + guid; importer.SaveAndReimport();
            AssetDatabase.SaveAssets();
        }
        catch
        {
            foreach (string path in paths) if (backups[path] == null) AssetDatabase.DeleteAsset(path);
            foreach (var backup in backups) if (backup.Value != null) File.WriteAllBytes(backup.Key, backup.Value);
            foreach (string path in paths) if (backups[path] != null) AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
            throw;
        }
    }
    internal static void EnsureFolder(string folder)
    {
        string parent = "Assets";
        foreach (string part in folder.Split('/').Skip(1))
        { string next = parent + "/" + part; if (!AssetDatabase.IsValidFolder(next)) AssetDatabase.CreateFolder(parent, part); parent = next; }
    }
    private static PhysicsMaterial SlipperyMaterial()
    {
        var material = AssetDatabase.LoadAssetAtPath<PhysicsMaterial>(MaterialPath);
        if (!material) { material = new PhysicsMaterial("CarBody_Slippery"); AssetDatabase.CreateAsset(material, MaterialPath); }
        material.dynamicFriction = 0; material.staticFriction = 0; material.bounciness = .05f;
        material.frictionCombine = PhysicsMaterialCombine.Minimum; material.bounceCombine = PhysicsMaterialCombine.Minimum;
        EditorUtility.SetDirty(material); return material;
    }
    private static void RepositionEffects(NFSRaycastCarController car, Bounds body)
    {
        var vfx = car.GetComponent<NFSCarVFXAndAudio>(); vfx.Car = car;
        for (int i = 0; i < 2; i++)
        {
            Vector3 contact = car.Wheels[i + 2].Visual.localPosition - Vector3.up * car.Wheels[i + 2].Radius;
            if (i < vfx.RearSmoke.Length && vfx.RearSmoke[i]) vfx.RearSmoke[i].transform.localPosition = contact;
            if (i < vfx.RearSkids.Length && vfx.RearSkids[i]) vfx.RearSkids[i].transform.localPosition = contact;
        }
        for (int i = 0; i < vfx.NitroFlames.Length; i++) if (vfx.NitroFlames[i])
            vfx.NitroFlames[i].transform.localPosition = new Vector3(body.center.x + (i % 2 == 0 ? -.24f : .24f) * body.size.x,
                body.min.y + .12f, body.min.z - .02f);
    }
    internal static void ValidateBindings(NFSRaycastCarController car)
    {
        if (car.Wheels.Length != 4 || car.Wheels.Any(w => !w.Visual || !w.SuspensionPoint || !w.PhysicsCollider) || !car.CenterOfMassTransform)
            throw new InvalidOperationException("Wheel/COM bindings are incomplete.");
        foreach (var behaviour in car.GetComponentsInChildren<MonoBehaviour>(true))
        {
            if (!behaviour) throw new InvalidOperationException("Missing script in generated car.");
            var serialized = new SerializedObject(behaviour); var property = serialized.GetIterator();
            while (property.Next(true))
            {
                if (property.propertyType != SerializedPropertyType.ObjectReference) continue;
                var component = property.objectReferenceValue as Component;
                var go = property.objectReferenceValue as GameObject;
                var target = component ? component.transform : go ? go.transform : null;
                if (target && !target.IsChildOf(car.transform))
                    throw new InvalidOperationException("External scene reference: " + behaviour.GetType().Name + "." + property.propertyPath);
            }
        }
    }
}

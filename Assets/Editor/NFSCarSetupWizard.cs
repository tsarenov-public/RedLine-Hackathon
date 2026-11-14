using System;
using System.IO;
using System.Linq;
using EasyRoads3Dv3;
using RedLine.NFS;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

public sealed class NFSCarSetupWizard : EditorWindow
{
    private const string Folder = "Assets/GeneratedVehicles/NFS";
    [SerializeField] private GameObject model;
    [SerializeField] private Transform frontLeft, frontRight, rearLeft, rearRight;
    [SerializeField] private string status = "W/S — throttle/brake/reverse; A/D — steering; Space — handbrake; Left Shift — NOS.";

    [MenuItem("Tools/NFS Car Setup Wizard")]
    public static void Open() => GetWindow<NFSCarSetupWizard>("NFS Car Setup Wizard");

    private void OnGUI()
    {
        EditorGUILayout.LabelField("Raycast sportscar setup", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox("Models must be upright, use +Z as forward and real-world metres. Wheel objects may have offset pivots: the wizard creates centred spin pivots. Existing model assets remain unchanged.", MessageType.Info);
        using (new EditorGUI.DisabledScope(EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling))
        {
            if (GUILayout.Button("Spawn Ready Test Car on Road")) Run(() => SpawnReadyTestCar());
            EditorGUILayout.Space();
            model = (GameObject)EditorGUILayout.ObjectField("Custom model root", model, typeof(GameObject), true);
            frontLeft = (Transform)EditorGUILayout.ObjectField("Front Left wheel", frontLeft, typeof(Transform), true);
            frontRight = (Transform)EditorGUILayout.ObjectField("Front Right wheel", frontRight, typeof(Transform), true);
            rearLeft = (Transform)EditorGUILayout.ObjectField("Rear Left wheel", rearLeft, typeof(Transform), true);
            rearRight = (Transform)EditorGUILayout.ObjectField("Rear Right wheel", rearRight, typeof(Transform), true);
            if (GUILayout.Button("Bind Custom 3D Car Model")) Run(() => BindCustomModel(model, new[] { frontLeft, frontRight, rearLeft, rearRight }));
        }
        EditorGUILayout.Space(); EditorGUILayout.HelpBox(status, MessageType.None);
    }

    private void Run(Func<NFSRaycastCarController> action)
    {
        try
        {
            var car = action(); Selection.activeGameObject = car.gameObject;
            status = "Ready: " + car.name + ". Prefab saved in " + Folder + ". Save the scene, then press Play. Setup supports Undo.";
        }
        catch (Exception exception) { status = exception.Message; Debug.LogError("[NFS Car Setup] " + status); }
    }

    public static NFSRaycastCarController SpawnReadyTestCar()
    {
        RequireScene(); EnsureAssets();
        Undo.IncrementCurrentGroup(); int group = Undo.GetCurrentGroup(); Undo.SetCurrentGroupName("Spawn NFS Test Car");
        GameObject root = null;
        try
        {
            var spawn = FindSpawn(); root = new GameObject("NFS_TestCar");
            SceneManager.MoveGameObjectToScene(root, SceneManager.GetActiveScene());
            root.transform.SetPositionAndRotation(spawn.position, spawn.rotation);
            var collider = root.AddComponent<BoxCollider>(); collider.size = new Vector3(1.8f, .55f, 4.2f);
            var car = root.AddComponent<NFSRaycastCarController>();
            BuildTestBody(root.transform);
            BuildWheels(car); Complete(car);
            SavePrefab(car, "NFS_TestCar"); BindCamera(car);
            Undo.RegisterCreatedObjectUndo(root, "Spawn NFS Test Car"); Undo.CollapseUndoOperations(group);
            EditorSceneManager.MarkSceneDirty(root.scene); return car;
        }
        catch { if (root) Object.DestroyImmediate(root); Undo.RevertAllDownToGroup(group); throw; }
    }

    public static NFSRaycastCarController BindCustomModel(GameObject source, Transform[] selectedWheels)
    {
        RequireScene();
        if (!source || selectedWheels == null || selectedWheels.Length != 4 || selectedWheels.Any(w => !w) || selectedWheels.Distinct().Count() != 4)
            throw new InvalidOperationException("Assign a model root and four distinct wheel objects.");
        if (selectedWheels.Any(w => w == source.transform || !w.IsChildOf(source.transform)))
            throw new InvalidOperationException("All four wheels must be children of the selected model root.");
        if (selectedWheels.Any(a => selectedWheels.Any(b => a != b && b.IsChildOf(a))))
            throw new InvalidOperationException("Wheel objects cannot contain another selected wheel.");
        if (source.GetComponentsInChildren<Rigidbody>(true).Length > 0 || source.GetComponentsInChildren<WheelCollider>(true).Length > 0)
            throw new InvalidOperationException("Select a visual model without an existing Rigidbody or WheelCollider setup.");
        string[] paths = selectedWheels.Select(w => AnimationUtility.CalculateTransformPath(w, source.transform)).ToArray();
        if (paths.Distinct().Count() != 4 || paths.Where((p, i) => source.transform.Find(p) != selectedWheels[i]).Any())
            throw new InvalidOperationException("Wheel hierarchy paths are not unique. Give sibling wheel objects distinct names.");
        EnsureAssets();
        Undo.IncrementCurrentGroup(); int group = Undo.GetCurrentGroup(); Undo.SetCurrentGroupName("Bind NFS Car Model");
        GameObject root = null, visual = null;
        try
        {
            bool asset = EditorUtility.IsPersistent(source);
            visual = asset ? (GameObject)PrefabUtility.InstantiatePrefab(source, SceneManager.GetActiveScene()) : Object.Instantiate(source);
            visual.name = source.name + "_Visual"; visual.SetActive(true);
            var clonedWheels = paths.Select(p => visual.transform.Find(p)).ToArray();
            if (PrefabUtility.IsPartOfPrefabInstance(visual)) PrefabUtility.UnpackPrefabInstance(visual, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
            Vector3 center = Vector3.zero;
            foreach (var wheel in clonedWheels) center += RendererBounds(wheel, null).center * .25f;
            root = new GameObject("NFS_" + source.name); SceneManager.MoveGameObjectToScene(root, SceneManager.GetActiveScene());
            root.transform.SetPositionAndRotation(center + Vector3.up * .17f, Quaternion.Euler(0, visual.transform.eulerAngles.y, 0));
            visual.transform.SetParent(root.transform, true);
            foreach (var oldCollider in visual.GetComponentsInChildren<Collider>(true)) Object.DestroyImmediate(oldCollider);
            var collider = root.AddComponent<BoxCollider>();
            var car = root.AddComponent<NFSRaycastCarController>();
            for (int i = 0; i < 4; i++)
            {
                var bounds = RendererBounds(clonedWheels[i], root.transform);
                float radius = Mathf.Max(bounds.size.y, bounds.size.z) * .5f;
                if (radius < .1f || radius > 1.2f) throw new InvalidOperationException("Wheel dimensions are outside 0.2–2.4 metres. Check model import units.");
                var pivot = Child(root.transform, "WheelVisual_" + WheelName(i)); pivot.localPosition = bounds.center;
                clonedWheels[i].SetParent(pivot, true);
                var wheel = car.Wheels[i]; wheel.Radius = radius; wheel.Visual = pivot;
                float preload = car.Mass * 9.81f / (4 * car.SpringStiffness);
                var mount = Child(root.transform, "Suspension_" + WheelName(i));
                mount.localPosition = bounds.center + Vector3.up * (car.SuspensionRestLength - preload);
                wheel.SuspensionPoint = mount; wheel.LocalAnchor = mount.localPosition;
            }
            var body = RendererBounds(visual.transform, root.transform);
            if (body.size.z < 1 || body.size.z > 12 || body.size.x < .5f || body.size.x > 5)
                throw new InvalidOperationException("Expected a passenger-car model in metres, facing +Z.");
            collider.size = new Vector3(body.size.x * .94f, Mathf.Clamp(body.size.y * .7f, .4f, .85f), body.size.z * .95f);
            collider.center = new Vector3(body.center.x, body.min.y + collider.size.y * .5f, body.center.z);
            car.CenterOfMass = collider.center + Vector3.down * .25f;
            if (asset) { var spawn = FindSpawn(); root.transform.SetPositionAndRotation(spawn.position, spawn.rotation); }
            else { Undo.RecordObject(source, "Hide original visual model"); source.SetActive(false); }
            Complete(car); SavePrefab(car, "NFS_" + source.name); BindCamera(car);
            Undo.RegisterCreatedObjectUndo(root, "Bind NFS Car Model"); Undo.CollapseUndoOperations(group);
            EditorSceneManager.MarkSceneDirty(root.scene); return car;
        }
        catch
        {
            if (root) Object.DestroyImmediate(root); else if (visual) Object.DestroyImmediate(visual);
            Undo.RevertAllDownToGroup(group); throw;
        }
    }

    private static void RequireScene()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode || PrefabStageUtility.GetCurrentPrefabStage() != null)
            throw new InvalidOperationException("Use this wizard in an open scene, outside Play mode and Prefab Mode.");
    }

    private static Pose FindSpawn()
    {
        var road = Object.FindObjectsByType<ERModularRoad>()
            .FirstOrDefault(r => r.name == "road_0001" && r.gameObject.scene == SceneManager.GetActiveScene());
        Vector3 point = Vector3.zero, forward = Vector3.forward;
        if (road)
        {
            var wrapper = road.road ?? new ERRoad(road); var points = wrapper.GetSplinePointsCenter();
            if (points != null && points.Length >= 2)
            {
                int index = Mathf.Min(8, points.Length - 2);
                point = road.transform.TransformPoint(points[index]);
                forward = Vector3.ProjectOnPlane(road.transform.TransformVector(points[index + 1] - points[index]), Vector3.up).normalized;
                var collider = road.GetComponent<Collider>();
                if (collider && collider.Raycast(new Ray(point + Vector3.up * 10, Vector3.down), out var roadHit, 30))
                    return new Pose(roadHit.point + Vector3.up, Quaternion.LookRotation(forward, Vector3.up));
            }
        }
        if (Physics.Raycast(point + Vector3.up * 1000, Vector3.down, out var hit, 2000, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore)) point = hit.point;
        else
        {
            var ground = GameObject.CreatePrimitive(PrimitiveType.Plane); ground.name = "NFS_TestGround";
            ground.transform.position = point; ground.transform.localScale = Vector3.one * 20;
            ground.GetComponent<Renderer>().sharedMaterial = Lit("TestAsphalt", new Color(.13f, .14f, .15f));
            Undo.RegisterCreatedObjectUndo(ground, "Create NFS test ground");
        }
        return new Pose(point + Vector3.up, Quaternion.LookRotation(forward.sqrMagnitude > .01f ? forward : Vector3.forward, Vector3.up));
    }

    private static void BuildTestBody(Transform root)
    {
        var visual = Child(root, "BodyVisual");
        var paint = Lit("GTRBlue", new Color(.055f, .18f, .42f), .6f, .55f);
        var white = Lit("SilverStripe", new Color(.75f, .79f, .82f), .6f, .7f);
        var glass = Lit("DarkGlass", new Color(.025f, .055f, .075f), .8f, .3f);
        var trim = Lit("BlackTrim", new Color(.025f, .028f, .03f), .15f);
        Cube(visual, "Chassis", Vector3.zero, new Vector3(1.78f, .55f, 4.12f), paint);
        Cube(visual, "Cabin", new Vector3(0, .5f, -.3f), new Vector3(1.43f, .52f, 1.85f), glass);
        Cube(visual, "Roof", new Vector3(0, .79f, -.36f), new Vector3(1.47f, .06f, 1.54f), white);
        Cube(visual, "BonnetStripe", new Vector3(-.32f, .283f, 1.35f), new Vector3(.38f, .014f, 1.35f), white);
        Cube(visual, "BootStripe", new Vector3(-.32f, .283f, -1.72f), new Vector3(.38f, .014f, .65f), white);
        Cube(visual, "FrontSplitter", new Vector3(0, -.24f, 2.08f), new Vector3(1.85f, .1f, .26f), trim);
        Cube(visual, "RearWing", new Vector3(0, .69f, -1.88f), new Vector3(1.95f, .07f, .35f), trim);
        foreach (float side in new[] { -1f, 1f })
        {
            Cube(visual, "WingMount", new Vector3(side * .59f, .45f, -1.87f), new Vector3(.05f, .45f, .07f), trim);
            Cube(visual, "SideStripe", new Vector3(side * .897f, .12f, 0), new Vector3(.015f, .16f, 3.7f), white);
            Cube(visual, "Headlamp", new Vector3(side * .59f, .11f, 2.067f), new Vector3(.4f, .13f, .022f), white);
            Cube(visual, "Taillamp", new Vector3(side * .59f, .1f, -2.067f), new Vector3(.4f, .1f, .022f), Lit("TailRed", new Color(.6f, .015f, .01f), .5f));
        }
    }

    private static void BuildWheels(NFSRaycastCarController car)
    {
        var tyre = Lit("Tyre", new Color(.025f, .025f, .028f), .1f);
        var rim = Lit("WheelMetal", new Color(.4f, .43f, .48f), .6f, .8f);
        for (int i = 0; i < 4; i++)
        {
            var wheel = car.Wheels[i]; var mount = Child(car.transform, "Suspension_" + WheelName(i)); mount.localPosition = wheel.LocalAnchor;
            wheel.SuspensionPoint = mount;
            var pivot = Child(car.transform, "WheelVisual_" + WheelName(i));
            pivot.localPosition = mount.localPosition - Vector3.up * car.SuspensionRestLength; wheel.Visual = pivot;
            Cylinder(pivot, "Tyre", new Vector3(wheel.Radius * 2, .14f, wheel.Radius * 2), tyre);
            Cylinder(pivot, "Rim", new Vector3(wheel.Radius * 1.45f, .145f, wheel.Radius * 1.45f), rim);
        }
    }

    private static void Complete(NFSRaycastCarController car)
    {
        var physics = AssetDatabase.LoadAssetAtPath<PhysicsMaterial>(Folder + "/Chassis.physicMaterial");
        if (!physics)
        {
            physics = new PhysicsMaterial("NFS zero friction chassis") { dynamicFriction = 0, staticFriction = 0, bounciness = 0,
                frictionCombine = PhysicsMaterialCombine.Minimum, bounceCombine = PhysicsMaterialCombine.Minimum };
            AssetDatabase.CreateAsset(physics, Folder + "/Chassis.physicMaterial");
        }
        foreach (var collider in car.GetComponentsInChildren<Collider>()) { collider.sharedMaterial = physics; collider.contactOffset = .025f; }
        var body = car.GetComponent<Rigidbody>(); body.mass = car.Mass; body.centerOfMass = car.CenterOfMass;
        body.interpolation = RigidbodyInterpolation.Interpolate; body.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
        var vfx = car.gameObject.AddComponent<NFSCarVFXAndAudio>(); vfx.Car = car;
        for (int i = 0; i < 2; i++)
        {
            var wheel = car.Wheels[i + 2];
            var smoke = Particle(car.transform, "TyreSmoke_" + i, ParticleMaterial("Smoke", false), new Color(.65f, .67f, .7f, .3f), 1.2f, .65f, .8f, 22, false);
            smoke.transform.localPosition = wheel.LocalAnchor - Vector3.up * (car.SuspensionRestLength + wheel.Radius);
            var main = smoke.main; main.gravityModifier = -.04f;
            var size = smoke.sizeOverLifetime; size.enabled = true;
            size.size = new ParticleSystem.MinMaxCurve(1, new AnimationCurve(new Keyframe(0, .3f), new Keyframe(1, 2)));
            vfx.RearSmoke[i] = smoke;
            var trail = Child(car.transform, "TyreMark_" + i).gameObject.AddComponent<TrailRenderer>();
            trail.sharedMaterial = SkidMaterial(); trail.time = 14; trail.minVertexDistance = .12f; trail.startWidth = .23f; trail.endWidth = .21f;
            trail.alignment = LineAlignment.TransformZ; trail.emitting = false; trail.shadowCastingMode = ShadowCastingMode.Off; trail.receiveShadows = false;
            trail.colorGradient = Gradient(Color.white, new Color(1, 1, 1, 0)); vfx.RearSkids[i] = trail;
        }
        vfx.WallSparks = Particle(car.transform, "WallSparks", ParticleMaterial("Sparks", true), new Color(4, 1.1f, .08f, 1), .28f, .045f, 8, 80, false);
        var sparkMain = vfx.WallSparks.main; sparkMain.gravityModifier = 1.1f;
        var sparkRenderer = vfx.WallSparks.GetComponent<ParticleSystemRenderer>(); sparkRenderer.renderMode = ParticleSystemRenderMode.Stretch;
        sparkRenderer.lengthScale = .5f; sparkRenderer.velocityScale = .08f;
        vfx.NitroFlames = new ParticleSystem[2];
        for (int i = 0; i < 2; i++)
        {
            var flame = Particle(car.transform, "NOSFlame_" + i, ParticleMaterial("Nitro", true), new Color(.15f, .7f, 3, 1), .15f, .19f, 5.5f, 65, true);
            flame.transform.localPosition = new Vector3(i == 0 ? -.42f : .42f, -.12f, -2.12f);
            flame.transform.localRotation = Quaternion.Euler(0, 180, 0); vfx.NitroFlames[i] = flame;
        }
        vfx.EngineAudio = Audio(car.transform, "EngineAudio", SynthClip("EngineLoop", false), .5f);
        vfx.TyreAudio = Audio(car.transform, "TyreAudio", SynthClip("TyreLoop", true), 0);
        SetLayer(car.transform, car.VehicleLayer);
    }

    private static void BindCamera(NFSRaycastCarController car)
    {
        var camera = Object.FindObjectsByType<Camera>()
            .FirstOrDefault(c => c.gameObject.scene == car.gameObject.scene && (c.CompareTag("MainCamera") || c.name == "Main Camera"));
        if (!camera)
        {
            var go = new GameObject("Main Camera", typeof(Camera), typeof(AudioListener)); go.tag = "MainCamera";
            camera = go.GetComponent<Camera>(); camera.nearClipPlane = .1f; Undo.RegisterCreatedObjectUndo(go, "Create car camera");
        }
        var chase = camera.GetComponent<NFSArcadeCamera>();
        if (!chase) chase = Undo.AddComponent<NFSArcadeCamera>(camera.gameObject);
        Undo.RecordObject(chase, "Bind car camera"); chase.Target = car; EditorUtility.SetDirty(chase);
    }

    private static void SavePrefab(NFSRaycastCarController car, string name)
    {
        string path = AssetDatabase.GenerateUniqueAssetPath(Folder + "/" + name + ".prefab");
        PrefabUtility.SaveAsPrefabAssetAndConnect(car.gameObject, path, InteractionMode.AutomatedAction);
        AssetDatabase.SaveAssets();
    }

    private static ParticleSystem Particle(Transform parent, string name, Material material, Color color, float lifetime, float size, float speed, float rate, bool local)
    {
        var system = Child(parent, name).gameObject.AddComponent<ParticleSystem>(); system.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        var main = system.main; main.playOnAwake = false; main.loop = true; main.duration = 1; main.maxParticles = 200;
        main.startLifetime = lifetime; main.startSize = size; main.startSpeed = speed; main.startColor = color;
        main.simulationSpace = local ? ParticleSystemSimulationSpace.Local : ParticleSystemSimulationSpace.World;
        var emission = system.emission; emission.rateOverTime = rate; emission.enabled = false;
        var shape = system.shape; shape.shapeType = ParticleSystemShapeType.Cone; shape.angle = 17; shape.radius = .07f;
        var fade = system.colorOverLifetime; fade.enabled = true; fade.color = Gradient(Color.white, new Color(1, 1, 1, 0));
        system.GetComponent<ParticleSystemRenderer>().sharedMaterial = material; return system;
    }

    private static Gradient Gradient(Color start, Color end)
    {
        var gradient = new Gradient(); gradient.SetKeys(new[] { new GradientColorKey(start, 0), new GradientColorKey(end, 1) },
            new[] { new GradientAlphaKey(start.a, 0), new GradientAlphaKey(end.a, 1) }); return gradient;
    }

    private static AudioSource Audio(Transform parent, string name, AudioClip clip, float volume)
    {
        var source = Child(parent, name).gameObject.AddComponent<AudioSource>(); source.clip = clip; source.playOnAwake = false;
        source.loop = true; source.spatialBlend = 1; source.minDistance = 4; source.maxDistance = 100; source.volume = volume; source.dopplerLevel = .15f;
        return source;
    }

    private static AudioClip SynthClip(string name, bool tyre)
    {
        string path = Folder + "/" + name + ".wav";
        var clip = AssetDatabase.LoadAssetAtPath<AudioClip>(path); if (clip) return clip;
        const int sampleRate = 22050; var random = new System.Random(37);
        using (var writer = new BinaryWriter(File.Create(path)))
        {
            writer.Write(System.Text.Encoding.ASCII.GetBytes("RIFF")); writer.Write(36 + sampleRate * 2);
            writer.Write(System.Text.Encoding.ASCII.GetBytes("WAVEfmt ")); writer.Write(16); writer.Write((short)1); writer.Write((short)1);
            writer.Write(sampleRate); writer.Write(sampleRate * 2); writer.Write((short)2); writer.Write((short)16);
            writer.Write(System.Text.Encoding.ASCII.GetBytes("data")); writer.Write(sampleRate * 2);
            for (int i = 0; i < sampleRate; i++)
            {
                double t = (double)i / sampleRate, noise = random.NextDouble() * 2 - 1;
                double value = tyre ? Math.Sin(t * 2 * Math.PI * 1400) * .18 + noise * .2 :
                    Math.Sin(t * 2 * Math.PI * 80) * .28 + Math.Sin(t * 2 * Math.PI * 160) * .16 + Math.Sin(t * 2 * Math.PI * 320) * .08 + noise * .015;
                // Fade only the noise at the loop seam; all oscillators have an integer number of cycles.
                double edge = Math.Min(1, Math.Min(i, sampleRate - 1 - i) / 120.0);
                writer.Write((short)(Math.Tanh(value) * edge * short.MaxValue));
            }
        }
        AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport); return AssetDatabase.LoadAssetAtPath<AudioClip>(path);
    }

    private static Material Lit(string name, Color color, float smoothness = .2f, float metallic = 0)
    {
        string path = Folder + "/" + name + ".mat"; var material = AssetDatabase.LoadAssetAtPath<Material>(path); if (material) return material;
        material = NewMaterial("Universal Render Pipeline/Lit", name); material.SetColor("_BaseColor", color);
        material.SetFloat("_Smoothness", smoothness); material.SetFloat("_Metallic", metallic); AssetDatabase.CreateAsset(material, path); return material;
    }

    private static Material ParticleMaterial(string name, bool additive)
    {
        string path = Folder + "/" + name + ".mat"; var material = AssetDatabase.LoadAssetAtPath<Material>(path); if (material) return material;
        material = NewMaterial("Universal Render Pipeline/Particles/Unlit", name);
        Transparent(material, additive); material.SetTexture("_BaseMap", SoftTexture());
        AssetDatabase.CreateAsset(material, path); return material;
    }

    private static Material SkidMaterial()
    {
        string path = Folder + "/TyreMark.mat"; var material = AssetDatabase.LoadAssetAtPath<Material>(path); if (material) return material;
        material = NewMaterial("Universal Render Pipeline/Unlit", "TyreMark"); Transparent(material, false);
        material.SetColor("_BaseColor", new Color(.015f, .015f, .018f, .8f)); AssetDatabase.CreateAsset(material, path); return material;
    }

    private static void Transparent(Material material, bool additive)
    {
        material.SetFloat("_Surface", 1); material.SetFloat("_Blend", additive ? 2 : 0);
        material.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha); material.SetFloat("_DstBlend", (float)(additive ? BlendMode.One : BlendMode.OneMinusSrcAlpha));
        material.SetFloat("_ZWrite", 0); material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT"); material.SetOverrideTag("RenderType", "Transparent"); material.renderQueue = 3000;
    }

    private static Texture2D SoftTexture()
    {
        const string path = Folder + "/SoftParticle.asset";
        var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(path); if (texture) return texture;
        texture = new Texture2D(64, 64, TextureFormat.RGBA32, true) { name = "SoftParticle", wrapMode = TextureWrapMode.Clamp };
        for (int y = 0; y < 64; y++) for (int x = 0; x < 64; x++)
        {
            float r = new Vector2((x - 31.5f) / 31.5f, (y - 31.5f) / 31.5f).magnitude;
            texture.SetPixel(x, y, new Color(1, 1, 1, Mathf.Pow(Mathf.Clamp01(1 - r), 1.5f)));
        }
        texture.Apply(); AssetDatabase.CreateAsset(texture, path); return texture;
    }

    private static Material NewMaterial(string shaderName, string name)
    { var shader = Shader.Find(shaderName); if (!shader) throw new InvalidOperationException("Missing URP shader: " + shaderName); return new Material(shader) { name = "NFS_" + name }; }
    private static void EnsureAssets()
    {
        string parent = "Assets";
        foreach (string part in Folder.Split('/').Skip(1)) { string next = parent + "/" + part; if (!AssetDatabase.IsValidFolder(next)) AssetDatabase.CreateFolder(parent, part); parent = next; }
    }
    private static string WheelName(int i) => new[] { "FL", "FR", "RL", "RR" }[i];
    private static void SetLayer(Transform root, int layer)
    { foreach (var child in root.GetComponentsInChildren<Transform>(true)) child.gameObject.layer = layer; }
    private static Transform Child(Transform parent, string name) { var go = new GameObject(name); go.transform.SetParent(parent, false); return go.transform; }
    private static void Cube(Transform parent, string name, Vector3 position, Vector3 scale, Material material)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Cube); go.name = name; go.transform.SetParent(parent, false);
        go.transform.localPosition = position; go.transform.localScale = scale; go.GetComponent<Renderer>().sharedMaterial = material; Object.DestroyImmediate(go.GetComponent<Collider>());
    }
    private static void Cylinder(Transform parent, string name, Vector3 scale, Material material)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Cylinder); go.name = name; go.transform.SetParent(parent, false);
        go.transform.localRotation = Quaternion.Euler(0, 0, 90); go.transform.localScale = scale;
        go.GetComponent<Renderer>().sharedMaterial = material; Object.DestroyImmediate(go.GetComponent<Collider>());
    }

    private static Bounds RendererBounds(Transform root, Transform frame)
    {
        bool any = false; var bounds = new Bounds();
        foreach (var renderer in root.GetComponentsInChildren<Renderer>(true))
        {
            if (renderer is ParticleSystemRenderer || renderer is TrailRenderer) continue;
            var local = renderer.localBounds;
            for (int i = 0; i < 8; i++)
            {
                Vector3 p = renderer.transform.TransformPoint(local.center + Vector3.Scale(local.extents, new Vector3((i & 1) == 0 ? -1 : 1, (i & 2) == 0 ? -1 : 1, (i & 4) == 0 ? -1 : 1)));
                if (frame) p = frame.InverseTransformPoint(p);
                if (!any) { bounds = new Bounds(p, Vector3.zero); any = true; } else bounds.Encapsulate(p);
            }
        }
        if (!any) throw new InvalidOperationException("The selected model/wheel has no Renderer."); return bounds;
    }
}

[CustomEditor(typeof(NFSRaycastCarController))]
public sealed class NFSRaycastCarControllerInspector : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();
        var car = (NFSRaycastCarController)target;
        EditorGUILayout.Space(); EditorGUILayout.LabelField("Runtime telemetry", EditorStyles.boldLabel);
        using (new EditorGUI.DisabledScope(true))
        {
            EditorGUILayout.FloatField("Speed (km/h)", car.SpeedKmh); EditorGUILayout.IntField("Current gear (-1 = R)", car.CurrentGear);
            EditorGUILayout.FloatField("RPM", car.CurrentRPM); EditorGUILayout.FloatField("Slip angle", car.SlipAngle);
            EditorGUILayout.FloatField("NOS", car.NitroAmount); EditorGUILayout.IntField("Grounded wheels", car.GroundedWheelCount);
            EditorGUILayout.Toggle("Drifting", car.IsDrifting); EditorGUILayout.Toggle("Shifting", car.IsShifting);
        }
        if (Application.isPlaying) Repaint();
    }
}

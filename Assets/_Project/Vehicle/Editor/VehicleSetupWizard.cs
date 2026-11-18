#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Arcade.Vehicle.EditorTools
{
    public static class VehicleSetupWizard
    {
        [MenuItem("Tools/Arcade Vehicle/Setup Car From Selection")]
        static void Setup()
        {
            var model = Selection.activeGameObject;
            if (model == null) { Debug.LogError("Выдели корень модели машины в Hierarchy."); return; }

            if (PrefabUtility.IsPartOfPrefabInstance(model))
            {
                if (!EditorUtility.DisplayDialog("Распаковать префаб?",
                    "Модель — prefab instance. Нужно Unpack Completely.", "Unpack", "Отмена")) return;
                model = PrefabUtility.GetOutermostPrefabInstanceRoot(model);
                PrefabUtility.UnpackPrefabInstance(model, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
            }

            var tuning = FindOrCreateTuning();
            float rest = tuning.suspension.restLength;

            // ---- ищем колёса ----
            var wheels = new List<Transform>();
            foreach (var t in model.GetComponentsInChildren<Transform>())
            {
                string n = t.name.ToLowerInvariant();
                if (n.Contains("wheel") && !n.Contains("steering")) wheels.Add(t);
            }
            if (wheels.Count != 4) { Debug.LogError($"Найдено {wheels.Count} колёс, нужно ровно 4."); return; }

            var centers = new Vector3[4];
            var radii = new float[4];
            var isFront = new bool[4];
            float groundY = float.MaxValue;
            int frontCount = 0;

            for (int i = 0; i < 4; i++)
            {
                var b = WorldBounds(wheels[i].GetComponentsInChildren<Renderer>());
                centers[i] = b.center;
                radii[i] = b.size.y * 0.5f;
                groundY = Mathf.Min(groundY, b.min.y);
                isFront[i] = wheels[i].name.ToLowerInvariant().Contains("front");
                if (isFront[i]) frontCount++;
            }
            if (frontCount != 2)
            {
                Debug.LogError("Нужно ровно 2 колеса со словом 'Front' в имени. Проверь имена мешей колёс.");
                return;
            }

            // ---- ГЛАВНОЕ: направление берём из расположения осей ----
            Vector3 avg = Vector3.zero, frontCenter = Vector3.zero, rearCenter = Vector3.zero;
            for (int i = 0; i < 4; i++)
            {
                avg += centers[i] * 0.25f;
                if (isFront[i]) frontCenter += centers[i] * 0.5f;
                else rearCenter += centers[i] * 0.5f;
            }
            Vector3 fwd = frontCenter - rearCenter;
            fwd.y = 0f;
            if (fwd.sqrMagnitude < 0.01f) { Debug.LogError("Не удалось определить направление машины."); return; }
            Quaternion rootRot = Quaternion.LookRotation(fwd.normalized, Vector3.up);

            // ---- корень ----
            var rootGO = new GameObject(model.name + "_Car");
            Undo.RegisterCreatedObjectUndo(rootGO, "Setup Car");
            rootGO.transform.SetPositionAndRotation(new Vector3(avg.x, groundY, avg.z), rootRot);
            Undo.SetTransformParent(model.transform, rootGO.transform, true, "Setup Car");

            foreach (var col in model.GetComponentsInChildren<Collider>()) Undo.DestroyObjectImmediate(col);

            // ---- mounts + visual ----
            var mounts = new Transform[4];
            var visuals = new Transform[4];
            var wheelSet = new HashSet<Transform>(wheels);

            for (int i = 0; i < 4; i++)
            {
                bool left = Vector3.Dot(centers[i] - avg, rootGO.transform.right) < 0f;
                var mount = new GameObject((isFront[i] ? "F" : "R") + (left ? "L" : "R") + "_Mount");
                Undo.RegisterCreatedObjectUndo(mount, "Setup Car");
                mount.transform.SetParent(rootGO.transform, false);
                mount.transform.SetPositionAndRotation(centers[i] + Vector3.up * rest, rootRot);

                var visual = new GameObject("Visual");
                Undo.RegisterCreatedObjectUndo(visual, "Setup Car");
                visual.transform.SetParent(mount.transform, false);
                visual.transform.SetPositionAndRotation(centers[i], rootRot);

                Undo.SetTransformParent(wheels[i], visual.transform, true, "Setup Car");
                mounts[i] = mount.transform;
                visuals[i] = visual.transform;
            }

            // ----_bounds кузова (без колёс) ----
            var bodyRenderers = new List<Renderer>();
            foreach (var r in model.GetComponentsInChildren<Renderer>())
            {
                bool isWheel = false;
                foreach (var w in wheelSet) if (r.transform.IsChildOf(w)) { isWheel = true; break; }
                if (!isWheel) bodyRenderers.Add(r);
            }
            var bb = LocalBounds(rootGO.transform, bodyRenderers);

            float lowerH = bb.size.y * 0.55f;
            float cabH = bb.size.y - lowerH;

            var lower = Undo.AddComponent<BoxCollider>(rootGO);
            lower.center = new Vector3(bb.center.x, bb.min.y + lowerH * 0.5f + 0.04f, bb.center.z);
            lower.size = new Vector3(bb.size.x * 0.94f, lowerH, bb.size.z * 0.98f);

            var cabin = Undo.AddComponent<BoxCollider>(rootGO);
            cabin.center = new Vector3(bb.center.x, bb.min.y + lowerH + cabH * 0.5f, bb.center.z - bb.size.z * 0.05f);
            cabin.size = new Vector3(bb.size.x * 0.82f, cabH, bb.size.z * 0.48f);

            // ---- компоненты ----
            var av = Undo.AddComponent<ArcadeVehicle>(rootGO);
            Undo.AddComponent<VehicleInputSource>(rootGO);
            Undo.AddComponent<VehicleEffects>(rootGO);

            var so = new SerializedObject(av);
            so.FindProperty("tuning").objectReferenceValue = tuning;
            var arr = so.FindProperty("wheelRigs");
            arr.arraySize = 4;
            for (int i = 0; i < 4; i++)
            {
                var e = arr.GetArrayElementAtIndex(i);
                e.FindPropertyRelative("mount").objectReferenceValue = mounts[i];
                e.FindPropertyRelative("visual").objectReferenceValue = visuals[i];
                e.FindPropertyRelative("radius").floatValue = radii[i];
                e.FindPropertyRelative("steered").boolValue = isFront[i];
                e.FindPropertyRelative("driven").boolValue = !isFront[i];
                e.FindPropertyRelative("handbraked").boolValue = !isFront[i];
                e.FindPropertyRelative("gripScale").floatValue = 1f;
                e.FindPropertyRelative("modelOffset").quaternionValue = Quaternion.identity;
            }
            int groundLayer = LayerMask.NameToLayer("Ground");
            if (groundLayer >= 0) so.FindProperty("groundMask").intValue = 1 << groundLayer;
            so.ApplyModifiedProperties();

            int vehLayer = LayerMask.NameToLayer("Vehicle");
            if (vehLayer >= 0) SetLayerRecursive(rootGO, vehLayer);

            Selection.activeGameObject = rootGO;
            EditorSceneManager.MarkSceneDirty(rootGO.scene);

            Debug.Log($"[VehicleSetup] Готово: {rootGO.name}\n" +
                      $"Кузов Ш×В×Д: {bb.size.x:F2} × {bb.size.y:F2} × {bb.size.z:F2}\n" +
                      $"Радиусы колёс: {radii[0]:F3} / {radii[1]:F3} / {radii[2]:F3} / {radii[3]:F3}\n" +
                      $"База: {Vector3.Distance(frontCenter, rearCenter):F2} м");

            if (bb.size.z < bb.size.x)
                Debug.LogWarning("[VehicleSetup] Длина меньше ширины — ориентация всё ещё неверная, напиши мне.");
            else
                Debug.Log("[VehicleSetup] Ориентация OK ✅ (длина по Z). Можно ехать.");
        }

        static void SetLayerRecursive(GameObject go, int layer)
        {
            go.layer = layer;
            foreach (Transform t in go.transform) SetLayerRecursive(t.gameObject, layer);
        }

        static VehicleTuning FindOrCreateTuning()
        {
            var guids = AssetDatabase.FindAssets("t:VehicleTuning");
            if (guids.Length > 0)
                return AssetDatabase.LoadAssetAtPath<VehicleTuning>(AssetDatabase.GUIDToAssetPath(guids[0]));
            var t = ScriptableObject.CreateInstance<VehicleTuning>();
            System.IO.Directory.CreateDirectory("Assets/_Project/Vehicle");
            AssetDatabase.CreateAsset(t, "Assets/_Project/Vehicle/Tuning_Car.asset");
            AssetDatabase.SaveAssets();
            return t;
        }

        static Bounds WorldBounds(IEnumerable<Renderer> rs)
        {
            var b = new Bounds(); bool first = true;
            foreach (var r in rs) { if (first) { b = r.bounds; first = false; } else b.Encapsulate(r.bounds); }
            return b;
        }

        static Bounds LocalBounds(Transform root, IEnumerable<Renderer> rs)
        {
            var b = new Bounds(); bool first = true;
            foreach (var r in rs)
            {
                var wb = r.bounds; var c = wb.center; var e = wb.extents;
                for (int i = 0; i < 8; i++)
                {
                    var p = c + new Vector3((i & 1) == 0 ? -e.x : e.x,
                                            (i & 2) == 0 ? -e.y : e.y,
                                            (i & 4) == 0 ? -e.z : e.z);
                    var lp = root.InverseTransformPoint(p);
                    if (first) { b = new Bounds(lp, Vector3.zero); first = false; } else b.Encapsulate(lp);
                }
            }
            return b;
        }
    }
}
#endif
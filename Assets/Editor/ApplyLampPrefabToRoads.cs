using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

public static class ApplyLampPrefabToRoads
{
    [MenuItem("Tools/Применить префаб Lamp ко всем фонарям на дорогах")]
    public static void ApplyToAllRoadLamps()
    {
        GameObject lampPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Lamp.prefab");
        if (lampPrefab == null)
        {
            Debug.LogError("Не найден префаб Assets/Lamp.prefab!");
            return;
        }

        // Находим все контейнеры "Lampposts 2", созданные EasyRoads3D на сцене
        Transform[] allTransforms = Object.FindObjectsByType<Transform>(FindObjectsSortMode.None);
        List<Transform> targets = new List<Transform>();

        foreach (Transform t in allTransforms)
        {
            if (t != null && t.parent != null && t.parent.name == "Lampposts 2")
            {
                targets.Add(t);
            }
        }

        if (targets.Count == 0)
        {
            Debug.LogWarning("Не найдено ни одного фонаря внутри 'Lampposts 2'. Убедись, что Combine Objects выключен и нажата кнопка Update Active Scene Instances.");
            return;
        }

        Undo.IncrementCurrentGroup();
        int group = Undo.GetCurrentGroup();
        int replaced = 0;

        foreach (Transform oldLamp in targets)
        {
            if (oldLamp == null) continue;
            if (PrefabUtility.GetCorrespondingObjectFromSource(oldLamp.gameObject) == lampPrefab)
                continue;

            Transform parent = oldLamp.parent;
            Vector3 pos = oldLamp.position;
            Quaternion rot = oldLamp.rotation;
            Vector3 scale = oldLamp.localScale;
            int siblingIndex = oldLamp.GetSiblingIndex();

            GameObject newLamp = (GameObject)PrefabUtility.InstantiatePrefab(lampPrefab, parent);
            Undo.RegisterCreatedObjectUndo(newLamp, "Apply Lamp Prefab");

            newLamp.transform.SetPositionAndRotation(pos, rot);
            newLamp.transform.localScale = scale;
            newLamp.transform.SetSiblingIndex(siblingIndex);

            Undo.DestroyObjectImmediate(oldLamp.gameObject);
            replaced++;
        }

        Undo.CollapseUndoOperations(group);
        Debug.Log($"Готово! Префаб Lamp применён ко всем фонарям на трассе: {replaced} шт.");
    }
}
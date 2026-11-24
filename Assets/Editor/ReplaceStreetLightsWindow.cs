using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public sealed class ReplaceStreetLightsWindow : EditorWindow
{
    private GameObject lampPrefab;
    private string targetNames = "StreetLightA, StreetLight 2, StreetLight2";
    private bool partialMatch = false;
    private bool includeInactive = true;

    [MenuItem("Tools/Массовая замена фонарей на Lamp")]
    public static void ShowWindow()
    {
        var window = GetWindow<ReplaceStreetLightsWindow>("Замена фонарей");
        window.minSize = new Vector2(420f, 250f);
    }

    private void OnEnable()
    {
        if (lampPrefab == null)
        {
            lampPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Lamp.prefab");
            if (lampPrefab == null)
            {
                string[] guids = AssetDatabase.FindAssets("Lamp t:Prefab");
                if (guids.Length > 0)
                {
                    string path = AssetDatabase.GUIDToAssetPath(guids[0]);
                    lampPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                }
            }
        }
    }

    private void OnGUI()
    {
        GUILayout.Space(8);
        EditorGUILayout.LabelField("Замена уличных фонарей по всей карте", EditorStyles.boldLabel);
        GUILayout.Space(6);

        lampPrefab = (GameObject)EditorGUILayout.ObjectField(
            "Новый префаб (Lamp)", lampPrefab, typeof(GameObject), false);

        targetNames = EditorGUILayout.TextField(
            new GUIContent("Имена старых фонарей", "Укажи названия через запятую"), targetNames);

        partialMatch = EditorGUILayout.Toggle(
            new GUIContent("Частичное совпадение", "Искать объекты, в имени которых содержится указанный текст"), partialMatch);

        includeInactive = EditorGUILayout.Toggle(
            new GUIContent("Учитывать выключенные", "Заменять также скрытые объекты в иерархии"), includeInactive);

        GUILayout.Space(14);

        GUI.enabled = lampPrefab != null && !string.IsNullOrWhiteSpace(targetNames);

        if (GUILayout.Button("1. Заменить все фонари на текущей сцене", GUILayout.Height(34)))
        {
            ReplaceInActiveScene();
        }

        GUILayout.Space(6);

        if (GUILayout.Button("2. Заменить внутри префабов дорог в папке Assets", GUILayout.Height(30)))
        {
            if (EditorUtility.DisplayDialog(
                "Замена в префабах Assets",
                "Скрипт проверит все префабы дорог в проекте и заменит внутри них старые фонари на Lamp. Продолжить?",
                "Да, заменить", "Отмена"))
            {
                ReplaceInProjectPrefabs();
            }
        }

        GUI.enabled = true;
    }

    private void ReplaceInActiveScene()
    {
        string[] patterns = ParsePatterns(targetNames);
        var inactiveMode = includeInactive ? FindObjectsInactive.Include : FindObjectsInactive.Exclude;
        Transform[] allTransforms = FindObjectsByType<Transform>(inactiveMode, FindObjectsSortMode.None);

        List<GameObject> candidates = new List<GameObject>();
        foreach (Transform t in allTransforms)
        {
            if (t == null) continue;
            GameObject go = t.gameObject;

            if (PrefabUtility.GetCorrespondingObjectFromSource(go) == lampPrefab)
                continue;

            if (MatchesName(go.name, patterns, partialMatch))
                candidates.Add(go);
        }

        if (candidates.Count == 0)
        {
            Debug.LogWarning($"[Замена фонарей] Объекты с именами [{targetNames}] на сцене не найдены.");
            return;
        }

        Undo.IncrementCurrentGroup();
        int undoGroup = Undo.GetCurrentGroup();
        int replacedCount = 0;

        foreach (GameObject oldObj in candidates)
        {
            if (oldObj == null) continue;

            // Если участок дороги на сцене сам является префабом, распаковываем его, чтобы Unity разрешил удалить дочерний фонарь
            if (oldObj.transform.parent != null && PrefabUtility.IsPartOfPrefabInstance(oldObj.transform.parent.gameObject))
            {
                GameObject outerRoot = PrefabUtility.GetOutermostPrefabInstanceRoot(oldObj.transform.parent.gameObject);
                if (outerRoot != null)
                {
                    PrefabUtility.UnpackPrefabInstance(outerRoot, PrefabUnpackMode.OutermostRoot, InteractionMode.AutomatedAction);
                }
            }

            Transform parent = oldObj.transform.parent;
            Vector3 pos = oldObj.transform.position;
            Quaternion rot = oldObj.transform.rotation;
            Vector3 scale = oldObj.transform.localScale;
            int siblingIndex = oldObj.transform.GetSiblingIndex();

            GameObject newLamp = (GameObject)PrefabUtility.InstantiatePrefab(lampPrefab, parent);
            Undo.RegisterCreatedObjectUndo(newLamp, "Replace StreetLight with Lamp");

            newLamp.transform.SetPositionAndRotation(pos, rot);
            newLamp.transform.localScale = scale;
            newLamp.transform.SetSiblingIndex(siblingIndex);

            Undo.DestroyObjectImmediate(oldObj);
            replacedCount++;
        }

        Undo.CollapseUndoOperations(undoGroup);
        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        Debug.Log($"[Замена фонарей] Готово! Заменено объектов на сцене: {replacedCount}");
    }

    private void ReplaceInProjectPrefabs()
    {
        string[] patterns = ParsePatterns(targetNames);
        string lampPath = AssetDatabase.GetAssetPath(lampPrefab);
        string[] allPrefabGuids = AssetDatabase.FindAssets("t:Prefab", new[] { "Assets" });

        int modifiedPrefabs = 0;
        int totalReplaced = 0;

        try
        {
            for (int i = 0; i < allPrefabGuids.Length; i++)
            {
                string path = AssetDatabase.GUIDToAssetPath(allPrefabGuids[i]);
                if (path == lampPath) continue;

                EditorUtility.DisplayProgressBar("Проверка префабов дорог", path, (float)i / allPrefabGuids.Length);

                GameObject root = PrefabUtility.LoadPrefabContents(path);
                bool dirty = false;

                Transform[] children = root.GetComponentsInChildren<Transform>(true);
                List<GameObject> toReplace = new List<GameObject>();

                foreach (Transform t in children)
                {
                    if (t == null || t.gameObject == root) continue;
                    if (PrefabUtility.GetCorrespondingObjectFromSource(t.gameObject) == lampPrefab) continue;

                    if (MatchesName(t.name, patterns, partialMatch))
                        toReplace.Add(t.gameObject);
                }

                foreach (GameObject oldObj in toReplace)
                {
                    if (oldObj == null) continue;

                    Transform parent = oldObj.transform.parent;
                    Vector3 pos = oldObj.transform.localPosition;
                    Quaternion rot = oldObj.transform.localRotation;
                    Vector3 scale = oldObj.transform.localScale;
                    int siblingIndex = oldObj.transform.GetSiblingIndex();

                    GameObject newLamp = (GameObject)PrefabUtility.InstantiatePrefab(lampPrefab, parent);
                    newLamp.transform.localPosition = pos;
                    newLamp.transform.localRotation = rot;
                    newLamp.transform.localScale = scale;
                    newLamp.transform.SetSiblingIndex(siblingIndex);

                    DestroyImmediate(oldObj);
                    dirty = true;
                    totalReplaced++;
                }

                if (dirty)
                {
                    PrefabUtility.SaveAsPrefabAsset(root, path);
                    modifiedPrefabs++;
                }

                PrefabUtility.UnloadPrefabContents(root);
            }
        }
        finally
        {
            EditorUtility.ClearProgressBar();
        }

        AssetDatabase.SaveAssets();
        Debug.Log($"[Замена фонарей] Обновлено префабов дорог: {modifiedPrefabs}, всего заменено фонарей: {totalReplaced}");
    }

    private static string[] ParsePatterns(string raw)
    {
        string[] parts = raw.Split(new[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries);
        for (int i = 0; i < parts.Length; i++)
            parts[i] = parts[i].Trim();
        return parts;
    }

    private static bool MatchesName(string objName, string[] patterns, bool partial)
    {
        // Убираем суффиксы дубликатов вида " (1)", " (2)" при точном поиске
        string cleanName = Regex.Replace(objName, @"\s*\(\d+\)$", "").Trim();

        foreach (string p in patterns)
        {
            if (string.IsNullOrEmpty(p)) continue;
            if (partial)
            {
                if (objName.IndexOf(p, StringComparison.OrdinalIgnoreCase) >= 0)
                    return true;
            }
            else
            {
                if (string.Equals(cleanName, p, StringComparison.OrdinalIgnoreCase))
                    return true;
            }
        }
        return false;
    }
}
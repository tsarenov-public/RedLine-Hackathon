using UnityEditor;
using UnityEngine;

public class SyncTerrainLayers
{
    [MenuItem("Tools/Sync Terrain Layers From First")]
    public static void Sync()
    {
        GameObject parent = GameObject.Find("MainTerrains");
        if (parent == null) { Debug.LogError("Не найден объект MainTerrains"); return; }

        Terrain[] terrains = parent.GetComponentsInChildren<Terrain>();
        if (terrains.Length < 2) return;

        TerrainLayer[] sourceLayers = terrains[0].terrainData.terrainLayers;

        foreach (Terrain t in terrains)
        {
            Undo.RecordObject(t.terrainData, "Sync Terrain Layers");
            t.terrainData.terrainLayers = sourceLayers;
            EditorUtility.SetDirty(t.terrainData);
        }

        AssetDatabase.SaveAssets();
        Debug.Log($"Слои текстур скопированы на {terrains.Length} террейнов!");
    }
}
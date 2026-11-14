using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>Ownership and reversible terrain appearance for the editor city generator.</summary>
[DisallowMultipleComponent]
public sealed class NfsGeneratedCity : MonoBehaviour
{
    public const string Owner = "NfsCityGenerator.v1";
    [HideInInspector] public string owner = Owner;
    [HideInInspector] public string assetFolder;
    [HideInInspector] public string roadFingerprint;
    [HideInInspector] public int seed;
    [HideInInspector] public int frontBuildings;
    [HideInInspector] public int skylineBuildings;
    [HideInInspector] public int streetProps;
    [HideInInspector] public Terrain terrain;
    [HideInInspector] public TerrainData originalTerrainData;
    [HideInInspector] public TerrainData generatedTerrainData;
    [HideInInspector] public TerrainData originalColliderData;
    [HideInInspector] public Material originalTerrainMaterial;
    [HideInInspector] public Material generatedTerrainMaterial;
    [HideInInspector] public List<Placement> placements = new List<Placement>();

    [Serializable]
    public sealed class Placement
    {
        public Transform transform;
        public Bounds localBounds;
        public string prefabPath;
        public float station;
        public int side;
        public int row;
    }
}

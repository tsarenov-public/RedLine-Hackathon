using System;
using UnityEditor;
using UnityEngine;

internal static class FinishSpriteAuthoring
{
    internal const string Paper = "Assets/UI/Finish/StoryPaperSheet.png";

    internal static readonly string[] Keys =
    {
        "Assets/Cars/race cars/honda nsx/FinishKey.png",
        "Assets/Cars/race cars/gtr/FinishKey.png",
        "Assets/Cars/race cars/mclaren/FinishKey.png",
        "Assets/Cars/race cars/lamborgini/FinishKey.png"
    };

    [MenuItem("Tools/Arcade Vehicle/Configure finish sprites")]
    internal static void Configure()
    {
        Import(Paper, new Vector4(78f, 78f, 78f, 78f));
        foreach (var key in Keys) Import(key, Vector4.zero);
    }

    private static void Import(string path, Vector4 border)
    {
        if (AssetImporter.GetAtPath(path) is not TextureImporter importer)
            throw new InvalidOperationException($"Missing finish-screen PNG: {path}");

        var settings = new TextureImporterSettings();
        importer.ReadTextureSettings(settings);
        settings.textureType = TextureImporterType.Sprite;
        settings.spriteMode = (int)SpriteImportMode.Single;
        settings.spriteMeshType = SpriteMeshType.FullRect;
        settings.spriteBorder = border;
        settings.alphaIsTransparency = true;
        settings.mipmapEnabled = false;
        settings.wrapMode = TextureWrapMode.Clamp;
        importer.SetTextureSettings(settings);
        importer.textureCompression = TextureImporterCompression.Uncompressed;
        importer.SaveAndReimport();
    }
}

using System;
using System.IO;
using System.Linq;
using EasyRoads3Dv3;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

/// <summary>Non-destructive review cameras; never edits Main Camera or scene lighting.</summary>
public static class NfsCityPreview
{
    [MenuItem("Tools/NFS City Views/Frame City")]
    public static void FrameCity()
    {
        var path = ReadPath();
        var view = SceneView.lastActiveSceneView ?? EditorWindow.GetWindow<SceneView>();
        var bounds = new Bounds(path.centers[0], Vector3.zero);
        foreach (var point in path.centers) bounds.Encapsulate(point);
        view.LookAt(bounds.center, Quaternion.Euler(60, 0, 0), bounds.extents.magnitude * .8f, false, true);
    }

    [MenuItem("Tools/NFS City Views/Street View")]
    public static void StreetView()
    {
        var path = ReadPath(); var pose = path.Sample(path.length * .24f, 1);
        var view = SceneView.lastActiveSceneView ?? EditorWindow.GetWindow<SceneView>();
        view.LookAt(pose.center + pose.tangent * 24 + Vector3.up * 5,
            Quaternion.LookRotation(pose.tangent + Vector3.down * .04f), 26, false, true);
    }

    public static void RenderSavedScene()
    {
        EditorSceneManager.OpenScene("Assets/Scenes/FistMission.unity", OpenSceneMode.Single);
        NfsCityGenerator.ValidateCurrent();
        var path = ReadPath(); Directory.CreateDirectory("Logs/NfsCity");
        var bounds = new Bounds(path.centers[0], Vector3.zero);
        foreach (var point in path.centers) bounds.Encapsulate(point);
        Render("city-overview", bounds.center + new Vector3(-.12f, .9f, -.85f).normalized * bounds.size.magnitude,
            bounds.center + Vector3.up * 20, 55);
        RenderStreet(path, .12f, "city-street-start");
        RenderStreet(path, .38f, "city-street-middle");
        RenderStreet(path, .72f, "city-street-end");
        Debug.Log("[NFS City] Preview images rendered to Logs/NfsCity. Scene was not changed or saved.");
    }

    private static void RenderStreet(NfsCityGeometry.Path path, float fraction, string name)
    {
        float distance = path.length * fraction;
        var pose = path.Sample(distance, 1);
        var ahead = path.Sample(Mathf.Min(path.length, distance + 50), 1);
        Render(name, pose.center + Vector3.up * 2.5f, ahead.center + Vector3.up * 4, 67);
    }

    private static void Render(string name, Vector3 position, Vector3 target, float fov)
    {
        var go = new GameObject("NFS Temporary Review Camera") { hideFlags = HideFlags.HideAndDontSave };
        var texture = new RenderTexture(1600, 900, 24, RenderTextureFormat.ARGB32) { antiAliasing = 1 };
        var previous = RenderTexture.active;
        Texture2D image = null;
        try
        {
            var camera = go.AddComponent<Camera>(); camera.enabled = false;
            camera.transform.SetPositionAndRotation(position, Quaternion.LookRotation(target - position));
            camera.fieldOfView = fov; camera.aspect = 1600f / 900;
            camera.nearClipPlane = .15f; camera.farClipPlane = 6000;
            camera.clearFlags = CameraClearFlags.Skybox; camera.allowHDR = true;
            var additional = go.AddComponent<UniversalAdditionalCameraData>();
            additional.renderPostProcessing = false; additional.antialiasing = AntialiasingMode.FastApproximateAntialiasing;
            texture.Create();
            var request = new RenderPipeline.StandardRequest { destination = texture };
            RenderPipeline.SubmitRenderRequest(camera, request);
            RenderTexture.active = texture;
            image = new Texture2D(texture.width, texture.height, TextureFormat.RGB24, false);
            image.ReadPixels(new Rect(0, 0, texture.width, texture.height), 0, 0); image.Apply();
            File.WriteAllBytes("Logs/NfsCity/" + name + ".png", image.EncodeToPNG());
        }
        finally
        {
            RenderTexture.active = previous;
            if (image) UnityEngine.Object.DestroyImmediate(image);
            texture.Release(); UnityEngine.Object.DestroyImmediate(texture); UnityEngine.Object.DestroyImmediate(go);
        }
    }

    private static NfsCityGeometry.Path ReadPath()
    {
        var network = SceneManager.GetActiveScene().GetRootGameObjects().Single(g => g.name == "MainRoad");
        var script = network.transform.Find("Road Objects/road_0001").GetComponent<ERModularRoad>();
        return NfsCityGenerator.ReadPath(script.transform, script.road ?? new ERRoad(script));
    }
}

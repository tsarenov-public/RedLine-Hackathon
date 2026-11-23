using Arcade.Vehicle;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

internal static class MinimapHUDAuthoring
{
    private const string Folder = "Assets/Cars/MinimapHUD/";
    private const float Size = 248f;

    public static ArcadeMinimapHUD AddTo(Transform parent)
    {
        var radar = Element<ArcadeMinimapHUD>(parent, "Race Radar", new(Size, Size));
        var rect = (RectTransform)radar.transform;
        rect.anchorMin = rect.anchorMax = rect.pivot = Vector2.zero;
        rect.anchoredPosition = new Vector2(32, 32);

        var stencil = Image(rect, "Circular Mask", "Radar_Mask", new(Size, Size), new Color32(29, 30, 33, 255));
        stencil.gameObject.AddComponent<Mask>();
        var heading = Element<RectTransform>(stencil.transform, "Map Heading", new(Size, Size));
        var origin = Element<RectTransform>(heading, "Map Origin", Vector2.zero);
        var roads = Element<RawImage>(origin, "Road Network", Vector2.zero);
        roads.raycastTarget = false;
        var route = Element<MinimapRouteGraphic>(origin, "Race Route", Vector2.zero);
        route.color = new Color32(224, 226, 220, 255);
        route.raycastTarget = false;

        var markers = Element<RectTransform>(stencil.transform, "Car Markers", new(Size, Size));
        var player = Image(markers, "Player Chevron", "Radar_Chevron", new(26, 26), new Color32(223, 255, 63, 255));
        Image(rect, "8-Segment Bezel", "Radar_Bezel", new(Size, Size), Color.white);

        var binding = new SerializedObject(radar);
        binding.FindProperty("mapRotation").objectReferenceValue = heading;
        binding.FindProperty("mapOffset").objectReferenceValue = origin;
        binding.FindProperty("roadMap").objectReferenceValue = roads;
        binding.FindProperty("route").objectReferenceValue = route;
        binding.FindProperty("playerMarker").objectReferenceValue = player;
        binding.ApplyModifiedPropertiesWithoutUndo();
        radar.gameObject.SetActive(false);
        return radar;
    }

    private static Image Image(Transform parent, string name, string sprite, Vector2 size, Color color)
    {
        var image = Element<Image>(parent, name, size);
        image.sprite = RaceHudSprites.SpriteAt(Folder + sprite + ".png");
        image.color = color;
        image.raycastTarget = false;
        return image;
    }

    private static T Element<T>(Transform parent, string name, Vector2 size) where T : Component
    {
        var child = new GameObject(name, typeof(RectTransform)) { layer = 5 };
        var rect = (RectTransform)child.transform;
        rect.SetParent(parent, false);
        rect.anchorMin = rect.anchorMax = rect.pivot = Vector2.one * .5f;
        rect.sizeDelta = size;
        rect.anchoredPosition = Vector2.zero;
        return typeof(T) == typeof(RectTransform) ? (T)(Component)rect : child.AddComponent<T>();
    }
}

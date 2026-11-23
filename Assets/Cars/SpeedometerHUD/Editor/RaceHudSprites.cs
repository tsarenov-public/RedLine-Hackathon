using System;
using System.IO;
using UnityEditor;
using UnityEngine;

internal static class RaceHudSprites
{
    private const float NosSweep = Arcade.Vehicle.ArcadeSpeedometerHUD.NosSweepDegrees;
    private const string Tach = "Assets/Cars/SpeedometerHUD/";
    private const string Radar = "Assets/Cars/MinimapHUD/";
    private static readonly Color32 White = new(241, 239, 227, 255);
    private static readonly Color32 Lime = new(190, 245, 55, 255);
    private static readonly Color32 Charcoal = new(51, 53, 55, 255);
    private static readonly string[] Digits =
    {
        "01110100011001110101110011000101110", "00100011000010000100001000010001110",
        "01110100010000100110010001000011111", "11110000010000101110000010000111110",
        "00010001100101010010111110001000010", "11111100001000011110000010000111110",
        "01110100001000011110100011000101110", "11111000010001000100010000100001000",
        "01110100011000101110100011000101110", "01110100011000101111000010000101110"
    };

    internal static void Generate()
    {
        var face = new Raster(1024);
        face.Disc(494, new Color32(12, 13, 16, 178));
        face.Disc(479, new Color32(49, 51, 53, 255));
        face.Disc(467, new Color32(19, 20, 23, 255));
        face.Disc(452, new Color32(33, 34, 36, 255));
        for (int y = 73; y < 950; y += 7)
        {
            float half = Mathf.Sqrt(Mathf.Max(0, 441 * 441 - (y - 512) * (y - 512)));
            face.Line(new Vector2(512 - half, y), new Vector2(512 + half, y),
                1.5f, new Color32(38, 39, 41, 255));
        }
        face.Arc(453, 4, 0, 359.99f, new Color32(104, 104, 101, 255));
        face.Arc(416, 12, -135, 270, White);
        face.Arc(416, 15, 108, 27, new Color32(238, 66, 50, 255));
        face.Arc(433, 6, 108, 27, new Color32(163, 48, 37, 255));
        for (int i = 0; i <= 40; i++)
        {
            float angle = -135 + i * 6.75f;
            bool major = i % 4 == 0;
            face.Line(Point(angle, 410), Point(angle, major ? 370 : 391), major ? 7 : 3, White);
            if (major) face.Number(i / 4, Point(angle, 321), 7);
        }
        face.Save(Tach + "Speedometer_Dial.png");

        var needle = new Raster(1024);
        needle.Polygon(new Color32(75, 53, 29, 255), new(491, 468), new(512, 912), new(532, 468));
        needle.Polygon(new Color32(222, 188, 126, 255), new(499, 470), new(512, 911), new(524, 478));
        needle.Line(new(507, 520), new(512, 890), 3, new Color32(250, 226, 166, 255));
        needle.Disc(42, new Color32(15, 17, 20, 255));
        needle.Disc(36, new Color32(166, 165, 154, 255));
        needle.Disc(31, new Color32(91, 93, 92, 255));
        needle.Arc(27, 3, -76, 140, new Color32(189, 186, 169, 255));
        needle.Disc(15, new Color32(109, 111, 109, 255));
        needle.Save(Tach + "Speedometer_Needle.png");

        var track = new Raster(1024);
        track.Arc(469, 34, 180, NosSweep, new Color32(21, 22, 25, 255));
        track.Arc(469, 26, 180, NosSweep, new Color32(105, 110, 95, 255));
        track.Arc(469, 20, 180, NosSweep, new Color32(58, 63, 53, 255));
        track.Save(Tach + "Nitro_Bar_Bg.png");

        var charge = new Raster(1024);
        charge.Arc(469, 17, 180, NosSweep, new Color32(220, 255, 160, 255));
        charge.Arc(472, 7, 180, NosSweep, new Color32(250, 255, 226, 255));
        charge.Save(Tach + "Nitro_Bar_Fill.png");

        Panel(256, 124, Tach + "Gear_Panel.png");
        Panel(384, 124, Tach + "Digital_Speed_Panel.png");
        var nos = new Raster(180);
        nos.Line(new(40, 45), new(108, 113), 49, Lime);
        nos.Line(new(109, 113), new(133, 137), 23, Lime);
        nos.Line(new(125, 148), new(145, 128), 12, Lime);
        nos.Polygon(new Color32(29, 43, 26, 255), new(79, 121), new(51, 73), new(75, 78),
            new(58, 38), new(103, 89), new(79, 87));
        nos.Polygon(Lime, new(127, 39), new(155, 66), new(122, 70));
        nos.Save(Tach + "Nitro_Bottle.png");
        var up = new Raster(96);
        up.Polygon(White, new(10, 15), new(48, 81), new(86, 15));
        up.Save(Tach + "Upshift.png");

        var bezel = new Raster(512);
        bezel.Arc(244, 15, 0, 359.99f, new Color32(16, 17, 18, 255));
        for (int i = 0; i < 8; i++)
        {
            float start = i * 45f + 1.5f;
            bezel.Arc(244, 13, start, 42, Charcoal);
            bezel.Arc(248, 2, start, 42, new Color32(112, 113, 113, 255));
            bezel.Arc(237, 2, start, 42, new Color32(31, 32, 33, 255));
        }
        bezel.Save(Radar + "Radar_Bezel.png");
        var mask = new Raster(512);
        mask.Disc(241, White);
        mask.Save(Radar + "Radar_Mask.png");
        var marker = new Raster(96);
        marker.Polygon(new Color32(27, 27, 24, 255), new(48, 92), new(91, 11), new(48, 29), new(5, 11));
        marker.Polygon(White, new(48, 79), new(76, 24), new(48, 36), new(20, 24));
        marker.Save(Radar + "Radar_Chevron.png");

        AssetDatabase.Refresh();
    }

    internal static Sprite SpriteAt(string path)
    {
        var importer = (TextureImporter)AssetImporter.GetAtPath(path);
        if (importer.textureType != TextureImporterType.Sprite || !importer.alphaIsTransparency ||
            importer.spriteImportMode != SpriteImportMode.Single)
        {
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = false;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.wrapMode = TextureWrapMode.Clamp;
            var settings = new TextureImporterSettings();
            importer.ReadTextureSettings(settings);
            settings.spriteMeshType = SpriteMeshType.FullRect;
            settings.spritePivot = new Vector2(.5f, .5f);
            settings.spriteAlignment = (int)SpriteAlignment.Center;
            importer.SetTextureSettings(settings);
            importer.SaveAndReimport();
        }
        return AssetDatabase.LoadAssetAtPath<Sprite>(path);
    }

    private static Vector2 Point(float angle, float radius)
    {
        float radians = angle * Mathf.Deg2Rad;
        return new Vector2(512 + Mathf.Sin(radians) * radius, 512 + Mathf.Cos(radians) * radius);
    }

    private static void Panel(int width, int height, string path)
    {
        var lcd = new Raster(width, height);
        lcd.Box(2, 2, width - 2, height - 2, 25, new Color32(18, 16, 13, 255));
        lcd.Box(7, 7, width - 7, height - 7, 21, new Color32(131, 109, 66, 255));
        lcd.Box(12, 12, width - 12, height - 12, 16, new Color32(198, 155, 84, 255));
        lcd.Box(14, 22, width - 14, height - 14, 14, new Color32(224, 177, 94, 255));
        lcd.Line(new(25, height - 22), new(width - 25, height - 22), 2, new Color32(246, 204, 128, 255));
        lcd.Save(path);
    }

    private sealed class Raster
    {
        private const int Samples = 2;
        private readonly int width, height;
        private readonly Color32[] samples;
        private Vector2 Center => new(width * .5f, height * .5f);

        internal Raster(int width, int height = 0)
        {
            this.width = width;
            this.height = height > 0 ? height : width;
            samples = new Color32[this.width * this.height * Samples * Samples];
        }

        internal void Disc(float radius, Color32 ink)
        {
            var c = Center;
            Paint(c.x - radius, c.y - radius, c.x + radius, c.y + radius, ink,
                (x, y) => (x - c.x) * (x - c.x) + (y - c.y) * (y - c.y) <= radius * radius);
        }

        internal void Arc(float radius, float thickness, float angle, float sweep, Color32 ink)
        {
            var c = Center;
            float outer = radius + thickness * .5f, inner = radius - thickness * .5f;
            Paint(c.x - outer, c.y - outer, c.x + outer, c.y + outer, ink, (x, y) =>
            {
                float dx = x - c.x, dy = y - c.y;
                float d = dx * dx + dy * dy;
                if (d < inner * inner || d > outer * outer) return false;
                return Mathf.Repeat(Mathf.Atan2(dx, dy) * Mathf.Rad2Deg - angle, 360) <= sweep;
            });
        }

        internal void Box(float x0, float y0, float x1, float y1, float radius, Color32 ink)
        {
            Paint(x0, y0, x1, y1, ink, (x, y) =>
            {
                float dx = x - Mathf.Clamp(x, x0 + radius, x1 - radius);
                float dy = y - Mathf.Clamp(y, y0 + radius, y1 - radius);
                return dx * dx + dy * dy <= radius * radius;
            });
        }

        internal void Line(Vector2 a, Vector2 b, float thickness, Color32 ink)
        {
            float r = thickness * .5f;
            var d = b - a;
            if (d.sqrMagnitude < .01f) return;
            float inverse = 1f / d.sqrMagnitude;
            Paint(Mathf.Min(a.x, b.x) - r, Mathf.Min(a.y, b.y) - r,
                Mathf.Max(a.x, b.x) + r, Mathf.Max(a.y, b.y) + r, ink, (x, y) =>
            {
                var delta = new Vector2(x, y) - a;
                return (delta - d * Mathf.Clamp01(Vector2.Dot(delta, d) * inverse)).sqrMagnitude <= r * r;
            });
        }

        internal void Polygon(Color32 ink, params Vector2[] points)
        {
            var min = points[0]; var max = min;
            foreach (var p in points) { min = Vector2.Min(min, p); max = Vector2.Max(max, p); }
            Paint(min.x, min.y, max.x, max.y, ink, (x, y) =>
            {
                bool inShape = false;
                for (int i = 0, prev = points.Length - 1; i < points.Length; prev = i++)
                {
                    var a = points[i]; var b = points[prev];
                    if ((a.y > y) != (b.y > y) && x < a.x + (b.x - a.x) * (y - a.y) / (b.y - a.y))
                        inShape = !inShape;
                }
                return inShape;
            });
        }

        internal void Number(int number, Vector2 center, int scale)
        {
            string digits = number.ToString();
            float left = center.x - ((digits.Length * 6 - 1) * scale) / 2f;
            for (int d = 0; d < digits.Length; d++)
            {
                string glyph = Digits[digits[d] - '0'];
                for (int row = 0; row < 7; row++)
                    for (int col = 0; col < 5; col++)
                        if (glyph[row * 5 + col] == '1')
                        {
                            float x = left + (d * 6 + col) * scale, y = center.y + (2.5f - row) * scale;
                            Box(x, y, x + scale, y + scale, .7f, White);
                        }
            }
        }

        private void Paint(float x0, float y0, float x1, float y1, Color32 ink, Func<float, float, bool> inside)
        {
            int left = Mathf.Max(0, Mathf.FloorToInt(x0 * Samples));
            int bottom = Mathf.Max(0, Mathf.FloorToInt(y0 * Samples));
            int right = Mathf.Min(width * Samples, Mathf.CeilToInt(x1 * Samples));
            int top = Mathf.Min(height * Samples, Mathf.CeilToInt(y1 * Samples));
            for (int y = bottom; y < top; y++)
                for (int x = left; x < right; x++)
                    if (inside((x + .5f) / Samples, (y + .5f) / Samples))
                        samples[y * width * Samples + x] = ink;
        }

        internal void Save(string path)
        {
            var result = new Color32[width * height];
            for (int y = 0; y < height; y++)
                for (int x = 0; x < width; x++)
                {
                    int a = 0, r = 0, g = 0, b = 0;
                    for (int j = 0; j < Samples; j++)
                        for (int i = 0; i < Samples; i++)
                        {
                            var color = samples[(y * Samples + j) * width * Samples + x * Samples + i];
                            a += color.a; r += color.r * color.a; g += color.g * color.a; b += color.b * color.a;
                        }
                    if (a > 0) result[y * width + x] = new Color32((byte)(r / a), (byte)(g / a), (byte)(b / a),
                        (byte)(a / (Samples * Samples)));
                }
            var texture = new Texture2D(width, height, TextureFormat.RGBA32, false);
            texture.SetPixels32(result);
            texture.Apply();
            File.WriteAllBytes(path, texture.EncodeToPNG());
            UnityEngine.Object.DestroyImmediate(texture);
        }
    }
}

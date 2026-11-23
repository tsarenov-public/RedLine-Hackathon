using UnityEngine;
using UnityEngine.UI;

namespace Arcade.Vehicle
{
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class LcdDisplay : MaskableGraphic
    {
        private static readonly byte[] DigitMask = { 63, 6, 91, 79, 102, 109, 125, 7, 127, 111 };
        private static readonly Vector2[] Starts =
        {
            new(.15f, .95f), new(.91f, .86f), new(.91f, .43f), new(.15f, .05f),
            new(.09f, .43f), new(.09f, .86f), new(.15f, .5f)
        };
        private static readonly Vector2[] Ends =
        {
            new(.85f, .95f), new(.91f, .58f), new(.91f, .14f), new(.85f, .05f),
            new(.09f, .14f), new(.09f, .58f), new(.85f, .5f)
        };

        [SerializeField] private string value = "000";

        public string Value
        {
            get => value;
            set
            {
                if (this.value == value) return;
                this.value = value;
                SetVerticesDirty();
            }
        }

        protected override void OnPopulateMesh(VertexHelper canvas)
        {
            canvas.Clear();
            if (string.IsNullOrEmpty(value)) return;
            Rect rect = GetPixelAdjustedRect();
            float height = Mathf.Min(rect.height * .88f, rect.width / (value.Length * .74f));
            var size = new Vector2(height * .56f, height);
            float step = height * .69f;
            var origin = new Vector2(rect.center.x - (step * (value.Length - 1) + size.x) * .5f,
                rect.center.y - height * .5f);
            var unlit = color;
            unlit.a *= .085f;

            foreach (char glyph in value)
            {
                int mask = glyph is >= '0' and <= '9' ? DigitMask[glyph - '0'] : glyph == 'R' ? 115 : 54;
                for (int i = 0; i < Starts.Length; i++)
                    AddSegment(canvas, origin + Starts[i] * size, origin + Ends[i] * size, height * .055f,
                        (mask & (1 << i)) != 0 ? color : unlit);

                if (glyph is 'R' or 'N')
                {
                    var start = glyph == 'R' ? new Vector2(.46f, .49f) : new Vector2(.12f, .88f);
                    AddSegment(canvas, origin + start * size, origin + new Vector2(.89f, .10f) * size,
                        height * .052f, color);
                }

                origin.x += step;
            }
        }

        private static void AddSegment(VertexHelper mesh, Vector2 from, Vector2 to, float halfWidth, Color32 ink)
        {
            var direction = (to - from).normalized;
            var side = new Vector2(-direction.y, direction.x) * halfWidth;
            var cap = direction * halfWidth;
            int i = mesh.currentVertCount;
            mesh.AddVert(from - cap, ink, Vector2.zero);
            mesh.AddVert(from - side, ink, Vector2.zero);
            mesh.AddVert(to - side, ink, Vector2.zero);
            mesh.AddVert(to + cap, ink, Vector2.zero);
            mesh.AddVert(to + side, ink, Vector2.zero);
            mesh.AddVert(from + side, ink, Vector2.zero);
            for (int j = 1; j < 5; j++) mesh.AddTriangle(i, i + j, i + j + 1);
        }
    }
}

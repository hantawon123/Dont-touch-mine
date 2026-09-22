using UnityEngine;
using UnityEngine.UI;

namespace Game.Client.Emotes
{
    /// <summary>Six donut wedges drawn as a UI mesh.</summary>
    public sealed class EmoteWheelRingGraphic : MaskableGraphic
    {
        private const int SegmentsPerSlice = 12;

        public int? HighlightedSlice { get; set; }

        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();
            var rect = GetPixelAdjustedRect();
            var center = rect.center;
            var outer = Mathf.Min(rect.width, rect.height) * 0.5f;
            var inner = outer * (EmoteWheelView.InnerRadius / EmoteWheelView.OuterRadius);
            var idle = new Color(0.08f, 0.07f, 0.06f, 0.78f);
            var lit = new Color(0.72f, 0.42f, 0.18f, 0.92f);
            var line = new Color(0.95f, 0.9f, 0.8f, 0.22f);

            for (var slice = 0; slice < EmoteCatalog.Count; slice++)
            {
                AddSlice(
                    vh,
                    center,
                    inner,
                    outer,
                    slice * EmoteWheelSelection.SliceDegrees,
                    EmoteWheelSelection.SliceDegrees,
                    HighlightedSlice == slice ? lit : idle);
            }

            for (var slice = 0; slice < EmoteCatalog.Count; slice++)
            {
                AddDivider(vh, center, inner, outer, slice * EmoteWheelSelection.SliceDegrees, line);
            }
        }

        private static void AddSlice(
            VertexHelper vh,
            Vector2 center,
            float inner,
            float outer,
            float startDegrees,
            float spanDegrees,
            Color color)
        {
            var startIndex = vh.currentVertCount;
            for (var i = 0; i <= SegmentsPerSlice; i++)
            {
                var t = i / (float)SegmentsPerSlice;
                var radians = (startDegrees + spanDegrees * t) * Mathf.Deg2Rad;
                var dir = new Vector2(Mathf.Sin(radians), Mathf.Cos(radians));
                vh.AddVert(center + dir * inner, color, Vector2.zero);
                vh.AddVert(center + dir * outer, color, Vector2.zero);
            }

            for (var i = 0; i < SegmentsPerSlice; i++)
            {
                var a = startIndex + i * 2;
                vh.AddTriangle(a, a + 1, a + 3);
                vh.AddTriangle(a, a + 3, a + 2);
            }
        }

        private static void AddDivider(
            VertexHelper vh,
            Vector2 center,
            float inner,
            float outer,
            float degrees,
            Color color)
        {
            var radians = degrees * Mathf.Deg2Rad;
            var dir = new Vector2(Mathf.Sin(radians), Mathf.Cos(radians));
            var tangent = new Vector2(-dir.y, dir.x) * 1.5f;
            var startIndex = vh.currentVertCount;
            vh.AddVert(center + dir * inner - tangent, color, Vector2.zero);
            vh.AddVert(center + dir * inner + tangent, color, Vector2.zero);
            vh.AddVert(center + dir * outer + tangent, color, Vector2.zero);
            vh.AddVert(center + dir * outer - tangent, color, Vector2.zero);
            vh.AddTriangle(startIndex, startIndex + 1, startIndex + 2);
            vh.AddTriangle(startIndex, startIndex + 2, startIndex + 3);
        }

        private void Update()
        {
            SetVerticesDirty();
        }
    }
}

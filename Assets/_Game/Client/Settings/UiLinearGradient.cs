using UnityEngine;
using UnityEngine.UI;

namespace Game.Client.Settings
{
    /// <summary>
    /// Tints a UI graphic from one colour to another along an axis. Used for
    /// the lobby settings' 게임 나가기 plate, which the design paints as a
    /// gradient rather than a flat fill.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class UiLinearGradient : BaseMeshEffect
    {
        private Color start = Color.white;
        private Color end = Color.white;
        private bool vertical;

        public void Bind(Color from, Color to, bool alongVertical)
        {
            start = from;
            end = to;
            vertical = alongVertical;
            if (graphic != null)
            {
                graphic.SetVerticesDirty();
            }
        }

        public override void ModifyMesh(VertexHelper helper)
        {
            if (!IsActive() || helper == null || graphic == null)
            {
                return;
            }

            var rect = graphic.rectTransform.rect;
            var span = vertical ? rect.height : rect.width;
            if (span <= 0f)
            {
                return;
            }

            var origin = vertical ? rect.yMin : rect.xMin;
            var vertex = new UIVertex();
            var count = helper.currentVertCount;
            for (var index = 0; index < count; index++)
            {
                helper.PopulateUIVertex(ref vertex, index);
                var t = vertical
                    ? (vertex.position.y - origin) / span
                    : (vertex.position.x - origin) / span;
                vertex.color = Color.Lerp(start, end, Mathf.Clamp01(t));
                helper.SetUIVertex(vertex, index);
            }
        }
    }
}

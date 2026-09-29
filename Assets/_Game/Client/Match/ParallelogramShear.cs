using UnityEngine;
using UnityEngine.UI;

namespace Game.Client.Match
{
    /// <summary>
    /// Shears a UI graphic into a parallelogram whose base angle is 60 degrees.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class ParallelogramShear : BaseMeshEffect
    {
        public const float AngleDegrees = 60f;

        public static float SlantForHeight(float height)
        {
            return Mathf.Abs(height) / Mathf.Tan(AngleDegrees * Mathf.Deg2Rad);
        }

        public override void ModifyMesh(VertexHelper helper)
        {
            if (!IsActive() || helper == null)
            {
                return;
            }

            var rect = graphic.rectTransform.rect;
            var slant = SlantForHeight(rect.height);
            if (slant <= 0f)
            {
                return;
            }

            var vertex = new UIVertex();
            var count = helper.currentVertCount;
            for (var index = 0; index < count; index++)
            {
                helper.PopulateUIVertex(ref vertex, index);
                var t = rect.height <= 0f
                    ? 0f
                    : (vertex.position.y - rect.yMin) / rect.height;
                vertex.position.x += t * slant;
                helper.SetUIVertex(vertex, index);
            }
        }
    }
}

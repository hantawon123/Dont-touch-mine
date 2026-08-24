using UnityEngine;

namespace Game.Client.Items
{
    [DisallowMultipleComponent]
    public sealed class PlacementPreviewView : MonoBehaviour
    {
        [SerializeField]
        private Renderer[] previewRenderers;

        [SerializeField]
        private Material validMaterial;

        [SerializeField]
        private Material invalidMaterial;

        private bool? lastCanPlace;

        public void SetCanPlace(bool canPlace)
        {
            if (lastCanPlace == canPlace || previewRenderers == null)
            {
                return;
            }

            var material = canPlace ? validMaterial : invalidMaterial;
            foreach (var previewRenderer in previewRenderers)
            {
                if (previewRenderer != null)
                {
                    previewRenderer.sharedMaterial = material;
                }
            }

            lastCanPlace = canPlace;
        }
    }
}

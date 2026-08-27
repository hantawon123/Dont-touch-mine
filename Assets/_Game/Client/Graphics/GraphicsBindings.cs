using UnityEngine;

namespace Game.Client.Graphics
{
    public static class GraphicsBindings
    {
        public static void EnsureCanvas(GameObject canvasObject)
        {
            if (canvasObject == null)
            {
                return;
            }

            if (canvasObject.GetComponent<GraphicsBrightnessOverlay>() == null)
            {
                canvasObject.AddComponent<GraphicsBrightnessOverlay>();
            }
        }
    }
}

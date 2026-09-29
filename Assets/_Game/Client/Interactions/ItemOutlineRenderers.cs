using UnityEngine;

namespace Game.Client.Interactions
{
    internal static class ItemOutlineRenderers
    {
        public static bool IsGenerated(Renderer renderer)
        {
            if (renderer == null)
            {
                return true;
            }

            var objectName = renderer.gameObject.name;
            return objectName.StartsWith("[") && objectName.Contains("Outline");
        }
    }
}

using UnityEngine;

namespace Game.Client.Graphics
{
    public sealed class GraphicsRuntimeHost : MonoBehaviour
    {
        internal static event System.Action Destroyed;

        private void OnDestroy()
        {
            Destroyed?.Invoke();
        }
    }
}

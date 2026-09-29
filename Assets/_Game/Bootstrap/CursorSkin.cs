using UnityEngine;
using VContainer.Unity;

namespace Game.Bootstrap
{
    /// <summary>
    /// Puts the game's own picture on the mouse pointer for as long as the
    /// application runs.
    /// </summary>
    /// <remarks>
    /// Set once at startup rather than per scene: the pointer belongs to the
    /// application, not to any screen, and a scene that forgot to set it would
    /// otherwise show the operating system's arrow for a moment.
    /// <para>
    /// <see cref="CursorMode.Auto"/> lets the platform draw it as a hardware
    /// cursor where it can. On WebGL that means the browser draws it, which is
    /// the only way it moves without the frame's latency — but browsers cap a
    /// hardware cursor at 32×32 (Chrome, Edge, Firefox) or 128×128 (Safari)
    /// and silently fall back to the default arrow above that. Keep the texture
    /// at 32×32 for the web build. The hotspot is the pixel that clicks, from
    /// the texture's top-left; an arrow's is its tip, a hand's is its
    /// fingertip.
    /// </para>
    /// <para>
    /// The texture must be imported as <c>Cursor</c> (Texture Type) with
    /// Read/Write enabled, or <see cref="Cursor.SetCursor"/> refuses it on
    /// standalone and shows nothing on the web.
    /// </para>
    /// </remarks>
    public sealed class CursorSkin : IStartable
    {
        private readonly Texture2D texture;
        private readonly Vector2 hotspot;

        public CursorSkin(Texture2D texture, Vector2 hotspot)
        {
            this.texture = texture;
            this.hotspot = hotspot;
        }

        public void Start()
        {
            if (texture == null)
            {
                return;
            }

            Cursor.SetCursor(texture, hotspot, CursorMode.Auto);
        }
    }
}

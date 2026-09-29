using UnityEngine;

namespace Game.Client.Common
{
    /// <summary>
    /// Marks a scene root that stays on screen while the player moves between
    /// the frontend screens that share its scene.
    /// </summary>
    /// <remarks>
    /// The frontend screens are kept loaded in each other's company and swapped
    /// by switching their scene roots off, which is why a screen comes back
    /// instantly. Anything that belongs to the application rather than to one
    /// screen — a notice waiting on an answer, say — would be switched off with
    /// the screen that happens to own its scene, and reappear only when the
    /// player wandered back to it.
    /// <para>
    /// A root carrying this is left alone by that switching. It still goes when
    /// its scene is unloaded, so this buys a life across screens, not a life
    /// across the whole application.
    /// </para>
    /// </remarks>
    [DisallowMultipleComponent]
    public sealed class FrontendPersistentRoot : MonoBehaviour
    {
    }
}

using UnityEngine;

namespace Game.Client.Character
{
    /// <summary>
    /// Takes a picture of the screen and softens it, for a panel to sit over.
    /// </summary>
    /// <remarks>
    /// The design blurs the closet behind its confirmations. A live blur costs
    /// a full-screen pass every frame and a renderer feature to go with it, and
    /// nothing behind a modal moves — so the screen is captured once, when the
    /// modal opens, and the still is what gets softened.
    /// <para>
    /// The softening is the graphics card's own mip chain read with a bias,
    /// rather than a hard downsample. Shrinking a screen to a sixteenth and
    /// stretching it back reads as blocks, because every screen pixel lands on
    /// one of a sixteenth as many; a biased trilinear read blends two mip
    /// levels instead, which is smooth, and the bias is a continuous number so
    /// the strength can be dialled rather than doubled.
    /// </para>
    /// </remarks>
    internal static class ScreenBlur
    {
        /// <summary>
        /// The captured screen, softened. The caller owns what comes back and
        /// must destroy it.
        /// </summary>
        /// <param name="halvings">
        /// How far the capture is shrunk before its mips are built. One step
        /// costs nothing visible and quarters the memory; more than two starts
        /// to show as blocks whatever the bias does.
        /// </param>
        /// <param name="bias">
        /// How far up the mip chain to read. Each whole number doubles the
        /// smear, and fractions land between two levels.
        /// </param>
        /// <remarks>
        /// Call at the end of a frame — after <c>WaitForEndOfFrame</c> — or the
        /// capture is of a frame that has not finished drawing.
        /// </remarks>
        public static RenderTexture Capture(int halvings, float bias)
        {
            var width = Mathf.Max(1, Screen.width);
            var height = Mathf.Max(1, Screen.height);

            var captured = RenderTexture.GetTemporary(width, height, 0);
            ScreenCapture.CaptureScreenshotIntoRenderTexture(captured);

            for (var step = 0; step < halvings; step++)
            {
                width = Mathf.Max(1, width / 2);
                height = Mathf.Max(1, height / 2);
            }

            // Kept rather than borrowed from the temporary pool: a temporary
            // handed back is fair game for whoever asks next, and this one has
            // to survive for as long as the modal is up.
            var kept = new RenderTexture(width, height, 0)
            {
                name = "ClosetBackdrop",
                useMipMap = true,
                autoGenerateMips = false,
                filterMode = FilterMode.Trilinear,
                mipMapBias = bias,
                hideFlags = HideFlags.HideAndDontSave
            };

            Graphics.Blit(captured, kept);
            kept.GenerateMips();
            RenderTexture.ReleaseTemporary(captured);
            return kept;
        }

        /// <summary>
        /// How to read the capture. On the platforms whose textures start at
        /// the top the screenshot comes back upside down, so it is drawn with
        /// its vertical axis reversed rather than flipped in memory.
        /// </summary>
        public static Rect UvRect =>
            SystemInfo.graphicsUVStartsAtTop
                ? new Rect(0f, 1f, 1f, -1f)
                : new Rect(0f, 0f, 1f, 1f);
    }
}

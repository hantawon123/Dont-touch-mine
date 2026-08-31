using UnityEngine;

namespace Game.Client.Match
{
    public interface IHighlightTransitionView
    {
        void SetOpacity(float opacity);
    }

    /// <summary>Presentation only; timing is owned by the playback presenter.</summary>
    public sealed class HighlightTransitionView : MonoBehaviour, IHighlightTransitionView
    {
        private float opacity;
        public void SetOpacity(float value) => opacity = Mathf.Clamp01(value);

        private void OnGUI()
        {
            if (opacity <= 0f) return;
            var previousColor = GUI.color;
            var previousDepth = GUI.depth;
            GUI.depth = -1000;
            GUI.color = new Color(0f, 0f, 0f, opacity);
            GUI.DrawTexture(new Rect(0f, 0f, Screen.width, Screen.height), Texture2D.whiteTexture);
            GUI.color = previousColor;
            GUI.depth = previousDepth;
        }
    }
}

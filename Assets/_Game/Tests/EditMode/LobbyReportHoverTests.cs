using Game.Client.Lobby;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;

namespace Game.Architecture.Tests
{
    public sealed class LobbyReportHoverTests
    {
        [Test]
        public void LeavingTheRowKeepsTheReportButtonUntilThePointerIsGone()
        {
            var row = new GameObject("Row", typeof(RectTransform), typeof(Image));
            var tooltip = new GameObject("Report", typeof(RectTransform), typeof(Image));
            tooltip.transform.SetParent(row.transform, false);
            try
            {
                var hover = row.AddComponent<LobbyReportHover>();
                hover.Bind(row.GetComponent<Image>(), tooltip, Color.white);
                hover.ShowTooltip();
                Assert.That(tooltip.activeSelf, Is.True);

                hover.OnPointerExit(null);
                Assert.That(tooltip.activeSelf, Is.True);

                hover.OnPointerEnter(null);
                hover.HideIfStillLeft();
                Assert.That(tooltip.activeSelf, Is.True);

                hover.OnPointerExit(null);
                hover.HideIfStillLeft();
                Assert.That(tooltip.activeSelf, Is.False);
            }
            finally
            {
                Object.DestroyImmediate(row);
            }
        }
    }
}

using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Game.Client.Settings
{
    /// <summary>
    /// 로비 오버레이의 게임 나가기 판이 포인터에 답하는 방식 (S15P21D205-1086): 평소에는
    /// 어두운 판에 주황 테두리, 포인터가 올라오면 판을 주황 그라데이션으로 채운다.
    /// </summary>
    /// <remarks>
    /// <see cref="Home.HomeHoverHighlight"/> 는 이미지의 <b>색</b>만 바꾼다. 여기서 바뀌는
    /// 것은 색이 아니라 그라데이션을 칠하느냐 마느냐라서 따로 둔다 -
    /// <see cref="SettingsTabHover"/> 가 탭을 따로 맡는 것과 같은 이유다.
    /// <para>
    /// 채워진 뒤에는 테두리와 아이콘이 배경과 같은 주황이 되어 사라진다. 그래서 둘을 흰색으로
    /// 올려 글자와 같은 대비를 준다.
    /// </para>
    /// </remarks>
    [DisallowMultipleComponent]
    internal sealed class SettingsLeavePlateHover : MonoBehaviour,
        IPointerEnterHandler,
        IPointerExitHandler
    {
        private UiLinearGradient gradient;
        private Image icon;
        private Image stroke;

        public void Bind(UiLinearGradient fill, Image glyph, Image border)
        {
            gradient = fill;
            icon = glyph;
            stroke = border;
            Apply(false);
        }

        public void OnPointerEnter(PointerEventData eventData) => Apply(true);

        public void OnPointerExit(PointerEventData eventData) => Apply(false);

        /// <summary>
        /// 오버레이는 닫힐 때 꺼진다. 포인터가 판 위에 있는 채로 닫히면 나가기 신호는 오지
        /// 않으므로, 다음에 열렸을 때 채워진 채로 남지 않게 여기서 되돌린다.
        /// </summary>
        private void OnDisable() => Apply(false);

        private void Apply(bool hovered)
        {
            if (gradient != null)
            {
                gradient.enabled = hovered;
            }

            if (stroke != null)
            {
                stroke.color = hovered
                    ? SettingsStyle.Palette.TextPrimary
                    : SettingsStyle.Palette.Accent;
            }

            if (icon != null)
            {
                icon.color = hovered
                    ? SettingsStyle.Palette.TextPrimary
                    : SettingsStyle.Palette.TextHover;
            }
        }
    }
}

using System;
using UnityEngine;
using UnityEngine.EventSystems;

namespace Game.Client.Home
{
    public sealed class HomeFriendContextClick : MonoBehaviour, IPointerClickHandler
    {
        public Action<PointerEventData> RightClicked { get; set; }

        public void OnPointerClick(PointerEventData eventData)
        {
            if (eventData.button == PointerEventData.InputButton.Right)
                RightClicked?.Invoke(eventData);
        }
    }
}

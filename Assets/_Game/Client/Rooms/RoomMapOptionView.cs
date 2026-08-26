using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Game.Client.Rooms
{
    /// <summary>
    /// One selectable map thumbnail inside the create-room modal.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class RoomMapOptionView : MonoBehaviour
    {
        [SerializeField]
        private TMP_Text nameText;

        [SerializeField]
        private GameObject selectionHighlight;

        [SerializeField]
        private Button selectButton;

        private string mapId;

        public event Action<string> Selected;

        public string MapId => mapId;

        private void Awake()
        {
            selectButton.onClick.AddListener(OnSelectButtonClicked);
        }

        private void OnDestroy()
        {
            selectButton.onClick.RemoveListener(OnSelectButtonClicked);
        }

        public void Bind(string boundMapId, bool isSelected)
        {
            mapId = boundMapId;
            nameText.text = boundMapId;
            selectionHighlight.SetActive(isSelected);
        }

        private void OnSelectButtonClicked()
        {
            Selected?.Invoke(mapId);
        }
    }
}

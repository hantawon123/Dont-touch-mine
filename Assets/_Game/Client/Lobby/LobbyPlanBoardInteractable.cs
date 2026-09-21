using System;
using Game.Client.Interactions;
using UnityEngine;

namespace Game.Client.Lobby
{
    /// <summary>
    /// 로비 서벽의 작업대(공구판 + 책상)를 하나로 묶은 작전 계획판.
    /// 조준하고 F키를 누르면 방 설정 화면이 열린다.
    /// 방장은 편집하고 나머지는 읽기 전용으로 본다. 안내 문구도 그래서
    /// 방장은 "방 설정", 나머지는 "방 설정 보기"다.
    /// </summary>
    /// <remarks>
    /// Scene component with no dependencies of its own: the presenter that
    /// knows the host session and how to open the screen binds in at start.
    /// Until then the board is inert, so a scene without the lobby scope
    /// shows no prompt for it.
    /// <para>
    /// 상호작용이 가능한 동안(바인딩된 동안)에는 <see cref="outline"/>으로
    /// 작업대 전체에 실루엣을 늘 켜 두고, <see cref="label"/>(공중의 "ROOM SETTING ▼")도
    /// 함께 띄운다. 집을 수 있는 소품은 조준했을 때만 실루엣이 켜지지만, 이 판은
    /// 방에 하나뿐인 설정 입구라 처음 들어온 플레이어도 무엇이 눌리는지 바로 알아야 한다.
    /// </para>
    /// </remarks>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Collider))]
    public sealed class LobbyPlanBoardInteractable : MonoBehaviour, IInteractable
    {
        public static string HostPrompt =>
            Game.Core.Settings.UiTextCatalog.Shipped.Get(Game.Core.Settings.UiText.Lobby.HostPrompt, "ko");

        public static string GuestPrompt =>
            Game.Core.Settings.UiTextCatalog.Shipped.Get(Game.Core.Settings.UiText.Lobby.GuestPrompt, "ko");

        [SerializeField]
        private string prompt = HostPrompt;

        [Tooltip("바인딩된 동안 켜 두는 작업대 실루엣. 없으면 실루엣 없이 동작한다.")]
        [SerializeField]
        private InteractableFocusOutline outline;

        [Tooltip("바인딩된 동안 켜 두는 공중 안내 라벨(ROOM SETTING ▼). 없으면 라벨 없이 동작한다.")]
        [SerializeField]
        private GameObject label;

        private Func<bool> isLocalHost;
        private Action onInteract;

        public bool IsBound => onInteract != null;

        public string InteractionPrompt
        {
            get
            {
                if (isLocalHost != null && !isLocalHost())
                {
                    return GuestPrompt;
                }

                return string.IsNullOrWhiteSpace(prompt) ? HostPrompt : prompt;
            }
        }

        public Color InteractionPromptColor => Color.black;

        public bool TryGetInteractionPromptWorldPosition(out Vector3 worldPosition)
        {
            worldPosition = PromptWorldPosition;
            return true;
        }

        /// <summary>
        /// 키 안내를 책상 상판 중앙 바로 위에 둔다. 콜라이더는 공구판까지 높아
        /// 기본(콜라이더 꼭대기) 위치를 쓰면 선반 높이로 올라간다.
        /// </summary>
        public Vector3 PromptWorldPosition
        {
            get
            {
                if (TryGetComponent<Collider>(out var col) && col.enabled)
                {
                    var bounds = col.bounds;
                    return new Vector3(
                        bounds.center.x,
                        bounds.min.y + InteractionPromptView.WorldLift,
                        bounds.center.z);
                }

                return transform.position;
            }
        }

        /// <summary>실루엣이 현재 켜져 있는지. 실루엣 컴포넌트가 없으면 false.</summary>
        public bool IsOutlineVisible => outline != null && outline.IsVisible;

        /// <summary>안내 라벨이 현재 켜져 있는지. 라벨이 없으면 false.</summary>
        public bool IsLabelVisible => label != null && label.activeSelf;

        public void Bind(Func<bool> localHostQuery, Action interact)
        {
            if (localHostQuery == null)
            {
                throw new ArgumentNullException(nameof(localHostQuery));
            }

            isLocalHost = localHostQuery;
            onInteract = interact ?? throw new ArgumentNullException(nameof(interact));
            SetHighlightVisible(true);
        }

        public void Unbind()
        {
            isLocalHost = null;
            onInteract = null;
            SetHighlightVisible(false);
        }

        /// <remarks>
        /// A carried item takes the F key for dropping, and a board that
        /// answered the same press would open a screen over a dropped box.
        /// </remarks>
        public bool CanInteract(PlayerInteractor interactor)
        {
            return IsBound && interactor != null && interactor.CarriedItem == null;
        }

        public void Interact(PlayerInteractor interactor)
        {
            if (!CanInteract(interactor))
            {
                return;
            }

            onInteract.Invoke();
        }

        /// <remarks>
        /// 로비 씬은 백그라운드로 미리 로드된 뒤 Fusion이 넘겨받는데, 그 사이에 씬
        /// 오브젝트가 한 번 꺼지고 다시 켜진다. 바인딩은 그 전에 끝나 있어 OnDisable이
        /// 숨긴 실루엣·라벨을 다시 켜 줄 곳이 여기밖에 없다(실제 입장 흐름에서
        /// bound=true인데 둘 다 꺼져 있던 문제).
        /// </remarks>
        private void OnEnable()
        {
            SetHighlightVisible(IsBound);
        }

        private void OnDisable()
        {
            SetHighlightVisible(false);
        }

        /// <summary>실루엣과 공중 라벨을 함께 켜고 끈다. 둘 다 없어도 된다.</summary>
        private void SetHighlightVisible(bool visible)
        {
            if (outline != null)
            {
                outline.SetVisible(visible);
            }

            if (label != null)
            {
                label.SetActive(visible);
            }
        }
    }
}

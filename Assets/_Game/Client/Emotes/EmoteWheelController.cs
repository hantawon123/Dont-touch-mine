using Game.Client.Cameras;
using Game.Client.Combat;
using Game.Client.Interactions;
using Game.Client.Match;
using Game.Client.Players;
using Game.Core.Emotes;
using Game.Core.Settings;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Game.Client.Emotes
{
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(-80)]
    public sealed class EmoteWheelController : MonoBehaviour
    {
        private static EmoteWheelController instance;

        private PlayerCameraController cameraRig;
        private ControlSettingsSystem controls;
        private EmoteWheelView view;
        private bool open;
        private bool lookWasSuspended;
        private bool blocksLobbyShortcuts;

        /// <summary>
        /// 휠이 열려 있거나, 이번 프레임에 1–6/취소를 처리했다.
        /// 로비 1·2·Esc가 같은 키를 먹지 않게 한다.
        /// </summary>
        public static bool BlocksLobbyShortcuts =>
            instance != null && instance.blocksLobbyShortcuts;

        public static bool IsOpen => instance != null && instance.open;

        public static void Bind(PlayerCameraController camera)
        {
            if (camera == null)
            {
                return;
            }

            var host = camera.GetComponent<EmoteWheelController>();
            if (host == null)
            {
                host = camera.gameObject.AddComponent<EmoteWheelController>();
            }

            host.cameraRig = camera;
        }

        public void BindSettings(ControlSettingsSystem settings) => controls = settings;

        private void OnEnable()
        {
#if UNITY_SERVER
            // Dedicated servers receive emotes through network input, not a local UI.
            enabled = false;
#else
            instance = this;
#endif
        }

        private void Awake()
        {
            if (cameraRig == null)
            {
                cameraRig = GetComponent<PlayerCameraController>();
            }
        }

        private void OnDisable()
        {
            if (open)
            {
                Close(false);
            }

            blocksLobbyShortcuts = false;
            if (instance == this)
            {
                instance = null;
            }
        }

        private void OnDestroy()
        {
            if (view != null)
            {
                Destroy(view.gameObject);
            }
        }

        private void Update()
        {
            if (open && !CanStayOpen())
            {
                Close(false);
                blocksLobbyShortcuts = true;
                return;
            }

            var pressed = WasBoundKeyPressed();

            if (!open)
            {
                if (pressed && CanOpen())
                {
                    Open();
                }

                blocksLobbyShortcuts = open;
                return;
            }

            blocksLobbyShortcuts = true;
            var keyboard = Keyboard.current;
            if (EmoteWheelSelection.TrySliceFromKeyboard(keyboard, out var slice))
            {
                view.SetHighlight(slice);
                Close(true, slice);
                return;
            }

            if (pressed || EmoteWheelSelection.WasCancelKeyPressed(keyboard) || WasAnyMouseButtonPressed())
            {
                Close(false);
            }
        }

        private static bool WasAnyMouseButtonPressed()
        {
            var mouse = Mouse.current;
            if (mouse == null)
            {
                return false;
            }

            return mouse.leftButton.wasPressedThisFrame ||
                   mouse.rightButton.wasPressedThisFrame ||
                   mouse.middleButton.wasPressedThisFrame ||
                   mouse.forwardButton.wasPressedThisFrame ||
                   mouse.backButton.wasPressedThisFrame;
        }

        private void Open()
        {
            open = true;
            blocksLobbyShortcuts = true;
            if (view == null)
            {
                view = EmoteWheelView.Create(transform);
            }

            view.Show();
            view.SetHighlight(null);
            if (cameraRig != null)
            {
                lookWasSuspended = cameraRig.LookSuspended;
                cameraRig.LookSuspended = true;
            }
        }

        private void Close(bool confirm, int? slice = null)
        {
            open = false;
            if (view != null)
            {
                view.Hide();
            }

            if (cameraRig != null)
            {
                cameraRig.LookSuspended = lookWasSuspended;
            }

            if (!confirm || slice is not int index)
            {
                return;
            }

            var follow = cameraRig != null ? cameraRig.FollowTarget : null;
            if (follow == null)
            {
                return;
            }

            var def = EmoteCatalog.All[index];
            var movement = follow.GetComponent<PlayerMovement>();
            // 요청은 항상 이동 컴포넌트에 남긴다. 네트워크 플레이어는 다음 입력 틱에 실려 모터가
            // 복제하고, 그 결과가 ApplyNetworkState로 모든 피어(본인 포함)에 재생된다.
            if (movement != null && !movement.RequestEmote((byte)def.Id))
            {
                return;
            }

            // 단독 플레이어(튜토리얼·캐릭터 테스트)는 복제 경로가 없으니 바로 재생한다.
            var driver = follow.GetComponent<PlayerAnimationDriver>();
            if (driver != null && !driver.UsesNetworkState)
            {
                driver.TryPlayEmote(def.Id);
            }
        }

        private bool CanOpen()
        {
            if (!CanStayOpen())
            {
                return false;
            }

            if (cameraRig != null && cameraRig.LookSuspended)
            {
                return false;
            }

            var follow = cameraRig != null ? cameraRig.FollowTarget : null;
            var driver = follow != null ? follow.GetComponent<PlayerAnimationDriver>() : null;
            return driver == null || !driver.IsPunching;
        }

        /// <summary>
        /// 열려 있어도 되는 조건. 열린 뒤 기절하거나 물건을 집거나 공중에 뜨면 휠을 바로 닫는다.
        /// </summary>
        private bool CanStayOpen()
        {
            if (MatchChatView.BlocksPlayerInput || PlayerMovement.IsTextInputFocused())
            {
                return false;
            }

            var follow = cameraRig != null ? cameraRig.FollowTarget : null;
            if (follow == null || !follow.gameObject.activeInHierarchy)
            {
                return false;
            }

            var combatant = follow.GetComponent<PlayerCombatant>();
            if (combatant != null && combatant.IsStunned)
            {
                return false;
            }

            var interactor = follow.GetComponent<PlayerInteractor>();
            if (interactor != null && interactor.IsCarrying)
            {
                return false;
            }

            var placement = follow.GetComponent<ItemPlacementController>();
            if (placement != null && placement.IsPlacing)
            {
                return false;
            }

            // 표현 클립은 모두 서 있는 자세라 공중에서는 열지 않는다. 열린 채 뛰어오르면 닫는다.
            var driver = follow.GetComponent<PlayerAnimationDriver>();
            if (driver != null)
            {
                return driver.IsGrounded;
            }

            var movement = follow.GetComponent<PlayerMovement>();
            return movement == null || movement.IsGrounded;
        }

        private string BoundCode()
        {
            var code = controls != null
                ? controls.Current.Get(ControlAction.EmoteWheel)
                : ControlCatalog.Defaults.Get(ControlAction.EmoteWheel);
            return string.IsNullOrEmpty(code)
                ? ControlCatalog.Defaults.Get(ControlAction.EmoteWheel)
                : code;
        }

        private bool WasBoundKeyPressed() => ReadBoundButton(BoundCode())?.wasPressedThisFrame == true;

        private static UnityEngine.InputSystem.Controls.ButtonControl ReadBoundButton(string code)
        {
            if (string.IsNullOrEmpty(code))
            {
                return null;
            }

            switch (code)
            {
                case ControlCatalog.MouseLeft:
                    return Mouse.current != null ? Mouse.current.leftButton : null;
                case ControlCatalog.MouseRight:
                    return Mouse.current != null ? Mouse.current.rightButton : null;
                case ControlCatalog.MouseMiddle:
                    return Mouse.current != null ? Mouse.current.middleButton : null;
                default:
                    var keyboard = Keyboard.current;
                    return keyboard == null
                        ? null
                        : keyboard.TryGetChildControl<UnityEngine.InputSystem.Controls.ButtonControl>(code);
            }
        }
    }
}

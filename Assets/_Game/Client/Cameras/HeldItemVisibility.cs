using System;
using Game.Client.Interactions;
using UnityEngine;

namespace Game.Client.Cameras
{
    /// <summary>
    /// 1인칭에서 내가 들고 있는 물건을 내 화면에서만 지운다.
    /// 렌더러의 <see cref="Renderer.forceRenderingOff"/>만 켜고 끄므로 물건의 물리·소유·네트워크 상태는 그대로이고,
    /// 다른 플레이어 화면(각자 자기 HoldPoint에 붙여 그림)에는 그대로 보인다.
    /// 로비·대기실·인게임 모두 같은 카메라 리그(PlayerCameraController)를 쓰므로 어디서 잡아도 같이 적용된다.
    /// </summary>
    public sealed class HeldItemVisibility
    {
        private CarryableItem hiddenItem;
        private Renderer[] hiddenRenderers = Array.Empty<Renderer>();

        /// <summary>지금 숨기고 있는 물건. 없으면 null.</summary>
        public CarryableItem HiddenItem => hiddenItem;

        /// <summary>
        /// 매 프레임(LateUpdate) 부른다. 들고 있는 물건이 바뀌거나 놓으면 이전 물건은 바로 되살린다.
        /// </summary>
        /// <param name="carried">지금 들고 있는 물건(없으면 null)</param>
        /// <param name="hide">1인칭처럼 숨겨야 하는 상태인지</param>
        /// <param name="rescan">렌더러 목록을 다시 모을지. 조준·배정 실루엣처럼 들고 난 뒤 붙는 자식 렌더러를 잡는다</param>
        public void Apply(CarryableItem carried, bool hide, bool rescan = false)
        {
            var target = hide ? carried : null;
            if (target != hiddenItem)
            {
                Reveal();
                if (target == null) return;
                hiddenItem = target;
                rescan = true;
            }
            else if (hiddenItem == null)
            {
                return;
            }

            if (rescan)
            {
                // 이전 목록 중 물건에서 떨어진 렌더러가 숨은 채 남지 않도록 먼저 되살린 뒤 다시 모은다.
                SetForceRenderingOff(false);
                hiddenRenderers = hiddenItem.GetComponentsInChildren<Renderer>(true);
            }

            // 매 프레임 다시 켠다. 다른 코드가 되살려도 1인칭인 동안은 숨긴 상태를 유지한다.
            SetForceRenderingOff(true);
        }

        /// <summary>숨긴 물건을 전부 되살린다. 리그가 꺼지거나 따라갈 대상이 바뀔 때 부른다.</summary>
        public void Reveal()
        {
            SetForceRenderingOff(false);
            hiddenRenderers = Array.Empty<Renderer>();
            hiddenItem = null;
        }

        private void SetForceRenderingOff(bool off)
        {
            foreach (var renderer in hiddenRenderers)
            {
                if (renderer != null) renderer.forceRenderingOff = off;
            }
        }
    }
}

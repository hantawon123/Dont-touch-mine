using Game.Core.Players;
using UnityEngine;

namespace Game.Client.Players
{
    /// <summary>
    /// 웅크리기·엎드리기에서 이동 캡슐이 덮지 못하는 몸을 덮는 판정용 캡슐.
    /// 이동 캡슐은 낮은 통로를 지나가야 해서 자세별로 짧게 줄어들지만(웅크리기 1.10, 엎드리기 0.56)
    /// 실제 메시는 웅크리면 1.47까지 올라오고 엎드리면 앞으로 1.15 뻗는다.
    /// 이 캡슐은 다른 플레이어를 막고 펀치에 맞는 용도이며, 자기 이동(KCC·CharacterController)은 무시한다.
    /// 치수 근거: docs/design/character/collider.md
    /// </summary>
    internal sealed class PlayerBodyBlocker
    {
        private readonly CapsuleCollider capsule;

        private PlayerBodyBlocker(CapsuleCollider capsule)
        {
            this.capsule = capsule;
        }

        /// <summary>
        /// 루트 자식으로 만든다. KCC는 Spawned에서 자식 콜라이더를 모아 무시하므로
        /// Awake에서 만들어 두면 자기 캡슐과 부딪히지 않는다.
        /// </summary>
        public static PlayerBodyBlocker Create(Transform root, CharacterController controller)
        {
            var go = new GameObject("BodyBlocker");
            go.layer = LayerMask.NameToLayer("Player");
            go.transform.SetParent(root, false);

            var capsule = go.AddComponent<CapsuleCollider>();
            capsule.direction = 1;
            // 들고 있던 물건을 엎드린 몸 앞에 놓거나 던질 때 이 캡슐이 밀어내지 않게 한다.
            capsule.excludeLayers = LayerMask.GetMask("Carryable");
            capsule.enabled = false;

            if (controller != null)
            {
                Physics.IgnoreCollision(controller, capsule, true);
            }

            return new PlayerBodyBlocker(capsule);
        }

        public void Apply(PlayerPosture posture)
        {
            var t = capsule.transform;
            switch (posture)
            {
                case PlayerPosture.Crouching:
                    // 웅크린 몸 최고점 1.47, 상체가 앞으로 약간 쏠려 있다.
                    t.localPosition = new Vector3(0f, 0.735f, 0.05f);
                    t.localRotation = Quaternion.identity;
                    capsule.radius = 0.33f;
                    capsule.height = 1.47f;
                    capsule.enabled = true;
                    break;

                case PlayerPosture.Prone:
                    // 엎드린 몸은 발끝 뒤 0.38부터 머리 앞 1.15까지이고, 머리 쪽이 0.84로 높다.
                    // 축을 (앞 -0.12, 높이 0.26) -> (앞 0.84, 높이 0.54)로 기울여 한 캡슐로 덮는다.
                    var tail = new Vector3(0f, 0.26f, -0.12f);
                    var head = new Vector3(0f, 0.54f, 0.84f);
                    var axis = head - tail;
                    t.localPosition = (tail + head) * 0.5f;
                    t.localRotation = Quaternion.FromToRotation(Vector3.up, axis);
                    capsule.radius = 0.3f;
                    capsule.height = axis.magnitude + 0.6f;
                    capsule.enabled = true;
                    break;

                default:
                    // 서 있을 때는 이동 캡슐이 이미 몸과 맞는다.
                    capsule.enabled = false;
                    break;
            }
        }
    }
}

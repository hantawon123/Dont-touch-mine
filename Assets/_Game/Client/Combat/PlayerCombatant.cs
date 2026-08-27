using Game.Client.Interactions;
using Game.Client.Players;
using Game.Core.Players;
using Game.SOAP.Config;
using UnityEngine;
using UnityEngine.InputSystem;
using VContainer;

namespace Game.Client.Combat
{
    /// <summary>
    /// 전투 참가자: 빈손 좌클릭 공격(전방 근접 판정)과 피격/기절 반응을 담당한다.
    /// 판정 규칙(3타 기절, 기절 시간, 무적)은 IPlayerCombatRules(서버 시스템)가 소유하고,
    /// 이 컴포넌트는 입력 의도 전달과 로컬 표현만 맡는다.
    /// 더미(공격 안 하는 대상)는 Input Actions를 비우고 Is Attacker를 꺼서 사용한다.
    /// </summary>
    public sealed class PlayerCombatant : MonoBehaviour
    {
        private const float HitFlashSeconds = 0.15f;
        private const int MaxAttackHits = 8;

        [SerializeField]
        private InputActionAsset inputActions;

        [SerializeField]
        private CombatConfigSO combatConfig;

        [SerializeField, Min(0)]
        private int playerIndex;

        [SerializeField]
        private bool isAttacker = true;

        [SerializeField]
        private Transform visualRoot;

        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        private static readonly Color StunnedColor = new(0.35f, 0.35f, 0.4f);
        private static readonly Color HitFlashColor = new(1f, 0.25f, 0.25f);

        private IPlayerCombatRules combatRules;
        private InputActionMap playerMap;
        private InputAction attackAction;
        private PlayerInteractor interactor;
        private PlayerMovement movement;
        private Renderer[] visualRenderers;
        private MaterialPropertyBlock propertyBlock;
        private readonly Collider[] attackHits = new Collider[MaxAttackHits];
        private float nextAttackTime;
        private float hitFlashUntil;
        private bool wasTintApplied;
        [SerializeField, HideInInspector]
        private bool usesNetworkState;
        private bool networkStunned;

        public bool IsStunned =>
            usesNetworkState
                ? networkStunned
                : combatRules != null && combatRules.IsStunned(playerIndex, Time.timeAsDouble);

        public int PlayerIndex => playerIndex;

        [Inject]
        public void Construct(IPlayerCombatRules rules)
        {
            combatRules = rules;
        }

        private void Awake()
        {
            if (combatConfig == null)
            {
                Debug.LogError("PlayerCombatant: CombatConfigSO가 연결되지 않았습니다.", this);
                enabled = false;
                return;
            }

            interactor = GetComponent<PlayerInteractor>();
            movement = GetComponent<PlayerMovement>();
            propertyBlock = new MaterialPropertyBlock();

            if (visualRoot == null)
            {
                visualRoot = transform.Find("Visual");
            }

            visualRenderers = visualRoot != null
                ? visualRoot.GetComponentsInChildren<Renderer>()
                : new Renderer[0];

            if (isAttacker)
            {
                if (inputActions == null)
                {
                    Debug.LogError("PlayerCombatant: 공격자는 InputActionAsset이 필요합니다.", this);
                    enabled = false;
                    return;
                }

                playerMap = inputActions.FindActionMap("Player", throwIfNotFound: true);
                attackAction = playerMap.FindAction("Attack", throwIfNotFound: true);
            }
        }

        private void Start()
        {
            if (combatRules == null && !usesNetworkState)
            {
                Debug.LogError(
                    "PlayerCombatant: IPlayerCombatRules가 주입되지 않았습니다. " +
                    "씬에 PlaygroundLifetimeScope가 있고 Auto Inject Game Objects에 이 오브젝트가 등록되어 있는지 확인하세요.",
                    this);
                enabled = false;
            }
        }

        public void ConfigureNetworkPlayer(int index, bool acceptsLocalInput)
        {
            playerIndex = index;
            usesNetworkState = true;
            isAttacker = acceptsLocalInput;
            enabled = true;

            if (acceptsLocalInput)
            {
                playerMap?.Enable();
            }
            else
            {
                playerMap?.Disable();
            }
        }

        public void SetNetworkStunned(bool stunned)
        {
            usesNetworkState = true;
            networkStunned = stunned;
        }

        private void OnEnable()
        {
            playerMap?.Enable();
        }

        private void OnDisable()
        {
            playerMap?.Disable();
        }

        private void Update()
        {
            UpdateTint();

            // 기절 상태를 이동·상호작용 컴포넌트의 입력 잠금으로 전파한다.
            var stunned = IsStunned;
            if (movement != null)
            {
                movement.IsMovementLocked = stunned;
            }

            if (interactor != null)
            {
                interactor.IsInputLocked = stunned;
            }

            if (!isAttacker || stunned || Cursor.lockState != CursorLockMode.Locked)
            {
                return;
            }

            // 빈손 좌클릭만 공격이다. (물건을 들고 있으면 던지기가 담당)
            var isEmptyHanded = interactor == null || interactor.CarriedItem == null;
            if (isEmptyHanded && attackAction.WasPressedThisFrame() && Time.time >= nextAttackTime)
            {
                nextAttackTime = Time.time + combatConfig.AttackCooldownSeconds;
                PerformAttack();
            }
        }

        private void PerformAttack()
        {
            var center = transform.position
                + Vector3.up * 1f
                + transform.forward * combatConfig.AttackForwardOffset;

            var hitCount = Physics.OverlapSphereNonAlloc(
                center, combatConfig.AttackRadius, attackHits,
                Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);

            for (var i = 0; i < hitCount; i++)
            {
                var target = attackHits[i].GetComponentInParent<PlayerCombatant>();
                if (target == null || target == this)
                {
                    continue;
                }

                var direction = target.transform.position - transform.position;
                direction.y = 0f;

                if (interactor != null && interactor.UsesAuthoritativeCommands)
                {
                    interactor.TryRequestHit(target.PlayerIndex);
                    continue;
                }

                target.ReceiveHit(direction.normalized);
            }
        }

        /// <summary>피격 처리: 판정 규칙에 등록하고 결과에 따라 연출과 드랍을 수행한다.</summary>
        public void ReceiveHit(Vector3 hitDirection)
        {
            var result = combatRules.RegisterHit(playerIndex, Time.timeAsDouble);
            if (result == HitResult.Ignored)
            {
                return;
            }

            hitFlashUntil = Time.time + HitFlashSeconds;

            if (movement != null)
            {
                movement.AddImpulse(hitDirection * combatConfig.HitKnockbackImpulse);
            }

            if (result == HitResult.Stunned)
            {
                // 기절하면 들고 있던 물건을 떨어뜨린다. (기획서 13절)
                GetComponent<ICarriedItemDropper>()?.DropCarriedItem();
            }
        }

        // 기절 중 회색, 피격 순간 붉은 점멸. 평상시에는 원래 색으로 되돌린다.
        private void UpdateTint()
        {
            if (IsStunned)
            {
                ApplyTint(StunnedColor);
            }
            else if (Time.time < hitFlashUntil)
            {
                ApplyTint(HitFlashColor);
            }
            else if (wasTintApplied)
            {
                ClearTint();
            }
        }

        private void ApplyTint(Color color)
        {
            wasTintApplied = true;
            foreach (var visualRenderer in visualRenderers)
            {
                propertyBlock.SetColor(BaseColorId, color);
                visualRenderer.SetPropertyBlock(propertyBlock);
            }
        }

        private void ClearTint()
        {
            wasTintApplied = false;
            foreach (var visualRenderer in visualRenderers)
            {
                visualRenderer.SetPropertyBlock(null);
            }
        }
    }
}

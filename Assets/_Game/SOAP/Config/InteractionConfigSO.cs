using UnityEngine;

namespace Game.SOAP.Config
{
    [CreateAssetMenu(fileName = "InteractionConfig", menuName = "Game/Interaction Config")]
    public sealed class InteractionConfigSO : ScriptableObject
    {
        [SerializeField, Min(0.1f)]
        private float interactionDistance = 2f;

        [SerializeField, Min(0.01f)]
        private float aimedHighlightIntensity = 1.35f;

        [SerializeField, Min(0f)]
        private float throwSpeed = 8f;

        [SerializeField, Range(0f, 1f)]
        private float throwUpwardBias = 0.15f;

        [Header("배치 모드")]
        [SerializeField, Min(0.1f), Tooltip("배치 모드에서 물건을 두는 최대 손 거리(m). 발 위치 기준이라 정면 시선에서는 앞으로 이보다 조금 짧게 나간다. 호스트 놓기 한도(InteractionAuthorityRules.DefaultReleaseDistance)보다 작아야 한다")]
        private float placementMaxDistance = 2.8f;

        [SerializeField, Min(1f), Tooltip("Q/E 비틀기 속도(도/초)")]
        private float placementRotateSpeedDegrees = 90f;

        public float InteractionDistance => interactionDistance;
        public float AimedHighlightIntensity => aimedHighlightIntensity;
        public float ThrowSpeed => throwSpeed;
        public float ThrowUpwardBias => throwUpwardBias;
        public float PlacementMaxDistance => placementMaxDistance;
        public float PlacementRotateSpeedDegrees => placementRotateSpeedDegrees;
    }
}

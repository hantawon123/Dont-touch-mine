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

        public float InteractionDistance => interactionDistance;
        public float AimedHighlightIntensity => aimedHighlightIntensity;
        public float ThrowSpeed => throwSpeed;
        public float ThrowUpwardBias => throwUpwardBias;
    }
}

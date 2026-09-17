using Cysharp.Threading.Tasks;
using Game.Client.Common;
using Game.Client.Players;
using UnityEngine;

namespace Game.Client.Tutorial
{
    [RequireComponent(typeof(Collider))]
    public sealed class TutorialCompletionTrigger : MonoBehaviour
    {
        [SerializeField]
        private string destinationScene = "Home";

        private bool isLoading;
        private TutorialSession session;

        private void Awake()
        {
            session = FindAnyObjectByType<TutorialSession>();
            var trigger = GetComponent<Collider>();
            if (!trigger.isTrigger)
            {
                Debug.LogError("TutorialCompletionTrigger requires an isTrigger collider.", this);
                enabled = false;
            }
        }

        private void OnTriggerEnter(Collider other)
        {
            if (isLoading || other.GetComponentInParent<PlayerMovement>() == null)
            {
                return;
            }

            if (session != null && !session.IsComplete)
            {
                Debug.Log($"[Tutorial] Exit locked at step: {session.CurrentStep}", this);
                return;
            }

            isLoading = true;
            new PlayerPrefsTutorialCompletionStore().MarkCurrentVersionCompleted();
            SceneLoadSlicer.LoadSingleAsync(destinationScene)
                .Forget(exception =>
                {
                    isLoading = false;
                    Debug.LogException(exception, this);
                });
        }
    }
}

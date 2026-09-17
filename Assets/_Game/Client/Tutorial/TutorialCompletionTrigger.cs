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

        private void Awake()
        {
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

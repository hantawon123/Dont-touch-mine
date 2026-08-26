using System;
using Cysharp.Threading.Tasks;
using Game.Client.Interactions;
using Game.Core.Match;
using Game.Server.Match;
using UnityEngine;

namespace Game.Bootstrap
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Collider))]
    public sealed class ShredderInteractable : MonoBehaviour, IInteractable
    {
        private const int EjectionDelayMilliseconds = 500;

        [SerializeField]
        private Transform ejectionPoint;

        [SerializeField]
        private Transform ejectionTarget;

        [SerializeField, Min(0f)]
        private float ejectionSpeed = 4f;

        [SerializeField, Min(0f)]
        private float ejectionUpwardSpeed = 1.5f;

        private MatchSessionCoordinator session;
        private IMatchClock clock;
        private int playerIndex;

        public string InteractionPrompt => "들고 있는 물건 투입 [F]";

        public void Bind(
            MatchSessionCoordinator matchSession,
            int localPlayerIndex,
            IMatchClock matchClock)
        {
            session = matchSession ?? throw new ArgumentNullException(nameof(matchSession));
            clock = matchClock ?? throw new ArgumentNullException(nameof(matchClock));
            playerIndex = localPlayerIndex;
        }

        public bool CanInteract(PlayerInteractor interactor)
        {
            return interactor != null &&
                   interactor.CarriedItem != null &&
                   ejectionPoint != null &&
                   ejectionTarget != null;
        }

        public void Interact(PlayerInteractor interactor)
        {
            if (!CanInteract(interactor))
            {
                return;
            }

            if (interactor.TryUseAuthoritativeShredder())
            {
                return;
            }

            var item = interactor.CarriedItem;
            if (session != null)
            {
                var now = clock.ServerTime;
                if (session.TryDestroyHeldPlayerItem(playerIndex, now))
                {
                    interactor.ReleaseCarriedItem();
                    Destroy(item.gameObject);
                    return;
                }

                var ejectionPose = new Pose(ejectionPoint.position, ejectionPoint.rotation);
                if (!session.TryUseShredderOnHeldMapObject(playerIndex, ejectionPose, now))
                {
                    return;
                }
            }
            else if (item.IsPlayerItem)
            {
                interactor.ReleaseCarriedItem();
                Destroy(item.gameObject);
                return;
            }

            interactor.ReleaseCarriedItem();
            item.transform.SetParent(transform, true);
            item.gameObject.SetActive(false);
            EjectAfterDelay(item, this.GetCancellationTokenOnDestroy()).Forget();
        }

        private async UniTask EjectAfterDelay(
            CarryableItem item,
            System.Threading.CancellationToken cancellationToken)
        {
            await UniTask.Delay(
                EjectionDelayMilliseconds,
                cancellationToken: cancellationToken);

            if (item == null || ejectionPoint == null)
            {
                return;
            }

            var ejectionDirection = Vector3.ProjectOnPlane(
                    ejectionTarget.position - ejectionPoint.position,
                    Vector3.up)
                .normalized;

            item.transform.SetPositionAndRotation(
                ejectionPoint.position,
                ejectionPoint.rotation);
            item.gameObject.SetActive(true);
            item.OnThrown(
                (ejectionDirection * ejectionSpeed) +
                (Vector3.up * ejectionUpwardSpeed));
        }
    }
}

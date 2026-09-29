using UnityEngine;

namespace Game.Client.Lobby
{
    /// <summary>
    /// 작전 계획판 위에 떠 있는 "ROOM SETTING ▼" 안내 라벨. 화살표만 위아래로 살짝 흔들린다.
    /// </summary>
    /// <remarks>
    /// 라벨은 작업대 정면(방 안쪽)을 향해 고정돼 있고 카메라를 따라 돌지 않는다.
    /// 배치 메뉴가 방향을 정해 두며, 이 컴포넌트는 보이는 동안의 움직임만 맡는다.
    /// 켜고 끄기는 <see cref="LobbyPlanBoardInteractable"/>이 바인딩 여부에 따라
    /// 이 오브젝트의 활성 상태로 다룬다.
    /// </remarks>
    [DisallowMultipleComponent]
    public sealed class LobbyPlanBoardLabel : MonoBehaviour
    {
        [Tooltip("위아래로 흔들 화살표. 없으면 라벨은 정지 상태다.")]
        [SerializeField]
        private Transform arrow;

        [Tooltip("화살표 흔들림 폭(m).")]
        [SerializeField]
        [Min(0f)]
        private float bobAmplitude = 0.04f;

        [Tooltip("화살표 흔들림 속도(초당 왕복 횟수).")]
        [SerializeField]
        [Min(0f)]
        private float bobFrequency = 1.2f;

        private Vector3 arrowRestPosition;
        private bool arrowRestCaptured;

        private void OnEnable()
        {
            CaptureArrowRest();
        }

        private void LateUpdate()
        {
            if (arrow == null)
            {
                return;
            }

            CaptureArrowRest();
            var offset = Mathf.Sin(Time.time * bobFrequency * Mathf.PI * 2f) * bobAmplitude;
            arrow.localPosition = arrowRestPosition + new Vector3(0f, offset, 0f);
        }

        private void CaptureArrowRest()
        {
            if (arrowRestCaptured || arrow == null)
            {
                return;
            }

            arrowRestPosition = arrow.localPosition;
            arrowRestCaptured = true;
        }
    }
}

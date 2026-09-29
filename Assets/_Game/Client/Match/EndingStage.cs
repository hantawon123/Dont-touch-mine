using UnityEngine;

namespace Game.Client.Match
{
    /// <summary>
    /// Result 씬에 놓인 엔딩 무대(유치장 환경 + 카메라 + 스폰 자리)의 참조 묶음.
    /// 프레젠터가 이 참조로 아바타 복제본을 세우고 카메라를 켠다.
    /// </summary>
    /// <remarks>
    /// The stage lives in the Result scene far below the match map so the two
    /// never overlap. <see cref="Game.Bootstrap.ResultLifetimeScope"/> turns
    /// off every other camera and light in that scene, but leaves the ones
    /// under this component alone.
    /// </remarks>
    [DisallowMultipleComponent]
    public sealed class EndingStage : MonoBehaviour
    {
        [SerializeField] private Camera stageCamera;
        [SerializeField] private Transform cameraAnchor;
        [SerializeField] private Transform escapeSpawnRoot;
        [SerializeField] private Transform arrestSpawnRoot;
        [SerializeField] private Transform visualsRoot;

        public Camera StageCamera => stageCamera;
        public Transform VisualsRoot => visualsRoot != null ? visualsRoot : transform;
        public int EscapeSlotCount => escapeSpawnRoot != null ? escapeSpawnRoot.childCount : 0;
        public int ArrestSlotCount => arrestSpawnRoot != null ? arrestSpawnRoot.childCount : 0;

        public bool IsWired =>
            stageCamera != null && cameraAnchor != null &&
            EscapeSlotCount > 0 && ArrestSlotCount > 0;

        public Transform Slot(bool escaped, int index)
        {
            var root = escaped ? escapeSpawnRoot : arrestSpawnRoot;
            if (root == null || root.childCount == 0) return null;
            return root.GetChild(Mathf.Clamp(index, 0, root.childCount - 1));
        }

        /// <summary>카메라를 앵커 자세로 옮기고 인게임 카메라 위에 올린다.</summary>
        public void ShowCamera()
        {
            if (stageCamera == null) return;
            if (cameraAnchor != null)
            {
                stageCamera.transform.SetPositionAndRotation(cameraAnchor.position, cameraAnchor.rotation);
            }
            stageCamera.enabled = true;
        }

        public void HideCamera()
        {
            if (stageCamera != null) stageCamera.enabled = false;
        }

        /// <summary>에디터 배치 도구가 참조를 채울 때 쓴다.</summary>
        public void Wire(Camera camera, Transform anchor, Transform escapeRoot, Transform arrestRoot, Transform visuals)
        {
            stageCamera = camera;
            cameraAnchor = anchor;
            escapeSpawnRoot = escapeRoot;
            arrestSpawnRoot = arrestRoot;
            visualsRoot = visuals;
        }
    }
}

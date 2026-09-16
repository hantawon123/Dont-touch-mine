using System;
using UnityEngine;

namespace Game.Client.Cameras
{
    /// <summary>
    /// 1인칭에서 들고 있는 물건을 화면 어디에, 어떤 기울기로 보일지 정하는 Inspector 값.
    /// Play 중에 바꾸면 바로 반영된다. 3인칭은 PlayerInteractor의 몸 기준 손 위치를 그대로 쓴다.
    /// </summary>
    [Serializable]
    public sealed class FirstPersonHoldSettings
    {
        [Tooltip("1인칭에서 물건을 카메라 기준 자리에 둔다. 끄면 3인칭과 같은 몸 기준 위치")]
        public bool enabled = true;

        [Tooltip("카메라 기준 물건 위치(m). x=오른쪽, y=위(음수면 아래), z=앞. z를 줄이면 물건이 커 보인다")]
        public Vector3 offset = new(0.05f, -0.28f, 0.6f);

        [Tooltip("카메라 기준 물건 기울기(도). x=앞으로 숙임, y=좌우 돌림, z=좌우 기울임")]
        public Vector3 tilt = new(10f, -15f, 0f);
    }
}

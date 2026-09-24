using UnityEngine;

namespace Game.BotRuntime.Perception
{
    /// <summary>
    /// 물건의 "종류 스티커". 사람이 눈으로 구별할 수 있는 외형 종류를 문자열 키로 적어 둔다.
    /// 학습 환경은 이 키를 고정 사전으로 0..N 번호로 바꿔 관측 벡터에 넣는다.
    /// 소유자나 숨겨진 상태처럼 사람에게 보이지 않는 정보는 여기에 적지 않는다.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class BotSightKind : MonoBehaviour
    {
        [SerializeField]
        [Tooltip("외형 종류 키. 예: box, vase, radio. 학습 사전과 정확히 같은 문자열을 쓴다.")]
        private string kindKey;

        public string KindKey => string.IsNullOrWhiteSpace(kindKey)
            ? string.Empty
            : kindKey.Trim();

        /// <summary>테스트와 편집기 도구가 키를 코드로 지정할 때 사용한다.</summary>
        public void SetKindKey(string value)
        {
            kindKey = value;
        }
    }
}

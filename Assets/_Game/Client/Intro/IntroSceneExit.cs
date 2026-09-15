using UnityEngine;
using UnityEngine.SceneManagement;

namespace Game.Client.Intro
{
    /// <summary>
    /// 인트로가 끝나면(한 바퀴 완료 또는 스킵) 다음 씬을 단독(Single) 로드한다.
    /// 기본 목적지는 Home. Home 은 Build Settings 첫 씬으로 스스로 부트스트랩하므로
    /// 그냥 로드만 하면 에디터에서 Home 을 직접 Play 했을 때와 같은 상태로 시작한다.
    /// </summary>
    [RequireComponent(typeof(IntroLoopController))]
    public class IntroSceneExit : MonoBehaviour
    {
        [Tooltip("인트로가 끝나면 로드할 씬. Build Settings 에 들어 있어야 한다.")]
        public string nextSceneName = "Home";

        IntroLoopController _ctrl;
        bool _loading;

        void Awake()
        {
            _ctrl = GetComponent<IntroLoopController>();
        }

        void OnEnable()  => _ctrl.onIntroFinished.AddListener(LoadNext);
        void OnDisable() => _ctrl.onIntroFinished.RemoveListener(LoadNext);

        public void LoadNext()
        {
            if (_loading) return;
            _loading = true;

            if (string.IsNullOrEmpty(nextSceneName))
            {
                Debug.LogWarning("[Intro] nextSceneName 이 비어 있어 씬 전환을 건너뜁니다.");
                return;
            }

            var op = SceneManager.LoadSceneAsync(nextSceneName, LoadSceneMode.Single);
            if (op == null)
            {
                // Unity 는 Build Settings 에 없는 씬이면 예외 대신 null 을 돌려준다.
                Debug.LogError($"[Intro] '{nextSceneName}' 씬을 로드할 수 없습니다. " +
                               "File > Build Settings 의 Scenes In Build 에 들어 있는지 확인하세요.");
                _loading = false;
            }
        }
    }
}

using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;

namespace Game.Editor.Intro
{
    /// <summary>
    /// 에디터에서 Home 씬을 열어 놓고 Play 를 누르면 인트로 씬부터 시작하게 한다.
    /// (인트로가 끝나면 IntroSceneExit 가 Home 을 다시 로드하므로 결과는 "인트로 → Home")
    ///
    /// 팀 습관("항상 Home 에서 Play")을 그대로 두면서 빌드와 같은 시작 흐름을 보기 위한 장치.
    /// Home 이 아닌 씬(Lobby, Playground ...)에서 Play 하면 아무 것도 건드리지 않는다.
    /// 켜고 끄는 상태는 EditorPrefs 라 팀원마다 따로 저장된다. 기본값 ON.
    /// </summary>
    [InitializeOnLoad]
    static class IntroPlayFromHome
    {
        const string Pref      = "Intro.PlayFromHome";
        const string HomeScene = "Home";
        const string ScenePath = "Assets/_Game/Content/Scenes/Intro.unity";
        const string MenuPath  = "Tools/Intro/Home 에서 Play 하면 인트로 먼저 (에디터 전용)";

        static bool Enabled
        {
            get => EditorPrefs.GetBool(Pref, true);
            set => EditorPrefs.SetBool(Pref, value);
        }

        static IntroPlayFromHome()
        {
            EditorSceneManager.activeSceneChangedInEditMode += (_, __) => Apply();
            EditorApplication.playModeStateChanged += OnPlayModeChanged;
            EditorApplication.delayCall += Apply;
        }

        static void OnPlayModeChanged(PlayModeStateChange state)
        {
            if (state == PlayModeStateChange.ExitingEditMode) Apply();
        }

        /// <summary>현재 활성 씬이 Home 이고 기능이 켜져 있고 인트로 씬이 저장돼 있으면 시작 씬을 인트로로.</summary>
        static void Apply()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode && EditorApplication.isPlaying) return;

            if (Game.Network.Session.EditorDevelopmentSession.Enabled) return;
            SceneAsset start = null;
            if (Enabled && SceneManager.GetActiveScene().name == HomeScene)
                start = AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath);   // 없으면 null → 평소처럼 Home

            if (EditorSceneManager.playModeStartScene != start)
                EditorSceneManager.playModeStartScene = start;
        }

        [MenuItem(MenuPath, priority = 35)]
        static void Toggle()
        {
            Enabled = !Enabled;
            Apply();
            UnityEngine.Debug.Log(Enabled
                ? "[Intro] Home 에서 Play 하면 인트로부터 시작합니다. (이 컴퓨터의 에디터 설정)"
                : "[Intro] Home 에서 Play 하면 바로 Home 입니다. 인트로는 인트로 씬에서 Play 하거나 빌드에서만 나옵니다.");
        }

        [MenuItem(MenuPath, true)]
        static bool ToggleValidate()
        {
            Menu.SetChecked(MenuPath, Enabled);
            return true;
        }
    }
}

using Game.Network.Lobby;
using Game.Network.Session;
using UnityEditor;
using UnityEditor.Compilation;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Game.Editor
{
    [InitializeOnLoad]
    public sealed class DevelopmentServerWindow : EditorWindow
    {
        private const string RestoreKey = "Game.DevelopmentSession.Restore";
        private string code;
        private static bool reloadLocked;

        static DevelopmentServerWindow()
        {
            CompilationPipeline.compilationStarted += _ =>
            {
                if (!EditorDevelopmentSession.Enabled || !EditorApplication.isPlayingOrWillChangePlaymode) return;
                // Photon/Voice peers cannot survive an in-place assembly reload.
                // Stop before reload instead of retaining half-initialized peers.
                if (!reloadLocked)
                {
                    EditorApplication.LockReloadAssemblies();
                    reloadLocked = true;
                }
                EditorDevelopmentSession.RestartRequested = false;
                Debug.Log("[DevelopmentServer] Scripts changed. Stopping Play; restart after compilation.");
                EditorApplication.isPlaying = false;
            };
            EditorApplication.playModeStateChanged += state =>
            {
                if (state != PlayModeStateChange.EnteredEditMode) return;
                if (reloadLocked)
                {
                    reloadLocked = false;
                    EditorApplication.UnlockReloadAssemblies();
                }
                if (SessionState.GetBool(RestoreKey, false))
                {
                    var path = SessionState.GetString(RestoreKey + ".Scene", "");
                    EditorSceneManager.playModeStartScene = string.IsNullOrEmpty(path)
                        ? null : AssetDatabase.LoadAssetAtPath<SceneAsset>(path);
                    EditorSettings.enterPlayModeOptionsEnabled = SessionState.GetBool(RestoreKey + ".Options", false);
                    SessionState.SetBool(RestoreKey, false);
                }
                var restart = EditorDevelopmentSession.IsServer && EditorDevelopmentSession.RestartRequested;
                var restartCode = EditorDevelopmentSession.Code;
                EditorDevelopmentSession.RestartRequested = false;
                if (EditorDevelopmentSession.Enabled)
                    EditorDevelopmentSession.Configure(EditorDevelopmentSession.PeerRole.Normal, EditorDevelopmentSession.Code);
                if (restart)
                {
                    EditorDevelopmentSession.Report("다음 방 준비 중");
                    // Recreate the complete scene/domain and DI lifetime, just as a
                    // fresh server Play does; never reuse the closed room state.
                    EditorApplication.delayCall += () =>
                    {
                        if (!EditorApplication.isPlayingOrWillChangePlaymode)
                            Start(EditorDevelopmentSession.PeerRole.Server, restartCode);
                    };
                }
            };
        }

        [MenuItem("Game/Network/Development Server")]
        public static void Open() => GetWindow<DevelopmentServerWindow>("개발 서버");

        private void OnEnable() => code = EditorDevelopmentSession.Code;
        private void OnInspectorUpdate() => Repaint();

        private void OnGUI()
        {
            EditorGUILayout.HelpBox("모두 같은 코드 변경분을 받은 뒤 같은 테스트 코드를 입력하세요. 서버 담당자 한 명만 서버로 실행합니다. 한국 지역을 사용합니다.", MessageType.Info);
            using (new EditorGUI.DisabledScope(EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling || EditorApplication.isUpdating))
            {
                code = EditorGUILayout.TextField("테스트 코드 (6자)", code);
                var valid = RoomCodeGenerator.IsWellFormed(RoomCodeGenerator.Normalize(code));
                if (!valid) EditorGUILayout.HelpBox("영문 대문자·숫자 6자를 입력하세요. 예: DEV001", MessageType.Warning);
                using (new EditorGUI.DisabledScope(!valid))
                {
                    if (GUILayout.Button("개발 서버로 Play")) Start(EditorDevelopmentSession.PeerRole.Server, code);
                    if (GUILayout.Button("개발 클라이언트로 Play")) Start(EditorDevelopmentSession.PeerRole.Client, code);
                }
            }
            EditorGUILayout.LabelField("현재 역할", EditorDevelopmentSession.Role.ToString());
            EditorGUILayout.LabelField("상태", EditorDevelopmentSession.Status);
            EditorGUILayout.HelpBox("서버 준비 후 첫 클라이언트가 ‘방 만들기’, 나머지는 ‘게임 찾기’로 참가합니다. 최대 6명입니다. 서버 담당자는 이 에디터로 직접 플레이하지 않습니다. 방장이 나가면 서버가 다음 방을 자동으로 준비합니다. 준비 중에는 잠시 기다리세요.", MessageType.None);
            if (EditorApplication.isPlaying && GUILayout.Button("Play 종료")) EditorApplication.isPlaying = false;
        }

        // Also used by the isolated Editor smoke run. No scene on disk is opened/saved.
        public static void Start(EditorDevelopmentSession.PeerRole role, string testCode)
        {
            if (EditorApplication.isCompiling || EditorApplication.isUpdating)
                throw new System.InvalidOperationException("Wait for Unity compilation and asset import before starting a development session.");
            var home = AssetDatabase.LoadAssetAtPath<SceneAsset>("Assets/_Game/Content/Scenes/Home.unity");
            if (home == null) throw new System.InvalidOperationException("Home scene is missing.");
            EditorDevelopmentSession.Configure(role, testCode);
            EditorDevelopmentSession.RestartRequested = false;
            SessionState.SetString(RestoreKey + ".Scene", AssetDatabase.GetAssetPath(EditorSceneManager.playModeStartScene));
            SessionState.SetBool(RestoreKey + ".Options", EditorSettings.enterPlayModeOptionsEnabled);
            SessionState.SetBool(RestoreKey, true);
            // Server scene preparation requires scene reload; restore the user's
            // fast-enter settings and start scene when this Play session ends.
            EditorSettings.enterPlayModeOptionsEnabled = false;
            EditorSceneManager.playModeStartScene = home;
            EditorApplication.isPlaying = true;
        }
    }
}

using System.Collections;
using System;
using Fusion;
using UnityEngine;
using UnityEngine.SceneManagement;
using System.Threading.Tasks;

#if UNITY_EDITOR
using Unity.Multiplayer.PlayMode;
#endif


namespace Game.Network.Players
{
    public sealed class BotKccTestBootstrap : MonoBehaviour
    {   
        [SerializeField]
        private NetworkObject botPrefab;

        [SerializeField]
        private Transform spawnPoint;

        [SerializeField]
        private Transform target;

        [SerializeField]
        private string sessionName = "BotKccTest";

        /// <summary>씬에 지정된 봇 시작점. 학습 환경이 에피소드 시작 자세의 기준으로 쓴다.</summary>
        public Transform SpawnPoint => spawnPoint;

        private static GameMode ResolveGameMode()
        {
        #if UNITY_EDITOR
            var tags = CurrentPlayer.ReadOnlyTags();

            if (tags != null)
            {
                foreach (string tag in tags)
                {
                    if (string.Equals(
                            tag,
                            "Host",
                            StringComparison.OrdinalIgnoreCase))
                    {
                        return GameMode.Host;
                    }

                    if (string.Equals(
                            tag,
                            "Client",
                            StringComparison.OrdinalIgnoreCase))
                    {
                        return GameMode.Client;
                    }
                }
            }
        #endif

            // Multiplayer Play Mode를 사용하지 않을 때는 기존 단독 시험 유지
            return GameMode.Single;
        }

        private async void Start()
        {
#if !GAME_TRAINING
            Debug.LogError(
                "[Bot Test] GAME_TRAINING이 설정된 학습 프로필을 사용하세요.",
                this);
            return;
#else
            try
            {
                GameMode gameMode = ResolveGameMode();

                // Host가 방을 먼저 열 시간을 준다.
                if (gameMode == GameMode.Client)
                {
                    await Task.Delay(1500);

                    if (this == null)
                    {
                        return;
                    }
                }

                int sceneIndex = gameObject.scene.buildIndex;

                if (sceneIndex < 0)
                {
                    Debug.LogError(
                        "[Bot Test] 현재 씬을 학습 프로필의 Scene List에 추가하세요.",
                        this);
                    return;
                }

                var sceneManager =
                    gameObject.AddComponent<NetworkSceneManagerDefault>();

                var runner = gameObject.AddComponent<NetworkRunner>();
                runner.ProvideInput = false;

                var sceneInfo = new NetworkSceneInfo();
                sceneInfo.AddSceneRef(
                    SceneRef.FromIndex(sceneIndex),
                    LoadSceneMode.Single);

                var result = await runner.StartGame(new StartGameArgs
                {
                    GameMode = gameMode,
                    SessionName =
                        gameMode == GameMode.Single ? null : sessionName,
                    EnableClientSessionCreation =
                        gameMode != GameMode.Client,
                    Scene = sceneInfo,
                    SceneManager = sceneManager
                });

                if (this == null)
                {
                    return;
                }

                if (!result.Ok)
                {
                    Debug.LogError(
                        $"[Bot Test] 시작 실패: {result.ShutdownReason}",
                        this);
                    return;
                }

                Debug.Log($"[Bot Test] {gameMode} 모드 실행 준비 완료.", this);
                StartCoroutine(SpawnWhenRunnerReady(runner));
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
            }
#endif
        }

        private IEnumerator SpawnWhenRunnerReady(NetworkRunner runner)
        {
            // Fusion이 로컬 플레이어 등록을 끝낼 때까지 기다린다.
            while (runner != null &&
                (!runner.IsRunning || !runner.LocalPlayer.IsRealPlayer))
            {
                yield return null;
            }

            if (runner == null)
            {
                yield break;
            }

            // 등록 직후의 한 프레임까지 마무리하도록 추가로 기다린다.
            yield return null;

            SpawnTestBot(runner);
        }


        private void SpawnTestBot(NetworkRunner runner)
        {
            if (!runner.IsServer)
            {
                return;
            }

            if (botPrefab == null || spawnPoint == null || target == null)
            {
                Debug.LogError("[Bot Test] 프리팹, 시작점, 목표를 모두 연결하세요.", this);
                return;
            }

            var bot = runner.Spawn(
                botPrefab,
                spawnPoint.position,
                spawnPoint.rotation,
                PlayerRef.None);

            if (bot == null)
            {
                Debug.LogError("[Bot Test] 봇 생성에 실패했습니다.", this);
                return;
            }

            var botInput = bot.GetComponent<BotMoveToTarget>();
            var motor = bot.GetComponent<NetworkPlayerMotor>();

            if (botInput == null || motor == null)
            {
                Debug.LogError("[Bot Test] 봇에 필요한 이동 컴포넌트가 없습니다.", this);
                runner.Despawn(bot);
                return;
            }

            botInput.SetTarget(target);

            var pose = new Pose(spawnPoint.position, spawnPoint.rotation);

            if (!motor.TryTeleport(pose))
            {
                Debug.LogError("[Bot Test] 시작 위치 배치 요청에 실패했습니다.", this);
                runner.Despawn(bot);
                return;
            }

            Debug.Log("[Bot Test] 봇 생성 및 목표 연결 완료.", this);
        }

    }
}
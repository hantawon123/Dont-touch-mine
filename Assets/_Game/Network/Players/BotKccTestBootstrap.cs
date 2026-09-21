using System;
using Fusion;
using UnityEngine;
using UnityEngine.SceneManagement;

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
                    GameMode = GameMode.Single,
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

                Debug.Log("[Bot Test] 테스트 실행 준비 완료.", this);
                SpawnTestBot(runner);
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
            }
#endif
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
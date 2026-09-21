using Unity.MLAgents;
using Unity.MLAgents.Actuators;
using Unity.MLAgents.Sensors;
using UnityEngine;

namespace Game.Training
{
    [RequireComponent(typeof(TrainingMovement))]
    public class ReachAgent : Agent
    {
        [SerializeField]
        private Transform target;

        [SerializeField, Min(0.1f)]
        private float successDistance = 0.8f;

        [Header("Evaluation")]
        [SerializeField]
        private bool evaluationMode;

        [SerializeField, Min(1)]
        private int evaluationEpisodes = 100;

        [SerializeField]
        private int evaluationSeed = 20260915;

        private System.Random evaluationRandom;

        private TrainingMovement trainingMovement;
        private CharacterController characterController;

        private int completedEpisodes;
        private int successfulEpisodes;
        private float totalSuccessfulSeconds;
        private float episodeStartedAt;
        private bool episodeHasStarted;
        private bool episodeSucceeded;
        private bool evaluationFinished;

        public override void Initialize()
        {
            trainingMovement = GetComponent<TrainingMovement>();
            characterController = GetComponent<CharacterController>();

            if (evaluationMode)
            {
                evaluationRandom = new System.Random(evaluationSeed);
            }
        }

        public override void OnEpisodeBegin()
        {
            // 평가가 이미 끝났다면 더 이상 새 문제를 만들지 않는다.
            if (evaluationFinished)
            {
                return;
            }

            // 첫 에피소드가 아니라면 이전 에피소드의 결과를 기록한다.
            if (evaluationMode && episodeHasStarted)
            {
                RecordPreviousEpisode();
            }

            // 방금 기록한 에피소드가 마지막이었다면 정지한다.
            if (evaluationFinished)
            {
                return;
            }

            // 이전 에피소드의 이동 명령을 제거한다.
            trainingMovement.SetMoveInput(Vector2.zero);

            // 캐릭터를 새로운 시작 위치로 옮긴다.
            characterController.enabled = false;
            transform.localPosition = new Vector3(
                GetRandomCoordinate(),
                0.1f,
                GetRandomCoordinate());
            characterController.enabled = true;

            // 캐릭터와 너무 가깝지 않은 목표 위치를 만든다.
            Vector3 targetPosition;

            do
            {
                targetPosition = new Vector3(
                    GetRandomCoordinate(),
                    0.25f,
                    GetRandomCoordinate());
            }
            while (Vector3.Distance(
                       transform.localPosition,
                       targetPosition) < 2f);

            target.localPosition = targetPosition;

            // 새 에피소드의 평가 정보를 초기화한다.
            episodeSucceeded = false;
            episodeStartedAt = Time.time;
            episodeHasStarted = true;
        }

        public override void CollectObservations(VectorSensor sensor)
        {
            // 캐릭터에서 목표까지의 상대 방향을 계산한다.
            Vector3 directionToTarget =
                target.localPosition - transform.localPosition;

            // AI에게 X와 Z 방향 차이 두 개를 알려준다.
            sensor.AddObservation(directionToTarget.x / 5f);
            sensor.AddObservation(directionToTarget.z / 5f);
        }

        public override void OnActionReceived(ActionBuffers actions)
        {
            if (evaluationFinished)
            {
                trainingMovement.SetMoveInput(Vector2.zero);
                return;
            }

            // AI가 선택한 연속 행동 두 개를 꺼낸다.
            float moveX = actions.ContinuousActions[0];
            float moveZ = actions.ContinuousActions[1];

            // AI의 행동을 실제 이동 코드에 전달한다.
            trainingMovement.SetMoveInput(new Vector2(moveX, moveZ));

            float distance =
                Vector3.Distance(transform.position, target.position);

            // 목표에 충분히 가까워지면 성공이다.
            if (distance < successDistance)
            {
                episodeSucceeded = true;
                SetReward(1f);
                EndEpisode();
                return;
            }

            // 더 빠르게 도착하도록 매 행동에 작은 비용을 준다.
            AddReward(-0.001f);

            // 바닥 아래로 떨어지면 실패다.
            if (transform.position.y < -1f)
            {
                SetReward(-1f);
                EndEpisode();
            }
        }

        private float GetRandomCoordinate()
        {
            if (evaluationMode)
            {
                // 평가에서는 언제나 같은 순서의 위치를 만든다.
                if (evaluationRandom == null)
                {
                    evaluationRandom =
                        new System.Random(evaluationSeed);
                }

                return (float)(
                    evaluationRandom.NextDouble() * 8.0 - 4.0);
            }

            // 학습에서는 매번 새로운 무작위 위치를 사용한다.
            return Random.Range(-4f, 4f);
        }

        private void RecordPreviousEpisode()
        {
            completedEpisodes++;

            if (episodeSucceeded)
            {
                successfulEpisodes++;
                totalSuccessfulSeconds +=
                    Time.time - episodeStartedAt;
            }

            // 10회마다 평가 결과를 Console에 출력한다.
            if (completedEpisodes % 10 == 0 ||
                completedEpisodes >= evaluationEpisodes)
            {
                float successRate =
                    successfulEpisodes * 100f /
                    completedEpisodes;

                float averageSuccessSeconds =
                    successfulEpisodes > 0
                        ? totalSuccessfulSeconds /
                          successfulEpisodes
                        : 0f;

                Debug.Log(
                    $"[Reach Evaluation] " +
                    $"Episodes: {completedEpisodes}/" +
                    $"{evaluationEpisodes}, " +
                    $"Success: {successfulEpisodes}, " +
                    $"Rate: {successRate:F1}%, " +
                    $"Average Success Time: " +
                    $"{averageSuccessSeconds:F2}s",
                    this);
            }

            // 정해진 평가 횟수가 끝나면 Agent를 멈춘다.
            if (completedEpisodes >= evaluationEpisodes)
            {
                evaluationFinished = true;
                episodeHasStarted = false;
                trainingMovement.SetMoveInput(Vector2.zero);

                DecisionRequester decisionRequester =
                    GetComponent<DecisionRequester>();

                if (decisionRequester != null)
                {
                    decisionRequester.enabled = false;
                }
            }
        }
    }
}
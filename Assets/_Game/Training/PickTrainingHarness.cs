using System.Collections;
using System.Collections.Generic;
using System.Text;
using Game.BotRuntime.Perception;
using Game.Client.Interactions;
using Game.Network.Match;
using Game.Network.Players;
using Game.Server.Items;
using UnityEngine;

namespace Game.Training
{
    /// <summary>
    /// Training_Pick 씬의 반복 실행기. 학습 모델 없이 "보이는 가장 가까운 물건으로 걸어가
    /// 실제 규칙으로 집고, 앞에 내려놓고, 초기화"를 N회 반복하며 단계별 성공·실패 사유를 기록한다.
    ///
    /// 목적은 실행부(눈·다리·손)가 학습 이전에 안정적으로 도는지 증명하는 것이다.
    /// 집기·놓기 판정은 <see cref="NpcCarryAuthority"/>가 하고, 화면 반영은 실제 경기와 같은
    /// PlayerInteractor의 확정 함수로만 한다. CarryableItem의 집기 함수를 직접 부르지 않는다.
    /// </summary>
    public sealed class PickTrainingHarness : MonoBehaviour
    {
        private enum Stage
        {
            Reset,
            Observe,
            Approach,
            Reobserve,
            Hold,
            Release,
            Complete,
        }

        private sealed class RoundResult
        {
            public int Round;
            public bool Success;
            public Stage FailedAt;
            public string Reason;
            public string TargetId;
            public string KindKey;
            public float Seconds;
        }

        [SerializeField]
        private string npcId = "npc:1";

        [SerializeField, Min(1)]
        private int rounds = 20;

        [SerializeField]
        private bool runOnStart = true;

        [SerializeField, Min(0.1f)]
        private float approachTimeoutSeconds = 15f;

        [SerializeField, Min(0.5f)]
        private float stallSeconds = 2f;

        [SerializeField, Min(0f)]
        private float holdSeconds = 0.5f;

        [SerializeField, Min(0f)]
        private float settleSeconds = 1f;

        [SerializeField, Min(0f)]
        private float resetSettleSeconds = 0.5f;

        [SerializeField]
        private Vector3 eyeLocalPosition = new(0f, 1.5f, 0f);

        private readonly List<RoundResult> results = new();
        private readonly Dictionary<string, CarryableItem> itemsById = new(System.StringComparer.Ordinal);
        private readonly Dictionary<string, Pose> initialPoses = new(System.StringComparer.Ordinal);

        private BotMoveToTarget mover;
        private NetworkPlayerMotor motor;
        private PlayerInteractor interactor;
        private BotPerception perception;
        private NpcCarryAuthority authority;
        private Pose botStartPose;
        private LayerMask carryableMask;

        public bool IsRunning { get; private set; }

        private void Start()
        {
            carryableMask = LayerMask.GetMask("Carryable");
            if (runOnStart)
            {
                StartCoroutine(RunAll());
            }
        }

        public IEnumerator RunAll()
        {
            if (IsRunning)
            {
                yield break;
            }

            IsRunning = true;
            results.Clear();

            yield return WaitForBot();
            CollectItems();

            for (var round = 1; round <= rounds; round++)
            {
                yield return RunRound(round);
            }

            LogSummary();
            IsRunning = false;
        }

        private IEnumerator WaitForBot()
        {
            // 봇은 BotKccTestBootstrap이 Fusion으로 생성한다. 나타날 때까지 기다린다.
            while (mover == null)
            {
                mover = FindFirstObjectByType<BotMoveToTarget>();
                yield return null;
            }

            // 실행기가 SetTarget을 부르는 프레임 뒤에 넘겨받는다.
            yield return null;

            motor = mover.GetComponent<NetworkPlayerMotor>();
            interactor = mover.GetComponent<PlayerInteractor>();
            if (motor == null || interactor == null)
            {
                Debug.LogError("[Pick Harness] 봇에 NetworkPlayerMotor 또는 PlayerInteractor가 없습니다.", this);
                yield break;
            }

            // 실제 경기의 NPC 처리와 같게: 사람 입력 경로를 끄고 손 위치만 유지한다.
            interactor.enabled = false;
            interactor.RefreshHoldPoint();
            var placement = mover.GetComponent<ItemPlacementController>();
            if (placement != null)
            {
                placement.enabled = false;
            }

            perception = mover.GetComponent<BotPerception>();
            if (perception == null)
            {
                perception = mover.gameObject.AddComponent<BotPerception>();
                var eye = new GameObject("BotEye").transform;
                eye.SetParent(mover.transform, false);
                eye.localPosition = eyeLocalPosition;
                perception.ConfigureEye(eye);
                Debug.Log("[Pick Harness] 봇 프리팹에 시야가 없어 실행 중 추가했습니다. 프리팹에 넣는 것은 후속 과제.", this);
            }

            mover.ClearDestination();

            while (!motor.TryGetSimulationPose(out botStartPose))
            {
                yield return null;
            }

            Debug.Log($"[Pick Harness] 봇 확보. 시작 위치 {botStartPose.position}.", this);
        }

        private void CollectItems()
        {
            itemsById.Clear();
            initialPoses.Clear();
            var states = new List<WorldObjectState>();
            foreach (var item in FindObjectsByType<CarryableItem>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
            {
                var id = item.ObjectId;
                if (string.IsNullOrWhiteSpace(id) || itemsById.ContainsKey(id))
                {
                    Debug.LogWarning($"[Pick Harness] 물건 '{item.name}'의 ID가 비었거나 중복입니다. 제외합니다.", item);
                    continue;
                }

                var pose = new Pose(item.transform.position, item.transform.rotation);
                itemsById.Add(id, item);
                initialPoses.Add(id, pose);
                states.Add(new WorldObjectState(id, pose));
            }

            authority = new NpcCarryAuthority(states);
            Debug.Log($"[Pick Harness] 물건 {states.Count}개 등록.", this);
        }

        private IEnumerator RunRound(int round)
        {
            var result = new RoundResult { Round = round, FailedAt = Stage.Reset };
            results.Add(result);
            var startedAt = Time.time;

            // 1) 초기화: 손·물건·권위·봇 위치를 처음 상태로.
            yield return ResetEpisode();
            if (!CheckNoResidualState(out var residual))
            {
                Fail(result, Stage.Reset, residual);
                yield break;
            }

            // 2) 관측: 보이는 가장 가까운 물건의 사진을 찍는다.
            result.FailedAt = Stage.Observe;
            if (!perception.TryObserveClosest(carryableMask, out var sighting))
            {
                Fail(result, Stage.Observe, "no carryable visible from start pose");
                yield break;
            }

            result.TargetId = sighting.Handle.TargetId;
            result.KindKey = sighting.Observation.KindKey;
            if (!itemsById.TryGetValue(result.TargetId, out var item))
            {
                Fail(result, Stage.Observe, $"sighted id '{result.TargetId}' is not a registered item");
                yield break;
            }

            // 3) 접근: 관측 당시 위치로 걷는다. 물건 자체를 따라가지 않는다.
            result.FailedAt = Stage.Approach;
            mover.SetDestination(sighting.Handle.ObservedWorldPosition);
            var approachReason = string.Empty;
            yield return Approach(reason => approachReason = reason);
            mover.ClearDestination();
            if (!string.IsNullOrEmpty(approachReason))
            {
                Fail(result, Stage.Approach, approachReason);
                yield break;
            }

            // 4) 재관측: 도착 후 다시 보고, 그 자리에 그대로 있는지 확인한다.
            result.FailedAt = Stage.Reobserve;
            yield return new WaitForSeconds(0.2f);
            if (!perception.TryObserve(item.transform, out _))
            {
                Fail(result, Stage.Reobserve, "target not visible after arrival");
                yield break;
            }

            // 5) 집기: 권위 판정 → 확정 반영 → 실제 상태 확인.
            result.FailedAt = Stage.Hold;
            if (!motor.TryGetSimulationPose(out var botPose))
            {
                Fail(result, Stage.Hold, "bot pose unavailable");
                yield break;
            }

            if (!authority.TryHold(npcId, item.ObjectId, botPose, out var holdReason))
            {
                Fail(result, Stage.Hold, $"authority rejected hold: {holdReason}");
                yield break;
            }

            if (!interactor.ApplyConfirmedPickup(item) || !item.IsCarried || interactor.CarriedItem != item)
            {
                Fail(result, Stage.Hold, "confirmed pickup did not attach the item");
                yield break;
            }

            yield return new WaitForSeconds(holdSeconds);

            // 6) 놓기: 앞 1 m, 바닥 위 0.5 m에 내려놓는다.
            result.FailedAt = Stage.Release;
            if (!motor.TryGetSimulationPose(out botPose))
            {
                Fail(result, Stage.Release, "bot pose unavailable");
                yield break;
            }

            var releasePose = new Pose(
                botPose.position + botPose.rotation * Vector3.forward * 1.0f + Vector3.up * 0.5f,
                Quaternion.identity);
            if (!authority.TryRelease(npcId, botPose, releasePose, out var releaseReason))
            {
                Fail(result, Stage.Release, $"authority rejected release: {releaseReason}");
                yield break;
            }

            interactor.ApplyConfirmedRelease(item, releasePose, Vector3.zero);
            if (item.IsCarried || interactor.CarriedItem != null)
            {
                Fail(result, Stage.Release, "confirmed release did not detach the item");
                yield break;
            }

            yield return new WaitForSeconds(settleSeconds);
            authority.TrySetPose(item.ObjectId, new Pose(item.transform.position, item.transform.rotation));

            result.FailedAt = Stage.Complete;
            result.Success = true;
            result.Seconds = Time.time - startedAt;
            Debug.Log($"[Pick Harness] 라운드 {round} 성공: '{result.TargetId}' ({result.KindKey}) {result.Seconds:F2}s", this);
        }

        private IEnumerator ResetEpisode()
        {
            if (interactor.CarriedItem != null)
            {
                interactor.ForgetConfirmedItem(interactor.CarriedItem);
            }

            foreach (var pair in itemsById)
            {
                // 처음 위치로 되돌리고 움직이지 않게 고정한다. 놓을 때 다시 물리가 켜진다.
                pair.Value.OnSettled(initialPoses[pair.Key], keepDynamic: false);
            }

            authority.Reset();
            mover.ClearDestination();

            if (!motor.TryTeleport(botStartPose))
            {
                Debug.LogWarning("[Pick Harness] 봇 시작 위치 이동 요청이 거절되었습니다.", this);
            }

            yield return new WaitForSeconds(resetSettleSeconds);
        }

        private bool CheckNoResidualState(out string reason)
        {
            if (authority.HeldCount != 0)
            {
                reason = "authority still records a held object after reset";
                return false;
            }

            if (interactor.CarriedItem != null)
            {
                reason = "interactor still holds an item after reset";
                return false;
            }

            foreach (var pair in itemsById)
            {
                if (pair.Value.IsCarried)
                {
                    reason = $"item '{pair.Key}' still flagged as carried after reset";
                    return false;
                }
            }

            reason = null;
            return true;
        }

        private IEnumerator Approach(System.Action<string> onFail)
        {
            var deadline = Time.time + approachTimeoutSeconds;
            var lastProgressAt = Time.time;
            var lastPosition = botStartPose.position;

            while (Time.time < deadline)
            {
                if (!motor.TryGetSimulationPose(out var pose))
                {
                    yield return null;
                    continue;
                }

                if (mover.IsAtDestination(pose.position))
                {
                    yield break;
                }

                if ((pose.position - lastPosition).sqrMagnitude > 0.05f * 0.05f)
                {
                    lastPosition = pose.position;
                    lastProgressAt = Time.time;
                }
                else if (Time.time - lastProgressAt > stallSeconds)
                {
                    onFail(mover.HasCompletePath
                        ? $"no movement progress for {stallSeconds:F1}s"
                        : "no complete NavMesh path to the observed position");
                    yield break;
                }

                yield return null;
            }

            onFail($"approach timed out after {approachTimeoutSeconds:F0}s");
        }

        private void Fail(RoundResult result, Stage stage, string reason)
        {
            result.Success = false;
            result.FailedAt = stage;
            result.Reason = reason;
            Debug.LogWarning($"[Pick Harness] 라운드 {result.Round} 실패 @{stage}: {reason}", this);
        }

        private void LogSummary()
        {
            var successes = 0;
            var totalSeconds = 0f;
            var failures = new Dictionary<string, int>();
            foreach (var result in results)
            {
                if (result.Success)
                {
                    successes++;
                    totalSeconds += result.Seconds;
                    continue;
                }

                var key = $"{result.FailedAt}: {result.Reason}";
                failures[key] = failures.TryGetValue(key, out var count) ? count + 1 : 1;
            }

            var report = new StringBuilder();
            report.AppendLine("[Pick Harness] ===== 요약 =====");
            report.AppendLine($"라운드 {results.Count}, 성공 {successes}, 실패 {results.Count - successes}");
            if (successes > 0)
            {
                report.AppendLine($"성공 라운드 평균 시간 {totalSeconds / successes:F2}s");
            }

            foreach (var pair in failures)
            {
                report.AppendLine($"  실패 {pair.Value}회 — {pair.Key}");
            }

            Debug.Log(report.ToString(), this);
        }
    }
}

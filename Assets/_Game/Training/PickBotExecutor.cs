using System;
using System.Collections;
using System.Collections.Generic;
using Game.BotRuntime;
using Game.BotRuntime.Perception;
using Game.BotRuntime.Policy;
using Game.Client.Interactions;
using Game.Network.Match;
using Game.Network.Players;
using UnityEngine;

namespace Game.Training
{
    /// <summary>한 번의 Collect 동작이 끝났을 때 실행부가 알려 주는 결과.</summary>
    public readonly struct PickCollectResult
    {
        public PickCollectResult(bool success, string targetId, string kindKey, PickFailure failure, string reason, float seconds)
        {
            Success = success;
            TargetId = targetId ?? string.Empty;
            KindKey = kindKey ?? string.Empty;
            Failure = failure;
            Reason = reason ?? string.Empty;
            Seconds = seconds;
        }

        public bool Success { get; }
        public string TargetId { get; }
        public string KindKey { get; }
        public PickFailure Failure { get; }
        public string Reason { get; }
        public float Seconds { get; }
    }

    /// <summary>
    /// 봇의 눈·다리·손을 묶은 실행부. 정책(모델이든 규칙이든)이 고른 행동 하나를 실제로 수행한다.
    /// - Observe: 보이는 물건의 사진을 가까운 순으로 모은다. 2초 안에 본 물건은 기억으로 유지한다.
    /// - Collect: 사진 속 좌표로 걷고 → 다시 보고 → 심판에게 집기 요청 → 확정되면 실제 게임 함수로 손에 붙인다.
    /// - LookAround: 제자리에서 90도 돈다(Explore v1).
    /// 물건의 현재 위치를 조회해 따라가지 않는다. 실패 종류는 정책 피드백으로 남긴다.
    /// </summary>
    public sealed class PickBotExecutor : MonoBehaviour
    {
        [SerializeField, Min(1f)]
        private float collectTimeoutSeconds = 8f;

        [SerializeField, Min(0.5f)]
        private float stallSeconds = 2f;

        [SerializeField, Min(0f)]
        private float memorySeconds = 2f;

        [SerializeField]
        private float lookAroundDegrees = 90f;

        [SerializeField, Min(0.1f)]
        private float lookAroundSeconds = 0.4f;

        [SerializeField]
        private Vector3 eyeLocalPosition = new(0f, 1.5f, 0f);

        private readonly Dictionary<string, (BotSighting sighting, double seenAt)> memory =
            new(StringComparer.Ordinal);
        private readonly List<BotSighting> scratch = new();

        private BotMoveToTarget mover;
        private NetworkPlayerMotor motor;
        private PlayerInteractor interactor;
        private BotPerception perception;
        private NpcCarryAuthority authority;
        private IReadOnlyDictionary<string, CarryableItem> items;
        private string npcId = "npc:1";
        private Coroutine running;

        public bool IsBound => mover != null && motor != null && interactor != null && perception != null;
        public bool Busy { get; private set; }
        public PickOutcome LastOutcome { get; private set; }
        public PickFailure LastFailure { get; private set; }
        public string LastReason { get; private set; } = string.Empty;
        public bool HandOccupied => interactor != null && interactor.CarriedItem != null;
        public float MemorySeconds => memorySeconds;
        public PlayerInteractor Interactor => interactor;

        /// <summary>봇 몸의 루트. 배치 가림 검사에서 봇 자신을 무시할 때 쓴다.</summary>
        public Transform BotRoot => mover != null ? mover.transform : null;

        public event Action<PickCollectResult> CollectFinished;

        /// <summary>Fusion이 만든 봇 오브젝트에서 부품을 찾아 묶는다. 실제 경기의 NPC 처리와 같게 사람 입력을 끈다.</summary>
        public bool TryBind(GameObject bot)
        {
            if (bot == null)
            {
                return false;
            }

            mover = bot.GetComponent<BotMoveToTarget>();
            motor = bot.GetComponent<NetworkPlayerMotor>();
            interactor = bot.GetComponent<PlayerInteractor>();
            if (mover == null || motor == null || interactor == null)
            {
                Debug.LogError("[Pick Executor] 봇에 BotMoveToTarget / NetworkPlayerMotor / PlayerInteractor가 모두 있어야 합니다.", bot);
                return false;
            }

            interactor.enabled = false;
            interactor.RefreshHoldPoint();
            var placement = bot.GetComponent<ItemPlacementController>();
            if (placement != null)
            {
                placement.enabled = false;
            }

            perception = bot.GetComponent<BotPerception>();
            if (perception == null)
            {
                perception = bot.AddComponent<BotPerception>();
                var eye = new GameObject("BotEye").transform;
                eye.SetParent(bot.transform, false);
                eye.localPosition = eyeLocalPosition;
                perception.ConfigureEye(eye);
                Debug.Log("[Pick Executor] 봇 프리팹에 시야가 없어 실행 중 추가했습니다. 프리팹에 넣는 것은 후속 과제.", this);
            }

            mover.ClearDestination();

            // The bot's body moves on Fusion ticks. Eyes, brain and referee must read the same clock.
            if (motor.Runner != null)
            {
                var runner = motor.Runner;
                BotSimClock.Use(() => runner.SimulationTime);
                Debug.Log("[Pick Executor] clock switched to Fusion simulation time.", this);
            }
            else
            {
                Debug.LogWarning("[Pick Executor] no NetworkRunner; falling back to Unity time. Time rules will drift under time-scale.", this);
            }

            return true;
        }

        private void OnDestroy()
        {
            BotSimClock.Reset();
        }

        /// <summary>Current time as seen by the executor. Fusion simulation time while training.</summary>
        public double Now => BotSimClock.Now;

        /// <summary>Waits in simulation time. WaitForSeconds uses Unity time and is not used here.</summary>
        private IEnumerator WaitSim(double seconds)
        {
            var until = Now + seconds;
            while (Now < until)
            {
                yield return null;
            }
        }

        public void Configure(string id, NpcCarryAuthority carryAuthority, IReadOnlyDictionary<string, CarryableItem> registeredItems)
        {
            npcId = id;
            authority = carryAuthority;
            items = registeredItems;
        }

        public bool TryGetPose(out Pose pose)
        {
            if (motor != null && motor.TryGetSimulationPose(out pose))
            {
                return true;
            }

            pose = default;
            return false;
        }

        public bool TryTeleport(Pose pose) => motor != null && motor.TryTeleport(pose);

        /// <summary>에피소드 초기화. 진행 중인 동작을 끊고 기억·피드백·목적지를 지운다.</summary>
        public void ResetForEpisode()
        {
            if (running != null)
            {
                StopCoroutine(running);
                running = null;
            }

            Busy = false;
            LastOutcome = PickOutcome.None;
            LastFailure = PickFailure.None;
            LastReason = string.Empty;
            memory.Clear();

            if (mover != null)
            {
                mover.ClearDestination();
                mover.ClearIdleYaw();
            }

            if (interactor != null && interactor.CarriedItem != null)
            {
                interactor.ForgetConfirmedItem(interactor.CarriedItem);
            }
        }

        /// <summary>
        /// 지금 보이는 물건과 2초 안에 봤던 물건의 사진을 가까운 순으로 채운다.
        /// 봇이 이미 들고 있는 물건은 후보가 아니다.
        /// </summary>
        public void Observe(List<BotSighting> into)
        {
            into.Clear();
            if (!IsBound || items == null)
            {
                return;
            }

            var now = Now;
            foreach (var pair in items)
            {
                var item = pair.Value;
                if (item == null || item.IsCarried)
                {
                    memory.Remove(pair.Key);
                    continue;
                }

                if (perception.TryObserve(item.transform, out var sighting))
                {
                    memory[pair.Key] = (sighting, now);
                }
            }

            scratch.Clear();
            var expired = new List<string>();
            foreach (var pair in memory)
            {
                if (now - pair.Value.seenAt > memorySeconds)
                {
                    expired.Add(pair.Key);
                    continue;
                }

                scratch.Add(pair.Value.sighting);
            }

            foreach (var id in expired)
            {
                memory.Remove(id);
            }

            scratch.Sort((a, b) => a.Observation.NormalizedDistance.CompareTo(b.Observation.NormalizedDistance));
            var count = Mathf.Min(scratch.Count, PickObservationLayout.CandidateSlots);
            for (var i = 0; i < count; i++)
            {
                into.Add(scratch[i]);
            }
        }

        public bool TryBeginLookAround()
        {
            if (Busy || !IsBound)
            {
                return false;
            }

            running = StartCoroutine(LookAround());
            return true;
        }

        public bool TryBeginCollect(BotSighting sighting)
        {
            if (Busy || !IsBound || authority == null || items == null)
            {
                return false;
            }

            running = StartCoroutine(Collect(sighting));
            return true;
        }

        private IEnumerator LookAround()
        {
            Busy = true;
            if (TryGetPose(out var pose))
            {
                mover.SetIdleYaw(pose.rotation.eulerAngles.y + lookAroundDegrees);
            }

            yield return WaitSim(lookAroundSeconds);
            Busy = false;
            running = null;
        }

        private IEnumerator Collect(BotSighting sighting)
        {
            Busy = true;
            var startedAt = Now;
            var targetId = sighting.Handle.TargetId;
            var kindKey = sighting.Observation.KindKey;

            if (!items.TryGetValue(targetId, out var item) || item == null)
            {
                Finish(false, targetId, kindKey, PickFailure.TargetLost, "sighted id is not a registered item", startedAt);
                yield break;
            }

            // 1) 사진 속 좌표로 걷는다.
            mover.SetDestination(sighting.Handle.ObservedWorldPosition);
            var deadline = startedAt + collectTimeoutSeconds;
            var lastProgressAt = Now;
            TryGetPose(out var lastPose);
            var arrived = false;
            var failure = PickFailure.None;
            var reason = string.Empty;

            while (Now < deadline)
            {
                if (!TryGetPose(out var pose))
                {
                    yield return null;
                    continue;
                }

                if (mover.IsAtDestination(pose.position))
                {
                    arrived = true;
                    break;
                }

                if ((pose.position - lastPose.position).sqrMagnitude > 0.05f * 0.05f)
                {
                    lastPose = pose;
                    lastProgressAt = Now;
                }
                else if (Now - lastProgressAt > stallSeconds)
                {
                    failure = mover.HasCompletePath ? PickFailure.Stalled : PickFailure.NoPath;
                    reason = mover.HasCompletePath
                        ? $"no movement progress for {stallSeconds:F1}s"
                        : "no complete NavMesh path to the observed position";
                    break;
                }

                yield return null;
            }

            mover.ClearDestination();
            if (!arrived)
            {
                if (failure == PickFailure.None)
                {
                    failure = PickFailure.Stalled;
                    reason = $"approach timed out after {collectTimeoutSeconds:F0}s";
                }

                Finish(false, targetId, kindKey, failure, reason, startedAt);
                yield break;
            }

            // 2) 도착 후 다시 본다. 그 사이 사라졌으면 실패.
            yield return WaitSim(0.1);
            if (item.IsCarried || !perception.TryObserve(item.transform, out _))
            {
                Finish(false, targetId, kindKey, PickFailure.TargetLost, "target not visible after arrival", startedAt);
                yield break;
            }

            // 3) 심판 → 확정 반영 → 실제 상태 확인.
            if (!TryGetPose(out var botPose))
            {
                Finish(false, targetId, kindKey, PickFailure.Rejected, "bot pose unavailable", startedAt);
                yield break;
            }

            if (!authority.TryHold(npcId, item.ObjectId, botPose, out var holdReason))
            {
                Finish(false, targetId, kindKey, PickFailure.Rejected, $"authority rejected hold: {holdReason}", startedAt);
                yield break;
            }

            if (!interactor.ApplyConfirmedPickup(item) || !item.IsCarried || interactor.CarriedItem != item)
            {
                Finish(false, targetId, kindKey, PickFailure.Rejected, "confirmed pickup did not attach the item", startedAt);
                yield break;
            }

            Finish(true, targetId, kindKey, PickFailure.None, string.Empty, startedAt);
        }

        private void Finish(bool success, string targetId, string kindKey, PickFailure failure, string reason, double startedAt)
        {
            Busy = false;
            running = null;
            LastOutcome = success ? PickOutcome.Success : PickOutcome.Failure;
            LastFailure = failure;
            LastReason = reason;
            CollectFinished?.Invoke(new PickCollectResult(success, targetId, kindKey, failure, reason, (float)(Now - startedAt)));
        }
    }
}

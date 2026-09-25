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
using UnityEngine.AI;

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

        [SerializeField, Range(0f, 90f)]
        [Tooltip("도착 후 물건이 정면에서 이 각도 이상 벗어나 있으면 그쪽으로 몸을 돌린 뒤 다시 본다.")]
        private float faceTargetAngleDegrees = 20f;

        [SerializeField, Min(0f)]
        private float faceTargetSeconds = 0.35f;

        /// <summary>도착 후 물건 쪽으로 몸을 돌린 횟수.</summary>
        public int FaceTurns { get; private set; }

        [SerializeField, Min(0f)]
        [Tooltip("집기에 실패한 물건을 이 시간(시뮬 초) 동안 후보에서 뺀다. 0이면 끔. 모델 선택을 바꾸지 않고 관측에서만 지운다.")]
        private float failedTargetCooldownSeconds = 10f;

        private readonly Dictionary<string, double> failedUntil = new(StringComparer.Ordinal);

        // Optional slot priority. When more props are visible than there are slots, the environment can ask
        // for relevant ones first (the mansion puts goal matches first). Null keeps pure nearest-first, which
        // is what the arena was trained and evaluated with.
        private Func<BotSighting, bool> preferCandidate;

        public void SetCandidatePriority(Func<BotSighting, bool> prefer) => preferCandidate = prefer;

        // Optional reachability filter. A prop is offered to the policy only if the nearest walkable point has
        // a complete path from the bot and lies within the interaction distance of the prop -- otherwise the
        // collect would end as NoPath or as an authority rejection. Results are cached per prop.
        private bool filterUnreachable;
        private float reachCacheSeconds = 10f;
        private readonly Dictionary<string, (Vector3 itemPos, double at, bool reachable)> reachCache =
            new(StringComparer.Ordinal);
        private NavMeshPath reachPath;

        /// <summary>Props hidden from the policy because they are unreachable (per observe, cumulative).</summary>
        public int UnreachableSuppressions { get; private set; }

        public void SetReachabilityFilter(bool enabled, float cacheSeconds = 10f)
        {
            filterUnreachable = enabled;
            reachCacheSeconds = cacheSeconds;
            reachCache.Clear();
        }

        private bool IsReachable(string id, Vector3 itemPosition, Vector3 botPosition, double now)
        {
            if (reachCache.TryGetValue(id, out var cached) &&
                now - cached.at < reachCacheSeconds &&
                (cached.itemPos - itemPosition).sqrMagnitude < 0.25f * 0.25f)
            {
                return cached.reachable;
            }

            reachPath ??= new NavMeshPath();
            var reachable =
                NavMesh.SamplePosition(itemPosition, out var itemHit, 2f, NavMesh.AllAreas) &&
                (itemHit.position - itemPosition).sqrMagnitude <=
                    InteractionAuthorityRules.DefaultInteractionDistance * InteractionAuthorityRules.DefaultInteractionDistance &&
                NavMesh.SamplePosition(botPosition, out var botHit, 2f, NavMesh.AllAreas) &&
                NavMesh.CalculatePath(botHit.position, itemHit.position, NavMesh.AllAreas, reachPath) &&
                reachPath.status == NavMeshPathStatus.PathComplete;

            reachCache[id] = (itemPosition, now, reachable);
            return reachable;
        }

        /// <summary>실패 쿨다운 때문에 후보에서 빠진 횟수(관측 호출 기준 누적).</summary>
        public int CooldownSuppressions { get; private set; }

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

        // Cost of one Observe call (all registered items ray-checked). Read and reset by the mansion sandbox
        // to see whether a real map with hundreds of props stays cheap enough per decision.
        private readonly System.Diagnostics.Stopwatch observeWatch = new();
        public int ObserveCalls { get; private set; }
        public double ObserveMillisecondsTotal { get; private set; }
        public double ObserveMillisecondsMax { get; private set; }
        public int VisibleTotal { get; private set; }

        /// <summary>학습장처럼 매 에피소드 물건이 새 자리에 놓이는 환경이 부른다.</summary>
        public void ClearFailedTargets() => failedUntil.Clear();

        public void ResetObserveStats()
        {
            CooldownSuppressions = 0;
            UnreachableSuppressions = 0;
            FaceTurns = 0;
            ObserveCalls = 0;
            ObserveMillisecondsTotal = 0;
            ObserveMillisecondsMax = 0;
            VisibleTotal = 0;
        }

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

        /// <summary>
        /// Diagnostic only: what is directly in front of the bot when it stalls.
        /// </summary>
        private string DescribeBlocker(Pose pose)
        {
            var forward = pose.rotation * Vector3.forward;
            var origin = pose.position + Vector3.up * 0.6f;
            var hits = Physics.SphereCastAll(origin, 0.25f, forward, 0.8f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
            var bestDistance = float.PositiveInfinity;
            string best = null;
            foreach (var hit in hits)
            {
                var t = hit.collider.transform;
                if (BotRoot != null && (t == BotRoot || t.IsChildOf(BotRoot)))
                {
                    continue;
                }

                if (hit.distance < bestDistance)
                {
                    bestDistance = hit.distance;
                    var body = hit.collider.attachedRigidbody != null ? hit.collider.attachedRigidbody.transform : t;
                    best = "'" + StripSuffix(body.name) + "' [" + LayerMask.LayerToName(hit.collider.gameObject.layer) + "]";
                }
            }

            return best ?? "nothing in front (floor step or NavMesh edge?)";
        }

        private static string StripSuffix(string name)
        {
            var cut = name.IndexOf(" (", StringComparison.Ordinal);
            return cut > 0 ? name.Substring(0, cut) : name;
        }

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
            // failedUntil survives episode resets on purpose: an unreachable prop stays unreachable.

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
            observeWatch.Restart();
            ObserveInternal(into);
            observeWatch.Stop();
            var ms = observeWatch.Elapsed.TotalMilliseconds;
            ObserveCalls++;
            ObserveMillisecondsTotal += ms;
            ObserveMillisecondsMax = Math.Max(ObserveMillisecondsMax, ms);
            VisibleTotal += scratch.Count; // all seen or remembered, before the 3-slot cap
        }

        private void ObserveInternal(List<BotSighting> into)
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

                if (failedUntil.TryGetValue(pair.Key, out var until))
                {
                    if (now < until)
                    {
                        memory.Remove(pair.Key);
                        CooldownSuppressions++;
                        continue;
                    }

                    failedUntil.Remove(pair.Key);
                }

                if (perception.TryObserve(item.transform, out var sighting))
                {
                    if (filterUnreachable && TryGetPose(out var botPose) &&
                        !IsReachable(pair.Key, sighting.Handle.ObservedWorldPosition, botPose.position, now))
                    {
                        memory.Remove(pair.Key);
                        UnreachableSuppressions++;
                        continue;
                    }

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

            scratch.Sort((a, b) =>
            {
                if (preferCandidate != null)
                {
                    var pa = preferCandidate(a);
                    var pb = preferCandidate(b);
                    if (pa != pb)
                    {
                        return pa ? -1 : 1;
                    }
                }

                return a.Observation.NormalizedDistance.CompareTo(b.Observation.NormalizedDistance);
            });
            var count = Mathf.Min(scratch.Count, PickObservationLayout.CandidateSlots);
            for (var i = 0; i < count; i++)
            {
                into.Add(scratch[i]);
            }
        }

        /// <summary>
        /// Walk to a floor point (carrying whatever is in hand). Reports success, the failure kind and a reason.
        /// Used by the mansion sandbox to carry a prop to the spot the placement policy chose.
        /// </summary>
        public bool TryBeginWalkTo(Vector3 destination, float timeoutSeconds, Action<bool, PickFailure, string> onDone)
        {
            if (Busy || !IsBound)
            {
                return false;
            }

            running = StartCoroutine(WalkTo(destination, timeoutSeconds, onDone));
            return true;
        }

        private IEnumerator WalkTo(Vector3 destination, float timeoutSeconds, Action<bool, PickFailure, string> onDone)
        {
            Busy = true;
            mover.ClearIdleYaw();
            mover.SetDestination(destination);
            var deadline = Now + timeoutSeconds;
            var lastProgressAt = Now;
            TryGetPose(out var lastPose);
            var ok = false;
            var failure = PickFailure.Stalled;
            var reason = $"walk timed out after {timeoutSeconds:F0}s";

            while (Now < deadline)
            {
                if (!TryGetPose(out var pose))
                {
                    yield return null;
                    continue;
                }

                if (mover.IsAtDestination(pose.position))
                {
                    ok = true;
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
                        ? $"no movement progress for {stallSeconds:F1}s; blocked by {DescribeBlocker(pose)}"
                        : "no complete NavMesh path to the chosen spot";
                    break;
                }

                yield return null;
            }

            mover.ClearDestination();
            Busy = false;
            running = null;
            onDone?.Invoke(ok, ok ? PickFailure.None : failure, ok ? string.Empty : reason);
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
                        ? $"no movement progress for {stallSeconds:F1}s; blocked by {DescribeBlocker(pose)}"
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

            // 2) 도착 후 다시 본다. 물건이 정면에서 크게 벗어나 있으면(식탁 옆에 바짝 붙은 경우 등) 먼저 그쪽으로
            //    몸을 돌린다. 사진 속 좌표를 향해 도는 것이라 물건을 추적하지 않는다. 학습장에서는 거의 정면으로
            //    도착하므로 이 분기에 들어가지 않고, 평가 조건이 바뀌지 않는다.
            if (TryGetPose(out var arrivedPose))
            {
                var toItem = sighting.Handle.ObservedWorldPosition - arrivedPose.position;
                toItem.y = 0f;
                var facing = arrivedPose.rotation * Vector3.forward;
                facing.y = 0f;
                if (toItem.sqrMagnitude > 0.0001f && Vector3.Angle(facing, toItem) > faceTargetAngleDegrees)
                {
                    mover.SetIdleYaw(Mathf.Atan2(toItem.x, toItem.z) * Mathf.Rad2Deg);
                    FaceTurns++;
                    yield return WaitSim(faceTargetSeconds);
                }
            }

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
            if (!success && failedTargetCooldownSeconds > 0f && !string.IsNullOrEmpty(targetId))
            {
                failedUntil[targetId] = Now + failedTargetCooldownSeconds;
            }

            CollectFinished?.Invoke(new PickCollectResult(success, targetId, kindKey, failure, reason, (float)(Now - startedAt)));
        }
    }
}

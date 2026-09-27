#if UNITY_EDITOR
using System.Collections.Generic;
using Game.Client.Interactions;
using Game.Core.Players;
using Game.Network.Match;
using Game.Network.Players;
using Game.Network.Session;
using Game.Training.HideSeek;
using Unity.InferenceEngine;
using UnityEditor;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;
using Random = UnityEngine.Random;
using DeviceType = Unity.InferenceEngine.DeviceType;

namespace Game.Training.Thief
{
    /// <summary>
    /// Thief NPC in a real match (docs/planning/thief-npc-v2.md 10), editor host only: in the searching phase the host
    /// spawns one match NPC and runs the thief from the simulator on the real state (<see cref="ThiefMatch.BeginMirror"/>):
    /// players and their items are copied in, the NPC avatar walks where the thief's macros say, and pickups and
    /// placements go through the real NPC carry authority. Brain: the trained ONNX models, or the rule thief.
    /// Tools > AI > Thief NPC turns it on. Clients need nothing but the code; the bank, NavMesh and models are local.
    /// </summary>
    public sealed class ThiefNpcDirector : MonoBehaviour
    {
        private const string EnabledKey = "ThiefNpc.InMatch";
        private const string BrainKey = "ThiefNpc.UseModel";
        private const string LocalDir = "Assets/_Game/Content/Training/Local/";
        // Shared copies (tracked) so any development server can run the NPC; the local training files as fallback.
        private const string SharedDir = "Assets/_Game/Content/Training/ThiefNpc/";
        private static readonly string[] BankPaths = { SharedDir + "Mansion_HidingSpotBank.asset", LocalDir + "Mansion_HidingSpotBank.asset" };
        private static readonly string[] NavMeshPaths = { SharedDir + "Mansion_ThiefNpc_NavMesh.asset", LocalDir + "Mansion_Thief_NavMesh.asset" };

        // Search brain (thief-npc-v2.md 11.4): the v1.1 hide-seek seeker, transferred; hiding stays the rule brain.
        private const string TransferSeekPath = SharedDir + "SeekSelect-v11d-3200028.onnx";
        public const string ModelDir = LocalDir + "ThiefCheckpoints/ingame/";
        private const float Step = 0.05f;

        private MatchStarter starter;
        private NetworkRunnerService runnerService;
        private ThiefArena arena;
        private ThiefMatch match;
        private PlayerAvatar npc;
        private BotMoveToTarget legs;
        private Transform legsTarget;
        private NavMeshDataInstance navMesh;
        private string npcId;
        private readonly string[] itemIds = new string[ThiefMatch.Players];
        private readonly float[] pivotAboveBottom = new float[ThiefMatch.Players];
        private readonly bool[] pivotKnown = new bool[ThiefMatch.Players];
        private readonly Dictionary<Collider, bool> ignoreCache = new();
        private readonly HashSet<Collider> itemColliders = new();
        private float accumulator;
        private bool forwarding;
        private Vector3 forwarded;
        private Vector3 lastProgressPos;
        private float lastProgressTime;
        private float nextLookup;
        private bool finishedThisMatch;
        private Worker seekWorker, hideWorker;
        private float[] seekObs, hideObs, seekMask, hideMask;
        private string brainLabel = "rule";

        // Player punches on the NPC (thief-npc-v2.md 12): the game's combat only knows player indices, so the host
        // counts them here with the same numbers (hit 0.35 s after the punch, sphere 0.8 m ahead r 0.7 m, 3 hits
        // -> 2 s stun and the held prop drops).
        private readonly Dictionary<NetworkPlayerMotor, int> lastAttack = new();
        private readonly List<(NetworkPlayerMotor who, float at)> pendingHits = new();
        private int npcHits;
        private float npcStunnedUntil = -1f;

        // 1 on, 0 off, unset: on for a development server (Game > Network > Development Server) only.
        [MenuItem("Tools/AI/Thief NPC/Enable In Match (editor host)")]
        private static void Enable() { EditorPrefs.SetInt(EnabledKey, 1); Debug.Log("[Thief NPC] enabled for the next match (editor host)."); }

        [MenuItem("Tools/AI/Thief NPC/Disable")]
        private static void Disable() { EditorPrefs.SetInt(EnabledKey, 0); Debug.Log("[Thief NPC] disabled."); }

        private static bool IsEnabled()
        {
            if (EditorPrefs.HasKey(EnabledKey))
            {
                try { return EditorPrefs.GetInt(EnabledKey, 0) == 1; }
                catch { return EditorPrefs.GetBool(EnabledKey, false); }
            }

            return EditorDevelopmentSession.IsServer;
        }

        private static T LoadFirst<T>(string[] paths) where T : Object
        {
            foreach (var path in paths)
            {
                var asset = AssetDatabase.LoadAssetAtPath<T>(path);
                if (asset != null) return asset;
            }

            return null;
        }

        [MenuItem("Tools/AI/Thief NPC/Brain: Trained Model")]
        private static void UseModel() { EditorPrefs.SetBool(BrainKey, true); Debug.Log("[Thief NPC] brain = trained model (" + ModelDir + ")."); }

        [MenuItem("Tools/AI/Thief NPC/Brain: Rule Thief")]
        private static void UseRule() { EditorPrefs.SetBool(BrainKey, false); Debug.Log("[Thief NPC] brain = rule thief."); }

        [MenuItem("Tools/AI/Thief NPC/Copy Latest Checkpoints")]
        private static void CopyLatest()
        {
            var root = System.IO.Path.GetFullPath(System.IO.Path.Combine(Application.dataPath, ".."));
            var results = System.IO.Path.Combine(root, "results");
            string newestRun = null;
            var newest = System.DateTime.MinValue;
            foreach (var run in System.IO.Directory.GetDirectories(results, "thief-*"))
            {
                var seekDir = System.IO.Path.Combine(run, "ThiefSeek");
                if (!System.IO.Directory.Exists(seekDir) || System.IO.Directory.GetFiles(seekDir, "ThiefSeek-*.onnx").Length == 0) continue;
                var t = System.IO.Directory.GetLastWriteTime(seekDir);
                if (t > newest) { newest = t; newestRun = run; }
            }

            if (newestRun == null) { Debug.LogError("[Thief NPC] no thief checkpoints under results/."); return; }
            string Latest(string behavior)
            {
                string best = null; var bestStep = -1L;
                foreach (var f in System.IO.Directory.GetFiles(System.IO.Path.Combine(newestRun, behavior), behavior + "-*.onnx"))
                {
                    var name = System.IO.Path.GetFileNameWithoutExtension(f);
                    if (long.TryParse(name.Substring(behavior.Length + 1), out var step) && step > bestStep) { bestStep = step; best = f; }
                }
                return best;
            }

            var seek = Latest("ThiefSeek");
            var hide = Latest("ThiefHide");
            if (seek == null || hide == null) { Debug.LogError($"[Thief NPC] {newestRun} has no ThiefSeek/ThiefHide checkpoint pair yet."); return; }
            var dir = System.IO.Path.Combine(root, ModelDir);
            System.IO.Directory.CreateDirectory(dir);
            System.IO.File.Copy(seek, System.IO.Path.Combine(dir, "ThiefSeek.onnx"), true);
            System.IO.File.Copy(hide, System.IO.Path.Combine(dir, "ThiefHide.onnx"), true);
            var label = $"{System.IO.Path.GetFileName(newestRun)} {System.IO.Path.GetFileNameWithoutExtension(seek)} {System.IO.Path.GetFileNameWithoutExtension(hide)}";
            System.IO.File.WriteAllText(System.IO.Path.Combine(dir, "source.txt"), label);
            AssetDatabase.Refresh();
            Debug.Log("[Thief NPC] copied " + label);
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Boot()
        {
            if (!IsEnabled()) return;
            var go = new GameObject("ThiefNpcDirector");
            DontDestroyOnLoad(go);
            go.AddComponent<ThiefNpcDirector>();
            Debug.Log($"[Thief NPC] director active (development server {EditorDevelopmentSession.IsServer}); waits for the mansion searching phase.");
        }

        private void Update()
        {
            if (match == null)
            {
                TryBegin();
                return;
            }

            if (starter == null || !starter.IsNpcSearchOpen || npc == null)
            {
                End("searching phase is over");
                return;
            }

            Tick(Time.deltaTime);
        }

        // ------------------------------------------------------------------ setup

        private void TryBegin()
        {
            if (Time.unscaledTime < nextLookup) return;
            nextLookup = Time.unscaledTime + 1f;
            if (starter == null) starter = FindFirstObjectByType<MatchStarter>();
            if (starter == null) Wait("no match yet");
            else if (!starter.IsNpcSearchOpen) Wait("match found, searching phase not open yet");
            if (starter == null || !starter.IsNpcSearchOpen)
            {
                finishedThisMatch = false;
                return;
            }

            if (finishedThisMatch) return;
            if (!MansionLoaded(out var scenes))
            {
                Debug.LogWarning($"[Thief NPC] no mansion scene loaded ({scenes}); the thief NPC runs on the mansion only.");
                finishedThisMatch = true;
                return;
            }

            runnerService ??= FindFirstObjectByType<Game.Bootstrap.ProjectLifetimeScope>()?.Container.Resolve(typeof(NetworkRunnerService)) as NetworkRunnerService;
            if (runnerService == null || !runnerService.IsServer)
            {
                Wait(runnerService == null ? "network service not found" : "this editor is not the server");
                return;
            }

            if (arena == null)
            {
                var bank = LoadFirst<HidingSpotBank>(BankPaths);
                var data = LoadFirst<NavMeshData>(NavMeshPaths);
                if (bank == null || data == null)
                {
                    Debug.LogError($"[Thief NPC] missing hiding spot bank or NavMesh under {SharedDir}.");
                    finishedThisMatch = true;
                    return;
                }

                if (NavMesh.CalculateTriangulation().vertices.Length == 0) navMesh = NavMesh.AddNavMeshData(data);
                var holder = new GameObject("ThiefNpcArena");
                holder.SetActive(false);
                holder.transform.SetParent(transform);
                arena = holder.AddComponent<ThiefArena>();
                arena.ConfigureForGame(bank);
                holder.SetActive(true); // Start builds the world next frame
                return;
            }

            if (arena.World == null) return;
            LoadBrain();
            match = new ThiefMatch(arena, arena.World, 0);
            if (!SpawnNpc(out var feet, out var yaw))
            {
                match = null;
                finishedThisMatch = true;
                return;
            }

            for (var p = 0; p < ThiefMatch.Players; p++)
            {
                pivotKnown[p] = false;
                itemIds[p] = null;
            }

            itemColliders.Clear();
            ignoreCache.Clear();
            HideSeekVision.IgnoreCollider = Ignored;
            match.MirrorPickup = Pickup;
            match.MirrorPlace = Place;
            match.MirrorDrop = DropHere;
            match.BeginMirror(Random.Range(1, int.MaxValue), feet, yaw);
            lastAttack.Clear();
            pendingHits.Clear();
            npcHits = 0;
            npcStunnedUntil = -1f;
            accumulator = 0f;
            forwarding = false;
            lastProgressPos = feet;
            lastProgressTime = Time.time;
            Debug.Log($"[Thief NPC] {npcId} spawned at {feet}, brain {brainLabel}, players {starter.NpcPlayerCount} (first {ThiefMatch.Players} are watched), waypoints {arena.World.Waypoints.Count}.");
        }

        private string lastWait;

        private void Wait(string why)
        {
            if (why == lastWait) return;
            lastWait = why;
            Debug.Log("[Thief NPC] waiting: " + why);
        }

        private static bool MansionLoaded(out string names)
        {
            names = "";
            var found = false;
            for (var i = 0; i < SceneManager.sceneCount; i++)
            {
                var scene = SceneManager.GetSceneAt(i);
                names += (i > 0 ? ", " : "") + scene.name;
                if (scene.isLoaded && scene.name == "Mansion") found = true;
            }

            return found;
        }

        private bool SpawnNpc(out Vector3 feet, out float yaw)
        {
            feet = default;
            yaw = Random.Range(0f, 360f);
            var wp = arena.World.Waypoints;
            var best = wp[Random.Range(0, wp.Count)];
            var bestScore = -1f;
            for (var tries = 0; tries < 40; tries++)
            {
                var c = wp[Random.Range(0, wp.Count)];
                var nearest = 999f;
                for (var i = 0; i < starter.NpcPlayerCount; i++)
                {
                    if (starter.TryGetNpcPlayerView(i, out var pose, out _)) nearest = Mathf.Min(nearest, ThiefWorld.Flat(pose.position - c));
                }

                if (nearest > bestScore)
                {
                    bestScore = nearest;
                    best = c;
                }

                if (nearest >= 8f) break;
            }

            feet = best;
            legsTarget ??= new GameObject("ThiefNpcTarget").transform;
            legsTarget.SetParent(transform);
            legsTarget.position = best;
            var profile = new MatchNpcBotProfile(1, "도둑");
            if (!runnerService.TrySpawnMatchNpc(profile, new Pose(best, Quaternion.Euler(0f, yaw, 0f)), legsTarget, out npc) || npc == null)
            {
                Debug.LogError("[Thief NPC] spawn failed (bot prefab, runner or searching phase).");
                return false;
            }

            npcId = profile.NpcId;
            legs = npc.GetComponent<BotMoveToTarget>();
            legs.ClearDestination();
            var field = typeof(BotMoveToTarget).GetField("stoppingDistance", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            field?.SetValue(legs, 0.25f);
            return true;
        }

        private void LoadBrain()
        {
            DisposeWorkers();
            brainLabel = "rule";
            seekObs = new float[ThiefArena.SeekObservationSize];
            hideObs = new float[ThiefArena.HideObservationSize];
            seekMask = new float[ThiefMatch.SeekActionCount];
            hideMask = new float[ThiefMatch.HideActionCount];
            if (!EditorPrefs.GetBool(BrainKey, true)) return;
            var transfer = AssetDatabase.LoadAssetAtPath<ModelAsset>(TransferSeekPath);
            if (transfer != null)
            {
                arena.SetTransferSeekModel(transfer);
                brainLabel = "trained search (v1.1 seeker) + rule hiding";
                return;
            }

            var seek = AssetDatabase.LoadAssetAtPath<ModelAsset>(ModelDir + "ThiefSeek.onnx");
            var hide = AssetDatabase.LoadAssetAtPath<ModelAsset>(ModelDir + "ThiefHide.onnx");
            if (seek == null || hide == null)
            {
                Debug.LogWarning($"[Thief NPC] no trained models in {ModelDir} (Tools > AI > Thief NPC > Copy Latest Checkpoints); using the rule thief.");
                return;
            }

            seekWorker = new Worker(ModelLoader.Load(seek), DeviceType.CPU);
            hideWorker = new Worker(ModelLoader.Load(hide), DeviceType.CPU);
            brainLabel = "trained model " + AssetDatabase.LoadAssetAtPath<TextAsset>(ModelDir + "source.txt")?.text.Trim();
        }

        // ------------------------------------------------------------------ per frame

        private void Tick(float dt)
        {
            var feet = npc.transform.position;
            MirrorWorld();
            ResolvePlayerPunches(feet);
            var stunned = Time.time < npcStunnedUntil;
            legs.Frozen = stunned;
            if (stunned)
            {
                legs.PostureButtons = PlayerInputButtons.None;
                match.StepMirror(dt, feet, false, true);
                return;
            }
            if (decoy != null)
            {
                StepDecoy(feet);
                return;
            }

            // Forward the thief's path end to the real legs; judge arrival on the real body.
            var body = match.Thief.Body;
            if (body.Moving)
            {
                // Going for a prop: the simulator's approach point can sit just outside the real 2 m pickup check
                // (box face vs item pivot), so the real legs walk right up to where the prop was seen.
                var t = match.Thief;
                var end = t.Macro == ThiefMatch.Macro.ToProp && t.Target >= 0 ? match.Mind.PropSeenAt[t.Target] : body.PathEnd;
                if (!forwarding || (end - forwarded).sqrMagnitude > 0.01f)
                {
                    legs.SetDestination(end);
                    forwarding = true;
                    forwarded = end;
                    lastProgressPos = feet;
                    lastProgressTime = Time.time;
                }
            }
            else if (forwarding)
            {
                legs.ClearDestination();
                forwarding = false;
            }

            if ((feet - lastProgressPos).sqrMagnitude > 0.0025f)
            {
                lastProgressPos = feet;
                lastProgressTime = Time.time;
            }

            var stalled = forwarding && Time.time - lastProgressTime > 2f;
            var stillMoving = forwarding && !stalled && !legs.IsAtDestination(feet);
            if (stalled) Debug.Log($"[Thief NPC] legs stalled {ThiefWorld.Flat(forwarded - feet):F1} m short; macro {match.Thief.Macro} ends there.");

            accumulator += dt;
            while (accumulator >= Step)
            {
                accumulator -= Step;
                match.StepMirror(Step, feet, stillMoving, false);
                if (match.Thief.Macro == ThiefMatch.Macro.Await) Decide();
            }

            if (!match.Thief.Body.Moving) legs.SetIdleYaw(match.Thief.Body.Yaw);
            var shadow = match.Thief.Body;
            legs.PostureButtons = shadow.Prone ? PlayerInputButtons.Prone : shadow.Crouched ? PlayerInputButtons.Crouch : PlayerInputButtons.None;
        }

        private void ResolvePlayerPunches(Vector3 npcFeet)
        {
            foreach (var avatar in runnerService.PlayerAvatars)
            {
                if (avatar == null || avatar.IsMatchNpc) continue;
                var motor = avatar.GetComponent<NetworkPlayerMotor>();
                if (motor == null) continue;
                if (!lastAttack.TryGetValue(motor, out var seen))
                {
                    lastAttack[motor] = motor.AttackSequence;
                    continue;
                }

                if (motor.AttackSequence == seen) continue;
                lastAttack[motor] = motor.AttackSequence;
                pendingHits.Add((motor, Time.time + ThiefMatch.AttackHitDelay));
            }

            for (var i = pendingHits.Count - 1; i >= 0; i--)
            {
                var (who, at) = pendingHits[i];
                if (Time.time < at) continue;
                pendingHits.RemoveAt(i);
                if (who == null || Time.time < npcStunnedUntil) continue;
                var t = who.transform;
                var centre = t.position + Vector3.up + t.forward * 0.8f;
                var npcCentre = npcFeet + Vector3.up;
                if ((centre - npcCentre).sqrMagnitude > (0.7f + 0.35f) * (0.7f + 0.35f)) continue;
                npcHits++;
                Debug.Log($"[Thief NPC] hit by a player ({npcHits}/{ThiefMatch.HitsToStun}).");
                if (npcHits < ThiefMatch.HitsToStun) continue;
                npcHits = 0;
                npcStunnedUntil = Time.time + ThiefMatch.StunSeconds;
                legs.ClearDestination();
                forwarding = false;
                if (decoy != null && decoy.Holding) starter.TryDropHeldObjectForMatchNpc(npcId, new Pose(npcFeet + Vector3.up * 0.2f, Quaternion.identity), out _);
                decoy = null;
                var dropped = starter.TryGetMatchNpcHeldObjectId(npcId, out _) && DropHere();
                Debug.Log($"[Thief NPC] stunned for {ThiefMatch.StunSeconds:F0} s{(dropped ? ", dropped what it held" : "")}.");
            }
        }

        private void MirrorWorld()
        {
            for (var i = 0; i < ThiefMatch.Players; i++)
            {
                var pose = default(Pose);
                var holds = false;
                var present = i < starter.NpcPlayerCount && starter.TryGetNpcPlayerView(i, out pose, out holds);
                match.MirrorPlayer(i, present, pose.position, pose.rotation.eulerAngles.y, false, holds);

                if (!present || !starter.TryGetNpcAssignedItem(i, out var itemId, out var itemPose, out var holderPlayer, out var holderNpc))
                {
                    match.MirrorProp(i, false, default, -1);
                    continue;
                }

                if (itemIds[i] != itemId)
                {
                    itemIds[i] = itemId;
                    RegisterItemColliders(itemId);
                }

                var heldBy = holderNpc == npcId ? ThiefMatch.Players : holderPlayer >= 0 && holderPlayer < ThiefMatch.Players ? holderPlayer : -1;
                if (heldBy < 0 && holderPlayer < 0 && holderNpc == null && !pivotKnown[i]) MeasurePivot(i, itemId, itemPose);
                var bottom = itemPose.position - Vector3.up * (pivotKnown[i] ? pivotAboveBottom[i] : 0f);
                match.MirrorProp(i, true, bottom, heldBy);
            }
        }

        private void Decide()
        {
            if (match.TryReflexGoForProp()) return;
            if (!match.ThiefCarrying && TryStartDecoy()) return;
            int action;
            if (match.ThiefCarrying)
            {
                action = hideWorker != null ? Infer(hideWorker, hideObs, hideMask, ThiefMatch.HideActionCount, arena.WriteHideObservation, match.HideAllowed) : arena.RuleHideAction(match);
            }
            else if (arena.HasTransferSeek)
            {
                action = arena.TransferSeekAction(match);
            }
            else
            {
                action = seekWorker != null ? Infer(seekWorker, seekObs, seekMask, ThiefMatch.SeekActionCount, arena.WriteSeekObservation, match.SeekAllowed) : arena.RuleSeekAction(match);
            }

            match.ApplyThiefAction(action);
        }

        private int Infer(Worker worker, float[] obs, float[] mask, int count, System.Action<ThiefMatch, float[]> write, System.Func<int, bool> allowed)
        {
            write(match, obs);
            for (var a = 0; a < count; a++) mask[a] = allowed(a) ? 1f : 0f;
            using var obsTensor = new Tensor<float>(new TensorShape(1, obs.Length), obs);
            using var maskTensor = new Tensor<float>(new TensorShape(1, count), mask);
            worker.SetInput("obs_0", obsTensor);
            worker.SetInput("action_masks", maskTensor);
            worker.Schedule();
            using var output = (worker.PeekOutput("deterministic_discrete_actions") as Tensor<int>)?.ReadbackAndClone();
            var action = output != null ? output[0, 0] : 0;
            return allowed(action) ? action : ThiefMatch.SeekActionLook;
        }

        // ------------------------------------------------------------------ decoys (thief-npc-v2.md 11)
        // When no player item is known, now and then (1 in 3 decisions, at most every 45 s) the thief carries a map
        // prop it can see within 8 m to a hiding spot, so players cannot tell which moved things matter. Fixed chance,
        // outside the learned brains: the simulator's players ignore map props, so a brain would learn it is useless.

        private sealed class Decoy
        {
            public CarryableItem Item;
            public string Id;
            public bool Holding;
            public int Spot = -1;
            public float Until;
        }

        private Decoy decoy;
        private float nextDecoyAt;
        private CarryableItem[] mapProps;
        private readonly List<int> decoySpots = new();
        private readonly List<float> decoyPaths = new();

        private bool TryStartDecoy()
        {
            if (Time.time < nextDecoyAt) return false;
            for (var k = 0; k < ThiefMatch.Players; k++) if (match.KnownPropSlots[k] >= 0) return false;
            nextDecoyAt = Time.time + 8f;
            if (Random.value > 1f / 3f) return false;
            mapProps ??= FindObjectsByType<CarryableItem>(FindObjectsSortMode.None);
            var body = match.Thief.Body;
            CarryableItem best = null;
            var bestDistance = 8f;
            foreach (var item in mapProps)
            {
                if (item == null || item.IsCarried) continue;
                var id = item.ObjectId;
                if (System.Array.IndexOf(itemIds, id) >= 0) continue;
                var d = Vector3.Distance(item.transform.position, body.Position);
                if (d >= bestDistance || !HideSeekVision.InCone(body.Eye, body.Yaw, body.Pitch, item.transform.position) ||
                    !SeesMapProp(body.Eye, item)) continue;
                best = item;
                bestDistance = d;
            }

            if (best == null) return false;
            decoy = new Decoy { Item = best, Id = best.ObjectId, Until = Time.time + 25f };
            legs.SetDestination(best.transform.position);
            nextDecoyAt = Time.time + 45f;
            Debug.Log($"[Thief NPC] decoy: going for map prop '{decoy.Id}' {bestDistance:F1} m away.");
            return true;
        }

        /// <summary>Line of sight to a map prop: the first thing the ray meets (avatars and players' items skipped) is the prop itself.</summary>
        private bool SeesMapProp(Vector3 eye, CarryableItem item)
        {
            var target = item.transform.position;
            var offset = target - eye;
            var distance = offset.magnitude;
            if (distance < 0.05f) return true;
            var hits = Physics.RaycastAll(eye, offset / distance, distance + 0.3f, arena.World.OccluderMask, QueryTriggerInteraction.Ignore);
            var nearest = float.PositiveInfinity;
            Collider first = null;
            foreach (var h in hits)
            {
                if (Ignored(h.collider) || h.distance >= nearest) continue;
                nearest = h.distance;
                first = h.collider;
            }

            return first == null || first.GetComponentInParent<CarryableItem>() == item;
        }

        private void StepDecoy(Vector3 feet)
        {
            var d = decoy;
            if (Time.time > d.Until || d.Item == null)
            {
                if (d.Holding) starter.TryDropHeldObjectForMatchNpc(npcId, new Pose(feet + npc.transform.forward * 0.5f + Vector3.up * 0.2f, Quaternion.identity), out _);
                EndDecoy("gave up");
                return;
            }

            if (!d.Holding)
            {
                if (Vector3.Distance(feet, d.Item.transform.position) > 1.6f && !legs.IsAtDestination(feet)) return;
                if (!starter.TryHoldObjectForMatchNpc(npcId, d.Id, out var reason))
                {
                    EndDecoy("pickup refused: " + reason);
                    return;
                }

                d.Holding = true;
                arena.World.FillHideCandidates(feet, match.Rng, null, 8, decoySpots, decoyPaths);
                if (decoySpots.Count == 0)
                {
                    starter.TryDropHeldObjectForMatchNpc(npcId, new Pose(feet + npc.transform.forward * 0.5f + Vector3.up * 0.2f, Quaternion.identity), out _);
                    EndDecoy("no spot");
                    return;
                }

                d.Spot = decoySpots[Random.Range(0, decoySpots.Count)];
                legs.SetDestination(arena.Bank.Spots[d.Spot].StandPosition);
                return;
            }

            if (!legs.IsAtDestination(feet)) return;
            var spot = arena.Bank.Spots[d.Spot];
            var ok = starter.TryDropHeldObjectForMatchNpc(npcId, new Pose(spot.Position + Vector3.up * 0.15f, Quaternion.Euler(0f, Random.Range(0f, 360f), 0f)), out var why);
            EndDecoy(ok ? $"moved '{d.Id}' to {spot.Position} ({spot.Tags})" : "hiding refused: " + why);
        }

        private void EndDecoy(string what)
        {
            Debug.Log("[Thief NPC] decoy: " + what);
            decoy = null;
            legs.ClearDestination();
            forwarding = false;
            match.AbortThiefMacro();
        }

        // ------------------------------------------------------------------ real authority

        private bool Pickup(int p)
        {
            if (itemIds[p] == null) return false;
            var ok = starter.TryHoldObjectForMatchNpc(npcId, itemIds[p], out var reason);
            Debug.Log(ok ? $"[Thief NPC] took player {p}'s item '{itemIds[p]}'." : $"[Thief NPC] pickup of '{itemIds[p]}' refused: {reason}");
            return ok;
        }

        private bool Place(int p, int spot)
        {
            var s = arena.Bank.Spots[spot];
            var pose = new Pose(s.Position + Vector3.up * (0.011f + (pivotKnown[p] ? pivotAboveBottom[p] : 0f)), Quaternion.Euler(0f, Random.Range(0f, 360f), 0f));
            var ok = starter.TryDropHeldObjectForMatchNpc(npcId, pose, out var reason);
            Debug.Log(ok ? $"[Thief NPC] hid player {p}'s item at {s.Position} ({s.Tags})." : $"[Thief NPC] hiding refused: {reason}");
            return ok;
        }

        private bool DropHere()
        {
            var at = npc.transform.position;
            if (NavMesh.SamplePosition(at, out var hit, 1f, NavMesh.AllAreas)) at = hit.position;
            var p = match.Thief.Holding;
            var lift = p >= 0 && pivotKnown[p] ? pivotAboveBottom[p] : 0.1f;
            var ok = starter.TryDropHeldObjectForMatchNpc(npcId, new Pose(at + npc.transform.forward * 0.5f + Vector3.up * lift, Quaternion.identity), out var reason);
            Debug.Log(ok ? "[Thief NPC] 30 s passed without hiding: dropped the item." : $"[Thief NPC] drop refused: {reason}");
            return ok;
        }

        // ------------------------------------------------------------------ helpers

        private void MeasurePivot(int p, string itemId, Pose pose)
        {
            foreach (var item in FindObjectsByType<CarryableItem>(FindObjectsSortMode.None))
            {
                if (item.ObjectId != itemId) continue;
                var bounds = new Bounds(item.transform.position, Vector3.zero);
                var any = false;
                foreach (var c in item.GetComponentsInChildren<Collider>())
                {
                    if (!any) bounds = c.bounds; else bounds.Encapsulate(c.bounds);
                    any = true;
                }

                if (!any) return;
                pivotAboveBottom[p] = Mathf.Max(0f, pose.position.y - bounds.min.y);
                pivotKnown[p] = true;
                return;
            }
        }

        private void RegisterItemColliders(string itemId)
        {
            foreach (var item in FindObjectsByType<CarryableItem>(FindObjectsSortMode.None))
            {
                if (item.ObjectId != itemId) continue;
                foreach (var c in item.GetComponentsInChildren<Collider>(true)) itemColliders.Add(c);
            }

            ignoreCache.Clear();
        }

        /// <summary>The simulator had no avatar or player-item colliders; skip them so sight lines match training.</summary>
        private bool Ignored(Collider c)
        {
            if (c == null) return true;
            if (!ignoreCache.TryGetValue(c, out var ignore))
            {
                ignore = itemColliders.Contains(c) || c.GetComponentInParent<PlayerAvatar>() != null;
                ignoreCache[c] = ignore;
            }

            return ignore;
        }

        private void End(string why)
        {
            if (npc != null && npc.Object != null && npc.Runner != null && npc.Object.HasStateAuthority)
            {
                npc.Runner.Despawn(npc.Object);
            }

            if (match != null) Debug.Log($"[Thief NPC] stopped ({why}): hides {match.ThiefHides}, 30 s drops {match.ThiefHoldTimeouts}, decisions {match.ThiefDecisions}.");
            npc = null;
            legs = null;
            match = null;
            decoy = null;
            mapProps = null;
            finishedThisMatch = true;
            HideSeekVision.IgnoreCollider = null;
            DisposeWorkers();
            if (arena != null) Destroy(arena.gameObject);
            arena = null;
            if (navMesh.valid) NavMesh.RemoveNavMeshData(navMesh);
            navMesh = default;
        }

        private void DisposeWorkers()
        {
            seekWorker?.Dispose();
            hideWorker?.Dispose();
            seekWorker = hideWorker = null;
        }

        private void OnDestroy()
        {
            HideSeekVision.IgnoreCollider = null;
            DisposeWorkers();
            if (navMesh.valid) NavMesh.RemoveNavMeshData(navMesh);
        }
    }
}
#endif

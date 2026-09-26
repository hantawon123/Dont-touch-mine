using System.Collections;
using Game.Training.HideSeek;
using UnityEngine;

namespace Game.Training.Thief
{
    /// <summary>
    /// Watch one thief round at human speed (docs/planning/thief-npc-v2.md): three rule players, the thief NPC, their
    /// props, the thief's view cone, a cutaway top view that follows the thief, and over-the-shoulder views of the
    /// thief and the player nearest to it. Visual only: the round runs exactly as in training and evaluation.
    /// Open it with Tools > AI > Thief Viewer.
    /// </summary>
    public sealed class ThiefViewer : MonoBehaviour
    {
        private static readonly Color[] PlayerColors =
        {
            new(0.2f, 0.55f, 1f), new(0.25f, 0.85f, 0.35f), new(0.7f, 0.35f, 1f),
        };

        private static readonly Color ThiefColor = new(1f, 0.25f, 0.2f);

        [SerializeField] private ThiefArena arena;
        [SerializeField] private GameObject characterPrefab;
        [SerializeField] private int firstSeed = 777001;
        [SerializeField, Range(0.25f, 16f)] private float playSpeed = 1f;
        [SerializeField] private float pauseAfterRoundSeconds = 3f;
        [SerializeField, Tooltip("Fast-forward (8x) the first round until this round time, then play at the chosen speed (0 = off).")]
        private float slowFrom;

        private ThiefMatch match;
        private readonly Transform[] bodies = new Transform[ThiefMatch.Players + 1];
        private readonly Animator[] animators = new Animator[ThiefMatch.Players + 1];
        private readonly string[] states = new string[ThiefMatch.Players + 1];
        private readonly Vector3[] lastPositions = new Vector3[ThiefMatch.Players + 1];
        private readonly Transform[] props = new Transform[ThiefMatch.Players];
        private LineRenderer thiefCone;
        private Camera topCamera, thiefCamera, playerCamera;
        private int seed;
        private float finishedAt = -1f;
        private string lastResult = "";
        private int roundsWatched;
        private GUIStyle panelStyle;

        public void Configure(ThiefArena owner, GameObject character, int seedFrom, float speed, float slowFromSeconds = 0f)
        {
            slowFrom = slowFromSeconds;
            arena = owner;
            characterPrefab = character;
            firstSeed = seedFrom;
            playSpeed = Mathf.Clamp(speed, 0.25f, 16f);
        }

        private void Awake()
        {
            if (arena == null) arena = FindFirstObjectByType<ThiefArena>();
            arena.ConfigureForViewer();
            seed = firstSeed;
        }

        private IEnumerator Start()
        {
            while (!arena.IsReady) yield return null;
            match = arena.Matches[0];
            foreach (var cam in Camera.allCameras) cam.enabled = false;
            for (var i = 0; i <= ThiefMatch.Players; i++)
            {
                var color = i < ThiefMatch.Players ? PlayerColors[i] : ThiefColor;
                bodies[i] = MakeBody(i < ThiefMatch.Players ? $"Viewer_Player{i}" : "Viewer_Thief", color, out animators[i]);
            }

            for (var p = 0; p < ThiefMatch.Players; p++)
            {
                props[p] = MakeBox($"Viewer_Prop{p}", arena.World.ObjectHalf * 2f, Color.Lerp(PlayerColors[p], Color.white, 0.25f));
            }

            thiefCone = MakeCone("Viewer_ThiefCone", 0.04f);
            topCamera = MakeCamera("Viewer_TopCamera", new Rect(0f, 0f, 0.5f, 1f));
            topCamera.orthographic = true;
            topCamera.orthographicSize = 9f;
            thiefCamera = MakeCamera("Viewer_ThiefCamera", new Rect(0.5f, 0.5f, 0.5f, 0.5f));
            playerCamera = MakeCamera("Viewer_PlayerCamera", new Rect(0.5f, 0f, 0.5f, 0.5f));
            arena.RestartMatch(0, seed);
            for (var i = 0; i <= ThiefMatch.Players; i++) lastPositions[i] = match.Actors[i].Body.Position;
        }

        private void Update()
        {
            var fastForward = match != null && roundsWatched == 0 && slowFrom > 0f && match.Time < slowFrom;
            Time.timeScale = fastForward ? 8f : playSpeed;
            if (match == null) return;
            if (match.Done && finishedAt < 0f)
            {
                finishedAt = Time.unscaledTime;
                roundsWatched++;
                lastResult = $"{match.Winners}/{ThiefMatch.Players} players won (thief reward {match.ThiefReward:F2}), thief re-hid {match.ThiefHides}, attacked {match.ThiefAttacks}, stunned {match.PlayersStunnedByThief} players, was stunned {match.Thief.TimesStunned}";
            }

            if (finishedAt >= 0f && Time.unscaledTime - finishedAt >= pauseAfterRoundSeconds) NextRound(seed + 1);
        }

        public void NextRound(int nextSeed)
        {
            seed = nextSeed;
            finishedAt = -1f;
            arena.RestartMatch(0, seed);
        }

        private void LateUpdate()
        {
            if (match == null || bodies[0] == null) return;
            for (var i = 0; i <= ThiefMatch.Players; i++) Sync(i);

            for (var p = 0; p < ThiefMatch.Players; p++)
            {
                var prop = match.Props[p];
                if (prop.HeldBy >= 0)
                {
                    var holder = match.Actors[prop.HeldBy].Body;
                    props[p].position = holder.Position + Quaternion.Euler(0f, holder.Yaw, 0f) * Vector3.forward * 0.35f + Vector3.up * 0.9f;
                    props[p].rotation = Quaternion.Euler(0f, holder.Yaw, 0f);
                }
                else
                {
                    props[p].position = prop.Bottom + Vector3.up * arena.World.ObjectHalf.y;
                    props[p].rotation = Quaternion.identity;
                }
            }

            var mind = match.Mind;
            var seesProp = false;
            var seesPlayer = false;
            for (var p = 0; p < ThiefMatch.Players; p++)
            {
                seesProp |= mind.PropKnown[p] && match.Time - mind.PropSeenTime[p] < 0.15f;
                seesPlayer |= mind.PlayerSeenNow[p];
            }

            DrawCone(thiefCone, match.Thief.Body, 6f, seesProp ? Color.red : seesPlayer ? Color.yellow : new Color(0.3f, 1f, 0.4f));

            var thief = match.Thief.Body;
            topCamera.transform.SetPositionAndRotation(new Vector3(thief.Position.x, thief.Position.y + 30f, thief.Position.z), Quaternion.Euler(90f, 0f, 0f));
            topCamera.nearClipPlane = 27f;
            topCamera.farClipPlane = 32f;
            FollowShoulder(thiefCamera, thief);
            var nearest = 0;
            var nearestDistance = float.PositiveInfinity;
            for (var p = 0; p < ThiefMatch.Players; p++)
            {
                var d = (match.Actors[p].Body.Position - thief.Position).sqrMagnitude;
                if (d < nearestDistance)
                {
                    nearestDistance = d;
                    nearest = p;
                }
            }

            FollowShoulder(playerCamera, match.Actors[nearest].Body);
        }

        private void Sync(int i)
        {
            var sim = match.Actors[i].Body;
            var planar = sim.Position - lastPositions[i];
            planar.y = 0f;
            var speed = Time.deltaTime > 0f ? planar.magnitude / Time.deltaTime / Mathf.Max(0.01f, playSpeed) : 0f;
            lastPositions[i] = sim.Position;
            bodies[i].position = Vector3.Lerp(bodies[i].position, sim.Position, 0.5f);
            bodies[i].rotation = Quaternion.Slerp(bodies[i].rotation, Quaternion.Euler(0f, sim.Yaw, 0f), 0.5f);
            var carrying = match.Actors[i].Holding >= 0;
            var moving = speed > 0.3f;
            var stunned = match.Actors[i].Stunned(match.Time);
            string state;
            if (stunned) state = "Stun_Idle";
            else if (sim.Crouched || sim.Prone) state = carrying ? (moving ? "Carry_TwoHands_Crouch_Walk_Forward" : "Carry_TwoHands_Crouch_Idle") : (moving ? "Crouch_Walk_Forward" : "Crouch_Idle");
            else if (match.Actors[i].Macro == ThiefMatch.Macro.Attack && !moving) state = "Punch";
            else if (speed > 4.5f) state = carrying ? "Carry_TwoHands_Run_Forward" : "Run_Forward";
            else state = carrying ? (moving ? "Carry_TwoHands_Walk_Forward" : "Carry_TwoHands") : (moving ? "Walk_Forward" : "Idle");
            var animator = animators[i];
            if (animator != null && state != states[i] && animator.HasState(0, Animator.StringToHash(state)))
            {
                states[i] = state;
                animator.CrossFadeInFixedTime(state, 0.15f, 0);
            }
        }

        private static void FollowShoulder(Camera cam, HideSeekBody sim)
        {
            var forward = Quaternion.Euler(0f, sim.Yaw, 0f) * Vector3.forward;
            var eye = sim.Eye;
            cam.transform.position = eye - forward * 2.4f + Vector3.up * 0.6f;
            cam.transform.LookAt(eye + forward * 3f + Vector3.up * Mathf.Tan(sim.Pitch * Mathf.Deg2Rad) * 3f);
        }

        private static void DrawCone(LineRenderer line, HideSeekBody sim, float length, Color color)
        {
            const int arc = 12;
            var half = HideSeekRules.HorizontalFov * 0.5f;
            var origin = sim.Position + Vector3.up * 0.1f;
            line.positionCount = arc + 3;
            line.SetPosition(0, origin);
            for (var i = 0; i <= arc; i++)
            {
                var yaw = sim.Yaw - half + i * (2f * half / arc);
                line.SetPosition(i + 1, origin + Quaternion.Euler(0f, yaw, 0f) * Vector3.forward * length);
            }

            line.SetPosition(arc + 2, origin);
            line.startColor = line.endColor = color;
        }

        private Transform MakeBody(string name, Color color, out Animator animator)
        {
            var holder = new GameObject(name + "_Holder");
            holder.SetActive(false);
            var root = characterPrefab != null ? Instantiate(characterPrefab, holder.transform) : GameObject.CreatePrimitive(PrimitiveType.Capsule);
            root.name = name;
            for (var pass = 0; pass < 4; pass++)
            {
                foreach (var mb in root.GetComponentsInChildren<MonoBehaviour>(true))
                {
                    if (mb != null && mb.GetType().Name != "AvatarAppearanceApplier")
                    {
                        try { DestroyImmediate(mb); } catch (System.Exception) { }
                    }
                }
            }

            foreach (var c in root.GetComponentsInChildren<Collider>(true)) DestroyImmediate(c);
            foreach (var rb in root.GetComponentsInChildren<Rigidbody>(true)) DestroyImmediate(rb);
            root.transform.SetParent(null, false);
            Destroy(holder);
            root.SetActive(true);
            animator = root.GetComponentInChildren<Animator>();
            var ring = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            Destroy(ring.GetComponent<Collider>());
            ring.name = name + "_Ring";
            ring.transform.SetParent(root.transform, false);
            ring.transform.localPosition = Vector3.up * 0.02f;
            ring.transform.localScale = new Vector3(0.9f, 0.01f, 0.9f);
            ring.GetComponent<Renderer>().material = Unlit(color);
            return root.transform;
        }

        private static Transform MakeBox(string name, Vector3 size, Color color)
        {
            var box = GameObject.CreatePrimitive(PrimitiveType.Cube);
            Destroy(box.GetComponent<Collider>());
            box.name = name;
            box.transform.localScale = size;
            box.GetComponent<Renderer>().material = Unlit(color);
            return box.transform;
        }

        private static LineRenderer MakeCone(string name, float width)
        {
            var line = new GameObject(name).AddComponent<LineRenderer>();
            line.useWorldSpace = true;
            line.widthMultiplier = width;
            line.material = new Material(Shader.Find("Sprites/Default"));
            return line;
        }

        private static Camera MakeCamera(string name, Rect viewport)
        {
            var cam = new GameObject(name).AddComponent<Camera>();
            cam.rect = viewport;
            cam.fieldOfView = HideSeekRules.VerticalFov;
            cam.nearClipPlane = 0.05f;
            cam.farClipPlane = 60f;
            cam.depth = 10f;
            return cam;
        }

        private static Material Unlit(Color color)
        {
            var shader = Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Unlit/Color");
            var m = new Material(shader) { color = color };
            if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", color);
            return m;
        }

        private void OnGUI()
        {
            if (match == null) return;
            panelStyle ??= new GUIStyle(GUI.skin.box) { alignment = TextAnchor.UpperLeft, fontSize = 14, richText = true };
            var t = match.Thief;
            var text = $"<b>Thief viewer</b>  seed {seed}  round {roundsWatched + 1}  speed {playSpeed:F2}x   time {match.Time:F0} / {ThiefMatch.RoundSeconds:F0}s{(match.Done ? "  <b>ROUND OVER</b>" : "")}\n" +
                       $"<color=#FF5040>thief</color> {arena.ThiefLabel}  {t.Macro}  holding {(t.Holding >= 0 ? $"P{t.Holding}'s prop" : "-")}  {(t.Stunned(match.Time) ? "<b>STUNNED</b>" : "")}  re-hid {match.ThiefHides}  attacks {match.ThiefAttacks}  stunned players {match.PlayersStunnedByThief}\n";
            string[] names = { "<color=#3390FF>P0</color>", "<color=#40D960>P1</color>", "<color=#B35AFF>P2</color>" };
            for (var p = 0; p < ThiefMatch.Players; p++)
            {
                var a = match.Actors[p];
                var own = match.Props[p];
                var where = own.HeldBy == p ? "<b>holding own</b>" : own.HeldBy == ThiefMatch.Players ? "own prop in THIEF's hands" : own.HeldBy >= 0 ? $"own prop held by P{own.HeldBy}" : own.MovedSinceHidden ? "own prop moved" : "own prop where hidden";
                text += $"{names[p]} {a.Macro}  {where}  {(a.Stunned(match.Time) ? "<b>STUNNED</b>" : "")}  {(match.Minds[p].HoldEarly ? "holds early" : $"guards, grabs at {match.Minds[p].GrabAt:F0}s")}\n";
            }

            text += $"winners now {match.Winners}/{ThiefMatch.Players}" + (lastResult.Length > 0 ? $"   last round: {lastResult}" : "") + "\n";
            text += "<size=12>cone: <color=#4DFF66>green</color> searching, <color=yellow>yellow</color> sees a player, <color=red>red</color> sees a player's prop | box colour = owner</size>";
            GUI.Box(new Rect(8, 8, 900, 150), text, panelStyle);
            var speeds = new[] { 0.5f, 1f, 2f, 4f, 8f };
            for (var i = 0; i < speeds.Length; i++)
            {
                if (GUI.Button(new Rect(8 + i * 58, 164, 54, 24), $"{speeds[i]}x")) playSpeed = speeds[i];
            }

            if (GUI.Button(new Rect(8, 194, 110, 24), "next round")) NextRound(seed + 1);
            if (GUI.Button(new Rect(124, 194, 110, 24), "replay seed")) NextRound(seed);
            GUI.Label(new Rect(Screen.width * 0.5f + 8, 8, 300, 22), "thief (over the shoulder)");
            GUI.Label(new Rect(Screen.width * 0.5f + 8, Screen.height * 0.5f + 8, 300, 22), "nearest player (over the shoulder)");
        }
    }
}

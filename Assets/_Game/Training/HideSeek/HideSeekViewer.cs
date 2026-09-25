using System.Collections;
using UnityEngine;

namespace Game.Training.HideSeek
{
    /// <summary>
    /// Watch one hide-seek round at human speed (docs/planning/hide-seek-v1.md). Runs only round 0 of the arena,
    /// draws both bodies with the bear character, the prop, the seeker's view cone, and three cameras: a cutaway
    /// top view (everything above the current floor's ceiling is clipped), the seeker and the hider over the
    /// shoulder. Purely visual: the round is simulated exactly as in training and evaluation.
    /// Open it with Tools > AI > Hide-Seek Viewer.
    /// </summary>
    public sealed class HideSeekViewer : MonoBehaviour
    {
        [SerializeField]
        private HideSeekArena arena;

        [SerializeField, Tooltip("Bear visuals (PlayerCharacter prefab). Gameplay scripts are removed from the copies.")]
        private GameObject characterPrefab;

        [SerializeField]
        private int firstSeed = 777001;

        [SerializeField, Range(0.25f, 16f)]
        private float playSpeed = 1f;

        [SerializeField]
        private float pauseAfterRoundSeconds = 3f;

        private HideSeekMatch match;
        private Body hider, seeker;
        private Transform prop, target;
        private LineRenderer seekerCone, hiderCone;
        private Camera topCamera, seekerCamera, hiderCamera;
        private int seed;
        private float finishedAt = -1f;
        private string lastResult = "";
        private int roundsWatched;
        private GUIStyle panelStyle;

        private sealed class Body
        {
            public Transform Root;
            public Animator Animator;
            public Transform Ring;
            public string State;
            public Vector3 LastPosition;
        }

        public float PlaySpeed { get => playSpeed; set => playSpeed = Mathf.Clamp(value, 0.25f, 16f); }

        public void Configure(HideSeekArena owner, GameObject character, int seedFrom, float speed)
        {
            arena = owner;
            characterPrefab = character;
            firstSeed = seedFrom;
            PlaySpeed = speed;
        }

        private void Awake()
        {
            if (arena == null)
            {
                arena = FindFirstObjectByType<HideSeekArena>();
            }

            arena.ConfigureForViewer();
            seed = firstSeed;
        }

        private IEnumerator Start()
        {
            while (!arena.IsReady)
            {
                yield return null;
            }

            match = arena.Matches[0];
            foreach (var cam in Camera.allCameras)
            {
                cam.enabled = false;
            }

            hider = MakeBody("Viewer_Hider", new Color(0.2f, 0.55f, 1f));
            seeker = MakeBody("Viewer_Seeker", new Color(1f, 0.25f, 0.2f));
            prop = MakeBox("Viewer_Prop", arena.ObjectHalf * 2f, new Color(1f, 0.85f, 0.1f));
            target = MakeBox("Viewer_HideTarget", new Vector3(0.25f, 0.02f, 0.25f), new Color(0.2f, 0.55f, 1f, 0.6f));
            seekerCone = MakeCone("Viewer_SeekerCone", 0.04f);
            hiderCone = MakeCone("Viewer_HiderCone", 0.02f);
            topCamera = MakeCamera("Viewer_TopCamera", new Rect(0f, 0f, 0.5f, 1f));
            topCamera.orthographic = true;
            topCamera.orthographicSize = 8f;
            seekerCamera = MakeCamera("Viewer_SeekerCamera", new Rect(0.5f, 0.5f, 0.5f, 0.5f));
            hiderCamera = MakeCamera("Viewer_HiderCamera", new Rect(0.5f, 0f, 0.5f, 0.5f));
            arena.RestartMatch(0, seed);
            hider.LastPosition = match.Hider.Position;
            seeker.LastPosition = match.Seeker.Position;
        }

        private void Update()
        {
            Time.timeScale = playSpeed;
            if (match == null)
            {
                return;
            }

            if (match.Done && finishedAt < 0f)
            {
                finishedAt = Time.unscaledTime;
                roundsWatched++;
                lastResult = match.FoundAt >= 0f
                    ? $"FOUND at {match.FoundAt:F1}s (hidden share {match.HiddenShare:F2})"
                    : $"NOT FOUND in {HideSeekRules.EpisodeSeconds:F0}s (hidden share 1.00)";
                if (match.DeadlineMissed) lastResult += ", late drop";
            }

            if (finishedAt >= 0f && Time.unscaledTime - finishedAt >= pauseAfterRoundSeconds)
            {
                NextRound(seed + 1);
            }
        }

        public void NextRound(int nextSeed)
        {
            seed = nextSeed;
            finishedAt = -1f;
            arena.RestartMatch(0, seed);
        }

        public void Replay() => NextRound(seed);

        private void LateUpdate()
        {
            if (match == null || hider == null)
            {
                return;
            }

            Sync(hider, match.Hider, match.ObjectHeld);
            Sync(seeker, match.Seeker, false);

            // The prop: in the hider's hands while held, on its spot once placed.
            var half = arena.ObjectHalf;
            prop.position = match.ObjectHeld
                ? match.Hider.Position + Quaternion.Euler(0f, match.Hider.Yaw, 0f) * Vector3.forward * 0.35f + Vector3.up * 0.9f
                : match.ObjectBottom + Vector3.up * half.y;
            prop.rotation = match.ObjectHeld ? Quaternion.Euler(0f, match.Hider.Yaw, 0f) : Quaternion.identity;

            target.gameObject.SetActive(match.ObjectHeld && match.TargetSpot >= 0);
            if (match.TargetSpot >= 0)
            {
                target.position = arena.Bank.Spots[match.TargetSpot].Position + Vector3.up * 0.02f;
            }

            var seekerColor = match.SeekerSeesObject && !match.ObjectHeld ? Color.red
                : match.SeekerSeesHider ? Color.yellow
                : new Color(0.3f, 1f, 0.4f);
            DrawCone(seekerCone, match.Seeker, 6f, seekerColor);
            DrawCone(hiderCone, match.Hider, 4f, match.HiderSeesSeeker ? Color.yellow : new Color(0.3f, 0.6f, 1f));

            // Cameras.
            var focus = Vector3.Distance(match.Hider.Position, match.Seeker.Position) < 14f &&
                        Mathf.Abs(match.Hider.Position.y - match.Seeker.Position.y) < 1.5f
                ? (match.Hider.Position + match.Seeker.Position) * 0.5f
                : match.Seeker.Position;
            var floorY = match.Seeker.Position.y;
            topCamera.transform.SetPositionAndRotation(new Vector3(focus.x, floorY + 30f, focus.z), Quaternion.Euler(90f, 0f, 0f));
            topCamera.nearClipPlane = 30f - 3.0f; // cut away everything above this floor's ceiling
            topCamera.farClipPlane = 30f + 2f;
            FollowShoulder(seekerCamera, match.Seeker);
            FollowShoulder(hiderCamera, match.Hider);
        }

        private void Sync(Body body, HideSeekBody sim, bool carrying)
        {
            var planar = sim.Position - body.LastPosition;
            planar.y = 0f;
            var speed = Time.deltaTime > 0f ? planar.magnitude / Time.deltaTime / Mathf.Max(0.01f, playSpeed) : 0f;
            body.LastPosition = sim.Position;
            body.Root.position = Vector3.Lerp(body.Root.position, sim.Position, 0.5f);
            body.Root.rotation = Quaternion.Slerp(body.Root.rotation, Quaternion.Euler(0f, sim.Yaw, 0f), 0.5f);

            var moving = speed > 0.3f;
            string state;
            if (sim.Crouched)
            {
                state = carrying ? (moving ? "Carry_TwoHands_Crouch_Walk_Forward" : "Carry_TwoHands_Crouch_Idle")
                    : (moving ? "Crouch_Walk_Forward" : "Crouch_Idle");
            }
            else
            {
                state = carrying ? (moving ? "Carry_TwoHands_Walk_Forward" : "Carry_TwoHands")
                    : (moving ? "Walk_Forward" : "Idle");
            }

            if (body.Animator != null && state != body.State && body.Animator.HasState(0, Animator.StringToHash(state)))
            {
                body.State = state;
                body.Animator.CrossFadeInFixedTime(state, 0.15f, 0);
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
            var eye = sim.Position + Vector3.up * 0.1f;
            line.positionCount = arc + 3;
            line.SetPosition(0, eye);
            for (var i = 0; i <= arc; i++)
            {
                var yaw = sim.Yaw - half + i * (2f * half / arc);
                line.SetPosition(i + 1, eye + Quaternion.Euler(0f, yaw, 0f) * Vector3.forward * length);
            }

            line.SetPosition(arc + 2, eye);
            line.startColor = line.endColor = color;
        }

        // ------------------------------------------------------------------ construction

        private Body MakeBody(string name, Color color)
        {
            var body = new Body();
            var holder = new GameObject(name + "_Holder");
            holder.SetActive(false); // no Awake on the gameplay scripts while they are removed
            var root = characterPrefab != null
                ? Instantiate(characterPrefab, holder.transform)
                : GameObject.CreatePrimitive(PrimitiveType.Capsule);
            root.name = name;
            // Several passes: a script another script requires can only go after the dependent one.
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
            body.Root = root.transform;
            body.Animator = root.GetComponentInChildren<Animator>();

            var ring = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            Destroy(ring.GetComponent<Collider>());
            ring.name = name + "_Ring";
            ring.transform.SetParent(body.Root, false);
            ring.transform.localPosition = Vector3.up * 0.02f;
            ring.transform.localScale = new Vector3(0.9f, 0.01f, 0.9f);
            ring.GetComponent<Renderer>().material = Unlit(color);
            body.Ring = ring.transform;
            return body;
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
            var go = new GameObject(name);
            var line = go.AddComponent<LineRenderer>();
            line.useWorldSpace = true;
            line.widthMultiplier = width;
            // Sprites/Default multiplies by vertex colour, so startColor / endColor show (URP Unlit ignores them).
            line.material = new Material(Shader.Find("Sprites/Default"));
            return line;
        }

        private static Camera MakeCamera(string name, Rect viewport)
        {
            var go = new GameObject(name);
            var cam = go.AddComponent<Camera>();
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

        // ------------------------------------------------------------------ overlay

        private void OnGUI()
        {
            if (match == null)
            {
                return;
            }

            panelStyle ??= new GUIStyle(GUI.skin.box) { alignment = TextAnchor.UpperLeft, fontSize = 14, richText = true };
            var hiderLabel = arena.HiderPolicyLabel;
            var seekerLabel = arena.SeekerPolicyLabel;
            var phase = match.Done ? "<b>ROUND OVER</b>" : match.ObjectHeld ? $"hiding ({Mathf.Max(0f, HideSeekRules.HideDeadlineSeconds - match.Time):F1}s left to place)" : "searching";
            var text =
                $"<b>Hide-Seek viewer</b>  seed {seed}  round {roundsWatched + 1}  speed {playSpeed:F2}x\n" +
                $"time {match.Time:F1} / {HideSeekRules.EpisodeSeconds:F0}s   {phase}\n" +
                $"<color=#5AA0FF>hider</color>  {hiderLabel}  state {match.HState}  relocations {match.Relocations}  sees seeker {(match.HiderSeesSeeker ? "YES" : "no")}\n" +
                $"<color=#FF5040>seeker</color> {seekerLabel}  state {match.SState}  sees hider {(match.SeekerSeesHider ? "YES" : "no")}  sees prop {(match.SeekerSeesObject && !match.ObjectHeld ? "YES" : "no")}\n" +
                (match.PlacedSpot >= 0 ? $"placed on spot {match.PlacedSpot} ({arena.Bank.Spots[match.PlacedSpot].Tags}) at {match.PlacedAt:F1}s\n" : "") +
                (lastResult.Length > 0 ? $"last round: {lastResult}\n" : "");
            text += "<size=12>cone: <color=#4DFF66>green</color> searching, <color=yellow>yellow</color> sees the other bot, <color=red>red</color> seeker sees the prop | ring: <color=#5AA0FF>blue</color> hider, <color=#FF5040>red</color> seeker | yellow box = prop, flat blue = hider's target spot</size>";
            GUI.Box(new Rect(8, 8, 760, 136), text, panelStyle);

            var y = 150f;
            var speeds = new[] { 0.5f, 1f, 2f, 4f, 8f };
            for (var i = 0; i < speeds.Length; i++)
            {
                if (GUI.Button(new Rect(8 + i * 58, y, 54, 24), $"{speeds[i]}x"))
                {
                    playSpeed = speeds[i];
                }
            }

            if (GUI.Button(new Rect(8, y + 30, 110, 24), "next round")) NextRound(seed + 1);
            if (GUI.Button(new Rect(124, y + 30, 110, 24), "replay seed")) Replay();

            GUI.Label(new Rect(Screen.width * 0.5f + 8, 8, 300, 22), "seeker (over the shoulder)");
            GUI.Label(new Rect(Screen.width * 0.5f + 8, Screen.height * 0.5f + 8, 300, 22), "hider (over the shoulder)");
        }
    }
}

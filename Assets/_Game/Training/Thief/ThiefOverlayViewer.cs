using System.Collections;
using UnityEngine;

namespace Game.Training.Thief
{
    /// <summary>
    /// Presentation view of the parallel training arena: all rounds of one Mansion_Thief arena (16) run at once on the
    /// same map, each in its own colour (capsules for the three players, a taller capsule for the thief, small boxes
    /// for the players' props). In training nothing is drawn: the bodies are positions only, which is why the rounds
    /// can overlap without touching each other. Visual only. Open with Tools > AI > Thief Overlay Viewer (16 rounds).
    /// </summary>
    public sealed class ThiefOverlayViewer : MonoBehaviour
    {
        [SerializeField] private ThiefArena arena;
        [SerializeField, Range(0.25f, 16f)] private float playSpeed = 4f;

        private Transform[,] bodies;
        private Transform[,] props;
        private Camera topCamera, sideCamera;
        private GUIStyle style;
        private int rounds;

        public void Configure(ThiefArena owner, float speed)
        {
            arena = owner;
            playSpeed = Mathf.Clamp(speed, 0.25f, 16f);
        }

        private void Awake()
        {
            if (arena == null) arena = FindFirstObjectByType<ThiefArena>();
            arena.ConfigureForOverlay();
        }

        private IEnumerator Start()
        {
            while (!arena.IsReady) yield return null;
            rounds = arena.Matches.Count;
            foreach (var cam in Camera.allCameras) cam.enabled = false;
            bodies = new Transform[rounds, ThiefMatch.Players + 1];
            props = new Transform[rounds, ThiefMatch.Players];
            for (var r = 0; r < rounds; r++)
            {
                var hue = Color.HSVToRGB(r / (float)rounds, 0.75f, 1f);
                for (var i = 0; i <= ThiefMatch.Players; i++)
                {
                    var thief = i == ThiefMatch.Players;
                    var body = GameObject.CreatePrimitive(PrimitiveType.Capsule);
                    Destroy(body.GetComponent<Collider>());
                    body.name = $"Overlay_R{r}_{(thief ? "Thief" : "P" + i)}";
                    body.transform.localScale = thief ? new Vector3(0.7f, 1.05f, 0.7f) : new Vector3(0.5f, 0.85f, 0.5f);
                    body.GetComponent<Renderer>().material = Unlit(thief ? Color.Lerp(hue, Color.black, 0.35f) : Color.Lerp(hue, Color.white, 0.35f));
                    bodies[r, i] = body.transform;
                }

                for (var p = 0; p < ThiefMatch.Players; p++)
                {
                    var box = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    Destroy(box.GetComponent<Collider>());
                    box.name = $"Overlay_R{r}_Prop{p}";
                    box.transform.localScale = arena.World.ObjectHalf * 2f;
                    box.GetComponent<Renderer>().material = Unlit(hue);
                    props[r, p] = box.transform;
                }
            }

            var wp = arena.World.Waypoints;
            var bounds = new Bounds(wp[0], Vector3.zero);
            foreach (var w in wp) bounds.Encapsulate(w);
            topCamera = MakeCamera("Overlay_TopCamera", new Rect(0f, 0f, 0.6f, 1f));
            topCamera.orthographic = true;
            topCamera.orthographicSize = Mathf.Max(bounds.extents.z, bounds.extents.x / 1.1f) + 3f;
            topCamera.transform.SetPositionAndRotation(new Vector3(bounds.center.x, bounds.max.y + 40f, bounds.center.z), Quaternion.Euler(90f, 0f, 0f));
            topCamera.nearClipPlane = 37f;
            topCamera.farClipPlane = 45f;
            sideCamera = MakeCamera("Overlay_SideCamera", new Rect(0.6f, 0f, 0.4f, 1f));
            sideCamera.transform.position = bounds.center + new Vector3(-bounds.extents.x - 6f, 14f, -bounds.extents.z - 6f);
            sideCamera.transform.LookAt(bounds.center);
            sideCamera.farClipPlane = 200f;
        }

        private void Update()
        {
            Time.timeScale = playSpeed;
        }

        private void LateUpdate()
        {
            if (bodies == null) return;
            for (var r = 0; r < rounds; r++)
            {
                var m = arena.Matches[r];
                for (var i = 0; i <= ThiefMatch.Players; i++)
                {
                    var b = m.Actors[i].Body;
                    var h = i == ThiefMatch.Players ? 1.05f : 0.85f;
                    bodies[r, i].position = b.Position + Vector3.up * (b.Prone ? 0.3f : b.Crouched ? h * 0.7f : h);
                    bodies[r, i].rotation = b.Prone ? Quaternion.Euler(90f, b.Yaw, 0f) : Quaternion.Euler(0f, b.Yaw, 0f);
                }

                for (var p = 0; p < ThiefMatch.Players; p++)
                {
                    var prop = m.Props[p];
                    props[r, p].position = prop.HeldBy >= 0
                        ? m.Actors[prop.HeldBy].Body.Position + Vector3.up * 1.9f
                        : prop.Bottom + Vector3.up * arena.World.ObjectHalf.y;
                }
            }
        }

        private static Camera MakeCamera(string name, Rect viewport)
        {
            var cam = new GameObject(name).AddComponent<Camera>();
            cam.rect = viewport;
            cam.fieldOfView = 45f;
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
            if (bodies == null) return;
            style ??= new GUIStyle(GUI.skin.box) { alignment = TextAnchor.UpperLeft, fontSize = 22, richText = true };
            var m = arena.Matches[0];
            GUI.Box(new Rect(12, 12, 760, 96),
                $"<b>한 맵 위에서 {rounds}판 동시 진행</b>  (판마다 색이 다름 · 진한 색 = 도둑, 연한 색 = 플레이어, 상자 = 물건)\n" +
                $"실제 학습은 화면 없이 계산 · 이 장면은 시각화 · 재생 {playSpeed:F0}배속 · 1판 시간 {m.Time:F0} / {ThiefMatch.RoundSeconds:F0}초", style);
        }
    }
}

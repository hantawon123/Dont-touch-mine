using UnityEngine;

namespace Game.Client.Interactions
{
    /// <summary>
    /// [테스트] 물리 들기 회전 표시. PLACE TO FIT 예시 구성: 원 세 개(카메라를 향한 원·적도·세로 대원)는 기본 흰색이고,
    /// 이번 조작으로 돌린 각도만큼 그 방향으로 원 자체가 색으로 찬다(180도 = 반, 360도 = 전부): 좌우 빨강, 앞뒤 파랑 + 위축 바늘,
    /// Q/E 비틀기 주황. 시작점은 시야에서 가장 가까운 점(카메라를 향한 원은 12시).
    /// 구 크기는 물건 크기와 무관하고, 카메라 거리에 비례해 키워 화면에서는 항상 같은 크기로 보인다. LineRenderer로 그린다.
    /// </summary>
    public sealed class PhysicalHoldGizmo : MonoBehaviour
    {
        /// <summary>화면에서 보이는 크기를 유지하기 위한 기준: 카메라에서 1 m 떨어졌을 때의 반지름(m). 거리에 비례해 커진다.</summary>
        private const float RadiusPerMeter = 0.11f;
        private const float MinRadius = 0.05f, MaxRadius = 0.35f;
        private const int Segments = 64;

        /// <summary>카메라에서 이 거리에 있는 구의 반지름(m). 안내 UI를 구 바로 위에 붙일 때 쓴다.</summary>
        public static float RadiusAt(float distanceFromCamera) =>
            Mathf.Clamp(distanceFromCamera * RadiusPerMeter, MinRadius, MaxRadius);
        private const float Width = 0.006f;

        private static readonly Color Idle = new(1f, 1f, 1f, 0.6f);                   // 기본 선(구 윤곽·적도·세로선): 흰색
        private static readonly Color Yaw = new(1f, 0.30f, 0.28f, 0.95f);                // 좌우 회전 호: 빨강
        private static readonly Color Pitch = new(0.25f, 0.60f, 1f, 0.95f);              // 앞뒤 기울이기 호·바늘: 파랑
        private static readonly Color Twist = new(0.96f, 0.63f, 0.29f, 0.95f);           // Q/E 비틀기 호: 주황

        private LineRenderer sphereCircle;  // 카메라를 향한 원. Q/E 비틀기가 12시부터 주황으로 찬다
        private LineRenderer equator;       // 바닥과 평행한 원. 좌우 회전이 카메라 쪽 점부터 빨강으로 찬다
        private LineRenderer vertical;      // 시선·위 방향 평면의 원(화면에서는 세로선). 앞뒤 기울이기가 카메라 쪽 점부터 파랑으로 찬다
        private LineRenderer needle;        // 물건 '위' 축(파랑)
        private Material material;

        public static PhysicalHoldGizmo Create(Transform owner)
        {
            var go = new GameObject("PhysicalHoldGizmo");
            go.transform.SetParent(owner, false);
            var gizmo = go.AddComponent<PhysicalHoldGizmo>();
            gizmo.Build();
            return gizmo;
        }

        private void Build()
        {
            var shader = Shader.Find("Hidden/Internal-Colored") ?? Shader.Find("Sprites/Default") ??
                         Shader.Find("Universal Render Pipeline/Unlit");
            material = new Material(shader) { hideFlags = HideFlags.HideAndDontSave };
            // 물건 안에 있어도 보이게 깊이 검사를 끈다(Internal-Colored가 지원). 다른 셰이더면 무시된다.
            if (material.HasProperty("_ZTest")) material.SetInt("_ZTest", (int)UnityEngine.Rendering.CompareFunction.Always);
            if (material.HasProperty("_ZWrite")) material.SetInt("_ZWrite", 0);
            material.renderQueue = 4000;

            sphereCircle = MakeLine("SphereCircle", Segments + 1, false);
            equator = MakeLine("Equator", Segments + 1, false);
            vertical = MakeLine("Vertical", Segments + 1, false);
            needle = MakeLine("UpNeedle", 2, false);
            Hide();
        }

        private LineRenderer MakeLine(string name, int count, bool loop)
        {
            var go = new GameObject(name);
            go.transform.SetParent(transform, false);
            var line = go.AddComponent<LineRenderer>();
            line.useWorldSpace = true;
            line.loop = loop;
            line.positionCount = count;
            line.startWidth = line.endWidth = Width;
            line.sharedMaterial = material;
            line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            line.receiveShadows = false;
            line.numCapVertices = 2;
            return line;
        }

        public void Hide()
        {
            if (sphereCircle == null) return;
            sphereCircle.enabled = equator.enabled = vertical.enabled = needle.enabled = false;
        }

        /// <summary>
        /// 매 프레임(Update) 호출. 우클릭 회전 중이거나 비틀기 중일 때만 그린다. 원은 모두 흰색이고,
        /// 이번 조작에서 돌린 각도만큼 그 방향으로 원 자체가 색으로 찬다: 180도면 반, 360도면 전부.
        /// 좌우(적도) 빨강, 앞뒤(세로 대원) 파랑, Q/E 비틀기(카메라를 향한 원) 주황.
        /// </summary>
        /// <param name="yawDeltaDeg">이번 우클릭 드래그에서 누적된 좌우 회전(도, 세계 수직축 기준 시계 +)</param>
        /// <param name="pitchDeltaDeg">이번 드래그에서 누적된 앞뒤 기울이기(도, 위→앞으로 넘어가는 방향 +)</param>
        /// <param name="twistDeltaDeg">이번 Q/E 누름에서 누적된 비틀기(도, 화면 기준 시계 +)</param>
        /// <remarks>
        /// 시작점은 구에서 내 시야에 가장 가까운 점(적도·세로 대원은 카메라 쪽 점)이고, 카메라를 향한 원은 모든 점이 같은
        /// 거리라 12시에서 시작한다. 원의 점 순서를 회전 방향으로 잡고 색 그라디언트로 앞부분만 칠하므로 끊김이 없다.
        /// </remarks>
        public void Show(Vector3 center, Transform cam, Quaternion itemRotation, bool rotating, bool twisting,
            float yawDeltaDeg, float pitchDeltaDeg, float twistDeltaDeg)
        {
            if (!rotating && !twisting)
            {
                Hide();
                return;
            }

            // 거리에 비례한 반지름: 화면에서 같은 크기
            var Radius = RadiusAt(Vector3.Distance(cam.position, center));
            var width = Radius * 0.028f;
            foreach (var l in new[] { sphereCircle, equator, vertical, needle }) l.startWidth = l.endWidth = width;
            var right = cam.right;
            var camUp = cam.up;
            var fwdFlat = cam.forward; fwdFlat.y = 0f; fwdFlat.Normalize();
            var rightFlat = right; rightFlat.y = 0f; rightFlat.Normalize();
            var toViewer = -fwdFlat;

            // 카메라를 향한 원: 12시에서 시작, 비튼 방향(화면 기준 시계 +)으로 점을 놓고 그 각도만큼 주황
            sphereCircle.enabled = true;
            FillCircle(sphereCircle, center, camUp, right, Radius, twisting ? twistDeltaDeg : 0f, Twist);

            // 적도: 카메라 쪽 점에서 시작, 좌우 회전 방향으로 점을 놓고 그 각도만큼 빨강.
            // 세계 수직축 기준 시계(+) 회전은 위에서 봤을 때 카메라 쪽 점을 내 왼쪽으로 보낸다(+z→+x가 시계이므로 -z→-x).
            equator.enabled = true;
            FillCircle(equator, center, toViewer, -rightFlat, Radius, rotating ? yawDeltaDeg : 0f, Yaw);

            // 세로 대원: 카메라 쪽 점에서 시작, 기울인 방향(위→앞 +)으로 점을 놓고 그 각도만큼 파랑.
            // 시선과 나란한 평면이라 화면에서는 세로선 위를 따라 차는 것으로 보인다.
            vertical.enabled = true;
            FillCircle(vertical, center, toViewer, Vector3.up, Radius, rotating ? pitchDeltaDeg : 0f, Pitch);

            // 바늘: 물건 '위' 축을 앞뒤 회전 평면에 투영한 방향(기울이는 중일 때만)
            var objUp = itemRotation * Vector3.up;
            var upInPlane = objUp - rightFlat * Vector3.Dot(objUp, rightFlat);
            if (upInPlane.sqrMagnitude < 1e-6f) upInPlane = Vector3.up;
            upInPlane.Normalize();
            needle.enabled = rotating && Mathf.Abs(pitchDeltaDeg) > 0.5f;
            if (needle.enabled)
            {
                needle.startColor = needle.endColor = Pitch;
                needle.SetPosition(0, center);
                needle.SetPosition(1, center + upInPlane * Radius);
            }
        }

        /// <summary>
        /// 원을 <paramref name="startDir"/>에서 시작해 <paramref name="positiveDir"/> 쪽으로 도는 순서로 놓는다(각도 부호가
        /// 음수면 반대 방향). 돌린 각도 / 360 비율만큼 앞부분을 <paramref name="color"/>로, 나머지는 흰색으로 칠한다.
        /// </summary>
        private static void FillCircle(LineRenderer line, Vector3 center, Vector3 startDir, Vector3 positiveDir,
            float radius, float deltaDeg, Color color)
        {
            var sign = deltaDeg < 0f ? -1f : 1f;
            var n = line.positionCount;
            for (var i = 0; i < n; i++)
            {
                var a = sign * (i / (float)(n - 1)) * Mathf.PI * 2f;
                line.SetPosition(i, center + (startDir * Mathf.Cos(a) + positiveDir * Mathf.Sin(a)) * radius);
            }

            var fraction = Mathf.Clamp01(Mathf.Abs(deltaDeg) / 360f);
            var gradient = new Gradient();
            if (fraction <= 0.002f)
            {
                gradient.SetKeys(
                    new[] { new GradientColorKey(Idle, 0f), new GradientColorKey(Idle, 1f) },
                    new[] { new GradientAlphaKey(Idle.a, 0f), new GradientAlphaKey(Idle.a, 1f) });
            }
            else if (fraction >= 0.998f)
            {
                gradient.SetKeys(
                    new[] { new GradientColorKey(color, 0f), new GradientColorKey(color, 1f) },
                    new[] { new GradientAlphaKey(color.a, 0f), new GradientAlphaKey(color.a, 1f) });
            }
            else
            {
                var edge = Mathf.Min(fraction + 0.002f, 1f);
                gradient.SetKeys(
                    new[]
                    {
                        new GradientColorKey(color, 0f), new GradientColorKey(color, fraction),
                        new GradientColorKey(Idle, edge), new GradientColorKey(Idle, 1f),
                    },
                    new[]
                    {
                        new GradientAlphaKey(color.a, 0f), new GradientAlphaKey(color.a, fraction),
                        new GradientAlphaKey(Idle.a, edge), new GradientAlphaKey(Idle.a, 1f),
                    });
            }
            gradient.mode = GradientMode.Fixed;
            line.colorGradient = gradient;
        }

        private void OnDestroy()
        {
            if (material != null) Destroy(material);
        }
    }
}

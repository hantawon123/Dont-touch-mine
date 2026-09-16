using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace Game.Client.Cameras
{
    /// <summary>
    /// 한 동작 상태에서 몸 애니메이션 위에 덧씌우는 팔 각도 묶음. 상태별로 하나씩 둔다.
    /// </summary>
    [Serializable]
    public sealed class FirstPersonArmPose
    {
        [Tooltip("0이면 몸 애니메이션 그대로, 1이면 아래 각도를 전부 적용")]
        [Range(0f, 1f)]
        public float poseWeight = 1f;

        [Tooltip("위팔을 앞으로 들어올리는 각도(도). 90이면 시선과 수평")]
        [Range(-90f, 120f)]
        public float upperArmLift = 70f;

        [Tooltip("위팔을 바깥으로 벌리는 각도(도)")]
        [Range(-60f, 60f)]
        public float upperArmSpread = 20f;

        [Tooltip("아래팔을 앞·위로 굽히는 각도(도)")]
        [Range(-30f, 150f)]
        public float forearmBend = 20f;

        [Tooltip("아래팔을 바깥으로 벌리는 각도(도)")]
        [Range(-60f, 60f)]
        public float forearmSpread = 0f;

        [Tooltip("손을 위로 꺾는 각도(도)")]
        [Range(-90f, 90f)]
        public float handBend = 0f;

        [Tooltip("손을 바깥으로 돌리는 각도(도)")]
        [Range(-90f, 90f)]
        public float handSpread = 0f;

        public void CopyFrom(FirstPersonArmPose other)
        {
            poseWeight = other.poseWeight;
            upperArmLift = other.upperArmLift;
            upperArmSpread = other.upperArmSpread;
            forearmBend = other.forearmBend;
            forearmSpread = other.forearmSpread;
            handBend = other.handBend;
            handSpread = other.handSpread;
        }

        /// <summary>이 자세를 <paramref name="target"/> 쪽으로 <paramref name="t"/>만큼 섞는다.</summary>
        public void BlendTowards(FirstPersonArmPose target, float t)
        {
            poseWeight = Mathf.Lerp(poseWeight, target.poseWeight, t);
            upperArmLift = Mathf.Lerp(upperArmLift, target.upperArmLift, t);
            upperArmSpread = Mathf.Lerp(upperArmSpread, target.upperArmSpread, t);
            forearmBend = Mathf.Lerp(forearmBend, target.forearmBend, t);
            forearmSpread = Mathf.Lerp(forearmSpread, target.forearmSpread, t);
            handBend = Mathf.Lerp(handBend, target.handBend, t);
            handSpread = Mathf.Lerp(handSpread, target.handSpread, t);
        }
    }

    /// <summary>
    /// 1인칭 전용 팔 모델을 어떻게 보일지 정하는 Inspector 값. Play 중에 바꾸면 바로 반영된다.
    /// </summary>
    [Serializable]
    public sealed class FirstPersonArmsSettings
    {
        [Tooltip("1인칭에서 카메라에 붙은 팔 모델을 그린다")]
        public bool showArms = true;

        [Tooltip("Resources 아래 팔 모델 경로. SmoothBear 몸에서 팔만 잘라낸 FBX")]
        public string modelResourcePath = "FirstPerson/SmoothBear_FirstPersonArms";

        [Header("전체 위치·크기·기울기")]
        [Tooltip("팔 리그의 머리(눈) 위치를 카메라 기준 어디에 둘지(m). x=오른쪽, y=위, z=앞. z를 키우면 팔 전체가 앞으로 나와 손이 크게 보인다")]
        public Vector3 eyeAnchor = new(0f, -0.02f, 0.06f);

        [Tooltip("팔 모델 크기 배율. 1이면 몸과 같은 크기, 키우면 손이 화면에서 커진다")]
        [Range(0.3f, 3f)]
        public float scale = 1f;

        [Tooltip("팔 리그 전체를 카메라 기준으로 기울이는 각도(도). x=아래로 숙임(+), y=좌우 돌림, z=좌우 기울임")]
        public Vector3 rootTilt = Vector3.zero;

        [Tooltip("위팔 길이 배율. 아래팔 본을 위팔 뿌리 쪽으로 당겨 위팔을 짧게 보이게 한다(1=원래 길이). 1인칭 팔에만 적용")]
        [Range(0.3f, 1f)]
        public float upperArmLength = 0.85f;

        [Header("오버레이 카메라 — 벽·문에 가까워도 팔이 가려지지 않게 세상 위에 덮어 그린다")]
        [Tooltip("팔 전용 레이어 이름(Project Settings > Tags and Layers). 없으면 오버레이 없이 그린다")]
        public string overlayLayerName = "FirstPersonView";

        [Tooltip("팔을 그리는 오버레이 카메라의 시야각(도). 0이면 메인 카메라와 같게")]
        [Range(0f, 120f)]
        public float armsFieldOfView = 0f;

        [Header("동작별 팔 자세 — 몸 애니메이션 위에 덧씌우는 각도. 상태에 따라 프로필을 고르고 부드럽게 섞는다")]
        [Tooltip("프로필 사이를 섞는 시간(초). 0이면 즉시 전환")]
        [Range(0f, 1f)]
        public float blendSeconds = 0.15f;

        [Tooltip("서기·걷기·달리기·점프·피격")]
        public FirstPersonArmPose locomotion = new();

        [Tooltip("펀치(Punch_*). 몸 모션이 이미 팔을 앞으로 뻗으므로 각도를 거의 0으로 두어 원본 모션이 그대로 나오게 한다")]
        public FirstPersonArmPose punch = new() { upperArmLift = 25f, upperArmSpread = 5f, forearmBend = 0f };

        [Tooltip("펀치 중 때리는 팔을 조준점(크로스헤어) 쪽으로 돌리는 비율. 0이면 몸 모션 방향 그대로, 1이면 어깨→손 방향이 정확히 조준점을 향한다")]
        [Range(0f, 1f)]
        public float punchAimWeight = 0.9f;

        [Tooltip("조준점으로 삼는 시선 앞 거리(m). 짧을수록 팔이 안쪽으로 많이 모인다")]
        [Range(0.3f, 5f)]
        public float punchAimDistance = 1.2f;

        [Tooltip("펀치 절정에서 주먹(손 본)이 카메라에서 앞으로 떨어질 목표 거리(m). 왼손·오른손 클립의 뻗는 정도가 달라도 같은 거리에 닿게 팔 뿌리를 밀어 맞춘다. 0이면 끔")]
        [Range(0f, 1.5f)]
        public float punchHandDistance = 0.6f;

        [Tooltip("밀어낼 때 아래팔을 펴는 각도(도). 카메라 좌우축 기준이라 자세에 따라 어색할 수 있어 기본 0")]
        [Range(0f, 90f)]
        public float punchStraighten = 0f;

        [Tooltip("밀어낼 때 팔꿈치를 안쪽(화면 가운데 쪽)으로 굽히는 각도(도). 주먹이 조준점을 향한 채 팔꿈치만 안으로 접힌다")]
        [Range(0f, 90f)]
        public float punchElbowIn = 20f;

        [Tooltip("펀치 진행도(0~1)에 따른 밀어내는 비율. 빠르게 나가 잠깐 머물고 돌아온다")]
        public AnimationCurve punchReachCurve = new(
            new Keyframe(0f, 0f, 0f, 2.5f),
            new Keyframe(0.4f, 1f, 0f, 0f),
            new Keyframe(0.6f, 1f, 0f, 0f),
            new Keyframe(1f, 0f, -1.5f, 0f));

        [Tooltip("펀치 시작 위치: 때리는 팔 뿌리를 카메라 기준으로 이만큼 옮긴 곳에서 출발한다(m). x=때리는 쪽 바깥, y=위, z=앞. 걷기 손 위치가 아니라 바깥에서 시작해 조준점으로 모이게 한다")]
        public Vector3 punchStartOffset = new(0.08f, -0.03f, -0.03f);

        [Tooltip("펀치 진행도(0~1)에 따른 시작 위치 적용 비율. 0에서 1(바깥), 절정(0.35)에 0으로 모이고 끝까지 0")]
        public AnimationCurve punchStartCurve = new(
            new Keyframe(0f, 1f, 0f, -2.5f),
            new Keyframe(0.4f, 0f, 0f, 0f),
            new Keyframe(1f, 0f, 0f, 0f));

        public float PunchStartAt(float progress) =>
            punchStartCurve != null && punchStartCurve.length > 0
                ? Mathf.Clamp01(punchStartCurve.Evaluate(Mathf.Clamp01(progress)))
                : 0f;

        [Tooltip("펀치 진행도(0~1)에 따른 주먹 쥠 비율. 시작하자마자 쥐고(0.08), 끝날 무렵(0.85)부터 편다")]
        public AnimationCurve punchClenchCurve = new(
            new Keyframe(0f, 0f, 0f, 12f),
            new Keyframe(0.08f, 1f, 0f, 0f),
            new Keyframe(0.85f, 1f, 0f, 0f),
            new Keyframe(1f, 0f, -6f, 0f));

        public float PunchClenchAt(float progress) =>
            punchClenchCurve != null && punchClenchCurve.length > 0
                ? Mathf.Clamp01(punchClenchCurve.Evaluate(Mathf.Clamp01(progress)))
                : 0f;

        public float PunchReachAt(float progress) =>
            punchReachCurve != null && punchReachCurve.length > 0
                ? Mathf.Clamp01(punchReachCurve.Evaluate(Mathf.Clamp01(progress)))
                : 0f;

        [Tooltip("물건 들기·집기·놓기·던지기(Carry_*, Pickup, PutDown, PutUp, Throw)")]
        public FirstPersonArmPose carry = new();

        [Tooltip("앉기(Crouch_*)")]
        public FirstPersonArmPose crouch = new();

        [Tooltip("엎드리기·기어가기(Prone_*, Crawl_*)")]
        public FirstPersonArmPose prone = new();

        /// <summary>애니메이션 드라이버의 상태(클립) 이름으로 프로필을 고른다. 펀치가 자세보다 우선한다.</summary>
        public FirstPersonArmPose Select(string animationState)
        {
            if (string.IsNullOrEmpty(animationState)) return locomotion;
            if (Has(animationState, "Punch")) return punch;
            if (Has(animationState, "Carry") || Has(animationState, "Pickup") || Has(animationState, "PutDown") ||
                Has(animationState, "PutUp") || Has(animationState, "Throw")) return carry;
            if (Has(animationState, "Crawl") || Has(animationState, "Prone")) return prone;
            if (Has(animationState, "Crouch")) return crouch;
            return locomotion;
        }

        private static bool Has(string state, string token) =>
            state.IndexOf(token, StringComparison.Ordinal) >= 0;
    }

    /// <summary>
    /// 1인칭 전용 팔 뷰. 3인칭 몸과는 별개의 팔 모델을 카메라 리그 아래에 두고, 매 프레임
    /// 몸 리그의 팔 본 회전을 복사한 뒤 카메라 기준 자세 오프셋을 더한다.
    /// </summary>
    /// <remarks>
    /// <para>몸(SmoothBear)과 같은 뼈대를 쓰므로 걷기·펀치·들기 등 몸 애니메이션의 팔 움직임이
    /// 그대로 1인칭 팔에 나타나고, 다른 플레이어가 보는 모션과도 어긋나지 않는다.
    /// 팔 모델의 머리 본을 카메라 위치에 맞추고 리그 전체를 카메라 회전으로 돌리므로
    /// 위·아래·옆 어디를 봐도 손은 화면의 같은 자리에 남는다.</para>
    /// <para>재질과 몸 색(MaterialPropertyBlock)은 몸의 Body 렌더러에서 매 프레임 복사한다.
    /// 그림자는 만들지 않는다 — 숨긴 3인칭 몸이 그림자 전용으로 계속 그리기 때문이다.</para>
    /// </remarks>
    public sealed class FirstPersonArmsView
    {
        private static readonly string[] CopiedBonePrefixes =
        {
            "Shoulder.", "UpperArm.", "Arm.", "Hand.", "Finger_", "Volume_UpperArm.", "Volume_Arm.",
        };

        private readonly List<(Transform arms, Transform body, bool finger)> copiedBones = new();
        // 펀치 클립에서 뽑아 둔 "주먹을 가장 꽉 쥔" 손가락 로컬 회전(copiedBones와 같은 인덱스, 손가락 아닌 칸은 identity)
        private readonly List<Quaternion> clenchedFingers = new();
        private bool triedClenchSample;
        private Animator bodyAnimator;
        // MonoBehaviour 필드 초기화 시점(생성자)에는 엔진 객체를 만들 수 없으므로 첫 사용 때 만든다.
        private MaterialPropertyBlock block;
        private Transform armsRoot;
        private Transform armsHead;
        private Transform upperArmLeft, upperArmRight, forearmLeft, forearmRight, handLeft, handRight;
        private Vector3 bodyScale = Vector3.one;
        private Renderer[] armsRenderers = Array.Empty<Renderer>();
        private Renderer bodyRenderer;
        private Vector3 eyeLocal;
        private bool ownsInstance;
        private bool warnedMissingModel;
        private readonly FirstPersonArmPose blendedPose = new();
        private bool hasBlendedPose;
        private Camera overlayCamera;
        private Camera overlayBase;
        private int overlayBaseCullingMask;
        private int overlayLayer = -1;
        private bool warnedMissingLayer;

        public bool IsReady => armsRoot != null;
        public Transform Root => armsRoot;

        /// <summary>
        /// 몸의 Visual과 카메라 리그를 연결한다. 팔 모델은 Resources에서 한 번만 불러 카메라 리그 아래에 만든다.
        /// </summary>
        public void Bind(Transform visual, Transform cameraRig, FirstPersonArmsSettings settings)
        {
            if (visual == null || cameraRig == null || settings == null)
            {
                Hide();
                return;
            }

            if (armsRoot == null)
            {
                var model = Resources.Load<GameObject>(settings.modelResourcePath);
                if (model == null)
                {
                    if (!warnedMissingModel)
                    {
                        warnedMissingModel = true;
                        Debug.LogError(
                            $"FirstPersonArmsView: Resources/{settings.modelResourcePath} 팔 모델을 찾을 수 없습니다. " +
                            "Tools/build_first_person_arms.py로 FBX를 만들고 Unity가 임포트했는지 확인하세요.");
                    }

                    return;
                }

                var instance = UnityEngine.Object.Instantiate(model, cameraRig, false);
                instance.name = "FirstPersonArms";
                ownsInstance = true;
                AttachInstance(instance.transform, visual);
            }
            else
            {
                MapBones(visual);
            }
        }

        /// <summary>
        /// 테스트용: 이미 만들어 둔 팔 인스턴스를 연결한다. Resources를 읽지 않는다.
        /// </summary>
        public void BindWithInstance(Transform visual, Transform cameraRig, Transform instance)
        {
            if (instance == null) return;
            instance.SetParent(cameraRig, false);
            ownsInstance = false;
            AttachInstance(instance, visual);
        }

        private void AttachInstance(Transform instance, Transform visual)
        {
            armsRoot = instance;
            bodyScale = visual.lossyScale;
            armsRoot.localScale = bodyScale;

            var animator = armsRoot.GetComponentInChildren<Animator>();
            if (animator != null) animator.enabled = false;

            armsRenderers = armsRoot.GetComponentsInChildren<Renderer>(true);
            foreach (var renderer in armsRenderers)
            {
                renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                renderer.receiveShadows = false;
                if (renderer is SkinnedMeshRenderer skinned) skinned.updateWhenOffscreen = true;
            }

            overlayLayer = -1; // 첫 Apply에서 설정 이름으로 다시 찾는다
            var armsBones = Collect(armsRoot);
            armsBones.TryGetValue("Head", out armsHead);
            armsBones.TryGetValue("UpperArm.L", out upperArmLeft);
            armsBones.TryGetValue("UpperArm.R", out upperArmRight);
            armsBones.TryGetValue("Arm.L", out forearmLeft);
            armsBones.TryGetValue("Arm.R", out forearmRight);
            armsBones.TryGetValue("Hand.L", out handLeft);
            armsBones.TryGetValue("Hand.R", out handRight);
            // 팔 리그의 몸통 본은 쉬는 자세로 고정되므로 머리 위치는 루트 기준 상수다.
            eyeLocal = armsHead != null ? armsRoot.InverseTransformPoint(armsHead.position) : Vector3.zero;

            MapBones(visual);
            armsRoot.gameObject.SetActive(false);
        }

        private void MapBones(Transform visual)
        {
            copiedBones.Clear();
            bodyRenderer = null;
            if (armsRoot == null || visual == null) return;

            var bodyBones = Collect(visual);
            foreach (var pair in Collect(armsRoot))
            {
                if (!IsCopied(pair.Key) || !bodyBones.TryGetValue(pair.Key, out var bodyBone)) continue;
                copiedBones.Add((pair.Value, bodyBone, pair.Key.StartsWith("Finger_", StringComparison.Ordinal)));
            }

            bodyAnimator = visual.GetComponentInChildren<Animator>(true);
            clenchedFingers.Clear();
            triedClenchSample = false;

            foreach (var renderer in visual.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {
                if (renderer.name != "Body") continue;
                bodyRenderer = renderer;
                break;
            }

            SyncAppearance();
        }

        /// <summary>
        /// 몸 렌더러의 재질과 슬롯별 MaterialPropertyBlock(몸 색·피격 틴트)을 팔에 복사한다.
        /// AvatarAppearanceApplier는 슬롯 인덱스를 붙여 블록을 쓰므로 슬롯 없는 GetPropertyBlock은 빈 값을 돌려준다.
        /// </summary>
        private void SyncAppearance()
        {
            if (bodyRenderer == null) return;
            var bodyMaterials = bodyRenderer.sharedMaterials;
            block ??= new MaterialPropertyBlock();
            foreach (var renderer in armsRenderers)
            {
                if (renderer == null) continue;
                if (!SameMaterials(renderer.sharedMaterials, bodyMaterials))
                    renderer.sharedMaterials = bodyMaterials;
                for (var slot = 0; slot < bodyMaterials.Length; slot++)
                {
                    bodyRenderer.GetPropertyBlock(block, slot);
                    renderer.SetPropertyBlock(block, slot);
                }
            }
        }

        private static bool SameMaterials(Material[] a, Material[] b)
        {
            if (a == null || b == null || a.Length != b.Length) return false;
            for (var i = 0; i < a.Length; i++)
                if (!ReferenceEquals(a[i], b[i])) return false;
            return true;
        }

        /// <summary>
        /// 매 프레임 LateUpdate에서 호출. 몸 Animator가 본을 쓴 뒤여야 팔 포즈 복사가 이 프레임 값을 얻는다.
        /// </summary>
        /// <param name="animationState">몸 애니메이션 드라이버의 현재 클립 이름. null이면 기본(이동) 프로필</param>
        /// <param name="punching">펀치 모션 재생 중이면 true</param>
        /// <param name="punchLeft">이번 펀치가 왼손이면 true</param>
        /// <param name="punchProgress">펀치 진행도 0~1(밀어내기 곡선에 쓴다). 모르면 0.35(최대)로 취급</param>
        public void Apply(bool visible, FirstPersonArmsSettings settings, Transform camera, string animationState = null,
            bool punching = false, bool punchLeft = false, float punchProgress = 0.35f)
        {
            if (armsRoot == null) return;
            if (!visible || settings == null || camera == null)
            {
                Hide();
                return;
            }

            if (!armsRoot.gameObject.activeSelf) armsRoot.gameObject.SetActive(true);
            EnsureOverlay(settings);

            // 리그를 카메라 방향(+기울기)으로 돌리고, 머리 본이 카메라 앵커에 오도록 루트를 놓는다.
            armsRoot.localScale = bodyScale * Mathf.Max(0.01f, settings.scale);
            armsRoot.rotation = camera.rotation * Quaternion.Euler(settings.rootTilt);
            armsRoot.position = camera.TransformPoint(settings.eyeAnchor) - armsRoot.TransformVector(eyeLocal);

            // 펀치 중 손가락은 몸 모션의 느린 쥠 대신, 바로 쥐고 끝날 무렵에만 펴는 곡선을 따른다.
            var clench = punching ? settings.PunchClenchAt(punchProgress) : 0f;
            if (clench > 0f) EnsureClenchedFingers();
            var useClench = clench > 0f && clenchedFingers.Count == copiedBones.Count;
            for (var i = 0; i < copiedBones.Count; i++)
            {
                var (arms, body, finger) = copiedBones[i];
                arms.localRotation = useClench && finger
                    ? Quaternion.Slerp(body.localRotation, clenchedFingers[i], clench)
                    : body.localRotation;
                // 아래팔 본을 위팔 뿌리 쪽으로 당기면 위팔이 짧아진다(아래팔·손 길이는 그대로).
                arms.localPosition = ReferenceEquals(arms, forearmLeft) || ReferenceEquals(arms, forearmRight)
                    ? body.localPosition * Mathf.Clamp(settings.upperArmLength, 0.3f, 1f)
                    : body.localPosition;
                arms.localScale = body.localScale;
            }

            // 상태에 맞는 프로필로 부드럽게 옮겨 간다(펀치 시작·끝에 팔이 툭 튀지 않게).
            var target = settings.Select(animationState);
            if (!hasBlendedPose || settings.blendSeconds <= 0f)
            {
                blendedPose.CopyFrom(target);
                hasBlendedPose = true;
            }
            else
            {
                blendedPose.BlendTowards(target, 1f - Mathf.Exp(-Time.deltaTime / settings.blendSeconds));
            }

            var weight = Mathf.Clamp01(blendedPose.poseWeight);
            if (weight > 0f)
            {
                PoseArm(upperArmRight, forearmRight, handRight, +1f, blendedPose, weight, camera);
                PoseArm(upperArmLeft, forearmLeft, handLeft, -1f, blendedPose, weight, camera);
            }

            // 펀치 중에는 때리는 팔(어깨→손)이 조준점을 향하도록 위팔을 돌린다. 몸 모션은 몸 정면으로 뻗지만
            // 1인칭에서는 화면 가운데(크로스헤어) 쪽으로 나가야 맞아 보인다.
            if (punching)
            {
                var upper = punchLeft ? upperArmLeft : upperArmRight;
                var fore = punchLeft ? forearmLeft : forearmRight;
                var hand = punchLeft ? handLeft : handRight;

                // 시작 위치: 걷기 손 자리가 아니라 바깥(때리는 쪽)에서 출발해 절정까지 조준점 쪽으로 모인다.
                var start = settings.PunchStartAt(punchProgress);
                if (upper != null && start > 0f)
                {
                    var side = punchLeft ? -1f : 1f;
                    var o = settings.punchStartOffset;
                    upper.position += (camera.right * (side * o.x) + camera.up * o.y + camera.forward * o.z) * start;
                }

                var k = settings.PunchReachAt(punchProgress);

                // 1) 팔꿈치: 아래팔을 안쪽(화면 가운데 쪽)으로 굽히고, 필요하면 펴기도 더한다. 조준보다 먼저 해야
                //    조준 보정이 굽힌 뒤의 손 위치를 기준으로 주먹을 조준점에 맞춘다.
                if (fore != null && k > 0f)
                {
                    var side = punchLeft ? -1f : 1f;
                    fore.rotation = Quaternion.AngleAxis(-side * settings.punchElbowIn * k, camera.up) *
                                    Quaternion.AngleAxis(settings.punchStraighten * k, camera.right) *
                                    fore.rotation;
                }

                // 2) 조준: 어깨→손 방향이 조준점을 향하게 위팔을 돌린다.
                if (settings.punchAimWeight > 0f)
                {
                    AimArmAt(upper, hand, camera.position + camera.forward * settings.punchAimDistance,
                        settings.punchAimWeight);
                }

                // 3) 거리: 주먹이 목표 거리까지 나가도록 팔 뿌리를 시선 방향으로 민다.
                //    왼손·오른손 클립의 뻗는 정도가 달라도 손 본이 같은 거리에 닿으므로 양손 펀치가 같아 보인다.
                if (k > 0f && upper != null && hand != null && settings.punchHandDistance > 0f)
                {
                    var handForward = Vector3.Dot(hand.position - camera.position, camera.forward);
                    var push = Mathf.Max(0f, settings.punchHandDistance - handForward) * k;
                    upper.position += camera.forward * push;
                }
            }

            SyncAppearance();
        }

        public void Hide()
        {
            if (armsRoot != null && armsRoot.gameObject.activeSelf) armsRoot.gameObject.SetActive(false);
        }

        /// <summary>카메라 리그가 사라질 때 만든 팔 인스턴스를 함께 지운다.</summary>
        public void Dispose()
        {
            TearDownOverlay();
            if (armsRoot != null && ownsInstance) UnityEngine.Object.Destroy(armsRoot.gameObject);
            armsRoot = null;
            copiedBones.Clear();
            armsRenderers = Array.Empty<Renderer>();
        }

        /// <summary>
        /// 팔을 메인 카메라 위에 덮어 그리는 URP 오버레이 카메라를 만든다. 팔 리그를 전용 레이어에 두고,
        /// 메인 카메라는 그 레이어를 빼고 그리며, 오버레이 카메라는 깊이를 지운 뒤 그 레이어만 그린다.
        /// 그래서 벽·문이 손보다 가까워도 손이 가려지지 않는다. 메인 카메라가 바뀌면(씬 전환) 다시 붙인다.
        /// </summary>
        private void EnsureOverlay(FirstPersonArmsSettings settings)
        {
            if (overlayLayer < 0)
            {
                overlayLayer = LayerMask.NameToLayer(settings.overlayLayerName);
                if (overlayLayer < 0)
                {
                    if (!warnedMissingLayer)
                    {
                        warnedMissingLayer = true;
                        Debug.LogWarning($"FirstPersonArmsView: 레이어 '{settings.overlayLayerName}'가 없어 팔을 오버레이 없이 그립니다. " +
                                         "Project Settings > Tags and Layers에 추가하세요.");
                    }

                    return;
                }

                SetLayerRecursively(armsRoot, overlayLayer);
            }

            var main = Camera.main;
            if (main == null) return;
            if (overlayCamera != null && ReferenceEquals(overlayBase, main))
            {
                if (settings.armsFieldOfView > 0f) overlayCamera.fieldOfView = settings.armsFieldOfView;
                else overlayCamera.fieldOfView = main.fieldOfView;
                return;
            }

            TearDownOverlay();
            overlayBase = main;
            overlayBaseCullingMask = main.cullingMask;
            main.cullingMask &= ~(1 << overlayLayer);

            var go = new GameObject("FirstPersonArmsCamera");
            go.transform.SetParent(main.transform, false);
            go.transform.SetLocalPositionAndRotation(Vector3.zero, Quaternion.identity);
            overlayCamera = go.AddComponent<Camera>();
            overlayCamera.CopyFrom(main);
            overlayCamera.cullingMask = 1 << overlayLayer;
            overlayCamera.nearClipPlane = 0.02f;
            overlayCamera.farClipPlane = 10f;
            overlayCamera.fieldOfView = settings.armsFieldOfView > 0f ? settings.armsFieldOfView : main.fieldOfView;
            overlayCamera.tag = "Untagged";
            overlayCamera.targetTexture = null;

            var data = overlayCamera.GetUniversalAdditionalCameraData();
            data.renderType = CameraRenderType.Overlay; // 오버레이는 기본으로 깊이를 지우고 그린다(clearDepth 기본값 true)
            data.renderPostProcessing = false;
            data.renderShadows = false;

            var baseData = main.GetUniversalAdditionalCameraData();
            if (baseData != null && !baseData.cameraStack.Contains(overlayCamera))
                baseData.cameraStack.Add(overlayCamera);
        }

        private void TearDownOverlay()
        {
            if (overlayBase != null)
            {
                var baseData = overlayBase.GetUniversalAdditionalCameraData();
                if (baseData != null && overlayCamera != null) baseData.cameraStack.Remove(overlayCamera);
                if (overlayLayer >= 0 && (overlayBaseCullingMask & (1 << overlayLayer)) != 0)
                    overlayBase.cullingMask |= 1 << overlayLayer;
            }

            if (overlayCamera != null) UnityEngine.Object.Destroy(overlayCamera.gameObject);
            overlayCamera = null;
            overlayBase = null;
        }

        private static void SetLayerRecursively(Transform root, int layer)
        {
            if (root == null) return;
            foreach (var t in root.GetComponentsInChildren<Transform>(true)) t.gameObject.layer = layer;
        }

        /// <summary>
        /// 몸 Animator의 "Punch" 클립을 팔 리그에 여러 시점으로 샘플링해, 손가락이 가장 많이 굽은 시점의 로컬 회전을
        /// 저장한다. 한 번만 시도하고, 샘플링 뒤에는 리그의 모든 본을 원래 값으로 되돌린다.
        /// </summary>
        private void EnsureClenchedFingers()
        {
            if (triedClenchSample || armsRoot == null) return;
            triedClenchSample = true;
            clenchedFingers.Clear();

            var controller = bodyAnimator != null ? bodyAnimator.runtimeAnimatorController : null;
            if (controller == null) return;
            AnimationClip rightClip = null, leftClip = null;
            foreach (var clip in controller.animationClips)
            {
                if (clip == null) continue;
                if (clip.name == "Punch") rightClip = clip;
                else if (clip.name == "Punch_Left") leftClip = clip;
            }

            if (rightClip == null && leftClip == null) return;

            var all = armsRoot.GetComponentsInChildren<Transform>(true);
            var saved = new (Vector3 p, Quaternion r, Vector3 s)[all.Length];
            for (var i = 0; i < all.Length; i++) saved[i] = (all[i].localPosition, all[i].localRotation, all[i].localScale);

            try
            {
                var wasActive = armsRoot.gameObject.activeSelf;
                if (!wasActive) armsRoot.gameObject.SetActive(true);

                var result = new Quaternion[copiedBones.Count];
                for (var i = 0; i < copiedBones.Count; i++) result[i] = Quaternion.identity;
                var found = false;
                // 오른손 손가락은 오른손 펀치 클립에서, 왼손 손가락은 왼손 펀치 클립에서 가장 꽉 쥔 시점을 고른다.
                found |= SampleTightestFist(rightClip ?? leftClip, ".R", result);
                found |= SampleTightestFist(leftClip ?? rightClip, ".L", result);
                if (found) clenchedFingers.AddRange(result);

                if (!wasActive) armsRoot.gameObject.SetActive(false);
            }
            finally
            {
                for (var i = 0; i < all.Length; i++)
                {
                    all[i].localPosition = saved[i].p;
                    all[i].localRotation = saved[i].r;
                    all[i].localScale = saved[i].s;
                }
            }
        }

        /// <summary>
        /// <paramref name="clip"/>을 16단계로 샘플링해 이름이 <paramref name="sideSuffix"/>로 끝나는 손가락 본이
        /// 가장 많이 굽은 시점의 로컬 회전을 <paramref name="result"/>에 써 넣는다.
        /// </summary>
        private bool SampleTightestFist(AnimationClip clip, string sideSuffix, Quaternion[] result)
        {
            if (clip == null) return false;
            clip.SampleAnimation(armsRoot.gameObject, 0f);
            var open = new Quaternion[copiedBones.Count];
            for (var i = 0; i < copiedBones.Count; i++) open[i] = copiedBones[i].arms.localRotation;

            var best = new Quaternion[copiedBones.Count];
            var bestScore = -1f;
            const int steps = 16;
            for (var step = 1; step <= steps; step++)
            {
                clip.SampleAnimation(armsRoot.gameObject, clip.length * step / steps);
                var score = 0f;
                for (var i = 0; i < copiedBones.Count; i++)
                {
                    if (!IsFingerOfSide(i, sideSuffix)) continue;
                    score += Quaternion.Angle(open[i], copiedBones[i].arms.localRotation);
                }

                if (score <= bestScore) continue;
                bestScore = score;
                for (var i = 0; i < copiedBones.Count; i++) best[i] = copiedBones[i].arms.localRotation;
            }

            if (bestScore <= 0f) return false;
            for (var i = 0; i < copiedBones.Count; i++)
                if (IsFingerOfSide(i, sideSuffix)) result[i] = best[i];
            return true;
        }

        private bool IsFingerOfSide(int index, string sideSuffix) =>
            copiedBones[index].finger &&
            copiedBones[index].arms.name.EndsWith(sideSuffix, StringComparison.Ordinal);

        /// <summary>어깨(위팔 뿌리)에서 손으로 향하는 방향이 <paramref name="target"/>을 향하도록 위팔을 돌린다.</summary>
        private static void AimArmAt(Transform upper, Transform hand, Vector3 target, float weight)
        {
            if (upper == null || hand == null) return;
            var current = hand.position - upper.position;
            var desired = target - upper.position;
            if (current.sqrMagnitude < 1e-6f || desired.sqrMagnitude < 1e-6f) return;
            var turn = Quaternion.FromToRotation(current, desired);
            upper.rotation = Quaternion.Slerp(Quaternion.identity, turn, Mathf.Clamp01(weight)) * upper.rotation;
        }

        private static void PoseArm(
            Transform upper, Transform fore, Transform hand, float side, FirstPersonArmPose s, float weight,
            Transform camera)
        {
            var right = camera.right;
            var forward = camera.forward;
            if (upper != null)
            {
                // 늘어진 팔(-up)을 카메라 오른쪽 축 기준 음의 각도로 돌리면 앞으로 올라온다.
                var offset = Quaternion.AngleAxis(side * s.upperArmSpread, forward) *
                             Quaternion.AngleAxis(-s.upperArmLift, right);
                upper.rotation = Quaternion.Slerp(Quaternion.identity, offset, weight) * upper.rotation;
            }

            if (fore != null)
            {
                var offset = Quaternion.AngleAxis(side * s.forearmSpread, forward) *
                             Quaternion.AngleAxis(-s.forearmBend, right);
                fore.rotation = Quaternion.Slerp(Quaternion.identity, offset, weight) * fore.rotation;
            }

            if (hand != null)
            {
                var offset = Quaternion.AngleAxis(side * s.handSpread, forward) *
                             Quaternion.AngleAxis(-s.handBend, right);
                hand.rotation = Quaternion.Slerp(Quaternion.identity, offset, weight) * hand.rotation;
            }
        }

        private static bool IsCopied(string boneName)
        {
            foreach (var prefix in CopiedBonePrefixes)
                if (boneName.StartsWith(prefix, StringComparison.Ordinal)) return true;
            return false;
        }

        private static Dictionary<string, Transform> Collect(Transform root)
        {
            var map = new Dictionary<string, Transform>();
            foreach (var t in root.GetComponentsInChildren<Transform>(true))
                map.TryAdd(t.name, t);
            return map;
        }
    }
}

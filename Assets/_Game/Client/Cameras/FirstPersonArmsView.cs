using System;
using System.Collections.Generic;
using UnityEngine;

namespace Game.Client.Cameras
{
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

        [Header("팔 자세 — 몸 애니메이션 위에 덧씌우는 각도")]
        [Tooltip("0이면 몸 애니메이션 그대로(팔이 아래로 늘어짐), 1이면 아래 각도를 전부 적용")]
        [Range(0f, 1f)]
        public float poseWeight = 1f;

        [Tooltip("위팔을 앞으로 들어올리는 각도(도). 90이면 시선과 수평")]
        [Range(-90f, 120f)]
        public float upperArmLift = 70f;

        [Tooltip("위팔을 바깥으로 벌리는 각도(도). 클수록 손이 화면 양옆으로")]
        [Range(-60f, 60f)]
        public float upperArmSpread = 20f;

        [Tooltip("아래팔을 앞·위로 굽히는 각도(도)")]
        [Range(-30f, 150f)]
        public float forearmBend = 20f;

        [Tooltip("아래팔을 바깥으로 벌리는 각도(도)")]
        [Range(-60f, 60f)]
        public float forearmSpread = 0f;

        [Tooltip("손을 위로 꺾는 각도(도). 손바닥이 더 정면을 보게 한다")]
        [Range(-90f, 90f)]
        public float handBend = 0f;

        [Tooltip("손을 바깥으로 돌리는 각도(도)")]
        [Range(-90f, 90f)]
        public float handSpread = 0f;
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

        private readonly List<(Transform arms, Transform body)> copiedBones = new();
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
                copiedBones.Add((pair.Value, bodyBone));
            }

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
        public void Apply(bool visible, FirstPersonArmsSettings settings, Transform camera)
        {
            if (armsRoot == null) return;
            if (!visible || settings == null || camera == null)
            {
                Hide();
                return;
            }

            if (!armsRoot.gameObject.activeSelf) armsRoot.gameObject.SetActive(true);

            // 리그를 카메라 방향(+기울기)으로 돌리고, 머리 본이 카메라 앵커에 오도록 루트를 놓는다.
            armsRoot.localScale = bodyScale * Mathf.Max(0.01f, settings.scale);
            armsRoot.rotation = camera.rotation * Quaternion.Euler(settings.rootTilt);
            armsRoot.position = camera.TransformPoint(settings.eyeAnchor) - armsRoot.TransformVector(eyeLocal);

            foreach (var (arms, body) in copiedBones)
            {
                arms.localRotation = body.localRotation;
                arms.localPosition = body.localPosition;
                arms.localScale = body.localScale;
            }

            var weight = Mathf.Clamp01(settings.poseWeight);
            if (weight > 0f)
            {
                PoseArm(upperArmRight, forearmRight, handRight, +1f, settings, weight, camera);
                PoseArm(upperArmLeft, forearmLeft, handLeft, -1f, settings, weight, camera);
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
            if (armsRoot != null && ownsInstance) UnityEngine.Object.Destroy(armsRoot.gameObject);
            armsRoot = null;
            copiedBones.Clear();
            armsRenderers = Array.Empty<Renderer>();
        }

        private static void PoseArm(
            Transform upper, Transform fore, Transform hand, float side, FirstPersonArmsSettings s, float weight,
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

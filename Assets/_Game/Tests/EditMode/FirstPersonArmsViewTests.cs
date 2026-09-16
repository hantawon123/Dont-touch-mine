using Game.Client.Cameras;
using NUnit.Framework;
using UnityEngine;

namespace Game.Tests.EditMode
{
    public sealed class FirstPersonArmsViewTests
    {
        private GameObject player;
        private Transform visual;
        private Transform cameraRig;
        private Transform armsInstance;

        [SetUp]
        public void SetUp()
        {
            player = new GameObject("Player");
            visual = Child(player.transform, "Visual");
            visual.localScale = Vector3.one * 0.55f;
            BuildRig(visual, "Body");

            cameraRig = new GameObject("PlayerCameraRig").transform;
            cameraRig.SetPositionAndRotation(new Vector3(3f, 1.6f, -2f), Quaternion.Euler(0f, 30f, 0f));

            armsInstance = new GameObject("SmoothBear_FirstPersonArms").transform;
            BuildRig(armsInstance, "Arms");
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(player);
            Object.DestroyImmediate(cameraRig.gameObject);
            if (armsInstance != null) Object.DestroyImmediate(armsInstance.gameObject);
        }

        /// <summary>SmoothBear와 같은 본 이름의 최소 뼈대와 스킨드 렌더러 하나.</summary>
        private static void BuildRig(Transform root, string meshName)
        {
            var armature = Child(root, "DGN_Armature");
            var hips = Child(armature, "Hips", new Vector3(0f, 0.46f, 0f));
            var spine = Child(hips, "Spine", new Vector3(0f, 0.36f, 0f));
            var neck = Child(spine, "Neck", new Vector3(0f, 0.37f, 0f));
            Child(neck, "Head", new Vector3(0f, 0.09f, 0f));
            foreach (var (side, sign) in new[] { ("L", 1f), ("R", -1f) })
            {
                var shoulder = Child(spine, "Shoulder." + side, new Vector3(sign * 0.16f, 0.33f, 0f));
                var upper = Child(shoulder, "UpperArm." + side, new Vector3(sign * 0.17f, -0.05f, 0f));
                var fore = Child(upper, "Arm." + side, new Vector3(sign * 0.2f, -0.17f, 0f));
                var hand = Child(fore, "Hand." + side, new Vector3(sign * 0.13f, -0.13f, 0f));
                Child(hand, "Finger_M1." + side, new Vector3(sign * 0.12f, -0.1f, 0f));
            }

            var mesh = Child(root, meshName);
            mesh.gameObject.AddComponent<SkinnedMeshRenderer>();
        }

        private static Transform Child(Transform parent, string name, Vector3 localPosition = default)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPosition;
            return go.transform;
        }

        private static Transform Find(Transform root, string name)
        {
            foreach (var t in root.GetComponentsInChildren<Transform>(true))
                if (t.name == name) return t;
            return null;
        }

        private FirstPersonArmsView BoundView()
        {
            var view = new FirstPersonArmsView();
            view.BindWithInstance(visual, cameraRig, armsInstance);
            return view;
        }

        private static FirstPersonArmsSettings Settings(float weight = 0f) =>
            new() { poseWeight = weight, eyeAnchor = new Vector3(0f, -0.02f, 0.06f) };

        [Test]
        public void Bind_ParentsUnderCameraRig_MatchesBodyScale_AndStartsHidden()
        {
            var view = BoundView();

            Assert.IsTrue(view.IsReady);
            Assert.AreSame(cameraRig, armsInstance.parent);
            Assert.AreEqual(0.55f, armsInstance.localScale.x, 1e-5f);
            Assert.IsFalse(armsInstance.gameObject.activeSelf);
        }

        [Test]
        public void Apply_Visible_PutsHeadBoneOnCameraAnchor_AndFacesWithCamera()
        {
            var view = BoundView();
            var settings = Settings();

            view.Apply(true, settings, cameraRig);

            Assert.IsTrue(armsInstance.gameObject.activeSelf);
            var head = Find(armsInstance, "Head");
            var anchor = cameraRig.TransformPoint(settings.eyeAnchor);
            Assert.Less((head.position - anchor).magnitude, 1e-4f, "head bone sits on the camera anchor");
            Assert.Less(Quaternion.Angle(armsInstance.rotation, cameraRig.rotation), 1e-3f);
        }

        [Test]
        public void Apply_CameraPitchedDown_HeadStaysOnAnchorAndRigTiltsWithCamera()
        {
            var view = BoundView();
            var settings = Settings();
            cameraRig.rotation = Quaternion.Euler(60f, 30f, 0f);

            view.Apply(true, settings, cameraRig);

            var head = Find(armsInstance, "Head");
            Assert.Less((head.position - cameraRig.TransformPoint(settings.eyeAnchor)).magnitude, 1e-4f);
            Assert.Less(Quaternion.Angle(armsInstance.rotation, cameraRig.rotation), 1e-3f);
            // 손은 카메라 기준으로 같은 자리: 카메라 공간 좌표가 수평 시선일 때와 같다.
            var hand = Find(armsInstance, "Hand.L");
            var pitched = cameraRig.InverseTransformPoint(hand.position);
            cameraRig.rotation = Quaternion.Euler(0f, 30f, 0f);
            view.Apply(true, settings, cameraRig);
            var level = cameraRig.InverseTransformPoint(hand.position);
            Assert.Less((pitched - level).magnitude, 1e-4f, "hand keeps its screen position while pitching");
        }

        [Test]
        public void Apply_CopiesArmBoneRotationsFromBody_NotTorso()
        {
            var view = BoundView();
            var bodyUpper = Find(visual, "UpperArm.L");
            var bodySpine = Find(visual, "Spine");
            bodyUpper.localRotation = Quaternion.Euler(10f, 20f, 30f);
            bodySpine.localRotation = Quaternion.Euler(45f, 0f, 0f);

            view.Apply(true, Settings(weight: 0f), cameraRig);

            Assert.Less(Quaternion.Angle(Find(armsInstance, "UpperArm.L").localRotation, bodyUpper.localRotation), 1e-3f);
            Assert.Less(Quaternion.Angle(Find(armsInstance, "Spine").localRotation, Quaternion.identity), 1e-3f,
                "torso bones stay in rest so the head anchor is stable");
        }

        [Test]
        public void Apply_WithPoseWeight_LiftsUpperArmsTowardCameraForward()
        {
            var view = BoundView();
            var settings = Settings(weight: 1f);
            settings.upperArmLift = 90f; settings.upperArmSpread = 0f; settings.forearmBend = 0f;

            view.Apply(true, settings, cameraRig);

            // 몸 기준으로 늘어진 팔(-up)이 카메라 앞을 향한다.
            var upper = Find(armsInstance, "UpperArm.L");
            Assert.Greater(Vector3.Dot(upper.rotation * Vector3.down, cameraRig.forward), 0.95f);
        }

        [Test]
        public void Apply_ScaleAndTilt_ApplyOnTopOfBodyScaleAndCameraRotation()
        {
            var view = BoundView();
            var settings = Settings();
            settings.scale = 1.5f;
            settings.rootTilt = new Vector3(10f, 0f, 0f);

            view.Apply(true, settings, cameraRig);

            Assert.AreEqual(0.55f * 1.5f, armsInstance.localScale.x, 1e-5f);
            Assert.Less(Quaternion.Angle(armsInstance.rotation, cameraRig.rotation * Quaternion.Euler(10f, 0f, 0f)), 1e-3f);
            var head = Find(armsInstance, "Head");
            Assert.Less((head.position - cameraRig.TransformPoint(settings.eyeAnchor)).magnitude, 1e-4f,
                "head stays on the anchor whatever the scale and tilt");
        }

        [Test]
        public void Apply_NotVisible_HidesArms()
        {
            var view = BoundView();
            view.Apply(true, Settings(), cameraRig);
            Assert.IsTrue(armsInstance.gameObject.activeSelf);

            view.Apply(false, Settings(), cameraRig);
            Assert.IsFalse(armsInstance.gameObject.activeSelf);
        }

        [Test]
        public void Apply_CopiesBodyMaterialsAndDisablesShadows()
        {
            var bodyRenderer = Find(visual, "Body").GetComponent<SkinnedMeshRenderer>();
            var material = new Material(Shader.Find("Hidden/InternalErrorShader"));
            bodyRenderer.sharedMaterial = material;

            var view = BoundView();
            view.Apply(true, Settings(), cameraRig);

            var armsRenderer = Find(armsInstance, "Arms").GetComponent<SkinnedMeshRenderer>();
            Assert.AreSame(material, armsRenderer.sharedMaterial);
            Assert.AreEqual(UnityEngine.Rendering.ShadowCastingMode.Off, armsRenderer.shadowCastingMode);
            Assert.IsTrue(armsRenderer.updateWhenOffscreen);
            Object.DestroyImmediate(material);
        }

        [Test]
        public void Bind_WithoutVisual_DoesNotThrow()
        {
            var view = new FirstPersonArmsView();
            Assert.DoesNotThrow(() => view.Bind(null, cameraRig, Settings()));
            Assert.IsFalse(view.IsReady);
            Assert.DoesNotThrow(() => view.Apply(true, Settings(), cameraRig));
            Assert.DoesNotThrow(() => view.Dispose());
        }
    }
}

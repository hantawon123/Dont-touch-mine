using UnityEngine;
using UnityEngine.UI;

namespace Game.Client.Character
{
    public sealed partial class CharacterClosetView
    {
        private GameObject generatedPreviewRoot;
        private RenderTexture generatedPreviewTexture;

        private void CreateGeneratedPreview(RectTransform parent)
        {
            if (previewCharacter != null || !Application.isPlaying) return;
            var prefab = Resources.Load<GameObject>("AvatarPreview");
            if (prefab == null) return;

            generatedPreviewRoot = new GameObject("ClosetCharacterStage");
            generatedPreviewRoot.transform.position = new Vector3(10000 + (Mathf.Abs(GetInstanceID()) % 1000) * 20, 10000, 10000);
            var character = Instantiate(prefab, generatedPreviewRoot.transform);
            character.transform.localPosition = Vector3.zero;
            character.transform.localRotation = Quaternion.identity;
            foreach (var child in character.GetComponentsInChildren<Transform>(true)) child.gameObject.layer = 31;
            previewCharacter = character.GetComponent<AvatarAppearanceApplier>();

            var cameraObject = new GameObject("PortraitCamera");
            cameraObject.transform.SetParent(generatedPreviewRoot.transform, false);
            cameraObject.transform.localPosition = new Vector3(.25f, 1.30f, 5);
            cameraObject.transform.LookAt(generatedPreviewRoot.transform.position + Vector3.up * 1.25f);
            var camera = cameraObject.AddComponent<Camera>();
            camera.cullingMask = 1 << 31;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = Color.clear;
            camera.orthographic = true;
            camera.orthographicSize = 1.55f;
            camera.nearClipPlane = .1f;
            camera.farClipPlane = 10;
            camera.allowHDR = false;
            camera.allowMSAA = false;
            generatedPreviewTexture = new RenderTexture(512, 640, 24, RenderTextureFormat.ARGB32) { name = "Closet portrait" };
            camera.targetTexture = generatedPreviewTexture;

            var lightObject = new GameObject("PortraitLight");
            lightObject.transform.SetParent(cameraObject.transform, false);
            lightObject.transform.localPosition = new Vector3(-2, 2, 1);
            var light = lightObject.AddComponent<Light>();
            // A local point light cannot replace the world's URP main light.
            light.type = LightType.Point;
            light.range = 10;
            light.intensity = 10;
            light.cullingMask = 1 << 31;

            var fillObject=new GameObject("PortraitFillLight");
            fillObject.transform.SetParent(cameraObject.transform,false);
            fillObject.transform.localPosition=new Vector3(2,1,1);
            var fill=fillObject.AddComponent<Light>();
            fill.type=LightType.Point;fill.range=10;fill.intensity=9;fill.cullingMask=1<<31;

            // Soft frontal fill lifts the lower torso and shoes without bleaching the key highlights.
            var bounceObject=new GameObject("PortraitBounceLight");
            bounceObject.transform.SetParent(cameraObject.transform,false);
            bounceObject.transform.localPosition=new Vector3(0,-1.2f,2);
            var bounce=bounceObject.AddComponent<Light>();
            bounce.type=LightType.Point;bounce.range=10;bounce.intensity=3;bounce.cullingMask=1<<31;

            var portrait = CreateRect("CharacterPortrait", parent);
            SetAnchor(portrait, new Vector2(.5f, .5f), new Vector2(.5f, .5f), new Vector2(.5f, .5f));
            portrait.anchoredPosition = new Vector2(-30, lobbyOverlay ? 10 : 25);
            portrait.sizeDelta = lobbyOverlay ? new Vector2(480, 600) : new Vector2(560, 700);
            var image = portrait.gameObject.AddComponent<RawImage>();
            image.texture = generatedPreviewTexture;
            image.raycastTarget = true;
            portrait.gameObject.AddComponent<AvatarPreviewOrbit>().Initialize(camera,generatedPreviewRoot.transform);
            var hint=CreateText("PortraitControlsHint",portrait,"좌우 드래그로 회전 · 두 번 클릭해 정면",20,
                Color.black,TMPro.TextAlignmentOptions.Center);
            SetAnchor(hint,new Vector2(.5f,0),new Vector2(.5f,0),new Vector2(.5f,1));
            hint.anchoredPosition=new Vector2(0,-8);hint.sizeDelta=new Vector2(560,32);
            generatedPreviewRoot.SetActive(isActiveAndEnabled);
        }

        private void ReleaseGeneratedPreview()
        {
            if (generatedPreviewRoot != null) Destroy(generatedPreviewRoot);
            if (generatedPreviewTexture != null)
            {
                generatedPreviewTexture.Release();
                Destroy(generatedPreviewTexture);
            }
        }
    }
}

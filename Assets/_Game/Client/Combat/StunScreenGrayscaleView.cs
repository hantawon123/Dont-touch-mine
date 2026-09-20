using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Game.Client.Combat
{
    /// <summary>
    /// Desaturates the local camera while this combatant is stunned.
    /// Remote copies never touch the volume, so only the stunned player's screen goes gray.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class StunScreenGrayscaleView : MonoBehaviour
    {
        public const string VolumeName = "StunScreenGrayscale";
        public const float Saturation = -100f;
        public const float Priority = 50f;

        private PlayerCombatant combatant;
        private Volume volume;
        private bool? previousPostProcessing;
        private Camera postProcessingCamera;

        public bool IsActive { get; private set; }

        private void Awake()
        {
            combatant = GetComponent<PlayerCombatant>();
        }

        private void OnEnable()
        {
            Apply();
        }

        private void LateUpdate()
        {
            Apply();
        }

        public void Apply()
        {
            combatant ??= GetComponent<PlayerCombatant>();
            var active = combatant != null && combatant.PresentsLocalScreen && combatant.IsStunned;
            SetActive(active);
        }

        private void SetActive(bool active)
        {
            if (IsActive == active && (!active || volume != null))
            {
                return;
            }

            IsActive = active;
            if (!active)
            {
                if (volume != null)
                {
                    volume.weight = 0f;
                    volume.enabled = false;
                }

                RestorePostProcessing();
                return;
            }

            EnsureVolume();
            volume.enabled = true;
            volume.weight = 1f;
            EnsurePostProcessing();
        }

        private void EnsureVolume()
        {
            if (volume != null)
            {
                return;
            }

            var host = new GameObject(VolumeName);
            host.hideFlags = HideFlags.HideAndDontSave;
            host.transform.SetParent(transform, false);
            volume = host.AddComponent<Volume>();
            volume.isGlobal = true;
            volume.priority = Priority;
            volume.profile = ScriptableObject.CreateInstance<VolumeProfile>();
            var color = volume.profile.Add<ColorAdjustments>(true);
            color.saturation.Override(Saturation);
        }

        private void EnsurePostProcessing()
        {
            var camera = Camera.main;
            if (camera == null)
            {
                return;
            }

            var data = camera.GetUniversalAdditionalCameraData();
            if (data == null)
            {
                return;
            }

            if (postProcessingCamera != camera)
            {
                RestorePostProcessing();
                postProcessingCamera = camera;
                previousPostProcessing = data.renderPostProcessing;
            }

            data.renderPostProcessing = true;
        }

        private void RestorePostProcessing()
        {
            if (postProcessingCamera != null && previousPostProcessing.HasValue)
            {
                var data = postProcessingCamera.GetUniversalAdditionalCameraData();
                if (data != null)
                {
                    data.renderPostProcessing = previousPostProcessing.Value;
                }
            }

            postProcessingCamera = null;
            previousPostProcessing = null;
        }

        private void OnDisable()
        {
            SetActive(false);
        }

        private void OnDestroy()
        {
            SetActive(false);
            if (volume != null)
            {
                if (volume.profile != null)
                {
                    DestroyOwned(volume.profile);
                }

                DestroyOwned(volume.gameObject);
                volume = null;
            }
        }

        private static void DestroyOwned(Object owned)
        {
            if (owned == null)
            {
                return;
            }

            if (Application.isPlaying)
            {
                Destroy(owned);
            }
            else
            {
                DestroyImmediate(owned);
            }
        }
    }
}

using System;
using System.Collections.Generic;
using Game.Client.Cameras;
using Game.Client.Interactions;
using UnityEngine;

namespace Game.Bootstrap
{
    public sealed partial class HighlightCameraDirector
    {
        private readonly IReadOnlyList<HighlightCctvCamera> cctvCameras;
        private HighlightCctvCamera activeCctv, pendingCctv;
        private float cctvHold, cctvCheck, cctvFade;
        private double cctvPlaybackTime;
        private Quaternion cctvRotation;
        private float cctvFieldOfView;
        private readonly List<Bounds> cctvOccluders = new();
        public string CctvLocation => activeCctv == null ? "" : activeCctv.LocationName;
        public float CctvOpacity => cctvFade <= 0f ? 0f : 1f - Mathf.Abs(cctvFade - 0.2f) / 0.2f;
        public Vector3? CctvPosition => activeCctv == null ? null : activeCctv.transform.position;

        private void CaptureCctvOccluders()
        {
            if (cctvCameras.Count == 0 || cctvCameras[0] == null || collisionLayerMask == 0) return;
            var subjects = new HashSet<Renderer>();
            foreach (var renderers in replayRenderers.Values) subjects.UnionWith(renderers);
            foreach (var root in cctvCameras[0].gameObject.scene.GetRootGameObjects())
            foreach (var renderer in root.GetComponentsInChildren<MeshRenderer>())
            {
                if (subjects.Contains(renderer) || !renderer.enabled || renderer.forceRenderingOff ||
                    renderer.bounds.size.y < 0.5f ||
                    (collisionLayerMask & (1 << renderer.gameObject.layer)) == 0 ||
                    renderer.GetComponentInParent<CarryableItem>() != null ||
                    renderer.GetComponentInParent<Animator>() != null ||
                    renderer.sharedMaterial != null && renderer.sharedMaterial.renderQueue > 2500) continue;
                // Static furniture can have no collider, and gameplay item colliders are disabled during replay.
                // Bounds are conservative: use authored visibility volumes if hollow props need finer coverage.
                cctvOccluders.Add(renderer.bounds);
            }
        }

        private bool IsCctvOccluded(Vector3 origin, Vector3 focus)
        {
            if (collisionLayerMask == 0) return false;
            // Judge what is rendered, not hidden live-avatar or invisible gameplay colliders.
            var ray = new Ray(origin, (focus - origin).normalized);
            var distance = Vector3.Distance(origin, focus);
            foreach (var bounds in cctvOccluders)
                if (!bounds.Contains(origin) && bounds.IntersectRay(ray, out var entry) &&
                    entry > 0.05f && entry < distance - 0.1f) return true;
            return false;
        }

        private void ResetCctv()
        {
            activeCctv = pendingCctv = null;
            cctvHold = cctvCheck = cctvFade = 0f;
        }

        private void AdvanceCctv(float delta)
        {
            cctvHold += delta;
            cctvCheck -= delta;
            if (cctvFade <= 0f) return;
            cctvFade = Mathf.Max(0f, cctvFade - delta);
            if (pendingCctv != null && cctvFade <= 0.2f)
            {
                SetCctv(pendingCctv);
                pendingCctv = null;
            }
        }

        private void SetCctv(HighlightCctvCamera camera)
        {
            activeCctv = camera;
            cctvRotation = camera.transform.rotation;
            cctvFieldOfView = camera.FieldOfView;
            cctvHold = 0f;
            if (replayCameraRig != null) replayCameraRig.SetFieldOfView(camera.FieldOfView);
            else if (cameraTransform.TryGetComponent<Camera>(out var output)) output.fieldOfView = camera.FieldOfView;
        }

        private void ApplyCctvPose(float interpolation)
        {
            if (currentTarget == null) return;
            // A destroyed replay item is inactive; keep the actor in view for the consequence.
            var target = currentTarget.gameObject.activeInHierarchy
                ? currentTarget : ResolvePlayer(currentHighlight.ActorPlayerIndex) ?? currentTarget;
            var subject = target.position + Vector3.up * 0.5f;
            var focus = subject;
            if (supportingPlayer != null && Vector3.Distance(subject, supportingPlayer.position) < 8f)
                focus = (subject + supportingPlayer.position + Vector3.up) * 0.5f;
            if (activeCctv == null || cctvCheck <= 0f && pendingCctv == null)
            {
                cctvCheck = 0.25f;
                HighlightCctvCamera best = null;
                var bestScore = float.NegativeInfinity;
                foreach (var camera in cctvCameras)
                {
                    if (camera == null) continue;
                    var score = CctvScore(camera, focus);
                    if (score > bestScore) { best = camera; bestScore = score; }
                }
                if (best != null && best != activeCctv)
                {
                    if (activeCctv == null) SetCctv(best);
                    else if (cctvHold >= 2f && NeedsCctvSwitch(focus) && bestScore > CctvScore(activeCctv, focus) + 5f &&
                        Math.Abs(cctvPlaybackTime - HighlightShotPlanner.PlaybackTimeOf(currentHighlight, currentHighlight.EventAt)) > 1.2d)
                    {
                        pendingCctv = best;
                        cctvFade = 0.4f;
                    }
                }
            }
            if (activeCctv == null) return;
            var position = activeCctv.transform.position;
            var direction = focus - position;
            if (direction.sqrMagnitude > 0.001f)
            {
                var limited = Quaternion.RotateTowards(activeCctv.transform.rotation,
                    Quaternion.LookRotation(direction), 75f);
                cctvRotation = Quaternion.Slerp(cctvRotation, limited, interpolation);
            }
            // Optical framing keeps the installed camera fixed while making the action readable.
            var radius = CurrentShot?.Framing switch
            {
                HighlightShotFraming.Close => 2f,
                HighlightShotFraming.Medium => 3f,
                _ => 5f,
            };
            if (supportingPlayer != null && Vector3.Distance(subject, supportingPlayer.position) < 8f)
                radius = Mathf.Max(radius, Vector3.Distance(subject, supportingPlayer.position) * 0.5f + 1.5f);
            var fieldOfView = Mathf.Clamp(2f * Mathf.Atan2(radius, direction.magnitude) * Mathf.Rad2Deg,
                22f, activeCctv.FieldOfView);
            cctvFieldOfView = Mathf.Lerp(cctvFieldOfView, fieldOfView, interpolation);
            if (replayCameraRig != null) replayCameraRig.SetFieldOfView(cctvFieldOfView);
            else if (cameraTransform.TryGetComponent<Camera>(out var output)) output.fieldOfView = cctvFieldOfView;
            // Switch mounting points behind the fade, never fly through the map.
            if (replayCameraRig != null) replayCameraRig.SetPose(position, cctvRotation, 1f, true);
            else cameraTransform.SetPositionAndRotation(position, cctvRotation);
        }

        private bool NeedsCctvSwitch(Vector3 focus)
        {
            var direction = focus - activeCctv.transform.position;
            return direction.magnitude > 30f ||
                Vector3.Angle(activeCctv.transform.forward, direction) > 75f ||
                IsCctvOccluded(activeCctv.transform.position, focus);
        }

        private float CctvScore(HighlightCctvCamera camera, Vector3 focus)
        {
            var direction = focus - camera.transform.position;
            var angle = Vector3.Angle(camera.transform.forward, direction);
            var score = 100f - direction.magnitude - angle * 0.5f;
            if (angle > 75f) score -= 200f;
            if (IsCctvOccluded(camera.transform.position, focus)) score -= 300f;
            return score;
        }
    }
}

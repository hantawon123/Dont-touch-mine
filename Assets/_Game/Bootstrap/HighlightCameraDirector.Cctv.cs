using System;
using System.Collections.Generic;
using Game.Client.Cameras;
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
        public string CctvLocation => activeCctv == null ? "" : activeCctv.LocationName;
        public float CctvOpacity => cctvFade <= 0f ? 0f : 1f - Mathf.Abs(cctvFade - 0.2f) / 0.2f;
        public Vector3? CctvPosition => activeCctv == null ? null : activeCctv.transform.position;

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
            cctvHold = 0f;
            if (replayCameraRig != null) replayCameraRig.SetFieldOfView(camera.FieldOfView);
            else if (cameraTransform.TryGetComponent<Camera>(out var output)) output.fieldOfView = camera.FieldOfView;
        }

        private void ApplyCctvPose(float interpolation)
        {
            if (currentTarget == null) return;
            var subject = currentTarget.position + Vector3.up * 0.5f;
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
                    else if (cctvHold >= 2f && bestScore > CctvScore(activeCctv, focus) + 5f &&
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
                    Quaternion.LookRotation(direction), 35f);
                cctvRotation = Quaternion.Slerp(cctvRotation, limited, interpolation);
            }
            // Switch mounting points behind the fade, never fly through the map.
            if (replayCameraRig != null) replayCameraRig.SetPose(position, cctvRotation, 1f, true);
            else cameraTransform.SetPositionAndRotation(position, cctvRotation);
        }

        private float CctvScore(HighlightCctvCamera camera, Vector3 focus)
        {
            var direction = focus - camera.transform.position;
            var angle = Vector3.Angle(camera.transform.forward, direction);
            var score = 100f - direction.magnitude - angle * 0.5f;
            if (angle > 55f) score -= 200f;
            if (collisionLayerMask != 0 && Physics.Linecast(camera.transform.position, focus,
                out var hit, collisionLayerMask, QueryTriggerInteraction.Ignore) &&
                !IsReplaySubject(hit.transform)) score -= 300f;
            return score;
        }
    }
}

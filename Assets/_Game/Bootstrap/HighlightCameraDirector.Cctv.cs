using System.Collections.Generic;
using Game.Client.Cameras;
using Game.Client.Interactions;
using UnityEngine;

namespace Game.Bootstrap
{
    public sealed partial class HighlightCameraDirector
    {
        private const int MaxCctvSwitches = 2;
        private const float CctvLookAheadSeconds = 0.75f;
        private const double FinalCctvSwitchStartRatio = 0.6d;
        private readonly IReadOnlyList<HighlightCctvCamera> cctvCameras;
        private HighlightCctvCamera activeCctv;
        private float cctvHold, cctvCheck, cctvSampleElapsed;
        private int cctvSwitchCount;
        private Vector3 previousCctvFocus;
        private bool hasPreviousCctvFocus;
        private Transform cctvTarget;
        private readonly List<Bounds> cctvOccluders = new();
        public string CctvLocation => activeCctv == null ? "" : activeCctv.LocationName;
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

        private bool IsCctvOccluded(Vector3 origin, Vector3 focus, Transform subject)
        {
            if (collisionLayerMask == 0) return false;
            // Judge what is rendered, not hidden live-avatar or invisible gameplay colliders.
            var ray = new Ray(origin, (focus - origin).normalized);
            var distance = Vector3.Distance(origin, focus);
            foreach (var bounds in cctvOccluders)
                if (!bounds.Contains(origin) && bounds.IntersectRay(ray, out var entry) &&
                    entry > 0.05f && entry < distance - 0.1f) return true;
            // Replay actors and objects move, so evaluate their current rendered bounds on each check.
            foreach (var pair in replayRenderers)
            {
                if (pair.Key == null || pair.Key == subject ||
                    !pair.Key.gameObject.activeInHierarchy) continue;
                foreach (var renderer in pair.Value)
                    if (renderer != null && renderer.enabled && !renderer.forceRenderingOff &&
                        renderer.bounds.IntersectRay(ray, out var entry) &&
                        entry > 0.05f && entry < distance - 0.1f) return true;
            }
            return false;
        }

        private void ResetCctv()
        {
            activeCctv = null;
            cctvHold = cctvCheck = cctvSampleElapsed = 0f;
            cctvSwitchCount = 0;
            hasPreviousCctvFocus = false;
        }

        private void AdvanceCctv(float delta)
        {
            cctvHold += delta;
            cctvCheck -= delta;
            cctvSampleElapsed += delta;
        }

        private void ResetCctvPrediction()
        {
            cctvSampleElapsed = 0f;
            hasPreviousCctvFocus = false;
        }

        private void SetCctv(HighlightCctvCamera camera)
        {
            if (activeCctv != null) cctvSwitchCount++;
            activeCctv = camera;
            cctvHold = 0f;
            if (replayCameraRig != null) replayCameraRig.SetFieldOfView(camera.FieldOfView);
            else if (cameraTransform.TryGetComponent<Camera>(out var output)) output.fieldOfView = camera.FieldOfView;
        }

        private void ApplyCctvPose()
        {
            if (currentTarget == null) return;
            // A destroyed replay item is inactive; keep the actor in view for the consequence.
            cctvTarget = currentTarget.gameObject.activeInHierarchy
                ? currentTarget : ResolvePlayer(currentHighlight.ActorPlayerIndex) ?? currentTarget;
            var subject = cctvTarget.position + Vector3.up * 0.5f;
            var focus = subject;
            if (supportingPlayer != null && Vector3.Distance(subject, supportingPlayer.position) < 8f)
                focus = (subject + supportingPlayer.position + Vector3.up) * 0.5f;
            if (activeCctv == null || cctvCheck <= 0f)
            {
                cctvCheck = 0.25f;
                var velocity = hasPreviousCctvFocus && cctvSampleElapsed > 0f
                    ? (focus - previousCctvFocus) / cctvSampleElapsed
                    : Vector3.zero;
                var predictedFocus = focus + velocity * CctvLookAheadSeconds;
                previousCctvFocus = focus;
                hasPreviousCctvFocus = true;
                cctvSampleElapsed = 0f;
                HighlightCctvCamera best = null;
                var bestScore = float.NegativeInfinity;
                foreach (var camera in cctvCameras)
                {
                    if (camera == null) continue;
                    var score = CctvScore(camera, focus);
                    if (velocity.sqrMagnitude > 0.01f &&
                        !CanCctvSeePoint(camera, cctvTarget, predictedFocus)) score -= 600f;
                    if (score > bestScore) { best = camera; bestScore = score; }
                }
                if (best != null && best != activeCctv)
                {
                    if (activeCctv == null) SetCctv(best);
                    else if (CanSwitchCctv())
                    {
                        var currentVisible = CanCctvSeeSubjects(activeCctv);
                        var currentWillStayVisible = velocity.sqrMagnitude <= 0.01f ||
                            CanCctvSeePoint(activeCctv, cctvTarget, predictedFocus);
                        var bestVisible = CanCctvSeeSubjects(best) &&
                            (velocity.sqrMagnitude <= 0.01f ||
                             CanCctvSeePoint(best, cctvTarget, predictedFocus));
                        // Cut immediately when the shot is blocked. A stable movement trend may
                        // trigger the same hard cut shortly before the subject leaves the view.
                        if (bestVisible && (!currentVisible ||
                            cctvHold >= 0.5f && !currentWillStayVisible)) SetCctv(best);
                    }
                }
            }

            if (activeCctv == null) return;
            // A CCTV keeps its authored position, direction and lens throughout the shot.
            var position = activeCctv.transform.position;
            var rotation = activeCctv.transform.rotation;
            if (replayCameraRig != null) replayCameraRig.SetPose(position, rotation, 1f, true);
            else cameraTransform.SetPositionAndRotation(position, rotation);
        }

        private bool CanSwitchCctv()
        {
            if (cctvSwitchCount >= MaxCctvSwitches) return false;
            if (cctvSwitchCount == 0) return true;

            return currentPlaybackTime >=
                   currentHighlight.PlaybackDurationSeconds * FinalCctvSwitchStartRatio;
        }

        private bool CanCctvSeeSubjects(HighlightCctvCamera camera) =>
            CanCctvSee(camera, cctvTarget) &&
            (supportingPlayer == null || supportingPlayer == cctvTarget ||
             CanCctvSee(camera, supportingPlayer));

        private bool CanCctvSee(HighlightCctvCamera camera, Transform target)
        {
            if (target == null) return false;
            // Feet need not be visible: a clear body centre or upper body is enough.
            // Sample actual bounds so small carried objects are not tested above their mesh.
            var bounds = new Bounds(target.position + Vector3.up * 0.5f, Vector3.zero);
            var found = false;
            if (replayRenderers.TryGetValue(target, out var renderers))
                foreach (var renderer in renderers)
                {
                    if (renderer == null || !renderer.enabled || renderer.forceRenderingOff ||
                        !renderer.gameObject.activeInHierarchy || renderer.bounds.size.sqrMagnitude < 0.001f) continue;
                    if (!found) { bounds = renderer.bounds; found = true; }
                    else bounds.Encapsulate(renderer.bounds);
                }
            return CanCctvSeePoint(camera, target, bounds.center) ||
                CanCctvSeePoint(camera, target, bounds.center + Vector3.up * bounds.extents.y * 0.6f);
        }

        private bool CanCctvSeePoint(HighlightCctvCamera camera, Transform target, Vector3 point)
        {
            var local = camera.transform.InverseTransformPoint(point);
            if (local.z <= 0f) return false;
            var aspect = cameraTransform.TryGetComponent<Camera>(out var output) ? output.aspect : 16f / 9f;
            var halfHeight = local.z * Mathf.Tan(camera.FieldOfView * Mathf.Deg2Rad * 0.5f) * 0.9f;
            return Mathf.Abs(local.y) <= halfHeight && Mathf.Abs(local.x) <= halfHeight * aspect &&
                !IsCctvOccluded(camera.transform.position, point, target);
        }

        private float CctvScore(HighlightCctvCamera camera, Vector3 focus)
        {
            var direction = focus - camera.transform.position;
            var angle = Vector3.Angle(camera.transform.forward, direction);
            // Visibility penalties dominate; among readable views favour proximity.
            // Framing angle only breaks close ties, rather than rewarding a distant centred view.
            var score = 100f / (1f + direction.magnitude) - angle * 0.01f;
            if (!CanCctvSee(camera, cctvTarget)) score -= 600f;
            if (supportingPlayer != null && supportingPlayer != cctvTarget &&
                !CanCctvSee(camera, supportingPlayer)) score -= 300f;
            return score;
        }
    }
}

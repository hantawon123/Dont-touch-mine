using System.Text;
using Game.Client.Cameras;
using Game.Network.Players;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Game.Bootstrap
{
    internal static class MatchTransitionDiagnostics
    {
        [System.Diagnostics.Conditional("GAME_TRANSITION_DIAGNOSTICS")]
        internal static void Dump(string reason)
        {
            var log = new StringBuilder($"[QA-Transition] {reason} frame={Time.frameCount} time={Time.unscaledTime:F2} focus={Application.isFocused} cursor={Cursor.lockState} activeScene={SceneManager.GetActiveScene().name}");
            for (var i = 0; i < SceneManager.sceneCount; i++)
            {
                var scene = SceneManager.GetSceneAt(i);
                log.Append($"\n scene={scene.name} handle={scene.handle} loaded={scene.isLoaded} roots={scene.rootCount}");
            }
            foreach (var scope in Object.FindObjectsByType<PlaygroundLifetimeScope>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                log.Append($"\n playgroundScope={scope.GetInstanceID()} scene={scope.gameObject.scene.name} cachedRoots={scope.SceneRoots.Count}");
                foreach (var root in scope.SceneRoots)
                {
                    if (root == null || !root.activeInHierarchy) continue;
                    var renderers = root.GetComponentsInChildren<Renderer>(true);
                    var visible = 0;
                    foreach (var r in renderers)
                        if (r.enabled && !r.forceRenderingOff && r.gameObject.activeInHierarchy) visible++;
                    log.Append($"\n  root={root.name} id={root.GetInstanceID()} scene={root.scene.name} active={root.activeInHierarchy} pos={root.transform.position} renderers={renderers.Length} drawable={visible}");
                }
            }
            foreach (var camera in Object.FindObjectsByType<Camera>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                log.Append($"\n camera={camera.name} id={camera.GetInstanceID()} scene={camera.gameObject.scene.name} enabled={camera.enabled} active={camera.gameObject.activeInHierarchy} depth={camera.depth} mask={camera.cullingMask} near={camera.nearClipPlane} far={camera.farClipPlane} targetTexture={camera.targetTexture} pos={camera.transform.position} rot={camera.transform.eulerAngles}");
            foreach (var rig in Object.FindObjectsByType<PlayerCameraController>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                log.Append($"\n rig={rig.GetInstanceID()} scene={rig.gameObject.scene.name} enabled={rig.enabled} active={rig.gameObject.activeInHierarchy} target={(rig.FollowTarget == null ? "none" : rig.FollowTarget.name)} pos={rig.transform.position}");
            foreach (var avatar in Object.FindObjectsByType<PlayerAvatar>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                log.Append($"\n avatar={avatar.GetInstanceID()} owner={avatar.IsOwner} scene={avatar.gameObject.scene.name} pos={avatar.transform.position}");
            Debug.Log(log.ToString());
        }
    }
}

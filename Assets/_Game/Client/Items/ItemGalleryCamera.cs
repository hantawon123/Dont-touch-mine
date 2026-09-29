using UnityEngine;
using UnityEngine.InputSystem;

namespace Game.Client.Items
{
    /// <summary>Standalone review-scene camera; no match or network session required.</summary>
    public sealed class ItemGalleryCamera : MonoBehaviour
    {
        public Transform[] viewpoints;
        public string[] labels;
        public Transform[] exhibits;
        public int[] categoryStarts;
        private int selected;
        private int focused;
        private float yaw, pitch;

        private void Start() => GoTo(0);

        private void GoTo(int index)
        {
            if (viewpoints == null || viewpoints.Length == 0) return;
            selected = Mathf.Clamp(index, 0, viewpoints.Length - 1);
            focused = categoryStarts[selected];
            transform.SetPositionAndRotation(viewpoints[selected].position, viewpoints[selected].rotation);
            yaw = transform.eulerAngles.y;
            pitch = transform.eulerAngles.x;
        }

        private void Update()
        {
            var keyboard = Keyboard.current;
            var mouse = Mouse.current;
            if (keyboard == null || mouse == null) return;
            for (var i = 0; i < viewpoints.Length && i < 6; i++)
                if (keyboard[(Key)((int)Key.Digit1 + i)].wasPressedThisFrame) GoTo(i);
            if (keyboard.homeKey.wasPressedThisFrame) GoTo(selected);
            if (keyboard.fKey.wasPressedThisFrame) Focus(0);
            if (keyboard.rightArrowKey.wasPressedThisFrame) Focus(1);
            if (keyboard.leftArrowKey.wasPressedThisFrame) Focus(-1);
            if (!mouse.rightButton.isPressed) return;
            var delta = mouse.delta.ReadValue();
            yaw += delta.x * .15f;
            pitch = Mathf.Clamp(pitch - delta.y * .15f, -85, 85);
            transform.rotation = Quaternion.Euler(pitch, yaw, 0);
            var direction = new Vector3(
                (keyboard.dKey.isPressed ? 1 : 0) - (keyboard.aKey.isPressed ? 1 : 0),
                (keyboard.eKey.isPressed ? 1 : 0) - (keyboard.qKey.isPressed ? 1 : 0),
                (keyboard.wKey.isPressed ? 1 : 0) - (keyboard.sKey.isPressed ? 1 : 0));
            transform.Translate(direction * ((keyboard.leftShiftKey.isPressed ? 16 : 6) * Time.deltaTime));
        }

        private void OnGUI()
        {
            GUILayout.BeginArea(new Rect(12, 12, 920, 135), GUI.skin.box);
            GUILayout.Label("ITEM COLLECTION  |  RMB + WASD: move   Q/E: down/up   Shift: fast   Home: reset");
            GUILayout.BeginHorizontal();
            for (var i = 0; i < labels.Length; i++)
                if (GUILayout.Button($"{i + 1}. {labels[i]}", GUILayout.Height(32))) GoTo(i);
            GUILayout.EndHorizontal();
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("< Previous",GUILayout.Width(110))) Focus(-1);
            if (GUILayout.Button("F: Inspect item",GUILayout.Width(130))) Focus(0);
            if (GUILayout.Button("Next >",GUILayout.Width(110))) Focus(1);
            if (exhibits != null && exhibits.Length>focused) GUILayout.Label(exhibits[focused].name.Split('[')[0]);
            GUILayout.EndHorizontal();
            GUILayout.EndArea();
        }

        private void Focus(int step)
        {
            if (exhibits==null || exhibits.Length==0) return;
            var start=categoryStarts[selected];
            var end=selected+1<categoryStarts.Length?categoryStarts[selected+1]:exhibits.Length;
            focused=start+(focused-start+step+end-start)%(end-start);
            var renderers=exhibits[focused].GetComponentsInChildren<Renderer>();
            if (renderers.Length==0) return;
            var bounds=renderers[0].bounds;
            foreach(var renderer in renderers) bounds.Encapsulate(renderer.bounds);
            transform.position=bounds.center+new Vector3(1.4f,1,-1.8f).normalized*Mathf.Max(.12f,bounds.size.magnitude*1.8f);
            transform.LookAt(bounds.center);
            yaw=transform.eulerAngles.y; pitch=transform.eulerAngles.x;
        }
    }
}

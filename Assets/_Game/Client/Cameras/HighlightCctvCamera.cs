using UnityEngine;

namespace Game.Client.Cameras
{
    // An authored mounting point; it has no Camera and costs no extra render pass.
    public sealed class HighlightCctvCamera : MonoBehaviour
    {
        [SerializeField] private string locationName = "CCTV";
        [SerializeField, Range(40f, 90f)] private float fieldOfView = 65f;
        public string LocationName => locationName;
        public float FieldOfView => fieldOfView;
        public void Configure(string location, float fov = 65f)
        {
            locationName = location;
            fieldOfView = Mathf.Clamp(fov, 40f, 90f);
        }
        private void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.cyan;
            Gizmos.DrawSphere(transform.position, 0.15f);
            Gizmos.DrawRay(transform.position, transform.forward * 8f);
        }
    }
}

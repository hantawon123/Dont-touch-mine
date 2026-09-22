using UnityEngine;

namespace Game.Client.Cameras
{
    // An authored mounting point; it has no Camera and costs no extra render pass.
    public sealed class HighlightCctvCamera : MonoBehaviour
    {
        [SerializeField] private string locationName = "CCTV";
        [SerializeField, Range(40f, 90f)] private float fieldOfView = 65f;
        // The floor this mount watches, as a world y range. Occlusion is judged from renderer
        // bounds taller than 0.5 m, and a floor slab can be thinner than that, so a mount on an
        // upper storey would otherwise be offered the action one storey below it.
        // Leave the range empty (y1 <= y0) on single-storey maps: the mount then covers every height.
        [SerializeField] private float floorY0;
        [SerializeField] private float floorY1;
        public string LocationName => locationName;
        public float FieldOfView => fieldOfView;
        public bool HasFloor => floorY1 > floorY0;
        public float FloorY0 => floorY0;
        public float FloorY1 => floorY1;
        public void Configure(string location, float fov = 65f)
        {
            locationName = location;
            fieldOfView = Mathf.Clamp(fov, 40f, 90f);
        }

        /// <summary>Restricts this mount to one storey. An empty range (y1 &lt;= y0) clears the limit.</summary>
        public void ConfigureFloor(float y0, float y1)
        {
            floorY0 = y0;
            floorY1 = y1;
        }

        /// <summary>True when a subject at this height belongs to the storey this mount watches.</summary>
        public bool CoversHeight(float y) => !HasFloor || (y >= floorY0 && y < floorY1);

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.cyan;
            Gizmos.DrawSphere(transform.position, 0.15f);
            Gizmos.DrawRay(transform.position, transform.forward * 8f);
        }
    }
}

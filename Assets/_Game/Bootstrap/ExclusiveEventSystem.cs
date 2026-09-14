using UnityEngine.EventSystems;

namespace Game.Bootstrap
{
    // Additive scenes may enable their input before sceneLoaded is raised.
    public sealed class ExclusiveEventSystem : EventSystem
    {
        protected override void OnEnable()
        {
            var previous = current;
            if (previous != null && previous != this) previous.enabled = false;
            base.OnEnable();
            current = this;
        }
    }
}

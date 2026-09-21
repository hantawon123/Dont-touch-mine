using UnityEngine;

namespace Game.Client.Interactions
{
    /// <summary>
    /// 물건의 겉모습만 남긴 복제본을 만든다 (S15P21D205-1087).
    /// </summary>
    /// <remarks>
    /// 스크립트·콜라이더·리지드바디를 지우는 이유는 보여 주는 것 말고 할 일이 없기 때문이고,
    /// 남겨 두면 복제본이 권위가 쥔 물건인 척하거나 아바타를 밀어낸다. 원본이 숨겨진 채(들고 있는
    /// 물건은 결과 화면에서 숨긴다) 복제될 수 있으므로 렌더러의 강제 숨김도 푼다.
    /// </remarks>
    public static class ItemDisplayCopy
    {
        public static GameObject Create(GameObject source, Transform parent, string name)
        {
            if (source == null) return null;

            var copy = Object.Instantiate(source, parent, false);
            copy.name = name;
            copy.transform.SetLocalPositionAndRotation(Vector3.zero, Quaternion.identity);

            foreach (var behaviour in copy.GetComponentsInChildren<MonoBehaviour>(true))
                Object.Destroy(behaviour);
            foreach (var collider in copy.GetComponentsInChildren<Collider>(true))
                Object.Destroy(collider);
            foreach (var body in copy.GetComponentsInChildren<Rigidbody>(true))
                Object.Destroy(body);

            foreach (var renderer in copy.GetComponentsInChildren<Renderer>(true))
                renderer.forceRenderingOff = false;

            return copy;
        }
    }
}

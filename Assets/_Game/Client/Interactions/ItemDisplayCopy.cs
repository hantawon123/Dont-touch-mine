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
    /// <para>
    /// 외곽선 껍데기도 지운다 (S15P21D205-1097). 그것 때문에 결과 화면과 하이라이트에서 물건이
    /// 자홍색으로 보였다 - 아래 <see cref="RemoveOutlineShells"/> 를 보라.
    /// </para>
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

            RemoveOutlineShells(copy);

            foreach (var renderer in copy.GetComponentsInChildren<Renderer>(true))
                if (renderer != null) renderer.forceRenderingOff = false;

            return copy;
        }

        /// <summary>
        /// 복제본에 따라온 외곽선 껍데기를 지운다 (S15P21D205-1097).
        /// </summary>
        /// <remarks>
        /// 껍데기(<c>[Assigned Item Outline]</c> 등)는 원본 메시를 살짝 부풀려 그리는 자식이고,
        /// <b>런타임에 만든 재질을 원본 컴포넌트와 공유한다.</b> 복제본은 원본이 파괴되기 직전에
        /// 만들어지는데(<see cref="DestroyedItemArchive"/>), 원본이 사라지면
        /// <see cref="AssignedItemOutline"/> 의 <c>OnDestroy</c> 가 그 재질을 파괴한다. 그러면
        /// 복제본의 껍데기가 재질 없는 렌더러로 남아 <b>자홍색</b>으로 그려지고, 껍데기가 물건보다
        /// 크므로 물건 전체가 자홍색 덩어리로 보인다. 결과 화면(엔딩 무대)과 하이라이트에서
        /// 파쇄된 물건이 그렇게 보였다(2026-09-21 확인).
        /// <para>
        /// 복제본은 보여 주는 것 말고 할 일이 없으므로 껍데기 자체가 필요 없다. 껍데기를 지우면
        /// "내 물건" 표시가 결과 화면까지 따라오는 것도 함께 사라진다.
        /// </para>
        /// <para>
        /// 편집기에서는 즉시 지운다. 지연 파괴는 프레임이 돌아야 반영되어, 검사도 그 프레임을
        /// 기다려야 한다(<see cref="AssignedItemOutline"/> 도 같은 규칙을 쓴다).
        /// </para>
        /// </remarks>
        private static void RemoveOutlineShells(GameObject copy)
        {
            var renderers = copy.GetComponentsInChildren<Renderer>(true);
            for (var index = 0; index < renderers.Length; index++)
            {
                var renderer = renderers[index];
                if (renderer == null || !ItemOutlineRenderers.IsGenerated(renderer)) continue;

                // 껍데기는 전용 자식이다. 혹시 물건 자신에게 붙어 있으면 렌더러만 지운다.
                var target = renderer.gameObject == copy
                    ? (Object)renderer
                    : renderer.gameObject;
                if (Application.isPlaying) Object.Destroy(target);
                else Object.DestroyImmediate(target);
            }
        }
    }
}

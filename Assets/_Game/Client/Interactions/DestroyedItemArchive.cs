using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Game.Client.Interactions
{
    /// <summary>
    /// 파괴된 물건의 겉모습을 남겨 두는 보관소 (S15P21D205-1087).
    /// </summary>
    /// <remarks>
    /// 확정 파괴는 씬에서 물건을 지운다. 엔딩 유치장은 각자 원래 자기 물건을 손에 들려 주는데,
    /// 파괴된 사람만 빈손이면 "무엇을 잃었는지"가 화면에서 사라지므로 지워지기 전에 복제본을
    /// 여기 맡긴다.
    /// <para>
    /// 맡는 것은 스크립트를 지운 복제본이라 <see cref="CarryableItem"/> 이 아니다. 물건 목록을
    /// ObjectId 로 훑는 쪽(씬 브리지·엔딩 무대)이 같은 id 를 두 번 보지 않는다.
    /// </para>
    /// </remarks>
    public sealed class DestroyedItemArchive : MonoBehaviour
    {
        private readonly Dictionary<string, GameObject> visuals = new(StringComparer.Ordinal);

        /// <summary>물건이 살던 씬의 보관소를 찾고, 없으면 그 씬에 만든다.</summary>
        public static DestroyedItemArchive Ensure(Scene scene)
        {
            var archive = Find(scene);
            if (archive != null) return archive;

            var holder = new GameObject(nameof(DestroyedItemArchive));
            if (scene.IsValid() && scene.isLoaded)
            {
                SceneManager.MoveGameObjectToScene(holder, scene);
            }

            return holder.AddComponent<DestroyedItemArchive>();
        }

        public static DestroyedItemArchive Find(Scene scene)
        {
            foreach (var archive in FindObjectsByType<DestroyedItemArchive>(
                         FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (archive == null) continue;
                if (!scene.IsValid() || archive.gameObject.scene == scene) return archive;
            }

            return null;
        }

        /// <summary>
        /// 파괴 직전의 모습을 맡아 둔다. 같은 물건을 두 번 맡기면 처음 것을 그대로 둔다.
        /// </summary>
        public void Archive(string itemId, GameObject source)
        {
            if (string.IsNullOrEmpty(itemId) || source == null) return;
            if (visuals.TryGetValue(itemId, out var kept) && kept != null) return;

            var copy = ItemDisplayCopy.Create(source, transform, itemId + " (Archived)");
            if (copy == null) return;

            // 보관 중에는 보이지 않아야 한다. 꺼내 쓰는 쪽이 켠다.
            copy.SetActive(false);
            visuals[itemId] = copy;
        }

        public bool TryGetVisual(string itemId, out GameObject visual)
        {
            visual = null;
            if (string.IsNullOrEmpty(itemId)) return false;
            if (!visuals.TryGetValue(itemId, out var kept) || kept == null) return false;

            visual = kept;
            return true;
        }
    }
}

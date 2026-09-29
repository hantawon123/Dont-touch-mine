using System;
using System.Linq;
using Game.SOAP.Config;
using UnityEditor;
using UnityEngine;

namespace Game.Editor
{
    [CustomEditor(typeof(ItemCatalogSO))]
    public sealed class ItemCatalogInspector : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            EditorGUILayout.HelpBox("카테고리와 아이템을 여기서 추가·수정합니다. Enabled를 끄면 플레이 선택/배정에서 제외됩니다. 변경 내용은 다음 플레이부터 적용됩니다.", MessageType.Info);
            DrawDefaultInspector();
            var catalog = (ItemCatalogSO)target;
            if (GUILayout.Button("카탈로그 검증"))
            {
                try { catalog.Apply(); Debug.Log("아이템 카탈로그 검증 완료", catalog); }
                catch (Exception e) { Debug.LogError(e.Message, catalog); }
            }
            foreach (var category in catalog.categories.Where(c => c != null && c.enabled))
            {
                var count = category.items.Count(i => i != null && i.enabled);
                EditorGUILayout.LabelField(category.label, $"{count}개");
                if (count < 20) EditorGUILayout.HelpBox($"{category.label}: 콘텐츠 목표 20개까지 {20 - count}개 부족합니다. 플레이에 필요한 최소 수는 6개입니다.", MessageType.Warning);
            }
        }
    }
}

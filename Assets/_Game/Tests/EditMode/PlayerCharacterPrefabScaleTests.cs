using Game.Client.Players;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Game.Tests.EditMode
{
    /// <summary>
    /// 머지나 에디터 도구가 캐릭터 크기를 되돌리면 바로 드러나게 하는 회귀 테스트.
    /// 크기를 의도적으로 바꿀 때는 프리팹과 <see cref="PlayerVisualScale.Value"/>를 함께 바꾼다.
    /// </summary>
    public sealed class PlayerCharacterPrefabScaleTests
    {
        private const string PlayerPrefabPath = "Assets/_Game/Content/Prefabs/PlayerCharacter.prefab";
        private const string NetworkedPrefabPath = "Assets/_Game/Content/Prefabs/NetworkedPlayer.prefab";

        [TestCase(PlayerPrefabPath)]
        [TestCase(NetworkedPrefabPath)]
        public void VisualScale_MatchesTeamDecision(string path)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            Assert.IsNotNull(prefab, path);
            var visual = prefab.transform.Find("Visual");
            Assert.IsNotNull(visual, "Visual child missing in " + path);

            var expected = Vector3.one * PlayerVisualScale.Value;
            Assert.Less((visual.localScale - expected).magnitude, 1e-4f,
                $"{path}의 Visual 스케일 {visual.localScale}이 팀 결정값 {PlayerVisualScale.Value}와 다릅니다. " +
                "머지로 되돌아갔다면 프리팹을 고치고, 크기를 바꾼 것이면 PlayerVisualScale.Value도 함께 바꾸세요.");
        }
    }
}

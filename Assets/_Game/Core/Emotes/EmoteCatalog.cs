using System;
using Game.Core.Settings;

namespace Game.Core.Emotes
{
    public enum EmoteId
    {
        HipHop,
        Chicken,
        Wave,
        Insult,
        Taunt,
        Spin
    }

    public readonly struct EmoteDefinition
    {
        public EmoteDefinition(EmoteId id, string labelKey, string stateName, bool loop, float durationSeconds)
        {
            Id = id;
            LabelKey = labelKey;
            StateName = stateName;
            Loop = loop;
            DurationSeconds = durationSeconds;
        }

        public EmoteId Id { get; }

        /// <summary><see cref="UiText.Emote"/> key; <see cref="Label"/> reads it in the applied language.</summary>
        public string LabelKey { get; }

        public string Label => UiLocale.Applied(LabelKey);
        public string StateName { get; }
        public bool Loop { get; }
        public float DurationSeconds { get; }
    }

    /// <summary>
    /// 감정 표현 목록. 휠(클라이언트)·네트워크 복제·하이라이트 재생이 같은 표를 읽는다.
    /// </summary>
    /// <remarks>
    /// 표현을 고르는 UI 는 <c>Game.Client.Emotes</c> 에 있지만, 표 자체는 <b>Core</b> 에 둔다.
    /// 하이라이트를 녹화하는 <c>Network</c>·<c>Server</c> 계층이 "이 표현이 몇 초짜리인지"를
    /// 알아야 재생 구간을 프레임에 담을 수 있는데, 두 계층은 <c>Client</c> 를 참조하지 않는다.
    /// </remarks>
    public static class EmoteCatalog
    {
        public const int Count = 6;
        public const string StatePrefix = "Emote_";

        /// <summary>
        /// 반복 표현이 한 번 시작하면 유지되는 시간(초). 다른 동작(주먹질·피격·기절·자세 전환)이
        /// 끊을 때까지 이어지므로, 실제 플레이의 <c>PlayerAnimationDriver</c> 와 같은 큰 값을 쓴다.
        /// </summary>
        public const float LoopSeconds = 600f;

        public static readonly EmoteDefinition[] All =
        {
            new EmoteDefinition(EmoteId.Wave, UiText.Emote.Wave, "Emote_Wave", false, 61f / 30f),
            new EmoteDefinition(EmoteId.Taunt, UiText.Emote.Taunt, "Emote_Taunt", false, 76f / 30f),
            new EmoteDefinition(EmoteId.Insult, UiText.Emote.Insult, "Emote_Insult", false, 81f / 30f),
            new EmoteDefinition(EmoteId.Chicken, UiText.Emote.Chicken, "Emote_Chicken", true, 144f / 30f),
            new EmoteDefinition(EmoteId.HipHop, UiText.Emote.HipHop, "Emote_HipHop", true, 520f / 30f),
            new EmoteDefinition(EmoteId.Spin, UiText.Emote.Spin, "Emote_Spin", true, 266f / 30f)
        };

        public static EmoteDefinition Of(EmoteId id)
        {
            for (var i = 0; i < All.Length; i++)
            {
                if (All[i].Id == id)
                {
                    return All[i];
                }
            }

            return All[0];
        }

        /// <summary>번호로 고르기. 네트워크와 하이라이트는 카탈로그 ID를 정수로 나른다.</summary>
        public static bool TryOf(int id, out EmoteDefinition definition)
        {
            if (Enum.IsDefined(typeof(EmoteId), id))
            {
                definition = Of((EmoteId)id);
                return true;
            }

            definition = default;
            return false;
        }

        /// <summary>이 표현이 한 번 시작하면 재생되는 시간(초). 반복 표현은 <see cref="LoopSeconds"/> 다.</summary>
        public static float PlaybackSeconds(int id) =>
            TryOf(id, out var definition)
                ? (definition.Loop ? LoopSeconds : definition.DurationSeconds)
                : 0f;

        public static bool IsEmoteState(string state) =>
            !string.IsNullOrEmpty(state) &&
            state.StartsWith(StatePrefix, StringComparison.Ordinal);

        /// <summary>
        /// 1회성 표현(인사·도발·모욕) 상태인가. 이들은 걸으면 끊긴다.
        /// 춤은 걸어도, 점프하거나 떨어져도 이어진다(전신 클립이라 캡슐이 미끄러지는 건 감수).
        /// </summary>
        public static bool IsOneShotEmoteState(string state)
        {
            if (!IsEmoteState(state))
            {
                return false;
            }

            for (var i = 0; i < All.Length; i++)
            {
                if (All[i].StateName == state)
                {
                    return !All[i].Loop;
                }
            }

            return true;
        }
    }
}

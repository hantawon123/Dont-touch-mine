using System;
using System.Linq;
using Game.Core.Ports;
using Game.Server.Match;
using Game.SOAP.Config;
using Game.Network.Session;
using NUnit.Framework;
using UnityEngine;

namespace Game.Architecture.Tests
{
    public sealed class HighlightDirectorTests
    {
        private static HighlightDirectorReply Reply(params int[] ids) => new() { available=true,
            picks=ids.Select(id=>new HighlightDirectorPick {id=id,title="주요 순간",summary="물건을 회수했습니다."}).ToArray() };
        [Test] public void InvalidSelectionCannotReplaceFallback()
        {
            Assert.That(Reply(2,0).IsUsable(5),Is.True);
            Assert.That(Reply(2,0,1).IsUsable(5),Is.True,
                "The deployed legacy backend may still return three ranked picks.");
            Assert.That(Reply(2,0,1).UsablePickCount(5),Is.EqualTo(2));
            Assert.That(Reply(0,0).IsUsable(5),Is.False);
            Assert.That(Reply(0,9).IsUsable(5),Is.False);
            Assert.That(Reply(0).IsUsable(5),Is.False);
            var unsafeReply=Reply(0); unsafeReply.picks[0].title="<b>잘못된 문구</b>";
            Assert.That(unsafeReply.IsUsable(1),Is.False);
        }
        [Test] public void RankedSequenceAndReplayTransferPreserveAiOrderAndCaptions()
        {
            var rules=ScriptableObject.CreateInstance<MatchRulesSO>();
            try
            {
                var low=new HighlightCandidate(HighlightType.ItemRecovered,new[]{new HighlightSegment(1,3)},"item",2,10);
                var high=new HighlightCandidate(HighlightType.FirstBlood,new[]{new HighlightSegment(4,6)},"item2",5,90);
                var selected=new HighlightSequence(new[]{low,high},rules,true);
                Assert.That(selected.Capture()[0].Type,Is.EqualTo(HighlightType.ItemRecovered));
                var replay=new[]{new HighlightReplayData(low,new[]{new HighlightReplayClip(low.Segments[0],
                    new[]{new HighlightReplayFrame(2,new[]{Pose.identity},Array.Empty<Game.Server.Items.WorldObjectState>())})},
                    "다시 내 손에","원주인이 물건을 회수했습니다.")};
                Assert.That(HighlightReplaySerializer.TryDeserializeCompressed(HighlightReplaySerializer.SerializeCompressed(replay),out var restored),Is.True);
                Assert.That(restored[0].Title,Is.EqualTo(replay[0].Title));
                Assert.That(restored[0].Summary,Is.EqualTo(replay[0].Summary));
            }
            finally { UnityEngine.Object.DestroyImmediate(rules); }
        }
    }
}

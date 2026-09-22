using System.Collections.Generic;
using Game.Bootstrap;
using Game.Core.Emotes;
using Game.Server.Items;
using Game.Server.Match;
using NUnit.Framework;
using UnityEngine;

namespace Game.Tests.EditMode
{
    public sealed class HighlightReplayPlayerTests
    {
        private readonly List<GameObject> gameObjects = new();

        [TearDown]
        public void TearDown()
        {
            foreach (var gameObject in gameObjects)
            {
                Object.DestroyImmediate(gameObject);
            }

            gameObjects.Clear();
        }

        [Test]
        public void Start_AppliesFirstRecordedPlayerAndObjectPoses()
        {
            var playerTarget = CreateGameObject("Player").transform;
            var objectTarget = CreateGameObject("Item").transform;
            var player = new HighlightReplayPlayer(
                new[] { playerTarget },
                new[] { new SceneWorldObjectReference("item", objectTarget) });

            Assert.That(player.Start(new[]
            {
                Clip(
                    new HighlightSegment(10d, 11d),
                    Frame(10d, Vector3.one, Vector3.right))
            }), Is.True);

            Assert.That(playerTarget.position, Is.EqualTo(Vector3.one));
            Assert.That(objectTarget.position, Is.EqualTo(Vector3.right));
        }

        [Test]
        public void Advance_InterpolatesBetweenRecordedFrames()
        {
            var target = CreateGameObject("Player").transform;
            var player = new HighlightReplayPlayer(
                new[] { target },
                new SceneWorldObjectReference[0]);
            player.Start(new[]
            {
                Clip(
                    new HighlightSegment(10d, 11d),
                    Frame(10d, Vector3.zero),
                    Frame(11d, Vector3.right * 10f))
            });

            Assert.That(player.Advance(0.5d), Is.True);

            Assert.That(target.position, Is.EqualTo(Vector3.right * 5f));
        }

        [Test]
        public void Advance_UsesPlaybackSpeedAndCompletesAllClips()
        {
            var target = CreateGameObject("Player").transform;
            var player = new HighlightReplayPlayer(
                new[] { target },
                new SceneWorldObjectReference[0]);
            player.Start(new[]
            {
                Clip(
                    new HighlightSegment(0d, 4d, 2d),
                    Frame(0d, Vector3.zero),
                    Frame(4d, Vector3.right * 4f)),
                Clip(
                    new HighlightSegment(10d, 11d),
                    Frame(10d, Vector3.up),
                    Frame(11d, Vector3.up * 2f))
            });

            Assert.That(player.Advance(1d), Is.True);
            Assert.That(target.position, Is.EqualTo(Vector3.right * 2f));
            Assert.That(player.Advance(1.5d), Is.True);
            Assert.That(target.position, Is.EqualTo(Vector3.up * 1.5f));
            Assert.That(player.Advance(0.5d), Is.False);
            Assert.That(player.IsPlaying, Is.False);
        }

        [Test]
        public void Advance_CutsToNextClipWithoutWaitingAtBoundary()
        {
            var clips = new[]
            {
                Clip(new HighlightSegment(0d, 1d), Frame(0d, Vector3.zero)),
                Clip(new HighlightSegment(10d, 11d), Frame(10d, Vector3.one)),
            };

            var target = CreateGameObject("Player").transform;
            var player = new HighlightReplayPlayer(new[] { target }, new SceneWorldObjectReference[0]);
            Assert.That(player.Start(clips), Is.True);
            Assert.That(player.Advance(1d), Is.True);
            Assert.That(player.CurrentClipIndex, Is.EqualTo(1));
            Assert.That(player.SourceTime, Is.EqualTo(10d));
            Assert.That(target.position, Is.EqualTo(Vector3.one));
        }

        [Test]
        public void AnimationStateOf_UsesFirstClipNames()
        {
            Assert.That(
                HighlightReplayPlayer.AnimationStateOf(HighlightPlayerAction.None),
                Is.EqualTo("Idle"));
            Assert.That(
                HighlightReplayPlayer.AnimationStateOf(HighlightPlayerAction.Crouching),
                Is.EqualTo("Crouch_Idle"));
            // 속도를 안 주면 제자리다. 엎드려 가만히 있으면 기어가는 동작이 아니라 Prone_Idle 이다.
            Assert.That(
                HighlightReplayPlayer.AnimationStateOf(HighlightPlayerAction.Prone),
                Is.EqualTo("Prone_Idle"));
            Assert.That(
                HighlightReplayPlayer.AnimationStateOf(HighlightPlayerAction.Airborne),
                Is.EqualTo("Fall"));
            Assert.That(
                HighlightReplayPlayer.AnimationStateOf(HighlightPlayerAction.Punching),
                Is.EqualTo("Punch"));
            // 기절은 실제 플레이와 같은 쓰러지는 클립이다. 옛 Stunned 클립은 더 쓰지 않는다.
            Assert.That(
                HighlightReplayPlayer.AnimationStateOf(HighlightPlayerAction.Stunned),
                Is.EqualTo("Stun_Idle"));
            Assert.That(
                HighlightReplayPlayer.AnimationStateOf(
                    HighlightPlayerAction.Stunned | HighlightPlayerAction.StunEntry),
                Is.EqualTo("Stun_Start"));
        }

        /// <summary>
        /// 맞은 쪽은 이번에 새로 구운 피격 클립으로 젖혀진다 (2026-09-22).
        ///
        /// <para>
        /// 전에는 맞은 쪽에 붙는 플래그가 아예 없어서, 얻어맞는 장면도 그냥 서 있거나 걷는
        /// 모습으로 나왔다.
        /// </para>
        /// </summary>
        [Test]
        public void AnimationStateOf_ShowsTheHitSideFlinching()
        {
            Assert.That(
                HighlightReplayPlayer.AnimationStateOf(HighlightPlayerAction.Hit),
                Is.EqualTo("Hit"));
            Assert.That(
                HighlightReplayPlayer.AnimationStateOf(HighlightPlayerAction.Hit, 2f),
                Is.EqualTo("Hit_Walk"));
            Assert.That(
                HighlightReplayPlayer.AnimationStateOf(HighlightPlayerAction.Hit, 6f),
                Is.EqualTo("Hit_Run"));
            Assert.That(
                HighlightReplayPlayer.AnimationStateOf(
                    HighlightPlayerAction.Hit | HighlightPlayerAction.Crouching),
                Is.EqualTo("Hit_Crouch"));
            Assert.That(
                HighlightReplayPlayer.AnimationStateOf(
                    HighlightPlayerAction.Hit | HighlightPlayerAction.Prone, 2f),
                Is.EqualTo("Hit_Crawl"));
            Assert.That(
                HighlightReplayPlayer.AnimationStateOf(
                    HighlightPlayerAction.Hit | HighlightPlayerAction.Carrying, 6f),
                Is.EqualTo("Carry_TwoHands_Hit_Run"));
            // 때린 쪽(Punching)과 맞은 쪽(Hit)이 한 사람에게 겹치면 맞은 쪽이 먼저다.
            Assert.That(
                HighlightReplayPlayer.AnimationStateOf(
                    HighlightPlayerAction.Hit | HighlightPlayerAction.Punching),
                Is.EqualTo("Hit"));
            // 기절로 쓰러지는 중에는 움찔하지 않는다.
            Assert.That(
                HighlightReplayPlayer.AnimationStateOf(
                    HighlightPlayerAction.Hit | HighlightPlayerAction.Stunned |
                    HighlightPlayerAction.StunEntry),
                Is.EqualTo("Stun_Start"));
            Assert.That(
                HighlightReplayPlayer.AnimationStateOf(HighlightPlayerAction.StunRecovery),
                Is.EqualTo("Stun_End"));
        }

        /// <summary>
        /// 감정 표현도 하이라이트에 나온다 (2026-09-22).
        ///
        /// <para>
        /// 1회성 표현은 걸으면 끊기고 춤은 걸어도 이어진다. 실제 플레이의
        /// <c>PlayerAnimationDriver</c> 규칙과 같다.
        /// </para>
        /// </summary>
        [Test]
        public void AnimationStateOf_ShowsEmotes()
        {
            var wave = HighlightPlayerAction.None.WithEmote((int)EmoteId.Wave);
            var hipHop = HighlightPlayerAction.None.WithEmote((int)EmoteId.HipHop);

            Assert.That(HighlightReplayPlayer.AnimationStateOf(wave), Is.EqualTo("Emote_Wave"));
            Assert.That(
                HighlightReplayPlayer.AnimationStateOf(wave, 2f),
                Is.EqualTo("Walk_Forward"), "1회성 표현은 걸으면 끊긴다.");
            Assert.That(HighlightReplayPlayer.AnimationStateOf(hipHop), Is.EqualTo("Emote_HipHop"));
            Assert.That(
                HighlightReplayPlayer.AnimationStateOf(hipHop, 2f),
                Is.EqualTo("Emote_HipHop"), "춤은 걸어도 이어진다.");
            // 전투·기절이 표현보다 먼저다.
            Assert.That(
                HighlightReplayPlayer.AnimationStateOf(hipHop | HighlightPlayerAction.Punching),
                Is.EqualTo("Punch"));
            Assert.That(
                HighlightReplayPlayer.AnimationStateOf(wave.WithoutEmote()),
                Is.EqualTo("Idle"));
        }

        /// <summary>
        /// 들고 있으면 두 손 클립으로 가야 한다. 들기를 안 보면 팔이 내려간 Idle 이 나와서
        /// 물건을 들고 있는데 맨손으로 서 있는 것처럼 보인다.
        /// </summary>
        [Test]
        public void AnimationStateOf_UsesTwoHandClipsWhileCarrying()
        {
            Assert.That(
                HighlightReplayPlayer.AnimationStateOf(HighlightPlayerAction.Carrying),
                Is.EqualTo("Carry_TwoHands"));
            Assert.That(
                HighlightReplayPlayer.AnimationStateOf(
                    HighlightPlayerAction.Carrying | HighlightPlayerAction.Crouching),
                Is.EqualTo("Carry_TwoHands_Crouch_Idle"));
            Assert.That(
                HighlightReplayPlayer.AnimationStateOf(
                    HighlightPlayerAction.Carrying | HighlightPlayerAction.Prone),
                Is.EqualTo("Carry_TwoHands_Prone_Idle"));
            Assert.That(
                HighlightReplayPlayer.AnimationStateOf(
                    HighlightPlayerAction.Carrying | HighlightPlayerAction.Prone, 2f),
                Is.EqualTo("Carry_TwoHands_Crawl_Forward"), "들고 기어가면 두 손 기기 클립이다.");
            Assert.That(
                HighlightReplayPlayer.AnimationStateOf(
                    HighlightPlayerAction.Carrying | HighlightPlayerAction.Airborne),
                Is.EqualTo("Carry_TwoHands_Jump"));
            // 주먹질은 들고 있어도 Punch 다. Carry_TwoHands_Hit 는 맞은 쪽 클립이다.
            Assert.That(
                HighlightReplayPlayer.AnimationStateOf(
                    HighlightPlayerAction.Carrying | HighlightPlayerAction.Punching),
                Is.EqualTo("Punch"));
        }

        /// <summary>
        /// 재생기가 부르는 상태 이름이 실제 애니메이터에 다 있는지 본다.
        ///
        /// <para>
        /// <c>Animator.Play</c> 는 없는 이름을 줘도 <b>조용히 아무것도 안 한다.</b> 그래서 상태 이름을
        /// 고치거나 지우면 하이라이트만 티 없이 멈춘 자세로 나온다 - 실제 플레이는 멀쩡하므로
        /// 알아채기까지 오래 걸린다.
        /// </para>
        /// </summary>
        [Test]
        public void AnimationStateOf_OnlyNamesStatesThatExistInThePlayerAnimator()
        {
            const string path = "Assets/_Game/Content/Animations/PlayerAnimator.controller";
            var controller = UnityEditor.AssetDatabase.LoadAssetAtPath<
                UnityEditor.Animations.AnimatorController>(path);
            Assert.That(controller, Is.Not.Null, path + " 가 필요합니다.");

            var states = new HashSet<string>();
            foreach (var layer in controller.layers) Collect(layer.stateMachine, states);
            Assert.That(states.Count, Is.GreaterThan(20), "애니메이터에서 상태를 못 읽었습니다.");

            // 플래그 조합(감정 표현 ID 세 비트를 포함해 열다섯 자리)을 제자리·걷기·달리기
            // 속도로 각각 돌려 이름을 모은다.
            var named = new HashSet<string>();
            for (var bits = 0; bits < 1 << 15; bits++)
            foreach (var speed in new[] { 0f, 2f, 6f })
                named.Add(HighlightReplayPlayer.AnimationStateOf((HighlightPlayerAction)bits, speed));

            foreach (var name in named)
                Assert.That(states, Does.Contain(name), $"애니메이터에 상태 '{name}' 가 없습니다.");
        }

        private static void Collect(UnityEditor.Animations.AnimatorStateMachine machine, HashSet<string> into)
        {
            foreach (var state in machine.states) into.Add(state.state.name);
            foreach (var child in machine.stateMachines) Collect(child.stateMachine, into);
        }

        /// <summary>
        /// 움직이면 걷기·달리기 클립으로 간다 (2026-09-21).
        ///
        /// <para>
        /// 전에는 속도를 보지 않아 뛰어가는 장면도 제자리 Idle 로 나왔다.
        /// </para>
        /// </summary>
        [Test]
        public void AnimationStateOf_WalksAndRunsWithSpeed()
        {
            Assert.That(HighlightReplayPlayer.AnimationStateOf(HighlightPlayerAction.None, 0f),
                Is.EqualTo("Idle"));
            Assert.That(HighlightReplayPlayer.AnimationStateOf(HighlightPlayerAction.None, 0.2f),
                Is.EqualTo("Idle"), "걸음으로 안 볼 만큼 느리면 제자리다.");
            Assert.That(HighlightReplayPlayer.AnimationStateOf(HighlightPlayerAction.None, 2f),
                Is.EqualTo("Walk_Forward"));
            Assert.That(HighlightReplayPlayer.AnimationStateOf(HighlightPlayerAction.None, 6f),
                Is.EqualTo("Run_Forward"));
            // 들고 뛰면 두 손 클립의 달리기다.
            Assert.That(
                HighlightReplayPlayer.AnimationStateOf(HighlightPlayerAction.Carrying, 6f),
                Is.EqualTo("Carry_TwoHands_Run_Forward"));
            Assert.That(
                HighlightReplayPlayer.AnimationStateOf(HighlightPlayerAction.Carrying, 2f),
                Is.EqualTo("Carry_TwoHands_Walk_Forward"));
        }

        /// <summary>웅크림·엎드림도 서 있는지 움직이는지로 갈린다.</summary>
        [Test]
        public void AnimationStateOf_SplitsCrouchAndProneByMovement()
        {
            Assert.That(
                HighlightReplayPlayer.AnimationStateOf(HighlightPlayerAction.Crouching, 0f),
                Is.EqualTo("Crouch_Idle"));
            Assert.That(
                HighlightReplayPlayer.AnimationStateOf(HighlightPlayerAction.Crouching, 2f),
                Is.EqualTo("Crouch_Walk_Forward"));
            Assert.That(
                HighlightReplayPlayer.AnimationStateOf(
                    HighlightPlayerAction.Crouching | HighlightPlayerAction.Carrying, 2f),
                Is.EqualTo("Carry_TwoHands_Crouch_Walk_Forward"));
            Assert.That(
                HighlightReplayPlayer.AnimationStateOf(HighlightPlayerAction.Prone, 0f),
                Is.EqualTo("Prone_Idle"));
            Assert.That(
                HighlightReplayPlayer.AnimationStateOf(HighlightPlayerAction.Prone, 2f),
                Is.EqualTo("Crawl_Forward"));
            // 순간 동작은 속도와 무관하게 먼저 본다.
            Assert.That(
                HighlightReplayPlayer.AnimationStateOf(HighlightPlayerAction.Punching, 6f),
                Is.EqualTo("Punch"));
            Assert.That(
                HighlightReplayPlayer.AnimationStateOf(HighlightPlayerAction.Stunned, 6f),
                Is.EqualTo("Stun_Idle"));
        }

        /// <summary>던지기·내려놓기는 순간 동작이라 들기보다 먼저 본다.</summary>
        [Test]
        public void AnimationStateOf_ShowsThrowAndPutDown()
        {
            Assert.That(
                HighlightReplayPlayer.AnimationStateOf(
                    HighlightPlayerAction.Throwing | HighlightPlayerAction.Carrying),
                Is.EqualTo("Throw_TwoHands"));
            Assert.That(
                HighlightReplayPlayer.AnimationStateOf(
                    HighlightPlayerAction.Throwing | HighlightPlayerAction.Crouching),
                Is.EqualTo("Throw_TwoHands_Crouch"));
            Assert.That(
                HighlightReplayPlayer.AnimationStateOf(HighlightPlayerAction.Placing),
                Is.EqualTo("PutDown_TwoHands"));
            Assert.That(
                HighlightReplayPlayer.AnimationStateOf(
                    HighlightPlayerAction.Placing | HighlightPlayerAction.Prone),
                Is.EqualTo("PutDown_TwoHands_Prone"));
        }

        private HighlightReplayFrame Frame(
            double recordedAt,
            Vector3 playerPosition,
            Vector3? objectPosition = null)
        {
            var worldObjects = objectPosition.HasValue
                ? new[]
                {
                    new WorldObjectState(
                        "item",
                        new Pose(objectPosition.Value, Quaternion.identity))
                }
                : new WorldObjectState[0];
            return new HighlightReplayFrame(
                recordedAt,
                new[] { new Pose(playerPosition, Quaternion.identity) },
                worldObjects);
        }

        private static HighlightReplayClip Clip(
            HighlightSegment segment,
            params HighlightReplayFrame[] frames)
        {
            return new HighlightReplayClip(segment, frames);
        }

        private GameObject CreateGameObject(string name)
        {
            var gameObject = new GameObject(name);
            gameObjects.Add(gameObject);
            return gameObject;
        }
    }
}

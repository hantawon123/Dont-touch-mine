using System.Collections;
using System.Linq;
using Game.Client.Combat;
using UnityEngine.TestTools;
using Game.Client.Character;
using Game.Client.Players;
using Game.Core.Players;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace Game.Tests.EditMode
{
    public sealed class PlayerAnimationDriverTests
    {
        [UnityTest]
        public IEnumerator ThrowStartsAfterWindupForLocalAndReplicatedPlayers()
        {
            UnityEditor.SceneManagement.EditorSceneManager.NewScene(
                UnityEditor.SceneManagement.NewSceneSetup.EmptyScene,
                UnityEditor.SceneManagement.NewSceneMode.Single);
            yield return new EnterPlayMode();
            foreach (var replicated in new[] { false, true })
            {
                var player = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(
                    "Assets/_Game/Content/Prefabs/PlayerCharacter.prefab"));
                try
                {
                    var driver = player.GetComponent<PlayerAnimationDriver>();
                    var animator = player.GetComponentInChildren<Animator>();
                    animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
                    if (replicated) driver.ApplyNetworkState(0f, true, 0, Vector2.zero, false);
                    animator.Play("Carry_TwoHands", 0, 0f);
                    animator.Update(0f);
                    driver.PlayThrow();
                    animator.Update(.06f);
                    var state = animator.GetCurrentAnimatorStateInfo(0);
                    Assert.That(state.IsName("Throw_TwoHands"), Is.True);
                    Assert.That(state.normalizedTime * state.length, Is.GreaterThanOrEqualTo(10f / 30f));
                }
                finally { Object.DestroyImmediate(player); }
            }
            yield return new ExitPlayMode();
        }

        [TestCase(PlayerPosture.Standing, 0f, "Punch_Left")]
        [TestCase(PlayerPosture.Standing, 4f, "Punch_Left_Walk")]
        [TestCase(PlayerPosture.Standing, 7f, "Punch_Left_Run")]
        [TestCase(PlayerPosture.Crouching, 0f, "Punch_Left_Crouch")]
        [TestCase(PlayerPosture.Crouching, 2f, "Punch_Left_Crouch_Walk")]
        public void LeftPunch_SelectsTheCurrentPostureAndSpeed(PlayerPosture posture, float speed, string expected)
        {
            Assert.That(PlayerAnimationDriver.ResolvePunchClip(posture, speed, 4f, 7f, true), Is.EqualTo(expected));
        }

        [Test]
        public void Direction_UsesPlusXAsLeft()
        {
            Assert.That(
                PlayerAnimationDriver.ResolveDirection(new Vector2(1f, 0f)),
                Is.EqualTo(PlayerAnimationDriver.MoveDirection.Left));
            Assert.That(
                PlayerAnimationDriver.ResolveDirection(new Vector2(-1f, 0f)),
                Is.EqualTo(PlayerAnimationDriver.MoveDirection.Right));
            Assert.That(
                PlayerAnimationDriver.ResolveDirection(new Vector2(0f, 1f)),
                Is.EqualTo(PlayerAnimationDriver.MoveDirection.Forward));
            Assert.That(
                PlayerAnimationDriver.ResolveDirection(new Vector2(0f, -1f)),
                Is.EqualTo(PlayerAnimationDriver.MoveDirection.Back));
        }

        [TestCase(1f, 1f, "Forward")]
        [TestCase(-1f, 1f, "Forward")]
        [TestCase(1f, -1f, "Back")]
        [TestCase(-1f, -1f, "Back")]
        public void Direction_UsesStableForwardOrBackClipForInitialDiagonalInput(
            float x,
            float y,
            string expected)
        {
            Assert.That(
                PlayerAnimationDriver.ResolveDirection(new Vector2(x, y)).ToString(),
                Is.EqualTo(expected));
        }

        [Test]
        public void Direction_KeepsPreviousDirectionWhenDiagonalInputJitters()
        {
            Assert.That(
                PlayerAnimationDriver.ResolveDirection(
                    new Vector2(0.74f, 0.68f),
                    PlayerAnimationDriver.MoveDirection.Forward),
                Is.EqualTo(PlayerAnimationDriver.MoveDirection.Forward));
            Assert.That(
                PlayerAnimationDriver.ResolveDirection(
                    new Vector2(0.68f, 0.74f),
                    PlayerAnimationDriver.MoveDirection.Left),
                Is.EqualTo(PlayerAnimationDriver.MoveDirection.Left));
        }

        [Test]
        public void Locomotion_UsesWalkAndRunForEveryDiagonal()
        {
            foreach (var diagonal in new[]
            {
                new Vector2(1f, 1f), new Vector2(-1f, 1f),
                new Vector2(1f, -1f), new Vector2(-1f, -1f)
            })
            {
                Assert.That(
                    PlayerAnimationDriver.ResolveLocomotionClip(
                        PlayerPosture.Standing, false, 4f, diagonal, 4f, 7f),
                    Does.StartWith("Walk_"));
                Assert.That(
                    PlayerAnimationDriver.ResolveLocomotionClip(
                        PlayerPosture.Standing, false, 7f, diagonal, 4f, 7f),
                    Does.StartWith("Run_"));
                Assert.That(
                    PlayerAnimationDriver.ResolveLocomotionClip(
                        PlayerPosture.Standing, true, 7f, diagonal, 4f, 7f),
                    Does.StartWith("Carry_TwoHands_Run_"));
            }
        }

        [Test]
        public void Locomotion_PicksCarryAndStrafeClips()
        {
            Assert.That(
                PlayerAnimationDriver.ResolveLocomotionClip(
                    PlayerPosture.Standing, false, 0f, Vector2.zero, 4f, 7f),
                Is.EqualTo("Idle"));
            Assert.That(
                PlayerAnimationDriver.ResolveLocomotionClip(
                    PlayerPosture.Standing, true, 0f, Vector2.zero, 4f, 7f),
                Is.EqualTo("Carry_TwoHands"));
            Assert.That(
                PlayerAnimationDriver.ResolveLocomotionClip(
                    PlayerPosture.Standing, false, 4f, new Vector2(-1f, 0f), 4f, 7f),
                Is.EqualTo("Walk_Right"));
            Assert.That(
                PlayerAnimationDriver.ResolveLocomotionClip(
                    PlayerPosture.Standing, true, 7f, new Vector2(0f, 1f), 4f, 7f),
                Is.EqualTo("Carry_TwoHands_Run_Forward"));
            Assert.That(
                PlayerAnimationDriver.ResolveLocomotionClip(
                    PlayerPosture.Crouching, false, 2f, new Vector2(0f, 1f), 4f, 7f),
                Is.EqualTo("Crouch_Walk_Forward"));
            Assert.That(
                PlayerAnimationDriver.ResolveLocomotionClip(
                    PlayerPosture.Prone, true, 0.8f, new Vector2(0f, -1f), 4f, 7f),
                    Is.EqualTo("Carry_TwoHands_Crawl_Back"));
        }

        [Test]
        public void PlaybackSpeed_ScalesWithPlanarSpeedForLocomotion()
        {
            Assert.That(
                PlayerAnimationDriver.ResolvePlaybackSpeed("Walk_Forward", 4f, 4f, 7f, 2f, 0.8f),
                Is.EqualTo(1f).Within(0.001f));
            Assert.That(
                PlayerAnimationDriver.ResolvePlaybackSpeed("Run_Forward", 10.5f, 4f, 7f, 2f, 0.8f),
                Is.EqualTo(1.5f).Within(0.001f));
            Assert.That(
                PlayerAnimationDriver.ResolvePlaybackSpeed("Crouch_Walk_Forward", 2f, 4f, 7f, 2f, 0.8f),
                Is.EqualTo(1f).Within(0.001f));
            Assert.That(
                PlayerAnimationDriver.ResolvePlaybackSpeed("Idle", 4f, 4f, 7f, 2f, 0.8f),
                Is.EqualTo(1f));
            Assert.That(
                PlayerAnimationDriver.ResolvePlaybackSpeed("Punch", 7f, 4f, 7f, 2f, 0.8f),
                Is.EqualTo(1f));
        }

        [Test]
        public void PickupPutDown_UsesPostureMatchedClips()
        {
            Assert.That(
                PlayerAnimationDriver.ResolvePickupClip(PlayerPosture.Standing),
                Is.EqualTo("PutUp_TwoHands"));
            Assert.That(
                PlayerAnimationDriver.ResolvePickupClip(PlayerPosture.Crouching),
                Is.EqualTo("PutUp_TwoHands_Crouch"));
            Assert.That(
                PlayerAnimationDriver.ResolvePickupClip(PlayerPosture.Prone),
                Is.EqualTo("PutUp_TwoHands_Prone"));
            Assert.That(
                PlayerAnimationDriver.ResolvePutDownClip(PlayerPosture.Standing),
                Is.EqualTo("PutDown_TwoHands"));
            Assert.That(
                PlayerAnimationDriver.ResolvePutDownClip(PlayerPosture.Crouching),
                Is.EqualTo("PutDown_TwoHands_Crouch"));
            Assert.That(
                PlayerAnimationDriver.ResolvePutDownClip(PlayerPosture.Prone),
                Is.EqualTo("PutDown_TwoHands_Prone"));
            Assert.That(
                PlayerAnimationDriver.TransitionClip(
                    PlayerPosture.Standing, PlayerPosture.Crouching, false),
                Is.EqualTo("Crouch_Start"));
            Assert.That(
                PlayerAnimationDriver.TransitionClip(
                    PlayerPosture.Standing, PlayerPosture.Crouching, true),
                Is.EqualTo("Carry_TwoHands_Crouch_Start"));
            Assert.That(
                PlayerAnimationDriver.TransitionClip(
                    PlayerPosture.Crouching, PlayerPosture.Prone, true),
                Is.EqualTo("Carry_TwoHands_Crouch_To_Prone"));
        }

        [Test]
        public void PickupOneShot_IsInterruptedByLocomotion()
        {
            Assert.That(PlayerAnimationDriver.IsMovementInterruptible("Pickup_Low"), Is.True);
            Assert.That(PlayerAnimationDriver.IsMovementInterruptible("PutUp_TwoHands"), Is.True);
            Assert.That(PlayerAnimationDriver.IsMovementInterruptible("PutDown_Prone"), Is.True);
            // 1회성 표현은 걸으면 끊기고, 춤은 걸어도 이어진다.
            Assert.That(PlayerAnimationDriver.IsMovementInterruptible("Emote_Wave"), Is.True);
            Assert.That(PlayerAnimationDriver.IsMovementInterruptible("Emote_HipHop"), Is.False);
            Assert.That(PlayerAnimationDriver.IsMovementInterruptible("Land"), Is.True);
            Assert.That(PlayerAnimationDriver.IsMovementInterruptible("Carry_TwoHands_Land"), Is.True);
            Assert.That(PlayerAnimationDriver.IsMovementInterruptible("Prone_End"), Is.False);
            Assert.That(PlayerAnimationDriver.IsLocomotionMoving("Carry_TwoHands_Walk_Forward"), Is.True);
            Assert.That(PlayerAnimationDriver.IsLocomotionMoving("Carry_TwoHands_Crawl_Forward"), Is.True);
            Assert.That(PlayerAnimationDriver.IsLocomotionMoving("Carry_TwoHands"), Is.False);
            Assert.That(PlayerAnimationDriver.ResolveJumpClip(false), Is.EqualTo("Jump"));
            Assert.That(PlayerAnimationDriver.ResolveJumpClip(true), Is.EqualTo("Carry_TwoHands_Jump"));
            Assert.That(PlayerAnimationDriver.ResolveLandClip(false), Is.EqualTo("Land"));
            Assert.That(PlayerAnimationDriver.ResolveLandClip(true), Is.EqualTo("Carry_TwoHands_Land"));
            Assert.That(PlayerAnimationDriver.IsJumpState("Carry_TwoHands_Jump"), Is.True);
            Assert.That(PlayerAnimationDriver.IsJumpState("Fall"), Is.False);
            Assert.That(
                PlayerAnimationDriver.ResolveThrowClip(PlayerPosture.Standing, 0f, 4f, 7f),
                Is.EqualTo("Throw_TwoHands"));
            Assert.That(
                PlayerAnimationDriver.ResolveThrowClip(PlayerPosture.Standing, 4f, 4f, 7f),
                Is.EqualTo("Throw_TwoHands_Walk"));
            Assert.That(
                PlayerAnimationDriver.ResolveThrowClip(PlayerPosture.Standing, 7f, 4f, 7f),
                Is.EqualTo("Throw_TwoHands_Run"));
            Assert.That(
                PlayerAnimationDriver.ResolveThrowClip(PlayerPosture.Crouching, 0f, 4f, 7f),
                Is.EqualTo("Throw_TwoHands_Crouch"));
            Assert.That(
                PlayerAnimationDriver.ResolveThrowClip(PlayerPosture.Crouching, 2f, 4f, 7f),
                Is.EqualTo("Throw_TwoHands_Crouch_Walk"));
            Assert.That(
                PlayerAnimationDriver.ResolveThrowClip(PlayerPosture.Prone, 0f, 4f, 7f),
                Is.EqualTo("Throw_TwoHands_Prone"));
            Assert.That(
                PlayerAnimationDriver.ResolveThrowClip(PlayerPosture.Prone, 0.8f, 4f, 7f),
                Is.EqualTo("Throw_TwoHands_Crawl"));
            Assert.That(
                PlayerAnimationDriver.ResolvePlaybackSpeed("Throw_TwoHands_Walk", 7f, 4f, 7f, 2f, 0.8f),
                Is.EqualTo(1f));
            Assert.That(
                PlayerAnimationDriver.ResolvePunchClip(PlayerPosture.Standing, 0f, 4f, 7f),
                Is.EqualTo("Punch"));
            Assert.That(
                PlayerAnimationDriver.ResolvePunchClip(PlayerPosture.Standing, 4f, 4f, 7f),
                Is.EqualTo("Punch_Walk"));
            Assert.That(
                PlayerAnimationDriver.ResolvePunchClip(PlayerPosture.Standing, 7f, 4f, 7f),
                Is.EqualTo("Punch_Run"));
            Assert.That(
                PlayerAnimationDriver.ResolvePunchClip(PlayerPosture.Crouching, 0f, 4f, 7f),
                Is.EqualTo("Punch_Crouch"));
            Assert.That(
                PlayerAnimationDriver.ResolvePunchClip(PlayerPosture.Crouching, 2f, 4f, 7f),
                Is.EqualTo("Punch_Crouch_Walk"));
            Assert.That(
                PlayerAnimationDriver.ResolveHitClip(PlayerPosture.Standing, 4f, 4f, 7f),
                Is.EqualTo("Hit_Walk"));
            Assert.That(
                PlayerAnimationDriver.ResolveHitClip(PlayerPosture.Crouching, 0f, 4f, 7f),
                Is.EqualTo("Hit_Crouch"));
            Assert.That(
                PlayerAnimationDriver.ResolveHitClip(PlayerPosture.Standing, 0f, 4f, 7f, true),
                Is.EqualTo("Carry_TwoHands_Hit"));
            Assert.That(
                PlayerAnimationDriver.ResolveHitClip(PlayerPosture.Standing, 4f, 4f, 7f, true),
                Is.EqualTo("Carry_TwoHands_Hit_Walk"));
            Assert.That(
                PlayerAnimationDriver.ResolveHitClip(PlayerPosture.Standing, 7f, 4f, 7f, true),
                Is.EqualTo("Carry_TwoHands_Hit_Run"));
            Assert.That(
                PlayerAnimationDriver.ResolveHitClip(PlayerPosture.Crouching, 0f, 4f, 7f, true),
                Is.EqualTo("Carry_TwoHands_Hit_Crouch"));
            Assert.That(
                PlayerAnimationDriver.ResolveHitClip(PlayerPosture.Crouching, 2f, 4f, 7f, true),
                Is.EqualTo("Carry_TwoHands_Hit_Crouch_Walk"));
            Assert.That(
                PlayerAnimationDriver.ResolveHitClip(PlayerPosture.Prone, 0f, 4f, 7f),
                Is.EqualTo("Hit_Prone"));
            Assert.That(
                PlayerAnimationDriver.ResolveHitClip(PlayerPosture.Prone, 0.8f, 4f, 7f),
                Is.EqualTo("Hit_Crawl"));
            Assert.That(
                PlayerAnimationDriver.ResolveHitClip(PlayerPosture.Prone, 0f, 4f, 7f, true),
                Is.EqualTo("Carry_TwoHands_Hit_Prone"));
            Assert.That(
                PlayerAnimationDriver.ResolveHitClip(PlayerPosture.Prone, 0.8f, 4f, 7f, true),
                Is.EqualTo("Carry_TwoHands_Hit_Crawl"));
            Assert.That(
                PlayerAnimationDriver.ResolvePlaybackSpeed("Punch_Walk", 7f, 4f, 7f, 2f, 0.8f),
                Is.EqualTo(1f));
            Assert.That(
                PlayerAnimationDriver.ResolvePlaybackSpeed("Hit_Run", 7f, 4f, 7f, 2f, 0.8f),
                Is.EqualTo(1f));
            Assert.That(PlayerAnimationDriver.IsStunState("Stun_Start"), Is.True);
            Assert.That(PlayerAnimationDriver.IsStunState("Stun_Idle"), Is.True);
            Assert.That(PlayerAnimationDriver.IsStunState("Stun_End"), Is.True);
            Assert.That(PlayerAnimationDriver.IsStunState("Idle"), Is.False);
        }

        [UnityTest]
        public IEnumerator ReplicatedStun_PlaysAndRecoversForLocalAndRemoteCharacters()
        {
            UnityEditor.SceneManagement.EditorSceneManager.NewScene(
                UnityEditor.SceneManagement.NewSceneSetup.EmptyScene,
                UnityEditor.SceneManagement.NewSceneMode.Single);
            yield return new EnterPlayMode();
            foreach (var acceptsLocalInput in new[] { true, false })
            {
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(
                    "Assets/_Game/Content/Prefabs/PlayerCharacter.prefab");
                var player = Object.Instantiate(prefab);
                try
                {
                    var combatant = player.GetComponent<PlayerCombatant>();
                    var driver = player.GetComponent<PlayerAnimationDriver>();
                    var animator = player.GetComponentInChildren<Animator>();
                    animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
                    combatant.enabled = acceptsLocalInput;
                    combatant.ConfigureNetworkPlayer(0, acceptsLocalInput);
                    driver.ApplyNetworkState(0f, true, 0, Vector2.zero, false);
                    combatant.SetNetworkHitCount(0);
                    combatant.SetNetworkStunned(true);
                    combatant.SetNetworkHitCount(3);
                    // Explicitly evaluate the animation in the headless EditMode harness.
                    driver.SendMessage("Update");
                    animator.Update(0.3f);
                    Assert.That(combatant.IsStunned, Is.True);
                    Assert.That(animator.GetCurrentAnimatorStateInfo(0).IsName("Stun_Start"),
                        Is.True, $"Stun start missing; local input={acceptsLocalInput}");
                    var clip = animator.GetCurrentAnimatorClipInfo(0);
                    Assert.That(clip.Length, Is.GreaterThan(0));
                    Assert.That(clip[0].clip.length, Is.GreaterThan(0f));
                    combatant.SetNetworkStunned(false);
                    driver.SendMessage("Update");
                    animator.Update(0.3f);
                    Assert.That(animator.GetCurrentAnimatorStateInfo(0).IsName("Stun_End"), Is.True);
                    yield return new WaitForSeconds(PlayerAnimationDriver.StunEndSeconds);
                    driver.SendMessage("Update");
                    animator.Update(0.2f);
                    Assert.That(animator.GetCurrentAnimatorStateInfo(0).IsName("Idle"), Is.True);
                }
                finally { Object.DestroyImmediate(player); }
            }
            yield return new ExitPlayMode();
        }

        [Test]
        public void PlayerCharacter_UsesFirstGenericVisualAndClips()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/_Game/Content/Prefabs/PlayerCharacter.prefab");
            Assert.That(prefab, Is.Not.Null);

            var visual = prefab.transform.Find("Visual");
            Assert.That(visual, Is.Not.Null);
            var source = PrefabUtility.GetCorrespondingObjectFromOriginalSource(visual.gameObject);
            Assert.That(source, Is.Not.Null);
            Assert.That(AssetDatabase.GetAssetPath(source), Does.Contain("SmoothBear.fbx"));

            var animator = visual.GetComponentInChildren<Animator>(true);
            Assert.That(animator, Is.Not.Null);
            Assert.That(animator.applyRootMotion, Is.False);
            var controller = animator.runtimeAnimatorController as AnimatorController;
            Assert.That(controller, Is.Not.Null);
            var names = controller.layers[0].stateMachine.states.Select(entry => entry.state.name).ToArray();
            Assert.That(names, Does.Contain("Idle"));
            Assert.That(names, Does.Contain("Walk_Left"));
            Assert.That(names, Does.Contain("Carry_TwoHands"));
            Assert.That(names, Does.Contain("Throw"));
            Assert.That(names, Does.Contain("Throw_TwoHands"));
            Assert.That(names, Does.Contain("Throw_TwoHands_Walk"));
            Assert.That(names, Does.Contain("Punch"));
            Assert.That(names, Does.Contain("Hit"));
            Assert.That(names, Does.Contain("Hit_Walk"));
            Assert.That(names, Does.Contain("Hit_Crouch"));
            Assert.That(names, Does.Contain("Hit_Prone"));
            Assert.That(names, Does.Contain("Stun_Start"));
            Assert.That(names, Does.Contain("Stun_Idle"));
            Assert.That(names, Does.Contain("Stun_End"));
            Assert.That(names, Does.Contain("Stunned"));

            var punch = controller.layers[0].stateMachine.states
                .Select(entry => entry.state)
                .First(state => state.name == "Punch");
            Assert.That(punch.motion, Is.Not.Null);
            Assert.That(punch.motion.name, Is.EqualTo("Punch"));

            var networked = AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/_Game/Content/Prefabs/NetworkedPlayer.prefab");
            Assert.That(networked.GetComponentInChildren<Animator>(true), Is.Not.Null);
            Assert.That(prefab.GetComponent<AvatarAppearanceApplier>(), Is.Not.Null);
        }

        [Test]
        public void HoldsLoopEmote_KeepsDancingThroughAJumpOrAFall()
        {
            // 점프·낙하·착지 클립은 감정 표현과 같은 원샷 슬롯을 쓴다. 춤이 슬롯을
            // 쥐고 있다고 답해야 그 클립들이 춤을 밀어내지 않는다.
            Assert.That(
                PlayerAnimationDriver.HoldsLoopEmote("Emote_HipHop", stillPlaying: true), Is.True);
            Assert.That(
                PlayerAnimationDriver.HoldsLoopEmote("Emote_Spin", stillPlaying: true), Is.True);
            Assert.That(
                PlayerAnimationDriver.HoldsLoopEmote("Emote_Chicken", stillPlaying: true), Is.True);
        }

        [Test]
        public void HoldsLoopEmote_LetsOneShotEmotesBeReplaced()
        {
            // 인사·도발은 걸으면 끊기는 표현이라 점프에도 자리를 내준다.
            Assert.That(
                PlayerAnimationDriver.HoldsLoopEmote("Emote_Wave", stillPlaying: true), Is.False);
            Assert.That(
                PlayerAnimationDriver.HoldsLoopEmote("Emote_Taunt", stillPlaying: true), Is.False);
        }

        [Test]
        public void HoldsLoopEmote_IgnoresAnExpiredOrAbsentEmote()
        {
            Assert.That(
                PlayerAnimationDriver.HoldsLoopEmote("Emote_HipHop", stillPlaying: false), Is.False);
            Assert.That(PlayerAnimationDriver.HoldsLoopEmote(null, stillPlaying: true), Is.False);
            Assert.That(PlayerAnimationDriver.HoldsLoopEmote("Jump", stillPlaying: true), Is.False);
            Assert.That(PlayerAnimationDriver.HoldsLoopEmote("Land", stillPlaying: true), Is.False);
        }
    }
}

using System;
using System.Collections.Generic;
using Game.Client.Combat;
using Game.Client.Interactions;
using Game.Client.Players;
using Game.Core.Match;
using Game.Core.Players;
using Game.Network.Match;
using Game.Network;
using Game.Network.Players;
using Game.Network.Session;
using Game.Server.Items;
using Game.Server.Match;
using Game.SOAP.Config;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Game.Architecture.Tests
{
    public sealed class NetworkContractTests
    {
        [Test]
        public void Participant_UsesOnlyStablePlayerIndex()
        {
            var participant = new MatchParticipant("player", 3);

            Assert.That(participant.PlayerIndex, Is.EqualTo(3));
            Assert.That(typeof(MatchParticipant).GetProperty("Seat"), Is.Null);
        }

        [Test]
        public void Participant_ConvertsRoomSeatOrderToContiguousPlayerIndices()
        {
            var participants = MatchParticipant.FromRoomParticipants(new[]
            {
                new Game.Core.Rooms.RoomParticipant("late-seat", 5, false),
                new Game.Core.Rooms.RoomParticipant("host", 0, true),
                new Game.Core.Rooms.RoomParticipant("middle-seat", 3, false),
            });

            Assert.That(
                Array.ConvertAll(participants, participant => participant.PlayerId),
                Is.EqualTo(new[] { "host", "middle-seat", "late-seat" }));
            Assert.That(
                Array.ConvertAll(participants, participant => participant.PlayerIndex),
                Is.EqualTo(new[] { 0, 1, 2 }));
        }

        [Test]
        public void InputIntent_NormalizesMovementYawAndButtons()
        {
            var input = new PlayerInputIntent(
                3f,
                4f,
                -90f,
                PlayerInputButtons.Jump | PlayerInputButtons.Sprint);

            Assert.That(input.MoveX, Is.EqualTo(0.6f).Within(0.0001f));
            Assert.That(input.MoveY, Is.EqualTo(0.8f).Within(0.0001f));
            Assert.That(input.LookYawDegrees, Is.EqualTo(270f));
            Assert.That(input.IsPressed(PlayerInputButtons.Jump), Is.True);
            Assert.That(input.IsPressed(PlayerInputButtons.Prone), Is.False);
        }

        [Test]
        public void InputIntent_RejectsInvalidNetworkValues()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() =>
                new PlayerInputIntent(float.NaN, 0f, 0f, PlayerInputButtons.None));
            Assert.Throws<ArgumentOutOfRangeException>(() =>
                new PlayerInputIntent(0f, 0f, 0f, (PlayerInputButtons)128));
        }

        [Test]
        public void NetworkInput_PreservesIntentAndUsesCameraYaw()
        {
            var intent = new PlayerInputIntent(
                0f,
                1f,
                90f,
                PlayerInputButtons.Jump |
                PlayerInputButtons.Sprint |
                PlayerInputButtons.Attack);
            var input = NetworkPlayerInput.FromIntent(intent);
            var direction = NetworkPlayerMotor.ToWorldDirection(
                input.Move,
                input.LookYawDegrees);

            Assert.That(input.IsPressed(NetworkPlayerButton.Jump), Is.True);
            Assert.That(input.IsPressed(NetworkPlayerButton.Sprint), Is.True);
            Assert.That(input.IsPressed(NetworkPlayerButton.Attack), Is.True);
            Assert.That(direction.x, Is.EqualTo(1f).Within(0.0001f));
            Assert.That(direction.z, Is.EqualTo(0f).Within(0.0001f));
        }

        [Test]
        public void NetworkPlayerPrefab_HasAuthoritativeMovementComponents()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/_Game/Content/Prefabs/NetworkedPlayer.prefab");

            Assert.That(prefab, Is.Not.Null, "NetworkedPlayer prefab is missing.");
            Assert.That(prefab.GetComponent<NetworkPlayerMotor>(), Is.Not.Null,
                "NetworkPlayerMotor is missing.");
            Assert.That(prefab.GetComponent<Fusion.NetworkTransform>(), Is.Not.Null,
                "NetworkTransform is missing.");
            Assert.That(prefab.GetComponent<CharacterController>(), Is.Not.Null,
                "CharacterController is missing.");
            Assert.That(
                prefab.GetComponent<PlayerMovement>(),
                Is.InstanceOf<IPlayerInputIntentSource>());
        }

        [Test]
        public void NetworkPlayerPrefab_LimitsLocalInputToItsOwner()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/_Game/Content/Prefabs/NetworkedPlayer.prefab");
            var avatar = prefab.GetComponent<PlayerAvatar>();
            var ownerOnly = new SerializedObject(avatar).FindProperty("_ownerOnly");
            var types = new HashSet<Type>();

            for (var index = 0; index < ownerOnly.arraySize; index++)
            {
                var behaviour = ownerOnly.GetArrayElementAtIndex(index)
                    .objectReferenceValue as Behaviour;
                Assert.That(behaviour, Is.Not.Null);
                types.Add(behaviour.GetType());
            }

            Assert.That(types, Is.EquivalentTo(new[]
            {
                typeof(PlayerMovement),
                typeof(PlayerInteractor),
                typeof(ItemPlacementController),
                typeof(PlayerCombatant),
            }));
        }

        [Test]
        public void NetworkPlayer_UsesTheSameMovementSettingsAsLocalPlayer()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/_Game/Content/Prefabs/NetworkedPlayer.prefab");
            var source = prefab.GetComponent<PlayerMovement>();
            var settings = source.MovementSettings;
            var config = AssetDatabase.LoadAssetAtPath<MovementConfigSO>(
                "Assets/_Game/Content/Config/MovementConfig.asset");

            Assert.That(config, Is.Not.Null);
            Assert.That(settings.WalkSpeed, Is.EqualTo(config.WalkSpeed));
            Assert.That(settings.SprintSpeed, Is.EqualTo(config.SprintSpeed));
            Assert.That(settings.RotationSpeedDegrees,
                Is.EqualTo(config.RotationSpeedDegrees));
            Assert.That(settings.JumpHeight, Is.EqualTo(config.JumpHeight));
            Assert.That(settings.GravityMultiplier,
                Is.EqualTo(config.GravityMultiplier));
        }

        [Test]
        public void SharedMovementKinematics_JumpRisesThenFallsContinuously()
        {
            var settings = new PlayerMovementSettings(4f, 7f, 720f, 1.1f, 2f);
            const float deltaTime = 1f / 60f;

            var takeoff = PlayerMovementKinematics.StepVerticalVelocity(
                0f, true, true, -9.81f, deltaTime, settings);
            var nextTick = PlayerMovementKinematics.StepVerticalVelocity(
                takeoff, false, false, -9.81f, deltaTime, settings);

            Assert.That(takeoff, Is.GreaterThan(0f));
            Assert.That(nextTick, Is.LessThan(takeoff));
            Assert.That(nextTick, Is.GreaterThan(0f));
        }

        [Test]
        public void MatchStateSnapshot_RejectsInvalidReplicatedState()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() =>
                new MatchStateSnapshot((MatchPhase)99, 1d));
            Assert.Throws<ArgumentOutOfRangeException>(() =>
                new MatchStateSnapshot(MatchPhase.Hiding, double.NaN));
        }

        [Test]
        public void MatchStarter_ForwardsReplicatedPhaseSnapshot()
        {
            var gameObject = new GameObject("MatchStarterTest");

            try
            {
                var starter = gameObject.AddComponent<MatchStarter>();
                var expected = new MatchStateSnapshot(MatchPhase.Searching, 120d);
                var received = new MatchStateSnapshot();
                var wasReceived = false;

                starter.MatchStateReceived += snapshot =>
                {
                    received = snapshot;
                    wasReceived = true;
                };

                starter.PublishSnapshot(expected);

                Assert.That(wasReceived, Is.True);
                Assert.That(received.Phase, Is.EqualTo(MatchPhase.Searching));
                Assert.That(received.PhaseEndsAt, Is.EqualTo(120d));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(gameObject);
            }
        }

        [Test]
        public void ShredderEjectionVelocity_UsesSpotForwardAndAddsLift()
        {
            var rotation = Quaternion.Euler(0f, 90f, 0f);

            var velocity = MatchStarter.CalculateShredderEjectionVelocity(rotation);

            Assert.That(velocity.y, Is.GreaterThan(0f));
            Assert.That(Vector3.Dot(velocity, rotation * Vector3.forward), Is.GreaterThan(0f));
        }

        [Test]
        public void ActionRequestRpcs_DeriveRequesterFromRpcInfo()
        {
            var names = new[]
            {
                "RPC_RequestHold",
                "RPC_RequestDrop",
                "RPC_RequestRelease",
                "RPC_RequestThrow",
                "RPC_RequestHit",
                "RPC_RequestShredder",
                "RPC_RequestReturnToLobby",
            };

            foreach (var name in names)
            {
                var rpc = typeof(MatchSessionState).GetMethod(name);

                Assert.That(rpc, Is.Not.Null, name);
                var attribute = (Fusion.RpcAttribute)Attribute.GetCustomAttribute(
                    rpc,
                    typeof(Fusion.RpcAttribute));
                Assert.That(attribute, Is.Not.Null, name);
                Assert.That(attribute.Sources, Is.EqualTo(Fusion.RpcSources.All), name);
                Assert.That(
                    attribute.Targets,
                    Is.EqualTo(Fusion.RpcTargets.StateAuthority),
                    name);
                Assert.That(
                    Array.Exists(
                        rpc.GetParameters(),
                        parameter => parameter.ParameterType == typeof(Fusion.RpcInfo)),
                    Is.True,
                    name);
                Assert.That(
                    Array.Exists(
                        rpc.GetParameters(),
                        parameter => parameter.Name == "playerIndex"),
                    Is.False,
                    name);
            }
        }

        [Test]
        public void NetworkScenes_ContainsBuildListedMatchAndLobbyScenes()
        {
            var scenes = AssetDatabase.LoadAssetAtPath<NetworkScenes>(
                "Assets/_Game/Content/Settings/NetworkScenes.asset");

            Assert.That(scenes, Is.Not.Null);
            Assert.That(scenes.MatchScene.IsValid, Is.True);
            Assert.That(scenes.LobbyScene.IsValid, Is.True);
            Assert.That(scenes.MatchScene, Is.Not.EqualTo(scenes.LobbyScene));
        }

        [Test]
        public void MatchEventRpcs_BroadcastOnlyAuthorityConfirmedData()
        {
            var names = new[]
            {
                "RPC_NotifyItemDestroyed",
                "RPC_NotifyPlayerStunned",
                "RPC_NotifyObjectThrown",
                "RPC_NotifyFinalWarning",
            };

            foreach (var name in names)
            {
                var rpc = typeof(MatchSessionState).GetMethod(name);

                Assert.That(rpc, Is.Not.Null, name);
                var attribute = (Fusion.RpcAttribute)Attribute.GetCustomAttribute(
                    rpc,
                    typeof(Fusion.RpcAttribute));
                Assert.That(attribute, Is.Not.Null, name);
                Assert.That(
                    attribute.Sources,
                    Is.EqualTo(Fusion.RpcSources.StateAuthority),
                    name);
                Assert.That(
                    attribute.Targets,
                    Is.EqualTo(Fusion.RpcTargets.All),
                    name);
            }

            var destroyedRpc = typeof(MatchSessionState).GetMethod(
                "RPC_NotifyItemDestroyed");
            Assert.That(
                Array.Exists(
                    destroyedRpc.GetParameters(),
                    parameter => parameter.Name.IndexOf(
                        "owner",
                        StringComparison.OrdinalIgnoreCase) >= 0),
                Is.False,
                "Destroyed item notifications must not reveal its owner.");
        }

        [Test]
        public void MatchStarter_ForwardsReplicatedActivityAndResult()
        {
            var gameObject = new GameObject("MatchStarterTest");

            try
            {
                var starter = gameObject.AddComponent<MatchStarter>();
                IReadOnlyList<bool> receivedActivity = null;
                IReadOnlyList<PlayerInteractionStateSnapshot> receivedInteractions = null;
                MatchResult? receivedResult = null;
                starter.ParticipantActivityReceived += value =>
                    receivedActivity = value;
                starter.PlayerInteractionStatesReceived += value =>
                    receivedInteractions = value;
                starter.MatchResultReceived += value => receivedResult = value;

                starter.PublishParticipantActivity(new[] { true, false });
                starter.PublishPlayerInteractionStates(new[]
                {
                    new PlayerInteractionStateSnapshot(0, 12d, 4),
                    new PlayerInteractionStateSnapshot(1, 0d, 5),
                });
                starter.PublishMatchResult(new MatchResult(
                    MatchEndReason.LastPlayerStanding,
                    300d,
                    new[] { 0 }));

                Assert.That(receivedActivity, Is.EqualTo(new[] { true, false }));
                Assert.That(receivedInteractions.Count, Is.EqualTo(2));
                Assert.That(receivedInteractions[0].StunEndsAt, Is.EqualTo(12d));
                Assert.That(
                    receivedInteractions[0].RemainingDestructionUses,
                    Is.EqualTo(4));
                Assert.That(receivedResult.HasValue, Is.True);
                Assert.That(
                    receivedResult.Value.EndReason,
                    Is.EqualTo(MatchEndReason.LastPlayerStanding));
                Assert.That(receivedResult.Value.EndedAt, Is.EqualTo(300d));
                Assert.That(receivedResult.Value.WinnerPlayerIndices, Is.EqualTo(new[] { 0 }));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(gameObject);
            }
        }

        [Test]
        public void PlayerInteractionState_IsPersistentNetworkedData()
        {
            var stunEndsAt = typeof(MatchSessionState).GetProperty("StunEndsAt");
            var remainingUses = typeof(MatchSessionState).GetProperty(
                "RemainingDestructionUses");

            Assert.That(stunEndsAt, Is.Not.Null);
            Assert.That(remainingUses, Is.Not.Null);
            Assert.That(
                Attribute.IsDefined(stunEndsAt, typeof(Fusion.NetworkedAttribute)),
                Is.True);
            Assert.That(
                Attribute.IsDefined(remainingUses, typeof(Fusion.NetworkedAttribute)),
                Is.True);

            var snapshot = new PlayerInteractionStateSnapshot(1, 15d, 3);
            Assert.That(snapshot.IsStunned(14.99d), Is.True);
            Assert.That(snapshot.IsStunned(15d), Is.False);
        }

        [Test]
        public void ObjectStateSnapshot_PreservesPhysicsAndVisibilityState()
        {
            var pose = new Pose(new Vector3(1f, 2f, 3f), Quaternion.Euler(0f, 45f, 0f));
            var velocity = new Vector3(4f, 5f, 6f);
            var snapshot = new MatchObjectStateSnapshot(
                "Soda_01",
                2,
                pose,
                velocity,
                true,
                7,
                true);

            Assert.That(snapshot.ObjectId, Is.EqualTo("Soda_01"));
            Assert.That(snapshot.HolderPlayerIndex, Is.EqualTo(2));
            Assert.That(snapshot.Pose, Is.EqualTo(pose));
            Assert.That(snapshot.InitialVelocity, Is.EqualTo(velocity));
            Assert.That(snapshot.IsDestroyed, Is.True);
            Assert.That(snapshot.Version, Is.EqualTo(7));
            Assert.That(snapshot.IsPhysicsActive, Is.True);
            Assert.That(snapshot.IsPendingEjection, Is.False);
        }

        [Test]
        public void HighlightReplaySerializer_RoundTripsOrderTimingAndFrames()
        {
            var segment = new HighlightSegment(10d, 12d, 2d);
            var candidate = new HighlightCandidate(
                HighlightType.FirstBlood,
                new[] { segment },
                "player-1");
            var frame = new HighlightReplayFrame(
                11d,
                new[]
                {
                    new Pose(Vector3.one, Quaternion.Euler(0f, 45f, 0f)),
                },
                new[]
                {
                    new WorldObjectState(
                        "Soda_01",
                        new Pose(Vector3.right, Quaternion.identity)),
                });
            var source = new[]
            {
                new HighlightReplayData(
                    candidate,
                    new[] { new HighlightReplayClip(segment, new[] { frame }) }),
            };

            var payload = HighlightReplaySerializer.Serialize(source);

            Assert.That(
                HighlightReplaySerializer.TryDeserialize(payload, out var restored),
                Is.True);
            Assert.That(restored, Has.Length.EqualTo(1));
            Assert.That(restored[0].Candidate.Type, Is.EqualTo(HighlightType.FirstBlood));
            Assert.That(restored[0].Candidate.TargetId, Is.EqualTo("player-1"));
            Assert.That(restored[0].Clips[0].Segment.PlaybackSpeed, Is.EqualTo(2d));
            Assert.That(restored[0].Clips[0].Frames[0].RecordedAt, Is.EqualTo(11d));
            Assert.That(
                restored[0].Clips[0].Frames[0].WorldObjects[0].ObjectId,
                Is.EqualTo("Soda_01"));
        }
    }
}

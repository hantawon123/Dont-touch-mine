using System;
using Game.Client.Interactions;
using Game.Client.Lobby;
using Game.Core.Lobby;
using NUnit.Framework;
using R3;
using UnityEngine;

namespace Game.Tests.EditMode
{
    public sealed class LobbyPlanBoardPresenterTests
    {
        private GameObject boardObject;
        private GameObject interactorObject;

        [TearDown]
        public void TearDown()
        {
            if (boardObject != null) UnityEngine.Object.DestroyImmediate(boardObject);
            if (interactorObject != null) UnityEngine.Object.DestroyImmediate(interactorObject);
        }

        [Test]
        public void UnboundBoard_ShowsNoInteractionAndIgnoresF()
        {
            var board = CreateBoard();
            var interactor = CreateInteractor();

            Assert.That(board.IsBound, Is.False);
            Assert.That(board.CanInteract(interactor), Is.False);
            Assert.DoesNotThrow(() => board.Interact(interactor));
        }

        [TestCase(true)]
        [TestCase(false)]
        public void AttachedBoard_PromptDependsOnHost(bool isHost)
        {
            using var session = new HostSession();
            session.SetLocalHost(isHost);
            var opener = new Opener();
            using var presenter = new LobbyPlanBoardPresenter(opener, session);
            var board = CreateBoard();

            presenter.Attach(board);

            Assert.That(
                board.InteractionPrompt,
                Is.EqualTo(isHost
                    ? LobbyPlanBoardInteractable.HostPrompt
                    : LobbyPlanBoardInteractable.GuestPrompt));
        }

        [Test]
        public void AttachedBoard_InteractOpensPlaySettingsOnce()
        {
            using var session = new HostSession();
            var opener = new Opener();
            using var presenter = new LobbyPlanBoardPresenter(opener, session);
            var board = CreateBoard();
            var interactor = CreateInteractor();
            presenter.Attach(board);

            Assert.That(board.CanInteract(interactor), Is.True);
            board.Interact(interactor);

            Assert.That(opener.OpenCount, Is.EqualTo(1));
        }

        [Test]
        public void HostChange_IsReflectedWithoutReattaching()
        {
            using var session = new HostSession();
            session.SetLocalHost(false);
            var opener = new Opener();
            using var presenter = new LobbyPlanBoardPresenter(opener, session);
            var board = CreateBoard();
            presenter.Attach(board);
            Assert.That(board.InteractionPrompt, Is.EqualTo(LobbyPlanBoardInteractable.GuestPrompt));

            session.SetLocalHost(true);

            Assert.That(board.InteractionPrompt, Is.EqualTo(LobbyPlanBoardInteractable.HostPrompt));
            Assert.That(board.IsBound, Is.True);
        }

        [Test]
        public void Dispose_UnbindsBoard()
        {
            using var session = new HostSession();
            var opener = new Opener();
            var presenter = new LobbyPlanBoardPresenter(opener, session);
            var board = CreateBoard();
            var interactor = CreateInteractor();
            presenter.Attach(board);

            presenter.Dispose();

            Assert.That(board.IsBound, Is.False);
            Assert.That(board.CanInteract(interactor), Is.False);
            board.Interact(interactor);
            Assert.That(opener.OpenCount, Is.Zero);
        }

        [Test]
        public void Start_FindsBoardInScene()
        {
            using var session = new HostSession();
            var opener = new Opener();
            using var presenter = new LobbyPlanBoardPresenter(opener, session);
            var board = CreateBoard();

            presenter.Start();

            // The open scene may hold its own board; any bound board proves the lookup.
            Assert.That(presenter.Board, Is.Not.Null);
            Assert.That(presenter.Board.IsBound, Is.True);
        }

        private LobbyPlanBoardInteractable CreateBoard()
        {
            boardObject = new GameObject("PlanBoard");
            boardObject.AddComponent<BoxCollider>();
            return boardObject.AddComponent<LobbyPlanBoardInteractable>();
        }

        private PlayerInteractor CreateInteractor()
        {
            interactorObject = new GameObject("Interactor");
            return interactorObject.AddComponent<PlayerInteractor>();
        }

        private sealed class Opener : IPlaySettingsOpener
        {
            public int OpenCount;
            public void OpenPlaySettingsFromWorld() => OpenCount++;
        }

        private sealed class HostSession : ILobbyHostSession, IDisposable
        {
            private readonly ReactiveProperty<bool> host = new(false);
            private readonly ReactiveProperty<PlaySettingsDraft> settings =
                new(new PlaySettingsDraft("방", "CODE", false, null, 6, 3, "playground"));

            public string LocalPlayerId => "me";
            public ReadOnlyReactiveProperty<bool> IsLocalHost => host;
            public ReadOnlyReactiveProperty<PlaySettingsDraft> Settings => settings;
            public event Action StartRequested { add { } remove { } }
            public event Action<string> KickRequested { add { } remove { } }
            public event Action<string> HostTransferRequested { add { } remove { } }
            public event Action<PlaySettingsDraft> SettingsApplyRequested { add { } remove { } }
            public void SetLocalHost(bool value) => host.Value = value;
            public void ReplaceSettings(PlaySettingsDraft value) => settings.Value = value;
            public void RequestStart() { }
            public void RequestKick(string id) { }
            public void RequestHostTransfer(string id) { }
            public void RequestApplySettings(PlaySettingsDraft value) => settings.Value = value;
            public void Dispose() { host.Dispose(); settings.Dispose(); }
        }
    }
}

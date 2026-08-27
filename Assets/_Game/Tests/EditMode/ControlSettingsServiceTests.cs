using System.Collections.Generic;
using Game.Client.Controls;
using Game.Core.Settings;
using NUnit.Framework;

namespace Game.Tests.EditMode
{
    public sealed class ControlSettingsServiceTests
    {
        [Test]
        public void Construction_LoadsStoreAndAppliesImmediately()
        {
            var stored = new ControlSettingsState();
            stored.TrySetPath(ControlAction.Jump, "<Keyboard>/g", out _);
            var store = new MemoryControlSettingsStore { NextLoad = stored };
            var applier = new RecordingControlSettingsApplier();

            var service = new ControlSettingsService(store, applier);

            Assert.That(service.Current.GetPath(ControlAction.Jump), Is.EqualTo("<Keyboard>/g"));
            Assert.That(applier.Applied.Count, Is.EqualTo(1));
        }

        [Test]
        public void TrySetPath_PersistsAndReapplies()
        {
            var store = new MemoryControlSettingsStore();
            var applier = new RecordingControlSettingsApplier();
            var service = new ControlSettingsService(store, applier);
            var changedCount = 0;
            service.Changed += _ => changedCount++;

            Assert.That(
                service.TrySetPath(ControlAction.Crouch, "<Keyboard>/x", out var error),
                Is.True);
            Assert.That(error, Is.EqualTo(ControlSettingsError.None));
            Assert.That(service.Current.GetPath(ControlAction.Crouch), Is.EqualTo("<Keyboard>/x"));
            Assert.That(store.Saved.GetPath(ControlAction.Crouch), Is.EqualTo("<Keyboard>/x"));
            Assert.That(applier.Applied.Count, Is.EqualTo(2));
            Assert.That(changedCount, Is.EqualTo(1));
        }

        [Test]
        public void TrySetPath_RejectsDuplicateAndDoesNotPersist()
        {
            var store = new MemoryControlSettingsStore();
            var applier = new RecordingControlSettingsApplier();
            var service = new ControlSettingsService(store, applier);
            ControlAction? conflict = null;
            service.BindingConflict += occupiedBy => conflict = occupiedBy;

            Assert.That(
                service.TrySetPath(ControlAction.Jump, "<Keyboard>/w", out var error),
                Is.False);
            Assert.That(error, Is.EqualTo(ControlSettingsError.DuplicatePath));
            Assert.That(service.Current.GetPath(ControlAction.Jump), Is.EqualTo("<Keyboard>/space"));
            Assert.That(store.Saved, Is.Null);
            Assert.That(applier.Applied.Count, Is.EqualTo(1));
            Assert.That(conflict, Is.EqualTo(ControlAction.MoveForward));
        }

        [Test]
        public void TryStartRebind_CompletesAndSavesPath()
        {
            var store = new MemoryControlSettingsStore();
            var applier = new RecordingControlSettingsApplier();
            var service = new ControlSettingsService(store, applier);
            var listening = new List<ControlAction?>();
            service.RebindListeningChanged += listening.Add;

            Assert.That(service.TryStartRebind(ControlAction.Jump, out var error), Is.True);
            Assert.That(error, Is.EqualTo(ControlSettingsError.None));
            Assert.That(service.ListeningAction, Is.EqualTo(ControlAction.Jump));
            Assert.That(listening, Has.Member(ControlAction.Jump));

            applier.CompleteLast("<Keyboard>/h");

            Assert.That(service.ListeningAction, Is.Null);
            Assert.That(service.Current.GetPath(ControlAction.Jump), Is.EqualTo("<Keyboard>/h"));
            Assert.That(store.Saved.GetPath(ControlAction.Jump), Is.EqualTo("<Keyboard>/h"));
        }

        [Test]
        public void ControlBindingDisplay_FormatsMouseAndKeys()
        {
            Assert.That(
                ControlBindingDisplay.ToLabel("<Mouse>/leftButton"),
                Is.EqualTo("마우스 좌클릭"));
            Assert.That(
                ControlBindingDisplay.ToLabel("<Mouse>/rightButton"),
                Is.EqualTo("마우스 우클릭"));
            Assert.That(
                ControlBindingDisplay.ToLabel("<Mouse>/scroll/y"),
                Is.EqualTo("마우스 스크롤"));
            Assert.That(ControlBindingDisplay.ToLabel("<Keyboard>/w"), Is.EqualTo("W"));
            Assert.That(ControlBindingDisplay.ToLabel("<Keyboard>/leftShift"), Is.EqualTo("Shift"));
            Assert.That(ControlBindingDisplay.ToLabel("<Keyboard>/space"), Is.EqualTo("Space"));
        }

        private sealed class MemoryControlSettingsStore : IControlSettingsStore
        {
            public ControlSettingsState NextLoad { get; set; }

            public ControlSettingsState Saved { get; private set; }

            public ControlSettingsState LoadOrDefault()
            {
                return NextLoad ?? new ControlSettingsState();
            }

            public void Save(ControlSettingsState settings)
            {
                Saved = settings;
            }
        }

        private sealed class RecordingControlSettingsApplier : IControlSettingsApplier
        {
            public List<ControlSettingsState> Applied { get; } = new List<ControlSettingsState>();

            private System.Action<string> completed;
            private System.Action cancelled;

            public void Apply(ControlSettingsState settings)
            {
                Applied.Add(settings);
            }

            public void StartRebind(
                ControlAction action,
                System.Action<string> completed,
                System.Action cancelled)
            {
                this.completed = completed;
                this.cancelled = cancelled;
            }

            public void CancelRebind()
            {
                cancelled = null;
                completed = null;
            }

            public void CompleteLast(string path)
            {
                completed?.Invoke(path);
            }
        }
    }
}

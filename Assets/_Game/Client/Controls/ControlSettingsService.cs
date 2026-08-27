using System;
using Game.Core.Settings;

namespace Game.Client.Controls
{
    public sealed class ControlSettingsService : IControlSettings
    {
        private readonly IControlSettingsStore store;
        private readonly IControlSettingsApplier applier;
        private bool rebinding;

        public ControlSettingsService(
            IControlSettingsStore store,
            IControlSettingsApplier applier)
        {
            this.store = store ?? throw new ArgumentNullException(nameof(store));
            this.applier = applier ?? throw new ArgumentNullException(nameof(applier));
            Current = store.LoadOrDefault() ?? new ControlSettingsState();
            ApplyCurrent();
        }

        public ControlSettingsState Current { get; }

        public ControlAction? ListeningAction { get; private set; }

        public event Action<ControlSettingsState> Changed;

        public event Action<ControlAction?> RebindListeningChanged;

        public event Action<ControlAction> BindingConflict;

        public bool TrySetPath(ControlAction action, string path, out ControlSettingsError error)
        {
            if (Current.GetPath(action) == path)
            {
                error = ControlSettingsError.None;
                return true;
            }

            if (!Current.TrySetPath(action, path, out error))
            {
                if (error == ControlSettingsError.DuplicatePath &&
                    Current.TryFindConflict(action, path, out var occupiedBy))
                {
                    BindingConflict?.Invoke(occupiedBy);
                }

                return false;
            }

            PersistAndApply();
            return true;
        }

        public bool TryStartRebind(ControlAction action, out ControlSettingsError error)
        {
            if (!Enum.IsDefined(typeof(ControlAction), action))
            {
                error = ControlSettingsError.UnknownAction;
                return false;
            }

            CancelRebind();
            rebinding = true;
            ListeningAction = action;
            RebindListeningChanged?.Invoke(action);
            applier.StartRebind(action, OnRebindCompleted, OnRebindCancelled);
            error = ControlSettingsError.None;
            return true;
        }

        public void CancelRebind()
        {
            if (!rebinding)
            {
                return;
            }

            rebinding = false;
            applier.CancelRebind();
            ClearListening();
        }

        private void OnRebindCompleted(string path)
        {
            var action = ListeningAction;
            rebinding = false;
            ClearListening();
            if (!action.HasValue)
            {
                return;
            }

            if (!TrySetPath(action.Value, path, out _))
            {
                ApplyCurrent();
            }
        }

        private void OnRebindCancelled()
        {
            if (!rebinding)
            {
                return;
            }

            rebinding = false;
            ClearListening();
        }

        private void ClearListening()
        {
            ListeningAction = null;
            RebindListeningChanged?.Invoke(null);
        }

        private void PersistAndApply()
        {
            store.Save(Current);
            ApplyCurrent();
            Changed?.Invoke(Current);
        }

        private void ApplyCurrent()
        {
            applier.Apply(Current);
        }
    }
}

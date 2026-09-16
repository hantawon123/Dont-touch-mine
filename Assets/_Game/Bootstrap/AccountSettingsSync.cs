using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using Game.Core.Ports;
using Game.Backend;
using Game.Core.Settings;
using UnityEngine;
using VContainer.Unity;

namespace Game.Bootstrap
{
    /// <summary>One writer per client, revision-checked remotely. Offline edits survive restart per account.</summary>
    public sealed class AccountSettingsSync : IStartable, IDisposable
    {
        private readonly IAccountReady signIn;
        private readonly IAccountSettingsGateway gateway;
        private readonly GeneralSettingsSystem general;
        private readonly ControlSettingsSystem control;
        private readonly InterfaceSettingsSystem ui;
        private readonly SoundSettingsSystem sound;
        private readonly NotificationSettingsSystem notification;
        private readonly CancellationTokenSource lifetime = new();
        private readonly int[] versions = new int[5];
        private int dirty;
        private bool applying;
        private string account;
        private long revision;
        private readonly string ownerKey;
        [Serializable] private sealed class Pending { public int mask; public AccountSettingsSnapshot settings; }
        private string CacheKey => "account-settings.pending." + account;

        public AccountSettingsSync(IAccountReady signIn, IAccountSettingsGateway gateway,
            GeneralSettingsSystem general, ControlSettingsSystem control, InterfaceSettingsSystem ui,
            SoundSettingsSystem sound, NotificationSettingsSystem notification, string ownerKey = null)
        {
            this.ownerKey = ownerKey ?? "account-settings.owner." + DeviceIdentity.Current();
            this.signIn = signIn; this.gateway = gateway; this.general = general; this.control = control;
            this.ui = ui; this.sound = sound; this.notification = notification;
        }
        public void Start()
        {
            general.Changed += General; control.Changed += Control; ui.Changed += Interface;
            sound.Changed += Sound; notification.Changed += Notification;
            Run(lifetime.Token).Forget(e => { if (!(e is OperationCanceledException)) Debug.LogException(e); });
        }
        public void Dispose()
        {
            general.Changed -= General; control.Changed -= Control; ui.Changed -= Interface;
            sound.Changed -= Sound; notification.Changed -= Notification;
            Persist(); lifetime.Cancel(); lifetime.Dispose();
        }
        private void General(GeneralSettings _) => Changed(0);
        private void Control(ControlSettings _) => Changed(1);
        private void Interface(InterfaceSettings _) => Changed(2);
        private void Sound(SoundSettings _) => Changed(3);
        private void Notification(NotificationSettings _) => Changed(4);
        private void Changed(int section)
        {
            if (applying) return;
            if (!string.IsNullOrEmpty(account) && !string.IsNullOrEmpty(gateway.UserId) && gateway.UserId != account) return;
            versions[section]++; dirty |= 1 << section; Persist();
        }
        private AccountSettingsSnapshot Capture() => AccountSettingsSnapshot.Capture(general, control, ui, sound, notification);
        private void Apply(AccountSettingsSnapshot snapshot, int preserve)
        {
            applying = true;
            try { snapshot?.Apply(general, control, ui, sound, notification, preserve); }
            finally { applying = false; }
        }
        private void Persist()
        {
            if (string.IsNullOrEmpty(account) || (!string.IsNullOrEmpty(gateway.UserId) && gateway.UserId != account)) return;
            if (dirty == 0) PlayerPrefs.DeleteKey(CacheKey);
            else PlayerPrefs.SetString(CacheKey, JsonUtility.ToJson(new Pending { mask = dirty, settings = Capture() }));
            PlayerPrefs.Save();
        }
        private async UniTask Run(CancellationToken cancellation)
        {
            bool signedIn = await signIn.Ready.AttachExternalCancellation(cancellation);
            account = signedIn ? gateway.UserId : PlayerPrefs.GetString(ownerKey, "");
            if (signedIn) PlayerPrefs.SetString(ownerKey, account);
            if (string.IsNullOrEmpty(account)) return;
            try
            {
                var pending = JsonUtility.FromJson<Pending>(PlayerPrefs.GetString(CacheKey, "{}"));
                if (pending?.settings?.IsValid == true)
                {
                    Apply(pending.settings, dirty | (~pending.mask & 31));
                    dirty |= pending.mask & 31;
                }
            }
            catch (ArgumentException) { /* Old/corrupt local cache does not stop login. */ }
            Persist();
            if (!signedIn) return;
            bool loaded = false, warned = false;
            while (!cancellation.IsCancellationRequested && gateway.UserId == account)
            {
                if (!loaded)
                {
                    var answer = await gateway.GetAsync(cancellation);
                    cancellation.ThrowIfCancellationRequested();
                    if (gateway.UserId != account) return;
                    if (answer.Ok && answer.Value != null &&
                        (answer.Value.settings == null || answer.Value.settings.IsValid))
                    {
                        revision = answer.Value.revision;
                        Apply(answer.Value.settings, dirty);
                        if (answer.Value.settings == null) dirty = 31; // First account save adopts existing local preferences.
                        loaded = true; warned = false; Persist();
                        continue;
                    }
                }
                else if (dirty != 0)
                {
                    // Coalesce the five Apply callbacks and never overlap PUT requests.
                    await UniTask.Delay(350, ignoreTimeScale: true, cancellationToken: cancellation);
                    if (gateway.UserId != account) return;
                    int sentMask = dirty;
                    var sentVersions = (int[])versions.Clone();
                    var answer = await gateway.SaveAsync(revision, Capture(), cancellation);
                    cancellation.ThrowIfCancellationRequested();
                    if (gateway.UserId != account) return;
                    if (answer.Ok && answer.Value != null && answer.Value.settings?.IsValid == true)
                    {
                        revision = answer.Value.revision;
                        if (answer.Value.accepted)
                        {
                            for (int i = 0; i < versions.Length; i++)
                                if (sentVersions[i] == versions[i]) dirty &= ~(sentMask & (1 << i));
                        }
                        else Apply(answer.Value.settings, dirty); // Preserve only this PC's unsaved sections on conflict.
                        warned = false; Persist();
                        continue;
                    }
                }
                else
                {
                    await UniTask.Delay(350, ignoreTimeScale: true, cancellationToken: cancellation);
                    continue;
                }
                if (!loaded || dirty != 0)
                {
                    if (!warned) Debug.LogWarning("[Settings] Account sync pending; local settings retained. Retrying in 10 seconds.");
                    warned = true;
                    await UniTask.Delay(10000, ignoreTimeScale: true, cancellationToken: cancellation);
                }
            }
        }
    }
}

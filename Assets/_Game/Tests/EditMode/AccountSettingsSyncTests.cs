using System;
using System.Collections;
using System.Threading;
using Cysharp.Threading.Tasks;
using Game.Bootstrap;
using Game.Core.Backend;
using Game.Core.Ports;
using Game.Core.Settings;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Game.Architecture.Tests
{
    public sealed class AccountSettingsSyncTests
    {
        private readonly string user = "settings-test-" + Guid.NewGuid();
        private readonly string owner = "settings-owner-test-" + Guid.NewGuid();
        private AccountSettingsSync sync;
        private GeneralSettingsSystem general;
        private ControlSettingsSystem control;
        private InterfaceSettingsSystem ui;
        private SoundSettingsSystem sound;
        private NotificationSettingsSystem notifications;
        private Gateway gateway;
        private sealed class FakeReady : IAccountReady { public bool Online = true; public UniTask<bool> Ready => UniTask.FromResult(Online); }
        private sealed class Gateway : IAccountSettingsGateway
        {
            public string UserId { get; set; }
            public Func<UniTask<BackendResult<AccountSettingsReply>>> Read;
            public Func<long, AccountSettingsSnapshot, UniTask<BackendResult<AccountSettingsReply>>> Write;
            public UniTask<BackendResult<AccountSettingsReply>> GetAsync(CancellationToken c) => Read();
            public UniTask<BackendResult<AccountSettingsReply>> SaveAsync(long r, AccountSettingsSnapshot s, CancellationToken c) => Write(r,s);
        }
        [SetUp] public void Setup()
        {
            general = new(new InMemoryGeneralSettingsStore()); control = new(new InMemoryControlSettingsStore());
            ui = new(new InMemoryInterfaceSettingsStore()); sound = new(new InMemorySoundSettingsStore());
            notifications = new(new InMemoryNotificationSettingsStore());
            gateway = new Gateway { UserId = user };
            sync = new(new FakeReady(), gateway, general, control, ui, sound, notifications, owner);
        }
        [TearDown] public void Cleanup()
        {
            sync?.Dispose(); PlayerPrefs.DeleteKey(owner); PlayerPrefs.DeleteKey("account-settings.pending."+user);
        }
        private AccountSettingsSnapshot Capture() => AccountSettingsSnapshot.Capture(general,control,ui,sound,notifications);
        private static BackendResult<AccountSettingsReply> Reply(AccountSettingsSnapshot s, long r=1, bool accepted=true) =>
            BackendResult<AccountSettingsReply>.Success(new AccountSettingsReply {settings=s, revision=r, accepted=accepted});
        private static IEnumerator Until(Func<bool> ready)
        {
            var deadline=DateTime.UtcNow.AddSeconds(5);
            while (!ready() && DateTime.UtcNow<deadline) yield return null;
            Assert.That(ready(),Is.True,"Timed out waiting for settings sync");
        }

        [UnityTest] public IEnumerator OfflineEditsSurviveRestartAndWinOverOlderRemoteValues()
        {
            sync.Dispose(); PlayerPrefs.SetString(owner,user);
            sync=new(new FakeReady {Online=false},gateway,general,control,ui,sound,notifications,owner);
            sync.Start(); sound.Apply(sound.Current.With(SoundVolume.Master,41)); sync.Dispose();
            sound.Apply(sound.Defaults);
            gateway.Read=()=>UniTask.FromResult(Reply(Capture())); int saves=0;
            gateway.Write=(r,s)=> { saves++; Assert.That(s.volumes[0],Is.EqualTo(41)); return UniTask.FromResult(Reply(s,r+1)); };
            sync=new(new FakeReady(),gateway,general,control,ui,sound,notifications,owner);
            sync.Start(); yield return Until(()=>saves==1);
        }
        [UnityTest] public IEnumerator DisposedSessionIgnoresLateResponse()
        {
            var read=new UniTaskCompletionSource<BackendResult<AccountSettingsReply>>();
            gateway.Read=()=>read.Task; var remote=Capture(); remote.volumes[0]=11;
            sync.Start(); sync.Dispose(); sync=null;
            read.TrySetResult(Reply(remote)); yield return null;
            Assert.That(sound.Current.Get(SoundVolume.Master),Is.EqualTo(50));
        }
        [UnityTest] public IEnumerator LateReadPreservesAppliedLocalSoundAndSavesIt()
        {
            var read=new UniTaskCompletionSource<BackendResult<AccountSettingsReply>>();
            var remote=Capture(); remote.volumes[0]=12;
            gateway.Read=()=>read.Task; int saves=0;
            gateway.Write=(r,s)=> { saves++; Assert.That(s.volumes[0],Is.EqualTo(73)); return UniTask.FromResult(Reply(s,r+1)); };
            sync.Start(); sound.Apply(sound.Current.With(SoundVolume.Master,73));
            read.TrySetResult(Reply(remote));
            yield return Until(()=>saves==1);
            Assert.That(sound.Current.Get(SoundVolume.Master),Is.EqualTo(73));
        }
        [UnityTest] public IEnumerator EditDuringSaveIsSentAfterFirstAck_NotLostOrOverlapped()
        {
            gateway.Read=()=>UniTask.FromResult(Reply(Capture()));
            var first=new UniTaskCompletionSource<BackendResult<AccountSettingsReply>>();
            AccountSettingsSnapshot firstValue=null; int saves=0;
            gateway.Write=(r,s)=> {
                saves++;
                if(saves==1) {firstValue=s; return first.Task;}
                Assert.That(r,Is.EqualTo(2)); Assert.That(s.volumes[0],Is.EqualTo(81));
                return UniTask.FromResult(Reply(s,3));
            };
            sync.Start(); sound.Apply(sound.Current.With(SoundVolume.Master,24));
            yield return Until(()=>saves==1);
            sound.Apply(sound.Current.With(SoundVolume.Master,81));
            Assert.That(saves,Is.EqualTo(1)); first.TrySetResult(Reply(firstValue,2));
            yield return Until(()=>saves==2);
        }
        [UnityTest] public IEnumerator ConflictKeepsOtherPcSectionsAndRetriesOnlyOurEdits()
        {
            gateway.Read=()=>UniTask.FromResult(Reply(Capture())); int saves=0;
            gateway.Write=(r,s)=> {
                saves++;
                if(saves==1) { var other=Capture(); other.bindings[0]="V"; return UniTask.FromResult(Reply(other,4,false)); }
                Assert.That(r,Is.EqualTo(4)); Assert.That(s.volumes[0],Is.EqualTo(19));
                Assert.That(s.bindings[0],Is.EqualTo("V"));
                return UniTask.FromResult(Reply(s,5));
            };
            sync.Start(); sound.Apply(sound.Current.With(SoundVolume.Master,19));
            yield return Until(()=>saves==2);
        }
    }
}

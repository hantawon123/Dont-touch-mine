using System.Reflection;
using Game.Network.Players;
using Game.Network.Voice;
using NUnit.Framework;
using Photon.Voice.Unity;
using UnityEngine;

namespace Game.Tests.EditMode
{
    public sealed class VoiceRigListenStateTests
    {
        private GameObject rigRoot, speakerRoot;
        private VoiceRig rig;
        private AudioSource source;

        [SetUp]
        public void SetUp()
        {
            rigRoot = new GameObject("Listen state rig");
            rig = rigRoot.AddComponent<VoiceRig>();
            AddSpeaker();
        }

        private void AddSpeaker()
        {
            speakerRoot = new GameObject("Listen state speaker");
            source = speakerRoot.AddComponent<AudioSource>();
            speakerRoot.AddComponent<Speaker>();
        }

        private void Refresh() => typeof(VoiceRig)
            .GetMethod("ApplyListenState", BindingFlags.Instance | BindingFlags.NonPublic)
            .Invoke(rig, null);

        [TestCase(true)]
        [TestCase(false)]
        public void ListeningToggleAndRepeatedRefresh_PreservePlaybackState(bool listening)
        {
            rig.SetListening(!listening);
            rig.SetListening(listening);
            Assert.That(source.mute, Is.EqualTo(!listening));
            for (var i = 0; i < 3; i++) Refresh();
            Assert.That(source.mute, Is.EqualTo(!listening));
            Assert.That(rig.IsListening.CurrentValue, Is.EqualTo(listening));
        }

        [Test]
        public void ReplacementSpeaker_ReceivesExistingMutedState()
        {
            rig.SetListening(false);
            Object.DestroyImmediate(speakerRoot);
            AddSpeaker();
            Assert.That(source.mute, Is.False);
            Refresh();
            Assert.That(source.mute, Is.True);
            rig.SetListening(true);
            Assert.That(source.mute, Is.False);
        }

        [Test]
        public void ExternalStateChange_IsCorrectedOnNextRefresh()
        {
            rig.SetListening(false);
            source.mute = false;
            Refresh();
            Assert.That(source.mute, Is.True);
        }

        [Test]
        public void RosterReplacementAndReactivation_UseCurrentListeningState()
        {
            var roster = rigRoot.AddComponent<PlayerRoster>();
            var avatar = speakerRoot.AddComponent<PlayerAvatar>();
            roster.Add(avatar);
            rig.SetListening(false);
            Assert.That(source.mute, Is.True);
            roster.Remove(avatar, null);
            Object.DestroyImmediate(speakerRoot);
            AddSpeaker();
            roster.Add(speakerRoot.AddComponent<PlayerAvatar>());
            Refresh();
            Assert.That(source.mute, Is.True);
            speakerRoot.SetActive(false);
            rig.SetListening(true);
            Assert.That(source.mute, Is.True, "Inactive objects are not included in the original search.");
            speakerRoot.SetActive(true);
            Refresh();
            Assert.That(source.mute, Is.False);
        }

        [Test]
        public void RegisteredSpeakers_KeepPlaybackStateAcrossRepeatedRefreshes()
        {
            var roster = rigRoot.AddComponent<PlayerRoster>();
            roster.Add(speakerRoot.AddComponent<PlayerAvatar>());
            var refresh = (System.Action)typeof(VoiceRig)
                .GetMethod("ApplyListenState", BindingFlags.Instance | BindingFlags.NonPublic)
                .CreateDelegate(typeof(System.Action), rig);
            rig.SetListening(false);
            for (var i = 0; i < 100; i++) refresh();
            Assert.That(source.mute, Is.True);
            source.mute = false;
            refresh();
            Assert.That(source.mute, Is.True);
        }

        [Test]
        public void LinkedSpeaker_ReconnectionReactivationAndExternalMuteRemainCorrect()
        {
            var roster = rigRoot.AddComponent<PlayerRoster>();
            roster.Add(speakerRoot.AddComponent<PlayerAvatar>());
            var client = rigRoot.AddComponent<Photon.Voice.Fusion.FusionVoiceClient>();
            client.enabled = false; client.AutoConnectAndJoin = false;
            var flags = BindingFlags.Instance | BindingFlags.NonPublic;
            typeof(VoiceRig).GetField("client", flags).SetValue(rig, client);
            var linked = typeof(VoiceRig).GetMethod("OnSpeakerLinked", flags);
            void Link() { linked?.Invoke(rig, new object[] { speakerRoot.GetComponent<Speaker>() }); Refresh(); }
            rig.SetListening(false); Link(); Assert.That(source.mute, Is.True);
            source.mute=false; Refresh(); Assert.That(source.mute,Is.True);
            speakerRoot.SetActive(false);rig.SetListening(true);Assert.That(source.mute,Is.True);
            speakerRoot.SetActive(true);Refresh();Assert.That(source.mute,Is.False);
            rig.SetListening(false);Link();Link();Assert.That(source.mute,Is.True);
            var refresh=(System.Action)typeof(VoiceRig).GetMethod("ApplyListenState",flags).CreateDelegate(typeof(System.Action),rig);
            for(var i=0;i<20;i++) refresh();
            var watch=System.Diagnostics.Stopwatch.StartNew(); var bytes=System.GC.GetAllocatedBytesForCurrentThread();
            for(var i=0;i<2000;i++) refresh();
            var allocated=System.GC.GetAllocatedBytesForCurrentThread()-bytes;watch.Stop();
            TestContext.WriteLine($"VOICE_LISTEN calls=2000 ms={watch.Elapsed.TotalMilliseconds} bytes={allocated}");
            roster.Remove(speakerRoot.GetComponent<PlayerAvatar>(),null); Object.DestroyImmediate(speakerRoot);
            AddSpeaker();roster.Add(speakerRoot.AddComponent<PlayerAvatar>());Link();Assert.That(source.mute,Is.True);
            rig.SetListening(true);Assert.That(source.mute,Is.False);
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(speakerRoot);
            Object.DestroyImmediate(rigRoot);
        }
    }
}

using System;
using Game.Network.Voice;
using NUnit.Framework;
using Photon.Voice.Fusion;
using UnityEngine;

// Fusion is not imported wholesale: it carries its own Assert, which would make
// every NUnit assertion in this file an ambiguous reference.

namespace Game.Tests.EditMode
{
    /// <summary>
    /// Voice connects without Fusion's credentials (S15P21D205-934).
    /// </summary>
    /// <remarks>
    /// Fusion and Voice are separate Photon applications and only Fusion has a
    /// custom authentication provider registered. The SDK's default is to copy
    /// the runner's credentials onto the voice client, which offers custom
    /// authentication to an application that has none; Photon answers 32755 and
    /// the voice client shuts itself off for the rest of the session.
    /// <para>
    /// Nothing surfaces when that happens - room entry carries on and the
    /// players simply cannot hear each other - so it went unnoticed from
    /// S15P21D205-925 until somebody said voice was silent. That is the reason
    /// for a test over a one-line assignment: the failure it guards is quiet.
    /// </para>
    /// </remarks>
    public sealed class VoiceAuthIsNotInheritedTests
    {
        [Test]
        public void TheVoiceClient_DoesNotCarryFusionCredentials()
        {
            using var runner = new BuiltRunner();

            Assert.That(
                runner.Voice.UseFusionAuthValues,
                Is.False,
                "Voice 가 Fusion 의 인증값을 들고 가면 등록되지 않은 Voice 앱에 "
                + "커스텀 인증을 내밀게 되고, 32755 로 거절당해 보이스가 죽습니다.");
        }

        [Test]
        public void TheSdkWouldOtherwiseInherit()
        {
            // 위 테스트가 무엇을 막고 있는지 고정합니다. SDK 기본값이 언젠가 false 로
            // 바뀌면 위 단언은 우리 코드를 지우고도 통과하게 되고, 이 테스트가 먼저
            // 깨져서 그 사실을 알려줍니다.
            var root = new GameObject("UntouchedVoiceClient");
            try
            {
                var untouched = root.AddComponent<FusionVoiceClient>();

                Assert.That(
                    untouched.UseFusionAuthValues,
                    Is.True,
                    "SDK 기본값이 바뀌었습니다. VoiceRig 의 명시적 설정이 아직 "
                    + "필요한지 다시 보세요.");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        /// <summary>A runner with the voice rig attached, the way a session builds it.</summary>
        private sealed class BuiltRunner : IDisposable
        {
            private readonly GameObject root;

            public BuiltRunner()
            {
                root = new GameObject("RunnerUnderTest");
                var runner = root.AddComponent<Fusion.NetworkRunner>();

                VoiceRig.Attach(runner);

                Voice = root.GetComponent<FusionVoiceClient>();
                Assert.That(Voice, Is.Not.Null, "VoiceRig.Attach did not add a voice client.");
            }

            public FusionVoiceClient Voice { get; }

            public void Dispose()
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }
    }
}

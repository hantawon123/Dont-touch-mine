using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using Game.Backend;
using Game.Core.Ports;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Game.Architecture.Tests
{
    /// <summary>
    /// Chat filtering on the dedicated server (S15P21D205-1028).
    /// </summary>
    /// <remarks>
    /// The cases here are deliberately the same ones the backend's ChatBlocklistTest fixes
    /// (S15P21D205-1027). The two judge every message independently — the server to decide what
    /// to broadcast, the backend to decide what to flag when it stores the original — so if they
    /// drift, an investigator reads a message marked as masked that the players saw in the clear.
    /// When one side's rules change, both test files change together.
    /// </remarks>
    public sealed class ChatModerationTests
    {
        private static ChatBlocklist Loaded()
        {
            var list = new ChatBlocklist();
            list.Load(new[] { "시발", "병신", "shit", "fuck" }, new[] { "시발점", "shiitake" });
            return list;
        }

        [Test]
        public void PlainMessage_Passes()
        {
            var list = Loaded();
            Assert.That(list.IsForbidden("어디 숨었어"), Is.False);
            Assert.That(list.Mask("어디 숨었어"), Is.EqualTo("어디 숨었어"));
        }

        [Test]
        public void PlainProfanity_IsCovered()
        {
            var list = Loaded();
            Assert.That(list.IsForbidden("야 이 시발아"), Is.True);
            Assert.That(list.Mask("야 이 시발아"), Is.EqualTo("야 이 **아"));
        }

        [Test]
        public void SymbolsInserted_StillHit()
        {
            var list = Loaded();
            Assert.That(list.IsForbidden("시 발"), Is.True);
            Assert.That(list.IsForbidden("시.발"), Is.True);
        }

        [Test]
        public void DigitsSwapped_StillHit()
        {
            var list = Loaded();
            Assert.That(list.IsForbidden("sh1t"), Is.True);
        }

        [Test]
        public void StackedEvasion_StillHits()
        {
            // Undoing only the digits leaves "시 발"; undoing only the spacing leaves "시1발".
            // Neither contains the word, so the combinations have to be checked as well.
            var list = Loaded();
            Assert.That(list.IsForbidden("시1 발"), Is.True);
            Assert.That(list.IsForbidden("s.h.1.t"), Is.True);
        }

        [Test]
        public void EvadedMessage_IsCoveredWhole()
        {
            // Nothing in the text reads as the word, so there is no part to cover. Covering some
            // of it would leave the rest readable.
            var list = Loaded();
            Assert.That(list.Mask("시 발"), Is.EqualTo("***"));
        }

        [Test]
        public void LatinWords_NeedToStandAlone()
        {
            var list = Loaded();
            Assert.That(list.IsForbidden("shitake mushroom"), Is.False);
            Assert.That(list.IsForbidden("bullshitting"), Is.False);
            Assert.That(list.IsForbidden("that is shit"), Is.True);
        }

        [Test]
        public void AllowedWords_Pass()
        {
            Assert.That(Loaded().IsForbidden("시발점이 어디야"), Is.False);
        }

        [Test]
        public void LengthChangingLowercase_DoesNotThrow()
        {
            // U+0130 becomes two characters under ToLowerInvariant. A position found there lands
            // on the wrong character in the original, or past its end. People type it.
            var list = Loaded();
            Assert.That(list.Mask("İ 시발"), Is.EqualTo("İ **"));
            Assert.That(list.Mask("İİİ"), Is.EqualTo("İİİ"));
        }

        [Test]
        public void BeforeTheListArrives_NothingIsCovered()
        {
            // A server that could not reach the backend opens the room anyway.
            var list = new ChatBlocklist();
            Assert.That(list.IsLoaded, Is.False);
            Assert.That(list.IsForbidden("시발"), Is.False);
            Assert.That(list.Mask("시발"), Is.EqualTo("시발"));
        }

        [UnityTest]
        public IEnumerator Service_LoadsTheList_ThenPostsRecordsInOneBatch()
        {
            var transport = new FakeTransport
            {
                Body = "{\"blocked\":[\"시발\"],\"allowed\":[\"시발점\"]}"
            };
            using var service = new ChatModerationService(
                transport, new BackendEndpoint("http://127.0.0.1:8080"), "key");

            service.LoadAsync(CancellationToken.None).GetAwaiter().GetResult();

            Assert.That(service.IsFiltering, Is.True);
            Assert.That(transport.Calls[0].Url, Does.EndWith("/internal/chat/blocklist"));
            Assert.That(transport.Calls[0].Headers[0].Name, Is.EqualTo("X-Internal-Key"));

            // What goes out to the players is covered; what goes to the backend is not.
            Assert.That(service.Mask("야 시발"), Is.EqualTo("야 **"));
            for (var index = 0; index < 3; index++)
            {
                service.Record(new ChatLogRecord("7K2M9P", ChatScope.Match, null, "p1",
                    "야 시발", DateTimeOffset.UtcNow));
            }

            var deadline = DateTime.UtcNow.AddSeconds(15);
            while (transport.Calls.Count < 2 && DateTime.UtcNow < deadline) yield return null;

            Assert.That(transport.Calls.Count, Is.EqualTo(2), "세 줄이 한 묶음으로 나가야 합니다.");
            var sent = transport.Calls[1];
            Assert.That(sent.Url, Does.EndWith("/internal/chat"));
            Assert.That(sent.JsonBody, Does.Contain("\"message\":\"야 시발\""), "원문이 나가야 합니다.");
            Assert.That(sent.JsonBody, Does.Contain("\"scope\":\"MATCH\""));
        }

        [UnityTest]
        public IEnumerator Service_WithoutAKey_StaysQuiet()
        {
            // No key means the path answers 404 for everything. Chat still works.
            var transport = new FakeTransport();
            using var service = new ChatModerationService(
                transport, new BackendEndpoint("http://127.0.0.1:8080"), null);

            LogAssert.Expect(LogType.Warning, "[Chat] No internal key was given; chat runs unfiltered and unrecorded.");
            service.LoadAsync(CancellationToken.None).GetAwaiter().GetResult();
            service.Record(new ChatLogRecord("7K2M9P", ChatScope.Lobby, null, "p1", "안녕", DateTimeOffset.UtcNow));

            yield return null;

            Assert.That(transport.Calls, Is.Empty);
            Assert.That(service.Mask("시발"), Is.EqualTo("시발"));
        }

        private sealed class FakeTransport : IHttpTransport
        {
            public readonly List<HttpCall> Calls = new();
            public string Body = "{}";
            public long Status = 200;

            public UniTask<HttpCallResult> SendAsync(HttpCall call, CancellationToken cancellation)
            {
                Calls.Add(call);
                return UniTask.FromResult(HttpCallResult.Completed(
                    Calls.Count == 1 ? Status : 202, Calls.Count == 1 ? Body : ""));
            }
        }
    }
}

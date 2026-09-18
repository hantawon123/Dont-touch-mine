using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
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

        /// <summary>
        /// The automaton and the plain per-word scan have to answer identically.
        /// </summary>
        /// <remarks>
        /// This is the test that matters for the automaton. Speed is not the risk — a rule that
        /// drifts is. The backend judges the same message again when it stores it, so a
        /// disagreement shows up as a line recorded as masked that the players read in the
        /// clear, or the other way round.
        /// </remarks>
        private static void AssertSameJudgement(ChatBlocklist list, string message)
        {
            var byAutomaton = list.IsForbidden(message);
            var byScan = false;
            if (!string.IsNullOrEmpty(message))
                foreach (var variant in ChatBlocklist.Variants(message))
                    if (list.HitsByScan(variant)) { byScan = true; break; }

            Assert.That(byAutomaton, Is.EqualTo(byScan),
                $"판정이 갈립니다: [{message}] 자동자={byAutomaton} 훑기={byScan}");
        }

        private static ChatBlocklist Wide()
        {
            var list = new ChatBlocklist();
            list.Load(
                new[]
                {
                    "시발", "씨발", "병신", "새끼", "지랄", "개새끼", "ㅅㅂ", "좆", "꺼져", "닥쳐",
                    "shit", "fuck", "fucking", "bitch", "anal", "ass", "b17ch", "@sshole",
                    "carpet muncher", "aa", "abab", "dick"
                },
                new[] { "시발점", "shiitake", "강아지새끼" });
            return list;
        }

        [Test]
        public void Automaton_AgreesWithScan_OnSamples()
        {
            var list = Wide();
            var samples = new[]
            {
                "", " ", "어디 숨었어", "야 이 시발아", "시 발", "시.발", "시1발", "sh1t", "s.h.1.t",
                "this game sucks", "analyst 모드", "shiitake 버섯", "강아지새끼 귀여워",
                "시발점이 어디야", "b17ch", "@sshole", "carpet muncher", "xxfuckxx", "fuck",
                "FUCK YOU", "aaa", "ababab", "ㅅㅂ 렉", "개새끼야", "dickhead", "dick head",
                "1234567890", "....", "좆같네", "닥쳐라"
            };
            foreach (var sample in samples) AssertSameJudgement(list, sample);
        }

        [Test]
        public void Automaton_AgreesWithScan_OnRandomMixtures()
        {
            var list = Wide();
            var pieces = new[]
            {
                "시발", "씨발", "병신", "새끼", "shit", "fuck", "anal", "ass", "aa", "abab",
                "가나다", " ", ".", "1", "0", "analyst", "시발점", "강아지", "ㅋㅋ", "abc"
            };
            // 씨앗을 고정합니다. 실패하면 같은 입력으로 다시 돌려 볼 수 있어야 합니다.
            var random = new System.Random(1048);
            for (var round = 0; round < 3000; round++)
            {
                var text = new System.Text.StringBuilder();
                var parts = 1 + random.Next(5);
                for (var part = 0; part < parts; part++)
                {
                    var piece = pieces[random.Next(pieces.Length)];
                    // 통째로 넣기도 하고 잘라 넣기도 합니다. 잘린 조각은 걸리면 안 됩니다.
                    text.Append(random.Next(3) == 0 && piece.Length > 1
                        ? piece.Substring(0, piece.Length - 1)
                        : piece);
                }
                AssertSameJudgement(list, text.ToString());
            }
        }

        /// <summary>
        /// Covering may grow but must never shrink.
        /// </summary>
        /// <remarks>
        /// The automaton finds overlapping hits that the old scan skipped past, so the two are
        /// not identical and cannot be asserted equal. What has to hold is that every character
        /// the scan covered is still covered, and that nothing else was touched — a covering that
        /// loses a character leaves part of the word readable, which is the thing covering exists
        /// to prevent.
        /// </remarks>
        private static void AssertCoveringNeverShrinks(ChatBlocklist list, string message)
        {
            var byAutomaton = list.Mask(message);
            var byScan = list.MaskByScan(message);

            Assert.That(byAutomaton.Length, Is.EqualTo(message.Length), $"길이가 바뀝니다: [{message}]");
            Assert.That(byScan.Length, Is.EqualTo(message.Length));
            for (var index = 0; index < message.Length; index++)
            {
                if (byScan[index] == '*')
                {
                    Assert.That(byAutomaton[index], Is.EqualTo('*'),
                        $"덜 가립니다: [{message}] 자리 {index} 자동자=[{byAutomaton}] 훑기=[{byScan}]");
                }
                else if (byAutomaton[index] != '*')
                {
                    Assert.That(byAutomaton[index], Is.EqualTo(message[index]),
                        $"가리지도 않고 글자가 바뀝니다: [{message}] 자리 {index}");
                }
            }
        }

        [Test]
        public void Mask_CoversOverlappingRepeats_ThatTheScanSkipped()
        {
            // "aa" 가 "aaa" 안에서 두 번 겹칩니다. 옛 코드는 한 번 가린 뒤 그 길이만큼 건너뛰어
            // 마지막 글자를 남겼습니다. 자기와 겹칠 수 있는 말이 실제 목록에도 26개 있습니다.
            var list = Wide();

            // 가리기는 이미 걸린 메시지에만 돕니다. "aaa" 만으로는 낱말 경계 때문에 걸리지 않으므로
            // 다른 말로 걸리게 한 뒤 가려지는 자리를 봅니다.
            Assert.That(list.Mask("aaa 시발"), Is.EqualTo("*** **"));
            Assert.That(list.MaskByScan("aaa 시발"), Is.EqualTo("**a **"));
        }

        [Test]
        public void Mask_NeverCoversLessThanTheScan_OnRandomMixtures()
        {
            var list = Wide();
            var pieces = new[]
            {
                "시발", "씨발", "병신", "새끼", "shit", "fuck", "anal", "ass", "aa", "abab",
                "가나다", " ", ".", "1", "0", "analyst", "시발점", "강아지", "ㅋㅋ", "abc"
            };
            // 씨앗을 고정합니다. 실패하면 같은 입력으로 다시 돌려 볼 수 있어야 합니다.
            var random = new System.Random(1078);
            for (var round = 0; round < 3000; round++)
            {
                var text = new System.Text.StringBuilder();
                var parts = 1 + random.Next(5);
                for (var part = 0; part < parts; part++)
                {
                    var piece = pieces[random.Next(pieces.Length)];
                    text.Append(random.Next(3) == 0 && piece.Length > 1
                        ? piece.Substring(0, piece.Length - 1)
                        : piece);
                }
                AssertCoveringNeverShrinks(list, text.ToString());
            }
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

        [UnityTest]
        public IEnumerator Service_StampsTimeInTheGregorianCalendar_WhateverTheLocale()
        {
            // A calendar-shifting culture would write 2569 for 2026. Fourteen digits either way,
            // so the backend accepts it and the record lands 543 years out: the retention sweep
            // never reaches it and the report lookup never finds it.
            var before = CultureInfo.CurrentCulture;
            CultureInfo.CurrentCulture = new CultureInfo("th-TH");
            try
            {
                var transport = new FakeTransport { Body = "{\"blocked\":[],\"allowed\":[]}" };
                using var service = new ChatModerationService(
                    transport, new BackendEndpoint("http://127.0.0.1:8080"), "key");
                service.LoadAsync(CancellationToken.None).GetAwaiter().GetResult();

                service.Record(new ChatLogRecord("7K2M9P", ChatScope.Match, null, "p1", "안녕",
                    new DateTimeOffset(2026, 9, 17, 1, 2, 3, TimeSpan.Zero)));

                var deadline = DateTime.UtcNow.AddSeconds(15);
                while (transport.Calls.Count < 2 && DateTime.UtcNow < deadline) yield return null;

                Assert.That(transport.Calls.Count, Is.EqualTo(2));
                Assert.That(transport.Calls[1].JsonBody, Does.Contain("\"sentAt\":\"20260917010203\""));
            }
            finally { CultureInfo.CurrentCulture = before; }
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

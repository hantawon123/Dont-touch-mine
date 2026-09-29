using System.Text;
using Game.Network.Session;
using NUnit.Framework;

namespace Game.Tests.EditMode
{
    /// <summary>
    /// The connection token's second layout: the backend account rides behind
    /// the password and the name, and the first layout is still readable.
    /// </summary>
    /// <remarks>
    /// Reading the old layout is not nostalgia. During a rollout a host on this
    /// build admits joiners still on the previous one, and the only acceptable
    /// loss is the account — which reads as empty — never the password check or
    /// the name.
    /// </remarks>
    public sealed class SessionTokenUserIdTests
    {
        private const string Account = "0f1e2d3c-4b5a-6978-8796-a5b4c3d2e1f0";

        [Test]
        public void AllThree_SurviveTheTrip()
        {
            var token = SessionConnectionTokenCodec.Encode("1111", "심재훈", Account);
            SessionConnectionTokenCodec.Decode(token, out var password, out var nickname, out var userId);

            Assert.That(password, Is.EqualTo("1111"));
            Assert.That(nickname, Is.EqualTo("심재훈"));
            Assert.That(userId, Is.EqualTo(Account));
        }

        /// <summary>
        /// The reason the nickname became length-prefixed in this layout: it
        /// used to be "everything after the password", and now something follows
        /// it. A name that looks like an account id must not be split wrongly.
        /// </summary>
        [TestCase("이름만")]
        [TestCase("0f1e2d3c-4b5a-6978-8796-a5b4c3d2e1f0")]
        [TestCase("")]
        public void NicknameAndAccount_AreNotConfused(string nickname)
        {
            var token = SessionConnectionTokenCodec.Encode("pw", nickname, Account);
            SessionConnectionTokenCodec.Decode(token, out _, out var readNickname, out var readUserId);

            Assert.That(readNickname, Is.EqualTo(nickname));
            Assert.That(readUserId, Is.EqualTo(Account));
        }

        [Test]
        public void NoAccount_ReadsEmpty()
        {
            var token = SessionConnectionTokenCodec.Encode("pw", "이름", null);
            SessionConnectionTokenCodec.Decode(token, out _, out var nickname, out var userId);

            // A player who never signed in. The host later sends this as null.
            Assert.That(nickname, Is.EqualTo("이름"));
            Assert.That(userId, Is.EqualTo(string.Empty));
        }

        [Test]
        public void OnlyAnAccount_IsStillWorthAToken()
        {
            var token = SessionConnectionTokenCodec.Encode(null, null, Account);

            Assert.That(token, Is.Not.Null);
            SessionConnectionTokenCodec.Decode(token, out var password, out var nickname, out var userId);
            Assert.That(password, Is.EqualTo(string.Empty));
            Assert.That(nickname, Is.EqualTo(string.Empty));
            Assert.That(userId, Is.EqualTo(Account));
        }

        [Test]
        public void TheOldLayout_StillReadsPasswordAndNickname()
        {
            // A token as the previous build wrote it: version 1, then the
            // password's length, the password, and the name as the remainder.
            var password = Encoding.UTF8.GetBytes("1111");
            var nickname = Encoding.UTF8.GetBytes("옛클라");
            var legacy = new byte[3 + password.Length + nickname.Length];
            legacy[0] = 1;
            legacy[1] = (byte)(password.Length >> 8);
            legacy[2] = (byte)(password.Length & 0xFF);
            password.CopyTo(legacy, 3);
            nickname.CopyTo(legacy, 3 + password.Length);

            SessionConnectionTokenCodec.Decode(legacy, out var readPassword, out var readNickname, out var userId);

            Assert.That(readPassword, Is.EqualTo("1111"));
            Assert.That(readNickname, Is.EqualTo("옛클라"));
            Assert.That(userId, Is.EqualTo(string.Empty));
        }

        [Test]
        public void TheTwoFieldReader_StillWorksOnTheNewLayout()
        {
            // Callers that only check the password keep using the old overload.
            var token = SessionConnectionTokenCodec.Encode("1111", "이름", Account);
            SessionConnectionTokenCodec.Decode(token, out var password, out var nickname);

            Assert.That(password, Is.EqualTo("1111"));
            Assert.That(nickname, Is.EqualTo("이름"));
        }

        [Test]
        public void AnUnknownVersion_ReadsAsEmptyRatherThanThrowing()
        {
            var token = SessionConnectionTokenCodec.Encode("1111", "이름", Account);
            token[0] = 9;

            Assert.DoesNotThrow(() =>
                SessionConnectionTokenCodec.Decode(token, out _, out _, out _));
            SessionConnectionTokenCodec.Decode(token, out var password, out var nickname, out var userId);
            Assert.That(password, Is.EqualTo(string.Empty));
            Assert.That(nickname, Is.EqualTo(string.Empty));
            Assert.That(userId, Is.EqualTo(string.Empty));
        }

        [Test]
        public void ATruncatedToken_ReadsAsEmptyRatherThanThrowing()
        {
            var token = SessionConnectionTokenCodec.Encode("1111", "이름", Account);

            for (var length = 0; length < token.Length; length++)
            {
                var cut = new byte[length];
                System.Array.Copy(token, cut, length);
                Assert.DoesNotThrow(
                    () => SessionConnectionTokenCodec.Decode(cut, out _, out _, out _),
                    $"cut at {length}");
            }
        }
    }
}

using Colosseum.Network;
using NUnit.Framework;

namespace Colosseum.Tests.EditMode
{
    public sealed class VersionValidatorTests
    {
        [TestCase("0.1.0", "0.1.0", true)]
        [TestCase("0.1.0", "0.1.5", true)]
        [TestCase("1.2.3", "1.2.99", true)]
        [TestCase("0.1.0", "0.2.0", false)]
        [TestCase("1.0.0", "2.0.0", false)]
        [TestCase("0.1.0", "1.1.0", false)]
        public void IsCompatible_MajorMinorMatch(string server, string client, bool expected)
        {
            Assert.That(VersionValidator.IsCompatible(server, client), Is.EqualTo(expected));
        }

        [TestCase("0.1", "0.1.0", true)]
        [TestCase("1.0", "1.0.3", true)]
        [TestCase("0.1", "0.2", false)]
        public void IsCompatible_TwoPartVersion(string server, string client, bool expected)
        {
            Assert.That(VersionValidator.IsCompatible(server, client), Is.EqualTo(expected));
        }

        [TestCase(null, "0.1.0")]
        [TestCase("0.1.0", null)]
        [TestCase("", "0.1.0")]
        [TestCase("0.1.0", "")]
        [TestCase(null, null)]
        public void IsCompatible_NullOrEmpty_ReturnsFalse(string server, string client)
        {
            Assert.That(VersionValidator.IsCompatible(server, client), Is.False);
        }

        [TestCase("abc", "0.1.0")]
        [TestCase("0.1.0", "xyz")]
        [TestCase("not-a-version", "also-not")]
        [TestCase("v0.1.0", "0.1.0")]
        [TestCase("1.beta", "1.0")]
        [TestCase("0.x", "0.0")]
        [TestCase("1.0-preview", "1.0")]
        public void IsCompatible_Garbage_ReturnsFalse(string server, string client)
        {
            Assert.That(VersionValidator.IsCompatible(server, client), Is.False);
        }

        [TestCase("0", "0", true)]
        [TestCase("0.1.0.0", "0.1.0", true)]
        [TestCase("0.1.0.0", "0.2.0", false)]
        public void IsCompatible_LenientParsing(string server, string client, bool expected)
        {
            Assert.That(VersionValidator.IsCompatible(server, client), Is.EqualTo(expected));
        }

        [Test]
        public void ParseVersionPayload_ValidMessage()
        {
            Assert.That(VersionValidator.ParseVersionPayload("VERSION:0.1.0"), Is.EqualTo("0.1.0"));
        }

        [Test]
        public void ParseVersionPayload_EmptyVersion()
        {
            Assert.That(VersionValidator.ParseVersionPayload("VERSION:"), Is.EqualTo(""));
        }

        [TestCase(null)]
        [TestCase("")]
        [TestCase("NICKNAME:test")]
        [TestCase("version:0.1.0")]
        public void ParseVersionPayload_InvalidMessage_ReturnsNull(string message)
        {
            Assert.That(VersionValidator.ParseVersionPayload(message), Is.Null);
        }

        [TestCase("0.1.0", "0")]
        [TestCase("1.2.3", "1")]
        [TestCase("12.0.0", "12")]
        [TestCase("0.1", "0")]
        public void MajorVersion_ExtractsMajor(string version, string expected)
        {
            Assert.That(VersionValidator.MajorVersion(version), Is.EqualTo(expected));
        }

        [TestCase(null, "")]
        [TestCase("", "")]
        [TestCase("abc", "abc")]
        public void MajorVersion_EdgeCases(string version, string expected)
        {
            Assert.That(VersionValidator.MajorVersion(version), Is.EqualTo(expected));
        }

        [Test]
        public void IsDevBuild_InEditor_ReturnsTrue()
        {
#if UNITY_EDITOR
            Assert.That(VersionValidator.IsDevBuild(), Is.True);
#else
            Assert.Ignore("Only runs in Editor");
#endif
        }
    }
}

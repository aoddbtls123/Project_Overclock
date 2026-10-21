using NUnit.Framework;
using Overclock.Core;

namespace Overclock.Tests
{
    public class RunSeedOptionTests
    {
        [Test]
        public void NoArgument_ReturnsFalseWithoutError()
        {
            Assert.IsFalse(RunSeedOption.TryFind(new[] { "Overclock.exe" }, out uint seed, out string error));
            Assert.AreEqual(0u, seed);
            Assert.IsTrue(error == null);

            Assert.IsFalse(RunSeedOption.TryFind(new string[0], out seed, out error));
            Assert.IsTrue(error == null);

            Assert.IsFalse(RunSeedOption.TryFind(null, out seed, out error));
            Assert.IsTrue(error == null);
        }

        [TestCase("1", 1u)]
        [TestCase("12345", 12345u)]
        [TestCase("5624218", 5624218u)]
        [TestCase("2147483647", 2147483647u)]
        public void ValidSeed_IsRead(string text, uint expected)
        {
            Assert.IsTrue(RunSeedOption.TryFind(new[] { "Overclock.exe", "-screen-fullscreen", "0", "-seed", text, "-logFile", "a.log" }, out uint seed, out string error), error);
            Assert.AreEqual(expected, seed);
            Assert.IsTrue(error == null);
        }

        // 0 은 Inspector 에서 '자동' 이라 QA 고정 시드로 쓰지 않는다. int 를 넘는 값은 로그 · Inspector 와 범위가 다르다
        [TestCase("0")]
        [TestCase("2147483648")]
        [TestCase("4294967295")]
        [TestCase("99999999999")]
        [TestCase("-5")]
        [TestCase("+5")]
        [TestCase(" 5")]
        [TestCase("5 ")]
        [TestCase("1.5")]
        [TestCase("1e3")]
        [TestCase("abc")]
        [TestCase("")]
        public void InvalidSeed_ReturnsFalseWithError(string text)
        {
            Assert.IsFalse(RunSeedOption.TryFind(new[] { "Overclock.exe", "-seed", text }, out uint seed, out string error));
            Assert.AreEqual(0u, seed);
            Assert.IsFalse(string.IsNullOrEmpty(error), "이유가 있어야 함: " + text);
        }

        [Test]
        public void MissingValue_ReturnsFalseWithError()
        {
            Assert.IsFalse(RunSeedOption.TryFind(new[] { "Overclock.exe", "-seed" }, out uint seed, out string error));
            Assert.AreEqual(0u, seed);
            Assert.IsFalse(string.IsNullOrEmpty(error));
        }

        [Test]
        public void FirstOccurrenceWins()
        {
            Assert.IsTrue(RunSeedOption.TryFind(new[] { "-seed", "7", "-seed", "9" }, out uint seed, out string error), error);
            Assert.AreEqual(7u, seed);

            Assert.IsFalse(RunSeedOption.TryFind(new[] { "-seed", "x", "-seed", "9" }, out seed, out error));
            Assert.IsFalse(string.IsNullOrEmpty(error));
        }

        [Test]
        public void ArgumentNameIsExact()
        {
            Assert.IsFalse(RunSeedOption.TryFind(new[] { "-SEED", "7" }, out uint seed, out string error));
            Assert.IsTrue(error == null);
            Assert.IsFalse(RunSeedOption.TryFind(new[] { "-seed=7" }, out seed, out error));
            Assert.IsTrue(error == null);
            Assert.IsFalse(RunSeedOption.TryFind(new[] { "--seed", "7" }, out seed, out error));
            Assert.IsTrue(error == null);
        }
    }
}

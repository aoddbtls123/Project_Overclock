using System;
using System.IO;
using NUnit.Framework;
using Overclock.Core;

namespace Overclock.Tests
{
    // 설정 규칙 + 실제 JsonUtility 와 파일 입출력. 파일은 임시 폴더에서만 읽고 쓴다
    public class GameSettingsTests
    {
        private string directory;

        [SetUp]
        public void CreateDirectoryName()
        {
            directory = Path.Combine(Path.GetTempPath(), "OverclockSettingsTests_" + Guid.NewGuid().ToString("N"));
        }

        [TearDown]
        public void DeleteDirectory()
        {
            if (Directory.Exists(directory))
            {
                Directory.Delete(directory, true);
            }
        }

        [Test]
        public void Defaults_AreShakeOnFlashOnNormalSpeed()
        {
            var settings = new GameSettings();

            Assert.IsTrue(settings.ShakeEnabled);
            Assert.IsTrue(settings.FlashEnabled);
            Assert.AreEqual(RevealSpeed.Normal, settings.RevealSpeed);
            Assert.AreEqual(1f, settings.RevealTimeMultiplier);
        }

        [TestCase(RevealSpeed.Normal, 1f)]
        [TestCase(RevealSpeed.Fast, 0.5f)]
        [TestCase(RevealSpeed.Instant, 0f)]
        public void RevealTimeMultiplier_MatchesSpeed(RevealSpeed speed, float expected)
        {
            var settings = new GameSettings { RevealSpeed = speed };

            Assert.AreEqual(expected, settings.RevealTimeMultiplier);
        }

        [Test]
        public void CycleRevealSpeed_GoesNormalFastInstantNormal()
        {
            var settings = new GameSettings();

            settings.CycleRevealSpeed();
            Assert.AreEqual(RevealSpeed.Fast, settings.RevealSpeed);
            settings.CycleRevealSpeed();
            Assert.AreEqual(RevealSpeed.Instant, settings.RevealSpeed);
            settings.CycleRevealSpeed();
            Assert.AreEqual(RevealSpeed.Normal, settings.RevealSpeed);
        }

        [Test]
        public void RevealSpeed_RejectsUnknownValue()
        {
            var settings = new GameSettings();

            Assert.Throws<ArgumentOutOfRangeException>(() => settings.RevealSpeed = (RevealSpeed)3);
            Assert.AreEqual(RevealSpeed.Normal, settings.RevealSpeed);
        }

        [TestCase(true, false, RevealSpeed.Instant)]
        [TestCase(false, true, RevealSpeed.Fast)]
        [TestCase(false, false, RevealSpeed.Normal)]
        public void Data_RoundTrips(bool shake, bool flash, RevealSpeed speed)
        {
            var settings = new GameSettings { ShakeEnabled = shake, FlashEnabled = flash, RevealSpeed = speed };

            GameSettings copy = GameSettings.FromData(settings.ToData());

            Assert.AreEqual(shake, copy.ShakeEnabled);
            Assert.AreEqual(flash, copy.FlashEnabled);
            Assert.AreEqual(speed, copy.RevealSpeed);
            Assert.AreEqual(GameSettingsData.CurrentVersion, settings.ToData().version);
        }

        [TestCase(2, 0, TestName = "버전 다름")]
        [TestCase(1, 3, TestName = "없는 속도")]
        [TestCase(1, -1, TestName = "음수 속도")]
        public void FromData_RejectsValuesTheGameCannotMake(int version, int speed)
        {
            var data = new GameSettingsData { version = version, shakeEnabled = true, flashEnabled = true, revealSpeed = speed };

            Assert.Catch<ArgumentException>(() => GameSettings.FromData(data));
        }

        [Test]
        public void FromData_RejectsNull()
        {
            Assert.Throws<ArgumentNullException>(() => GameSettings.FromData(null));
        }

        [Test]
        public void File_SaveThenLoad_KeepsSettings()
        {
            var file = new GameSettingsFile(directory);
            var settings = new GameSettings { ShakeEnabled = false, FlashEnabled = true, RevealSpeed = RevealSpeed.Fast };

            file.Save(settings);
            Assert.IsTrue(file.TryLoad(out GameSettings loaded, out string error), error);

            Assert.IsFalse(loaded.ShakeEnabled);
            Assert.IsTrue(loaded.FlashEnabled);
            Assert.AreEqual(RevealSpeed.Fast, loaded.RevealSpeed);
            Assert.IsFalse(File.Exists(file.FilePath + ".tmp"), "임시 파일이 남음");
        }

        [Test]
        public void File_Overwrite_KeepsLatest()
        {
            var file = new GameSettingsFile(directory);
            var settings = new GameSettings { FlashEnabled = false };
            file.Save(settings);
            settings.FlashEnabled = true;
            settings.RevealSpeed = RevealSpeed.Instant;
            file.Save(settings);

            Assert.IsTrue(file.TryLoad(out GameSettings loaded, out string error), error);
            Assert.IsTrue(loaded.FlashEnabled);
            Assert.AreEqual(RevealSpeed.Instant, loaded.RevealSpeed);
            Assert.IsFalse(File.Exists(file.FilePath + ".tmp"), "임시 파일이 남음");
        }

        [Test]
        public void File_Missing_LoadsDefaults()
        {
            var file = new GameSettingsFile(directory);

            Assert.IsTrue(file.TryLoad(out GameSettings loaded, out string error));
            Assert.IsNull(error);
            Assert.IsTrue(loaded.ShakeEnabled);
            Assert.IsTrue(loaded.FlashEnabled);
            Assert.AreEqual(RevealSpeed.Normal, loaded.RevealSpeed);
        }

        [TestCase("", TestName = "빈 파일")]
        [TestCase("{ 깨진", TestName = "형식 오류")]
        [TestCase("{\"version\":1,\"shakeEnabled\":false,\"flashEnabled\":false,\"revealSpeed\":7}", TestName = "없는 속도")]
        [TestCase("{\"version\":2,\"shakeEnabled\":false,\"flashEnabled\":false,\"revealSpeed\":1}", TestName = "버전 다름")]
        public void File_Unreadable_ReturnsFalseWithDefaults(string content)
        {
            Directory.CreateDirectory(directory);
            var file = new GameSettingsFile(directory);
            File.WriteAllText(file.FilePath, content);

            Assert.IsFalse(file.TryLoad(out GameSettings loaded, out string error));
            Assert.IsNotNull(error);
            Assert.IsTrue(loaded.ShakeEnabled);
            Assert.IsTrue(loaded.FlashEnabled);
            Assert.AreEqual(RevealSpeed.Normal, loaded.RevealSpeed);
            Assert.IsTrue(File.Exists(file.FilePath), "읽을 수 없는 파일을 지우면 안 된다");
        }

        [Test]
        public void File_RejectsEmptyDirectory()
        {
            Assert.Throws<ArgumentException>(() => new GameSettingsFile(""));
            Assert.Throws<ArgumentException>(() => new GameSettingsFile(null));
        }
    }
}

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
        public void Defaults_AreShakeOnFlashOnNormalSpeedSoundOn()
        {
            var settings = new GameSettings();

            Assert.IsTrue(settings.ShakeEnabled);
            Assert.IsTrue(settings.FlashEnabled);
            Assert.IsTrue(settings.SoundEnabled);
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

        [TestCase(true, false, RevealSpeed.Instant, false)]
        [TestCase(false, true, RevealSpeed.Fast, true)]
        [TestCase(false, false, RevealSpeed.Normal, false)]
        [TestCase(true, true, RevealSpeed.Normal, true)]
        public void Data_RoundTrips(bool shake, bool flash, RevealSpeed speed, bool sound)
        {
            var settings = new GameSettings { ShakeEnabled = shake, FlashEnabled = flash, RevealSpeed = speed, SoundEnabled = sound };

            GameSettings copy = GameSettings.FromData(settings.ToData());

            Assert.AreEqual(shake, copy.ShakeEnabled);
            Assert.AreEqual(flash, copy.FlashEnabled);
            Assert.AreEqual(speed, copy.RevealSpeed);
            Assert.AreEqual(sound, copy.SoundEnabled);
            Assert.AreEqual(2, settings.ToData().version);
        }

        [TestCase(false, TestName = "버전 1 · 효과음 값 없음")]
        [TestCase(true, TestName = "버전 1 · 효과음 값 있음")]
        public void FromData_Version1_TurnsSoundOnAndKeepsOtherValues(bool soundField)
        {
            var data = new GameSettingsData { version = 1, shakeEnabled = false, flashEnabled = true, revealSpeed = 2, soundEnabled = soundField };

            GameSettings settings = GameSettings.FromData(data);

            Assert.IsTrue(settings.SoundEnabled);
            Assert.IsFalse(settings.ShakeEnabled);
            Assert.IsTrue(settings.FlashEnabled);
            Assert.AreEqual(RevealSpeed.Instant, settings.RevealSpeed);
        }

        [Test]
        public void FromData_Version2_KeepsSoundOff()
        {
            var data = new GameSettingsData { version = 2, shakeEnabled = true, flashEnabled = true, revealSpeed = 0, soundEnabled = false };

            Assert.IsFalse(GameSettings.FromData(data).SoundEnabled);
        }

        [TestCase(3, 0, TestName = "버전 3")]
        [TestCase(0, 0, TestName = "버전 0")]
        [TestCase(2, 3, TestName = "없는 속도")]
        [TestCase(2, -1, TestName = "음수 속도")]
        [TestCase(1, 3, TestName = "버전 1 없는 속도")]
        public void FromData_RejectsValuesTheGameCannotMake(int version, int speed)
        {
            var data = new GameSettingsData { version = version, shakeEnabled = true, flashEnabled = true, revealSpeed = speed, soundEnabled = true };

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
            var settings = new GameSettings { ShakeEnabled = false, FlashEnabled = true, RevealSpeed = RevealSpeed.Fast, SoundEnabled = false };

            file.Save(settings);
            Assert.IsTrue(file.TryLoad(out GameSettings loaded, out string error), error);

            Assert.IsFalse(loaded.ShakeEnabled);
            Assert.IsTrue(loaded.FlashEnabled);
            Assert.AreEqual(RevealSpeed.Fast, loaded.RevealSpeed);
            Assert.IsFalse(loaded.SoundEnabled);
            Assert.IsFalse(File.Exists(file.FilePath + ".tmp"), "임시 파일이 남음");
        }

        [Test]
        public void File_Overwrite_KeepsLatest()
        {
            var file = new GameSettingsFile(directory);
            var settings = new GameSettings { FlashEnabled = false, SoundEnabled = false };
            file.Save(settings);
            settings.FlashEnabled = true;
            settings.RevealSpeed = RevealSpeed.Instant;
            settings.SoundEnabled = true;
            file.Save(settings);

            Assert.IsTrue(file.TryLoad(out GameSettings loaded, out string error), error);
            Assert.IsTrue(loaded.FlashEnabled);
            Assert.AreEqual(RevealSpeed.Instant, loaded.RevealSpeed);
            Assert.IsTrue(loaded.SoundEnabled);
            Assert.IsFalse(File.Exists(file.FilePath + ".tmp"), "임시 파일이 남음");
        }

        [Test]
        public void File_Version1FromEarlierBuild_LoadsWithSoundOn()
        {
            // f1ca6da ~ 9d4a6ba 가 쓴 settings.json 모양 그대로 (soundEnabled 줄 없음)
            Directory.CreateDirectory(directory);
            var file = new GameSettingsFile(directory);
            File.WriteAllText(file.FilePath, "{\n    \"version\": 1,\n    \"shakeEnabled\": false,\n    \"flashEnabled\": false,\n    \"revealSpeed\": 1\n}");

            Assert.IsTrue(file.TryLoad(out GameSettings loaded, out string error), error);
            Assert.IsTrue(loaded.SoundEnabled);
            Assert.IsFalse(loaded.ShakeEnabled);
            Assert.IsFalse(loaded.FlashEnabled);
            Assert.AreEqual(RevealSpeed.Fast, loaded.RevealSpeed);
        }

        [Test]
        public void File_Missing_LoadsDefaults()
        {
            var file = new GameSettingsFile(directory);

            Assert.IsTrue(file.TryLoad(out GameSettings loaded, out string error));
            Assert.IsNull(error);
            Assert.IsTrue(loaded.ShakeEnabled);
            Assert.IsTrue(loaded.FlashEnabled);
            Assert.IsTrue(loaded.SoundEnabled);
            Assert.AreEqual(RevealSpeed.Normal, loaded.RevealSpeed);
        }

        [TestCase("", TestName = "빈 파일")]
        [TestCase("{ 깨진", TestName = "형식 오류")]
        [TestCase("{\"version\":2,\"shakeEnabled\":false,\"flashEnabled\":false,\"revealSpeed\":7,\"soundEnabled\":false}", TestName = "없는 속도")]
        [TestCase("{\"version\":3,\"shakeEnabled\":false,\"flashEnabled\":false,\"revealSpeed\":1,\"soundEnabled\":false}", TestName = "버전 다름")]
        public void File_Unreadable_ReturnsFalseWithDefaults(string content)
        {
            Directory.CreateDirectory(directory);
            var file = new GameSettingsFile(directory);
            File.WriteAllText(file.FilePath, content);

            Assert.IsFalse(file.TryLoad(out GameSettings loaded, out string error));
            Assert.IsNotNull(error);
            Assert.IsTrue(loaded.ShakeEnabled);
            Assert.IsTrue(loaded.FlashEnabled);
            Assert.IsTrue(loaded.SoundEnabled);
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

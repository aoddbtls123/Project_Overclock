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
        public void Defaults_AreShakeOnFlashOnNormalSpeedFullVolume()
        {
            var settings = new GameSettings();

            Assert.IsTrue(settings.ShakeEnabled);
            Assert.IsTrue(settings.FlashEnabled);
            AssertVolumes(settings, 1f, 1f, 1f, 1f);
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

        [TestCase(SoundChannel.Master, 0.25f, 0.25f)]
        [TestCase(SoundChannel.Effects, 0.6f, 0.6f)]
        [TestCase(SoundChannel.Ui, 1.5f, 1f)]
        [TestCase(SoundChannel.Music, -0.2f, 0f)]
        public void SetVolume_ClampsToZeroOne(SoundChannel channel, float value, float expected)
        {
            var settings = new GameSettings();

            settings.SetVolume(channel, value);

            Assert.AreEqual(expected, settings.GetVolume(channel));
            foreach (SoundChannel other in Enum.GetValues(typeof(SoundChannel)))
            {
                if (other != channel)
                {
                    Assert.AreEqual(1f, settings.GetVolume(other), "다른 볼륨이 바뀜: " + other);
                }
            }
        }

        [Test]
        public void SetVolume_RejectsNaNInfinityAndUnknownChannel()
        {
            var settings = new GameSettings();

            Assert.Throws<ArgumentOutOfRangeException>(() => settings.SetVolume(SoundChannel.Effects, float.NaN));
            Assert.Throws<ArgumentOutOfRangeException>(() => settings.SetVolume(SoundChannel.Effects, float.PositiveInfinity));
            Assert.Throws<ArgumentOutOfRangeException>(() => settings.SetVolume((SoundChannel)4, 0.5f));
            Assert.Throws<ArgumentOutOfRangeException>(() => settings.GetVolume((SoundChannel)(-1)));
            AssertVolumes(settings, 1f, 1f, 1f, 1f);
        }

        [Test]
        public void EffectiveVolume_IsMasterTimesChannel()
        {
            var settings = new GameSettings { MasterVolume = 0.5f, EffectsVolume = 0.8f, UiVolume = 0.2f, MusicVolume = 0f };

            Assert.AreEqual(0.5f, settings.GetEffectiveVolume(SoundChannel.Master), 1e-6f);
            Assert.AreEqual(0.4f, settings.GetEffectiveVolume(SoundChannel.Effects), 1e-6f);
            Assert.AreEqual(0.1f, settings.GetEffectiveVolume(SoundChannel.Ui), 1e-6f);
            Assert.AreEqual(0f, settings.GetEffectiveVolume(SoundChannel.Music), 1e-6f);
        }

        [TestCase(true, false, RevealSpeed.Instant, 0f, 0.3f, 1f, 0.55f)]
        [TestCase(false, true, RevealSpeed.Fast, 1f, 1f, 0f, 1f)]
        [TestCase(false, false, RevealSpeed.Normal, 0.75f, 0f, 0.5f, 0.25f)]
        public void Data_RoundTrips(bool shake, bool flash, RevealSpeed speed, float master, float effects, float ui, float music)
        {
            var settings = new GameSettings
            {
                ShakeEnabled = shake, FlashEnabled = flash, RevealSpeed = speed,
                MasterVolume = master, EffectsVolume = effects, UiVolume = ui, MusicVolume = music,
            };

            GameSettings copy = GameSettings.FromData(settings.ToData());

            Assert.AreEqual(shake, copy.ShakeEnabled);
            Assert.AreEqual(flash, copy.FlashEnabled);
            Assert.AreEqual(speed, copy.RevealSpeed);
            AssertVolumes(copy, master, effects, ui, music);
            Assert.AreEqual(3, settings.ToData().version);
        }

        [TestCase(false, TestName = "버전 1 · 효과음 값 없음")]
        [TestCase(true, TestName = "버전 1 · 효과음 값 있음")]
        public void FromData_Version1_FullVolumeAndKeepsOtherValues(bool soundField)
        {
            var data = new GameSettingsData { version = 1, shakeEnabled = false, flashEnabled = true, revealSpeed = 2, soundEnabled = soundField, masterVolume = 0.3f };

            GameSettings settings = GameSettings.FromData(data);

            AssertVolumes(settings, 1f, 1f, 1f, 1f);
            Assert.IsFalse(settings.ShakeEnabled);
            Assert.IsTrue(settings.FlashEnabled);
            Assert.AreEqual(RevealSpeed.Instant, settings.RevealSpeed);
        }

        [TestCase(true, 1f, TestName = "버전 2 · 켬 = 100%")]
        [TestCase(false, 0f, TestName = "버전 2 · 끔 = 전체 0%")]
        public void FromData_Version2_SoundToggleBecomesMasterVolume(bool soundEnabled, float master)
        {
            // 버전 2 파일에는 볼륨 값이 없다 (0 으로 읽힘) — 켬/끔만 보고 정한다
            var data = new GameSettingsData { version = 2, shakeEnabled = true, flashEnabled = false, revealSpeed = 1, soundEnabled = soundEnabled };

            GameSettings settings = GameSettings.FromData(data);

            AssertVolumes(settings, master, 1f, 1f, 1f);
            Assert.IsFalse(settings.FlashEnabled);
            Assert.AreEqual(RevealSpeed.Fast, settings.RevealSpeed);
        }

        [TestCase(-0.01f, 1f, 1f, 1f, TestName = "전체 음수")]
        [TestCase(1f, 1.01f, 1f, 1f, TestName = "효과음 1 초과")]
        [TestCase(1f, 1f, float.NaN, 1f, TestName = "UI NaN")]
        [TestCase(1f, 1f, 1f, float.PositiveInfinity, TestName = "BGM 무한")]
        public void FromData_Version3_RejectsVolumeOutOfRange(float master, float effects, float ui, float music)
        {
            var data = new GameSettingsData { version = 3, shakeEnabled = true, flashEnabled = true, revealSpeed = 0, masterVolume = master, effectsVolume = effects, uiVolume = ui, musicVolume = music };

            Assert.Catch<ArgumentException>(() => GameSettings.FromData(data));
        }

        [TestCase(4, 0, TestName = "버전 4")]
        [TestCase(0, 0, TestName = "버전 0")]
        [TestCase(2, 3, TestName = "없는 속도")]
        [TestCase(2, -1, TestName = "음수 속도")]
        [TestCase(3, 3, TestName = "버전 3 없는 속도")]
        [TestCase(1, 3, TestName = "버전 1 없는 속도")]
        public void FromData_RejectsValuesTheGameCannotMake(int version, int speed)
        {
            var data = new GameSettingsData { version = version, shakeEnabled = true, flashEnabled = true, revealSpeed = speed, soundEnabled = true, masterVolume = 1f, effectsVolume = 1f, uiVolume = 1f, musicVolume = 1f };

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
            var settings = new GameSettings { ShakeEnabled = false, FlashEnabled = true, RevealSpeed = RevealSpeed.Fast, MasterVolume = 0.8f, EffectsVolume = 0.35f, UiVolume = 0f, MusicVolume = 0.6f };

            file.Save(settings);
            Assert.IsTrue(file.TryLoad(out GameSettings loaded, out string error), error);

            Assert.IsFalse(loaded.ShakeEnabled);
            Assert.IsTrue(loaded.FlashEnabled);
            Assert.AreEqual(RevealSpeed.Fast, loaded.RevealSpeed);
            AssertVolumes(loaded, 0.8f, 0.35f, 0f, 0.6f);
            Assert.IsFalse(File.Exists(file.FilePath + ".tmp"), "임시 파일이 남음");
        }

        [Test]
        public void File_Overwrite_KeepsLatest()
        {
            var file = new GameSettingsFile(directory);
            var settings = new GameSettings { FlashEnabled = false, MasterVolume = 0f };
            file.Save(settings);
            settings.FlashEnabled = true;
            settings.RevealSpeed = RevealSpeed.Instant;
            settings.MasterVolume = 0.9f;
            file.Save(settings);

            Assert.IsTrue(file.TryLoad(out GameSettings loaded, out string error), error);
            Assert.IsTrue(loaded.FlashEnabled);
            Assert.AreEqual(RevealSpeed.Instant, loaded.RevealSpeed);
            AssertVolumes(loaded, 0.9f, 1f, 1f, 1f);
            Assert.IsFalse(File.Exists(file.FilePath + ".tmp"), "임시 파일이 남음");
        }

        [Test]
        public void File_Version1FromEarlierBuild_LoadsWithFullVolume()
        {
            // f1ca6da ~ 9d4a6ba 가 쓴 settings.json 모양 그대로 (soundEnabled 줄 없음)
            Directory.CreateDirectory(directory);
            var file = new GameSettingsFile(directory);
            File.WriteAllText(file.FilePath, "{\n    \"version\": 1,\n    \"shakeEnabled\": false,\n    \"flashEnabled\": false,\n    \"revealSpeed\": 1\n}");

            Assert.IsTrue(file.TryLoad(out GameSettings loaded, out string error), error);
            AssertVolumes(loaded, 1f, 1f, 1f, 1f);
            Assert.IsFalse(loaded.ShakeEnabled);
            Assert.IsFalse(loaded.FlashEnabled);
            Assert.AreEqual(RevealSpeed.Fast, loaded.RevealSpeed);
        }

        [TestCase(true, 1f, TestName = "버전 2 파일 · 켬")]
        [TestCase(false, 0f, TestName = "버전 2 파일 · 끔")]
        public void File_Version2FromEarlierBuild_SoundToggleBecomesMasterVolume(bool soundEnabled, float master)
        {
            // 93e315f ~ 77287ab 가 쓴 settings.json 모양 그대로 (볼륨 줄 없음)
            Directory.CreateDirectory(directory);
            var file = new GameSettingsFile(directory);
            File.WriteAllText(file.FilePath, "{\n    \"version\": 2,\n    \"shakeEnabled\": true,\n    \"flashEnabled\": false,\n    \"revealSpeed\": 0,\n    \"soundEnabled\": " + (soundEnabled ? "true" : "false") + "\n}");

            Assert.IsTrue(file.TryLoad(out GameSettings loaded, out string error), error);
            AssertVolumes(loaded, master, 1f, 1f, 1f);
            Assert.IsFalse(loaded.FlashEnabled);
        }

        [Test]
        public void File_Missing_LoadsDefaults()
        {
            var file = new GameSettingsFile(directory);

            Assert.IsTrue(file.TryLoad(out GameSettings loaded, out string error));
            Assert.IsNull(error);
            Assert.IsTrue(loaded.ShakeEnabled);
            Assert.IsTrue(loaded.FlashEnabled);
            AssertVolumes(loaded, 1f, 1f, 1f, 1f);
            Assert.AreEqual(RevealSpeed.Normal, loaded.RevealSpeed);
        }

        [TestCase("", TestName = "빈 파일")]
        [TestCase("{ 깨진", TestName = "형식 오류")]
        [TestCase("{\"version\":2,\"shakeEnabled\":false,\"flashEnabled\":false,\"revealSpeed\":7,\"soundEnabled\":false}", TestName = "없는 속도")]
        [TestCase("{\"version\":4,\"shakeEnabled\":false,\"flashEnabled\":false,\"revealSpeed\":1,\"soundEnabled\":false}", TestName = "버전 다름")]
        [TestCase("{\"version\":3,\"shakeEnabled\":false,\"flashEnabled\":false,\"revealSpeed\":1,\"masterVolume\":1.5,\"effectsVolume\":1,\"uiVolume\":1,\"musicVolume\":1}", TestName = "볼륨 범위 밖")]
        public void File_Unreadable_ReturnsFalseWithDefaults(string content)
        {
            Directory.CreateDirectory(directory);
            var file = new GameSettingsFile(directory);
            File.WriteAllText(file.FilePath, content);

            Assert.IsFalse(file.TryLoad(out GameSettings loaded, out string error));
            Assert.IsNotNull(error);
            Assert.IsTrue(loaded.ShakeEnabled);
            Assert.IsTrue(loaded.FlashEnabled);
            AssertVolumes(loaded, 1f, 1f, 1f, 1f);
            Assert.AreEqual(RevealSpeed.Normal, loaded.RevealSpeed);
            Assert.IsTrue(File.Exists(file.FilePath), "읽을 수 없는 파일을 지우면 안 된다");
        }

        private static void AssertVolumes(GameSettings settings, float master, float effects, float ui, float music)
        {
            Assert.AreEqual(master, settings.MasterVolume, 1e-6f, "전체");
            Assert.AreEqual(effects, settings.EffectsVolume, 1e-6f, "효과음");
            Assert.AreEqual(ui, settings.UiVolume, 1e-6f, "UI");
            Assert.AreEqual(music, settings.MusicVolume, 1e-6f, "BGM");
        }

        [Test]
        public void File_RejectsEmptyDirectory()
        {
            Assert.Throws<ArgumentException>(() => new GameSettingsFile(""));
            Assert.Throws<ArgumentException>(() => new GameSettingsFile(null));
        }
    }
}

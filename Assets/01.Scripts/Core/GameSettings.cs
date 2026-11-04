using System;

namespace Overclock.Core
{
    /// <summary>
    /// 플레이어 설정 (접근성 · 연출 속도 · 볼륨). 기본값 = 흔들림 켬 · 섬광 켬 · 보통 속도 · 볼륨 모두 100%.
    /// </summary>
    public sealed class GameSettings
    {
        private const int ChannelCount = 4;

        private readonly float[] volumes = { 1f, 1f, 1f, 1f };
        private RevealSpeed revealSpeed = RevealSpeed.Normal;

        public bool ShakeEnabled { get; set; } = true;

        public bool FlashEnabled { get; set; } = true;

        public float MasterVolume
        {
            get => GetVolume(SoundChannel.Master);
            set => SetVolume(SoundChannel.Master, value);
        }

        public float EffectsVolume
        {
            get => GetVolume(SoundChannel.Effects);
            set => SetVolume(SoundChannel.Effects, value);
        }

        public float UiVolume
        {
            get => GetVolume(SoundChannel.Ui);
            set => SetVolume(SoundChannel.Ui, value);
        }

        public float MusicVolume
        {
            get => GetVolume(SoundChannel.Music);
            set => SetVolume(SoundChannel.Music, value);
        }

        public RevealSpeed RevealSpeed
        {
            get => revealSpeed;
            set => revealSpeed = ValidSpeed(value);
        }

        /// <summary>공개 연출 시간에 곱하는 값. 보통 1 · 빠름 0.5 · 즉시 0 (기다리지 않고 공개 효과도 없음).</summary>
        public float RevealTimeMultiplier
        {
            get
            {
                switch (revealSpeed)
                {
                    case RevealSpeed.Fast:
                        return 0.5f;
                    case RevealSpeed.Instant:
                        return 0f;
                    default:
                        return 1f;
                }
            }
        }

        /// <summary>보통 → 빠름 → 즉시 → 보통 순서로 바꾼다.</summary>
        public void CycleRevealSpeed()
        {
            revealSpeed = revealSpeed == RevealSpeed.Instant ? RevealSpeed.Normal : revealSpeed + 1;
        }

        public float GetVolume(SoundChannel channel)
        {
            return volumes[Index(channel)];
        }

        /// <summary>볼륨 (0~1). 범위 밖이면 끝 값으로 자른다 · NaN · 무한은 ArgumentOutOfRangeException.</summary>
        public void SetVolume(SoundChannel channel, float value)
        {
            if (float.IsNaN(value) || float.IsInfinity(value))
            {
                throw new ArgumentOutOfRangeException(nameof(value), "볼륨은 수여야 합니다: " + value);
            }

            volumes[Index(channel)] = Math.Max(0f, Math.Min(1f, value));
        }

        /// <summary>실제 소리 크기 = 전체 × 종류. 전체를 물으면 전체 값.</summary>
        public float GetEffectiveVolume(SoundChannel channel)
        {
            return channel == SoundChannel.Master ? MasterVolume : MasterVolume * GetVolume(channel);
        }

        public GameSettingsData ToData()
        {
            return new GameSettingsData
            {
                version = GameSettingsData.CurrentVersion,
                shakeEnabled = ShakeEnabled,
                flashEnabled = FlashEnabled,
                revealSpeed = (int)revealSpeed,
                masterVolume = MasterVolume,
                effectsVolume = EffectsVolume,
                uiVolume = UiVolume,
                musicVolume = MusicVolume,
            };
        }

        /// <summary>
        /// 파일에서 읽은 값으로 설정을 만든다. 게임이 만들 수 없는 값이면 ArgumentException.
        /// 버전 1 파일(효과음 설정이 생기기 전) = 볼륨 모두 100% · 버전 2 (효과음 켬/끔) = 켬이면 모두 100% · 끔이면 전체 0% (나머지 100%).
        /// 버전 3 볼륨은 0~1 이어야 한다 (자르지 않고 거부 — 게임은 범위 밖 값을 쓰지 않는다).
        /// </summary>
        public static GameSettings FromData(GameSettingsData data)
        {
            if (data == null)
            {
                throw new ArgumentNullException(nameof(data));
            }

            if (data.version != GameSettingsData.FirstVersion
                && data.version != GameSettingsData.SoundToggleVersion
                && data.version != GameSettingsData.CurrentVersion)
            {
                throw new ArgumentException("지원하지 않는 설정 버전입니다: " + data.version, nameof(data));
            }

            // 없는 속도 값은 RevealSpeed 대입에서 ArgumentOutOfRangeException (ArgumentException) 으로 거부된다
            var settings = new GameSettings
            {
                ShakeEnabled = data.shakeEnabled,
                FlashEnabled = data.flashEnabled,
                RevealSpeed = (RevealSpeed)data.revealSpeed,
            };

            if (data.version == GameSettingsData.SoundToggleVersion && !data.soundEnabled)
            {
                settings.MasterVolume = 0f;
            }
            else if (data.version == GameSettingsData.CurrentVersion)
            {
                settings.MasterVolume = ValidVolume(data.masterVolume, nameof(data.masterVolume));
                settings.EffectsVolume = ValidVolume(data.effectsVolume, nameof(data.effectsVolume));
                settings.UiVolume = ValidVolume(data.uiVolume, nameof(data.uiVolume));
                settings.MusicVolume = ValidVolume(data.musicVolume, nameof(data.musicVolume));
            }

            return settings;
        }

        private static float ValidVolume(float value, string name)
        {
            if (float.IsNaN(value) || value < 0f || value > 1f)
            {
                throw new ArgumentException("볼륨은 0~1 이어야 합니다: " + name + " = " + value, name);
            }

            return value;
        }

        private static int Index(SoundChannel channel)
        {
            int index = (int)channel;
            if (index < 0 || index >= ChannelCount)
            {
                throw new ArgumentOutOfRangeException(nameof(channel));
            }

            return index;
        }

        private static RevealSpeed ValidSpeed(RevealSpeed speed)
        {
            if (!Enum.IsDefined(typeof(RevealSpeed), speed))
            {
                throw new ArgumentOutOfRangeException(nameof(speed));
            }

            return speed;
        }
    }
}

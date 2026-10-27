using System;

namespace Overclock.Core
{
    /// <summary>
    /// 플레이어 설정 (접근성 · 연출 속도). 기본값 = 흔들림 켬 · 섬광 켬 · 보통 속도.
    /// </summary>
    public sealed class GameSettings
    {
        private RevealSpeed revealSpeed = RevealSpeed.Normal;

        public bool ShakeEnabled { get; set; } = true;

        public bool FlashEnabled { get; set; } = true;

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

        public GameSettingsData ToData()
        {
            return new GameSettingsData
            {
                version = GameSettingsData.CurrentVersion,
                shakeEnabled = ShakeEnabled,
                flashEnabled = FlashEnabled,
                revealSpeed = (int)revealSpeed,
            };
        }

        /// <summary>
        /// 파일에서 읽은 값으로 설정을 만든다. 게임이 만들 수 없는 값이면 ArgumentException.
        /// </summary>
        public static GameSettings FromData(GameSettingsData data)
        {
            if (data == null)
            {
                throw new ArgumentNullException(nameof(data));
            }

            if (data.version != GameSettingsData.CurrentVersion)
            {
                throw new ArgumentException("지원하지 않는 설정 버전입니다: " + data.version, nameof(data));
            }

            // 없는 속도 값은 RevealSpeed 대입에서 ArgumentOutOfRangeException (ArgumentException) 으로 거부된다
            return new GameSettings
            {
                ShakeEnabled = data.shakeEnabled,
                FlashEnabled = data.flashEnabled,
                RevealSpeed = (RevealSpeed)data.revealSpeed,
            };
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

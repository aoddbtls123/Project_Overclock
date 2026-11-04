using System;
using Overclock.Core;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Overclock.Presentation
{
    // 설정 버튼 3개 (화면 흔들림 · 섬광 · 연출 속도) + 볼륨 슬라이더 4개 (전체 · 효과음 · UI · BGM · 11/04 — 효과음 켬/끔 버튼 대신).
    // 일시정지 창과 타이틀 설정 창이 같은 컴포넌트를 하나씩 쓴다.
    // 값을 바꾸고 저장하는 일은 GrayBoxRunScreen 이 하고, 이 창은 버튼 · 슬라이더를 연결하고 글자를 채우기만 한다
    public sealed class SettingsMenuView : MonoBehaviour
    {
        [SerializeField] private Button shakeButton;
        [SerializeField] private TMP_Text shakeLabel;
        [SerializeField] private Button flashButton;
        [SerializeField] private TMP_Text flashLabel;
        [SerializeField] private Button speedButton;
        [SerializeField] private TMP_Text speedLabel;
        [Tooltip("전체 · 효과음 · UI · BGM (순서 무관 · 칸마다 종류를 정해 둔다). 비어 있는 칸은 건너뛴다")]
        [SerializeField] private VolumeSliderView[] volumeSliders = Array.Empty<VolumeSliderView>();

        public void Bind(Action onShake, Action onFlash, Action onSpeed, Action<SoundChannel, float> onVolumeChanged, Action<SoundChannel> onVolumeCommitted)
        {
            shakeButton.onClick.AddListener(() => onShake());
            flashButton.onClick.AddListener(() => onFlash());
            speedButton.onClick.AddListener(() => onSpeed());
            for (int i = 0; i < volumeSliders.Length; i++)
            {
                if (volumeSliders[i] != null)
                {
                    volumeSliders[i].Bind(onVolumeChanged, onVolumeCommitted);
                }
            }
        }

        public void Show(GameSettings settings)
        {
            shakeLabel.text = "화면 흔들림: " + OnOff(settings.ShakeEnabled);
            flashLabel.text = "섬광: " + OnOff(settings.FlashEnabled);
            speedLabel.text = "연출 속도: " + SpeedName(settings.RevealSpeed);
            for (int i = 0; i < volumeSliders.Length; i++)
            {
                if (volumeSliders[i] != null)
                {
                    volumeSliders[i].Show(settings.GetVolume(volumeSliders[i].Channel));
                }
            }
        }

        private static string OnOff(bool enabled)
        {
            return enabled ? "켬" : "끔";
        }

        private static string SpeedName(RevealSpeed speed)
        {
            switch (speed)
            {
                case RevealSpeed.Fast:
                    return "빠름";
                case RevealSpeed.Instant:
                    return "즉시";
                default:
                    return "보통";
            }
        }
    }
}

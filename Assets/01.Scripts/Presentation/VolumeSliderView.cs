using System;
using Overclock.Core;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Overclock.Presentation
{
    // 볼륨 슬라이더 한 줄 (이름 + 퍼센트 글자 + 슬라이더). 슬라이더와 같은 오브젝트에 붙임 — 마우스를 놓는 순간(OnPointerUp)을 받기 위해서.
    // 값을 바꾸고 저장하는 일은 GrayBoxRunScreen 이 함. 끄는 동안은 바로 소리에 반영하고, 놓을 때 한 번 저장함
    [RequireComponent(typeof(Slider))]
    public sealed class VolumeSliderView : MonoBehaviour, IPointerUpHandler
    {
        [SerializeField] private SoundChannel channel;
        [SerializeField] private Slider slider;
        [SerializeField] private TMP_Text label;

        private Action<SoundChannel> committed;

        public SoundChannel Channel => channel;

        public void Bind(Action<SoundChannel, float> onChanged, Action<SoundChannel> onCommitted)
        {
            if (onChanged == null)
            {
                throw new ArgumentNullException(nameof(onChanged));
            }

            committed = onCommitted;
            slider.minValue = 0f;
            slider.maxValue = 1f;
            slider.wholeNumbers = false;
            slider.onValueChanged.AddListener(value =>
            {
                ShowLabel(value);
                onChanged(channel, value);
            });
        }

        /// <summary>설정 값을 보여 줌. 슬라이더 이벤트는 내지 않음 (다른 창의 같은 슬라이더를 맞출 때도 씀).</summary>
        public void Show(float value)
        {
            slider.SetValueWithoutNotify(value);
            ShowLabel(value);
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            committed?.Invoke(channel);
        }

        private void ShowLabel(float value)
        {
            if (label != null)
            {
                label.text = Title(channel) + " " + Mathf.RoundToInt(value * 100f) + "%";
            }
        }

        internal static string Title(SoundChannel channel)
        {
            switch (channel)
            {
                case SoundChannel.Effects:
                    return "효과음";
                case SoundChannel.Ui:
                    return "UI";
                case SoundChannel.Music:
                    return "BGM";
                default:
                    return "전체";
            }
        }
    }
}

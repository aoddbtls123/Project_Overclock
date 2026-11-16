using UnityEngine;

namespace Overclock.Presentation
{
    // 왼쪽 계기판(등급별 확률 · 천장)의 변화 연출 값. 시간은 scaled time(초)
    [CreateAssetMenu(fileName = "GaugeDisplaySettings", menuName = "Overclock/Gauge Display Settings")]
    public sealed class GaugeDisplaySettings : ScriptableObject
    {
        [Tooltip("모듈을 단 뒤 확률 숫자가 이전 값에서 새 값으로 바뀌는 시간. 연출 속도 배율이 곱해짐 (즉시 = 바로 바뀜)")]
        [SerializeField, Min(0f)] private float rollSeconds = 0.6f;
        [Tooltip("바뀐 줄 옆 ▲▼ 와 변화량이 남아 있는 시간 (숫자가 다 바뀐 뒤부터)")]
        [SerializeField, Min(0f)] private float markSeconds = 2.5f;
        [Tooltip("▲▼ 글자 크기 (확률 글자 대비 %)")]
        [SerializeField, Range(30, 100)] private int markSizePercent = 75;
        [SerializeField] private Color upColor = new Color32(0x7C, 0xD9, 0x92, 0xFF);
        [SerializeField] private Color downColor = new Color32(0xE0, 0x66, 0x66, 0xFF);

        public float RollSeconds => rollSeconds;
        public float MarkSeconds => markSeconds;
        public int MarkSizePercent => markSizePercent;
        public Color UpColor => upColor;
        public Color DownColor => downColor;
    }
}

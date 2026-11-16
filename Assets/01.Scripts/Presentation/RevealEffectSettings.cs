using UnityEngine;

namespace Overclock.Presentation
{
    // 공개 연출 효과 값. 시간(초)은 RevealTimings 가 등급별로 정하고, 여기는 색 · 세기 · 효과 길이만 둠
    [CreateAssetMenu(fileName = "RevealEffectSettings", menuName = "Overclock/Reveal Effect Settings")]
    public sealed class RevealEffectSettings : ScriptableObject
    {
        [Header("SR 달아오름 (연출 시간 동안)")]
        [SerializeField] private Color heatStartColor = new Color(1f, 0.35f, 0f, 0f);
        [SerializeField] private Color heatEndColor = new Color(1f, 0.69f, 0.125f, 0.6f);
        [Tooltip("연출 시간 중 이 비율에서 가장 뜨거워지고, 남은 시간은 그 세기를 유지함")]
        [SerializeField, Range(0.1f, 1f)] private float heatRampRatio = 0.8f;
        [Tooltip("가장 뜨거울 때 흔들림 폭 (px)")]
        [SerializeField, Min(0f)] private float shakeStrength = 10f;
        [Tooltip("연출 전체 동안 흔들림이 바뀌는 횟수 (클수록 잘게 떪)")]
        [SerializeField, Min(1f)] private float shakeCycles = 30f;

        [Header("SR 공개 순간")]
        [SerializeField] private Color flashColor = new Color(1f, 0.91f, 0.69f, 0.55f);
        [SerializeField, Min(0.01f)] private float flashSeconds = 0.25f;
        [SerializeField] private Color iconGlowColor = new Color(1f, 0.69f, 0.125f, 0.9f);
        [SerializeField, Min(0f)] private float iconGlowStartScale = 0.6f;
        [SerializeField, Min(0f)] private float iconGlowEndScale = 1.8f;
        [Tooltip("공개 뒤 빛 · 테두리가 사라지는 시간")]
        [SerializeField, Min(0.01f)] private float afterglowSeconds = 0.4f;
        [SerializeField, Min(0f)] private float kickStrength = 14f;
        [SerializeField, Min(0.01f)] private float kickSeconds = 0.25f;

        [Header("아이콘 펀치 (R · SR 공개 순간)")]
        [SerializeField, Min(0f)] private float rarePunch = 0.15f;
        [SerializeField, Min(0f)] private float superRarePunch = 0.35f;
        [SerializeField, Min(0.01f)] private float punchSeconds = 0.3f;

        public Color HeatStartColor => heatStartColor;
        public Color HeatEndColor => heatEndColor;
        public float HeatRampRatio => heatRampRatio;
        public float ShakeStrength => shakeStrength;
        public float ShakeCycles => shakeCycles;
        public Color FlashColor => flashColor;
        public float FlashSeconds => flashSeconds;
        public Color IconGlowColor => iconGlowColor;
        public float IconGlowStartScale => iconGlowStartScale;
        public float IconGlowEndScale => iconGlowEndScale;
        public float AfterglowSeconds => afterglowSeconds;
        public float KickStrength => kickStrength;
        public float KickSeconds => kickSeconds;
        public float RarePunch => rarePunch;
        public float SuperRarePunch => superRarePunch;
        public float PunchSeconds => punchSeconds;
    }
}

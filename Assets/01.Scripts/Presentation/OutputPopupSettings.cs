using Overclock.Core;
using UnityEngine;

namespace Overclock.Presentation
{
    // 공개 순간 출력 창 위에 크게 뜨는 생산량 "+n" 의 겉모습 값 (11/06 · 기계연출 구상 §13 "팝업 확대").
    // 시간은 scaled time(초) · 연출 속도 배율이 곱해짐 · 크기는 연출 예산 N 10 / R 20 / SR 70% 를 따름
    [CreateAssetMenu(fileName = "OutputPopupSettings", menuName = "Overclock/Output Popup Settings")]
    public sealed class OutputPopupSettings : ScriptableObject
    {
        [Header("크기 (글자 기본 크기 대비 배율)")]
        [SerializeField, Min(0.1f)] private float scaleN = 0.8f;
        [SerializeField, Min(0.1f)] private float scaleR = 1.1f;
        [SerializeField, Min(0.1f)] private float scaleSR = 1.7f;
        [Tooltip("3연속이 발동했거나 오버클럭 중인 뽑기는 이만큼 더 크게 (발동 상태 표시 · 생산량 증가 여부와 별개)")]
        [SerializeField, Min(1f)] private float bonusScale = 1.15f;

        [Header("움직임")]
        [Tooltip("튀어나오는 시간 (작게 → 살짝 넘쳤음 → 제 크기)")]
        [SerializeField, Min(0.01f)] private float popSeconds = 0.14f;
        [Tooltip("다 튀어나온 뒤 그대로 있는 시간")]
        [SerializeField, Min(0f)] private float holdSeconds = 0.45f;
        [SerializeField, Min(0.01f)] private float fadeSeconds = 0.35f;
        [Tooltip("떠 있는 동안 위로 올라가는 거리 (px)")]
        [SerializeField, Min(0f)] private float riseN = 30f;
        [SerializeField, Min(0f)] private float riseR = 45f;
        [SerializeField, Min(0f)] private float riseSR = 70f;

        public float BonusScale => bonusScale;
        public float PopSeconds => popSeconds;
        public float HoldSeconds => holdSeconds;
        public float FadeSeconds => fadeSeconds;

        public float GetScale(Grade grade)
        {
            switch (grade)
            {
                case Grade.SR:
                    return scaleSR;
                case Grade.R:
                    return scaleR;
                default:
                    return scaleN;
            }
        }

        public float GetRise(Grade grade)
        {
            switch (grade)
            {
                case Grade.SR:
                    return riseSR;
                case Grade.R:
                    return riseR;
                default:
                    return riseN;
            }
        }
    }
}

using UnityEngine;

namespace Overclock.Presentation
{
    // 다시 받기 카드 회전 (11/07) 의 시간 값. 연출 속도 배율 (보통 1 · 빠름 0.5 · 즉시 0) 은 회전 · 시작 간격 둘 다에 곱한다
    [CreateAssetMenu(fileName = "OfferFlipSettings", menuName = "Overclock/Offer Flip Settings")]
    public sealed class OfferFlipSettings : ScriptableObject
    {
        [Tooltip("카드 한 장이 세로축으로 한 바퀴 (360°) 도는 시간 (초). 세 장 모두 같다")]
        [SerializeField, Min(0.05f)] private float spinSeconds = 0.6f;
        [Tooltip("왼쪽 카드부터 다음 카드가 돌기 시작할 때까지 (초)")]
        [SerializeField, Min(0f)] private float staggerSeconds = 0.06f;
        [Tooltip("정면에 닿는 순간 남아 있는 회전 속도 (평균 속도 대비). 0 = 끝까지 감속해 스르르 멈춤 · 클수록 걸쇠에 딱 걸리듯 멈춤")]
        [SerializeField, Range(0f, 0.5f)] private float latchSpeed = 0.15f;

        public float SpinSeconds => spinSeconds;
        public float StaggerSeconds => staggerSeconds;
        public float LatchSpeed => latchSpeed;
    }
}

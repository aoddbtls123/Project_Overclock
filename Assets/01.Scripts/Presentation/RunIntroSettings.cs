using UnityEngine;

namespace Overclock.Presentation
{
    // 런 시작 등장 연출 (11/07 · v3 레일 진입) 의 시간 값 (기본: 마지막 패널 도착 1.01초 · 복귀 끝 1.25초). 연출 속도 배율 (보통 1 · 빠름 0.5 · 즉시 0) 을 모든 시간에 곱함.
    // 들어오는 쪽 · 지나치는 거리는 패널마다 다르므로 씬의 IntroPanelView 에 둠. 출발 자리는 화면 크기로 매번 계산함
    [CreateAssetMenu(fileName = "RunIntroSettings", menuName = "Overclock/Run Intro Settings")]
    public sealed class RunIntroSettings : ScriptableObject
    {
        [Tooltip("런이 시작되고 첫 패널이 출발하기 전까지 (초). 이 동안 기계와 바탕만 보임")]
        [SerializeField, Min(0f)] private float firstDelaySeconds = 0.08f;
        [Tooltip("앞 패널이 출발한 뒤 다음 패널이 출발할 때까지 (초). 앞 패널이 도착하기 전에 다음이 출발함")]
        [SerializeField, Min(0f)] private float staggerSeconds = 0.14f;
        [Tooltip("화면 밖에서 최종 자리까지 오는 시간 (초). 끌려 나옴 → 풀려 들어옴 (가장 빠름) → 제동 순서로 이 안에서 속도가 바뀜")]
        [SerializeField, Min(0.01f)] private float travelSeconds = 0.65f;
        [Tooltip("이동 시간 중 '끌려 나옴' (천천히 움직이기 시작해 패널 끝이 화면에 조금 드러남) 의 비율")]
        [SerializeField, Range(0.05f, 0.8f)] private float pullTimeFraction = 0.45f;
        [Tooltip("끌려 나오는 동안 가는 거리 (출발 자리 → 최종 자리 거리의 비율). 화면 밖 여유보다 커야 패널 끝이 보임")]
        [SerializeField, Range(0.02f, 0.6f)] private float pullDistanceFraction = 0.12f;
        [Tooltip("최종 자리를 지나 가장 멀리 눌릴 때까지 (초). 지나는 순간의 속도를 이어받아 줄이며 멈춤")]
        [SerializeField, Min(0.01f)] private float overshootSeconds = 0.08f;
        [Tooltip("가장 멀리 눌린 자리에서 최종 자리로 돌아와 멈추기까지 (초). 멈춘 상태에서 살짝 속도가 붙었다가 감속하며 멎음")]
        [SerializeField, Min(0.01f)] private float settleSeconds = 0.16f;
        [Tooltip("출발 자리를 화면 끝에서 이만큼 더 바깥에 둠 (UI 단위)")]
        [SerializeField, Min(0f)] private float offscreenMargin = 16f;
        [Tooltip("지나칠 때 기계 등과 최소한 이만큼은 떨어짐 (UI 단위). 틈이 모자라면 지나치는 거리를 줄임")]
        [SerializeField, Min(0f)] private float clearanceMargin = 4f;
        [Tooltip("마지막 패널이 최종 자리에 닿은 뒤 조작 안내가 다 나타나기까지 (초)")]
        [SerializeField, Min(0.01f)] private float guideFadeSeconds = 0.2f;
        [Tooltip("레일 소리를 마지막 패널이 최종 자리에 닿기 이 시간 전부터 줄여 닿는 순간에 멈춤 (초)")]
        [SerializeField, Min(0.01f)] private float slideSoundFadeSeconds = 0.12f;
        [Tooltip("앞 도착음이 이 시간 안에 났으면 다음 도착음을 줄임 (초 · 겹쳐 커지지 않게)")]
        [SerializeField, Min(0f)] private float arrivalOverlapSeconds = 0.3f;
        [Tooltip("겹칠 때 다음 도착음 크기 배율")]
        [SerializeField, Range(0f, 1f)] private float arrivalOverlapVolume = 0.7f;

        public float FirstDelaySeconds => firstDelaySeconds;
        public float StaggerSeconds => staggerSeconds;
        public float TravelSeconds => travelSeconds;
        public float PullTimeFraction => pullTimeFraction;
        public float PullDistanceFraction => pullDistanceFraction;
        public float OvershootSeconds => overshootSeconds;
        public float SettleSeconds => settleSeconds;
        public float OffscreenMargin => offscreenMargin;
        public float ClearanceMargin => clearanceMargin;
        public float GuideFadeSeconds => guideFadeSeconds;
        public float SlideSoundFadeSeconds => slideSoundFadeSeconds;
        public float ArrivalOverlapSeconds => arrivalOverlapSeconds;
        public float ArrivalOverlapVolume => arrivalOverlapVolume;
    }
}

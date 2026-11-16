using UnityEngine;

namespace Overclock.Presentation
{
    // 런 실패 연출 (라인 셧다운 · 11/07) 의 값. 첫 조정값임 (밸런스 수치 아님 · Play 에서 느낌을 맞춤).
    // 시간은 scaled time(초) · 연출 속도 배율이 곱해짐 (빠름 = 반 · 즉시 = 움직임 없이 최종 모습).
    // 밋밋하면 흔들림을 늘리기 전에 차단 순간의 소리 · 소등 시간차와 부품이 늦게 멎는 시간차를 먼저 조정함
    [CreateAssetMenu(fileName = "RunFailSettings", menuName = "Overclock/Run Fail Settings")]
    public sealed class RunFailSettings : ScriptableObject
    {
        [Header("0. 마지막 뽑기를 기다림")]
        [Tooltip("마지막 뽑기의 조각 낙하 · 숫자 세기가 끝나기를 최대 이만큼 기다린 뒤 시작함")]
        [SerializeField, Min(0f)] private float maxSettleWait = 1.5f;

        [Header("A. 출력 불안정")]
        [SerializeField, Min(0.05f)] private float unstableSeconds = 0.4f;
        [Tooltip("기어가 힘겹게 도는 속도 (평소 대비 배율). 도중에 두 번 힘이 빠짐")]
        [SerializeField, Min(1f)] private float strainGearSpeed = 5f;
        [Tooltip("힘이 빠질 때 속도가 줄어드는 비율")]
        [SerializeField, Range(0f, 1f)] private float gearDipDepth = 0.6f;
        [Tooltip("바늘이 무출력 자리에서 살짝 뜨는 각도 (최고 출력까지 오르지 않음)")]
        [SerializeField, Min(0f)] private float needleLiftDegrees = 7f;
        [Tooltip("바늘 떨림 폭 (흔들림 끄기면 없음)")]
        [SerializeField, Min(0f)] private float needleJitterDegrees = 2f;
        [SerializeField, Min(0.1f)] private float needleJitterHz = 14f;
        [Tooltip("표시등이 깜빡일 때 밝기 (섬광 끄기면 깜빡이지 않음). 마지막 표시등은 이 값 · 앞 표시등은 절반만 어두워짐")]
        [SerializeField, Range(0f, 1f)] private float flickerBrightness = 0.35f;

        [Header("B. 동력 차단")]
        [SerializeField, Min(0.05f)] private float cutSeconds = 0.3f;
        [Tooltip("출력 창 테두리가 먼저 꺼지고, 표시등이 이 간격으로 차례로 꺼짐")]
        [SerializeField, Min(0f)] private float lightStagger = 0.07f;
        [SerializeField, Min(0.01f)] private float lightFadeSeconds = 0.1f;
        [Tooltip("꺼진 밝기 (원래 색 대비). 표시등 순서대로")]
        [SerializeField] private float[] lightOffBrightness = { 0.5f, 0.3f };
        [Tooltip("몸통이 내려앉는 거리 (px · 흔들림 끄기면 없음) · 시간")]
        [SerializeField, Min(0f)] private float sinkPixels = 4f;
        [SerializeField, Min(0.01f)] private float sinkSeconds = 0.12f;
        [Tooltip("잔압 증기 한 번 (차단 뒤 · 크기 = 작은 증기 대비 배율 · 0 이면 없음)")]
        [SerializeField, Min(0f)] private float leakDelay = 0.08f;
        [SerializeField, Min(0f)] private float leakScale = 0.6f;
        [SerializeField, Min(0.05f)] private float leakSeconds = 0.9f;

        [Header("C. 관성 정지")]
        [Tooltip("차단 뒤 기어가 멎기까지는 차단 + 관성 정지 시간 (감속하며 멎음)")]
        [SerializeField, Min(0.05f)] private float coastSeconds = 0.8f;
        [Tooltip("피스톤이 처지는 거리 (px) · 시작 (관성 정지 시작부터) · 자리 잡는 시간")]
        [SerializeField, Min(0f)] private float pistonSagPixels = 5f;
        [SerializeField, Min(0f)] private float pistonDelay = 0.1f;
        [SerializeField, Min(0.01f)] private float pistonSettleSeconds = 0.45f;
        [Tooltip("피스톤 덜컹 폭 (px · 흔들림 끄기면 없음)")]
        [SerializeField, Min(0f)] private float pistonRattlePixels = 3f;
        [Tooltip("바늘이 무출력 자리로 떨어지기 시작 (관성 정지 시작부터) · 떨어지는 시간")]
        [SerializeField, Min(0f)] private float needleDelay = 0.15f;
        [SerializeField, Min(0.01f)] private float needleDropSeconds = 0.35f;
        [Tooltip("바늘이 무출력 자리에 닿은 뒤 한 번 튀는 폭 (흔들림 끄기면 없음) · 시간")]
        [SerializeField, Min(0f)] private float needleBounceDegrees = 2.5f;
        [SerializeField, Min(0.01f)] private float needleBounceSeconds = 0.25f;
        [Tooltip("기계 몸체가 어두워지는 정도 (원래 색 대비 · 실루엣은 남게)")]
        [SerializeField, Range(0f, 1f)] private float bodyDim = 0.7f;
        [Tooltip("마지막 금속 걸림 (관성 정지 시작부터) · 기어가 뒤로 걸리는 각도 (흔들림 끄기면 없음) · 시간")]
        [SerializeField, Min(0f)] private float clankAt = 0.72f;
        [SerializeField, Min(0f)] private float clankGearDegrees = 4f;
        [SerializeField, Min(0.01f)] private float clankGearSeconds = 0.06f;

        [Header("D. 정적")]
        [Tooltip("멎은 기계를 보여 주는 시간 (그 뒤 결과 창)")]
        [SerializeField, Min(0f)] private float stillSeconds = 0.5f;

        public float MaxSettleWait => maxSettleWait;
        public float UnstableSeconds => unstableSeconds;
        public float StrainGearSpeed => strainGearSpeed;
        public float GearDipDepth => gearDipDepth;
        public float NeedleLiftDegrees => needleLiftDegrees;
        public float NeedleJitterDegrees => needleJitterDegrees;
        public float NeedleJitterHz => needleJitterHz;
        public float FlickerBrightness => flickerBrightness;
        public float CutSeconds => cutSeconds;
        public float LightStagger => lightStagger;
        public float LightFadeSeconds => lightFadeSeconds;
        public float SinkPixels => sinkPixels;
        public float SinkSeconds => sinkSeconds;
        public float LeakDelay => leakDelay;
        public float LeakScale => leakScale;
        public float LeakSeconds => leakSeconds;
        public float CoastSeconds => coastSeconds;
        public float PistonSagPixels => pistonSagPixels;
        public float PistonDelay => pistonDelay;
        public float PistonSettleSeconds => pistonSettleSeconds;
        public float PistonRattlePixels => pistonRattlePixels;
        public float NeedleDelay => needleDelay;
        public float NeedleDropSeconds => needleDropSeconds;
        public float NeedleBounceDegrees => needleBounceDegrees;
        public float NeedleBounceSeconds => needleBounceSeconds;
        public float BodyDim => bodyDim;
        public float ClankAt => clankAt;
        public float ClankGearDegrees => clankGearDegrees;
        public float ClankGearSeconds => clankGearSeconds;
        public float StillSeconds => stillSeconds;

        public float GetLightOffBrightness(int stage)
        {
            return lightOffBrightness != null && stage < lightOffBrightness.Length ? lightOffBrightness[stage] : 0.4f;
        }
    }
}

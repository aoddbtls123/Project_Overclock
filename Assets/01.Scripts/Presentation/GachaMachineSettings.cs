using System;
using Overclock.Core;
using UnityEngine;

namespace Overclock.Presentation
{
    // 런 화면 가운데 기계(기어 · 피스톤 · 바늘 · 증기 · 레버)의 움직임 값. 시간(초)은 RevealTimings 가 등급별로 정하고,
    // 기계는 그 시간을 늘리지 않음. 각도는 Z 회전(도) · 거리는 px.
    // 타격감 (11/04): 움직임은 끝에서 감속하지 않고 가속하며 딱 멈춤 (InCubic). 멈추는 순간 기계 전체가 덜컹 · 소리
    [CreateAssetMenu(fileName = "GachaMachineSettings", menuName = "Overclock/Gacha Machine Settings")]
    public sealed class GachaMachineSettings : ScriptableObject
    {
        [Header("기어")]
        [Tooltip("쉬는 동안 기준 기어 회전 속도 (도/초)")]
        [SerializeField] private float idleGearSpeed = 6f;
        [Tooltip("SR 기다림이 가장 뜨거울 때 기준 기어 회전 속도 (도/초)")]
        [SerializeField] private float heatGearSpeed = 360f;
        [Tooltip("공개 순간 한 칸 끊어서 도는 각도 (도)")]
        [SerializeField] private float ratchetAngle = 30f;
        [Tooltip("한 칸 돌기 전에 반대로 살짝 당기는 각도 (도)")]
        [SerializeField, Min(0f)] private float ratchetWindupAngle = 3f;
        [SerializeField, Min(0.01f)] private float ratchetWindupSeconds = 0.03f;
        [Tooltip("한 칸 도는 시간 (가속하며 딱 멈춤)")]
        [SerializeField, Min(0.01f)] private float ratchetSeconds = 0.05f;

        [Header("피스톤")]
        [Tooltip("피스톤이 내려가는 거리 (px)")]
        [SerializeField] private float pistonTravel = 40f;
        [Tooltip("R · SR 공개 때 내려가는 시간 (가속하며 딱 멈춤)")]
        [SerializeField, Min(0.01f)] private float pistonOutSeconds = 0.08f;
        [Tooltip("내려간 채 멈춰 있는 시간")]
        [SerializeField, Min(0f)] private float pistonHoldSeconds = 0.04f;
        [Tooltip("제자리로 돌아오는 시간")]
        [SerializeField, Min(0.01f)] private float pistonReturnSeconds = 0.15f;
        [Tooltip("SR 기다림 전체 동안 피스톤이 오르내리는 횟수")]
        [SerializeField, Min(1f)] private float heatPistonCycles = 10f;

        [Header("바늘 계기")]
        [SerializeField] private float needleRestAngle = 60f;
        [SerializeField] private float needleMaxAngle = -60f;
        [Tooltip("SR 공개 뒤 바늘이 쉬는 자리로 돌아오는 시간")]
        [SerializeField, Min(0.01f)] private float needleReturnSeconds = 0.6f;

        [Header("증기")]
        [SerializeField] private Color steamColor = new Color(0.85f, 0.88f, 0.9f, 0.7f);
        [SerializeField, Min(0f)] private float steamStartScale = 0.4f;
        [SerializeField, Min(0f)] private float smallSteamScale = 1.2f;
        [SerializeField, Min(0f)] private float bigSteamScale = 2.4f;
        [Tooltip("증기가 퍼지며 올라가는 거리 (px)")]
        [SerializeField] private float steamRise = 60f;
        [Tooltip("같은 구멍에서 동시에 나오는 증기끼리 옆으로 벌리는 간격 (px). 두 번째 +간격 · 세 번째 −간격 · 네 번째 +2×간격 …")]
        [SerializeField, Min(0f)] private float steamSpread = 40f;
        [SerializeField, Min(0.01f)] private float steamSeconds = 0.6f;
        [Tooltip("SR 공개 때 동시에 뿜는 증기 수")]
        [SerializeField, Min(1)] private int superRareSteamPuffs = 4;
        [Tooltip("돌파 때 이어서 뿜는 증기 수")]
        [SerializeField, Min(1)] private int breakthroughSteamPuffs = 5;
        [Tooltip("돌파 증기 사이 간격")]
        [SerializeField, Min(0f)] private float breakthroughSteamInterval = 0.15f;

        [Header("SR 기다림 증기 (11/05) — 위 증기 구멍에서 작은 증기, 공개가 가까울수록 잦게")]
        [Tooltip("SR 기다림 진행률(0~1)이 이 값을 넘는 순간마다 작은 증기 하나 (오름차순). 진행률을 시계로 써서 스킵 · 일시정지 · 연출 속도와 맞음. 증기 풀(10)을 SR 공개 · 돌파와 같이 쓰므로 6개 이하로")]
        [SerializeField] private float[] heatSteamPoints = { 0.3f, 0.5f, 0.64f, 0.76f, 0.86f, 0.94f };

        [Header("레버")]
        [Tooltip("당겼을 때 손잡이 각도 (도)")]
        [SerializeField] private float leverPulledAngle = -50f;
        [Tooltip("내려가는 시간 (가속하며 바닥에서 딱 멈춤 · 그 순간 레버 소리 · 덜컹)")]
        [SerializeField, Min(0.01f)] private float leverDownSeconds = 0.06f;
        [Tooltip("바닥에서 버티는 시간")]
        [SerializeField, Min(0f)] private float leverHoldSeconds = 0.08f;
        [Tooltip("올라오는 시간 (감속하며 제자리)")]
        [SerializeField, Min(0.01f)] private float leverUpSeconds = 0.25f;

        [Header("덜컹 (멈추는 순간 기계 전체 · 아래 → 위 1/3 → 제자리, 한 프레임씩) — 설정의 흔들림 끄기를 따른다")]
        [SerializeField, Min(0f)] private float leverThudPixels = 3f;
        [SerializeField, Min(0f)] private float ratchetThudPixels = 1f;
        [Tooltip("수확물이 트레이에 앉을 때")]
        [SerializeField, Min(0f)] private float landingThudPixels = 1f;

        [Header("출력 창 테두리 (11/05) — 공개 순간 등급 색으로 켜고 다음 레버까지 둔다 · 0 = 그 등급은 테두리 없음")]
        [SerializeField, Min(0f)] private float frameWidthN = 2f;
        [SerializeField, Min(0f)] private float frameWidthR = 3f;
        [SerializeField, Min(0f)] private float frameWidthSR = 4f;

        [Header("증기 그림 (11/16) — 그림은 오른쪽 배관에서 나오는 모양. 피벗 = 배관 입구 자리 (그림 밖이어도 된다) · 비어 있으면 증기 이미지의 원래 그림")]
        [Tooltip("그림 1 픽셀을 몇 단위로 그릴지 (기계 그림과 같은 배율)")]
        [SerializeField, Min(0.01f)] private float steamPixelScale = 4f;
        [Tooltip("R 공개 · SR 기다림의 작은 증기")]
        [SerializeField] private SteamShape smallSteamShape = new SteamShape();
        [Tooltip("SR 공개 · 돌파의 큰 증기")]
        [SerializeField] private SteamShape bigSteamShape = new SteamShape();
        [Tooltip("돌파 뒤 배관 근처에 잠깐 남는 증기 · 런 실패 잔압. 구멍마다 차례로 모두 냄")]
        [SerializeField] private SteamShape[] residualSteamShapes = Array.Empty<SteamShape>();
        [SerializeField, Min(0.01f)] private float residualSteamSeconds = 1.4f;
        [SerializeField] private float residualSteamRise = 16f;
        [Tooltip("잔류 증기 덩어리 사이 시간차")]
        [SerializeField, Min(0f)] private float residualSteamStagger = 0.12f;

        [Header("경고등 (11/16) — 눈부시지 않게 천천히. 값 = 빛 이미지 불투명도 (0~1)")]
        [SerializeField, Range(0f, 1f)] private float idleLampMin = 0.08f;
        [SerializeField, Range(0f, 1f)] private float idleLampMax = 0.28f;
        [Tooltip("평소 아래 등이 한 번 숨 쉬는 시간")]
        [SerializeField, Min(0.1f)] private float idleLampPeriod = 2.6f;
        [SerializeField, Range(0f, 1f)] private float overclockLampMin = 0.5f;
        [SerializeField, Range(0f, 1f)] private float overclockLampMax = 0.9f;
        [Tooltip("오버클럭 가동 중 아래 등이 한 번 숨 쉬는 시간")]
        [SerializeField, Min(0.1f)] private float overclockLampPeriod = 1.2f;
        [Tooltip("레버가 바닥에 닿을 때 왼쪽 아래 등")]
        [SerializeField, Range(0f, 1f)] private float leverLampFlash = 0.8f;
        [SerializeField, Min(0.01f)] private float leverLampSeconds = 0.4f;
        [Tooltip("공개 순간 오른쪽 아래 등 (N · R · SR)")]
        [SerializeField, Range(0f, 1f)] private float revealLampFlashN = 0.35f;
        [SerializeField, Range(0f, 1f)] private float revealLampFlashR = 0.65f;
        [SerializeField, Range(0f, 1f)] private float revealLampFlashSR = 1f;
        [SerializeField, Min(0.01f)] private float revealLampSeconds = 0.6f;
        [Tooltip("SR 기다림이 가장 뜨거울 때 위 등 밝기 (진행률에 비례해 오름)")]
        [SerializeField, Range(0f, 1f)] private float heatLampMax = 0.8f;
        [Tooltip("SR 기다림 위 등의 일렁임 폭 (밝기에 곱함)")]
        [SerializeField, Range(0f, 1f)] private float heatLampPulse = 0.2f;
        [Tooltip("일렁임 빠르기 — 처음 · 공개 직전 (회/초). 눈부시지 않게 3 이하")]
        [SerializeField, Min(0f)] private float heatLampPulseStart = 1f;
        [SerializeField, Min(0f)] private float heatLampPulseEnd = 2.5f;
        [Tooltip("SR 공개 때 위 등이 켜졌다 잦아드는 시간")]
        [SerializeField, Min(0.01f)] private float superRareLampSeconds = 0.9f;
        [Tooltip("돌파 때 세 등")]
        [SerializeField, Range(0f, 1f)] private float breakthroughLampFlash = 0.75f;
        [SerializeField, Min(0.01f)] private float breakthroughLampSeconds = 0.8f;

        public float IdleGearSpeed => idleGearSpeed;
        public float HeatGearSpeed => heatGearSpeed;
        public float RatchetAngle => ratchetAngle;
        public float RatchetWindupAngle => ratchetWindupAngle;
        public float RatchetWindupSeconds => ratchetWindupSeconds;
        public float RatchetSeconds => ratchetSeconds;
        public float PistonTravel => pistonTravel;
        public float PistonOutSeconds => pistonOutSeconds;
        public float PistonHoldSeconds => pistonHoldSeconds;
        public float PistonReturnSeconds => pistonReturnSeconds;
        public float HeatPistonCycles => heatPistonCycles;
        public float NeedleRestAngle => needleRestAngle;
        public float NeedleMaxAngle => needleMaxAngle;
        public float NeedleReturnSeconds => needleReturnSeconds;
        public Color SteamColor => steamColor;
        public float SteamStartScale => steamStartScale;
        public float SmallSteamScale => smallSteamScale;
        public float BigSteamScale => bigSteamScale;
        public float SteamRise => steamRise;
        public float SteamSpread => steamSpread;
        public float SteamSeconds => steamSeconds;
        public int SuperRareSteamPuffs => superRareSteamPuffs;
        public int BreakthroughSteamPuffs => breakthroughSteamPuffs;
        public float BreakthroughSteamInterval => breakthroughSteamInterval;
        public float LeverPulledAngle => leverPulledAngle;
        public float LeverDownSeconds => leverDownSeconds;
        public float LeverHoldSeconds => leverHoldSeconds;
        public float LeverUpSeconds => leverUpSeconds;
        public float LeverThudPixels => leverThudPixels;
        public float RatchetThudPixels => ratchetThudPixels;
        public float LandingThudPixels => landingThudPixels;
        public float SteamPixelScale => steamPixelScale;
        public SteamShape SmallSteamShape => smallSteamShape;
        public SteamShape BigSteamShape => bigSteamShape;
        public int ResidualSteamShapeCount => residualSteamShapes == null ? 0 : residualSteamShapes.Length;
        public float ResidualSteamSeconds => residualSteamSeconds;
        public float ResidualSteamRise => residualSteamRise;
        public float ResidualSteamStagger => residualSteamStagger;
        public float IdleLampMin => idleLampMin;
        public float IdleLampMax => idleLampMax;
        public float IdleLampPeriod => idleLampPeriod;
        public float OverclockLampMin => overclockLampMin;
        public float OverclockLampMax => overclockLampMax;
        public float OverclockLampPeriod => overclockLampPeriod;
        public float LeverLampFlash => leverLampFlash;
        public float LeverLampSeconds => leverLampSeconds;
        public float RevealLampSeconds => revealLampSeconds;
        public float HeatLampMax => heatLampMax;
        public float HeatLampPulse => heatLampPulse;
        public float HeatLampPulseStart => heatLampPulseStart;
        public float HeatLampPulseEnd => heatLampPulseEnd;
        public float SuperRareLampSeconds => superRareLampSeconds;
        public float BreakthroughLampFlash => breakthroughLampFlash;
        public float BreakthroughLampSeconds => breakthroughLampSeconds;
        public int HeatSteamPointCount => heatSteamPoints == null ? 0 : heatSteamPoints.Length;

        public float GetHeatSteamPoint(int index)
        {
            return heatSteamPoints[index];
        }

        public SteamShape GetResidualSteamShape(int index)
        {
            return residualSteamShapes[index];
        }

        public float GetRevealLampFlash(Grade grade)
        {
            switch (grade)
            {
                case Grade.R:
                    return revealLampFlashR;
                case Grade.SR:
                    return revealLampFlashSR;
                default:
                    return revealLampFlashN;
            }
        }

        public float GetFrameWidth(Grade grade)
        {
            switch (grade)
            {
                case Grade.R:
                    return frameWidthR;
                case Grade.SR:
                    return frameWidthSR;
                default:
                    return frameWidthN;
            }
        }
    }

    /// <summary>증기 그림 한 종류. 크기 = 그림 픽셀 × 배율 × 배수 (시작 → 끝).</summary>
    [Serializable]
    public sealed class SteamShape
    {
        [SerializeField] private Sprite sprite;
        [Tooltip("오른쪽 배관 입구 자리 (그림 기준 0~1 · 그림 밖이면 범위를 넘음)")]
        [SerializeField] private Vector2 pivot = new Vector2(0.5f, 0.5f);
        [SerializeField, Min(0f)] private float startScale = 0.5f;
        [SerializeField, Min(0f)] private float endScale = 1f;

        public Sprite Sprite => sprite;
        public Vector2 Pivot => pivot;
        public float StartScale => startScale;
        public float EndScale => endScale;
        public bool IsValid => sprite != null;
    }
}

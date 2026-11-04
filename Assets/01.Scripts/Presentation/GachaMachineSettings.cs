using UnityEngine;

namespace Overclock.Presentation
{
    // 런 화면 가운데 기계(기어 · 피스톤 · 바늘 · 증기 · 레버)의 움직임 값. 시간(초)은 RevealTimings 가 등급별로 정하고,
    // 기계는 그 시간을 늘리지 않는다. 각도는 Z 회전(도) · 거리는 px.
    // 타격감 (11/04): 움직임은 끝에서 감속하지 않고 가속하며 딱 멈춘다 (InCubic). 멈추는 순간 기계 전체가 덜컹 · 소리
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
    }
}

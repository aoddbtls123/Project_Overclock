using UnityEngine;

namespace Overclock.Presentation
{
    // 일반모드 R8 클리어 마무리 연출의 값 (11/06 · v4 = 압력을 모으고 → 버티고 → 분출 · 반동 → 잦아듦).
    // 첫 조정값이다 (밸런스 수치 아님 · Play 에서 느낌을 맞춘다). 시간은 scaled time(초) · 연출 속도 배율이 곱해진다 (즉시 = 연출 없음)
    [CreateAssetMenu(fileName = "RunClearSettings", menuName = "Overclock/Run Clear Settings")]
    public sealed class RunClearSettings : ScriptableObject
    {
        [Header("1. 기 모으기")]
        [SerializeField, Min(0.1f)] private float chargeSeconds = 1.4f;
        [Tooltip("끝 무렵 바늘 떨림 최대 폭 (도). 진행률³ 으로 커진다 — 처음엔 거의 없고 끝에 가까울수록")]
        [SerializeField, Min(0f)] private float needleJitterDegrees = 4f;
        [SerializeField, Min(0.1f)] private float needleJitterHz = 18f;
        [Tooltip("충전음 크기: 시작 → 끝 (압력이 오르는 느낌)")]
        [SerializeField, Range(0f, 1f)] private float chargeVolumeStart = 0.55f;
        [SerializeField, Range(0f, 1f)] private float chargeVolumeEnd = 1f;
        [Tooltip("기계 불빛 (작은 표시등 → 계기판 → 출력 창 순). 따뜻한 황색에서 금색으로")]
        [SerializeField] private Color warmLight = new Color(1f, 0.72f, 0.3f, 1f);
        [SerializeField] private Color goldLight = new Color(1f, 0.85f, 0.35f, 1f);
        [Tooltip("단계마다 불이 들어오는 정도 (원래 색 대비 빛 색 비율). 표시등 · 계기판 · 출력 창 순")]
        [SerializeField] private float[] lightStrength = { 1f, 0.55f, 0.3f };

        [Header("2. 수축 · 버팀")]
        [SerializeField, Min(0.01f)] private float holdSeconds = 0.18f;
        [Tooltip("몸통이 내려앉는 거리 (px)")]
        [SerializeField, Min(0f)] private float sinkPixels = 8f;
        [Tooltip("세로 압축 (가로는 그대로)")]
        [SerializeField, Range(0.8f, 1f)] private float squashY = 0.97f;
        [Tooltip("버티는 동안 충전음 크기 (눌리는 느낌)")]
        [SerializeField, Range(0f, 1f)] private float holdChargeVolume = 0.45f;

        [Header("3. 분출 · 반동")]
        [Tooltip("몸통이 원래 자리보다 위로 튀는 거리 (px)")]
        [SerializeField, Min(0f)] private float bouncePixels = 13f;
        [SerializeField, Range(1f, 1.1f)] private float popY = 1.025f;
        [Tooltip("몸통이 자리 잡는 시간 (부품보다 먼저)")]
        [SerializeField, Min(0.01f)] private float bodySettleSeconds = 0.32f;
        [SerializeField, Min(0f)] private float gearDelay = 0.03f;
        [SerializeField, Min(0f)] private float pistonDelay = 0.06f;
        [SerializeField, Min(0f)] private float needleDelay = 0.09f;
        [Tooltip("바늘 · 작은 계기 잔떨림 폭 (도) · 시간")]
        [SerializeField, Min(0f)] private float needleWobbleDegrees = 3f;
        [SerializeField, Min(0.01f)] private float needleWobbleSeconds = 0.45f;
        [Tooltip("주 타격음 뒤 증기 소리까지 (겹치지 않게)")]
        [SerializeField, Min(0f)] private float steamSoundDelay = 0.22f;

        [Header("4. 잦아듦 · 완료")]
        [Tooltip("분출 뒤 결과 창까지 (마지막 증기가 끝나는 시각보다 짧으면 그 시각까지 늘어난다)")]
        [SerializeField, Min(0.1f)] private float afterBurstSeconds = 1.6f;
        [SerializeField, Min(0.05f)] private float steamInterval = 0.33f;
        [Tooltip("증기마다 크기 (분출 → 잦아듦). 개수 = 증기 횟수")]
        [SerializeField] private float[] steamScales = { 1f, 0.7f, 0.45f };
        [Tooltip("분출 뒤 글자가 나오기까지 (기계 반동이 먼저 읽히게)")]
        [SerializeField, Min(0f)] private float bannerDelay = 0.1f;
        [SerializeField, Min(0.01f)] private float bannerPopSeconds = 0.3f;
        [SerializeField] private string bannerText = "전 라인 할당량 달성!";

        public float ChargeSeconds => chargeSeconds;
        public float NeedleJitterDegrees => needleJitterDegrees;
        public float NeedleJitterHz => needleJitterHz;
        public float ChargeVolumeStart => chargeVolumeStart;
        public float ChargeVolumeEnd => chargeVolumeEnd;
        public Color WarmLight => warmLight;
        public Color GoldLight => goldLight;
        public float HoldSeconds => holdSeconds;
        public float SinkPixels => sinkPixels;
        public float SquashY => squashY;
        public float HoldChargeVolume => holdChargeVolume;
        public float BouncePixels => bouncePixels;
        public float PopY => popY;
        public float BodySettleSeconds => bodySettleSeconds;
        public float GearDelay => gearDelay;
        public float PistonDelay => pistonDelay;
        public float NeedleDelay => needleDelay;
        public float NeedleWobbleDegrees => needleWobbleDegrees;
        public float NeedleWobbleSeconds => needleWobbleSeconds;
        public float SteamSoundDelay => steamSoundDelay;
        public float AfterBurstSeconds => afterBurstSeconds;
        public float SteamInterval => steamInterval;
        public int SteamBursts => steamScales != null && steamScales.Length > 0 ? steamScales.Length : 1;
        public float BannerDelay => bannerDelay;
        public float BannerPopSeconds => bannerPopSeconds;
        public string BannerText => bannerText;

        public float GetLightStrength(int stage)
        {
            return lightStrength != null && stage < lightStrength.Length ? lightStrength[stage] : 1f;
        }

        public float GetSteamScale(int burst)
        {
            return steamScales != null && burst < steamScales.Length ? steamScales[burst] : 1f;
        }
    }
}

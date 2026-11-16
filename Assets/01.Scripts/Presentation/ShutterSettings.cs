using Overclock.Core;
using UnityEngine;

namespace Overclock.Presentation
{
    // 출력 창 셔터와 기다림 박자의 겉모습 값 (기계연출 구상 §14-5). 시간은 scaled time(초) · 거리는 px.
    // 모든 시간은 뜸들이기(N 0.3 · R 0.8 · SR 1.5초) 안에서 쓰고, 연출 속도 배율(빠름 0.5)을 같이 받는다. 뜸들이기 시간은 늘리지 않는다
    [CreateAssetMenu(fileName = "ShutterSettings", menuName = "Overclock/Shutter Settings")]
    public sealed class ShutterSettings : ScriptableObject
    {
        [Header("셔터 (철 · 커 · 착)")]
        [Tooltip("뽑기 시작부터 셔터가 내려오기 시작할 때까지 (레버가 바닥에 닿는 시간)")]
        [SerializeField, Min(0f)] private float closeDelay = 0.05f;
        [Tooltip("가속하며 내려와 쾅 닫히는 시간. 닫히는 순간 기계 덜컹 · 닫힘 소리")]
        [SerializeField, Min(0.01f)] private float closeSeconds = 0.1f;
        [Tooltip("닫힐 때 기계 덜컹 (px). 흔들림 끄기면 없음")]
        [SerializeField, Min(0f)] private float closeThudPixels = 2f;
        [Tooltip("N · R 공개 순간 열리는 시간")]
        [SerializeField, Min(0.01f)] private float openSeconds = 0.08f;
        [Tooltip("SR 공개 순간 튕겨 올라가는 시간")]
        [SerializeField, Min(0.01f)] private float superRareOpenSeconds = 0.06f;
        [Tooltip("SR 이 위에 부딪혀 다시 내려왔다 올라가는 시간")]
        [SerializeField, Min(0.01f)] private float superRareBounceSeconds = 0.16f;
        [Tooltip("SR 튕김 때 다시 내려오는 정도 (창 높이 대비)")]
        [SerializeField, Range(0f, 0.5f)] private float superRareBounceAmount = 0.15f;

        [Header("철판 (11/16) — 떨어지는 동안 철판 사이가 벌어져 있다가 닿으면 아래부터 차례로 포개진다 · 열 때는 위부터 벌어진다")]
        [Tooltip("철판 사이가 벌어지는 거리 (px · 기계 그림 1 픽셀 = 4)")]
        [SerializeField, Min(0f)] private float slatGap = 4f;
        [Tooltip("닫힌 뒤 철판이 아래부터 차례로 포개지는 시간 (닫히는 시간과 같은 배율을 받는다)")]
        [SerializeField, Min(0.01f)] private float slatSettleSeconds = 0.12f;
        [Tooltip("열 때 철판 사이가 다 벌어지는 시점 (여는 시간 대비)")]
        [SerializeField, Range(0.05f, 1f)] private float slatOpenLead = 0.5f;

        [Header("기다리는 동안 (R · SR)")]
        [Tooltip("R 래칫 틱 간격")]
        [SerializeField, Min(0.01f)] private float rareTickInterval = 0.2f;
        [Tooltip("SR 래칫 틱 간격 — 처음")]
        [SerializeField, Min(0.01f)] private float superRareTickIntervalStart = 0.2f;
        [Tooltip("SR 래칫 틱 간격 — 공개 직전 (점점 빨라짐)")]
        [SerializeField, Min(0.01f)] private float superRareTickIntervalEnd = 0.05f;
        [Tooltip("틱 한 번에 래칫이 도는 각도")]
        [SerializeField] private float tickAngle = 4f;
        [Tooltip("틱 한 번이 가속하며 딱 멈추는 시간")]
        [SerializeField, Min(0.01f)] private float tickSeconds = 0.03f;
        [Tooltip("SR 셔터 떨림 최대 (px · 공개 직전). 닫혀 있을 때만")]
        [SerializeField, Min(0f)] private float superRareJitterPixels = 2f;
        [Tooltip("떨림 빠르기")]
        [SerializeField, Min(0f)] private float jitterFrequency = 40f;

        [Header("틈새 증기 (SR 틱마다)")]
        [Tooltip("셔터 아래 틈의 가로 자리를 차례로 돌려 쓴다 (창 가운데 기준 px · 난수 없음)")]
        [SerializeField] private float[] gapPuffOffsetsX = { -120f, 90f, -40f, 150f, 20f, -160f, 60f };
        [SerializeField, Min(0.01f)] private float gapPuffSeconds = 0.35f;
        [SerializeField, Min(0f)] private float gapPuffRise = 25f;
        [SerializeField, Min(0.01f)] private float gapPuffStartScale = 0.2f;
        [SerializeField, Min(0.01f)] private float gapPuffEndScale = 0.5f;
        [SerializeField] private Color gapPuffColor = new Color(0.85f, 0.88f, 0.9f, 0.6f);

        public float CloseDelay => closeDelay;
        public float CloseSeconds => closeSeconds;
        public float CloseThudPixels => closeThudPixels;
        public float OpenSeconds => openSeconds;
        public float SuperRareOpenSeconds => superRareOpenSeconds;
        public float SuperRareBounceSeconds => superRareBounceSeconds;
        public float SuperRareBounceAmount => superRareBounceAmount;
        public float SlatGap => slatGap;
        public float SlatSettleSeconds => slatSettleSeconds;
        public float SlatOpenLead => slatOpenLead;
        public float TickAngle => tickAngle;
        public float TickSeconds => tickSeconds;
        public float SuperRareJitterPixels => superRareJitterPixels;
        public float JitterFrequency => jitterFrequency;
        public float GapPuffSeconds => gapPuffSeconds;
        public float GapPuffRise => gapPuffRise;
        public float GapPuffStartScale => gapPuffStartScale;
        public float GapPuffEndScale => gapPuffEndScale;
        public Color GapPuffColor => gapPuffColor;

        /// <summary>틱 간격 (연출 속도 배율 전). N 은 틱 없음 (0). SR 은 진행률(0~1)에 따라 처음 → 끝으로 줄어든다.</summary>
        public float GetTickInterval(Grade grade, float progress)
        {
            switch (grade)
            {
                case Grade.SR:
                    return Mathf.Lerp(superRareTickIntervalStart, superRareTickIntervalEnd, Mathf.Clamp01(progress));
                case Grade.R:
                    return rareTickInterval;
                default:
                    return 0f;
            }
        }

        public float GetGapPuffOffsetX(int index)
        {
            return gapPuffOffsetsX == null || gapPuffOffsetsX.Length == 0 ? 0f : gapPuffOffsetsX[index % gapPuffOffsetsX.Length];
        }
    }
}

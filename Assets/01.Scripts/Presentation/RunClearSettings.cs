using UnityEngine;

namespace Overclock.Presentation
{
    // 일반모드 R8 클리어 마무리 연출의 값 (11/06). 시간은 scaled time(초) · 연출 속도 배율이 곱해진다 (즉시 = 연출 없이 바로 결과 창)
    [CreateAssetMenu(fileName = "RunClearSettings", menuName = "Overclock/Run Clear Settings")]
    public sealed class RunClearSettings : ScriptableObject
    {
        [Tooltip("결과 창을 띄우기 전까지 마무리 연출 시간 (마지막 증기가 끝나는 시각보다 짧으면 그 시각까지 늘어난다)")]
        [SerializeField, Min(0.1f)] private float totalSeconds = 2.0f;
        [Tooltip("돌파 증기를 몇 번 뿜는가 (처음 한 번 + 간격마다)")]
        [SerializeField, Min(1)] private int steamBursts = 3;
        [SerializeField, Min(0.05f)] private float steamInterval = 0.35f;
        [Tooltip("글자가 튀어나오는 시간 (작게 → 살짝 넘쳤다 → 제 크기)")]
        [SerializeField, Min(0.01f)] private float bannerPopSeconds = 0.3f;
        [SerializeField] private string bannerText = "전 라인 할당량 달성!";

        public float TotalSeconds => totalSeconds;
        public int SteamBursts => steamBursts;
        public float SteamInterval => steamInterval;
        public float BannerPopSeconds => bannerPopSeconds;
        public string BannerText => bannerText;
    }
}

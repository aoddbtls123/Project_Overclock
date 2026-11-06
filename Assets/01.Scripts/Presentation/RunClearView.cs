using System;
using System.Collections;
using Overclock.Core;
using TMPro;
using UnityEngine;

namespace Overclock.Presentation
{
    // 일반모드 R8 클리어 마무리 연출 (11/06): 큰 글자 + 기계 증기 여러 번 + 출력 창 테두리 · 계수기 금색 → 끝나면 결과 창.
    // 약속은 DrawRevealView 와 같다: Play 한 번에 onFinished 정확히 한 번 · Skip = 바로 끝내고 콜백 · Cancel = 콜백 없음 ·
    // 시간은 Time.deltaTime 만 (일시정지면 멈춤). 결과 · 기록은 이미 저장돼 있다 — 이 창은 결과 창을 늦출 뿐이다
    public sealed class RunClearView : MonoBehaviour
    {
        [SerializeField] private RunClearSettings settings;
        [SerializeField] private TMP_Text bannerText;
        [Tooltip("비어 있으면 증기 · 테두리 없이")]
        [SerializeField] private GachaMachineView machine;
        [Tooltip("비어 있으면 계수기 금색 없이")]
        [SerializeField] private MechanicalCounterView counter;

        private Coroutine routine;
        private bool skipRequested;

        public bool IsPlaying { get; private set; }

        private void Awake()
        {
            HideBanner();
        }

        private void OnDisable()
        {
            Cancel();
        }

        /// <summary>마무리 연출을 시작한다. timeMultiplier = 연출 속도 배율 (0 이하 = 연출 없이 바로 onFinished).</summary>
        public void Play(float timeMultiplier, Action onFinished)
        {
            if (onFinished == null)
            {
                throw new ArgumentNullException(nameof(onFinished));
            }

            if (IsPlaying)
            {
                throw new InvalidOperationException("이미 마무리 연출 중입니다.");
            }

            if (settings == null || timeMultiplier <= 0f)
            {
                onFinished();
                return;
            }

            IsPlaying = true;
            skipRequested = false;
            Coroutine started = StartCoroutine(PlayRoutine(timeMultiplier, onFinished));
            if (IsPlaying && routine == null)
            {
                routine = started;
            }
        }

        /// <summary>남은 연출을 건너뛰고 결과 창으로. 연출 중이 아니면 무시한다.</summary>
        public void Skip()
        {
            if (IsPlaying)
            {
                skipRequested = true;
            }
        }

        /// <summary>멈추고 콜백을 부르지 않는다 (런을 버릴 때).</summary>
        public void Cancel()
        {
            if (routine != null)
            {
                StopCoroutine(routine);
                routine = null;
            }

            IsPlaying = false;
            HideBanner();
            StopMachineAndCounter();
        }

        // 스킵 · 취소 때 남은 증기 예약 · 재생을 지우고 계수기 잠금을 푼다. 출력 창 금색 테두리는 남긴다 (결과 창 뒤 최종 모습)
        private void StopMachineAndCounter()
        {
            if (machine != null)
            {
                machine.StopReactions();
            }

            if (counter != null)
            {
                counter.UnlockFlash();
            }
        }

        private IEnumerator PlayRoutine(float multiplier, Action onFinished)
        {
            float interval = settings.SteamInterval * multiplier;
            // 마지막 증기가 끝나기 전에 결과 창으로 넘어가지 않는다 (빠름도 같은 비율 · 교차검토 #40)
            float steamEnd = machine != null ? (settings.SteamBursts - 1) * interval + machine.GetBreakthroughSeconds(multiplier) : 0f;
            float total = Mathf.Max(settings.TotalSeconds * multiplier, steamEnd);
            float pop = settings.BannerPopSeconds * multiplier;
            int burstsDone = 0;

            if (machine != null)
            {
                machine.ShowGradeFrame(Grade.SR);
            }

            ShowBanner(0f);
            float elapsed = 0f;
            while (true)
            {
                // 증기는 시간이 흐른 만큼 · 한 프레임에 하나까지 (긴 프레임에 겹쳐 터지지 않게)
                if (burstsDone < settings.SteamBursts && elapsed >= burstsDone * interval)
                {
                    if (machine != null)
                    {
                        machine.PlayBreakthrough(multiplier);
                    }

                    // 생산량 바퀴를 기다리지 않고 바로 · 연출 동안 생산량 번쩍임이 금색을 덮지 못하게 잠근다
                    if (counter != null)
                    {
                        counter.FlashNow(Grade.SR, true);
                    }

                    burstsDone++;
                }

                SetBannerScale(Mathf.Clamp01(elapsed / pop));
                if (elapsed >= total || skipRequested)
                {
                    break;
                }

                yield return null;
                elapsed += Time.deltaTime;
            }

            routine = null;
            IsPlaying = false;
            HideBanner();
            if (skipRequested)
            {
                // 스킵이면 남은 증기도 지운다. 끝까지 갔으면 증기는 이미 끝났다 (total ≥ 마지막 증기 끝)
                StopMachineAndCounter();
            }
            else if (counter != null)
            {
                counter.UnlockFlash();
            }

            onFinished();
        }

        private void ShowBanner(float popProgress)
        {
            if (bannerText == null)
            {
                return;
            }

            bannerText.text = settings.BannerText;
            bannerText.color = GradeStyle.GetColor(Grade.SR);
            bannerText.gameObject.SetActive(true);
            SetBannerScale(popProgress);
        }

        private void SetBannerScale(float popProgress)
        {
            if (bannerText == null)
            {
                return;
            }

            // 0 → 1 로 가며 1 을 살짝 넘었다 돌아온다
            const float overshoot = 1.70158f;
            float u = popProgress - 1f;
            float eased = 1f + (overshoot + 1f) * u * u * u + overshoot * u * u;
            float scale = 0.4f + 0.6f * eased;
            bannerText.rectTransform.localScale = new Vector3(scale, scale, 1f);
        }

        private void HideBanner()
        {
            if (bannerText == null)
            {
                return;
            }

            bannerText.rectTransform.localScale = Vector3.one;
            bannerText.gameObject.SetActive(false);
        }
    }
}

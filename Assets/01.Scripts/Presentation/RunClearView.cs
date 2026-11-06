using System;
using System.Collections;
using Overclock.Core;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Overclock.Presentation
{
    // 일반모드 R8 클리어 마무리 연출 (11/06 · v4): 묵직한 철제 기계가 압력을 모으고 → 잠깐 버틴 뒤 → 증기를 분출하며
    // 몸통과 부품이 시간차로 덜컹거리고 → 잦아들며 금색 불빛이 남는다.
    //   1. 기 모으기   충전음 (점점 크게) · 불빛 표시등 → 계기판 → 출력 창 (황색 → 금색) · 기어 가속 · 바늘 올라감 · 끝 무렵 작은 떨림 ·
    //                  틈의 작은 증기 · 피스톤은 뒤로 갈수록 깊게. 화면 테두리 달굼 · 큰 흔들림은 쓰지 않는다
    //   2. 수축 · 버팀 몸통이 내려앉고 세로만 살짝 눌림 · 바늘은 높은 자리에서 버팀 · 충전음 눌림 · 새 증기 없음
    //   3. 분출 · 반동 충전음 끊고 주 타격음 하나 (증기 소리는 조금 뒤) · 굵은 증기 · 짧은 화면 흔들림 · 몸통이 위로 튀었다 먼저 자리 잡고
    //                  기어 래칫 → 피스톤 → 바늘 복귀 · 잔떨림 순 (GachaMachineView.PlayClearBurst) · 금색 테두리 · 계수기 금색
    //   4. 잦아듦     증기가 점점 작게 · 글자는 분출 0.1초 뒤 · 마지막 증기가 끝나면 결과 창 · 금색 불빛은 남는다
    // 약속은 DrawRevealView 와 같다: Play 한 번에 onFinished 정확히 한 번 · Skip = 바로 끝내고 콜백 · Cancel = 콜백 없음 ·
    // 시간은 Time.deltaTime 만 (일시정지면 멈춤 · 새 효과도 없음). 결과 · 기록은 이미 저장돼 있다 — 결과 창을 늦출 뿐이다
    public sealed class RunClearView : MonoBehaviour
    {
        [SerializeField] private RunClearSettings settings;
        [SerializeField] private TMP_Text bannerText;
        [Tooltip("비어 있으면 기계 반응 없이")]
        [SerializeField] private GachaMachineView machine;
        [Tooltip("비어 있으면 계수기 금색 없이")]
        [SerializeField] private MechanicalCounterView counter;
        [Tooltip("분출 순간 짧은 화면 흔들림. 비어 있으면 없이")]
        [SerializeField] private RevealEffectsView effects;
        [Tooltip("비어 있으면 소리 없이")]
        [SerializeField] private SoundPlayer sounds;
        [Tooltip("불이 들어오는 순서대로: 작은 표시등 → 계기판 → 출력 창. 원래 색에서 빛 색으로 바뀐다")]
        [SerializeField] private Image[] lightStages = Array.Empty<Image>();
        [Tooltip("완료 글자를 띄울 때 남은 생산량 +n 팝업을 지운다 (겹침 방지). 비어 있으면 그대로")]
        [SerializeField] private OutputPopupView outputPopup;

        private Coroutine routine;
        private bool skipRequested;
        private bool charging;
        private Color[] lightRestColors = Array.Empty<Color>();

        public bool IsPlaying { get; private set; }

        private void Awake()
        {
            lightRestColors = new Color[lightStages.Length];
            for (int i = 0; i < lightStages.Length; i++)
            {
                lightRestColors[i] = lightStages[i] != null ? lightStages[i].color : Color.white;
            }

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

        /// <summary>남은 연출을 건너뛰고 결과 창으로 (금색 불빛 · 테두리만 남김). 연출 중이 아니면 무시한다.</summary>
        public void Skip()
        {
            if (IsPlaying)
            {
                skipRequested = true;
            }
        }

        /// <summary>멈추고 콜백을 부르지 않는다 (런을 버릴 때 · 새 런). 금색 불빛까지 원래대로 돌린다.</summary>
        public void Cancel()
        {
            if (routine != null)
            {
                StopCoroutine(routine);
                routine = null;
            }

            bool wasPlaying = IsPlaying;
            IsPlaying = false;
            HideBanner();
            if (wasPlaying)
            {
                StopMotionAndSound();
            }

            // 끝난 뒤 남겨 둔 금색 불빛도 런을 떠나면 지운다 (다음 런에 남지 않게)
            SetLights(0f, 0f);
        }

        private IEnumerator PlayRoutine(float multiplier, Action onFinished)
        {
            float charge = settings.ChargeSeconds * multiplier;
            float hold = settings.HoldSeconds * multiplier;
            float burstAt = charge + hold;
            float interval = settings.SteamInterval * multiplier;
            float bannerAt = burstAt + settings.BannerDelay * multiplier;
            float steamSoundAt = burstAt + settings.SteamSoundDelay * multiplier;
            float bannerPop = settings.BannerPopSeconds * multiplier;

            // 마지막 증기가 끝나기 전에 결과 창으로 넘어가지 않는다 (빠름도 같은 비율 · 교차검토 #40)
            float steamEnd = machine != null ? burstAt + (settings.SteamBursts - 1) * interval + machine.GetBreakthroughSeconds(multiplier) : 0f;
            float total = Mathf.Max(burstAt + settings.AfterBurstSeconds * multiplier, steamEnd);
            float steamSeconds = machine != null ? machine.GetBreakthroughSeconds(multiplier) : 0f;
            float lastSteamEnd = 0f;

            StartCharge();
            bool holding = false;
            bool burst = false;
            bool bannerShown = false;
            bool steamSoundPlayed = false;
            int burstsDone = 0;
            float elapsed = 0f;
            // 이번 반복에서 시간이 흘렀는가. 일시정지 (deltaTime 0) 중에는 밀린 증기도 새로 내지 않는다 (교차검토 #41)
            bool timeMoved = true;
            while (true)
            {
                if (elapsed < charge)
                {
                    Charge(elapsed / charge, elapsed, multiplier);
                }
                else if (!holding)
                {
                    holding = true;
                    Hold(hold);
                }

                if (!burst && elapsed >= burstAt)
                {
                    burst = true;
                    Burst(multiplier);
                }

                if (burst && !steamSoundPlayed && elapsed >= steamSoundAt)
                {
                    steamSoundPlayed = true;
                    if (sounds != null)
                    {
                        // 예약하지 않고 바로 — 스킵하면 뒤늦게 나지 않는다 (교차검토 #41)
                        sounds.PlayBreakthroughNow();
                    }
                }

                // 증기는 분출 뒤 시간이 흐른 만큼 · 시간이 흐른 프레임에 하나까지 · 뒤로 갈수록 작게 (잦아듦).
                // 긴 프레임으로 밀린 증기는 다음 프레임들에 늦게 내고, 결과 창은 그 증기가 끝날 때까지 기다린다
                if (burst && timeMoved && burstsDone < settings.SteamBursts && elapsed - burstAt >= burstsDone * interval)
                {
                    if (machine != null)
                    {
                        machine.PlayBreakthrough(multiplier, settings.GetSteamScale(burstsDone));
                    }

                    // 생산량 바퀴를 기다리지 않고 바로 · 연출 동안 생산량 번쩍임이 금색을 덮지 못하게 잠근다
                    if (counter != null)
                    {
                        counter.FlashNow(Grade.SR, true);
                    }

                    lastSteamEnd = elapsed + steamSeconds;
                    burstsDone++;
                }

                if (!bannerShown && elapsed >= bannerAt)
                {
                    bannerShown = true;
                    ShowBanner();
                }

                SetBannerScale(bannerShown ? Mathf.Clamp01((elapsed - bannerAt) / bannerPop) : 0f);
                bool steamDone = burstsDone >= settings.SteamBursts && elapsed >= lastSteamEnd;
                if ((elapsed >= total && steamDone) || skipRequested)
                {
                    break;
                }

                yield return null;
                float delta = Time.deltaTime;
                elapsed += delta;
                timeMoved = delta > 0f;
            }

            routine = null;
            IsPlaying = false;
            HideBanner();
            if (skipRequested)
            {
                // 스킵: 충전음 · 예약된 증기 · 반동을 지우고 기계를 제자리로. 완료용 금색 (불빛 · 테두리) 만 남긴다
                StopMotionAndSound();
            }
            else if (counter != null)
            {
                counter.UnlockFlash();
            }

            SetLights(1f, 1f);
            if (machine != null)
            {
                machine.ShowGradeFrame(Grade.SR);
            }

            onFinished();
        }

        private void StartCharge()
        {
            charging = true;
            if (sounds != null)
            {
                sounds.StartCharge();
                sounds.SetChargeVolumeScale(settings.ChargeVolumeStart);
            }

            if (machine != null)
            {
                machine.SetClearCharge(0f, 0f, 1f);
            }

            SetLights(0f, 0f);
        }

        private void Charge(float progress, float elapsed, float multiplier)
        {
            if (machine != null)
            {
                // 끝에 가까울수록 작은 떨림이 커진다 (진행률³)
                float swing = Mathf.Sin(2f * Mathf.PI * settings.NeedleJitterHz * elapsed);
                float jitter = settings.NeedleJitterDegrees * progress * progress * progress * swing;
                machine.SetClearCharge(progress, jitter, multiplier);
            }

            if (sounds != null)
            {
                sounds.SetChargeVolumeScale(Mathf.Lerp(settings.ChargeVolumeStart, settings.ChargeVolumeEnd, progress));
            }

            SetLights(progress, progress);
        }

        private void Hold(float seconds)
        {
            if (machine != null)
            {
                machine.HoldClearPressure(settings.SinkPixels, settings.SquashY, seconds);
            }

            if (sounds != null)
            {
                sounds.SetChargeVolumeScale(settings.HoldChargeVolume);
            }

            SetLights(1f, 1f);
        }

        // 분출 순간: 충전음 끊고 주 타격음 하나 (SR 공개음 자리) · 몸통 · 부품 시간차 반동 · 굵은 증기는 증기 차례에서 · 짧은 흔들림
        private void Burst(float multiplier)
        {
            StopCharge();
            if (sounds != null)
            {
                sounds.PlayReveal(Grade.SR, false);
            }

            if (effects != null)
            {
                effects.PlayKick(multiplier);
            }

            if (machine != null)
            {
                machine.PlayClearBurst(settings.BouncePixels, settings.PopY, settings.BodySettleSeconds,
                    settings.GearDelay, settings.PistonDelay, settings.NeedleDelay,
                    settings.NeedleWobbleDegrees, settings.NeedleWobbleSeconds, multiplier);
                machine.ShowGradeFrame(Grade.SR);
            }
        }

        private void StopCharge()
        {
            if (charging && sounds != null)
            {
                sounds.StopCharge();
            }

            charging = false;
        }

        // 스킵 · 취소: 충전음 · 흔들림 · 증기 · 반동을 지우고 계수기 잠금을 푼다
        private void StopMotionAndSound()
        {
            StopCharge();
            if (effects != null)
            {
                effects.StopAll();
            }

            if (machine != null)
            {
                machine.StopClearMotion();
            }

            if (counter != null)
            {
                counter.UnlockFlash();
            }
        }

        // reach 0 → 1 = 표시등 → 계기판 → 출력 창 순서로 켜짐 · warmth 0 → 1 = 황색 → 금색
        private void SetLights(float reach, float warmth)
        {
            Color light = Color.Lerp(settings != null ? settings.WarmLight : Color.white, settings != null ? settings.GoldLight : Color.white, warmth);
            int count = lightStages.Length;
            for (int i = 0; i < count; i++)
            {
                if (lightStages[i] == null || i >= lightRestColors.Length)
                {
                    continue;
                }

                // 단계마다 진행률 구간을 나눠 차례로 켠다 (겹치게 · 다음 단계가 앞 단계 끝 무렵 시작)
                float lastStart = count > 1 ? 0.9f * (count - 1) / count : 0f;
                float start = count > 1 ? 0.9f * i / count : 0f;
                float on = Mathf.Clamp01((reach - start) / (1f - lastStart));
                float strength = settings != null ? settings.GetLightStrength(i) : 1f;
                lightStages[i].color = Color.Lerp(lightRestColors[i], light, on * strength);
            }
        }

        private void ShowBanner()
        {
            // 남은 생산량 +n 은 이때 지운다 — 완료 글자와 겹치지 않게 (보통은 이미 끝나 있다)
            if (outputPopup != null)
            {
                outputPopup.HideNow();
            }

            if (bannerText == null)
            {
                return;
            }

            bannerText.text = settings.BannerText;
            bannerText.color = GradeStyle.GetColor(Grade.SR);
            bannerText.gameObject.SetActive(true);
            SetBannerScale(0f);
        }

        private void SetBannerScale(float popProgress)
        {
            if (bannerText == null || !bannerText.gameObject.activeSelf)
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

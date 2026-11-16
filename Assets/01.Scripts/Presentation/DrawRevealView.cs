using System;
using System.Collections;
using Overclock.Core;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Overclock.Presentation
{
    // 뽑기 결과 공개 연출. 결과는 연출 전에 이미 확정 · 저장돼 있고, 이 창은 보여 주는 시점만 늦춤.
    // 약속 (10/26 구조 검수): Play 한 번에 onFinished 는 정확히 한 번 · Cancel 은 콜백을 부르지 않음 ·
    // 시간은 Time.deltaTime 만 씀 (timeScale 0 = 일시정지면 그 자리에서 멈춤) · 연출 중인지는 IsPlaying 하나로 판단.
    // 겉모습 효과(SR 달아오름 · 공개 순간)는 RevealEffectsView 가, 기계 반응(기어 · 피스톤 · 바늘 · 증기)은 GachaMachineView 가,
    // 소리(SR 충전 · 등급 공개 · 3연속)는 SoundPlayer 가 내고, 언제 낼지는 여기서 정함.
    // 셔터 (11/04 · §14-5): 기다리는 동안 출력 창을 닫아 두고 공개 순간 엶 · R · SR 은 기다리는 동안 래칫 틱 · SR 은 떨림 + 틈새 증기.
    // 모두 뜸들이기 시간 안에서만 · 스킵 · 즉시 = 셔터 없이 바로 최종 모습
    public sealed class DrawRevealView : MonoBehaviour
    {
        [SerializeField] private RevealTimings revealTimings;
        [SerializeField] private TMP_Text partText;
        [SerializeField] private Image partIcon;
        [Tooltip("비어 있으면 효과 없이 시간만 흐름")]
        [SerializeField] private RevealEffectsView effects;
        [Tooltip("비어 있으면 기계 반응 없이 연출만 함")]
        [SerializeField] private GachaMachineView machine;
        [Tooltip("비어 있으면 소리 없이 연출만 함")]
        [SerializeField] private SoundPlayer sounds;
        [Tooltip("비어 있으면 셔터 · 기다림 틱 없이 연출만 함")]
        [SerializeField] private MachineShutterView shutter;
        [Tooltip("공개 순간 출력 창 위에 뜨는 큰 생산량 +n (11/06). 비어 있으면 없이 연출함")]
        [SerializeField] private OutputPopupView outputPopup;

        private Coroutine routine;
        private bool skipRequested;
        private float timeMultiplier = 1f;
        private bool shutterHooked;

        public bool IsPlaying { get; private set; }

        /// <summary>마지막 연출이 공개 모습까지 보여 줬는가 (false = 스킵 · 즉시). onFinished 안에서 읽음 — 더미 · 숫자 롤업이 같은 판단을 씀.</summary>
        public bool LastRevealAnimated { get; private set; }

        public void Play(DrawResult result, Action onFinished)
        {
            if (onFinished == null)
            {
                throw new ArgumentNullException(nameof(onFinished));
            }

            // 연출을 겹치면 앞 연출의 콜백이 사라지거나 두 번 불림. 부르는 쪽이 IsPlaying 으로 막음
            if (IsPlaying)
            {
                throw new InvalidOperationException("이미 연출 중입니다. 새 연출 전에 Cancel 해야 합니다.");
            }

            // 스킵 요청은 여기 한 곳에서 지움. 앞 연출에서 남은 요청이 이번 연출을 건너뛰지 않게 함
            IsPlaying = true;
            skipRequested = false;
            StopEffects();
            ShowPlaceholder("???");

            // 연출 시간이 0 이면 StartCoroutine 안에서 바로 끝나고 콜백까지 불림.
            // 그때는 끝난 코루틴을 들고 있지 않고, 콜백 안에서 시작된 다음 연출의 참조(routine)도 덮어쓰지 않음
            Coroutine started = StartCoroutine(PlayRoutine(result, onFinished));
            if (IsPlaying && routine == null)
            {
                routine = started;
            }
        }

        /// <summary>남은 연출을 건너뜀. 결과는 이번 프레임 코루틴 차례(Update 뒤)에 보여 줌. 연출 중이 아니면 무시함.</summary>
        public void Skip()
        {
            if (IsPlaying)
            {
                skipRequested = true;
            }
        }

        /// <summary>연출을 멈춤. 결과를 보여 주지 않고 콜백도 부르지 않음 (런을 버릴 때).</summary>
        public void Cancel()
        {
            if (routine != null)
            {
                StopCoroutine(routine);
                routine = null;
            }

            IsPlaying = false;
            StopEffects();
            StopCharge();
        }

        /// <summary>
        /// 플레이어 설정을 적용함. 연출 속도는 다음 Play 부터 (진행 중인 연출의 시간은 바꾸지 않음) · 흔들림 · 섬광은 바로.
        /// </summary>
        public void ApplySettings(GameSettings settings)
        {
            if (settings == null)
            {
                throw new ArgumentNullException(nameof(settings));
            }

            timeMultiplier = settings.RevealTimeMultiplier;
            if (effects != null)
            {
                effects.SetMotionOptions(settings.ShakeEnabled, settings.FlashEnabled);
            }

            // 기계 덜컹만 흔들림 설정을 따름. 기어 · 피스톤 · 증기는 그대로 (기계연출 구상 §14-1)
            if (machine != null)
            {
                machine.SetThudEnabled(settings.ShakeEnabled);
            }
        }

        /// <summary>뽑은 부품이 없을 때의 표시 (런 시작 · 모듈 선택 뒤).</summary>
        public void ShowIdle()
        {
            StopEffects();
            ShowPlaceholder("-");
            if (machine != null)
            {
                machine.ClearGradeFrame();
            }
        }

        private void OnDestroy()
        {
            if (shutterHooked && shutter != null)
            {
                shutter.Closed -= OnShutterClosed;
            }
        }

        private void OnDisable()
        {
            // 오브젝트가 꺼지면 Unity 가 코루틴을 멈춤. IsPlaying 이 true 로 남지 않게 같이 정리함
            Cancel();
        }

        private IEnumerator PlayRoutine(DrawResult result, Action onFinished)
        {
            // 속도는 시작할 때 한 번 읽음. 즉시(0)면 기다리지 않고 공개 효과도 없음
            // 이 뽑기가 끝날 때까지 이 배율을 씀 — 기다리는 도중 설정을 바꿔도 이번 박자는 그대로 · 다음 뽑기부터 (교차검토 #35)
            Grade grade = result.Part.Grade;
            float multiplier = timeMultiplier;
            bool instant = multiplier <= 0f;
            float duration = revealTimings.GetSeconds(grade) * multiplier;
            bool heating = grade == Grade.SR && effects != null && duration > 0f;
            if (heating)
            {
                effects.SetHeat(0f);
            }

            bool machineHeating = grade == Grade.SR && machine != null && duration > 0f;
            if (machineHeating)
            {
                machine.SetHeat(0f);
            }

            // 충전음은 SR 을 기다리는 동안만. 즉시(기다림 0)면 시작하지 않음
            bool charging = grade == Grade.SR && sounds != null && duration > 0f;
            if (charging)
            {
                sounds.StartCharge();
            }

            // 셔터: 레버가 바닥에 닿을 즈음 내려와 닫히고, 기다리는 동안 R · SR 은 래칫 틱 (뜸들이기 시간 안 · 연출 속도 배율 적용)
            bool shuttering = shutter != null && duration > 0f;
            ShutterSettings shutterSettings = shuttering ? shutter.Settings : null;
            float nextTick = float.PositiveInfinity;
            int ticks = 0;
            if (shuttering)
            {
                HookShutter();
                shutter.Close(shutterSettings.CloseDelay * multiplier, shutterSettings.CloseSeconds * multiplier);
                if (shutterSettings.GetTickInterval(grade, 0f) > 0f)
                {
                    nextTick = (shutterSettings.CloseDelay + shutterSettings.CloseSeconds) * multiplier;
                }
            }

            float elapsed = 0f;
            while (elapsed < duration && !skipRequested)
            {
                yield return null;
                elapsed += Time.deltaTime;
                if (heating)
                {
                    effects.SetHeat(elapsed / duration);
                }

                if (machineHeating)
                {
                    machine.SetHeat(elapsed / duration);
                }

                if (shuttering && !skipRequested && elapsed < duration)
                {
                    float progress = elapsed / duration;
                    if (grade == Grade.SR)
                    {
                        // 공개가 가까울수록 크게 떪. 시계(elapsed)로 값을 정해 스킵 · 일시정지와 맞음
                        float amplitude = shutterSettings.SuperRareJitterPixels * progress;
                        shutter.SetJitter(amplitude * (Mathf.PerlinNoise(elapsed * shutterSettings.JitterFrequency, 0.37f) * 2f - 1f));
                    }

                    // 시간이 흐른 프레임에만 틱 하나 (일시정지 중에는 없음 · 긴 프레임에 소리가 겹쳐 터지지 않게).
                    // 긴 프레임으로 이미 지나간 박자는 소리 없이 넘겨 다음 틱을 미래로 옮김 — 안 그러면 멈춘 동안 밀린 틱이 남 (교차검토 #35)
                    if (elapsed >= nextTick && Time.deltaTime > 0f)
                    {
                        Tick(grade, ticks, multiplier);
                        ticks++;
                        while (nextTick <= elapsed)
                        {
                            nextTick += shutterSettings.GetTickInterval(grade, nextTick / duration) * multiplier;
                        }
                    }
                }
            }

            // 스킵을 눌렀으면 스킵임. 누른 프레임에 시간이 끝 선을 넘었어도 마찬가지 (교차검토 #29).
            // 스킵이면 공개 효과 없이 바로 쉬는 상태로 보여 줌
            bool skipped = skipRequested;

            // 콜백보다 먼저 연출을 끝낸 상태로 만듦. 콜백 안에서 다음 연출을 시작할 수 있음
            routine = null;
            IsPlaying = false;
            LastRevealAnimated = !(skipped || instant);
            ShowResult(result);
            if (skipped || instant)
            {
                StopEffects();
            }
            else
            {
                if (effects != null)
                {
                    effects.PlayReveal(grade);
                }

                if (machine != null)
                {
                    machine.PlayReveal(grade);
                }

                if (outputPopup != null)
                {
                    outputPopup.Play(result, multiplier);
                }

                if (shuttering)
                {
                    shutter.Open(grade, multiplier);
                    if (sounds != null)
                    {
                        sounds.PlayShutterOpen(grade);
                    }
                }
            }

            // 출력 창 테두리 = 공개된 등급 색. 스킵 · 즉시여도 같은 최종 모습 (다음 레버에서 꺼짐)
            if (machine != null)
            {
                machine.ShowGradeFrame(grade);
            }

            // 소리는 스킵 · 즉시여도 냄 (10/29 결정). 충전음은 공개 순간에 끊음.
            // 콜백(돌파 · 런 끝 소리 예약)보다 먼저 내야 그 소리들이 3연속보다 늦게 시작함
            StopCharge();
            if (sounds != null)
            {
                sounds.PlayReveal(grade, result.StreakTriggered);
            }

            onFinished();
        }

        private void Tick(Grade grade, int index, float multiplier)
        {
            ShutterSettings shutterSettings = shutter.Settings;
            if (machine != null)
            {
                machine.PlayWaitTick(shutterSettings.TickAngle, shutterSettings.TickSeconds * multiplier);
            }

            if (sounds != null)
            {
                sounds.PlayRatchetTick();
            }

            if (grade == Grade.SR)
            {
                shutter.PuffGap(index);
            }
        }

        private void HookShutter()
        {
            if (!shutterHooked)
            {
                shutter.Closed += OnShutterClosed;
                shutterHooked = true;
            }
        }

        private void OnShutterClosed()
        {
            if (machine != null)
            {
                machine.Bump(shutter.Settings.CloseThudPixels);
            }

            if (sounds != null)
            {
                sounds.PlayShutterClose();
            }
        }

        private void StopEffects()
        {
            if (effects != null)
            {
                effects.StopAll();
            }

            // 셔터는 바로 열린 자세로 (스킵 · 즉시 · 새 연출 · Cancel · 쉬는 상태)
            if (shutter != null)
            {
                shutter.ShowOpen();
            }

            // 레버는 멈추지 않음 (GachaMachineView.StopReactions). 런 이탈 때 레버는 GrayBoxRunScreen 이 되돌림
            if (machine != null)
            {
                machine.StopReactions();
            }

            // 큰 +n 은 공개 효과임. 새 연출 · 스킵 · 즉시 · Cancel · 쉬는 상태에서는 지움
            if (outputPopup != null)
            {
                outputPopup.HideNow();
            }
        }

        private void StopCharge()
        {
            if (sounds != null)
            {
                sounds.StopCharge();
            }
        }

        private void ShowResult(DrawResult result)
        {
            PartData part = result.Part;
            Color gradeColor = GradeStyle.GetColor(part.Grade);
            partText.text = GradeStyle.GetStars(part.Grade) + " " + part.DisplayName + "  +" + result.Output;
            partText.color = gradeColor;
            partIcon.sprite = part.Icon;
            partIcon.color = gradeColor;
            partIcon.enabled = part.Icon != null;
        }

        private void ShowPlaceholder(string text)
        {
            partText.text = text;
            partText.color = Color.white;
            partIcon.enabled = false;
        }
    }
}

using System;
using System.Collections;
using Overclock.Core;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Overclock.Presentation
{
    // 뽑기 결과 공개 연출. 결과는 연출 전에 이미 확정 · 저장돼 있고, 이 창은 보여 주는 시점만 늦춘다.
    // 약속 (10/26 구조 검수): Play 한 번에 onFinished 는 정확히 한 번 · Cancel 은 콜백을 부르지 않음 ·
    // 시간은 Time.deltaTime 만 쓴다 (timeScale 0 = 일시정지면 그 자리에서 멈춤) · 연출 중인지는 IsPlaying 하나로 판단.
    // 겉모습 효과(SR 달아오름 · 공개 순간)는 RevealEffectsView 가 그리고, 언제 그릴지는 여기서 정한다
    public sealed class DrawRevealView : MonoBehaviour
    {
        [SerializeField] private RevealTimings revealTimings;
        [SerializeField] private TMP_Text partText;
        [SerializeField] private Image partIcon;
        [Tooltip("비어 있으면 효과 없이 시간만 흐른다")]
        [SerializeField] private RevealEffectsView effects;

        private Coroutine routine;
        private bool skipRequested;
        private float timeMultiplier = 1f;

        public bool IsPlaying { get; private set; }

        public void Play(DrawResult result, Action onFinished)
        {
            if (onFinished == null)
            {
                throw new ArgumentNullException(nameof(onFinished));
            }

            // 연출을 겹치면 앞 연출의 콜백이 사라지거나 두 번 불린다. 부르는 쪽이 IsPlaying 으로 막는다
            if (IsPlaying)
            {
                throw new InvalidOperationException("이미 연출 중입니다. 새 연출 전에 Cancel 해야 합니다.");
            }

            // 스킵 요청은 여기 한 곳에서 지운다. 앞 연출에서 남은 요청이 이번 연출을 건너뛰지 않게 한다
            IsPlaying = true;
            skipRequested = false;
            StopEffects();
            ShowPlaceholder("???");

            // 연출 시간이 0 이면 StartCoroutine 안에서 바로 끝나고 콜백까지 불린다.
            // 그때는 끝난 코루틴을 들고 있지 않고, 콜백 안에서 시작된 다음 연출의 참조(routine)도 덮어쓰지 않는다
            Coroutine started = StartCoroutine(PlayRoutine(result, onFinished));
            if (IsPlaying && routine == null)
            {
                routine = started;
            }
        }

        /// <summary>남은 연출을 건너뛴다. 결과는 이번 프레임 코루틴 차례(Update 뒤)에 보여 준다. 연출 중이 아니면 무시한다.</summary>
        public void Skip()
        {
            if (IsPlaying)
            {
                skipRequested = true;
            }
        }

        /// <summary>연출을 멈춘다. 결과를 보여 주지 않고 콜백도 부르지 않는다 (런을 버릴 때).</summary>
        public void Cancel()
        {
            if (routine != null)
            {
                StopCoroutine(routine);
                routine = null;
            }

            IsPlaying = false;
            StopEffects();
        }

        /// <summary>
        /// 플레이어 설정을 적용한다. 연출 속도는 다음 Play 부터 (진행 중인 연출의 시간은 바꾸지 않음) · 흔들림 · 섬광은 바로.
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
        }

        /// <summary>뽑은 부품이 없을 때의 표시 (런 시작 · 모듈 선택 뒤).</summary>
        public void ShowIdle()
        {
            StopEffects();
            ShowPlaceholder("-");
        }

        private void OnDisable()
        {
            // 오브젝트가 꺼지면 Unity 가 코루틴을 멈춘다. IsPlaying 이 true 로 남지 않게 같이 정리한다
            Cancel();
        }

        private IEnumerator PlayRoutine(DrawResult result, Action onFinished)
        {
            // 속도는 시작할 때 한 번 읽는다. 즉시(0)면 기다리지 않고 공개 효과도 없다
            Grade grade = result.Part.Grade;
            bool instant = timeMultiplier <= 0f;
            float duration = revealTimings.GetSeconds(grade) * timeMultiplier;
            bool heating = grade == Grade.SR && effects != null && duration > 0f;
            if (heating)
            {
                effects.SetHeat(0f);
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
            }

            // 스킵을 눌렀으면 스킵이다. 누른 프레임에 시간이 끝 선을 넘었어도 마찬가지 (교차검토 #29).
            // 스킵이면 공개 효과 없이 바로 쉬는 상태로 보여 준다
            bool skipped = skipRequested;

            // 콜백보다 먼저 연출을 끝낸 상태로 만든다. 콜백 안에서 다음 연출을 시작할 수 있다
            routine = null;
            IsPlaying = false;
            ShowResult(result);
            if (skipped || instant)
            {
                StopEffects();
            }
            else if (effects != null)
            {
                effects.PlayReveal(grade);
            }

            onFinished();
        }

        private void StopEffects()
        {
            if (effects != null)
            {
                effects.StopAll();
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

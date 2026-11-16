using DG.Tweening;
using Overclock.Core;
using TMPro;
using UnityEngine;

namespace Overclock.Presentation
{
    // 공개 순간 출력 창 위에 생산량 "+n" 을 등급 색 · 크기로 크게 띄우고 위로 떠오르며 사라짐 (11/06 · 멘토 "숫자 크게").
    // 공개 효과라서 스킵 · 즉시면 띄우지 않음 (DrawRevealView 가 부름). 게임 상태는 모름 · 시간은 scaled time.
    // 돌파 카드 · 결과 창은 공개 직후 같은 흐름에서 열려 출력 창을 덮음 → 그 창이 켜져 있으면 창 위쪽 빈자리에 띄움 (교차검토 #39)
    public sealed class OutputPopupView : MonoBehaviour
    {
        [SerializeField] private OutputPopupSettings settings;
        [SerializeField] private TMP_Text popupText;
        [Tooltip("이 중 하나라도 켜져 있으면 (돌파 카드 · 결과 창) 그 창에 가리지 않는 자리에 띄움. 팝업은 이 창들보다 뒤에 그려지도록 (형제 순서가 더 아래) 둠")]
        [SerializeField] private GameObject[] coveringPanels;
        [Tooltip("덮는 창이 켜져 있을 때의 자리 (카드 위 · 결과 창 위 빈칸)")]
        [SerializeField] private Vector2 uncoveredPosition = new Vector2(0f, 400f);

        private RectTransform popupRect;
        private Vector2 restPosition;
        private Tween tween;

        public bool IsShowing => tween != null;

        private void Awake()
        {
            if (popupText != null)
            {
                popupRect = popupText.rectTransform;
                restPosition = popupRect.anchoredPosition;
            }

            HideNow();
        }

        private void OnDisable()
        {
            HideNow();
        }

        private void OnDestroy()
        {
            KillTween();
        }

        /// <summary>이 뽑기의 생산량을 띄움. 앞 팝업이 남아 있으면 지우고 새로 띄움. timeMultiplier = 연출 속도 배율 (0 이하면 띄우지 않음).</summary>
        public void Play(DrawResult result, float timeMultiplier)
        {
            HideNow();
            if (popupText == null || settings == null || timeMultiplier <= 0f)
            {
                return;
            }

            Grade grade = result.Part.Grade;
            bool bonus = result.StreakTriggered || result.Overclocked;
            float peak = settings.GetScale(grade) * (bonus ? settings.BonusScale : 1f);
            float rise = settings.GetRise(grade);
            float pop = settings.PopSeconds * timeMultiplier;
            float hold = settings.HoldSeconds * timeMultiplier;
            float fade = settings.FadeSeconds * timeMultiplier;
            float total = pop + hold + fade;
            Color color = GradeStyle.GetColor(grade);

            popupText.text = "+" + result.Output + Suffix(result);
            popupText.gameObject.SetActive(true);
            Apply(0f, peak, rise, pop, hold, total, color);
            tween = DOTween.To(elapsed => Apply(elapsed, peak, rise, pop, hold, total, color), 0f, total, total)
                .SetEase(Ease.Linear)
                .OnComplete(() =>
                {
                    tween = null;
                    HideNow();
                });
        }

        /// <summary>바로 지움 (다음 뽑기 시작 · 스킵 · 런 이탈 · 쉬는 화면).</summary>
        public void HideNow()
        {
            KillTween();
            if (popupText == null)
            {
                return;
            }

            if (popupRect != null)
            {
                popupRect.anchoredPosition = restPosition;
                popupRect.localScale = Vector3.one;
            }

            popupText.gameObject.SetActive(false);
        }

        private static string Suffix(DrawResult result)
        {
            // 작은 둘째 줄로 이 뽑기에 걸린 상태를 붙임 (3연속 발동 · 오버클럭 중) — 생산량이 늘었는지와는 별개
            if (result.StreakTriggered && result.Overclocked)
            {
                return "\n<size=45%>3연속 + 오버클럭</size>";
            }

            if (result.StreakTriggered)
            {
                return "\n<size=45%>3연속</size>";
            }

            return result.Overclocked ? "\n<size=45%>오버클럭</size>" : "";
        }

        // elapsed 하나로 크기 · 높이 · 투명도를 정함 (일시정지 · 긴 프레임에도 같은 모습)
        private void Apply(float elapsed, float peak, float rise, float pop, float hold, float total, Color color)
        {
            float popT = Mathf.Clamp01(elapsed / pop);
            float scale = peak * (0.5f + 0.5f * EaseOutBack(popT));
            popupRect.localScale = new Vector3(scale, scale, 1f);

            float riseT = Mathf.Clamp01(elapsed / total);
            float eased = 1f - (1f - riseT) * (1f - riseT);
            // 매 프레임 고름 — 팝업을 띄운 직후 같은 흐름에서 카드 · 결과 창이 열림
            Vector2 basePosition = IsCovered() ? uncoveredPosition : restPosition;
            popupRect.anchoredPosition = basePosition + new Vector2(0f, rise * eased);

            float fadeStart = pop + hold;
            float alpha = elapsed <= fadeStart ? 1f : 1f - Mathf.Clamp01((elapsed - fadeStart) / (total - fadeStart));
            popupText.color = new Color(color.r, color.g, color.b, alpha);
        }

        private bool IsCovered()
        {
            if (coveringPanels == null)
            {
                return false;
            }

            foreach (GameObject panel in coveringPanels)
            {
                if (panel != null && panel.activeSelf)
                {
                    return true;
                }
            }

            return false;
        }

        // 0 → 1 로 가며 1 을 살짝 넘었다 돌아옴
        private static float EaseOutBack(float t)
        {
            const float overshoot = 1.70158f;
            float u = t - 1f;
            return 1f + (overshoot + 1f) * u * u * u + overshoot * u * u;
        }

        private void KillTween()
        {
            if (tween != null && tween.IsActive())
            {
                tween.Kill();
            }

            tween = null;
        }
    }
}

using DG.Tweening;
using Overclock.Core;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Overclock.Presentation
{
    // 할당량 줄의 생산량 숫자 · 게이지. 공개 뒤 이전 값에서 새 값으로 세어 올라가고, 다 세면 등급 색으로 잠깐 튄다.
    // 보이는 값만 늦출 뿐 생산량은 이미 확정돼 있다. 숫자는 뒤로 가지 않는다 — 새 값이 작거나 할당량이 바뀌면(새 라운드) 바로 맞춘다.
    // 시간은 scaled time (일시정지면 멈춤)
    public sealed class ProductionCounterView : MonoBehaviour
    {
        [SerializeField] private HarvestDisplaySettings settings;
        [Tooltip("\"생산량 / 할당량\" 글자")]
        [SerializeField] private TMP_Text valueText;
        [SerializeField] private Slider gauge;

        private float shownProduction;
        private int targetProduction;
        private int quota;
        private Color restColor = Color.white;
        private Tween rollTween;
        private Tween punchTween;

        /// <summary>세는 중이거나 튀는 중인가.</summary>
        public bool IsAnimating => rollTween != null || punchTween != null;

        private void Awake()
        {
            if (valueText != null)
            {
                restColor = valueText.color;
            }
        }

        private void OnDisable()
        {
            CompleteNow();
        }

        private void OnDestroy()
        {
            KillTweens();
        }

        /// <summary>바로 그 값으로 보여 준다 (라운드 시작 · 모듈 선택 · 이어하기 · 스킵 · 즉시).</summary>
        public void SetImmediate(int production, int quota)
        {
            KillTweens();
            targetProduction = production;
            this.quota = quota;
            shownProduction = production;
            ResetPunch();
            Apply();
        }

        /// <summary>
        /// 지금 보이는 값에서 새 값까지 등급별 시간 동안 세어 올린다. 세는 중이면 앞의 값을 끝낸 자리에서 이어 센다.
        /// </summary>
        public void RollTo(int production, int quota, Grade grade)
        {
            CompleteNow();
            if (quota != this.quota || production <= targetProduction)
            {
                SetImmediate(production, quota);
                return;
            }

            float start = shownProduction;
            targetProduction = production;
            rollTween = DOTween.To(t => SetShown(start + (production - start) * t), 0f, 1f, settings.GetRollSeconds(grade))
                .SetEase(Ease.OutQuad)
                .OnComplete(() =>
                {
                    rollTween = null;
                    SetShown(production);
                    Punch(grade);
                });
        }

        /// <summary>세는 중 · 튀는 중이면 바로 최종 값 · 평소 모습으로 (다음 뽑기 시작 · 런 이탈).</summary>
        public void CompleteNow()
        {
            if (!IsAnimating)
            {
                return;
            }

            KillTweens();
            shownProduction = targetProduction;
            ResetPunch();
            Apply();
        }

        private void Punch(Grade grade)
        {
            float peak = settings.GetPunchScale(grade);
            if (peak <= 1f || valueText == null)
            {
                return;
            }

            Color gradeColor = GradeStyle.GetColor(grade);
            valueText.color = gradeColor;
            punchTween = DOTween.To(t => ApplyPunch(peak, t), 0f, 1f, settings.PunchSeconds)
                .OnComplete(() =>
                {
                    punchTween = null;
                    ResetPunch();
                });
        }

        private void ApplyPunch(float peak, float t)
        {
            // 0 → 절반에서 가장 크게 → 1 에서 제자리
            float bump = 1f - Mathf.Abs(2f * t - 1f);
            float scale = 1f + (peak - 1f) * bump;
            valueText.rectTransform.localScale = new Vector3(scale, scale, 1f);
        }

        private void ResetPunch()
        {
            if (valueText == null)
            {
                return;
            }

            valueText.rectTransform.localScale = Vector3.one;
            valueText.color = restColor;
        }

        private void SetShown(float value)
        {
            shownProduction = value;
            Apply();
        }

        private void Apply()
        {
            int shown = Mathf.RoundToInt(shownProduction);
            if (valueText != null)
            {
                valueText.text = shown + " / " + quota;
            }

            if (gauge != null)
            {
                gauge.maxValue = quota;
                gauge.value = Mathf.Min(shown, quota);
            }
        }

        private void KillTweens()
        {
            if (rollTween != null && rollTween.IsActive())
            {
                rollTween.Kill();
            }

            if (punchTween != null && punchTween.IsActive())
            {
                punchTween.Kill();
            }

            rollTween = null;
            punchTween = null;
        }
    }
}

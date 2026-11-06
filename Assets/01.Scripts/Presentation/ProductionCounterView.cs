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
        [Tooltip("할당량을 넘은 양 \"+n\" 글자 (11/05). 숫자와 같이 세어 올라가고 돌파 뽑기 끝에 등급 색으로 튄다. 비어 있으면 표시 없음")]
        [SerializeField] private TMP_Text overflowText;

        private float shownProduction;
        private int targetProduction;
        private int quota;
        private Color restColor = Color.white;
        private Color overflowRestColor = Color.white;
        private Tween overflowTween;
        private Tween rollTween;
        private Tween punchTween;
        private bool counting;
        private Grade countGrade;

        /// <summary>세는 중이거나 튀는 중인가.</summary>
        public bool IsAnimating => rollTween != null || punchTween != null || overflowTween != null || counting;

        private void Awake()
        {
            if (valueText != null)
            {
                restColor = valueText.color;
            }

            if (overflowText != null)
            {
                overflowRestColor = overflowText.color;
                overflowText.text = "";
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
            counting = false;
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

        /// <summary>
        /// 조각 단위로 세기 시작한다 (수확물 조각이 앉을 때마다 Advance). 값이 줄었거나 할당량이 바뀌면 바로 맞춘다.
        /// </summary>
        public void BeginCount(int production, int quota, Grade grade)
        {
            CompleteNow();
            if (quota != this.quota || production <= targetProduction)
            {
                SetImmediate(production, quota);
                return;
            }

            targetProduction = production;
            countGrade = grade;
            counting = true;
        }

        /// <summary>앉은 조각만큼 올린다. 목표에 닿으면 등급 색으로 튄다. 세는 중이 아니면 무시한다.</summary>
        public void Advance(int units)
        {
            if (!counting)
            {
                return;
            }

            SetShown(Mathf.Min(shownProduction + units, targetProduction));
            if (shownProduction >= targetProduction)
            {
                counting = false;
                Punch(countGrade);
            }
        }

        /// <summary>세는 중 · 튀는 중이면 바로 최종 값 · 평소 모습으로 (다음 뽑기 시작 · 런 이탈).</summary>
        public void CompleteNow()
        {
            if (!IsAnimating)
            {
                return;
            }

            KillTweens();
            counting = false;
            shownProduction = targetProduction;
            ResetPunch();
            Apply();
        }

        private void Punch(Grade grade)
        {
            PunchOverflow(grade);
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

        private void PunchOverflow(Grade grade)
        {
            // 돌파시킨 부품의 등급 색으로 튄다 (금색은 SR 일 때만). 초과분이 크면 더 크게
            int overflow = targetProduction - quota;
            if (overflowText == null || overflow <= 0)
            {
                return;
            }

            overflowText.color = GradeStyle.GetColor(grade);
            float peak = overflow >= quota * settings.BigOverflowRatio ? settings.BigOverflowPunchScale : settings.OverflowPunchScale;
            overflowTween = DOTween.To(t => ScaleByBump(overflowText, peak, t), 0f, 1f, settings.PunchSeconds)
                .OnComplete(() =>
                {
                    overflowTween = null;
                    overflowText.rectTransform.localScale = Vector3.one;
                });
        }

        private static void ScaleByBump(TMP_Text text, float peak, float t)
        {
            float bump = 1f - Mathf.Abs(2f * t - 1f);
            float scale = 1f + (peak - 1f) * bump;
            text.rectTransform.localScale = new Vector3(scale, scale, 1f);
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
            if (overflowText != null)
            {
                overflowText.rectTransform.localScale = Vector3.one;
            }
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

            if (overflowText != null)
            {
                // 보이는 숫자가 할당량을 넘은 만큼. 새 라운드 (생산량 0) · 실패 (미달) 면 빈 글자
                int overflow = shown - quota;
                overflowText.text = quota > 0 && overflow > 0 ? "+" + overflow : "";
                if (overflow <= 0)
                {
                    overflowText.color = overflowRestColor;
                }
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

            if (overflowTween != null && overflowTween.IsActive())
            {
                overflowTween.Kill();
            }

            rollTween = null;
            punchTween = null;
            overflowTween = null;
        }
    }
}

using System;
using DG.Tweening;
using Overclock.Core;
using TMPro;
using UnityEngine;

namespace Overclock.Presentation
{
    // 왼쪽 계기판 (등급별 확률 · 천장). 모듈을 달아 확률이 바뀌면 보이던 값에서 새 값으로 세어 바꾸고,
    // 바뀐 줄 옆에 ▲▼ 와 변화량(%p)을 잠깐 띄운다 (시트 §2 "계기판 수치 변화는 코어 연출").
    // 표시만 늦출 뿐 확률은 이미 바뀌어 있다. 시간은 scaled time (일시정지면 멈춤)
    public sealed class GaugePanelView : MonoBehaviour
    {
        private const int GradeCount = 3;
        private static readonly Grade[] Grades = { Grade.N, Grade.R, Grade.SR };
        private static readonly string[] Labels = { "N  ", "R  ", "SR " };

        [SerializeField] private GaugeDisplaySettings settings;
        [SerializeField] private TMP_Text panelText;

        private readonly double[] shown = new double[GradeCount];
        private readonly double[] rollStart = new double[GradeCount];
        private readonly double[] target = new double[GradeCount];
        private readonly string[] marks = new string[GradeCount];
        private bool hasTarget;
        private int pityDraws;
        private int pityThreshold;
        private Tween rollTween;
        private Tween markTween;

        /// <summary>숫자가 바뀌는 중이거나 ▲▼ 가 떠 있는가.</summary>
        public bool IsAnimating => rollTween != null || markTween != null;

        private void OnDisable()
        {
            CompleteNow();
        }

        private void OnDestroy()
        {
            KillTweens();
        }

        /// <summary>
        /// 평소 갱신 (뽑기 · 라운드 · 런 시작). 확률이 지금 목표와 같으면 진행 중인 연출을 그대로 두고 천장 줄만 바꾼다.
        /// 다르면 바로 그 값으로 보여 주고 ▲▼ 를 지운다.
        /// </summary>
        public void Show(double normal, double rare, double superRare, int drawsWithoutSuperRare, int pityThreshold)
        {
            pityDraws = drawsWithoutSuperRare;
            this.pityThreshold = pityThreshold;
            if (!hasTarget || !IsSameAsTarget(normal, rare, superRare))
            {
                KillTweens();
                SetTarget(normal, rare, superRare);
                CopyTargetToShown();
                ClearMarks();
            }

            Apply();
        }

        /// <summary>
        /// 확률이 바뀐 순간 (모듈 장착). 보이던 값에서 새 값으로 세어 바꾸고 바뀐 줄에 ▲▼ 를 띄운다.
        /// timeMultiplier = 연출 속도 배율 (0 = 바로 바꾸고 ▲▼ 만). 목표와 같으면 아무것도 하지 않는다.
        /// </summary>
        public void AnimateTo(double normal, double rare, double superRare, float timeMultiplier)
        {
            if (!hasTarget)
            {
                SetTarget(normal, rare, superRare);
                CopyTargetToShown();
                Apply();
                return;
            }

            if (IsSameAsTarget(normal, rare, superRare))
            {
                return;
            }

            KillTweens();

            // ▲▼ 는 이전 확률 → 새 확률. 숫자는 지금 보이는 값에서 출발한다 (앞 연출 도중이어도 뒤로 튀지 않게)
            for (int i = 0; i < GradeCount; i++)
            {
                rollStart[i] = shown[i];
            }

            double[] previous = (double[])target.Clone();
            SetTarget(normal, rare, superRare);
            for (int i = 0; i < GradeCount; i++)
            {
                marks[i] = BuildMark(previous[i], target[i]);
            }

            float seconds = settings.RollSeconds * Mathf.Max(0f, timeMultiplier);
            if (seconds <= 0f)
            {
                CopyTargetToShown();
                Apply();
                StartMarkTimer();
                return;
            }

            rollTween = DOTween.To(SetRoll, 0f, 1f, seconds)
                .SetEase(Ease.OutQuad)
                .OnComplete(() =>
                {
                    rollTween = null;
                    SetRoll(1f);
                    StartMarkTimer();
                });
            Apply();
        }

        /// <summary>바로 최종 값 · ▲▼ 없음 (새 런 시작 · 꺼질 때).</summary>
        public void CompleteNow()
        {
            KillTweens();
            if (hasTarget)
            {
                CopyTargetToShown();
            }

            ClearMarks();
            Apply();
        }

        private void SetRoll(float t)
        {
            for (int i = 0; i < GradeCount; i++)
            {
                shown[i] = rollStart[i] + (target[i] - rollStart[i]) * t;
            }

            Apply();
        }

        private void StartMarkTimer()
        {
            if (!HasMarks())
            {
                return;
            }

            if (settings.MarkSeconds <= 0f)
            {
                ClearMarks();
                Apply();
                return;
            }

            markTween = DOTween.To(_ => { }, 0f, 1f, settings.MarkSeconds)
                .OnComplete(() =>
                {
                    markTween = null;
                    ClearMarks();
                    Apply();
                });
        }

        private string BuildMark(double before, double after)
        {
            // 화면에 보이는 숫자(0.1 단위)가 바뀐 줄만 표시한다. 변화량도 보이는 두 숫자의 차이로 적는다 (반올림 경계에서 ▲0.0 이 나오지 않게)
            string beforeText = FormatNumber(before);
            string afterText = FormatNumber(after);
            if (beforeText == afterText)
            {
                return null;
            }

            double delta = double.Parse(afterText) - double.Parse(beforeText);
            bool up = delta > 0;
            Color color = up ? settings.UpColor : settings.DownColor;
            return "  <size=" + settings.MarkSizePercent + "%><color=#" + ColorUtility.ToHtmlStringRGB(color) + ">"
                + (up ? "▲" : "▼") + Math.Abs(delta).ToString("0.0") + "</color></size>";
        }

        private void Apply()
        {
            if (panelText == null)
            {
                return;
            }

            string text = "";
            for (int i = 0; i < GradeCount; i++)
            {
                text += GradeStyle.Colorize(Grades[i], Labels[i] + FormatNumber(shown[i]) + "%") + marks[i] + "\n";
            }

            panelText.text = text + FormatPity(pityDraws, pityThreshold);
        }

        public static string FormatPity(int drawsWithoutSuperRare, int threshold)
        {
            return drawsWithoutSuperRare >= threshold
                ? "다음 뽑기 SR 확정"
                : "SR 보장까지 " + (threshold - drawsWithoutSuperRare + 1) + "회";
        }

        private bool IsSameAsTarget(double normal, double rare, double superRare)
        {
            return target[0] == normal && target[1] == rare && target[2] == superRare;
        }

        private void SetTarget(double normal, double rare, double superRare)
        {
            target[0] = normal;
            target[1] = rare;
            target[2] = superRare;
            hasTarget = true;
        }

        private void CopyTargetToShown()
        {
            for (int i = 0; i < GradeCount; i++)
            {
                shown[i] = target[i];
            }
        }

        private bool HasMarks()
        {
            for (int i = 0; i < GradeCount; i++)
            {
                if (marks[i] != null)
                {
                    return true;
                }
            }

            return false;
        }

        private void ClearMarks()
        {
            for (int i = 0; i < GradeCount; i++)
            {
                marks[i] = null;
            }
        }

        private void KillTweens()
        {
            if (rollTween != null && rollTween.IsActive())
            {
                rollTween.Kill();
            }

            if (markTween != null && markTween.IsActive())
            {
                markTween.Kill();
            }

            rollTween = null;
            markTween = null;
        }

        private static string FormatNumber(double probability)
        {
            return (probability * 100.0).ToString("0.0");
        }
    }
}

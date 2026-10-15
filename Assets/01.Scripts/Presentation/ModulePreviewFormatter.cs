using System.Collections.Generic;
using Overclock.Core;

namespace Overclock.Presentation
{
    public static class ModulePreviewFormatter
    {
        private static readonly Grade[] Grades = { Grade.N, Grade.R, Grade.SR };

        public static string Format(ModulePreview preview, IReadOnlyList<PartData> parts, int nextQuota)
        {
            var lines = new List<string>();
            AddExpectedOutputLines(preview, parts, nextQuota, lines);

            int changeStart = lines.Count;
            AddProbabilityLines(preview, lines);
            AddOutputLines(preview, parts, lines);
            AddDrawsLine(preview, lines);

            if (lines.Count == changeStart)
            {
                lines.Add("변화 없음");
            }

            return string.Join("\n", lines);
        }

        private static void AddExpectedOutputLines(ModulePreview preview, IReadOnlyList<PartData> parts, int nextQuota, List<string> lines)
        {
            string beforeText = preview.ExpectedRoundOutputBefore(parts).ToString("0.0");
            string afterText = preview.ExpectedRoundOutputAfter(parts).ToString("0.0");
            string valueText = beforeText == afterText ? afterText : beforeText + " → " + afterText;

            lines.Add("다음 라운드 예상 생산량 " + valueText + " (할당량 " + nextQuota + ")");
            // 할당량에 도달해도 멈추지 않고 다 뽑았을 때의 평균이다. 실제 판정과 다르게 읽히지 않게 한다
            lines.Add("(전체 횟수 기준 · 보증 제외)");
        }

        private static void AddProbabilityLines(ModulePreview preview, List<string> lines)
        {
            preview.GetProbabilitiesBefore(out double beforeN, out double beforeR, out double beforeSR);
            preview.GetProbabilitiesAfter(out double afterN, out double afterR, out double afterSR);
            double[] before = { beforeN, beforeR, beforeSR };
            double[] after = { afterN, afterR, afterSR };

            // SR 부터 보여준다. 확률 모듈이 직접 바꾸는 값이고 N·R 은 비례 축소의 결과다
            bool changed = false;
            for (int i = Grades.Length - 1; i >= 0; i--)
            {
                string beforeText = FormatPercent(before[i]);
                string afterText = FormatPercent(after[i]);
                if (beforeText != afterText)
                {
                    lines.Add(Grades[i] + " 확률 " + beforeText + " → " + afterText);
                    changed = true;
                }
            }

            // 보증이 차서 다음 뽑기가 SR 확정이어도 이 표는 그대로다. 다음 뽑기 확률로 읽히지 않게 한다
            if (changed)
            {
                lines.Add("(모듈 적용 확률표 · 보증 확정 별도)");
            }
        }

        private static void AddOutputLines(ModulePreview preview, IReadOnlyList<PartData> parts, List<string> lines)
        {
            bool hasStreakBonus = false;
            foreach (Grade grade in Grades)
            {
                PartData part = FindRepresentativePart(parts, grade);
                if (part == null)
                {
                    continue;
                }

                int before = preview.OutputBefore(part, false);
                int after = preview.OutputAfter(part, false);
                if (before != after)
                {
                    lines.Add(grade + " 생산량 " + before + " → " + after);
                }

                if (preview.OutputAfter(part, true) != after)
                {
                    hasStreakBonus = true;
                }
            }

            // 장착은 효과를 더하기만 하므로 '후'에 연속 보너스가 없으면 '전'에도 없다. 그때 연속 시 값은 평상시와 같아 생략한다
            if (!hasStreakBonus)
            {
                return;
            }

            foreach (Grade grade in Grades)
            {
                PartData part = FindRepresentativePart(parts, grade);
                if (part == null)
                {
                    continue;
                }

                int before = preview.OutputBefore(part, true);
                int after = preview.OutputAfter(part, true);
                if (before != after)
                {
                    lines.Add("3연속 시 " + grade + " " + before + " → " + after);
                }
            }
        }

        private static void AddDrawsLine(ModulePreview preview, List<string> lines)
        {
            if (preview.DrawsPerRoundBefore != preview.DrawsPerRoundAfter)
            {
                lines.Add("다음 라운드부터 뽑기 " + preview.DrawsPerRoundBefore + " → " + preview.DrawsPerRoundAfter + "회");
            }
        }

        private static PartData FindRepresentativePart(IReadOnlyList<PartData> parts, Grade grade)
        {
            if (parts == null)
            {
                return null;
            }

            foreach (PartData part in parts)
            {
                if (part != null && part.Grade == grade)
                {
                    return part;
                }
            }

            return null;
        }

        private static string FormatPercent(double probability)
        {
            return (probability * 100.0).ToString("0.0") + "%";
        }
    }
}

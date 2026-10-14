using System.Collections.Generic;
using Overclock.Core;

namespace Overclock.Presentation
{
    public static class ModulePreviewFormatter
    {
        private static readonly Grade[] Grades = { Grade.N, Grade.R, Grade.SR };

        public static string Format(ModulePreview preview, IReadOnlyList<PartData> parts)
        {
            var lines = new List<string>();
            AddProbabilityLines(preview, lines);
            AddOutputLines(preview, parts, lines);
            AddDrawsLine(preview, lines);

            if (lines.Count == 0)
            {
                return "변화 없음";
            }

            return string.Join("\n", lines);
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
                int baseOutput = FindBaseOutput(parts, grade);
                if (baseOutput < 0)
                {
                    continue;
                }

                int before = preview.OutputBefore(grade, baseOutput, false);
                int after = preview.OutputAfter(grade, baseOutput, false);
                if (before != after)
                {
                    lines.Add(grade + " 생산량 " + before + " → " + after);
                }

                if (preview.OutputAfter(grade, baseOutput, true) != after)
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
                int baseOutput = FindBaseOutput(parts, grade);
                if (baseOutput < 0)
                {
                    continue;
                }

                int before = preview.OutputBefore(grade, baseOutput, true);
                int after = preview.OutputAfter(grade, baseOutput, true);
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

        private static int FindBaseOutput(IReadOnlyList<PartData> parts, Grade grade)
        {
            if (parts == null)
            {
                return -1;
            }

            foreach (PartData part in parts)
            {
                if (part != null && part.Grade == grade)
                {
                    return part.BaseOutput;
                }
            }

            return -1;
        }

        private static string FormatPercent(double probability)
        {
            return (probability * 100.0).ToString("0.0") + "%";
        }
    }
}

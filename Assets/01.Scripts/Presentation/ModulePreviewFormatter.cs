using System.Collections.Generic;
using Overclock.Core;

namespace Overclock.Presentation
{
    public static class ModulePreviewFormatter
    {
        private static readonly Grade[] Grades = { Grade.N, Grade.R, Grade.SR };
        private static readonly PartTag[] Tags = { PartTag.Electronic, PartTag.Mechanical, PartTag.Power, PartTag.None };

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

            // 희귀한 등급부터 SR → R → N 순서로 고정한다. 어떤 모듈이든 같은 자리에서 비교할 수 있게
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
            List<PartData> validParts = CollectParts(parts);

            AddOutputChangeLines(preview, validParts, false, lines);

            // 장착은 효과를 더하기만 하므로 '후'에 연속 보너스가 없으면 '전'에도 없다. 그때 연속 시 값은 평상시와 같아 생략한다
            if (HasStreakBonus(preview, validParts))
            {
                AddOutputChangeLines(preview, validParts, true, lines);
            }
        }

        // 바뀌는 묶음을 가장 크게 묶어 보여준다: 등급 전체가 같게 바뀌면 등급 한 줄, 아니면 태그별, 그래도 다르면 부품별.
        // 대표 부품 하나만 보면 태그 효과가 등급 전체에 걸린 것처럼 보이거나 줄이 빠진다
        private static void AddOutputChangeLines(ModulePreview preview, List<PartData> parts, bool streakTriggered, List<string> lines)
        {
            foreach (Grade grade in Grades)
            {
                List<PartData> gradeParts = FilterParts(parts, grade, null);
                if (gradeParts.Count == 0)
                {
                    continue;
                }

                if (HasSameChange(preview, gradeParts, streakTriggered))
                {
                    AddChangeLine(preview, gradeParts, grade.ToString(), streakTriggered, lines);
                    continue;
                }

                foreach (PartTag tag in Tags)
                {
                    List<PartData> tagParts = FilterParts(gradeParts, grade, tag);
                    if (tagParts.Count == 0)
                    {
                        continue;
                    }

                    if (HasSameChange(preview, tagParts, streakTriggered))
                    {
                        AddChangeLine(preview, tagParts, grade + " " + TagLabel(tag), streakTriggered, lines);
                        continue;
                    }

                    foreach (PartData part in tagParts)
                    {
                        AddChangeLine(preview, new List<PartData> { part }, grade + " " + part.DisplayName, streakTriggered, lines);
                    }
                }
            }
        }

        // group 은 같은 값으로 바뀌는 부품 묶음이다 (HasSameChange 를 통과한 것)
        private static void AddChangeLine(ModulePreview preview, List<PartData> group, string label, bool streakTriggered, List<string> lines)
        {
            int before = preview.OutputBefore(group[0], streakTriggered);
            int after = preview.OutputAfter(group[0], streakTriggered);
            if (before == after)
            {
                return;
            }

            // 연속 보너스가 없는 묶음의 '3연속 시' 값은 평상시 줄과 같다. 같은 내용을 두 번 쓰면 카드가 길어져 잘린다
            if (streakTriggered && !HasStreakBonus(preview, group))
            {
                return;
            }

            if (streakTriggered)
            {
                lines.Add("3연속 시 " + label + " " + before + " → " + after);
            }
            else
            {
                lines.Add(label + " 생산량 " + before + " → " + after);
            }
        }

        private static bool HasStreakBonus(ModulePreview preview, List<PartData> parts)
        {
            foreach (PartData part in parts)
            {
                if (preview.OutputAfter(part, true) != preview.OutputAfter(part, false))
                {
                    return true;
                }
            }

            return false;
        }

        private static bool HasSameChange(ModulePreview preview, List<PartData> parts, bool streakTriggered)
        {
            int before = preview.OutputBefore(parts[0], streakTriggered);
            int after = preview.OutputAfter(parts[0], streakTriggered);
            for (int i = 1; i < parts.Count; i++)
            {
                if (preview.OutputBefore(parts[i], streakTriggered) != before || preview.OutputAfter(parts[i], streakTriggered) != after)
                {
                    return false;
                }
            }

            return true;
        }

        private static List<PartData> CollectParts(IReadOnlyList<PartData> parts)
        {
            var result = new List<PartData>();
            if (parts == null)
            {
                return result;
            }

            foreach (PartData part in parts)
            {
                if (part != null)
                {
                    result.Add(part);
                }
            }

            return result;
        }

        private static List<PartData> FilterParts(List<PartData> parts, Grade grade, PartTag? tag)
        {
            var result = new List<PartData>();
            foreach (PartData part in parts)
            {
                if (part.Grade == grade && (tag == null || part.Tag == tag.Value))
                {
                    result.Add(part);
                }
            }

            return result;
        }

        private static string TagLabel(PartTag tag)
        {
            switch (tag)
            {
                case PartTag.Electronic:
                    return "전자";
                case PartTag.Mechanical:
                    return "기계";
                case PartTag.Power:
                    return "동력";
                default:
                    return "태그 없음";
            }
        }

        private static void AddDrawsLine(ModulePreview preview, List<string> lines)
        {
            if (preview.DrawsPerRoundBefore != preview.DrawsPerRoundAfter)
            {
                lines.Add("다음 라운드부터 뽑기 " + preview.DrawsPerRoundBefore + " → " + preview.DrawsPerRoundAfter + "회");
            }
        }

        private static string FormatPercent(double probability)
        {
            return (probability * 100.0).ToString("0.0") + "%";
        }
    }
}

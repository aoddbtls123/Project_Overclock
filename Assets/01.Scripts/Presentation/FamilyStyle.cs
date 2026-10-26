using Overclock.Core;
using UnityEngine;

namespace Overclock.Presentation
{
    // 계열은 등급 기준이라 색도 등급 색을 그대로 쓴다 (물량 = N · 안정 = R · 대박 = SR). 범용은 표시하지 않는다
    public static class FamilyStyle
    {
        public static bool IsShown(ModuleFamily family)
        {
            return family != ModuleFamily.General;
        }

        public static string GetLabel(ModuleFamily family)
        {
            switch (family)
            {
                case ModuleFamily.Volume:
                    return "물량 계열";
                case ModuleFamily.Stable:
                    return "안정 계열";
                case ModuleFamily.Jackpot:
                    return "대박 계열";
                default:
                    return "";
            }
        }

        public static Color GetColor(ModuleFamily family)
        {
            switch (family)
            {
                case ModuleFamily.Volume:
                    return GradeStyle.GetColor(Grade.N);
                case ModuleFamily.Stable:
                    return GradeStyle.GetColor(Grade.R);
                case ModuleFamily.Jackpot:
                    return GradeStyle.GetColor(Grade.SR);
                default:
                    return Color.white;
            }
        }

        public static string Colorize(ModuleFamily family)
        {
            if (!IsShown(family))
            {
                return "";
            }

            return "<color=#" + ColorUtility.ToHtmlStringRGB(GetColor(family)) + ">" + GetLabel(family) + "</color>";
        }
    }
}

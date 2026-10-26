using Overclock.Core;
using UnityEngine;

namespace Overclock.Presentation
{
    public static class GradeStyle
    {
        private static readonly Color NormalColor = new Color32(0x9A, 0xA0, 0xA6, 0xFF);
        private static readonly Color RareColor = new Color32(0x4A, 0x90, 0xE2, 0xFF);
        private static readonly Color SuperRareColor = new Color32(0xFF, 0xB0, 0x20, 0xFF);

        public static Color GetColor(Grade grade)
        {
            switch (grade)
            {
                case Grade.R:
                    return RareColor;
                case Grade.SR:
                    return SuperRareColor;
                default:
                    return NormalColor;
            }
        }

        public static string GetStars(Grade grade)
        {
            switch (grade)
            {
                case Grade.R:
                    return "★★";
                case Grade.SR:
                    return "★★★";
                default:
                    return "★";
            }
        }

        public static string Colorize(Grade grade, string text)
        {
            return "<color=#" + ColorUtility.ToHtmlStringRGB(GetColor(grade)) + ">" + text + "</color>";
        }
    }
}

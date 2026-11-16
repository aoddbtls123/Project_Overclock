using System;
using System.Collections.Generic;
using System.Globalization;

namespace Overclock.Core
{
    /// <summary>
    /// QA 고정 시드 실행 인자. 예) Overclock.exe -seed 12345
    /// </summary>
    public static class RunSeedOption
    {
        public const string ArgumentName = "-seed";

        // 화면의 자동 시드 · Inspector 칸과 같은 범위. 로그의 시드를 그대로 다시 넣을 수 있어야 함
        public const uint MaxSeed = int.MaxValue;

        /// <summary>
        /// 인자에 -seed 가 있고 값이 1 ~ int.MaxValue 이면 true. 없으면 false 와 error null,
        /// 있는데 값이 잘못됐으면 false 와 이유. 여러 번 있으면 처음 것만 봄.
        /// </summary>
        public static bool TryFind(IReadOnlyList<string> args, out uint seed, out string error)
        {
            seed = 0;
            error = null;
            if (args == null)
            {
                return false;
            }

            for (int i = 0; i < args.Count; i++)
            {
                if (args[i] != ArgumentName)
                {
                    continue;
                }

                if (i + 1 >= args.Count)
                {
                    error = ArgumentName + " 뒤에 시드 값이 없습니다.";
                    return false;
                }

                string text = args[i + 1];
                // 부호 · 공백 · 소수점을 받지 않음. 로그에 찍힌 숫자 그대로만 받음
                if (!uint.TryParse(text, NumberStyles.None, CultureInfo.InvariantCulture, out uint value) || value < 1 || value > MaxSeed)
                {
                    error = ArgumentName + " 값은 1 ~ " + MaxSeed + " 사이 정수여야 합니다: " + text;
                    return false;
                }

                seed = value;
                return true;
            }

            return false;
        }
    }
}

using System;
using Random = Unity.Mathematics.Random;

namespace Overclock.Core
{
    public sealed class RandomService
    {
        // readonly 금지: struct라서 readonly면 복사본만 갱신되어 같은 값이 반복된다
        private Random random;

        public RandomService(uint seed)
        {
            if (seed == uint.MaxValue)
            {
                throw new ArgumentException("uint.MaxValue 시드는 난수 상태 0을 만들어 사용할 수 없습니다.", nameof(seed));
            }

            random = Random.CreateFromIndex(seed);
        }

        public uint State => random.state;

        public void RestoreState(uint state)
        {
            if (state == 0)
            {
                throw new ArgumentException("난수 상태 0은 유효하지 않습니다.", nameof(state));
            }

            random.state = state;
        }

        public double NextDouble()
        {
            return random.NextDouble();
        }
    }
}
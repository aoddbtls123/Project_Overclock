using System;

namespace Overclock.Core
{
    /// <summary>런 안 화폐 (크레딧). 런마다 0 에서 시작 · 이월 · 이자 없음. 난수를 쓰지 않는다.</summary>
    public sealed class CreditWallet
    {
        private int balance;

        public int Balance => balance;

        public void Earn(int amount)
        {
            if (amount < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(amount));
            }

            // 무한모드 후반 초과분이 커도 음수로 넘어가지 않게 int 최댓값에서 멈춘다
            balance = (int)Math.Min((long)balance + amount, int.MaxValue);
        }

        public bool CanAfford(int cost)
        {
            if (cost < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(cost));
            }

            return balance >= cost;
        }

        public bool TrySpend(int cost)
        {
            if (!CanAfford(cost))
            {
                return false;
            }

            balance -= cost;
            return true;
        }

        // 세이브 복원 전용
        internal void Restore(int savedBalance)
        {
            if (savedBalance < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(savedBalance));
            }

            balance = savedBalance;
        }
    }
}

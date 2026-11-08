using System;
using System.Collections.Generic;

namespace Overclock.Core
{
    public static class RunSimulator
    {
        // 선택 시드 = 게임 시드 XOR 이 값. 최상위 비트가 0 이라 게임 시드가 1 ~ int.MaxValue(화면 규칙)이면
        // 선택 시드도 그 범위에 있어 uint.MaxValue 가 되지 않는다. 바꾸면 이전 CSV 의 무작위 런을 재현할 수 없다
        private const uint ChoiceSeedSalt = 0x2545F491u;

        public static RunRecord Simulate(uint seed, IReadOnlyList<PartData> parts, IReadOnlyList<ModuleData> modules, IModuleStrategy strategy)
        {
            return SimulateWithShop(seed, parts, modules, strategy, null, null, SimulationPlayPolicy.None).Run;
        }

        /// <summary>
        /// 상점 · 오버클럭 · 다시 받기까지 쓰는 판. shop 이 null 이면 상점 없음 (shopStrategy 는 안 씀).
        /// 상점 + 안 삼 + 플레이 없음 = Simulate 와 같은 판이다 (상점은 난수를 쓰지 않음).
        /// </summary>
        public static ShopRunRecord SimulateWithShop(uint seed, IReadOnlyList<PartData> parts, IReadOnlyList<ModuleData> modules, IModuleStrategy strategy, ShopSettings shop, IShopStrategy shopStrategy, SimulationPlayPolicy play)
        {
            if (strategy == null)
            {
                throw new ArgumentNullException(nameof(strategy));
            }

            if (play == null)
            {
                throw new ArgumentNullException(nameof(play));
            }

            if (shop != null && shopStrategy == null)
            {
                throw new ArgumentNullException(nameof(shopStrategy));
            }

            RunSession session = RunSession.Create(seed, parts, modules, RunMode.Normal, shop);
            RunManager runManager = session.RunManager;

            // 플레이어의 선택은 게임 난수를 쓰지 않는다. 런마다 게임 시드로 정해지므로 CSV 의 시드 하나로 그 런을 재현할 수 있다
            var choiceRandom = new RandomService(seed ^ ChoiceSeedSalt);
            var visits = new List<ShopVisitRecord>();
            int rerolls = 0;
            int overclocks = 0;

            while (true)
            {
                if (runManager.State == RunState.Drawing)
                {
                    if (play.ShouldActivateOverclock(runManager, session.GachaEngine))
                    {
                        runManager.ActivateOverclock();
                        overclocks++;
                    }

                    runManager.Draw();
                    continue;
                }

                if (runManager.State != RunState.RoundCleared)
                {
                    break;
                }

                if (session.IsShopOpen)
                {
                    visits.Add(Visit(session, shopStrategy, choiceRandom));
                    session.LeaveShop();
                    continue;
                }

                // 게임 화면과 같은 길: 돌파 1회당 제안을 열고 (OpenOffer) → 고른 모듈을 장착하며 진행 (SelectModule).
                // 고르지 않는 전략도 제안은 연다. 게임 난수 소비를 화면과 맞추기 위해서다
                IReadOnlyList<ModuleData> offer = session.OpenOffer();
                while (play.ShouldReroll(session, offer, parts))
                {
                    offer = session.RerollOffer();
                    rerolls++;
                }

                ModuleData choice = strategy.Choose(offer, runManager, choiceRandom);
                if (choice == null)
                {
                    session.DeclineOffer();
                    continue;
                }

                if (!Contains(offer, choice))
                {
                    throw new InvalidOperationException(strategy.Name + " 전략이 제안에 없는 모듈을 골랐습니다.");
                }

                session.SelectModule(choice);
            }

            var run = new RunRecord(seed, runManager.CurrentRound, runManager.State == RunState.RunCleared, new List<ModuleData>(runManager.EquippedModules));
            return new ShopRunRecord(run, visits, session.Wallet.Balance, rerolls, overclocks);
        }

        // 산 것은 전략의 말이 아니라 세션 상태의 차이로 센다
        private static ShopVisitRecord Visit(RunSession session, IShopStrategy shopStrategy, RandomService choiceRandom)
        {
            int creditsOnEntry = session.Wallet.Balance;
            int available = 0;
            foreach (ShopItemData item in session.ShopItems)
            {
                if (session.GetItemStatus(item) == ShopItemStatus.Available)
                {
                    available++;
                }
            }

            bool hadSupercharger = session.HasSupercharger;
            int sparesBefore = session.SpareCards;
            // 대체 모듈을 고르지 않은 전략은 다음 LeaveShop 이 거부한다
            shopStrategy.Visit(session, choiceRandom);
            return new ShopVisitRecord(
                session.RunManager.CurrentRound,
                creditsOnEntry,
                available,
                session.HasSupercharger && !hadSupercharger,
                session.SpareCards - sparesBefore,
                session.DecomposedThisVisit,
                session.Wallet.Balance);
        }

        private static bool Contains(IReadOnlyList<ModuleData> offer, ModuleData module)
        {
            for (int i = 0; i < offer.Count; i++)
            {
                if (offer[i] == module)
                {
                    return true;
                }
            }

            return false;
        }

        public static List<RunRecord> SimulateMany(uint firstSeed, int runCount, IReadOnlyList<PartData> parts, IReadOnlyList<ModuleData> modules, IModuleStrategy strategy)
        {
            if (runCount < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(runCount));
            }

            var records = new List<RunRecord>(runCount);
            for (int i = 0; i < runCount; i++)
            {
                records.Add(Simulate(firstSeed + (uint)i, parts, modules, strategy));
            }

            return records;
        }
    }
}

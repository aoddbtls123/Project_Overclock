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
            if (strategy == null)
            {
                throw new ArgumentNullException(nameof(strategy));
            }

            RunSession session = RunSession.Create(seed, parts, modules);
            RunManager runManager = session.RunManager;

            // 플레이어의 선택은 게임 난수를 쓰지 않는다. 런마다 게임 시드로 정해지므로 CSV 의 시드 하나로 그 런을 재현할 수 있다
            var choiceRandom = new RandomService(seed ^ ChoiceSeedSalt);

            while (true)
            {
                if (runManager.State == RunState.Drawing)
                {
                    runManager.Draw();
                    continue;
                }

                if (runManager.State != RunState.RoundCleared)
                {
                    break;
                }

                // 게임 화면과 같은 길: 돌파 1회당 제안을 열고 (OpenOffer) → 고른 모듈을 장착하며 진행 (SelectModule).
                // 고르지 않는 전략도 제안은 연다. 게임 난수 소비를 화면과 맞추기 위해서다
                IReadOnlyList<ModuleData> offer = session.OpenOffer();
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

            return new RunRecord(seed, runManager.CurrentRound, runManager.State == RunState.RunCleared, new List<ModuleData>(runManager.EquippedModules));
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

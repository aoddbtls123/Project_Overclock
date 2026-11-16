using System;
using System.Collections.Generic;

namespace Overclock.Core
{
    public static class RunSimulator
    {
        // 선택 시드 = 게임 시드 XOR 이 값. 최상위 비트가 0 이라 게임 시드가 1 ~ int.MaxValue(화면 규칙)이면
        // 선택 시드도 그 범위에 있어 uint.MaxValue 가 되지 않음. 바꾸면 이전 CSV 의 무작위 런을 재현할 수 없음
        private const uint ChoiceSeedSalt = 0x2545F491u;

        public static RunRecord Simulate(uint seed, IReadOnlyList<PartData> parts, IReadOnlyList<ModuleData> modules, IModuleStrategy strategy)
        {
            if (strategy == null)
            {
                throw new ArgumentNullException(nameof(strategy));
            }

            RunSession session = RunSession.Create(seed, parts, modules);
            RunManager runManager = session.RunManager;

            // 플레이어의 선택은 게임 난수를 쓰지 않음. 런마다 게임 시드로 정해지므로 CSV 의 시드 하나로 그 런을 재현할 수 있음
            var choiceRandom = new RandomService(seed ^ ChoiceSeedSalt);
            var equippedModules = new List<ModuleData>();

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

                // 게임 화면과 같은 순서: 돌파 1회당 제안 1번 → 장착 → 라운드 진행.
                // 고르지 않는 전략도 제안은 만듦. 게임 난수 소비를 화면과 맞추기 위해서임
                List<ModuleData> offer = session.ModuleOffer.CreateOffer();
                ModuleData choice = strategy.Choose(offer, runManager, choiceRandom);
                if (choice != null)
                {
                    if (!offer.Contains(choice))
                    {
                        throw new InvalidOperationException(strategy.Name + " 전략이 제안에 없는 모듈을 골랐습니다.");
                    }

                    runManager.EquipModule(choice);
                    equippedModules.Add(choice);
                }

                runManager.AdvanceRound();
            }

            return new RunRecord(seed, runManager.CurrentRound, runManager.State == RunState.RunCleared, equippedModules);
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

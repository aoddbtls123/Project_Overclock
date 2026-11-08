using System;
using System.Collections.Generic;
using NUnit.Framework;
using Overclock.Core;
using UnityEngine;

namespace Overclock.Tests
{
    // 상점 하네스 (11/08 사전고정 v3): 구매 정책 3 (안 삼 · 무작위 · 탐욕) · 플레이 정책 (최적 오버클럭 · 기대 이득 기준 다시 받기) · 방문 기록.
    // 기대값은 기획 숫자 리터럴 (할당량 R1 8 · R2 12 · 생산량 11 판에서 R2 상점 크레딧 23)
    public class ShopSimulationTests
    {
        private const int Wage = 5;

        private readonly List<UnityEngine.Object> createdObjects = new List<UnityEngine.Object>();

        private PartData[] flatParts;
        private ModuleData[] zeroModules;
        private PartData[] mixedParts;
        private ModuleData[] realModules;
        private ShopSettings shop;

        [SetUp]
        public void CreateData()
        {
            flatParts = new[]
            {
                CreatePart("n11", Grade.N, 11, PartTag.Mechanical),
                CreatePart("r11", Grade.R, 11, PartTag.Power),
                CreatePart("sr11", Grade.SR, 11, PartTag.Power),
            };
            zeroModules = new[]
            {
                CreateModule("p0", ModuleAxis.Probability, ModifierType.SuperRareChance, 0f),
                CreateModule("m0", ModuleAxis.Multiplier, ModifierType.OutputMultiplier, 0f),
                CreateModule("c0", ModuleAxis.Conditional, ModifierType.StreakBonus, 0f),
                CreateModule("d0", ModuleAxis.Draws, ModifierType.ExtraDraws, 0f),
            };
            mixedParts = new[]
            {
                CreatePart("볼트", Grade.N, 1, PartTag.Mechanical),
                CreatePart("전선", Grade.N, 1, PartTag.Electronic),
                CreatePart("코일", Grade.R, 3, PartTag.Power),
                CreatePart("기어", Grade.R, 3, PartTag.Mechanical),
                CreatePart("코어", Grade.SR, 10, PartTag.Power),
            };
            realModules = new[]
            {
                CreateModule("sr_up", ModuleAxis.Probability, ModifierType.SuperRareChance, 0.05f),
                CreateModule("r_up", ModuleAxis.Probability, ModifierType.RareChance, 0.1f),
                CreateModule("coil", ModuleAxis.Multiplier, ModifierType.OutputMultiplier, 0.3f),
                CreateModule("flat", ModuleAxis.Multiplier, ModifierType.FlatBonus, 1f),
                CreateModule("sync", ModuleAxis.Conditional, ModifierType.StreakBonus, 10f),
                CreateModule("extra", ModuleAxis.Draws, ModifierType.ExtraDraws, 2f),
                CreateModule("carry", ModuleAxis.Draws, ModifierType.CarryOverDraws, 5f),
            };
            shop = CreateShop(Wage, 20, 4, 3, 10);
        }

        [TearDown]
        public void DestroyCreatedObjects()
        {
            foreach (UnityEngine.Object created in createdObjects)
            {
                UnityEngine.Object.DestroyImmediate(created);
            }

            createdObjects.Clear();
        }

        // ── S0 ──

        [Test]
        public void NoPurchaseWithoutPlay_SameRunsAsHarnessWithoutShop()
        {
            var strategies = new IModuleStrategy[] { new NoModuleStrategy(), new RandomModuleStrategy(), new GreedyModuleStrategy(mixedParts) };
            foreach (IModuleStrategy strategy in strategies)
            {
                for (uint seed = 1; seed <= 200; seed++)
                {
                    RunRecord plain = RunSimulator.Simulate(seed, mixedParts, realModules, strategy);
                    ShopRunRecord withShop = RunSimulator.SimulateWithShop(seed, mixedParts, realModules, strategy, shop, new NoPurchaseShopStrategy(), SimulationPlayPolicy.None);
                    string at = strategy.Name + " seed=" + seed;
                    Assert.AreEqual(plain.FinalRound, withShop.Run.FinalRound, at);
                    Assert.AreEqual(plain.Cleared, withShop.Run.Cleared, at);
                    CollectionAssert.AreEqual(plain.EquippedModules, withShop.Run.EquippedModules, at);
                    Assert.AreEqual((plain.FinalRound - 1) / 2, withShop.Visits.Count, at + " 방문 수 = 끝난 라운드 앞의 짝수 라운드");
                    Assert.AreEqual(0, withShop.RerollsUsed, at);
                    Assert.AreEqual(0, withShop.OverclocksUsed, at);
                }
            }
        }

        // ── 구매 정책 ──

        [Test]
        public void Greedy_SuperchargerThenDecomposeThenSparesWithRest_RecordedFromSessionState()
        {
            ShopRunRecord record = RunSimulator.SimulateWithShop(1, flatParts, zeroModules, new GreedyModuleStrategy(flatParts), shop, new GreedyShopStrategy(flatParts), SimulationPlayPolicy.None);
            ShopVisitRecord first = record.Visits[0];
            Assert.AreEqual(2, first.Round);
            Assert.AreEqual(23, first.CreditsOnEntry);
            Assert.AreEqual(3, first.AvailableOnEntry, "과급기 20 · 예비 카드 4 · 분해 3 모두 23 이하");
            Assert.IsTrue(first.BoughtSupercharger);
            Assert.IsTrue(first.Decomposed, "효과 0 모듈 손실 0 < 8 + 7");
            Assert.AreEqual(2, first.SpareCardsBought, "23 − 20 − 3 + 10 = 10 → 4 짜리 2개");
            Assert.AreEqual(2, first.CreditsOnLeave);

            // R3 +5+5 · R4 +5+9 → 26 · 분해 −3+10 → 33 · 예비 카드 8개 → 1
            ShopVisitRecord second = record.Visits[1];
            Assert.AreEqual(4, second.Round);
            Assert.AreEqual(26, second.CreditsOnEntry);
            Assert.IsFalse(second.BoughtSupercharger, "매진");
            Assert.AreEqual(8, second.SpareCardsBought, "이번 방문에 산 수");
            Assert.AreEqual(1, second.CreditsOnLeave);
        }

        [Test]
        public void Greedy_DecomposesWhenLossBelowRerollThresholdPlusNetRefund()
        {
            // 모듈마다 FlatBonus +1 → 빼면 다음 라운드 기대 생산량 −10 (10뽑기)
            ModuleData[] plusOne =
            {
                CreateModule("f1", ModuleAxis.Probability, ModifierType.FlatBonus, 1f),
                CreateModule("f2", ModuleAxis.Multiplier, ModifierType.FlatBonus, 1f),
                CreateModule("f3", ModuleAxis.Conditional, ModifierType.FlatBonus, 1f),
                CreateModule("f4", ModuleAxis.Draws, ModifierType.FlatBonus, 1f),
            };
            ShopRunRecord profit = RunSimulator.SimulateWithShop(1, flatParts, plusOne, new GreedyModuleStrategy(flatParts), shop, new GreedyShopStrategy(flatParts), SimulationPlayPolicy.None);
            Assert.IsTrue(profit.Visits[0].Decomposed, "10 < 8 + (10 − 3)");

            ShopSettings noProfit = CreateShop(Wage, 20, 4, 3, 3);
            ShopRunRecord none = RunSimulator.SimulateWithShop(1, flatParts, plusOne, new GreedyModuleStrategy(flatParts), noProfit, new GreedyShopStrategy(flatParts), SimulationPlayPolicy.None);
            foreach (ShopVisitRecord visit in none.Visits)
            {
                Assert.IsFalse(visit.Decomposed, "R" + visit.Round + ": 10 ≥ 8 + 0");
            }
        }

        [Test]
        public void Greedy_DecomposesModuleThatLosesLeastOutput_AndPicksBestReplacement()
        {
            ModuleData big = CreateModule("big", ModuleAxis.Multiplier, ModifierType.OutputMultiplier, 1f);
            ModuleData[] pool = { zeroModules[0], big, zeroModules[2], zeroModules[3] };
            // 효과 0 모듈을 먼저 (칸 0) · big 을 나중에 (칸 1) 단다 → "늘 앞 칸" · "늘 마지막 칸" 둘 다 틀리게.
            // 대체 제안에 big 이 첫 장이 아닌 자리로 나오는 판을 찾는다 → "첫 장" 도 틀리게
            for (uint seed = 1; seed <= 500; seed++)
            {
                RunSession mirror = OpenR2ShopWithBigSecond(seed, pool, big);
                if (mirror == null)
                {
                    continue;
                }

                mirror.DecomposeModule(0);
                int bigAt = IndexOf(mirror.ReplacementOffer, big);
                if (bigAt <= 0)
                {
                    continue;
                }

                RunSession session = OpenR2ShopWithBigSecond(seed, pool, big);
                new GreedyShopStrategy(flatParts).Visit(session, new RandomService(1));
                Assert.IsTrue(session.DecomposedThisVisit, "(전제) 분해함");
                Assert.IsFalse(session.IsChoosingReplacement, "대체 카드도 고름");
                Assert.AreEqual(2, session.RunManager.EquippedModules.Count, "1 개 빼고 1 개 장착");
                Assert.AreSame(big, session.RunManager.EquippedModules[0], "효과 0 모듈 (칸 0) 을 뺀다");
                Assert.AreSame(big, session.RunManager.EquippedModules[1], "대체 제안의 " + bigAt + "번째 big = 기대 생산량 최대");
                return;
            }

            Assert.Fail("조건에 맞는 시드가 없음");
        }

        // R1 효과 0 모듈 · R2 big 을 골라 R2 상점에 들어간 판 (R2 제안에 big 이 없으면 null)
        private RunSession OpenR2ShopWithBigSecond(uint seed, ModuleData[] pool, ModuleData big)
        {
            RunSession session = RunSession.Create(seed, flatParts, pool, RunMode.Normal, shop);
            DrawUntilNotDrawing(session);
            session.SelectModule(FirstOther(session.OpenOffer(), big));
            DrawUntilNotDrawing(session);
            if (!Contains(session.OpenOffer(), big))
            {
                return null;
            }

            session.SelectModule(big);
            Assert.IsTrue(session.IsShopOpen, "(전제) R2 상점");
            return session;
        }

        private static int IndexOf(IReadOnlyList<ModuleData> offer, ModuleData module)
        {
            for (int i = 0; i < offer.Count; i++)
            {
                if (offer[i] == module)
                {
                    return i;
                }
            }

            return -1;
        }

        [Test]
        public void Greedy_KeepsModulesThatLoseTooMuch()
        {
            ModuleData[] valuable =
            {
                CreateModule("f1", ModuleAxis.Probability, ModifierType.FlatBonus, 2f),
                CreateModule("f2", ModuleAxis.Multiplier, ModifierType.FlatBonus, 2f),
                CreateModule("f3", ModuleAxis.Conditional, ModifierType.FlatBonus, 2f),
                CreateModule("f4", ModuleAxis.Draws, ModifierType.FlatBonus, 2f),
            };
            ShopRunRecord record = RunSimulator.SimulateWithShop(1, flatParts, valuable, new GreedyModuleStrategy(flatParts), shop, new GreedyShopStrategy(flatParts), SimulationPlayPolicy.None);
            Assert.Greater(record.Visits.Count, 0, "(전제)");
            foreach (ShopVisitRecord visit in record.Visits)
            {
                Assert.IsFalse(visit.Decomposed, "R" + visit.Round + ": 빼면 기대 생산량 −20 (10뽑기 × +2) ≥ 8 + 7");
            }
        }

        [Test]
        public void RandomPurchase_OnlyBuysAvailableItems_AndIsReproducible()
        {
            for (uint seed = 1; seed <= 200; seed++)
            {
                ShopRunRecord a = RunSimulator.SimulateWithShop(seed, mixedParts, realModules, new RandomModuleStrategy(), shop, new RandomShopStrategy(), SimulationPlayPolicy.None);
                ShopRunRecord b = RunSimulator.SimulateWithShop(seed, mixedParts, realModules, new RandomModuleStrategy(), shop, new RandomShopStrategy(), SimulationPlayPolicy.None);
                Assert.AreEqual(a.Run.FinalRound, b.Run.FinalRound, "seed=" + seed);
                Assert.AreEqual(a.FinalCredits, b.FinalCredits, "seed=" + seed);
                Assert.AreEqual(a.Visits.Count, b.Visits.Count);
                foreach (ShopVisitRecord visit in a.Visits)
                {
                    Assert.GreaterOrEqual(visit.CreditsOnLeave, 0);
                    if (visit.AvailableOnEntry == 0)
                    {
                        Assert.AreEqual(visit.CreditsOnEntry, visit.CreditsOnLeave, "살 수 있는 것이 없으면 그대로 나감");
                    }
                }
            }
        }

        [Test]
        public void RandomPurchase_BuysSomethingAcrossSeeds()
        {
            int bought = 0;
            int left = 0;
            for (uint seed = 1; seed <= 200; seed++)
            {
                ShopRunRecord record = RunSimulator.SimulateWithShop(seed, flatParts, zeroModules, new RandomModuleStrategy(), shop, new RandomShopStrategy(), SimulationPlayPolicy.None);
                foreach (ShopVisitRecord visit in record.Visits)
                {
                    if (visit.BoughtSupercharger || visit.Decomposed || visit.SpareCardsBought > 0)
                    {
                        bought++;
                    }
                    else
                    {
                        left++;
                    }
                }
            }

            Assert.Greater(bought, 0, "살 때도 있고");
            Assert.Greater(left, 0, "안 사고 나갈 때도 있다");
        }

        [Test]
        public void RandomPurchase_ChoosesEachItemAndLeavingAboutEvenly()
        {
            // 품목 셋 다 20 · R2 상점 23 → 한 번만 살 수 있다 → 첫 선택이 그대로 남는다 (나가기 포함 각 약 1/4)
            ShopSettings oneBuy = CreateShop(Wage, 20, 20, 20, 0);
            var counts = new int[4];
            for (uint choiceSeed = 1; choiceSeed <= 400; choiceSeed++)
            {
                RunSession session = RunSession.Create(1, flatParts, zeroModules, RunMode.Normal, oneBuy);
                for (int r = 0; r < 2; r++)
                {
                    DrawUntilNotDrawing(session);
                    session.SelectModule(session.OpenOffer()[0]);
                }

                Assert.AreEqual(23, session.Wallet.Balance, "(전제)");
                new RandomShopStrategy().Visit(session, new RandomService(choiceSeed));
                if (session.HasSupercharger)
                {
                    counts[0]++;
                }
                else if (session.SpareCards > 0)
                {
                    counts[1]++;
                }
                else if (session.DecomposedThisVisit)
                {
                    counts[2]++;
                }
                else
                {
                    counts[3]++;
                }
            }

            string[] names = { "과급기", "예비 카드", "분해", "나가기" };
            for (int i = 0; i < 4; i++)
            {
                Assert.Greater(counts[i], 60, names[i] + " " + counts[i] + "/400 (15% 초과)");
            }
        }

        // ── 플레이 정책 D ──

        [Test]
        public void OptimalOverclock_NeverWhenOnlyNormalParts()
        {
            // N 만 나오면 오버클럭 = 생산량 0 → 켜서 나아지는 경우가 없다
            RunManager run = CreateFixedGradeRun(1.0, 0.0, 0.0, 10, 1, out GachaEngine engine);
            var planner = new OverclockPlanner(FixedParts);
            while (run.State == RunState.Drawing)
            {
                Assert.IsFalse(planner.ShouldActivate(run, engine), "남은 " + run.DrawsRemaining);
                run.Draw();
            }
        }

        [Test]
        public void OptimalOverclock_OnlyWhenItRaisesPassChance_AndSeesSuperchargerBonus()
        {
            // R 만 · 생산량 3 (오버 6 · 과급기 9) · 라운드당 6뽑기 · 할당량 R1 8 · R2 12 · R3 17 · R4 24 · R5 35
            RunManager plain = CreateFixedGradeRun(0.0, 1.0, 0.0, 6, 3, out GachaEngine plainEngine);
            var planner = new OverclockPlanner(FixedParts);
            Assert.IsFalse(planner.ShouldActivate(plain, plainEngine), "R1: 켜지 않아도 확실히 통과 (켜도 같음 → 안 켬)");
            PlayRound(plain, plainEngine, planner);
            PlayRound(plain, plainEngine, planner);
            PlayRound(plain, plainEngine, planner);
            Assert.AreEqual(4, plain.CurrentRound);
            // R4 (24): 안 켜면 18. 남은 3 이나 2 에서 켜도 27 / 24 로 통과 → 처음에는 미룬다 (같으면 안 켬)
            Assert.IsFalse(planner.ShouldActivate(plain, plainEngine), "R4 처음: 나중에 켜도 확실히 통과");
            PlayRound(plain, plainEngine, planner);
            Assert.AreEqual(5, plain.CurrentRound, "R4 통과 = 어딘가에서 켰다");
            Assert.AreEqual(0.0, planner.PassChance(plain, plainEngine, true), 1e-12, "R5 +1: 켜도 27 < 35");
            PlayRound(plain, plainEngine, planner);
            Assert.AreEqual(RunState.RunFailed, plain.State);

            // 같은 판에 과급기 (+2): R5 = 9 × 3 + 3 × 3 = 36 → 통과. 같은 planner (표 열쇠에 배수가 들어가야 한다)
            RunManager boosted = CreateFixedGradeRun(0.0, 1.0, 0.0, 6, 3, out GachaEngine boostedEngine);
            for (int r = 0; r < 4; r++)
            {
                PlayRound(boosted, boostedEngine, planner);
            }

            Assert.AreEqual(5, boosted.CurrentRound, "(전제)");
            boosted.SetOverclockMultiplierBonus(2.0);
            Assert.AreEqual(1.0, planner.PassChance(boosted, boostedEngine, false), 1e-12, "남은 3 까지 미뤄도 36");
            PlayRound(boosted, boostedEngine, planner);
            Assert.AreEqual(6, boosted.CurrentRound, "R5 통과");
        }

        [Test]
        public void OptimalOverclock_MatchesExactPassChance()
        {
            // N 0.5 (1 · 오버 0) · R 0.5 (4 · 오버 8) · 2뽑기 · 할당량 8
            // 지금 켬: R (8) 이면 바로 통과 0.5 + N (0) 뒤 R (8) 0.25 = 0.75
            // 안 켬: R (4) 뒤 R (4 또는 8) 0.25 + N (1) 뒤 켜고 R (8) 0.25 = 0.5 → 켠다
            RunManager run = CreateFixedGradeRun(0.5, 0.5, 0.0, 2, 0, out GachaEngine engine, 1, 4);
            var planner = new OverclockPlanner(FixedParts);
            Assert.AreEqual(0.75, planner.PassChance(run, engine, true), 1e-12);
            Assert.AreEqual(0.5, planner.PassChance(run, engine, false), 1e-12);
            Assert.IsTrue(planner.ShouldActivate(run, engine), "0.75 > 0.5");

            // 같은 판에서 R 이 3 (오버 6) 이면: 지금 켬 R R 0.25 · 안 켬 R (3) 뒤 켜고 R (6) 0.25 → 같음 → 안 켬
            RunManager tie = CreateFixedGradeRun(0.5, 0.5, 0.0, 2, 0, out GachaEngine tieEngine, 1, 3);
            Assert.IsFalse(new OverclockPlanner(FixedParts).ShouldActivate(tie, tieEngine), "0.25 = 0.25");
        }

        [Test]
        public void OptimalOverclock_WeightsPartsWithinGrade()
        {
            // N 만 · 부품 생산량 1 · 1 · 4 (같은 등급 안에서 고르게 → 1 이 2/3) · 2뽑기 · 할당량 R1 8 → 4 + 4 만 통과 = 1/9
            var random = new RandomService(1);
            PartData[] parts = { CreatePart("a", Grade.N, 1), CreatePart("b", Grade.N, 1), CreatePart("c", Grade.N, 4), CreatePart("r", Grade.R, 1), CreatePart("s", Grade.SR, 1) };
            var engine = new GachaEngine(random, int.MaxValue, 1.0, 0.0, 0.0);
            var run = new RunManager(engine, new PartCatalog(parts, random), 8, 2);
            Assert.AreEqual(1.0 / 9.0, new OverclockPlanner(parts).PassChance(run, engine, false), 1e-12);
        }

        [Test]
        public void OptimalOverclock_KnowsPityForcesSuperRare()
        {
            // 천장 2 · N 만 (1 · 오버 0) · SR 3 (오버 6) · 3뽑기 · 할당량 8: N N 뒤 세 번째는 반드시 SR → 그때 켜면 1 + 1 + 6 = 8
            var random = new RandomService(1);
            PartData[] parts = { CreatePart("n", Grade.N, 1), CreatePart("r", Grade.R, 1), CreatePart("s", Grade.SR, 3) };
            var engine = new GachaEngine(random, 2, 1.0, 0.0, 0.0);
            var run = new RunManager(engine, new PartCatalog(parts, random), 8, 3);
            var planner = new OverclockPlanner(parts);
            Assert.IsFalse(planner.ShouldActivate(run, engine), "지금 켜면 0 + 0 + 6");
            Assert.AreEqual(1.0, planner.PassChance(run, engine, false), 1e-12, "미루면 확실히 통과");
            run.Draw();
            run.Draw();
            Assert.IsTrue(planner.ShouldActivate(run, engine), "천장으로 SR 이 온다");
            run.ActivateOverclock();
            run.Draw();
            Assert.AreEqual(RunState.RoundCleared, run.State);
            Assert.AreEqual(8, run.Production);
        }

        [Test]
        public void OptimalOverclock_FlatPartsNeverNeedIt()
        {
            ShopRunRecord record = RunSimulator.SimulateWithShop(1, flatParts, zeroModules, new GreedyModuleStrategy(flatParts), null, null, new SimulationPlayPolicy("최적 오버클럭만", flatParts, false, 0.0));
            Assert.IsTrue(record.Run.Cleared, "생산량 11 은 켜지 않아도 확실히 클리어");
            Assert.AreEqual(0, record.OverclocksUsed);
        }

        [Test]
        public void OptimalOverclock_UsedAndCountedWhenBehind()
        {
            // 생산량 3 (R 만) 판: R4 부터 켜야 통과할 수 있다
            PartData[] rOnly = { CreatePart("rn", Grade.N, 3), CreatePart("rr", Grade.R, 3), CreatePart("rsr", Grade.SR, 3) };
            int used = 0;
            for (uint seed = 1; seed <= 50; seed++)
            {
                used += RunSimulator.SimulateWithShop(seed, rOnly, zeroModules, new GreedyModuleStrategy(rOnly), null, null, new SimulationPlayPolicy("최적", rOnly, false, 0.0)).OverclocksUsed;
            }

            Assert.Greater(used, 0);
        }

        [Test]
        public void Reroll_FreeOnceWhenBestGainBelowThreshold()
        {
            // 효과 0 모듈 → 이득 0 < 1.0 → 돌파마다 무료 1번. 오버클럭은 N 을 0 으로 만들어 판이 바뀌므로 뺀다
            var rerollOnly = new SimulationPlayPolicy("다시 받기만", null, true, 1.0);
            ShopRunRecord noShop = RunSimulator.SimulateWithShop(1, flatParts, zeroModules, new GreedyModuleStrategy(flatParts), null, null, rerollOnly);
            Assert.IsTrue(noShop.Run.Cleared, "(전제) 생산량 11 판은 클리어");
            Assert.AreEqual(7, noShop.RerollsUsed, "R1~R7 돌파마다 1번");

            ShopRunRecord greedy = RunSimulator.SimulateWithShop(1, flatParts, zeroModules, new GreedyModuleStrategy(flatParts), shop, new GreedyShopStrategy(flatParts), rerollOnly);
            Assert.AreEqual(7, greedy.RerollsUsed, "예비 카드는 다시 받기 횟수를 늘리지 않는다");
        }

        [Test]
        public void Reroll_NotWhenBestGainMeetsThreshold()
        {
            var neverBelow = new SimulationPlayPolicy("기준 −1", null, true, -1.0);
            ShopRunRecord record = RunSimulator.SimulateWithShop(1, flatParts, zeroModules, new GreedyModuleStrategy(flatParts), null, null, neverBelow);
            Assert.AreEqual(0, record.RerollsUsed, "이득 0 ≥ −1");
        }

        private sealed class DecomposeWithoutChoosing : IShopStrategy
        {
            public string Name => "고르지 않음";

            public void Visit(RunSession session, RandomService choiceRandom)
            {
                session.DecomposeModule(0);
            }
        }

        [Test]
        public void SimulateWithShop_RejectsStrategyThatLeavesReplacementOpen()
        {
            Assert.Throws<InvalidOperationException>(() => RunSimulator.SimulateWithShop(1, flatParts, zeroModules, new GreedyModuleStrategy(flatParts), shop, new DecomposeWithoutChoosing(), SimulationPlayPolicy.None));
        }

        [Test]
        public void SimulateWithShop_RejectsMissingArguments()
        {
            Assert.Throws<ArgumentNullException>(() => RunSimulator.SimulateWithShop(1, flatParts, zeroModules, null, null, null, SimulationPlayPolicy.None));
            Assert.Throws<ArgumentNullException>(() => RunSimulator.SimulateWithShop(1, flatParts, zeroModules, new NoModuleStrategy(), null, null, null));
            Assert.Throws<ArgumentNullException>(() => RunSimulator.SimulateWithShop(1, flatParts, zeroModules, new NoModuleStrategy(), shop, null, SimulationPlayPolicy.None));
        }

        [Test]
        public void PreviewRemoveModule_DoesNotChangeEngine()
        {
            RunSession session = RunSession.Create(1, flatParts, realModules, RunMode.Normal, shop);
            DrawUntilNotDrawing(session);
            session.SelectModule(session.OpenOffer()[0]);
            RunManager run = session.RunManager;
            ModulePreview preview = run.PreviewRemoveModule(0);
            Assert.AreEqual(1, run.EquippedModules.Count);
            Assert.AreEqual(preview.ExpectedRoundOutputBefore(flatParts), run.PreviewModule(zeroModules[0]).ExpectedRoundOutputBefore(flatParts), 1e-9);
            Assert.Throws<ArgumentOutOfRangeException>(() => run.PreviewRemoveModule(1));
        }

        // ── 도우미 ──

        private PartData[] FixedParts;

        // 등급 확률을 고정한 런 (천장 없음). 부품 생산량: N = nOutput (0 이면 1) · R = rOutput (0 이면 output) · SR = output
        private RunManager CreateFixedGradeRun(double n, double r, double sr, int drawsPerRound, int output, out GachaEngine engine, int nOutput = 0, int rOutput = 0)
        {
            var random = new RandomService(1);
            FixedParts = new[]
            {
                CreatePart("fn", Grade.N, nOutput > 0 ? nOutput : output),
                CreatePart("fr", Grade.R, rOutput > 0 ? rOutput : output),
                CreatePart("fsr", Grade.SR, Math.Max(output, 1)),
            };
            engine = new GachaEngine(random, int.MaxValue, n, r, sr);
            return new RunManager(engine, new PartCatalog(FixedParts, random), 8, drawsPerRound);
        }

        private static void PlayRound(RunManager run, GachaEngine engine, OverclockPlanner planner)
        {
            while (run.State == RunState.Drawing)
            {
                if (planner.ShouldActivate(run, engine))
                {
                    run.ActivateOverclock();
                }

                run.Draw();
            }

            if (run.State == RunState.RoundCleared)
            {
                run.AdvanceRound();
            }
        }

        private static void DrawUntilNotDrawing(RunSession session)
        {
            while (session.RunManager.State == RunState.Drawing)
            {
                session.RunManager.Draw();
            }
        }

        private static bool Contains(IReadOnlyList<ModuleData> offer, ModuleData module)
        {
            foreach (ModuleData m in offer)
            {
                if (m == module)
                {
                    return true;
                }
            }

            return false;
        }

        private static ModuleData FirstOther(IReadOnlyList<ModuleData> offer, ModuleData not)
        {
            foreach (ModuleData m in offer)
            {
                if (m != not)
                {
                    return m;
                }
            }

            return null;
        }

        private ShopSettings CreateShop(int wage, int superchargerPrice, int sparePrice, int decomposePrice, int decomposeRefund)
        {
            var settings = ScriptableObject.CreateInstance<ShopSettings>();
            settings.InitializeForTests(
                wage,
                CreateItem("supercharger", ShopItemKind.Part, ShopItemEffect.Supercharger, superchargerPrice, 0),
                CreateItem("spare_card", ShopItemKind.Service, ShopItemEffect.SpareCard, sparePrice, 0),
                CreateItem("decompose", ShopItemKind.Service, ShopItemEffect.Decompose, decomposePrice, decomposeRefund));
            createdObjects.Add(settings);
            return settings;
        }

        private ShopItemData CreateItem(string id, ShopItemKind kind, ShopItemEffect effect, int price, int refund)
        {
            var item = ScriptableObject.CreateInstance<ShopItemData>();
            item.InitializeForTests(id, kind, effect, price, refund);
            createdObjects.Add(item);
            return item;
        }

        private PartData CreatePart(string displayName, Grade grade, int baseOutput, PartTag tag = PartTag.None)
        {
            var part = ScriptableObject.CreateInstance<PartData>();
            part.InitializeForTests(displayName, grade, baseOutput, tag);
            createdObjects.Add(part);
            return part;
        }

        private ModuleData CreateModule(string id, ModuleAxis axis, ModifierType type, float value)
        {
            var module = ScriptableObject.CreateInstance<ModuleData>();
            module.InitializeForTests(id, axis, new ModifierEffect(type, GradeFilter.Any, value));
            module.SetIdForTests(id);
            createdObjects.Add(module);
            return module;
        }
    }
}

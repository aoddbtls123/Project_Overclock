using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using NUnit.Framework;
using Overclock.Core;
using UnityEngine;

namespace Overclock.Tests
{
    // 상점 · 화폐 최소판 (11/08): 돌파마다 기본급 + 초과분 1:1 · 짝수 라운드 모듈 선택 뒤 상점 · 품목 3 (과급기 · 예비 카드 · 모듈 분해 + 대체 제안) · 안 사면 난수 미사용.
    // 기대값은 기획 숫자 리터럴로 쓴다 (할당량 R1 8 · R2 12 · R3 17 · R4 24 · R5 35 · R6 51 · R7 74 · R8 108)
    public class ShopTests
    {
        private const int Wage = 5;
        private const int SuperchargerPrice = 20;
        private const int SparePrice = 4;
        private const int DecomposePrice = 3;
        private const int DecomposeRefund = 10;

        private readonly List<UnityEngine.Object> createdObjects = new List<UnityEngine.Object>();

        // 모든 부품 생산량 11 · 효과 0 모듈 → 판이 시드와 무관하게 정해진다 (R1 1뽑기 11 · R2 2뽑기 22 · … · R8 10뽑기 110)
        private PartData[] flatParts;
        private ModuleData[] zeroModules;
        // 생산량이 등급마다 다르고 효과가 있는 모듈 → 난수 회귀 · 세이브 왕복용
        private PartData[] mixedParts;
        private ModuleData[] realModules;
        private ShopSettings shop;
        private ShopItemData supercharger;
        private ShopItemData spare;
        private ShopItemData decompose;

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

            supercharger = CreateItem("supercharger", ShopItemKind.Part, ShopItemEffect.Supercharger, SuperchargerPrice, 0);
            spare = CreateItem("spare_card", ShopItemKind.Service, ShopItemEffect.SpareCard, SparePrice, 0);
            decompose = CreateItem("decompose", ShopItemKind.Service, ShopItemEffect.Decompose, DecomposePrice, DecomposeRefund);
            shop = ScriptableObject.CreateInstance<ShopSettings>();
            shop.InitializeForTests(Wage, supercharger, spare, decompose);
            createdObjects.Add(shop);
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

        // ── 크레딧 ──

        [Test]
        public void Wallet_EarnSpendAndNeverNegative()
        {
            var wallet = new CreditWallet();
            Assert.AreEqual(0, wallet.Balance);
            wallet.Earn(7);
            Assert.IsFalse(wallet.TrySpend(8), "모자라면 안 씀");
            Assert.AreEqual(7, wallet.Balance);
            Assert.IsTrue(wallet.TrySpend(7));
            Assert.AreEqual(0, wallet.Balance);
            Assert.Throws<ArgumentOutOfRangeException>(() => wallet.Earn(-1));
            Assert.Throws<ArgumentOutOfRangeException>(() => wallet.TrySpend(-1));
        }

        [Test]
        public void Wallet_StopsAtIntMax()
        {
            var wallet = new CreditWallet();
            wallet.Earn(int.MaxValue - 1);
            wallet.Earn(5);
            Assert.AreEqual(int.MaxValue, wallet.Balance);
        }

        [Test]
        public void Credits_WageAndOverflowEveryBreakthrough_CarriedOverToClear()
        {
            RunSession session = RunSession.Create(1, flatParts, zeroModules, RunMode.Normal, shop);
            // 라운드마다 (기본급 5 + 초과분): R1 11−8 · R2 22−12 · R3 22−17 · R4 33−24 · R5 44−35 · R6 55−51 · R7 77−74 · R8 110−108
            int[] expectedAfterRound = { 8, 23, 33, 47, 61, 70, 78, 85 };
            for (int round = 1; round <= 8; round++)
            {
                DrawUntilNotDrawing(session);
                Assert.AreEqual(expectedAfterRound[round - 1], session.Wallet.Balance, "R" + round);
                if (round < 8)
                {
                    Assert.AreEqual(RunState.RoundCleared, session.RunManager.State);
                    session.OpenOffer();
                    session.SelectModule(session.CurrentOffer[0]);
                    if (session.IsShopOpen)
                    {
                        session.LeaveShop();
                    }
                }
            }

            Assert.AreEqual(RunState.RunCleared, session.RunManager.State, "R8 클리어 돌파도 지급 (쓸 곳 없음)");
        }

        [Test]
        public void Credits_NoneOnFailAndNoneWithoutShop()
        {
            // 생산량 1 · 10뽑기 → R1 (8) 돌파 · R2 (12) 실패
            PartData[] ones = { CreatePart("n1", Grade.N, 1), CreatePart("r1", Grade.R, 1), CreatePart("sr1", Grade.SR, 1) };
            RunSession session = RunSession.Create(1, ones, zeroModules, RunMode.Normal, shop);
            DrawUntilNotDrawing(session);
            Assert.AreEqual(Wage, session.Wallet.Balance, "R1: 8 − 8 = 0 초과 → 기본급만");
            session.OpenOffer();
            session.SelectModule(session.CurrentOffer[0]);
            DrawUntilNotDrawing(session);
            Assert.AreEqual(RunState.RunFailed, session.RunManager.State);
            Assert.AreEqual(Wage, session.Wallet.Balance, "실패는 돌파가 아님");

            RunSession plain = RunSession.Create(1, flatParts, zeroModules);
            DrawUntilNotDrawing(plain);
            Assert.IsFalse(plain.HasShop);
            Assert.AreEqual(0, plain.Wallet.Balance);
        }

        [Test]
        public void Credits_StartAtZeroEachRun()
        {
            RunSession first = RunSession.Create(1, flatParts, zeroModules, RunMode.Normal, shop);
            DrawUntilNotDrawing(first);
            Assert.AreEqual(8, first.Wallet.Balance);
            RunSession second = RunSession.Create(1, flatParts, zeroModules, RunMode.Normal, shop);
            Assert.AreEqual(0, second.Wallet.Balance);
        }

        // ── 상점 시점 ──

        [Test]
        public void Shop_OpensAfterModuleSelectOnEvenRoundsOnly_Normal()
        {
            RunSession session = RunSession.Create(1, flatParts, zeroModules, RunMode.Normal, shop);
            var opened = new List<int>();
            for (int round = 1; round <= 7; round++)
            {
                DrawUntilNotDrawing(session);
                session.OpenOffer();
                Assert.IsFalse(session.IsShopOpen, "모듈 선택 전에는 안 열림");
                session.SelectModule(session.CurrentOffer[0]);
                if (session.IsShopOpen)
                {
                    opened.Add(round);
                    Assert.AreEqual(round, session.RunManager.CurrentRound, "상점 동안 라운드는 그대로");
                    Assert.AreEqual(RunState.RoundCleared, session.RunManager.State);
                    session.LeaveShop();
                }

                Assert.AreEqual(round + 1, session.RunManager.CurrentRound);
            }

            CollectionAssert.AreEqual(new[] { 2, 4, 6 }, opened);
        }

        [Test]
        public void Shop_KeepsOpeningEveryTwoRounds_Endless()
        {
            RunSession session = RunSession.Create(1, flatParts, zeroModules, RunMode.Endless, shop);
            var opened = new List<int>();
            for (int round = 1; round <= 8; round++)
            {
                DrawUntilNotDrawing(session);
                Assert.AreEqual(RunState.RoundCleared, session.RunManager.State, "R" + round);
                session.OpenOffer();
                session.SelectModule(session.CurrentOffer[0]);
                if (session.IsShopOpen)
                {
                    opened.Add(round);
                    session.LeaveShop();
                }
            }

            CollectionAssert.AreEqual(new[] { 2, 4, 6, 8 }, opened, "무한모드 R8 은 클리어가 아님");
        }

        [Test]
        public void Shop_DeclineOfferAlsoOpensShop_AndLeaveRequiresOpenShop()
        {
            RunSession session = RunSession.Create(1, flatParts, zeroModules, RunMode.Normal, shop);
            Assert.Throws<InvalidOperationException>(() => session.LeaveShop());
            AdvanceDeclining(session, 2);
            Assert.IsTrue(session.IsShopOpen);
            Assert.AreEqual(0, session.RunManager.EquippedModules.Count);
            uint before = session.CaptureSaveData().randomState;
            Assert.Throws<InvalidOperationException>(() => session.OpenOffer(), "상점 동안 제안을 다시 열면 새 카드로 난수를 쓴다");
            Assert.AreEqual(before, session.CaptureSaveData().randomState);
        }

        // ── 품목 ──

        [Test]
        public void Supercharger_RaisesOverclockMultiplierBonusToTwo()
        {
            RunManager rare = CreateFixedGradeRun(0.0, 1.0, 0.0, 10, out _, 3);
            rare.SetOverclockMultiplierBonus(2.0);
            rare.ActivateOverclock();
            Assert.AreEqual(9, rare.Draw().Output, "3 × (1 + 2)");
            Assert.IsFalse(rare.CanActivateOverclock, "라운드당 1번은 그대로");

            RunManager normal = CreateFixedGradeRun(1.0, 0.0, 0.0, 10, out _, 3);
            normal.SetOverclockMultiplierBonus(2.0);
            normal.ActivateOverclock();
            Assert.AreEqual(0, normal.Draw().Output, "N 은 그대로 0");

            var engine = new ModifierEngine(0.72, 0.25, 0.03);
            engine.Equip(CreateModule("big", ModuleAxis.Multiplier, ModifierType.OutputMultiplier, 7f));
            Assert.AreEqual(24, engine.CalculateOverclockedOutput(CreatePart("r3", Grade.R, 3), false, 2.0), "3 × min(1 + 7 + 2, 8)");
            Assert.Throws<ArgumentOutOfRangeException>(() => rare.SetOverclockMultiplierBonus(0.5));
        }

        [Test]
        public void Supercharger_BoughtOnceThenSoldOut_LastsForRestOfRun()
        {
            RunSession session = OpenShopWithCredits(4, out _);
            Assert.AreEqual(47, session.Wallet.Balance, "(전제) R4 상점 크레딧");
            Assert.AreEqual(1.0, session.RunManager.OverclockMultiplierBonus, "(전제) 기본 +1");
            Assert.AreEqual(ShopItemStatus.Available, session.GetItemStatus(supercharger));
            session.BuyItem(supercharger);
            Assert.AreEqual(47 - SuperchargerPrice, session.Wallet.Balance);
            Assert.IsTrue(session.HasSupercharger);
            Assert.AreEqual(ShopItemStatus.SoldOut, session.GetItemStatus(supercharger));
            Assert.Throws<InvalidOperationException>(() => session.BuyItem(supercharger));

            session.LeaveShop();
            Assert.AreEqual(2.0, session.RunManager.OverclockMultiplierBonus, "다음 라운드부터 바로");
            DrawUntilNotDrawing(session);
            session.OpenOffer();
            session.SelectModule(session.CurrentOffer[0]);
            Assert.AreEqual(2.0, session.RunManager.OverclockMultiplierBonus, "다음 라운드에도");
        }

        [Test]
        public void SpareCard_BuyAsManyAsAffordable_EachOpensOneFourCardOffer()
        {
            RunSession session = OpenShopWithCredits(2, out _);
            Assert.AreEqual(23, session.Wallet.Balance, "(전제) R2 상점 크레딧");
            session.BuyItem(spare);
            session.BuyItem(spare);
            session.BuyItem(spare);
            Assert.AreEqual(3, session.SpareCards, "한 방문에 여러 개");
            Assert.AreEqual(23 - 3 * SparePrice, session.Wallet.Balance);
            session.LeaveShop();

            DrawUntilNotDrawing(session);
            IReadOnlyList<ModuleData> wide = session.OpenOffer();
            Assert.AreEqual(4, wide.Count, "축 4개에서 하나씩");
            AssertDistinctAxes(wide);
            Assert.IsTrue(session.IsWideOffer);
            Assert.AreEqual(2, session.SpareCards, "제안 1번에 1개");
            Assert.AreSame(wide, session.OpenOffer(), "다시 열어도 같은 카드 · 예비 카드도 그대로");
            Assert.AreEqual(2, session.SpareCards);
            Assert.AreEqual(4, session.RerollOffer().Count, "다시 받아도 4장");
            Assert.IsFalse(session.CanRerollOffer, "다시 받기는 돌파마다 1번 그대로");
            session.SelectModule(session.CurrentOffer[0]);
            Assert.IsFalse(session.IsWideOffer);

            DrawUntilNotDrawing(session);
            Assert.AreEqual(4, session.OpenOffer().Count);
            Assert.AreEqual(1, session.SpareCards);
            session.SelectModule(session.CurrentOffer[0]);
            session.LeaveShop();
            DrawUntilNotDrawing(session);
            Assert.AreEqual(4, session.OpenOffer().Count);
            Assert.AreEqual(0, session.SpareCards);
            session.SelectModule(session.CurrentOffer[0]);
            DrawUntilNotDrawing(session);
            Assert.AreEqual(3, session.OpenOffer().Count, "예비 카드가 없으면 3장");
            Assert.IsFalse(session.IsWideOffer);
        }

        [Test]
        public void SpareCard_NotEnoughCredits()
        {
            ShopSettings poor = CreateShop(0, CreateItem("cheap_super", ShopItemKind.Part, ShopItemEffect.Supercharger, 0, 0), CreateItem("rich_spare", ShopItemKind.Service, ShopItemEffect.SpareCard, 1000, 0));
            RunSession session = RunSession.Create(1, flatParts, zeroModules, RunMode.Normal, poor);
            AdvanceSelecting(session, 2);
            ShopItemData expensive = session.FindShopItem(ShopItemEffect.SpareCard);
            Assert.AreEqual(ShopItemStatus.NotEnoughCredits, session.GetItemStatus(expensive));
            Assert.Throws<InvalidOperationException>(() => session.BuyItem(expensive));
            Assert.AreEqual(0, session.SpareCards);
            Assert.AreEqual(13, session.Wallet.Balance, "기본급 0 · 초과분 3 + 10");
        }

        [Test]
        public void Decompose_RemovesChosenModule_RefundsThenReplacementMustBeChosen()
        {
            RunSession session = OpenShopWithCredits(2, out _);
            Assert.AreEqual(2, session.RunManager.EquippedModules.Count);
            ModuleData justPicked = session.RunManager.EquippedModules[1];
            ModuleData kept = session.RunManager.EquippedModules[0];

            Assert.AreEqual(ShopItemStatus.Available, session.GetItemStatus(decompose));
            ModuleData removed = session.DecomposeModule(1);
            Assert.AreSame(justPicked, removed, "방금 고른 모듈도 분해 가능");
            Assert.AreEqual(1, session.RunManager.EquippedModules.Count);
            Assert.AreSame(kept, session.RunManager.EquippedModules[0]);
            Assert.AreEqual(23 - DecomposePrice + DecomposeRefund, session.Wallet.Balance);
            Assert.IsTrue(session.DecomposedThisVisit);

            Assert.IsTrue(session.IsChoosingReplacement);
            Assert.AreEqual(3, session.ReplacementOffer.Count, "대체 제안 3장");
            AssertDistinctAxes(session.ReplacementOffer);
            Assert.Throws<InvalidOperationException>(() => session.LeaveShop(), "고르기 전에는 못 나감");
            Assert.Throws<InvalidOperationException>(() => session.BuyItem(spare));
            Assert.Throws<InvalidOperationException>(() => session.DecomposeModule(0));
            ModuleData notOffered = NotIn(session.ReplacementOffer);
            Assert.Throws<ArgumentException>(() => session.SelectReplacement(notOffered));

            ModuleData chosen = session.ReplacementOffer[2];
            session.SelectReplacement(chosen);
            Assert.IsFalse(session.IsChoosingReplacement);
            Assert.AreEqual(0, session.ReplacementOffer.Count);
            Assert.AreEqual(2, session.RunManager.EquippedModules.Count);
            Assert.AreSame(chosen, session.RunManager.EquippedModules[1]);
            Assert.Throws<InvalidOperationException>(() => session.SelectReplacement(chosen), "한 번만");
            Assert.AreEqual(ShopItemStatus.SoldOut, session.GetItemStatus(decompose), "방문마다 1번");
            Assert.Throws<InvalidOperationException>(() => session.DecomposeModule(0));
            Assert.Throws<InvalidOperationException>(() => session.BuyItem(decompose), "분해는 BuyItem 으로 안 함");

            session.LeaveShop();
            AdvanceSelecting(session, 2);
            Assert.IsTrue(session.IsShopOpen, "(전제) R4 상점");
            Assert.IsFalse(session.DecomposedThisVisit, "다음 방문에 다시");
            Assert.AreEqual(ShopItemStatus.Available, session.GetItemStatus(decompose));
        }

        [Test]
        public void Decompose_UnavailableWithoutModules_AndRejectsBadIndex()
        {
            RunSession session = RunSession.Create(1, flatParts, zeroModules, RunMode.Normal, shop);
            AdvanceDeclining(session, 2);
            Assert.AreEqual(ShopItemStatus.Unavailable, session.GetItemStatus(decompose));
            Assert.Throws<InvalidOperationException>(() => session.DecomposeModule(0));

            RunSession withModules = OpenShopWithCredits(2, out _);
            int before = withModules.Wallet.Balance;
            Assert.Throws<ArgumentOutOfRangeException>(() => withModules.DecomposeModule(2));
            Assert.Throws<ArgumentOutOfRangeException>(() => withModules.DecomposeModule(-1));
            Assert.AreEqual(before, withModules.Wallet.Balance, "실패하면 돈도 안 나감");
            Assert.IsFalse(withModules.DecomposedThisVisit);
        }

        [Test]
        public void RemoveModule_RecalculatesProbabilitiesFromRemainingModules()
        {
            RunManager run = CreateFixedGradeRun(0.72, 0.25, 0.03, 10, out GachaEngine engine);
            ModuleData srUp = realModules[0];
            ModuleData rUp = realModules[1];
            run.EquipModule(srUp);
            run.EquipModule(rUp);
            run.EquipModule(srUp);

            AssertProbabilities(engine, 0.13, 0.35, "(전제) SR 0.03 + 0.05 × 2 · R 0.25 + 0.1");
            run.RemoveModule(0);
            AssertProbabilities(engine, 0.08, 0.35, "SR 0.03 + 0.05 · R 0.25 + 0.1");
            run.RemoveModule(0);
            AssertProbabilities(engine, 0.08, 0.25 * 0.92 / 0.97, "R 은 안 올림 → 남은 몫을 비율대로");
            run.RemoveModule(0);
            AssertProbabilities(engine, 0.03, 0.25, "모듈 없음 = 기본표");
            Assert.AreEqual(0.72, engine.GetProbability(Grade.N), 1e-12);
            Assert.Throws<ArgumentOutOfRangeException>(() => run.RemoveModule(0));
        }

        [Test]
        public void RemoveModule_DrawCountChangesFromNextRound()
        {
            RunManager run = CreateFixedGradeRun(1.0, 0.0, 0.0, 10, out _, 11);
            run.EquipModule(realModules[5]);
            run.Draw();
            Assert.AreEqual(RunState.RoundCleared, run.State);
            run.RemoveModule(0);
            run.AdvanceRound();
            Assert.AreEqual(10, run.DrawsRemaining, "추가 뽑기 2 가 빠짐");
        }

        // ── 난수 ──

        [Test]
        public void NoPurchase_SameDrawsAsSessionWithoutShop()
        {
            for (uint seed = 1; seed <= 300; seed++)
            {
                RunSession plain = RunSession.Create(seed, mixedParts, realModules);
                RunSession withShop = RunSession.Create(seed, mixedParts, realModules, RunMode.Normal, shop);
                int shops = 0;
                while (true)
                {
                    if (plain.RunManager.State == RunState.Drawing)
                    {
                        DrawResult a = plain.RunManager.Draw();
                        DrawResult b = withShop.RunManager.Draw();
                        Assert.AreSame(a.Part, b.Part, "seed=" + seed);
                        Assert.AreEqual(a.Output, b.Output, "seed=" + seed);
                        continue;
                    }

                    Assert.AreEqual(plain.RunManager.State, withShop.RunManager.State, "seed=" + seed);
                    if (plain.RunManager.State != RunState.RoundCleared)
                    {
                        break;
                    }

                    plain.SelectModule(plain.OpenOffer()[0]);
                    withShop.SelectModule(withShop.OpenOffer()[0]);
                    if (withShop.IsShopOpen)
                    {
                        shops++;
                        withShop.LeaveShop();
                    }

                    Assert.AreEqual(plain.CaptureSaveData().randomState, withShop.CaptureSaveData().randomState, "seed=" + seed);
                }

                Assert.AreEqual(plain.RunManager.CurrentRound, withShop.RunManager.CurrentRound);
                Assert.AreEqual((plain.RunManager.CurrentRound - 1) / 2, shops, "seed=" + seed + " 상점 수 (끝난 라운드 앞의 짝수 라운드)");
            }
        }

        [Test]
        public void Purchases_DoNotUseRandom_DecomposeUsesExactlyOneOffer()
        {
            RunSession session = OpenShopWithCredits(4, out uint seed);
            RunSession mirror = OpenShopWithCredits(4, out _);
            uint before = session.CaptureSaveData().randomState;
            session.GetItemStatus(supercharger);
            session.BuyItem(supercharger);
            session.BuyItem(spare);
            Assert.AreEqual(before, session.CaptureSaveData().randomState, "과급기 · 예비 카드는 난수 안 씀");

            session.DecomposeModule(0);
            mirror.ModuleOffer.CreateOffer();
            Assert.AreEqual(mirror.CaptureSaveData().randomState, session.CaptureSaveData().randomState, "분해 = 대체 제안 1번 만큼");
            uint afterOffer = session.CaptureSaveData().randomState;
            session.SelectReplacement(session.ReplacementOffer[0]);
            session.LeaveShop();
            Assert.AreEqual(afterOffer, session.CaptureSaveData().randomState, "고르기 · 나가기는 안 씀");
        }

        // ── 세이브 ──

        [Test]
        public void SaveInShop_WhileChoosingReplacement_RestoresAndPlaysOnIdentically()
        {
            for (uint seed = 1; seed <= 300; seed++)
            {
                RunSession session = RunSession.Create(seed, mixedParts, realModules, RunMode.Normal, shop);
                if (!TryAdvanceSelecting(session, 2) || !session.IsShopOpen)
                {
                    continue;
                }

                session.Wallet.Earn(100);
                session.BuyItem(supercharger);
                session.BuyItem(spare);
                session.DecomposeModule(0);
                string json = JsonUtility.ToJson(session.CaptureSaveData(), true);
                RunSession restored = RunSession.Restore(JsonUtility.FromJson<RunSaveData>(json), mixedParts, realModules, shop);

                Assert.IsTrue(restored.IsShopOpen);
                Assert.AreEqual(session.Wallet.Balance, restored.Wallet.Balance);
                Assert.IsTrue(restored.HasSupercharger);
                Assert.AreEqual(2.0, restored.RunManager.OverclockMultiplierBonus);
                Assert.AreEqual(1, restored.SpareCards);
                Assert.IsTrue(restored.DecomposedThisVisit);
                Assert.IsTrue(restored.IsChoosingReplacement);
                CollectionAssert.AreEqual(session.ReplacementOffer, restored.ReplacementOffer);
                CollectionAssert.AreEqual(session.RunManager.EquippedModules, restored.RunManager.EquippedModules);

                session.SelectReplacement(session.ReplacementOffer[0]);
                restored.SelectReplacement(restored.ReplacementOffer[0]);
                session.LeaveShop();
                restored.LeaveShop();
                for (int i = 0; i < 30 && session.RunManager.State == RunState.Drawing; i++)
                {
                    Assert.AreSame(session.RunManager.Draw().Part, restored.RunManager.Draw().Part, "seed=" + seed);
                }

                Assert.AreEqual(session.Wallet.Balance, restored.Wallet.Balance, "돌파 지급도 같게");
                if (session.RunManager.State == RunState.RoundCleared)
                {
                    CollectionAssert.AreEqual(session.OpenOffer(), restored.OpenOffer(), "예비 카드 4장 제안도 같게");
                }

                return;
            }

            Assert.Fail("R2 상점까지 가는 시드가 없음");
        }

        [Test]
        public void SaveAfterLeavingShop_RestoresAfterDecompose()
        {
            RunSession session = OpenShopWithCredits(2, out _);
            session.DecomposeModule(0);
            session.SelectReplacement(session.ReplacementOffer[0]);
            session.LeaveShop();
            RunSaveData data = session.CaptureSaveData();
            Assert.IsFalse(data.shopOpen);
            Assert.IsFalse(data.decomposedThisVisit, "나가면 이번 방문 기록을 지운다");
            RunSession restored = RunSession.Restore(data, flatParts, zeroModules, shop);
            Assert.AreEqual(3, restored.RunManager.CurrentRound);
            Assert.AreEqual(session.Wallet.Balance, restored.Wallet.Balance);
        }

        [Test]
        public void SaveWithWideOffer_RestoresFourCardsAndRerollsFour()
        {
            RunSession session = OpenShopWithCredits(2, out _);
            session.BuyItem(spare);
            session.BuyItem(spare);
            session.LeaveShop();
            DrawUntilNotDrawing(session);
            session.OpenOffer();
            RunSaveData data = session.CaptureSaveData();
            Assert.IsTrue(data.wideOffer);
            Assert.AreEqual(4, data.offeredModuleIds.Length);
            Assert.AreEqual(1, data.spareCards);

            RunSession restored = RunSession.Restore(Clone(data), flatParts, zeroModules, shop);
            Assert.IsTrue(restored.IsWideOffer);
            CollectionAssert.AreEqual(session.CurrentOffer, restored.CurrentOffer);
            Assert.AreEqual(1, restored.SpareCards);
            CollectionAssert.AreEqual(session.RerollOffer(), restored.RerollOffer());
            Assert.AreEqual(4, restored.CurrentOffer.Count);
        }

        [Test]
        public void OldSaveWithoutNewFields_ReadsAsZeroAndUsedOverclockAsOnce()
        {
            RunSession session = RunSession.Create(1, flatParts, zeroModules);
            session.RunManager.ActivateOverclock();
            string json = JsonUtility.ToJson(session.CaptureSaveData(), true);
            string old = json;
            foreach (string field in new[] { "overclockUseCount", "credits", "shopOpen", "hasSupercharger", "spareCards", "wideOffer", "decomposedThisVisit", "replacementModuleIds" })
            {
                old = Regex.Replace(old, ",\\s*\"" + field + "\"\\s*:\\s*[^,}\\s]+", "");
            }

            Assert.IsFalse(old.Contains("overclockUseCount"), "(전제) 필드를 지움");
            Assert.IsFalse(old.Contains("credits"), "(전제) 필드를 지움");
            RunSession restored = RunSession.Restore(JsonUtility.FromJson<RunSaveData>(old), flatParts, zeroModules);
            Assert.AreEqual(1, restored.RunManager.OverclockUseCount);
            Assert.AreEqual(3, restored.RunManager.OverclockDrawsLeft);
            Assert.IsFalse(restored.RunManager.CanActivateOverclock);
            Assert.AreEqual(0, restored.Wallet.Balance);
            Assert.IsFalse(restored.IsShopOpen);

            RunSession withShop = RunSession.Restore(JsonUtility.FromJson<RunSaveData>(old), flatParts, zeroModules, shop);
            Assert.AreEqual(0, withShop.Wallet.Balance, "상점판으로 옛 세이브를 이어도 0 에서");
            Assert.AreEqual(0, withShop.SpareCards);
        }

        [Test]
        public void Restore_RejectsValuesTheGameCannotMake()
        {
            RunSession inShop = OpenShopWithCredits(2, out _);
            RunSaveData valid = inShop.CaptureSaveData();
            RunSession.Restore(Clone(valid), flatParts, zeroModules, shop);

            AssertRejected(valid, d => d.credits = -1, shop, "음수 크레딧");
            AssertRejected(valid, d => d.spareCards = -1, shop, "음수 예비 카드");
            AssertRejected(valid, d => d.replacementModuleIds = new[] { "p0", "m0", "c0" }, shop, "분해 안 했는데 대체 제안");
            AssertRejected(valid, d => { d.decomposedThisVisit = true; d.replacementModuleIds = new[] { "p0", "m0", "c0", "d0" }; }, shop, "대체 제안 4장");
            AssertRejected(valid, d => { }, null, "상점 없는 세션에 상점 값");
            AssertRejected(valid, d => d.offeredModuleIds = new[] { "p0", "m0", "c0" }, shop, "상점이 열렸는데 제안 카드");
            AssertRejected(valid, d => d.overclockUseCount = 1, shop, "안 썼는데 횟수");
            AssertRejected(valid, d => { d.overclockUsed = true; d.overclockUseCount = 2; }, shop, "라운드당 2회");
            AssertRejected(valid, d => d.overclockUseCount = -1, shop, "음수 횟수");

            RunSession oddRound = RunSession.Create(1, flatParts, zeroModules, RunMode.Normal, shop);
            DrawUntilNotDrawing(oddRound);
            RunSaveData r1 = oddRound.CaptureSaveData();
            AssertRejected(r1, d => d.shopOpen = true, shop, "홀수 라운드에 상점");
            AssertRejected(r1, d => d.decomposedThisVisit = true, shop, "상점 밖 분해 기록");
            AssertRejected(r1, d => d.wideOffer = true, shop, "제안 없이 4장 표시");
            AssertRejected(r1, d => d.offeredModuleIds = new[] { "p0", "m0", "c0", "d0" }, shop, "4장인데 4장 표시 없음");
            AssertRejected(r1, d => { d.offeredModuleIds = new[] { "p0", "m0", "c0" }; d.wideOffer = true; }, shop, "4장 표시인데 3장");
            RunSaveData wideR1 = Clone(r1);
            wideR1.offeredModuleIds = new[] { "p0", "m0", "c0", "d0" };
            wideR1.wideOffer = true;
            Assert.IsTrue(RunSession.Restore(wideR1, flatParts, zeroModules, shop).IsWideOffer, "4장 + 표시는 게임이 만들 수 있음");

            RunSession drawing = RunSession.Create(1, flatParts, zeroModules, RunMode.Normal, shop);
            AdvanceSelecting(drawing, 1);
            AdvanceSelecting(drawing, 1);
            drawing.LeaveShop();
            RunSaveData r3Drawing = drawing.CaptureSaveData();
            Assert.AreEqual(RunState.Drawing, r3Drawing.state, "(전제)");
            AssertRejected(r3Drawing, d => { d.round = 2; d.shopOpen = true; }, shop, "뽑기 중 상점");

            ShopSettings noSupercharger = CreateShop(Wage, CreateItem("only_spare", ShopItemKind.Service, ShopItemEffect.SpareCard, 1, 0));
            RunSaveData withSupercharger = Clone(valid);
            withSupercharger.hasSupercharger = true;
            withSupercharger.shopOpen = false;
            Assert.Catch<ArgumentException>(() => RunSession.Restore(withSupercharger, flatParts, zeroModules, noSupercharger), "과급기 안 파는 상점");
        }

        [Test]
        public void ShopSettings_RejectsDuplicateEffectsAndEmptySlots()
        {
            ShopSettings duplicated = CreateShop(Wage, supercharger, CreateItem("super2", ShopItemKind.Part, ShopItemEffect.Supercharger, 1, 0));
            Assert.Throws<ArgumentException>(() => RunSession.Create(1, flatParts, zeroModules, RunMode.Normal, duplicated));
            ShopSettings empty = CreateShop(Wage, supercharger, null);
            Assert.Throws<ArgumentException>(() => RunSession.Create(1, flatParts, zeroModules, RunMode.Normal, empty));
        }

        [Test]
        public void ShopItems_RejectItemFromAnotherShop()
        {
            RunSession session = OpenShopWithCredits(2, out _);
            ShopItemData stranger = CreateItem("spare_card", ShopItemKind.Service, ShopItemEffect.SpareCard, SparePrice, 0);
            Assert.Throws<ArgumentException>(() => session.BuyItem(stranger));
            Assert.Throws<ArgumentNullException>(() => session.BuyItem(null));
        }

        // ── 도우미 ──

        // flatParts 판을 round 라운드 상점까지 진행 (카드 0번을 고르고 그 전 상점은 그냥 나감). R2 상점 = 23 · R4 = 47 크레딧
        private RunSession OpenShopWithCredits(int round, out uint seed)
        {
            seed = 1;
            RunSession session = RunSession.Create(seed, flatParts, zeroModules, RunMode.Normal, shop);
            for (int r = 1; r <= round; r++)
            {
                if (session.IsShopOpen)
                {
                    session.LeaveShop();
                }

                AdvanceSelecting(session, 1);
            }

            Assert.IsTrue(session.IsShopOpen, "(전제) R" + round + " 상점");
            return session;
        }

        private static void AssertDistinctAxes(IReadOnlyList<ModuleData> offer)
        {
            var axes = new HashSet<ModuleAxis>();
            foreach (ModuleData module in offer)
            {
                Assert.IsTrue(axes.Add(module.Axis), "축 겹침 " + module.Axis);
            }
        }

        private ModuleData NotIn(IReadOnlyList<ModuleData> offer)
        {
            foreach (ModuleData module in zeroModules)
            {
                if (!Contains(offer, module))
                {
                    return module;
                }
            }

            return CreateModule("stranger", ModuleAxis.Probability, ModifierType.SuperRareChance, 0f);
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

        private static void AdvanceSelecting(RunSession session, int breakthroughs)
        {
            Assert.IsTrue(TryAdvanceSelecting(session, breakthroughs), "(전제) 돌파");
        }

        private static bool TryAdvanceSelecting(RunSession session, int breakthroughs)
        {
            for (int i = 0; i < breakthroughs; i++)
            {
                if (session.IsShopOpen)
                {
                    session.LeaveShop();
                }

                DrawUntilNotDrawing(session);
                if (session.RunManager.State != RunState.RoundCleared)
                {
                    return false;
                }

                session.SelectModule(session.OpenOffer()[0]);
            }

            return true;
        }

        private static void AdvanceDeclining(RunSession session, int breakthroughs)
        {
            for (int i = 0; i < breakthroughs; i++)
            {
                if (session.IsShopOpen)
                {
                    session.LeaveShop();
                }

                DrawUntilNotDrawing(session);
                Assert.AreEqual(RunState.RoundCleared, session.RunManager.State, "(전제) 돌파");
                session.OpenOffer();
                session.DeclineOffer();
            }
        }

        private static void DrawUntilNotDrawing(RunSession session)
        {
            while (session.RunManager.State == RunState.Drawing)
            {
                session.RunManager.Draw();
            }
        }

        private RunManager CreateFixedGradeRun(double n, double r, double sr, int drawsPerRound, out GachaEngine engine, int output = 1)
        {
            var random = new RandomService(1);
            var parts = new List<PartData> { CreatePart("fn", Grade.N, output), CreatePart("fr", Grade.R, output), CreatePart("fsr", Grade.SR, output) };
            engine = new GachaEngine(random, int.MaxValue, n, r, sr);
            return new RunManager(engine, new PartCatalog(parts, random), 8, drawsPerRound);
        }

        private static void AssertProbabilities(GachaEngine engine, double expectedSuperRare, double expectedRare, string message)
        {
            // 모듈 값이 float 라 1e-6 까지만 본다
            Assert.AreEqual(expectedSuperRare, engine.GetProbability(Grade.SR), 1e-6, message);
            Assert.AreEqual(expectedRare, engine.GetProbability(Grade.R), 1e-6, message);
        }

        // 고친 세이브는 ArgumentException (또는 그 하위 ArgumentOutOfRangeException) 으로 거부한다
        private void AssertRejected(RunSaveData valid, Action<RunSaveData> change, ShopSettings shopForRestore, string message)
        {
            RunSaveData data = Clone(valid);
            change(data);
            Assert.Catch<ArgumentException>(() => RunSession.Restore(data, flatParts, zeroModules, shopForRestore), message);
        }

        private static RunSaveData Clone(RunSaveData data)
        {
            return JsonUtility.FromJson<RunSaveData>(JsonUtility.ToJson(data));
        }

        private ShopSettings CreateShop(int wage, params ShopItemData[] items)
        {
            var settings = ScriptableObject.CreateInstance<ShopSettings>();
            settings.InitializeForTests(wage, items);
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

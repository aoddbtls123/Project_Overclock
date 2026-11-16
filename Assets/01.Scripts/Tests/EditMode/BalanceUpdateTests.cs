using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using Overclock.Core;
using UnityEditor;
using UnityEngine;

namespace Overclock.Tests
{
    public class BalanceUpdateTests
    {
        private readonly List<UnityEngine.Object> objects = new List<UnityEngine.Object>();
        private PartData[] parts;
        private ModuleData rare, sr, compound, flat, multiplier, extra;
        private ModuleData[] pool;

        [SetUp]
        public void Setup()
        {
            parts = new[] { Part("n", Grade.N, 1), Part("r", Grade.R, 5), Part("sr", Grade.SR, 18) };
            rare = Module("rare", ModuleAxis.Probability, new ModifierEffect(ModifierType.RareChance, GradeFilter.Any, .15f));
            sr = Module("sr", ModuleAxis.Probability, new ModifierEffect(ModifierType.SuperRareChance, GradeFilter.Any, .08f));
            compound = Module("compound", ModuleAxis.Probability,
                new ModifierEffect(ModifierType.RareChance, GradeFilter.Any, .1f),
                new ModifierEffect(ModifierType.SuperRareChance, GradeFilter.Any, .03f));
            flat = Module("flat", ModuleAxis.Conditional, new ModifierEffect(ModifierType.FlatBonus, GradeFilter.Any, 1));
            multiplier = Module("multiplier", ModuleAxis.Multiplier, new ModifierEffect(ModifierType.OutputMultiplier, GradeFilter.SR, 1));
            extra = Module("extra", ModuleAxis.Draws, new ModifierEffect(ModifierType.ExtraDraws, GradeFilter.Any, 10));
            pool = new[] { rare, sr, compound, flat, multiplier, extra };
        }

        [TearDown]
        public void Cleanup()
        {
            foreach (var obj in objects) UnityEngine.Object.DestroyImmediate(obj);
            objects.Clear();
        }

        private PartData Part(string id, Grade grade, int output)
        {
            var part = ScriptableObject.CreateInstance<PartData>(); objects.Add(part);
            part.InitializeForTests(id, grade, output); return part;
        }

        private ModuleData Module(string id, ModuleAxis axis, params ModifierEffect[] effects)
        {
            var module = ScriptableObject.CreateInstance<ModuleData>(); objects.Add(module);
            module.InitializeForTests(id, axis, effects); module.SetIdForTests(id); return module;
        }

        private RunSession Session() => RunSession.Create(123, parts, pool);
        private void CapBoth(RunManager run)
        {
            run.EquipModule(rare); run.EquipModule(rare);
            run.EquipModule(sr); run.EquipModule(sr); run.EquipModule(sr);
        }

        [Test]
        public void RealPartAssets_AndOverclock_MatchApprovedValues()
        {
            var assets = AssetDatabase.FindAssets("t:PartData", new[] { "Assets/02.Data/Parts" })
                .Select(guid => AssetDatabase.LoadAssetAtPath<PartData>(AssetDatabase.GUIDToAssetPath(guid))).ToArray();
            Assert.AreEqual(4, assets.Count(p => p.Grade == Grade.R));
            Assert.AreEqual(2, assets.Count(p => p.Grade == Grade.SR));
            var engine = new ModifierEngine(.72, .25, .03);
            foreach (var part in assets)
            {
                Assert.AreEqual(part.Grade == Grade.N ? 1 : part.Grade == Grade.R ? 5 : 18, part.BaseOutput, part.Id);
                Assert.AreEqual(part.Grade == Grade.N ? 0 : part.Grade == Grade.R ? 20 : 72, engine.CalculateOverclockedOutput(part, false), part.Id);
            }
            engine.Equip(multiplier);
            Assert.AreEqual(90, engine.CalculateOverclockedOutput(parts[2], false));
            engine.Equip(multiplier); engine.Equip(multiplier); engine.Equip(multiplier); engine.Equip(multiplier);
            Assert.AreEqual(144, engine.CalculateOverclockedOutput(parts[2], false), "18 × 8 상한");
        }

        [Test]
        public void TwentiethDraw_IsGuaranteedAcrossRoundBoundary()
        {
            var session = Session(); var run = session.RunManager;
            run.EquipModule(extra);
            session.GachaEngine.SetProbabilities(1, 0, 0);
            for (int i = 0; i < 8; i++) Assert.AreEqual(Grade.N, run.Draw().Part.Grade);
            Assert.AreEqual(RunState.RoundCleared, run.State);
            run.AdvanceRound();
            Assert.AreEqual(8, session.GachaEngine.DrawsWithoutSuperRare);
            for (int i = 8; i < 19; i++) Assert.AreEqual(Grade.N, run.Draw().Part.Grade);
            Assert.AreEqual(Grade.SR, run.Draw().Part.Grade);
            Assert.AreEqual(0, session.GachaEngine.DrawsWithoutSuperRare);
        }

        [Test]
        public void NaturalSuperRare_RestartsTwentyDrawGuarantee()
        {
            var gacha = Session().GachaEngine;
            gacha.SetProbabilities(1, 0, 0);
            for (int i = 0; i < 10; i++) gacha.Draw();
            gacha.SetProbabilities(0, 0, 1); gacha.Draw();
            Assert.AreEqual(0, gacha.DrawsWithoutSuperRare);
            gacha.SetProbabilities(1, 0, 0);
            for (int i = 0; i < 19; i++) Assert.AreEqual(Grade.N, gacha.Draw());
            Assert.AreEqual(Grade.SR, gacha.Draw());
        }

        [TestCase(0, "SR 보장까지 20회")]
        [TestCase(18, "SR 보장까지 2회")]
        [TestCase(19, "다음 뽑기 SR 확정")]
        [TestCase(50, "다음 뽑기 SR 확정")]
        public void Gauge_ReportsRemainingDrawsIncludingGuaranteedDraw(int counter, string expected)
        {
            // Presentation은 Assembly-CSharp에 있으므로 테스트 어셈블리에서 직접 의존하지 않음.
            var type = Type.GetType("Overclock.Presentation.GaugePanelView, Assembly-CSharp");
            Assert.IsNotNull(type);
            Assert.AreEqual(expected, type.GetMethod("FormatPity").Invoke(null, new object[] { counter, 19 }));
        }

        [TestCase(19)]
        [TestCase(20)]
        [TestCase(50)]
        public void VersionOne_OverdueCounterSurvivesFileResave_ThenForcesSR(int counter)
        {
            var data = Session().CaptureSaveData(); data.version = 1;
            data.round = 6; data.production = 8; data.drawsRemaining = 2;
            data.streakGrade = Grade.N; data.streakCount = 2;
            data.roundPartIds = Enumerable.Repeat("n", 8).ToArray();
            data.drawsWithoutSuperRare = counter;
            string directory = Path.Combine(Path.GetTempPath(), "OverclockBalance-" + Guid.NewGuid().ToString("N"));
            try
            {
                var file = new RunSaveFile(directory); file.Write(data);
                Assert.IsTrue(file.TryLoad(parts, pool, out var migrated, out var error), error);
                Assert.AreEqual(counter, migrated.GachaEngine.DrawsWithoutSuperRare);
                Assert.AreEqual(8, migrated.RunManager.RoundParts.Count);
                Assert.AreEqual(data.randomState, migrated.CaptureSaveData().randomState);
                file.Save(migrated);
                Assert.AreEqual(2, migrated.CaptureSaveData().version);
                Assert.AreEqual(counter > 19, migrated.CaptureSaveData().legacyPityPending);
                Assert.IsTrue(file.TryLoad(parts, pool, out var restored, out error), error);
                Assert.AreEqual(8, restored.RunManager.RoundParts.Count);
                Assert.AreEqual(Grade.SR, restored.RunManager.Draw().Part.Grade);
                Assert.AreEqual(0, restored.GachaEngine.DrawsWithoutSuperRare);
                Assert.IsFalse(restored.CaptureSaveData().legacyPityPending);
                file.Save(restored);
                Assert.IsTrue(file.TryLoad(parts, pool, out var afterSR, out error), error);
                Assert.AreEqual(9, afterSR.RunManager.RoundParts.Count);
            }
            finally { if (Directory.Exists(directory)) Directory.Delete(directory, true); }
        }

        [TestCase(1, false, -1)]
        [TestCase(1, false, 51)]
        [TestCase(2, false, 20)]
        [TestCase(2, true, 19)]
        [TestCase(2, true, 51)]
        public void InvalidPityMigration_IsRejected(int version, bool legacy, int counter)
        {
            var data = Session().CaptureSaveData(); data.version = version;
            data.drawsWithoutSuperRare = counter; data.legacyPityPending = legacy;
            Assert.Catch<ArgumentException>(() => RunSession.Restore(data, parts, pool));
        }

        [Test]
        public void ProbabilityFilter_KeepsPartialAndMixedEffects()
        {
            var run = Session().RunManager;
            run.EquipModule(rare); run.EquipModule(rare);
            Assert.IsFalse(run.CanOfferModule(rare));
            Assert.IsTrue(run.CanOfferModule(compound), "SR 증가분이 남음");
            run.EquipModule(sr); run.EquipModule(sr); run.EquipModule(sr);
            Assert.IsFalse(run.CanOfferModule(sr)); Assert.IsFalse(run.CanOfferModule(compound));
            Assert.IsTrue(run.CanOfferModule(flat));
            var mixed = Module("mixed", ModuleAxis.Probability,
                new ModifierEffect(ModifierType.RareChance, GradeFilter.Any, .1f),
                new ModifierEffect(ModifierType.FlatBonus, GradeFilter.Any, 1));
            Assert.IsTrue(run.CanOfferModule(mixed));
            var other = Session().RunManager;
            other.EquipModule(sr); other.EquipModule(sr); other.EquipModule(sr);
            Assert.IsTrue(other.CanOfferModule(compound), "R 증가분이 남음");
        }

        [Test]
        public void NewOffersAndRerolls_ExcludeCappedAxis_KeepThreeCardsAndRestore()
        {
            var session = Session(); CapBoth(session.RunManager);
            session.RunManager.RestoreProgress(1, 8, 2, RunState.RoundCleared, Grade.N, 2);
            session.RunManager.MarkRoundPartsUnknown();
            for (int i = 0; i < 30; i++)
            {
                var offer = session.ModuleOffer.CreateOffer();
                Assert.AreEqual(3, offer.Count);
                Assert.IsTrue(offer.All(m => m.Axis != ModuleAxis.Probability));
            }
            Assert.IsTrue(session.OpenOffer().All(m => m.Axis != ModuleAxis.Probability));
            var rerolled = session.RerollOffer().ToArray();
            Assert.IsTrue(rerolled.All(m => m.Axis != ModuleAxis.Probability));
            var saved = session.CaptureSaveData();
            var restored = RunSession.Restore(saved, parts, pool);
            CollectionAssert.AreEqual(rerolled, restored.CurrentOffer);
            Assert.AreEqual(saved.randomState, restored.CaptureSaveData().randomState);
            Assert.IsTrue(restored.OfferRerolled);
        }

        [Test]
        public void OldOpenOffer_IsPreservedUntilSelectionOrReroll()
        {
            var session = Session(); CapBoth(session.RunManager);
            var data = session.CaptureSaveData(); data.version = 1;
            data.round = 1; data.production = 8; data.drawsRemaining = 2;
            data.state = RunState.RoundCleared; data.roundPartsRecorded = false;
            data.offeredModuleIds = new[] { rare.Id, flat.Id, extra.Id };
            var restored = RunSession.Restore(data, parts, pool);
            CollectionAssert.Contains(restored.CurrentOffer, rare);
            var again = RunSession.Restore(restored.CaptureSaveData(), parts, pool);
            CollectionAssert.Contains(again.CurrentOffer, rare);
            Assert.IsTrue(again.RerollOffer().All(m => m.Axis != ModuleAxis.Probability));
            restored.SelectModule(rare);
            Assert.AreEqual(2, restored.RunManager.CurrentRound);
        }
    }
}

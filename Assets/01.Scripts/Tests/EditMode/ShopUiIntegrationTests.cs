using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using Overclock.Core;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace Overclock.Tests
{
    public sealed class ShopUiIntegrationTests
    {
        private readonly List<Object> objects = new List<Object>();
        private PartData[] parts;
        private ModuleData[] modules;
        private ShopSettings shop;
        private RunSession session;
        private RunSaveFile file;
        private string directory;
        private Component view;
        private int saves;
        private bool left;

        [SetUp]
        public void Setup()
        {
            saves = 0; left = false; view = null;
            // OpenScene은 저장되지 않은 ScriptableObject도 정리하므로 테스트 데이터보다 먼저 연다.
            EditorSceneManager.OpenScene("Assets/Scenes/Run.unity");
            parts = new PartData[3];
            for (int i = 0; i < 3; i++)
            {
                parts[i] = Make<PartData>();
                parts[i].InitializeForTests("part" + i, (Grade)i, 11, PartTag.Mechanical);
            }
            modules = new ModuleData[4];
            for (int i = 0; i < 4; i++)
            {
                modules[i] = Make<ModuleData>();
                modules[i].InitializeForTests("module" + i, (ModuleAxis)i,
                    new ModifierEffect(ModifierType.FlatBonus, GradeFilter.Any, 0));
                modules[i].SetIdForTests("module" + i);
            }
            shop = Make<ShopSettings>();
            var items = new ShopItemData[3];
            for (int i = 0; i < 3; i++)
            {
                items[i] = Make<ShopItemData>();
                items[i].InitializeForTests("item" + i, i == 0 ? ShopItemKind.Part : ShopItemKind.Service,
                    (ShopItemEffect)i, i == 0 ? 16 : i == 1 ? 5 : 0, i == 2 ? 2 : 0);
            }
            shop.InitializeForTests(2, items);
            directory = Path.Combine(Path.GetTempPath(), "OverclockShopUI_" + Guid.NewGuid().ToString("N"));
            file = new RunSaveFile(directory);
            session = RunSession.Create(42, parts, modules, RunMode.Endless, shop);
            ReachShop();
            Assert.AreEqual(17, session.Wallet.Balance);
        }

        private T Make<T>() where T : ScriptableObject { var obj = ScriptableObject.CreateInstance<T>(); objects.Add(obj); return obj; }
        private void ReachShop()
        {
            while (!session.IsShopOpen)
            {
                while (session.RunManager.State == RunState.Drawing) session.RunManager.Draw();
                session.OpenOffer(); session.SelectModule(session.CurrentOffer[0]);
            }
        }
        private RunSession RoundTrip()
        {
            file.Save(session);
            Assert.IsTrue(file.TryLoad(parts, modules, shop, out RunSession loaded, out string error), error);
            Assert.AreEqual(JsonUtility.ToJson(session.CaptureSaveData()), JsonUtility.ToJson(loaded.CaptureSaveData()));
            return loaded;
        }

        [Test]
        public void File_OpenShopAndReplacement_RestoreExactRandomState()
        {
            Assert.IsTrue(RoundTrip().IsShopOpen);
            session.DecomposeModule(0);
            RunSession loaded = RoundTrip();
            Assert.IsTrue(loaded.IsChoosingReplacement);
            Assert.AreEqual(3, loaded.ReplacementOffer.Count);
            Assert.Throws<InvalidOperationException>(() => loaded.LeaveShop());
            session.SelectReplacement(session.ReplacementOffer[1]);
            loaded.SelectReplacement(loaded.ReplacementOffer[1]);
            session.LeaveShop(); loaded.LeaveShop();
            Assert.AreEqual(JsonUtility.ToJson(session.CaptureSaveData()), JsonUtility.ToJson(loaded.CaptureSaveData()));
            Assert.AreEqual(session.RunManager.Draw().Part.Id, loaded.RunManager.Draw().Part.Id);
        }

        [Test]
        public void File_FourCardOffer_DoesNotConsumeSpareAgainAfterRestore()
        {
            session.BuyItem(shop.Items[1]); session.LeaveShop();
            while (session.RunManager.State == RunState.Drawing) session.RunManager.Draw();
            session.OpenOffer();
            Assert.AreEqual(4, session.CurrentOffer.Count);
            RunSession loaded = RoundTrip();
            Assert.AreEqual(0, loaded.SpareCards);
            Assert.AreEqual(4, loaded.OpenOffer().Count);
            Assert.AreEqual(0, loaded.SpareCards);
            Assert.AreEqual(4, loaded.RerollOffer().Count);
        }

        [Test]
        public void File_OldSessionLoadsWithShop_ButShopSaveRequiresSettings()
        {
            var old = RunSession.Create(42, parts, modules);
            file.Save(old);
            Assert.IsTrue(file.TryLoad(parts, modules, shop, out RunSession loaded, out string error), error);
            Assert.IsTrue(loaded.HasShop);
            file.Save(session);
            Assert.IsFalse(file.TryLoad(parts, modules, out loaded, out error));
            Assert.IsNotNull(error);
        }

        private void OpenView(bool restored = true, RevealSpeed speed = RevealSpeed.Instant)
        {
            Assert.IsNotNull(session.FindShopItem(ShopItemEffect.Decompose));
            var type = Type.GetType("Overclock.Presentation.ShopView, Assembly-CSharp", true);
            view = (Component)Object.FindFirstObjectByType(type, FindObjectsInactive.Include);
            Assert.IsNotNull(view, "상점 씬 배치 필요");
            foreach (Component card in (Array)Get(view, "replacementCards")) Call(card, "Awake");
            Call(view, "Show", session, new GameSettings { RevealSpeed = speed },
                (Action)(() => { saves++; file.Save(session); }), (Action)(() => left = true), (Func<bool>)(() => true), restored);
            Unlock();
        }

        private void Unlock() { Set(view, "lockedFrame", -1); Set(view, "nextActionTime", -1f); }
        private static object Get(object target, string name) => target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public).GetValue(target);
        private static void Set(object target, string name, object value) => target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public).SetValue(target, value);
        private static object Call(object target, string name, params object[] args) => target.GetType().GetMethod(name, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public).Invoke(target, args);
        private Button ProductButton(int index) => (Button)Get(((Array)Get(view, "products")).GetValue(index), "button");

        [Test]
        public void View_PurchaseIsSavedOnce_ImmediateDuplicateAndPauseAreBlocked()
        {
            OpenView();
            ProductButton(1).onClick.Invoke(); ProductButton(1).onClick.Invoke();
            Assert.AreEqual(1, session.SpareCards); Assert.AreEqual(12, session.Wallet.Balance); Assert.AreEqual(1, saves);
            Assert.IsTrue(file.TryLoad(parts, modules, shop, out RunSession loaded, out string error), error);
            Assert.AreEqual(1, loaded.SpareCards);
            Unlock(); Set(view, "inputAllowed", (Func<bool>)(() => false));
            ProductButton(1).onClick.Invoke(); Assert.AreEqual(1, session.SpareCards);
        }

        [Test]
        public void View_DecomposeCancellationThenCommit_RestoreLocksExitUntilReplacement()
        {
            OpenView(); ProductButton(2).onClick.Invoke();
            Unlock(); ((Button)Get(view, "backButton")).onClick.Invoke();
            Assert.AreEqual(17, session.Wallet.Balance); Assert.AreEqual(0, saves);
            Unlock(); ProductButton(2).onClick.Invoke();
            Set(view, "selectedIndex", 0); Call(view, "RenderSelection"); Unlock();
            ((Button)Get(view, "confirmButton")).onClick.Invoke();
            Assert.IsTrue(session.IsChoosingReplacement); Assert.AreEqual(19, session.Wallet.Balance); Assert.AreEqual(1, saves);
            Assert.IsFalse(((Button)Get(view, "leaveButton")).interactable);
            session = RoundTrip();
            Call(view, "Show", session, new GameSettings { RevealSpeed = RevealSpeed.Instant },
                (Action)(() => { saves++; file.Save(session); }), (Action)(() => left = true), (Func<bool>)(() => true), true);
            Unlock(); Call(view, "Leave"); Assert.IsFalse(left);
            Call(view, "SelectReplacement", session.ReplacementOffer[0]);
            Assert.IsFalse(session.IsChoosingReplacement); Assert.IsTrue(session.DecomposedThisVisit);
            Assert.IsFalse(ProductButton(2).interactable);
            Unlock(); ((Button)Get(view, "leaveButton")).onClick.Invoke();
            Assert.IsTrue(left); Assert.AreEqual(3, session.RunManager.CurrentRound);
        }

        [Test]
        public void View_SkipFinishesOnlyPresentation_AndBlocksSameFramePurchase()
        {
            OpenView(false, RevealSpeed.Normal);
            Assert.IsTrue((bool)Get(view, "busy"));
            Call(view, "Skip"); ProductButton(1).onClick.Invoke();
            Assert.AreEqual(0, session.SpareCards); Assert.AreEqual(0, saves);
            Unlock(); ProductButton(1).onClick.Invoke();
            Assert.AreEqual(1, session.SpareCards); Assert.AreEqual(1, saves);
            Call(view, "Skip"); Assert.AreEqual(1, saves);
        }

        [Test]
        public void View_NoMoneyAndSoldOut_StayInTheirOriginalSlots()
        {
            OpenView();
            Transform first = ProductButton(0).transform.parent;
            ProductButton(0).onClick.Invoke();
            Assert.IsTrue(session.HasSupercharger); Assert.AreEqual(1, session.Wallet.Balance);
            Assert.IsFalse(ProductButton(0).interactable); Assert.IsFalse(ProductButton(1).interactable);
            Assert.AreSame(first, ProductButton(0).transform.parent); Assert.IsTrue(first.gameObject.activeSelf);
        }

        [TestCase(RevealSpeed.Normal, .28f)]
        [TestCase(RevealSpeed.Fast, .14f)]
        [TestCase(RevealSpeed.Instant, 0f)]
        public void View_EntranceSpeedAndRestore(RevealSpeed speed, float expected)
        {
            OpenView(false, speed); Assert.AreEqual(expected, (float)Get(view, "duration"), .001f);
            Call(view, "Hide");
            Call(view, "Show", session, new GameSettings(), (Action)(() => {}), (Action)(() => {}), (Func<bool>)(() => true), true);
            Assert.IsFalse((bool)Get(view, "busy"));
        }

        [Test]
        public void View_TargetRowsHaveReadableWidth_CounterMasksAdjacentDigits()
        {
            OpenView(); ProductButton(2).onClick.Invoke();
            Canvas.ForceUpdateCanvases();
            var content = (RectTransform)Get(view, "listContent");
            LayoutRebuilder.ForceRebuildLayoutImmediate(content);
            var rows = (List<Button>)Get(view, "rows");
            Assert.AreEqual(2, rows.Count);
            foreach (Button row in rows) Assert.Greater(((RectTransform)row.transform).rect.width, 200f);
            var counter = (Component)Get(view, "credits");
            var strips = (RectTransform[])Get(counter, "digitStrips");
            Assert.AreEqual(5, strips.Length);
            foreach (var strip in strips)
                Assert.LessOrEqual(((RectTransform)strip.parent).rect.height, (float)Get(counter, "digitHeight"));
            for (int i = 0; i < 30; i++) session.RunManager.EquipModule(modules[i % 4]);
            Call(view, "BuildRows");
            Canvas.ForceUpdateCanvases(); LayoutRebuilder.ForceRebuildLayoutImmediate(content);
            Assert.AreEqual(32, rows.Count);
            Assert.Greater(content.rect.height, ((RectTransform)content.parent).rect.height);
            Unlock(); rows[31].onClick.Invoke();
            Assert.AreEqual(31, (int)Get(view, "selectedIndex"));
        }

        [Test]
        public void View_FourNormalCardsFitAndDoNotOverlap()
        {
            OpenView();
            Type type = Type.GetType("Overclock.Presentation.GrayBoxRunScreen, Assembly-CSharp", true);
            var screen = (Component)Object.FindFirstObjectByType(type, FindObjectsInactive.Include);
            var cards = (Array)Get(screen, "moduleCards");
            Assert.AreEqual(4, cards.Length);
            Call(screen, "LayoutOfferCards", 4);
            var ordered = cards.Cast<Component>().Select(card => (RectTransform)card.transform).OrderBy(rect => rect.anchoredPosition.x).ToArray();
            for (int i = 0; i < ordered.Length - 1; i++)
            {
                float right = ordered[i].anchoredPosition.x + ordered[i].rect.width * ordered[i].localScale.x / 2f;
                float nextLeft = ordered[i+1].anchoredPosition.x - ordered[i+1].rect.width * ordered[i+1].localScale.x / 2f;
                Assert.Less(right, nextLeft);
            }
            Assert.LessOrEqual(ordered[3].anchoredPosition.x + ordered[3].rect.width * ordered[3].localScale.x / 2f, 960f);
        }

        [TearDown]
        public void Cleanup()
        {
            if (view != null) Call(view, "Hide");
            foreach (Object obj in objects) Object.DestroyImmediate(obj);
            objects.Clear();
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene);
            if (Directory.Exists(directory)) Directory.Delete(directory, true);
        }
    }
}

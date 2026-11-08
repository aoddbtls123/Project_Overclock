using System;
using System.Collections.Generic;
using Overclock.Core;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem;

namespace Overclock.Presentation
{
    public sealed class ShopView : MonoBehaviour
    {
        [Serializable]
        public sealed class Product
        {
            public ShopItemEffect effect;
            public RectTransform plate;
            public TMP_Text title, description, price, status;
            public Button button;
            public TMP_Text buttonLabel;
            public RectTransform icon;
        }

        [SerializeField] private ShopMotionSettings motion;
        [SerializeField] private RectTransform trade;
        [SerializeField] private GameObject productsPage, targetsPage, replacementPage;
        [SerializeField] private Product[] products;
        [SerializeField] private MechanicalCounterView credits;
        [SerializeField] private TMP_Text exactCredits, selectionText, replacementHint;
        [SerializeField] private RectTransform listContent;
        [SerializeField] private Button rowTemplate, confirmButton, backButton, leaveButton;
        [SerializeField] private ModuleCardView[] replacementCards;
        private readonly List<Button> rows = new List<Button>();
        private RunSession session;
        private GameSettings options;
        private Action save, leave;
        private Func<bool> inputAllowed;
        private int selectedIndex = -1;
        private int lockedFrame = -1;
        private float nextActionTime;
        private float elapsed, duration;
        private int reaction = -1;
        private bool choosingTarget, busy;
        private bool animateMotion;
        private Vector2 tradeRest;
        private Vector2[] plateRest, iconRest;
        private bool initialized;
        private bool blockedPointer;
        public int ReactionStartFrame { get; private set; }

        public bool IsPlaying => busy;
        public bool CanAct => session != null && session.IsShopOpen && !busy &&
            Time.frameCount > lockedFrame && Time.unscaledTime >= nextActionTime &&
            inputAllowed != null && inputAllowed() &&
            !(blockedPointer && Mouse.current != null && Mouse.current.leftButton.wasReleasedThisFrame);

        private void Initialize()
        {
            if (initialized) return;
            initialized = true;
            tradeRest = trade.anchoredPosition;
            plateRest = new Vector2[products.Length];
            iconRest = new Vector2[products.Length];
            for (int i = 0; i < products.Length; i++)
            {
                int index = i;
                plateRest[i] = products[i].plate.anchoredPosition;
                iconRest[i] = products[i].icon.anchoredPosition;
                Bind(products[i].button, () => Buy(index));
            }
            Bind(confirmButton, ConfirmDecompose);
            Bind(backButton, () => { LockInput(); choosingTarget = false; Render(); });
            Bind(leaveButton, Leave);
        }

        private void Bind(Button button, Action action)
        {
            var guarded = button.GetComponent<ShopActionButton>();
            if (guarded == null) guarded = button.gameObject.AddComponent<ShopActionButton>();
            guarded.Bind(() => CanAct && button.interactable, action);
        }

        public void Show(RunSession session, GameSettings options, Action save, Action leave,
            Func<bool> inputAllowed, bool restored)
        {
            Initialize();
            ResetMotion();
            this.session = session;
            this.options = options;
            this.save = save;
            this.leave = leave;
            this.inputAllowed = inputAllowed;
            choosingTarget = false;
            selectedIndex = -1;
            gameObject.SetActive(true);
            credits.SetPlaybackSpeed(options.RevealTimeMultiplier > 0f ? 1f / options.RevealTimeMultiplier : 0f);
            credits.gameObject.SetActive(session.Wallet.Balance <= 99999);
            credits.SetTarget(Mathf.Min(session.Wallet.Balance, 99999));
            credits.CompleteNow();
            LockInput();
            Render();
            if (!restored) StartReaction(-1, motion.entranceSeconds);
        }

        public void Hide()
        {
            ResetMotion();
            session = null;
            gameObject.SetActive(false);
        }

        private void OnDisable() { ResetMotion(); }
        private void OnEnable()
        {
            if (initialized && session != null) { LockInput(); Render(); }
        }

        private void LockInput()
        {
            lockedFrame = Time.frameCount;
            nextActionTime = Time.unscaledTime + motion.repeatGuardSeconds;
        }

        private void Buy(int index)
        {
            if (!CanAct || session.IsChoosingReplacement) return;
            ShopItemData item = session.FindShopItem(products[index].effect);
            if (item == null || session.GetItemStatus(item) != ShopItemStatus.Available) return;
            LockInput();
            if (item.Effect == ShopItemEffect.Decompose)
            {
                choosingTarget = true;
                selectedIndex = -1;
                BuildRows();
                Render();
                return;
            }
            session.BuyItem(item);
            save();
            Render();
            StartReaction(index, motion.purchaseSeconds);
        }

        private void BuildRows()
        {
            foreach (Button row in rows) row.gameObject.SetActive(false);
            for (int i = 0; i < session.RunManager.EquippedModules.Count; i++)
            {
                int index = i;
                if (i == rows.Count) rows.Add(Instantiate(rowTemplate, listContent));
                Button row = rows[i];
                row.gameObject.SetActive(true);
                row.GetComponentInChildren<TMP_Text>().text = (i + 1) + ". " + session.RunManager.EquippedModules[i].DisplayName;
                Bind(row, () => { selectedIndex = index; LockInput(); RenderSelection(); });
            }
        }

        private void RenderSelection()
        {
            ShopItemData item = session.FindShopItem(ShopItemEffect.Decompose);
            bool valid = selectedIndex >= 0 && selectedIndex < session.RunManager.EquippedModules.Count;
            confirmButton.interactable = valid;
            selectionText.text = "분해할 모듈을 선택하세요.";
            if (!valid) return;
            ModuleData module = session.RunManager.EquippedModules[selectedIndex];
            selectionText.text = module.DisplayName + "\n" + module.Description +
                "\n\n이 모듈 제거 · 비용 " + item.Price + " C · " + item.Refund +
                " C 환급\n새 제안 3장 중 1개 선택\n확정 후에는 취소할 수 없습니다.";
        }

        private void ConfirmDecompose()
        {
            if (!CanAct || !choosingTarget || selectedIndex < 0 ||
                selectedIndex >= session.RunManager.EquippedModules.Count) return;
            ShopItemData item = session.FindShopItem(ShopItemEffect.Decompose);
            if (session.GetItemStatus(item) != ShopItemStatus.Available) return;
            LockInput();
            session.DecomposeModule(selectedIndex);
            choosingTarget = false;
            save();
            Render();
            StartReaction(-2, motion.decomposeSeconds);
        }

        private void SelectReplacement(ModuleData module)
        {
            if (!CanAct || !session.IsChoosingReplacement) return;
            LockInput();
            session.SelectReplacement(module);
            save();
            Render();
        }

        private void Leave()
        {
            if (!CanAct || session.IsChoosingReplacement) return;
            LockInput();
            session.LeaveShop();
            save();
            Hide();
            leave();
        }

        private void Render()
        {
            bool replacement = session.IsChoosingReplacement;
            productsPage.SetActive(!choosingTarget && !replacement);
            targetsPage.SetActive(choosingTarget && !replacement);
            replacementPage.SetActive(replacement);
            leaveButton.interactable = !replacement;
            credits.SetPlaybackSpeed(options.RevealTimeMultiplier > 0f ? 1f / options.RevealTimeMultiplier : 0f);
            credits.gameObject.SetActive(session.Wallet.Balance <= 99999);
            credits.SetTarget(Mathf.Min(session.Wallet.Balance, 99999));
            // 바퀴가 움직이는 동안에도 실제 잔액은 읽을 수 있고 int 전 범위를 표시한다.
            exactCredits.text = session.Wallet.Balance.ToString("N0") + " C";
            if (options.RevealTimeMultiplier == 0f) credits.CompleteNow();
            foreach (Product product in products)
            {
                ShopItemData item = session.FindShopItem(product.effect);
                if (item == null) { product.button.interactable = false; continue; }
                product.title.text = item.DisplayName;
                product.description.text = item.Description;
                product.price.text = item.Effect == ShopItemEffect.Decompose
                    ? "비용 " + item.Price + " C · 환급 " + item.Refund + " C" : item.Price + " C";
                ShopItemStatus status = session.GetItemStatus(item);
                string owned = item.Effect == ShopItemEffect.SpareCard ? "보유 " + session.SpareCards + "개" :
                    item.Effect == ShopItemEffect.Decompose ? "방문당 1회" : "이번 런 동안 유지";
                product.status.text = status == ShopItemStatus.SoldOut
                    ? (item.Effect == ShopItemEffect.Decompose ? "이번 방문 이용 완료" : "장착 완료")
                    : status == ShopItemStatus.Unavailable ? "분해할 모듈 없음"
                    : status == ShopItemStatus.NotEnoughCredits ? owned + "\n크레딧 부족" : owned;
                product.button.interactable = status == ShopItemStatus.Available;
                product.buttonLabel.text = item.Effect == ShopItemEffect.Decompose ? "분해할 모듈 선택" : "구매";
            }
            if (choosingTarget) RenderSelection();
            replacementHint.text = "교체할 모듈을 선택하세요.";
            for (int i = 0; i < replacementCards.Length; i++)
            {
                if (replacement && i < session.ReplacementOffer.Count)
                {
                    replacementCards[i].Show(session.ReplacementOffer[i], "장착 모듈로 교체", SelectReplacement);
                    replacementCards[i].SetSelectable(true);
                }
                else replacementCards[i].Hide();
            }
        }

        private void StartReaction(int kind, float seconds)
        {
            ReactionStartFrame = Time.frameCount;
            reaction = kind;
            elapsed = 0f;
            duration = seconds * options.RevealTimeMultiplier;
            animateMotion = options.ShakeEnabled;
            busy = duration > 0f;
            if (!busy) { Skip(); return; }
            if (kind == -2)
            {
                replacementHint.text = "모듈 교체 작업 중…";
                foreach (ModuleCardView card in replacementCards) card.gameObject.SetActive(false);
            }
            ApplyMotion(0f);
        }

        private void Update()
        {
            if (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame)
                blockedPointer = !CanAct;
            if (!busy || Time.deltaTime <= 0f) return;
            elapsed += Time.deltaTime;
            float progress = Mathf.Clamp01(elapsed / duration);
            ApplyMotion(progress);
            if (progress >= 1f) Skip();
        }

        private void ApplyMotion(float progress)
        {
            if (!animateMotion || (options != null && !options.ShakeEnabled)) { RestorePositions(); return; }
            if (reaction == -1)
                trade.anchoredPosition = tradeRest + Vector2.down * (motion.entranceDistance * Mathf.Pow(1f - progress, 3f));
            else if (reaction == -2)
                trade.anchoredPosition = tradeRest + Vector2.down * (Mathf.Sin(progress * Mathf.PI * 4f) * (1f - progress) * motion.fasteningDistance);
            else if (reaction >= 0)
            {
                Product product = products[reaction];
                if (product.effect == ShopItemEffect.SpareCard)
                    product.icon.anchoredPosition = iconRest[reaction] + Vector2.down * (Mathf.Sin(progress * Mathf.PI) * motion.cardTravel);
                else product.plate.anchoredPosition = plateRest[reaction] + Vector2.down * (Mathf.Sin(progress * Mathf.PI) * motion.fasteningDistance);
            }
        }

        private void RestorePositions()
        {
            if (!initialized) return;
            trade.anchoredPosition = tradeRest;
            for (int i = 0; i < products.Length; i++)
            {
                products[i].plate.anchoredPosition = plateRest[i];
                products[i].icon.anchoredPosition = iconRest[i];
            }
        }

        private void ResetMotion() { busy = false; RestorePositions(); }

        public void Skip()
        {
            ResetMotion();
            lockedFrame = Time.frameCount;
            credits.CompleteNow();
            if (session != null) Render();
        }
    }
}

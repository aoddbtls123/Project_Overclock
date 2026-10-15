using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Overclock.Core;
using TMPro;
using UnityEngine.UI;
using UnityEngine.InputSystem;

namespace Overclock.Presentation
{
    public sealed class GrayBoxRunScreen : MonoBehaviour
    {
        [SerializeField, Min(0)] private int seed;
        [SerializeField] private PartData[] parts;
        [SerializeField] private ModuleData[] modules;
        [SerializeField] private RevealTimings revealTimings;

        [SerializeField] private TMP_Text roundText;
        [SerializeField] private TMP_Text quotaText;
        [SerializeField] private Slider quotaGauge;
        [SerializeField] private TMP_Text drawsText;
        [SerializeField] private TMP_Text gaugePanelText;
        [SerializeField] private TMP_Text lastPartText;
        [SerializeField] private TMP_Text resultText;
        [SerializeField] private TMP_Text equippedText;
        [SerializeField] private Button drawButton;
        [SerializeField] private Button restartButton;
        [SerializeField] private GameObject moduleSelectPanel;
        [SerializeField] private ModuleCardView[] moduleCards;

        private readonly List<ModuleData> equippedModules = new List<ModuleData>();
        private GachaEngine gachaEngine;
        private RunManager runManager;
        private ModuleOffer moduleOffer;
        private bool isRevealing;
        private bool skipRequested;
        private int revealStartFrame;
        private bool ignoreDrawButtonClick;

        private void Awake()
        {
            drawButton.onClick.AddListener(OnDrawButtonClicked);
            restartButton.onClick.AddListener(StartNewRun);
        }

        private void Start()
        {
            StartNewRun();
        }

        private void Update()
        {
            Keyboard keyboard = Keyboard.current;
            Mouse mouse = Mouse.current;
            UpdateDrawButtonGuard(mouse);

            if (isRevealing)
            {
                if (IsSkipPressed(keyboard, mouse))
                {
                    skipRequested = true;
                }
            }
            else if (keyboard != null && keyboard.spaceKey.wasPressedThisFrame && runManager.State == RunState.Drawing)
            {
                OnDrawClicked();
            }
        }

        private void UpdateDrawButtonGuard(Mouse mouse)
        {
            if (mouse == null)
            {
                return;
            }

            // 버튼은 누른 대상을 기억했다가 뗄 때 그 순간 켜져 있으면 클릭으로 처리한다.
            // 연출 중에 시작된 누름은 공개 뒤에 떼도 뽑기가 되지 않게 한다. 다음 누름이 시작되면 다시 판단한다
            if (mouse.leftButton.wasPressedThisFrame)
            {
                ignoreDrawButtonClick = isRevealing;
            }
        }

        private bool IsSkipPressed(Keyboard keyboard, Mouse mouse)
        {
            // 연출을 시작한 입력(버튼을 뗀 순간·Space)이 같은 프레임에 스킵으로 잡히지 않게 한다
            if (Time.frameCount <= revealStartFrame)
            {
                return false;
            }

            if (keyboard != null && (keyboard.spaceKey.wasPressedThisFrame || keyboard.escapeKey.wasPressedThisFrame))
            {
                return true;
            }

            return mouse != null && mouse.leftButton.wasReleasedThisFrame;
        }

        private void StartNewRun()
        {
            // 자동 시드는 Inspector 의 int 칸에 다시 넣을 수 있는 1 ~ int.MaxValue 로 제한한다
            uint runSeed = (uint)seed;
            if (runSeed == 0)
            {
                runSeed = unchecked((uint)Environment.TickCount) & 0x7FFFFFFFu;
                if (runSeed == 0)
                {
                    runSeed = 1;
                }
            }

            RunSession session = RunSession.Create(runSeed, parts, modules);
            gachaEngine = session.GachaEngine;
            runManager = session.RunManager;
            moduleOffer = session.ModuleOffer;
            equippedModules.Clear();

            Debug.Log("Run seed: " + runSeed);
            lastPartText.text = "-";
            moduleSelectPanel.SetActive(false);
            Refresh();
        }

        private void OnDrawButtonClicked()
        {
            if (ignoreDrawButtonClick)
            {
                return;
            }

            OnDrawClicked();
        }

        private void OnDrawClicked()
        {
            // 연출 중에도 State 는 Drawing 이다. 막지 않으면 공개 전에 다음 뽑기가 실행된다
            if (isRevealing || runManager.State != RunState.Drawing)
            {
                return;
            }

            // 결과는 여기서 확정된다. 연출은 표시만 늦추므로 스킵해도 최종 상태가 같다
            DrawResult result = runManager.Draw();
            StartCoroutine(RevealDraw(result));
        }

        private IEnumerator RevealDraw(DrawResult result)
        {
            isRevealing = true;
            skipRequested = false;
            revealStartFrame = Time.frameCount;
            lastPartText.text = "???";
            drawButton.interactable = false;

            float duration = revealTimings.GetSeconds(result.Part.Grade);
            float elapsed = 0f;
            while (elapsed < duration && !skipRequested)
            {
                yield return null;
                elapsed += Time.deltaTime;
            }

            isRevealing = false;
            ShowDrawResult(result);
        }

        private void ShowDrawResult(DrawResult result)
        {
            PartData part = result.Part;
            lastPartText.text = part.DisplayName + " [" + part.Grade + "] +" + result.Output;
            Refresh();

            if (runManager.State == RunState.RoundCleared)
            {
                ShowModuleOffer();
            }
        }

        private void ShowModuleOffer()
        {
            // 돌파 1회당 한 번만 부른다. 다시 부르면 공유 난수를 더 소비해 같은 시드의 판이 달라진다
            List<ModuleData> offer = moduleOffer.CreateOffer();
            int nextQuota = RunManager.QuotaFor(runManager.CurrentRound + 1);

            moduleSelectPanel.SetActive(true);
            for (int i = 0; i < moduleCards.Length; i++)
            {
                if (i < offer.Count)
                {
                    ModulePreview preview = runManager.PreviewModule(offer[i]);
                    moduleCards[i].Show(offer[i], ModulePreviewFormatter.Format(preview, parts, nextQuota), OnModuleSelected);
                }
                else
                {
                    moduleCards[i].Hide();
                }
            }
        }

        private void OnModuleSelected(ModuleData module)
        {
            if (runManager.State != RunState.RoundCleared)
            {
                return;
            }

            // 장착이 라운드 진행보다 먼저. 새 라운드의 뽑기 횟수는 시작할 때 장착된 모듈로 정해진다
            runManager.EquipModule(module);
            equippedModules.Add(module);
            runManager.AdvanceRound();

            moduleSelectPanel.SetActive(false);
            lastPartText.text = "-";
            Refresh();
        }

        private void Refresh()
        {
            RunState state = runManager.State;

            roundText.text = "R" + runManager.CurrentRound + " / " + runManager.RoundCount;
            quotaText.text = runManager.Production + " / " + runManager.Quota;
            quotaGauge.maxValue = runManager.Quota;
            quotaGauge.value = Mathf.Min(runManager.Production, runManager.Quota);
            drawsText.text = "남은 뽑기 " + runManager.DrawsRemaining;
            gaugePanelText.text =
                "N  " + FormatPercent(gachaEngine.GetProbability(Grade.N)) + "\n" +
                "R  " + FormatPercent(gachaEngine.GetProbability(Grade.R)) + "\n" +
                "SR " + FormatPercent(gachaEngine.GetProbability(Grade.SR)) + "\n" +
                "보증 " + gachaEngine.DrawsWithoutSuperRare + " / " + gachaEngine.PityThreshold;
            equippedText.text = FormatEquippedModules();

            drawButton.interactable = state == RunState.Drawing;
            restartButton.gameObject.SetActive(state == RunState.RunFailed || state == RunState.RunCleared);

            if (state == RunState.RunFailed)
            {
                resultText.text = "라인 셧다운 — R" + runManager.CurrentRound + " 미달";
            }
            else if (state == RunState.RunCleared)
            {
                resultText.text = "클리어!";
            }
            else if (state == RunState.RoundCleared)
            {
                resultText.text = "할당량 돌파 — 모듈 선택";
            }
            else
            {
                resultText.text = "";
            }
        }

        private string FormatEquippedModules()
        {
            string text = "장착 모듈 " + equippedModules.Count;
            foreach (ModuleData module in equippedModules)
            {
                text += "\n- " + module.DisplayName;
            }

            return text;
        }

        private static string FormatPercent(double probability)
        {
            return (probability * 100.0).ToString("0.0") + "%";
        }
    }
}

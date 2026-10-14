using System;
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
        private const int PityThreshold = 50;
        private const int RoundCount = 8;
        private const int DrawsPerRound = 10;
        private const double BaseN = 0.72;
        private const double BaseR = 0.25;
        private const double BaseSR = 0.03;

        [SerializeField, Min(0)] private int seed;
        [SerializeField] private PartData[] parts;
        [SerializeField] private ModuleData[] modules;

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

        private void Awake()
        {
            drawButton.onClick.AddListener(OnDrawClicked);
            restartButton.onClick.AddListener(StartNewRun);
        }

        private void Start()
        {
            StartNewRun();
        }

        private void Update()
        {
            Keyboard keyboard = Keyboard.current;
            if (keyboard != null && keyboard.spaceKey.wasPressedThisFrame && runManager.State == RunState.Drawing)
            {
                OnDrawClicked();
            }
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

            // 보증 카운터가 런 전체에서 이어져야 하므로 GachaEngine 은 런마다 한 번만 만든다
            // 같은 시드로 같은 판을 재현하려면 모든 난수 소비가 이 RandomService 하나를 거쳐야 한다
            var randomService = new RandomService(runSeed);
            gachaEngine = new GachaEngine(randomService, PityThreshold, BaseN, BaseR, BaseSR);
            var partCatalog = new PartCatalog(parts, randomService);
            runManager = new RunManager(gachaEngine, partCatalog, RoundCount, DrawsPerRound);
            moduleOffer = new ModuleOffer(modules, randomService);
            equippedModules.Clear();

            Debug.Log("Run seed: " + runSeed);
            lastPartText.text = "-";
            moduleSelectPanel.SetActive(false);
            Refresh();
        }

        private void OnDrawClicked()
        {
            DrawResult result = runManager.Draw();
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

            moduleSelectPanel.SetActive(true);
            for (int i = 0; i < moduleCards.Length; i++)
            {
                if (i < offer.Count)
                {
                    ModulePreview preview = runManager.PreviewModule(offer[i]);
                    moduleCards[i].Show(offer[i], ModulePreviewFormatter.Format(preview, parts), OnModuleSelected);
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

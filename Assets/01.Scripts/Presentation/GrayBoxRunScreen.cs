using System;
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

        [SerializeField] private TMP_Text roundText;
        [SerializeField] private TMP_Text quotaText;
        [SerializeField] private Slider quotaGauge;
        [SerializeField] private TMP_Text drawsText;
        [SerializeField] private TMP_Text gaugePanelText;
        [SerializeField] private TMP_Text lastPartText;
        [SerializeField] private TMP_Text resultText;
        [SerializeField] private Button drawButton;
        [SerializeField] private Button nextRoundButton;
        [SerializeField] private Button restartButton;

        private GachaEngine gachaEngine;
        private RunManager runManager;

        private void Awake()
        {
            drawButton.onClick.AddListener(OnDrawClicked);
            nextRoundButton.onClick.AddListener(OnNextRoundClicked);
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
            var randomService = new RandomService(runSeed);
            gachaEngine = new GachaEngine(randomService, PityThreshold, BaseN, BaseR, BaseSR);
            var partCatalog = new PartCatalog(parts, randomService);
            runManager = new RunManager(gachaEngine, partCatalog, RoundCount, DrawsPerRound);

            Debug.Log("Run seed: " + runSeed);
            lastPartText.text = "-";
            Refresh();
        }

        private void OnDrawClicked()
        {
            PartData part = runManager.Draw();
            lastPartText.text = part.DisplayName + " [" + part.Grade + "] +" + part.BaseOutput;
            Refresh();
        }

        private void OnNextRoundClicked()
        {
            runManager.AdvanceRound();
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

            drawButton.interactable = state == RunState.Drawing;
            nextRoundButton.gameObject.SetActive(state == RunState.RoundCleared);
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
                resultText.text = "할당량 돌파";
            }
            else
            {
                resultText.text = "";
            }
        }

        private static string FormatPercent(double probability)
        {
            return (probability * 100.0).ToString("0.0") + "%";
        }
    }
}

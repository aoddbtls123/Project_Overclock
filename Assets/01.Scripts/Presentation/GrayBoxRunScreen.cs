using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
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
        [SerializeField] private EquippedModuleListView equippedModuleList;
        [SerializeField] private Button drawButton;
        [SerializeField] private Button restartButton;
        [SerializeField] private GameObject moduleSelectPanel;
        [SerializeField] private ModuleCardView[] moduleCards;
        [SerializeField] private GameObject modeSelectPanel;
        [SerializeField] private Button normalModeButton;
        [SerializeField] private Button endlessModeButton;
        [SerializeField] private Button continueButton;
        [SerializeField] private TMP_Text continueLabel;
        [SerializeField] private TMP_Text qaSeedText;

        private RunSaveFile saveFile;
        private uint qaSeed;
        private RunSession savedSession;
        private RunSession session;
        private GachaEngine gachaEngine;
        private RunManager runManager;
        private RunMode runMode;
        private bool isRevealing;
        private bool skipRequested;
        private int revealStartFrame;
        private bool ignoreDrawButtonClick;

        private void Awake()
        {
            drawButton.onClick.AddListener(OnDrawButtonClicked);
            restartButton.onClick.AddListener(ShowModeSelect);
            normalModeButton.onClick.AddListener(() => StartNewRun(RunMode.Normal));
            endlessModeButton.onClick.AddListener(() => StartNewRun(RunMode.Endless));
            continueButton.onClick.AddListener(ContinueSavedRun);
            saveFile = new RunSaveFile(Application.persistentDataPath);
            ReadQaSeed();
        }

        private void Start()
        {
            ShowModeSelect();
        }

        private void Update()
        {
            Keyboard keyboard = Keyboard.current;
            Mouse mouse = Mouse.current;
            UpdateDrawButtonGuard(mouse);

            // 모드를 고르기 전에는 런이 없다
            if (runManager == null)
            {
                return;
            }

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

        private void ReadQaSeed()
        {
            // 0 = 실행 인자 없음. 잘못된 값은 무시하고 평소대로 시작한다 (QA 가 로그로 알아챈다)
            qaSeed = 0;
            if (RunSeedOption.TryFind(Environment.GetCommandLineArgs(), out uint found, out string error))
            {
                qaSeed = found;
            }
            else if (error != null)
            {
                Debug.LogWarning("QA 시드 무시: " + error);
            }

            qaSeedText.text = qaSeed != 0 ? "QA 시드 " + qaSeed + " (새 판은 모두 이 시드)" : "";
        }

        private void ShowModeSelect()
        {
            moduleSelectPanel.SetActive(false);
            equippedModuleList.SetInteractable(false);
            RefreshContinueButton();
            modeSelectPanel.SetActive(true);
        }

        private void RefreshContinueButton()
        {
            // 읽을 수 없는 세이브는 지우지 않는다. 이어하기만 끄고, 새 런을 시작할 때 덮어쓴다
            savedSession = null;
            if (!saveFile.Exists)
            {
                continueLabel.text = "이어하기 (저장된 판 없음)";
            }
            else if (saveFile.TryLoad(parts, modules, out RunSession loaded, out string error))
            {
                savedSession = loaded;
                continueLabel.text = "이어하기";
            }
            else
            {
                Debug.LogWarning("세이브를 읽을 수 없습니다: " + error);
                continueLabel.text = "이어하기 (세이브를 읽을 수 없음)";
            }

            continueButton.interactable = savedSession != null;
        }

        private void StartNewRun(RunMode mode)
        {
            // 우선순위: 실행 인자(QA) → Inspector → 자동.
            // 자동 시드는 Inspector 의 int 칸에 다시 넣을 수 있는 1 ~ int.MaxValue 로 제한한다
            uint runSeed = qaSeed != 0 ? qaSeed : (uint)seed;
            if (runSeed == 0)
            {
                runSeed = unchecked((uint)Environment.TickCount) & 0x7FFFFFFFu;
                if (runSeed == 0)
                {
                    runSeed = 1;
                }
            }

            BeginRun(RunSession.Create(runSeed, parts, modules, mode), qaSeed != 0 ? " (QA 시드)" : "");
            SaveRun();
        }

        private void ContinueSavedRun()
        {
            if (savedSession == null)
            {
                return;
            }

            RunSession restored = savedSession;
            savedSession = null;
            BeginRun(restored, " (이어하기)");

            // 돌파 직후에 꺼진 판. 저장된 난수 상태에서 카드를 열므로 끄기 전과 같은 카드가 나온다
            if (runManager.State == RunState.RoundCleared)
            {
                ShowModuleOffer();
            }
        }

        private void BeginRun(RunSession newSession, string logSuffix)
        {
            session = newSession;
            runMode = session.Mode;
            gachaEngine = session.GachaEngine;
            runManager = session.RunManager;

            Debug.Log("Run seed: " + session.Seed + " mode: " + session.Mode + logSuffix);
            lastPartText.text = "-";
            modeSelectPanel.SetActive(false);
            moduleSelectPanel.SetActive(false);
            equippedModuleList.Show(runManager.EquippedModules);
            equippedModuleList.SetInteractable(true);
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
            if (runManager == null || isRevealing || runManager.State != RunState.Drawing)
            {
                return;
            }

            // 결과는 여기서 확정된다. 연출은 표시만 늦추므로 스킵해도 최종 상태가 같다
            DrawResult result = runManager.Draw();

            // 연출 전에 저장한다. 연출 중에 꺼도 이 뽑기는 되돌릴 수 없다
            if (runManager.State == RunState.RunFailed || runManager.State == RunState.RunCleared)
            {
                DeleteSave();
            }
            else
            {
                SaveRun();
            }

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
            // 여기서는 저장하지 않는다. 돌파 직후(카드를 열기 전) 저장된 난수 상태에서 다시 열면 같은 카드가 나온다
            IReadOnlyList<ModuleData> offer = session.OpenOffer();
            int nextQuota = RunManager.QuotaFor(runManager.CurrentRound + 1);

            moduleSelectPanel.SetActive(true);
            // 장착 목록이 카드 뒤에 깔리므로 선택 중에는 팝업을 띄우지 않는다
            equippedModuleList.SetInteractable(false);
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

            session.SelectModule(module);
            SaveRun();

            moduleSelectPanel.SetActive(false);
            equippedModuleList.Show(runManager.EquippedModules);
            equippedModuleList.SetInteractable(true);
            lastPartText.text = "-";
            Refresh();
        }

        private void Refresh()
        {
            RunState state = runManager.State;

            roundText.text = runMode == RunMode.Endless
                ? "R" + runManager.CurrentRound + " (무한)"
                : "R" + runManager.CurrentRound + " / " + runManager.RoundCount;
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

        private void SaveRun()
        {
            try
            {
                saveFile.Save(session);
            }
            catch (IOException exception)
            {
                Debug.LogWarning("세이브를 쓰지 못했습니다: " + exception.Message);
            }
            catch (UnauthorizedAccessException exception)
            {
                Debug.LogWarning("세이브를 쓰지 못했습니다: " + exception.Message);
            }
        }

        private void DeleteSave()
        {
            try
            {
                saveFile.Delete();
            }
            catch (IOException exception)
            {
                Debug.LogWarning("세이브를 지우지 못했습니다: " + exception.Message);
            }
            catch (UnauthorizedAccessException exception)
            {
                Debug.LogWarning("세이브를 지우지 못했습니다: " + exception.Message);
            }
        }

        private static string FormatPercent(double probability)
        {
            return (probability * 100.0).ToString("0.0") + "%";
        }
    }
}

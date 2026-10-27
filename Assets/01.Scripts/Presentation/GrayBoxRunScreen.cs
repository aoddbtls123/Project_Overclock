using System;
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

        [SerializeField] private TMP_Text roundText;
        [SerializeField] private TMP_Text quotaText;
        [SerializeField] private Slider quotaGauge;
        [SerializeField] private TMP_Text drawsText;
        [SerializeField] private TMP_Text gaugePanelText;
        [SerializeField] private TMP_Text resultText;
        [SerializeField] private MachinePanelView machinePanel;
        [SerializeField] private Button drawButton;
        [SerializeField] private DrawRevealView drawReveal;
        [SerializeField] private GameObject moduleSelectPanel;
        [SerializeField] private ModuleCardView[] moduleCards;
        [SerializeField] private GameObject titlePanel;
        [SerializeField] private Button startButton;
        [SerializeField] private Button titleQuitButton;
        [SerializeField] private GameObject modeSelectPanel;
        [SerializeField] private Button modeBackButton;
        [SerializeField] private TMP_Text normalBestText;
        [SerializeField] private TMP_Text endlessBestText;
        [SerializeField] private Button normalModeButton;
        [SerializeField] private Button endlessModeButton;
        [SerializeField] private Button continueButton;
        [SerializeField] private TMP_Text continueLabel;
        [SerializeField] private TMP_Text qaSeedText;
        [SerializeField] private PauseMenuView pauseMenu;
        [SerializeField] private ResultPanelView resultPanel;
        [SerializeField] private GameObject settingsPanel;
        [SerializeField] private Button titleSettingsButton;
        [SerializeField] private Button settingsBackButton;
        [SerializeField] private SettingsMenuView titleSettingsMenu;
        [SerializeField] private SettingsMenuView pauseSettingsMenu;

        private RunSaveFile saveFile;
        private BestRecordFile recordFile;
        private GameSettingsFile settingsFile;
        private GameSettings settings;
        private BestRecords bestRecords;
        private bool lastRunNewRecord;
        private uint qaSeed;
        private RunSession savedSession;
        private RunSession session;
        private GachaEngine gachaEngine;
        private RunManager runManager;
        private RunMode runMode;
        private int revealStartFrame;
        private bool ignoreDrawButtonClick;
        private int resumeFrame;

        private void Awake()
        {
            drawButton.onClick.AddListener(OnDrawButtonClicked);
            startButton.onClick.AddListener(ShowModeSelect);
            titleQuitButton.onClick.AddListener(QuitGame);
            modeBackButton.onClick.AddListener(ShowTitle);
            titleSettingsButton.onClick.AddListener(ShowSettings);
            settingsBackButton.onClick.AddListener(ShowTitle);
            titleSettingsMenu.Bind(ToggleShake, ToggleFlash, CycleRevealSpeed);
            pauseSettingsMenu.Bind(ToggleShake, ToggleFlash, CycleRevealSpeed);
            normalModeButton.onClick.AddListener(() => StartNewRun(RunMode.Normal));
            endlessModeButton.onClick.AddListener(() => StartNewRun(RunMode.Endless));
            continueButton.onClick.AddListener(ContinueSavedRun);
            pauseMenu.Bind(ResumeGame, ReturnToTitle, QuitGame);
            pauseMenu.Close();
            resultPanel.Bind(RetrySameMode, ReturnToTitle);
            resultPanel.Hide();
            saveFile = new RunSaveFile(Application.persistentDataPath);
            recordFile = new BestRecordFile(Application.persistentDataPath);
            settingsFile = new GameSettingsFile(Application.persistentDataPath);
            LoadBestRecords();
            LoadSettings();
            ReadQaSeed();
        }

        private void Start()
        {
            // 설정 적용은 Start 에서 한다. 연출 효과(RevealEffectsView)의 Awake 가 끝난 뒤여야 한다
            ApplySettings();
            ShowTitle();
        }

        private void OnDestroy()
        {
            // 일시정지 중에 씬이 내려가도 시간이 멈춘 채 남지 않게 한다
            Time.timeScale = 1f;
        }

        private void Update()
        {
            Keyboard keyboard = Keyboard.current;
            Mouse mouse = Mouse.current;
            UpdateDrawButtonGuard(mouse);

            // ESC 는 일시정지 전용이다 (10/21 결정). 일시정지 중에는 아래 입력을 모두 막는다
            if (keyboard != null && keyboard.escapeKey.wasPressedThisFrame && CanPause())
            {
                if (pauseMenu.IsOpen)
                {
                    ResumeGame();
                }
                else
                {
                    PauseGame();
                }

                return;
            }

            if (!IsRunInputAllowed())
            {
                return;
            }

            if (drawReveal.IsPlaying)
            {
                if (IsSkipPressed(keyboard, mouse))
                {
                    drawReveal.Skip();
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
                ignoreDrawButtonClick = drawReveal.IsPlaying;
            }
        }

        private bool IsSkipPressed(Keyboard keyboard, Mouse mouse)
        {
            // 연출을 시작한 입력(버튼을 뗀 순간·Space)이 같은 프레임에 스킵으로 잡히지 않게 한다.
            // 일시정지 창의 '계속하기'를 뗀 클릭도 같은 이유로 스킵에 쓰지 않는다
            if (Time.frameCount <= revealStartFrame || Time.frameCount <= resumeFrame)
            {
                return false;
            }

            // 스킵은 Space 와 클릭(뗄 때)뿐이다. ESC 는 일시정지에 쓴다 (10/21 결정)
            if (keyboard != null && keyboard.spaceKey.wasPressedThisFrame)
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

        private bool IsRunInputAllowed()
        {
            // 런 입력(뽑기 · 스킵 · 카드 선택)을 받는 조건은 여기 한 곳에서 정한다.
            // 모드 선택 화면에는 런이 없고(runManager = null), 일시정지 중에는 창이 열려 있다
            return runManager != null && !pauseMenu.IsOpen;
        }

        private bool CanPause()
        {
            // 런 화면에서만 연다 (결과 창이 떠 있어도 런 화면이다). 타이틀로 갈 때는 런을 버리므로(runManager = null) 타이틀 · 모드 선택에서는 열리지 않는다
            return runManager != null;
        }

        private void PauseGame()
        {
            // 연출은 Time.deltaTime 으로 흐르므로 timeScale 0 이면 그 자리에서 멈춘다.
            // 뽑기 결과는 연출 전에 이미 확정되고 저장됐으므로 멈춰도 결과는 바뀌지 않는다
            Time.timeScale = 0f;
            machinePanel.SetInteractable(false);
            pauseMenu.Open();
        }

        private void ResumeGame()
        {
            pauseMenu.Close();
            machinePanel.SetInteractable(true);
            Time.timeScale = 1f;
            resumeFrame = Time.frameCount;
        }

        private void ReturnToTitle()
        {
            // 런을 떠나는 길은 여기 한 곳이다 (일시정지 '처음으로' · 결과 창 '처음으로').
            // 진행은 뽑기와 모듈 선택마다 저장돼 있다. 메모리의 런만 버리고, 이어하기로 돌아올 수 있게 한다
            pauseMenu.Close();
            Time.timeScale = 1f;
            StopReveal();
            session = null;
            gachaEngine = null;
            runManager = null;
            ShowTitle();
        }

        private void RetrySameMode()
        {
            if (!IsRunInputAllowed())
            {
                return;
            }

            StartNewRun(runMode);
        }

        private void QuitGame()
        {
            // 저장은 이미 끝나 있으므로 바로 끈다
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }

        private void StopReveal()
        {
            // 연출 중에 런을 버리면, 남은 연출이 끝난 뒤 버린 런의 결과와 카드를 띄우지 않게 멈춘다 (Cancel 은 콜백을 부르지 않는다)
            drawReveal.Cancel();
        }

        private void ShowTitle()
        {
            // 화면은 패널을 켜고 끄기만 한다. RunScreen 오브젝트는 끄지 않는다 (10/26 화면 전환 규칙)
            moduleSelectPanel.SetActive(false);
            modeSelectPanel.SetActive(false);
            settingsPanel.SetActive(false);
            resultPanel.Hide();
            machinePanel.SetInteractable(false);
            titlePanel.SetActive(true);
        }

        private void ShowSettings()
        {
            titlePanel.SetActive(false);
            settingsPanel.SetActive(true);
        }

        private void ToggleShake()
        {
            settings.ShakeEnabled = !settings.ShakeEnabled;
            OnSettingsChanged();
        }

        private void ToggleFlash()
        {
            settings.FlashEnabled = !settings.FlashEnabled;
            OnSettingsChanged();
        }

        private void CycleRevealSpeed()
        {
            settings.CycleRevealSpeed();
            OnSettingsChanged();
        }

        private void OnSettingsChanged()
        {
            // 바꿀 때마다 바로 저장한다. 일시정지 창 · 타이틀 설정 창 두 곳의 글자를 같이 맞춘다
            SaveSettings();
            ApplySettings();
        }

        private void ApplySettings()
        {
            drawReveal.ApplySettings(settings);
            titleSettingsMenu.Show(settings);
            pauseSettingsMenu.Show(settings);
        }

        private void LoadSettings()
        {
            // 읽을 수 없는 설정은 기본값으로 시작한다. 파일은 다음에 설정을 바꿀 때 덮어쓴다
            if (!settingsFile.TryLoad(out settings, out string error))
            {
                Debug.LogWarning("설정을 읽을 수 없습니다: " + error);
            }
        }

        private void ShowModeSelect()
        {
            titlePanel.SetActive(false);
            RefreshContinueButton();
            normalBestText.text = RecordText.Best(bestRecords, RunMode.Normal);
            endlessBestText.text = RecordText.Best(bestRecords, RunMode.Endless);
            modeSelectPanel.SetActive(true);
        }

        private void LoadBestRecords()
        {
            // 읽을 수 없는 기록은 빈 기록으로 시작한다. 다음 런이 끝날 때 새 기록으로 덮어쓴다
            if (!recordFile.TryLoad(out bestRecords, out string error))
            {
                Debug.LogWarning("최고 기록을 읽을 수 없습니다: " + error);
            }
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
            // 새 런은 언제나 연출이 없는 상태에서 시작한다. 이전 런의 연출이 새 런 화면에 결과를 띄우지 않게 한다
            StopReveal();
            session = newSession;
            runMode = session.Mode;
            gachaEngine = session.GachaEngine;
            runManager = session.RunManager;

            Debug.Log("Run seed: " + session.Seed + " mode: " + session.Mode + logSuffix);
            drawReveal.ShowIdle();
            titlePanel.SetActive(false);
            modeSelectPanel.SetActive(false);
            settingsPanel.SetActive(false);
            moduleSelectPanel.SetActive(false);
            resultPanel.Hide();
            machinePanel.Show(runManager.EquippedModules);
            machinePanel.SetInteractable(true);
            Refresh();
        }

        private void OnDrawButtonClicked()
        {
            // 일시정지 창이 클릭을 막지만, 씬 배치가 바뀌어도 일시정지 중 뽑기가 되지 않게 코드에서도 막는다
            if (ignoreDrawButtonClick || !IsRunInputAllowed())
            {
                return;
            }

            OnDrawClicked();
        }

        private void OnDrawClicked()
        {
            // 연출 중에도 State 는 Drawing 이다. 막지 않으면 공개 전에 다음 뽑기가 실행된다
            if (runManager == null || drawReveal.IsPlaying || runManager.State != RunState.Drawing)
            {
                return;
            }

            // 결과는 여기서 확정된다. 연출은 표시만 늦추므로 스킵해도 최종 상태가 같다
            DrawResult result = runManager.Draw();

            // 연출 전에 저장한다. 연출 중에 꺼도 이 뽑기는 되돌릴 수 없다 (최고 기록도 같다)
            if (runManager.State == RunState.RunFailed || runManager.State == RunState.RunCleared)
            {
                DeleteSave();
                lastRunNewRecord = bestRecords.Submit(session);
                SaveBestRecords();
            }
            else
            {
                SaveRun();
            }

            revealStartFrame = Time.frameCount;
            drawButton.interactable = false;
            drawReveal.Play(result, OnRevealFinished);
        }

        private void OnRevealFinished()
        {
            // 부품 표시는 DrawRevealView 가 끝냈다. 화면은 나머지 숫자와 다음 단계(카드 · 결과 창)만 연다
            Refresh();

            if (runManager.State == RunState.RoundCleared)
            {
                ShowModuleOffer();
            }
            else if (runManager.State == RunState.RunFailed || runManager.State == RunState.RunCleared)
            {
                resultPanel.Show(RecordText.ResultTitle(runManager.State), RecordText.ResultDetail(session, bestRecords, lastRunNewRecord));
            }
        }

        private void ShowModuleOffer()
        {
            // 여기서는 저장하지 않는다. 돌파 직후(카드를 열기 전) 저장된 난수 상태에서 다시 열면 같은 카드가 나온다
            IReadOnlyList<ModuleData> offer = session.OpenOffer();
            int nextQuota = RunManager.QuotaFor(runManager.CurrentRound + 1);

            // 기계 패널은 카드 영역 밖에서 오버레이 위에 그려지므로 고르는 동안에도 팝업을 쓸 수 있다
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
            if (!IsRunInputAllowed() || runManager.State != RunState.RoundCleared)
            {
                return;
            }

            session.SelectModule(module);
            SaveRun();

            moduleSelectPanel.SetActive(false);
            machinePanel.Show(runManager.EquippedModules);
            machinePanel.SetInteractable(true);
            drawReveal.ShowIdle();
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
                GradeStyle.Colorize(Grade.N, "N  " + FormatPercent(gachaEngine.GetProbability(Grade.N))) + "\n" +
                GradeStyle.Colorize(Grade.R, "R  " + FormatPercent(gachaEngine.GetProbability(Grade.R))) + "\n" +
                GradeStyle.Colorize(Grade.SR, "SR " + FormatPercent(gachaEngine.GetProbability(Grade.SR))) + "\n" +
                "천장 " + gachaEngine.DrawsWithoutSuperRare + " / " + gachaEngine.PityThreshold;

            drawButton.interactable = state == RunState.Drawing;

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

        private void SaveBestRecords()
        {
            try
            {
                recordFile.Save(bestRecords);
            }
            catch (IOException exception)
            {
                Debug.LogWarning("최고 기록을 쓰지 못했습니다: " + exception.Message);
            }
            catch (UnauthorizedAccessException exception)
            {
                Debug.LogWarning("최고 기록을 쓰지 못했습니다: " + exception.Message);
            }
        }

        private void SaveSettings()
        {
            try
            {
                settingsFile.Save(settings);
            }
            catch (IOException exception)
            {
                Debug.LogWarning("설정을 쓰지 못했습니다: " + exception.Message);
            }
            catch (UnauthorizedAccessException exception)
            {
                Debug.LogWarning("설정을 쓰지 못했습니다: " + exception.Message);
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

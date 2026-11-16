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
        [SerializeField] private Button overclockButton;
        [SerializeField] private TMP_Text overclockLabel;
        [SerializeField] private DrawRevealView drawReveal;
        [SerializeField] private GameObject moduleSelectPanel;
        [SerializeField] private ModuleCardView[] moduleCards;
        [Tooltip("카드 3장 다시 받기 (돌파마다 1번 · 11/06). 비어 있으면 다시 받기 없이 진행함")]
        [SerializeField] private Button rerollButton;
        [Tooltip("카드 효과 간단히 / 자세히 전환 (11/16). 비어 있으면 카드는 늘 자세히")]
        [SerializeField] private Button cardDetailToggle;
        [SerializeField] private TMP_Text cardDetailLabel;
        [SerializeField] private TMP_Text rerollLabel;
        [Tooltip("다시 받기 글자색 — 쓸 수 있을 때 / 쓴 뒤 (버튼은 꺼진 색으로 어두워지고, 글자는 읽히게 조금만 낮춤)")]
        [SerializeField] private Color rerollReadyLabelColor = new Color(0.9098039f, 0.91764706f, 0.92941177f, 1f);
        [SerializeField] private Color rerollUsedLabelColor = new Color(0.6627451f, 0.6901961f, 0.7294118f, 1f);
        [Tooltip("다시 받기 카드 회전 (11/07). 비어 있으면 돌지 않고 바로 새 카드를 보여 줌")]
        [SerializeField] private OfferFlipView offerFlip;
        [Tooltip("새 런 시작 등장 연출 (11/07 · 정보 패널이 펼쳐짐). 비어 있으면 바로 최종 화면")]
        [SerializeField] private RunIntroView runIntro;
        [Tooltip("배경 방 조명 (11/16). 비어 있으면 배경 연출 없음")]
        [SerializeField] private RoomLightView roomLight;
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
        [SerializeField] private SoundPlayer soundPlayer;
        [Tooltip("비어 있으면 레버 · 돌파 증기 없이 진행함")]
        [SerializeField] private GachaMachineView gachaMachine;
        [Tooltip("비어 있으면 수확물 더미 없이 진행함")]
        [SerializeField] private HarvestTrayView harvestTray;
        [Tooltip("비어 있으면 생산량 숫자를 세어 올리지 않고 바로 바꿈 (quotaText · quotaGauge 를 직접 씀)")]
        [SerializeField] private ProductionCounterView productionCounter;
        [Tooltip("비어 있으면 확률을 세어 바꾸지 않고 바로 바꿈 (gaugePanelText 를 직접 씀)")]
        [SerializeField] private GaugePanelView gaugePanel;
        [Tooltip("일반모드 클리어 마무리 연출 (11/06). 비어 있으면 바로 결과 창")]
        [SerializeField] private RunClearView runClear;
        [Tooltip("런 실패 연출 (11/07 · 일반 · 무한 공통). 비어 있으면 예전처럼 실패 소리 예약 + 바로 결과 창")]
        [SerializeField] private RunFailView runFail;

        private RunSaveFile saveFile;
        private BestRecordFile recordFile;
        private GameSettingsFile settingsFile;
        private GameSettings settings;
        private bool volumeDirty;
        private BestRecords bestRecords;
        private bool lastRunNewRecord;
        private uint qaSeed;
        private RunSession savedSession;
        private RunSession session;
        private GachaEngine gachaEngine;
        private RunManager runManager;
        private RunMode runMode;
        private int revealStartFrame;
        private DrawResult lastDrawResult;
        private bool ignoreDrawButtonClick;
        private bool ignoreCardClick;
        private int offerFlipEndFrame = -1;
        private int introEndFrame = -1;
        private int resultShownFrame = -1;
        private int resumeFrame;
        private readonly PlaytestLog playtestLog = new PlaytestLog();
        private bool showCardDetails;

        private void Awake()
        {
            // 뽑기 버튼 · 카드 선택은 자기 소리를 냄 (뽑기가 받아들여졌을 때만 · 장착할 때). 창을 여는 버튼은 열기 소리만 냄 (10/29 결정)
            drawButton.onClick.AddListener(OnDrawButtonClicked);

            // 레버 소리 · 래칫 · 착지 소리는 멈추는 순간에 남 (11/04 타격감). 기계가 없으면 레버 소리는 클릭 때
            if (gachaMachine != null)
            {
                gachaMachine.LeverBottomed += soundPlayer.PlayDrawLever;
                gachaMachine.RatchetStopped += soundPlayer.PlayRatchetStop;
            }

            if (harvestTray != null)
            {
                harvestTray.PieceLanded += OnPieceLanded;
            }

            overclockButton.onClick.AddListener(OnOverclockClicked);
            if (rerollButton != null)
            {
                // 새 카드가 나오면 카드 출현 소리를 냄 (받아들여졌을 때만). 클릭 소리는 따로 내지 않음
                rerollButton.onClick.AddListener(OnRerollClicked);
            }

            if (cardDetailToggle != null)
            {
                cardDetailToggle.onClick.AddListener(WithClickSound(ToggleCardDetails).Invoke);
            }

            ApplyCardDetails();

            startButton.onClick.AddListener(WithOpenSound(ShowModeSelect).Invoke);
            titleSettingsButton.onClick.AddListener(WithOpenSound(ShowSettings).Invoke);
            titleQuitButton.onClick.AddListener(WithClickSound(QuitGame).Invoke);
            modeBackButton.onClick.AddListener(WithClickSound(ShowTitle).Invoke);
            settingsBackButton.onClick.AddListener(WithClickSound(ShowTitle).Invoke);
            titleSettingsMenu.Bind(WithClickSound(ToggleShake), WithClickSound(ToggleFlash), WithClickSound(CycleRevealSpeed), OnVolumeChanged, OnVolumeCommitted);
            pauseSettingsMenu.Bind(WithClickSound(ToggleShake), WithClickSound(ToggleFlash), WithClickSound(CycleRevealSpeed), OnVolumeChanged, OnVolumeCommitted);
            normalModeButton.onClick.AddListener(WithClickSound(() => StartNewRun(RunMode.Normal)).Invoke);
            endlessModeButton.onClick.AddListener(WithClickSound(() => StartNewRun(RunMode.Endless)).Invoke);
            continueButton.onClick.AddListener(WithClickSound(ContinueSavedRun).Invoke);
            pauseMenu.Bind(WithClickSound(ResumeGame), WithClickSound(ReturnToTitle), WithClickSound(QuitGame));
            pauseMenu.Close();
            resultPanel.Bind(IgnoreOnResultFrame(WithClickSound(RetrySameMode)), IgnoreOnResultFrame(WithClickSound(ReturnToTitle)));
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
            // 설정 적용은 Start 에서 함. 연출 효과(RevealEffectsView) · 소리(SoundPlayer, 소스를 Awake 에서 만듦)의 Awake 가 끝난 뒤여야 함
            ApplySettings();
            ShowTitle();
        }

        private void OnApplicationQuit()
        {
            // 창의 X · Alt+F4 로 끄면 '놓기' · QuitGame 을 거치지 않음. 키보드로 바꾼 볼륨이 남지 않게 여기서도 저장함 (교차검토 #36)
            SaveVolumeIfDirty();
            if (runManager != null)
            {
                playtestLog.RunLeft(runManager, Time.time);
            }
        }

        private void OnDestroy()
        {
            // 일시정지 중에 씬이 내려가도 시간이 멈춘 채 남지 않게 함
            Time.timeScale = 1f;

            if (gachaMachine != null)
            {
                gachaMachine.LeverBottomed -= soundPlayer.PlayDrawLever;
                gachaMachine.RatchetStopped -= soundPlayer.PlayRatchetStop;
            }

            if (harvestTray != null)
            {
                harvestTray.PieceLanded -= OnPieceLanded;
            }
        }

        private void OnPieceLanded(bool firstOfDraw)
        {
            // 조각이 앉을 때마다 숫자가 하나씩 오름 (마지막 조각에서 최종 값). 이번 뽑기의 첫 조각만 덜컹 · 착지음
            if (productionCounter != null)
            {
                productionCounter.Advance(1);
            }

            if (firstOfDraw)
            {
                soundPlayer.PlayTrayLand();
                if (gachaMachine != null)
                {
                    gachaMachine.OnPartLanded();
                }
            }
            else
            {
                soundPlayer.PlayPieceLand();
            }
        }

        private void Update()
        {
            Keyboard keyboard = Keyboard.current;
            Mouse mouse = Mouse.current;
            UpdateDrawButtonGuard(mouse);

            // ESC 는 일시정지 전용임 (10/21 결정). 일시정지 중에는 아래 입력을 모두 막음
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
                    playtestLog.Skipped(Time.time);
                }
            }
            else if (runClear != null && runClear.IsPlaying)
            {
                // 클리어 마무리 연출도 같은 스킵 입력으로 넘김 (결과 창으로)
                if (IsSkipPressed(keyboard, mouse))
                {
                    runClear.Skip();
                }
            }
            else if (runFail != null && runFail.IsPlaying)
            {
                // 실패 연출도 같은 스킵 입력으로 넘김 (멎은 모습 + 결과 창으로)
                if (IsSkipPressed(keyboard, mouse))
                {
                    runFail.Skip();
                }
            }
            else if (IsIntroPlaying)
            {
                // 런 시작 등장도 같은 스킵 입력으로 넘김 (모든 패널을 최종 모습으로). 이 입력으로 뽑기까지 하지 않음
                if (IsSkipPressed(keyboard, mouse))
                {
                    runIntro.Skip();
                    if (roomLight != null)
                    {
                        roomLight.ShowLit();
                    }
                }
            }
            else if (IsOfferFlipping)
            {
                // 다시 받기 회전도 같은 스킵 입력으로 넘김 (이미 확정된 새 카드를 바로 정면으로)
                if (IsSkipPressed(keyboard, mouse))
                {
                    offerFlip.Skip();
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

            // 버튼은 누른 대상을 기억했다가 뗄 때 그 순간 켜져 있으면 클릭으로 처리함.
            // 연출 중에 시작된 누름은 공개 뒤에 떼도 뽑기가 되지 않게 함. 다음 누름이 시작되면 다시 판단함
            if (mouse.leftButton.wasPressedThisFrame)
            {
                ignoreDrawButtonClick = drawReveal.IsPlaying || IsIntroPlaying;

                // 카드 회전 중에 시작된 누름도 같음. 회전이 끝난 뒤에 떼도 새 카드를 고르지 않음
                ignoreCardClick = IsOfferFlipping;
            }
        }

        private bool IsSkipPressed(Keyboard keyboard, Mouse mouse)
        {
            // 연출을 시작한 입력(버튼을 뗀 순간·Space)이 같은 프레임에 스킵으로 잡히지 않게 함.
            // 일시정지 창의 '계속하기'를 뗀 클릭도 같은 이유로 스킵에 쓰지 않음
            if (Time.frameCount <= revealStartFrame || Time.frameCount <= resumeFrame)
            {
                return false;
            }

            // 스킵은 Space 와 클릭(뗄 때)뿐임. ESC 는 일시정지에 씀 (10/21 결정)
            if (keyboard != null && keyboard.spaceKey.wasPressedThisFrame)
            {
                return true;
            }

            return mouse != null && mouse.leftButton.wasReleasedThisFrame;
        }

        private void ReadQaSeed()
        {
            // 0 = 실행 인자 없음. 잘못된 값은 무시하고 평소대로 시작함 (QA 가 로그로 알아챔)
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
            // 런 입력(뽑기 · 스킵 · 카드 선택)을 받는 조건은 여기 한 곳에서 정함.
            // 모드 선택 화면에는 런이 없고(runManager = null), 일시정지 중에는 창이 열려 있음
            return runManager != null && !pauseMenu.IsOpen;
        }

        private bool IsOfferFlipping => offerFlip != null && offerFlip.IsPlaying;

        private bool IsIntroPlaying => runIntro != null && runIntro.IsPlaying;

        private bool CanPause()
        {
            // 런 화면에서만 엶 (결과 창이 떠 있어도 런 화면임). 타이틀로 갈 때는 런을 버리므로(runManager = null) 타이틀 · 모드 선택에서는 열리지 않음
            return runManager != null;
        }

        private void PauseGame()
        {
            // 연출은 Time.deltaTime 으로 흐르므로 timeScale 0 이면 그 자리에서 멈춤.
            // 뽑기 결과는 연출 전에 이미 확정되고 저장됐으므로 멈춰도 결과는 바뀌지 않음
            Time.timeScale = 0f;
            soundPlayer.SetPaused(true);
            soundPlayer.PlayUiOpen();
            machinePanel.SetInteractable(false);
            pauseMenu.Open();
        }

        private void ResumeGame()
        {
            // 슬라이더를 키보드로 바꿨거나 놓는 순간을 놓쳤어도 창을 닫을 때 저장함
            SaveVolumeIfDirty();
            pauseMenu.Close();

            // 등장 연출 중에 멈췄다 이으면 기계 패널은 등장이 끝날 때 엶
            machinePanel.SetInteractable(!IsIntroPlaying);
            Time.timeScale = 1f;
            soundPlayer.SetPaused(false);
            resumeFrame = Time.frameCount;
        }

        private void ReturnToTitle()
        {
            // 런을 떠나는 길은 여기 한 곳임 (일시정지 '처음으로' · 결과 창 '처음으로').
            // 진행은 뽑기와 모듈 선택마다 저장돼 있음. 메모리의 런만 버리고, 이어하기로 돌아올 수 있게 함
            pauseMenu.Close();
            Time.timeScale = 1f;
            soundPlayer.SetPaused(false);
            StopReveal();
            if (runManager != null)
            {
                playtestLog.RunLeft(runManager, Time.time);
            }

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
            // 런 진행은 이미 저장돼 있음. 볼륨만 아직이면 저장하고 끔
            SaveVolumeIfDirty();
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }

        private void StopReveal()
        {
            // 연출 중에 런을 버리면, 남은 연출이 끝난 뒤 버린 런의 결과와 카드를 띄우지 않게 멈춤 (Cancel 은 콜백을 부르지 않음).
            // 버린 런의 소리(SR 충전 · 3연속 · 돌파 · 런 실패 예약)도 같이 멈춤
            drawReveal.Cancel();
            if (runClear != null)
            {
                runClear.Cancel();
            }

            // 등장 중이던 패널은 최종 자리 · 다 열린 모습으로 (다음 시작에 가린 채 남지 않게) · 등장 소리만 멈춤
            if (runIntro != null)
            {
                runIntro.Cancel();
            }

            // 돌던 카드도 콜백 없이 멈추고 정면 자세로 되돌림 (다음 카드 표시 때 각도 · 앞뒷면이 남지 않게)
            if (offerFlip != null)
            {
                offerFlip.Cancel();
            }

            // 실패 연출 · 결과 창 뒤에 남은 멎은 모습 (어두운 색 · 멈춘 기어 · 내려앉은 몸통) 도 되돌림
            if (runFail != null)
            {
                runFail.Cancel();
            }

            soundPlayer.StopRunSounds();
            if (gachaMachine != null)
            {
                gachaMachine.ResetAll();
            }

            if (harvestTray != null)
            {
                harvestTray.Clear();
            }

            if (productionCounter != null)
            {
                productionCounter.CompleteNow();
            }
        }

        private void ShowTitle()
        {
            // 설정 창 '뒤로' · 일시정지 '처음으로' 모두 여기를 지남
            SaveVolumeIfDirty();
            if (roomLight != null)
            {
                roomLight.ShowDark();
            }

            // 화면은 패널을 켜고 끄기만 함. RunScreen 오브젝트는 끄지 않음 (10/26 화면 전환 규칙)
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

        private Action WithClickSound(Action action)
        {
            return () =>
            {
                soundPlayer.PlayUiClick();
                action();
            };
        }

        private Action WithOpenSound(Action action)
        {
            return () =>
            {
                soundPlayer.PlayUiOpen();
                action();
            };
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
            playtestLog.SpeedChanged(settings.RevealSpeed);
        }

        private void OnVolumeChanged(SoundChannel channel, float value)
        {
            // 끄는 동안은 소리 · 두 창 글자만 바로 맞추고 저장하지 않음 (파일을 매 프레임 쓰지 않게). 놓을 때 · 창을 닫을 때 저장
            settings.SetVolume(channel, value);
            volumeDirty = true;
            soundPlayer.ApplyVolumes(settings);
            titleSettingsMenu.Show(settings);
            pauseSettingsMenu.Show(settings);
        }

        private void OnVolumeCommitted(SoundChannel channel)
        {
            SaveVolumeIfDirty();
            soundPlayer.PlayPreview(channel);
        }

        private void SaveVolumeIfDirty()
        {
            // 표시는 저장이 성공했을 때만 지움 (SaveSettings) — 실패하면 창을 닫을 때 다시 시도함 (교차검토 #36)
            if (volumeDirty)
            {
                SaveSettings();
            }
        }

        private void OnSettingsChanged()
        {
            // 바꿀 때마다 바로 저장함. 일시정지 창 · 타이틀 설정 창 두 곳의 글자를 같이 맞춤
            SaveSettings();
            ApplySettings();
        }

        private void ApplySettings()
        {
            drawReveal.ApplySettings(settings);
            if (runFail != null)
            {
                runFail.SetMotionOptions(settings.ShakeEnabled, settings.FlashEnabled);
            }

            // 흔들림 끄기 = 런 시작 패널은 들어오기만 하고 지나침 · 되돌아옴 없음
            if (runIntro != null)
            {
                runIntro.SetMotionOptions(settings.ShakeEnabled);
            }

            soundPlayer.ApplyVolumes(settings);
            titleSettingsMenu.Show(settings);
            pauseSettingsMenu.Show(settings);
        }

        private void LoadSettings()
        {
            // 읽을 수 없는 설정은 기본값으로 시작함. 파일은 다음에 설정을 바꿀 때 덮어씀
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
            // 읽을 수 없는 기록은 빈 기록으로 시작함. 다음 런이 끝날 때 새 기록으로 덮어씀
            if (!recordFile.TryLoad(out bestRecords, out string error))
            {
                Debug.LogWarning("최고 기록을 읽을 수 없습니다: " + error);
            }
        }

        private void RefreshContinueButton()
        {
            // 읽을 수 없는 세이브는 지우지 않음. 이어하기만 끄고, 새 런을 시작할 때 덮어씀
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
            // 자동 시드는 Inspector 의 int 칸에 다시 넣을 수 있는 1 ~ int.MaxValue 로 제한함
            uint runSeed = qaSeed != 0 ? qaSeed : (uint)seed;
            if (runSeed == 0)
            {
                runSeed = unchecked((uint)Environment.TickCount) & 0x7FFFFFFFu;
                if (runSeed == 0)
                {
                    runSeed = 1;
                }
            }

            BeginRun(RunSession.Create(runSeed, parts, modules, mode), qaSeed != 0 ? " (QA 시드)" : "", false);
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
            BeginRun(restored, " (이어하기)", true);

            // 돌파 직후에 꺼진 판. 저장된 난수 상태에서 카드를 열므로 끄기 전과 같은 카드가 나옴
            if (runManager.State == RunState.RoundCleared)
            {
                ShowModuleOffer();
            }
        }

        private void BeginRun(RunSession newSession, string logSuffix, bool continued)
        {
            // 새 런은 언제나 연출이 없는 상태에서 시작함. 이전 런의 연출이 새 런 화면에 결과를 띄우지 않게 함
            StopReveal();
            session = newSession;
            runMode = session.Mode;
            gachaEngine = session.GachaEngine;
            runManager = session.RunManager;

            Debug.Log("Run seed: " + session.Seed + " mode: " + session.Mode + logSuffix);
            playtestLog.RunStarted(session, settings.RevealSpeed, continued, Time.time);
            drawReveal.ShowIdle();

            // 이어하기면 이번 라운드에 뽑아 둔 부품을 바로 놓음. 돌파 직후에 꺼진 판은 이미 쏟은 뒤라 빈 트레이임
            if (harvestTray != null)
            {
                if (runManager.State == RunState.RoundCleared)
                {
                    harvestTray.Clear();
                }
                else
                {
                    harvestTray.ShowPile(runManager.RoundParts, runManager.Production);
                }
            }

            titlePanel.SetActive(false);
            modeSelectPanel.SetActive(false);
            settingsPanel.SetActive(false);
            moduleSelectPanel.SetActive(false);
            resultPanel.Hide();
            machinePanel.Show(runManager.EquippedModules);
            machinePanel.SetInteractable(true);

            // 앞 런에서 남은 계기판 연출 (▲▼) 을 지우고 시작함
            if (gaugePanel != null)
            {
                gaugePanel.CompleteNow();
            }

            // 새 런만 등장 연출. 이어하기는 최종 화면 그대로 (StopReveal 이 이미 최종 모습으로 맞췄음).
            // 값은 아래 Refresh 가 실제 시작 상태로 바로 넣음 (0 부터 세어 올리지 않음)
            // 방 조명: 새 런은 암전 → 전등 켜짐 (패널 등장과 같이 흐름) · 이어하기는 바로 밝은 방
            if (roomLight != null)
            {
                if (runIntro != null && !continued)
                {
                    roomLight.PlayLightsOn(settings.RevealTimeMultiplier);
                }
                else
                {
                    roomLight.ShowLit();
                }
            }

            if (runIntro != null && !continued)
            {
                // 런을 시작한 클릭 · 제출 (버튼을 뗀 순간) 이 같은 프레임에 등장 스킵으로 잡히지 않게 함
                revealStartFrame = Time.frameCount;
                machinePanel.SetInteractable(false);
                runIntro.Play(settings.RevealTimeMultiplier, OnIntroFinished);
            }

            Refresh();
        }

        private void OnIntroFinished()
        {
            // 준비 끝 — 기존 조건대로 입력을 엶. 이 프레임의 입력 (스킵한 클릭 · Space) 은 뽑기 · 오버클럭에 쓰지 않음
            introEndFrame = Time.frameCount;
            machinePanel.SetInteractable(!pauseMenu.IsOpen);
            drawButton.interactable = runManager.State == RunState.Drawing;
            RefreshOverclock();
        }

        private void OnDrawButtonClicked()
        {
            // 일시정지 창이 클릭을 막지만, 씬 배치가 바뀌어도 일시정지 중 뽑기가 되지 않게 코드에서도 막음
            if (ignoreDrawButtonClick || !IsRunInputAllowed())
            {
                return;
            }

            OnDrawClicked();
        }

        private void OnOverclockClicked()
        {
            // 뽑기와 같은 조건: 일시정지 · 연출 중 · 뽑기 상태가 아니면 받지 않음. 라운드마다 1번은 RunManager 가 지킴
            if (!IsRunInputAllowed() || drawReveal.IsPlaying || runManager.State != RunState.Drawing || !runManager.CanActivateOverclock)
            {
                return;
            }

            if (IsIntroPlaying || Time.frameCount <= introEndFrame)
            {
                return;
            }

            soundPlayer.PlayUiClick();
            runManager.ActivateOverclock();
            playtestLog.OverclockActivated(runManager, runManager.RoundParts.Count);

            // 켠 상태도 저장함. 껐다 켜서 다시 켤 수 있게 되면 라운드마다 1번 규칙이 깨짐
            SaveRun();

            // 버튼 글자만 바꿈. 전체 Refresh 는 생산량 숫자를 바로 맞춰, 조각이 날아오는 중이면 세기가 끊김 (교차검토 #34)
            RefreshOverclock();
        }

        private void OnDrawClicked()
        {
            // 연출 중에도 State 는 Drawing 임. 막지 않으면 공개 전에 다음 뽑기가 실행됨
            if (runManager == null || drawReveal.IsPlaying || runManager.State != RunState.Drawing)
            {
                return;
            }

            // 등장 연출 중 · 등장이 끝난 프레임에는 뽑지 않음 (버튼은 꺼 두지만 Space 와 씬 배치가 바뀐 경우까지)
            if (IsIntroPlaying || Time.frameCount <= introEndFrame)
            {
                return;
            }

            // 결과는 여기서 확정됨. 연출은 표시만 늦추므로 스킵해도 최종 상태가 같음
            if (gachaMachine != null)
            {
                gachaMachine.PullLever();
            }
            else
            {
                soundPlayer.PlayDrawLever();
            }

            // 앞 뽑기의 조각 · 숫자가 아직 날아가는 중이면 바로 최종 모습으로 (숫자가 뒤처지지 않게)
            if (harvestTray != null)
            {
                harvestTray.CompleteFlights();
            }

            if (productionCounter != null)
            {
                productionCounter.CompleteNow();
            }

            bool overclockActive = runManager.OverclockDrawsLeft > 0;
            DrawResult result = runManager.Draw();
            lastDrawResult = result;
            playtestLog.Draw(result, runManager, overclockActive, Time.time);

            // 연출 전에 저장함. 연출 중에 꺼도 이 뽑기는 되돌릴 수 없음 (최고 기록도 같음)
            if (runManager.State == RunState.RunFailed || runManager.State == RunState.RunCleared)
            {
                DeleteSave();
                lastRunNewRecord = bestRecords.Submit(session);
                SaveBestRecords();
                playtestLog.RunEnded(runManager, Time.time);
            }
            else
            {
                SaveRun();
            }

            revealStartFrame = Time.frameCount;
            drawButton.interactable = false;
            overclockButton.interactable = false;
            drawReveal.Play(result, OnRevealFinished);
        }

        private void OnRevealFinished()
        {
            // 부품 표시는 DrawRevealView 가 끝냈음. 화면은 나머지 숫자와 다음 단계(카드 · 결과 창)만 엶.
            // 더미 · 숫자 롤업은 공개 모습과 같이 움직임: 스킵 · 즉시면 바로 최종 모습
            bool animated = drawReveal.LastRevealAnimated;
            Refresh(animated);
            if (harvestTray != null)
            {
                harvestTray.AddOutput(lastDrawResult.Part, lastDrawResult.Output, animated);
            }

            // 소리 시작 순서: 공개음 · 3연속(DrawRevealView 가 이미 냄) → 돌파 → 카드 출현 / 런 실패. 앞 소리의 시작 시각 기준이라 꼬리는 겹침
            if (runManager.State == RunState.RoundCleared)
            {
                soundPlayer.PlayBreakthrough();
                if (gachaMachine != null)
                {
                    gachaMachine.PlayBreakthrough();
                }

                // 돌파 증기와 같이 이번 라운드 수확을 쏟음. 런 실패 · 클리어는 쏟지 않고 그대로 둠 (무엇을 뽑았는지 보이게)
                if (harvestTray != null)
                {
                    harvestTray.Dump();
                }

                ShowModuleOffer();
            }
            else if (runManager.State == RunState.RunFailed || runManager.State == RunState.RunCleared)
            {
                if (runManager.State == RunState.RunFailed && runFail != null)
                {
                    // 결과 · 기록은 뽑기 때 이미 저장됐음. 실패 연출 (라인 셧다운) 뒤에 결과 창만 늦게 띄움.
                    // 실패 소리는 연출이 냄 (동력 차단부터 모터가 멎는 소리 · 즉시 속도면 소리 없음)
                    runFail.Play(settings.RevealTimeMultiplier, ShowRunResult);
                }
                else if (runManager.State == RunState.RunFailed)
                {
                    soundPlayer.PlayRunFailed();
                    ShowRunResult();
                }
                else if (runClear != null)
                {
                    // 결과 · 기록은 뽑기 때 이미 저장됐음. 마무리 연출 뒤에 결과 창만 늦게 띄움.
                    // 소리는 넣지 않음 (클리어 소리는 소리 연결 때 · 10/29 소리 규칙 그대로)
                    runClear.Play(settings.RevealTimeMultiplier, ShowRunResult);
                }
                else
                {
                    ShowRunResult();
                }
            }
        }

        private void ShowRunResult()
        {
            // 연출을 스킵한 입력 (Space · 클릭) 이 같은 프레임에 결과 창 버튼까지 누르지 않게 이 프레임을 기억함
            resultShownFrame = Time.frameCount;
            resultPanel.Show(RecordText.ResultTitle(runManager.State), RecordText.ResultDetail(session, bestRecords, lastRunNewRecord));
        }

        private Action IgnoreOnResultFrame(Action action)
        {
            return () =>
            {
                if (Time.frameCount > resultShownFrame)
                {
                    action();
                }
            };
        }

        private void ShowModuleOffer()
        {
            // 여기서는 저장하지 않음. 돌파 직후(카드를 열기 전) 저장된 난수 상태에서 다시 열면 같은 카드가 나옴
            IReadOnlyList<ModuleData> offer = session.OpenOffer();
            int nextQuota = RunManager.QuotaFor(runManager.CurrentRound + 1);

            // 기계 패널은 카드 영역 밖에서 오버레이 위에 그려지므로 고르는 동안에도 팝업을 쓸 수 있음
            moduleSelectPanel.SetActive(true);
            soundPlayer.PlayCardAppear();
            ShowOfferCards(offer, nextQuota);
        }

        private void ShowOfferCards(IReadOnlyList<ModuleData> offer, int nextQuota)
        {
            // 돌다가 패널이 꺼졌던 회전이 남아 있으면 먼저 정리함 (뒤늦게 옛 내용을 덮어쓰지 않게)
            if (offerFlip != null)
            {
                offerFlip.Cancel();
            }

            for (int i = 0; i < moduleCards.Length; i++)
            {
                moduleCards[i].ResetFlip();
                moduleCards[i].SetSelectable(true);
                CreateCardContent(moduleCards[i], offer, i, nextQuota)();
            }

            RefreshRerollButton();
        }

        private Action CreateCardContent(ModuleCardView card, IReadOnlyList<ModuleData> offer, int index, int nextQuota)
        {
            if (index >= offer.Count)
            {
                return card.Hide;
            }

            // 미리보기 문구는 지금 만듦 (회전이 끝날 때 계산하지 않음). PreviewModule 은 복제본으로만 계산해 상태 · 난수가 그대로임
            ModuleData module = offer[index];
            ModulePreview modulePreview = runManager.PreviewModule(module);
            string detail = ModulePreviewFormatter.Format(modulePreview, parts, nextQuota);
            string summary = ModulePreviewFormatter.FormatSummary(modulePreview, parts, nextQuota);
            return () => card.Show(module, summary, detail, OnModuleSelected);
        }

        // 간단히 / 자세히는 카드마다 기억함 — 회전 중에 눌러도 회전이 끝나 새 내용을 쓸 때 지금 방식을 따름. 앱을 켜 둔 동안만 유지
        private void ToggleCardDetails()
        {
            showCardDetails = !showCardDetails;
            ApplyCardDetails();
        }

        private void ApplyCardDetails()
        {
            bool detailed = showCardDetails || cardDetailToggle == null;
            for (int i = 0; i < moduleCards.Length; i++)
            {
                if (moduleCards[i] != null)
                {
                    moduleCards[i].SetDetailed(detailed);
                }
            }

            if (cardDetailLabel != null)
            {
                cardDetailLabel.text = showCardDetails ? "간단히 보기" : "자세히 보기";
            }
        }

        private void RefreshRerollButton()
        {
            if (rerollButton == null)
            {
                return;
            }

            bool canReroll = session != null && session.CanRerollOffer;
            rerollButton.interactable = canReroll && !IsOfferFlipping;
            if (rerollLabel != null)
            {
                // 글자 크기는 씬에서 고정 (28 · 자동 크기 끔). 두 문구가 같은 크기로 보이게 여기서는 글자 · 색만 바꿈
                rerollLabel.text = canReroll ? "카드 다시 받기 (1회)" : "다시 받기 사용함";
                rerollLabel.color = canReroll ? rerollReadyLabelColor : rerollUsedLabelColor;
            }
        }

        private void OnRerollClicked()
        {
            if (!IsRunInputAllowed() || IsOfferFlipping || runManager.State != RunState.RoundCleared || !session.CanRerollOffer)
            {
                return;
            }

            // 공유 난수를 썼으므로 바로 저장함. 저장하지 않고 껐다 켜면 처음 카드로 돌아가 다시 받을 수 있게 됨.
            // 새 제안은 여기서 한 번만 정함. 회전 · 스킵 · 일시정지는 화면만 늦추므로 다시 뽑거나 난수를 더 쓰지 않음
            IReadOnlyList<ModuleData> offer = session.RerollOffer();
            playtestLog.OfferRerolled(runManager.CurrentRound);
            SaveRun();
            int nextQuota = RunManager.QuotaFor(runManager.CurrentRound + 1);
            if (offerFlip == null)
            {
                soundPlayer.PlayCardAppear();
                ShowOfferCards(offer, nextQuota);
                return;
            }

            // 누른 입력 (버튼을 뗀 순간) 이 같은 프레임에 회전 스킵으로 잡히지 않게 함
            revealStartFrame = Time.frameCount;
            // 카드 선택 잠금은 OfferFlipView 가 걺 (Play) · 풂 (끝 · 스킵 · 취소 · 꺼짐 복구)
            Action[] showNewContent = new Action[moduleCards.Length];
            for (int i = 0; i < moduleCards.Length; i++)
            {
                showNewContent[i] = CreateCardContent(moduleCards[i], offer, i, nextQuota);
            }

            // 다시 받기는 이미 썼음 — 글자는 바로 '사용함' (크기는 같음), 버튼은 꺼짐
            RefreshRerollButton();
            offerFlip.Play(moduleCards, showNewContent, settings.RevealTimeMultiplier, OnOfferFlipFinished);
        }

        private void OnOfferFlipFinished()
        {
            // 세 장이 모두 정면에 멎은 뒤에만 고를 수 있음 (선택 버튼은 OfferFlipView 가 이미 켰음).
            // 이 프레임의 입력 (스킵한 클릭) 은 카드 선택으로 쓰지 않음
            offerFlipEndFrame = Time.frameCount;
            RefreshRerollButton();

            // 걸림 소리는 정면에 멎을 때만 (자연히 끝남 · 즉시). 스킵은 남은 연출 소리를 내지 않음
            if (!offerFlip.LastFinishSkipped)
            {
                soundPlayer.PlayOfferLatch();
            }
        }

        private void OnModuleSelected(ModuleData module)
        {
            if (!IsRunInputAllowed() || runManager.State != RunState.RoundCleared)
            {
                return;
            }

            // 버튼을 꺼 두지만, 씬 배치 · 키보드 제출이 바뀌어도 회전 중 · 회전이 끝난 프레임에는 고르지 않게 코드에서도 막음
            if (IsOfferFlipping || Time.frameCount <= offerFlipEndFrame)
            {
                return;
            }

            Mouse mouse = Mouse.current;
            if (ignoreCardClick && mouse != null && mouse.leftButton.wasReleasedThisFrame)
            {
                return;
            }

            int clearedRound = runManager.CurrentRound;
            session.SelectModule(module);
            playtestLog.ModuleSelected(clearedRound, module);
            SaveRun();
            soundPlayer.PlayModuleEquip();

            moduleSelectPanel.SetActive(false);
            machinePanel.Show(runManager.EquippedModules);
            machinePanel.SetInteractable(true);
            drawReveal.ShowIdle();
            if (harvestTray != null)
            {
                harvestTray.Clear();
            }

            // 확률이 바뀌었으면 계기판이 이전 값에서 세어 바꿈. 이어지는 Refresh 는 같은 확률이라 이 연출을 끊지 않음
            if (gaugePanel != null)
            {
                gaugePanel.AnimateTo(
                    gachaEngine.GetProbability(Grade.N),
                    gachaEngine.GetProbability(Grade.R),
                    gachaEngine.GetProbability(Grade.SR),
                    settings.RevealTimeMultiplier);
            }

            Refresh();
        }

        private void Refresh()
        {
            Refresh(false);
        }

        // rollProduction = 방금 공개한 뽑기의 생산량을 세어 올림. 그 밖에는 바로 바꿈
        private void Refresh(bool rollProduction)
        {
            RunState state = runManager.State;

            roundText.text = runMode == RunMode.Endless
                ? "R" + runManager.CurrentRound + " (무한)"
                : "R" + runManager.CurrentRound + " / " + runManager.RoundCount;
            ShowProduction(rollProduction);
            drawsText.text = "남은 뽑기 " + runManager.DrawsRemaining;
            ShowGauge();

            drawButton.interactable = state == RunState.Drawing && !IsIntroPlaying;
            RefreshOverclock();

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

        private void ShowGauge()
        {
            if (gaugePanel == null)
            {
                gaugePanelText.text =
                    GradeStyle.Colorize(Grade.N, "N  " + FormatPercent(gachaEngine.GetProbability(Grade.N))) + "\n" +
                    GradeStyle.Colorize(Grade.R, "R  " + FormatPercent(gachaEngine.GetProbability(Grade.R))) + "\n" +
                    GradeStyle.Colorize(Grade.SR, "SR " + FormatPercent(gachaEngine.GetProbability(Grade.SR))) + "\n" +
                    GaugePanelView.FormatPity(gachaEngine.DrawsWithoutSuperRare, gachaEngine.PityThreshold);
                return;
            }

            gaugePanel.Show(
                gachaEngine.GetProbability(Grade.N),
                gachaEngine.GetProbability(Grade.R),
                gachaEngine.GetProbability(Grade.SR),
                gachaEngine.DrawsWithoutSuperRare,
                gachaEngine.PityThreshold);
        }

        private void ShowProduction(bool roll)
        {
            if (productionCounter == null)
            {
                quotaText.text = runManager.Production + " / " + runManager.Quota;
                quotaGauge.maxValue = runManager.Quota;
                quotaGauge.value = Mathf.Min(runManager.Production, runManager.Quota);
            }
            else if (roll && harvestTray != null && lastDrawResult.Output > 0)
            {
                // 조각이 앉을 때마다 오름 (OnPieceLanded). 다 앉으면 최종 값 · 튐
                productionCounter.BeginCount(runManager.Production, runManager.Quota, lastDrawResult.Part.Grade);
            }
            else if (roll)
            {
                productionCounter.RollTo(runManager.Production, runManager.Quota, lastDrawResult.Part.Grade);
            }
            else
            {
                productionCounter.SetImmediate(runManager.Production, runManager.Quota);
            }
        }

        private void RefreshOverclock()
        {
            overclockButton.interactable = runManager.CanActivateOverclock && !drawReveal.IsPlaying && !IsIntroPlaying;
            if (gachaMachine != null)
            {
                gachaMachine.SetOverclockActive(runManager.OverclockDrawsLeft > 0);
            }

            if (runManager.OverclockDrawsLeft > 0)
            {
                overclockLabel.text = "오버클럭 가동 중\n" + runManager.OverclockDrawsLeft + "회 남음";
            }
            else if (runManager.OverclockUsed)
            {
                overclockLabel.text = "오버클럭\n이번 라운드 사용함";
            }
            else
            {
                overclockLabel.text = "오버클럭\n3회 동안 R·SR 배수 +3 / N 0";
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

                // 설정 전체를 쓰므로 아직 안 쓴 볼륨도 이때 같이 저장됨
                volumeDirty = false;
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

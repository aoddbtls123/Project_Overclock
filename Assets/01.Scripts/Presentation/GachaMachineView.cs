using System;
using DG.Tweening;
using Overclock.Core;
using UnityEngine;
using UnityEngine.UI;

namespace Overclock.Presentation
{
    // 런 화면 가운데 기계의 겉모습 (기어 · 피스톤 · 바늘 · 증기 · 레버 손잡이).
    // 언제 움직일지는 DrawRevealView(기다림 · 공개)와 GrayBoxRunScreen(레버 · 돌파 · 런 이탈)이 정한다.
    // 결과 · 콜백은 모르고, 무엇을 해도 게임 상태는 바뀌지 않는다 (RevealEffectsView 와 같은 약속).
    // 시간은 scaled time 만 쓴다 — 일시정지(timeScale 0)면 기계도 그 자리에서 멈춘다.
    // 모습은 값(기어 각도 · 열 · 피스톤 · 바늘)만 바꾸고 ApplyPose 한 곳에서 그린다.
    // 쉬는 상태 = 열 0 · 피스톤 · 바늘 제자리 · 증기 투명 (기어는 천천히 계속 돈다). 레버는 따로 — 아래 StopReactions 참고.
    // 타격감 (11/04): 레버 · 래칫 · 피스톤은 가속하며 딱 멈춘다. 멈추는 순간 기계 전체가 덜컹하고 이벤트를 낸다 (소리는 받는 쪽이 낸다).
    // 덜컹만 설정의 흔들림 끄기를 따른다 (기계연출 구상 §14-1). 이 오브젝트의 위치를 움직이므로 부모 레이아웃에 묶이지 않게 둔다
    public sealed class GachaMachineView : MonoBehaviour
    {
        [Serializable]
        private sealed class GearSlot
        {
            [SerializeField] private RectTransform gear;
            [Tooltip("기준 기어 대비 회전 배율. 맞물린 기어는 부호를 반대로 · 작은 기어일수록 크게 (톱니 수에 반비례) · 소수 둘째 자리까지")]
            [SerializeField] private float speedRatio = 1f;

            public RectTransform Gear => gear;
            public float SpeedRatio => speedRatio;
        }

        [SerializeField] private GachaMachineSettings settings;
        [SerializeField] private GearSlot[] gears = Array.Empty<GearSlot>();
        [SerializeField] private RectTransform piston;
        [SerializeField] private RectTransform needle;
        [Tooltip("회전 축이 피벗인 레버 손잡이. 클릭 영역(Button)은 따로 둔다")]
        [SerializeField] private RectTransform leverHandle;
        [Tooltip("증기 이미지 풀. 차례로 돌려 쓴다")]
        [SerializeField] private Image[] steamPuffs = Array.Empty<Image>();
        [Tooltip("증기가 나오는 자리. 증기는 이 위치에서 시작한다")]
        [SerializeField] private RectTransform[] steamVents = Array.Empty<RectTransform>();
        [Tooltip("출력 창 이미지의 Outline. 공개된 등급 색 테두리 (11/05). 비어 있으면 테두리 없음")]
        [SerializeField] private Outline outputFrame;

        /// <summary>레버가 바닥에 닿은 순간 (받아들여진 뽑기마다 한 번).</summary>
        public event Action LeverBottomed;

        /// <summary>공개 반응의 래칫 한 칸이 딱 멈춘 순간 (스킵 · 즉시면 없음).</summary>
        public event Action RatchetStopped;

        private float gearAngle;
        private float ratchetOffset;
        private float heat;
        private float needleHeat;
        private float pistonHeatOffset;
        private float pistonKick;
        private float leverAngle;
        private Vector2 restPistonPosition;
        private RectTransform root;
        private Vector2 restRootPosition;
        // 클리어 마무리 연출 전용 겹 (11/06). 평소 연출은 쓰지 않는다 — 기본값이면 모습이 그대로다.
        // 같은 부품을 두 연출이 덮어쓰지 않게 값만 더하고 ApplyPose 한 곳에서 그린다
        private Vector3 restRootScale = Vector3.one;
        private float clearRootOffsetY;
        private float clearScaleY = 1f;
        private float needleJitter;
        private float needleWobble;
        private readonly System.Collections.Generic.List<Tween> clearTweens = new System.Collections.Generic.List<Tween>();
        // 런 실패 연출 전용 겹 (11/07 · 라인 셧다운). 값은 RunFailView 가 매 프레임 정해 넣고 여기서는 그리기만 한다.
        // 기본값이면 모습이 그대로다 (기어 속도 배율 1 · 나머지 0). ResetAll 이 되돌린다
        private float failGearSpeedScale = 1f;
        private float failNeedleLift;
        private float failPistonOffset;
        private float failRootOffsetY;
        private float thudOffset;
        private bool thudEnabled = true;
        private bool leverBottomPending;
        private Sequence revealSequence;
        private Tween leverTween;
        private Tween ratchetTween;
        private Tween tickTween;
        private float tickEnd;
        private Tween pistonTween;
        private int thudStage;
        private int thudFrame;
        private float thudPixels;
        private Tween[] puffTweens;
        private Vector2[] puffStartPositions;
        private int nextPuff;
        private int nextHeatSteam;

        private void Awake()
        {
            if (piston != null)
            {
                restPistonPosition = piston.anchoredPosition;
            }

            root = transform as RectTransform;
            if (root != null)
            {
                restRootPosition = root.anchoredPosition;
                restRootScale = root.localScale;
            }

            puffTweens = new Tween[steamPuffs.Length];
            puffStartPositions = new Vector2[steamPuffs.Length];
            for (int i = 0; i < steamPuffs.Length; i++)
            {
                // 증기는 투명해도 클릭을 막는다. 씬 설정과 관계없이 클릭을 통과시킨다 (레버가 증기 뒤에 있을 수 있음)
                if (steamPuffs[i] != null)
                {
                    steamPuffs[i].raycastTarget = false;
                    HidePuff(i);
                }
            }

            ClearGradeFrame();
            ApplyPose();
        }

        private void Update()
        {
            // 기어는 늘 돈다. 열이 오를수록 빨라진다 (처음엔 천천히, 끝에 갈수록 빠르게)
            // 런 실패 연출 중에는 속도 배율이 곱해진다 (힘겹게 돌다 동력이 끊기면 감속하며 멎는다 · 0 = 멈춤)
            float speed = Mathf.Lerp(settings.IdleGearSpeed, settings.HeatGearSpeed, heat * heat) * failGearSpeedScale;
            // 값이 한없이 커지지 않게 되돌린다. 360 × 3600 이면 배율이 소수 둘째 자리까지일 때 되돌리는 순간에도 각도가 튀지 않는다
            gearAngle = Mathf.Repeat(gearAngle + speed * Time.deltaTime, 360f * 3600f);
            AdvanceThud();
            ApplyPose();
        }

        private void OnDisable()
        {
            ResetAll();
        }

        private void OnDestroy()
        {
            KillReveal();
            KillLever();
            KillThud();
            KillPuffs();
            KillClear();
        }

        /// <summary>덜컹을 켜고 끈다 (설정의 흔들림). 끄면 지금 덜컹도 바로 제자리.</summary>
        public void SetThudEnabled(bool enabled)
        {
            thudEnabled = enabled;
            if (!enabled)
            {
                KillThud();
                thudOffset = 0f;
                ApplyPose();
            }
        }

        /// <summary>수확물이 트레이에 앉은 순간 (작은 덜컹).</summary>
        public void OnPartLanded()
        {
            Thud(settings.LandingThudPixels);
        }

        /// <summary>기계 전체 덜컹 (셔터가 닫힌 순간 등). 흔들림 끄기면 없음.</summary>
        public void Bump(float pixels)
        {
            Thud(pixels);
        }

        /// <summary>
        /// 기다리는 동안의 래칫 틱 (셔터 · §14-5) — 한 칸 조금 가속하며 돌고 딱 멈춘다. 덜컹 · 이벤트 없음 (소리는 부르는 쪽).
        /// 앞 틱이 끝나기 전이면 그 끝 자리에서 이어 돈다 (각도가 뒤로 가지 않게)
        /// </summary>
        public void PlayWaitTick(float angle, float seconds)
        {
            CompleteTick();
            float start = ratchetOffset;
            float end = start + angle;
            tickEnd = end;
            tickTween = DOTween.To(t => ratchetOffset = start + (end - start) * InCubic(t), 0f, 1f, seconds)
                .SetEase(Ease.Linear)
                .OnComplete(() =>
                {
                    tickTween = null;
                    ratchetOffset = end;
                });
        }

        /// <summary>
        /// 뽑기가 받아들여졌을 때 레버 손잡이를 내렸다 올린다. 연출 · 스킵과 관계없이 끝까지 움직인다.
        /// 바닥에 닿는 순간 덜컹 + LeverBottomed (손잡이가 없어도 같은 시각에 낸다 — 레버 소리가 이 이벤트로 난다)
        /// </summary>
        public void PullLever()
        {
            // 앞 레버가 바닥에 닿기 전에 다시 당기면 (즉시 속도에서 연타) 앞 레버를 바닥에 놓고 그 순간을 지금 낸다.
            // 그냥 끊으면 레버 소리가 한 번도 안 날 수 있다. 새 당김은 지금 각도에서 시작한다 (바닥에서 튀어 오르지 않게)
            KillLever();
            if (leverBottomPending)
            {
                Bottom();
            }

            // 새 뽑기 — 앞 공개의 등급 테두리를 끈다
            ClearGradeFrame();

            float start = leverAngle;
            float pulled = settings.LeverPulledAngle;
            float down = settings.LeverDownSeconds;
            float hold = settings.LeverHoldSeconds;
            float total = down + hold + settings.LeverUpSeconds;
            leverBottomPending = true;
            leverTween = DOTween.To(t =>
                {
                    float elapsed = t * total;
                    if (elapsed < down)
                    {
                        SetLeverAngle(start + (pulled - start) * InCubic(elapsed / down));
                        return;
                    }

                    // 바닥을 처음 넘은 갱신은 바닥 자세로 멈춘다 (소리 · 덜컹과 같은 프레임). 돌아오는 자세는 다음 갱신부터 (교차검토 #33)
                    if (leverBottomPending)
                    {
                        Bottom();
                        return;
                    }

                    // 바닥에서 잠깐 버틴 뒤 감속하며 올라온다 (11/04 사용자: "너무 빠르게 내려갔다 올라옴")
                    if (elapsed < down + hold)
                    {
                        SetLeverAngle(pulled);
                        return;
                    }

                    SetLeverAngle(pulled * (1f - OutQuad((elapsed - down - hold) / settings.LeverUpSeconds)));
                }, 0f, 1f, total)
                .SetEase(Ease.Linear)
                .OnComplete(() => leverTween = null);
        }

        private void Bottom()
        {
            leverBottomPending = false;
            SetLeverAngle(settings.LeverPulledAngle);
            Thud(settings.LeverThudPixels);
            LeverBottomed?.Invoke();
        }

        /// <summary>SR 기다림 진행률(0~1)에 맞춰 기어를 가속하고 피스톤을 연타하고 바늘을 올린다. 같은 진행률이면 피스톤 · 바늘은 같은 모습이다.</summary>
        public void SetHeat(float progress)
        {
            ApplyHeat(progress, settings.SteamSeconds, 1f);
        }

        // SR 기다림 · 클리어 기 모으기가 같이 쓴다. puffSeconds = 위 작은 증기 시간 · pistonPower = 피스톤 연타 깊이 배율 (평소 1)
        private void ApplyHeat(float progress, float puffSeconds, float pistonPower)
        {
            float clamped = Mathf.Clamp01(progress);
            heat = clamped;
            needleHeat = clamped;

            // 진행률을 시계로 쓰는 오르내림 — 스킵 · 일시정지와 늘 맞는다. 열이 오를수록 깊게 내려간다
            float wave = 0.5f - 0.5f * Mathf.Cos(2f * Mathf.PI * clamped * settings.HeatPistonCycles);
            pistonHeatOffset = settings.PistonTravel * wave * clamped * pistonPower;

            // 진행률이 다음 점을 넘으면 위 증기 구멍에서 작은 증기 하나 (구멍을 번갈아).
            // 한 번에 하나만 — 긴 프레임으로 여러 점을 지났으면 나머지는 증기 없이 넘긴다 (한 프레임에 몰려 나오지 않게)
            if (progress <= 0f)
            {
                nextHeatSteam = 0;
                return;
            }

            int count = settings.HeatSteamPointCount;
            if (nextHeatSteam < count && clamped >= settings.GetHeatSteamPoint(nextHeatSteam))
            {
                Puff(steamVents.Length > 0 ? nextHeatSteam % steamVents.Length : 0, settings.SmallSteamScale, 0f, puffSeconds);
                nextHeatSteam++;
                while (nextHeatSteam < count && clamped >= settings.GetHeatSteamPoint(nextHeatSteam))
                {
                    nextHeatSteam++;
                }
            }
        }

        /// <summary>공개된 등급 색 테두리를 출력 창에 켠다. 다음 레버 · 쉬는 화면 · 런 이탈까지 남는다 (스킵 · 즉시도 같은 모습).</summary>
        public void ShowGradeFrame(Grade grade)
        {
            if (outputFrame == null)
            {
                return;
            }

            float width = settings.GetFrameWidth(grade);
            outputFrame.effectColor = GradeStyle.GetColor(grade);
            outputFrame.effectDistance = new Vector2(width, -width);
            outputFrame.enabled = width > 0f;
        }

        /// <summary>출력 창 테두리를 끈다.</summary>
        public void ClearGradeFrame()
        {
            if (outputFrame != null)
            {
                outputFrame.enabled = false;
            }
        }

        /// <summary>
        /// 공개 순간 반응. N = 기어 한 칸 · R = 한 칸 + 피스톤 1회 + 작은 증기 · SR = 한 칸 + 피스톤 + 큰 증기 + 바늘 복귀.
        /// 스킵 · 즉시일 때는 부르지 않는다 (DrawRevealView).
        /// </summary>
        public void PlayReveal(Grade grade)
        {
            KillReveal();

            // 기다림 동안 쌓인 열은 공개 순간에 내려놓는다. 바늘만 SR 에서 천천히 돌아온다
            heat = 0f;
            pistonHeatOffset = 0f;
            pistonKick = 0f;
            if (grade != Grade.SR)
            {
                needleHeat = 0f;
            }

            PlayRatchet();
            if (grade != Grade.N)
            {
                PlayPistonKick();
            }

            if (grade == Grade.R)
            {
                Puff(0, settings.SmallSteamScale, 0f);
            }
            else if (grade == Grade.SR)
            {
                revealSequence = DOTween.Sequence();
                revealSequence.Insert(0f, DOTween.To(() => needleHeat, SetNeedleHeat, 0f, settings.NeedleReturnSeconds).SetEase(Ease.OutQuad));
                revealSequence.OnComplete(() => revealSequence = null);
                for (int i = 0; i < settings.SuperRareSteamPuffs; i++)
                {
                    Puff(i, settings.BigSteamScale, 0f);
                }
            }
        }

        /// <summary>할당량 돌파 — 긴 증기 배출. 스킵 · 즉시여도 낸다 (10/30 결정: 공개가 아니라 라운드 사건).</summary>
        public void PlayBreakthrough()
        {
            PlayBreakthrough(1f);
        }

        /// <summary>
        /// 돌파 증기. timeMultiplier 가 간격 · 증기 시간에 곱해진다 (클리어 마무리 연출의 빠름 · 11/06).
        /// 라운드 돌파는 위 PlayBreakthrough() = 배율 1 그대로 (연출 속도와 무관 · 10/30 결정).
        /// </summary>
        public void PlayBreakthrough(float timeMultiplier)
        {
            PlayBreakthrough(timeMultiplier, 1f);
        }

        /// <summary>돌파 증기 · steamScale = 큰 증기 크기 배율 (클리어 뒤로 갈수록 잦아드는 증기 · 11/06). 평소는 1</summary>
        public void PlayBreakthrough(float timeMultiplier, float steamScale)
        {
            float multiplier = timeMultiplier > 0f ? timeMultiplier : 1f;
            for (int i = 0; i < settings.BreakthroughSteamPuffs; i++)
            {
                Puff(i, settings.BigSteamScale * steamScale, i * settings.BreakthroughSteamInterval * multiplier, settings.SteamSeconds * multiplier);
            }
        }

        /// <summary>
        /// 클리어 기 모으기 (11/06). progress 0 → 1: 기어 가속 · 바늘 올라감 · 틈의 작은 증기는 SR 기다림과 같고,
        /// 피스톤은 뒤로 갈수록 깊어진다 (진행률²). needleJitterDegrees = 바늘을 열 각도 위에서 더 흔드는 각도 (부르는 쪽이 정한다) ·
        /// timeMultiplier = 작은 증기 시간 배율 (빠름 0.5)
        /// </summary>
        public void SetClearCharge(float progress, float needleJitterDegrees, float timeMultiplier)
        {
            float multiplier = timeMultiplier > 0f ? timeMultiplier : 1f;
            ApplyHeat(progress, settings.SteamSeconds * multiplier, Mathf.Clamp01(progress));
            needleJitter = needleJitterDegrees;
            ApplyPose();
        }

        /// <summary>
        /// 클리어 수축 · 버팀 (11/06): seconds 동안 몸통이 sinkPixels 만큼 내려앉고 세로만 squashY 로 눌린다 (가로는 그대로) ·
        /// 바늘은 높은 자리에서 멈춰 버틴다 · 새 증기는 내지 않는다
        /// </summary>
        public void HoldClearPressure(float sinkPixels, float squashY, float seconds)
        {
            KillClear();
            needleJitter = 0f;
            needleWobble = 0f;
            float duration = seconds > 0f ? seconds : 0.01f;
            AddClearTween(DOTween.To(t =>
                {
                    // 점점 빠르게 눌린다 (힘을 모아 버티는 느낌)
                    float eased = t * t;
                    clearRootOffsetY = -sinkPixels * eased;
                    clearScaleY = Mathf.Lerp(1f, squashY, eased);
                }, 0f, 1f, duration).SetEase(Ease.Linear));
            ApplyPose();
        }

        /// <summary>
        /// 클리어 분출 · 반동 (11/06). 몸통 즉시 → 기어 래칫 → 피스톤 → 바늘 복귀 · 잔떨림 순으로 시간차를 두고 움직인다.
        /// 몸통은 bouncePixels 만큼 위로 튀었다가 settleSeconds 안에 먼저 자리 잡고, 세로는 popY 를 살짝 넘었다 돌아온다.
        /// 모든 시간 (지연 · 래칫 · 피스톤 · 바늘 · 잔떨림) 에 timeMultiplier 가 곱해진다. 증기는 PlayBreakthrough 로 따로
        /// </summary>
        public void PlayClearBurst(float bouncePixels, float popY, float settleSeconds, float gearDelay, float pistonDelay, float needleDelay,
            float needleWobbleDegrees, float wobbleSeconds, float timeMultiplier)
        {
            float multiplier = timeMultiplier > 0f ? timeMultiplier : 1f;
            // 눌린 자리에서 출발한다 — KillClear 가 값을 0 으로 돌리기 전에 읽는다 (안 그러면 분출 순간 한 번 제자리로 튄다)
            float startOffset = clearRootOffsetY;
            float startScale = clearScaleY;
            KillClear();
            KillReveal();
            clearRootOffsetY = startOffset;
            clearScaleY = startScale;
            heat = 0f;
            pistonHeatOffset = 0f;
            pistonKick = 0f;
            needleJitter = 0f;
            float settle = Mathf.Max(0.01f, settleSeconds * multiplier);
            AddClearTween(DOTween.To(t =>
                {
                    if (t < 0.2f)
                    {
                        // 눌린 자리에서 위로 튀어 오른다
                        float u = t / 0.2f;
                        float rise = 1f - (1f - u) * (1f - u);
                        clearRootOffsetY = Mathf.Lerp(startOffset, bouncePixels, rise);
                        clearScaleY = Mathf.Lerp(startScale, popY, rise);
                    }
                    else
                    {
                        // 한 번 살짝 내려갔다가 자리 잡는다 (철제 몸통 — 오래 출렁이지 않게)
                        float u = (t - 0.2f) / 0.8f;
                        float damp = (1f - u) * (1f - u);
                        clearRootOffsetY = bouncePixels * damp * Mathf.Cos(Mathf.PI * 1.5f * u);
                        clearScaleY = 1f + (popY - 1f) * damp;
                    }
                }, 0f, 1f, settle).SetEase(Ease.Linear).OnComplete(() =>
                {
                    clearRootOffsetY = 0f;
                    clearScaleY = 1f;
                }));

            AddClearTween(Delay(gearDelay * multiplier, () => PlayRatchet(multiplier)));
            AddClearTween(Delay(pistonDelay * multiplier, () => PlayPistonKick(multiplier)));
            AddClearTween(Delay(needleDelay * multiplier, () =>
            {
                // 바늘은 높은 자리에서 내려오며 작게 잔떨림 (작은 부품만 짧게 남는다)
                float from = needleHeat;
                AddClearTween(DOTween.To(t => needleHeat = from * (1f - (1f - (1f - t) * (1f - t))), 0f, 1f, settings.NeedleReturnSeconds * multiplier)
                    .SetEase(Ease.Linear));
                float wobble = Mathf.Max(0.01f, wobbleSeconds * multiplier);
                AddClearTween(DOTween.To(t => needleWobble = needleWobbleDegrees * (1f - t) * (1f - t) * Mathf.Sin(2f * Mathf.PI * 4f * t), 0f, 1f, wobble)
                    .SetEase(Ease.Linear)
                    .OnComplete(() => needleWobble = 0f));
            }));
        }

        private static Tween Delay(float seconds, Action action)
        {
            return DOTween.To(t => { }, 0f, 1f, Mathf.Max(0.0001f, seconds)).SetEase(Ease.Linear).OnComplete(() => action());
        }

        private void AddClearTween(Tween tween)
        {
            clearTweens.Add(tween);
        }

        private void KillClear()
        {
            for (int i = 0; i < clearTweens.Count; i++)
            {
                if (clearTweens[i] != null && clearTweens[i].IsActive())
                {
                    clearTweens[i].Kill();
                }
            }

            clearTweens.Clear();
            clearRootOffsetY = 0f;
            clearScaleY = 1f;
            needleJitter = 0f;
            needleWobble = 0f;
        }

        /// <summary>
        /// 런 실패 연출의 자세 (11/07). gearSpeedScale = 기어 속도 배율 (1 평소 · 0 멈춤) · needleLiftDegrees = 바늘이 무출력 자리에서
        /// 출력 쪽으로 뜬 각도 · pistonSagPixels = 피스톤이 내려간 거리 · bodyOffsetY = 몸통 높이 (흔들림 끄기면 몸통은 제자리).
        /// 값만 받고 시간은 부르는 쪽 (RunFailView) 이 잰다
        /// </summary>
        public void SetFailPose(float gearSpeedScale, float needleLiftDegrees, float pistonSagPixels, float bodyOffsetY)
        {
            failGearSpeedScale = Mathf.Max(0f, gearSpeedScale);
            failNeedleLift = needleLiftDegrees;
            failPistonOffset = pistonSagPixels;
            failRootOffsetY = bodyOffsetY;
            ApplyPose();
        }

        /// <summary>런 실패 자세를 평소대로 (런을 떠날 때 · 새 런).</summary>
        public void ClearFailPose()
        {
            SetFailPose(1f, 0f, 0f, 0f);
        }

        /// <summary>약한 잔압 증기 한 번 (런 실패 · 동력 차단 뒤). scale = 작은 증기 대비 크기 배율 · seconds = 증기 시간.</summary>
        public void PlayLeak(float scale, float seconds)
        {
            Puff(0, settings.SmallSteamScale * scale, 0f, Mathf.Max(0.01f, seconds));
        }

        /// <summary>마지막 돌파 증기가 끝나는 시각 (호출 시점부터 · 초). 클리어 연출이 증기보다 먼저 끝나지 않게 쓴다.</summary>
        public float GetBreakthroughSeconds(float timeMultiplier)
        {
            float multiplier = timeMultiplier > 0f ? timeMultiplier : 1f;
            int puffs = settings.BreakthroughSteamPuffs > 1 ? settings.BreakthroughSteamPuffs : 1;
            return ((puffs - 1) * settings.BreakthroughSteamInterval + settings.SteamSeconds) * multiplier;
        }

        /// <summary>
        /// 기다림 · 공개 · 돌파 반응을 멈추고 쉬는 상태로 돌린다 (새 연출 · 스킵 · Cancel · 모듈 선택 뒤).
        /// 레버는 건드리지 않는다 — 클릭 순서가 PullLever → Draw → Play(여기를 부름) 라서 레버까지 멈추면 당기자마자 끊긴다.
        /// </summary>
        public void StopReactions()
        {
            KillReveal();
            KillPuffs();
            KillClear();
            nextHeatSteam = 0;
            heat = 0f;
            needleHeat = 0f;
            pistonHeatOffset = 0f;
            pistonKick = 0f;
            ApplyPose();
        }

        /// <summary>클리어 연출을 건너뛸 때 (11/06). StopReactions + 덜컹까지 제자리 — 래칫이 멈춘 순간의 2프레임 덜컹이 결과 창 뒤에 남지 않게.
        /// 레버 · 출력 창 테두리는 그대로 둔다.</summary>
        public void StopClearMotion()
        {
            KillThud();
            StopReactions();
        }

        /// <summary>레버 · 덜컹까지 포함해 모두 쉬는 상태로 (런 이탈 · 비활성화).</summary>
        public void ResetAll()
        {
            // 런을 떠날 때는 바닥 순간(레버 소리)을 내지 않는다
            KillLever();
            leverBottomPending = false;
            KillThud();
            leverAngle = 0f;
            thudOffset = 0f;
            failGearSpeedScale = 1f;
            failNeedleLift = 0f;
            failPistonOffset = 0f;
            failRootOffsetY = 0f;
            ClearGradeFrame();
            StopReactions();
        }

        private void PlayRatchet()
        {
            PlayRatchet(1f);
        }

        // multiplier = 시간 배율 (클리어 빠름 · 11/06). 평소 1
        private void PlayRatchet(float multiplier)
        {
            // 반대로 살짝 당겼다가 (windup) 한 칸을 가속하며 돌고 딱 멈춘다. 멈추는 순간 덜컹 + RatchetStopped
            Kill(ref ratchetTween);
            float start = ratchetOffset;
            float windup = settings.RatchetWindupSeconds * multiplier;
            float spin = settings.RatchetSeconds * multiplier;
            float total = windup + spin;
            float back = start - settings.RatchetWindupAngle;
            float end = start + settings.RatchetAngle;
            ratchetTween = DOTween.To(t =>
                {
                    float elapsed = t * total;
                    ratchetOffset = elapsed < windup
                        ? start + (back - start) * (elapsed / windup)
                        : back + (end - back) * InCubic((elapsed - windup) / spin);
                }, 0f, 1f, total)
                .SetEase(Ease.Linear)
                .OnComplete(() =>
                {
                    ratchetTween = null;
                    ratchetOffset = end;
                    Thud(settings.RatchetThudPixels);
                    RatchetStopped?.Invoke();
                });
        }

        private void PlayPistonKick()
        {
            PlayPistonKick(1f);
        }

        // multiplier = 시간 배율 (클리어 빠름 · 11/06). 평소 1
        private void PlayPistonKick(float multiplier)
        {
            // 가속하며 내려가 딱 멈춤 → 잠깐 멈춰 있음 → 돌아옴
            Kill(ref pistonTween);
            float travel = settings.PistonTravel;
            float outTime = settings.PistonOutSeconds * multiplier;
            float hold = settings.PistonHoldSeconds * multiplier;
            float back = settings.PistonReturnSeconds * multiplier;
            float total = outTime + hold + back;
            pistonTween = DOTween.To(t =>
                {
                    float elapsed = t * total;
                    if (elapsed < outTime)
                    {
                        pistonKick = travel * InCubic(elapsed / outTime);
                    }
                    else if (elapsed < outTime + hold)
                    {
                        pistonKick = travel;
                    }
                    else
                    {
                        pistonKick = travel * (1f - OutQuad((elapsed - outTime - hold) / back));
                    }
                }, 0f, 1f, total)
                .SetEase(Ease.Linear)
                .OnComplete(() =>
                {
                    pistonTween = null;
                    pistonKick = 0f;
                });
        }

        private void Thud(float pixels)
        {
            // 아래로 → 위로 1/3 → 제자리, 각 자세를 한 프레임씩 (§14-3 "2프레임"). 시간으로 나누면 긴 프레임에서 위 단계가 사라진다 (교차검토 #33).
            // 멈춘 그 프레임에 바로 아래로. 다음 단계는 이 프레임 다음의 Update 부터 · 일시정지(deltaTime 0) 중에는 넘어가지 않는다.
            // 흔들림을 껐거나 크기가 0 이면 하지 않는다
            if (!thudEnabled || pixels <= 0f || root == null)
            {
                return;
            }

            thudPixels = pixels;
            thudStage = 1;
            thudFrame = Time.frameCount;
            thudOffset = -pixels;
            ApplyPose();
        }

        private void AdvanceThud()
        {
            if (thudStage == 0 || Time.frameCount <= thudFrame || Time.deltaTime <= 0f)
            {
                return;
            }

            thudFrame = Time.frameCount;
            thudStage = thudStage == 1 ? 2 : 0;
            thudOffset = thudStage == 2 ? thudPixels / 3f : 0f;
        }

        private static float InCubic(float x)
        {
            float clamped = Mathf.Clamp01(x);
            return clamped * clamped * clamped;
        }

        private static float OutQuad(float x)
        {
            float rest = 1f - Mathf.Clamp01(x);
            return 1f - rest * rest;
        }

        private void Puff(int index, float endScale, float delay)
        {
            Puff(index, endScale, delay, settings.SteamSeconds);
        }

        private void Puff(int index, float endScale, float delay, float seconds)
        {
            if (steamPuffs.Length == 0 || steamVents.Length == 0)
            {
                return;
            }

            int slot = nextPuff;
            nextPuff = (nextPuff + 1) % steamPuffs.Length;
            KillPuff(slot);
            RectTransform vent = steamVents[index % steamVents.Length];
            if (steamPuffs[slot] == null || vent == null)
            {
                return;
            }

            // 구멍을 돌아가며 쓰고, 같은 구멍을 두 번째 이상 쓰면 옆으로 벌린다 (안 벌리면 같은 시각에 완전히 겹쳐 한 덩어리로 보임).
            // 위치는 차례로만 정한다 — 난수 없음
            RectTransform puffRect = steamPuffs[slot].rectTransform;
            puffRect.position = vent.position;
            puffStartPositions[slot] = puffRect.anchoredPosition + new Vector2(SpreadOffset(index / steamVents.Length), 0f);
            HidePuff(slot);

            puffTweens[slot] = DOTween.To(t => ApplyPuff(slot, endScale, t), 0f, 1f, seconds)
                .SetDelay(delay)
                .SetEase(Ease.OutQuad)
                .OnComplete(() =>
                {
                    puffTweens[slot] = null;
                    HidePuff(slot);
                });
        }

        private float SpreadOffset(int round)
        {
            // 0 → 0 · 1 → +간격 · 2 → −간격 · 3 → +2×간격 · 4 → −2×간격 …
            int step = (round + 1) / 2;
            float sign = round % 2 == 1 ? 1f : -1f;
            return settings.SteamSpread * step * sign;
        }

        private void ApplyPuff(int slot, float endScale, float t)
        {
            Image puff = steamPuffs[slot];
            float scale = Mathf.Lerp(settings.SteamStartScale, endScale, t);
            puff.rectTransform.localScale = new Vector3(scale, scale, 1f);
            puff.rectTransform.anchoredPosition = puffStartPositions[slot] + new Vector2(0f, settings.SteamRise * t);
            Color color = settings.SteamColor;
            color.a *= 1f - t;
            puff.color = color;
        }

        private void HidePuff(int slot)
        {
            Color color = settings.SteamColor;
            color.a = 0f;
            steamPuffs[slot].color = color;
        }

        private void ApplyPose()
        {
            // 씬을 내릴 때는 다른 오브젝트가 먼저 파괴될 수 있다. 파괴된 대상은 건드리지 않는다
            float angle = gearAngle + ratchetOffset;
            for (int i = 0; i < gears.Length; i++)
            {
                RectTransform gear = gears[i] != null ? gears[i].Gear : null;
                if (gear != null)
                {
                    gear.localEulerAngles = new Vector3(0f, 0f, -angle * gears[i].SpeedRatio);
                }
            }

            if (piston != null)
            {
                piston.anchoredPosition = restPistonPosition + new Vector2(0f, -(pistonHeatOffset + pistonKick + failPistonOffset));
            }

            if (needle != null)
            {
                // 실패 연출의 뜬 각도는 무출력 자리에서 출력 쪽으로 (설정의 각도 부호와 관계없이)
                float failLift = Mathf.Sign(settings.NeedleMaxAngle - settings.NeedleRestAngle) * failNeedleLift;
                needle.localEulerAngles = new Vector3(0f, 0f, Mathf.Lerp(settings.NeedleRestAngle, settings.NeedleMaxAngle, needleHeat) + needleJitter + needleWobble + failLift);
            }

            if (leverHandle != null)
            {
                leverHandle.localEulerAngles = new Vector3(0f, 0f, leverAngle);
            }

            if (root != null)
            {
                // 클리어 몸통 반동 (내려앉음 · 튀어오름 · 세로 눌림) · 실패 내려앉음은 덜컹과 같이 흔들림 설정을 따른다. 끄면 몸통은 제자리 · 부품은 그대로 움직임
                float bodyOffset = thudEnabled ? clearRootOffsetY + failRootOffsetY : 0f;
                float bodyScaleY = thudEnabled ? clearScaleY : 1f;
                root.anchoredPosition = restRootPosition + new Vector2(0f, thudOffset + bodyOffset);
                root.localScale = new Vector3(restRootScale.x, restRootScale.y * bodyScaleY, restRootScale.z);
            }
        }

        private void SetLeverAngle(float value)
        {
            leverAngle = value;
            ApplyPose();
        }

        private void SetNeedleHeat(float value)
        {
            needleHeat = value;
        }

        private void KillReveal()
        {
            if (revealSequence != null && revealSequence.IsActive())
            {
                revealSequence.Kill();
            }

            revealSequence = null;
            CompleteTick();
            Kill(ref ratchetTween);
            Kill(ref pistonTween);
        }

        private void CompleteTick()
        {
            // 틱은 끝 자리로 보낸다 (공개 래칫 · 다음 틱이 그 자리에서 시작 · 각도가 뒤로 가지 않게)
            if (tickTween == null)
            {
                return;
            }

            Kill(ref tickTween);
            ratchetOffset = tickEnd;
        }

        private void KillLever()
        {
            Kill(ref leverTween);
        }

        private void KillThud()
        {
            thudStage = 0;
            thudOffset = 0f;
        }

        private static void Kill(ref Tween tween)
        {
            if (tween != null && tween.IsActive())
            {
                tween.Kill();
            }

            tween = null;
        }

        private void KillPuffs()
        {
            if (puffTweens == null)
            {
                return;
            }

            for (int i = 0; i < puffTweens.Length; i++)
            {
                KillPuff(i);
                if (steamPuffs[i] != null)
                {
                    HidePuff(i);
                }
            }
        }

        private void KillPuff(int slot)
        {
            Tween tween = puffTweens[slot];
            if (tween != null && tween.IsActive())
            {
                tween.Kill();
            }

            puffTweens[slot] = null;
        }
    }
}

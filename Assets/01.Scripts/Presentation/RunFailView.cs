using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace Overclock.Presentation
{
    // 런 실패 연출 (11/07 · 라인 셧다운): 할당량을 못 채운 생산 라인이 전원이 내려가며 멎음. 일반 · 무한 모드 공통.
    //   0. 기다림      마지막 뽑기의 조각 낙하 · 숫자 세기 · 기계식 계수기 바퀴가 멈출 때까지 (최대 maxSettleWait) — 마지막 결과를 자르지 않음
    //   A. 출력 불안정  기어가 힘겹게 돌다 두 번 힘이 빠짐 · 바늘이 무출력 자리 부근에서 살짝 떠 떨림 · 표시등 한두 번 깜빡 · 버벅임 소리
    //   B. 동력 차단    차단기 소리 하나 · 출력 창 테두리부터 꺼지고 표시등이 시간차로 따라 꺼짐 · 몸통이 살짝 내려앉음 ·
    //                   약한 잔압 증기 한 번 · 모터가 멎는 소리 시작
    //   C. 관성 정지    기어는 감속하며 멎음 · 피스톤이 몸통보다 늦게 처지며 작게 덜컹 · 바늘이 무출력 자리로 떨어져 한 번 튀고 멈춤 ·
    //                   모터 소리 점점 작게 · 몸체가 어두워짐 · 끝에 작은 금속 걸림 (소리 · 기어가 뒤로 살짝)
    //   D. 정적        멎은 기계를 잠깐 보여 준 뒤 결과 창. 어두운 모습은 결과 창 뒤에 남고 런을 떠날 때 (Cancel) 되돌림
    // 약속은 RunClearView 와 같음: Play 한 번에 onFinished 정확히 한 번 · Skip = 바로 최종 모습 + 콜백 · Cancel = 콜백 없음 ·
    // 시간은 Time.deltaTime 만 (일시정지면 멈춤 · 새 효과도 없음). 생산량 · 수확물 · 결과 · 기록은 바꾸지 않음.
    // 모습은 경과 시간 하나로 매 프레임 계산함 (ApplyAt) — 스킵 · 즉시는 같은 계산의 끝 모습이라 서로 갈리지 않음
    public sealed class RunFailView : MonoBehaviour
    {
        private const int StutterEvent = 0;
        private const int CutEvent = 1;
        private const int LeakEvent = 2;
        private const int ClankEvent = 3;
        private const int EventCount = 4;
        // 최종 모습을 계산할 때 쓰는 충분히 늦은 시각 (모든 단계가 끝난 뒤)
        private const float FinalTime = 1000000f;

        [SerializeField] private RunFailSettings settings;
        [Tooltip("비어 있으면 기계 반응 없이")]
        [SerializeField] private GachaMachineView machine;
        [Tooltip("비어 있으면 소리 없이")]
        [SerializeField] private SoundPlayer sounds;
        [Tooltip("마지막 뽑기의 조각 낙하를 기다림. 비어 있으면 기다리지 않음")]
        [SerializeField] private HarvestTrayView harvestTray;
        [Tooltip("마지막 뽑기의 숫자 세기를 기다림. 비어 있으면 기다리지 않음")]
        [SerializeField] private ProductionCounterView productionCounter;
        [Tooltip("기계식 계수기 바퀴가 멈추기를 기다림 (숫자 세기가 끝나도 바퀴는 조금 더 따라감). 비어 있으면 기다리지 않음")]
        [SerializeField] private MechanicalCounterView mechanicalCounter;
        [Tooltip("출력 창 테두리 다음에 꺼지는 순서대로 (계기판 → 작은 표시등). 원래 색에서 어두운 색으로")]
        [SerializeField] private Image[] lightStages = Array.Empty<Image>();
        [Tooltip("관성 정지 동안 어두워지는 기계 몸체 이미지 (수확물 · 계수기 · 버튼은 넣지 않음 — 결과가 읽혀야 함)")]
        [SerializeField] private Image[] dimTargets = Array.Empty<Image>();

        private Coroutine routine;
        private bool skipRequested;
        private bool shakeEnabled = true;
        private bool flashEnabled = true;
        // 멎은 모습이 남아 있는가 (끝난 뒤에도 결과 창 뒤에 남음 · Cancel 이 되돌림)
        private bool shutDown;
        private Color[] lightRestColors = Array.Empty<Color>();
        private Color[] dimRestColors = Array.Empty<Color>();

        public bool IsPlaying { get; private set; }

        private void Awake()
        {
            lightRestColors = ReadColors(lightStages);
            dimRestColors = ReadColors(dimTargets);
        }

        private void OnDisable()
        {
            Cancel();
        }

        /// <summary>흔들림 · 섬광 설정 (바로 적용). 흔들림 끄기 = 몸통 내려앉음 · 피스톤 덜컹 · 바늘 떨림/튐 · 기어 걸림 없음 · 섬광 끄기 = 깜빡임 없음.</summary>
        public void SetMotionOptions(bool shake, bool flash)
        {
            shakeEnabled = shake;
            flashEnabled = flash;
        }

        /// <summary>실패 연출을 시작함. timeMultiplier = 연출 속도 배율 (0 이하 = 움직임 · 증기 · 소리 없이 최종 모습 + 바로 onFinished).</summary>
        public void Play(float timeMultiplier, Action onFinished)
        {
            if (onFinished == null)
            {
                throw new ArgumentNullException(nameof(onFinished));
            }

            if (IsPlaying)
            {
                throw new InvalidOperationException("이미 실패 연출 중입니다.");
            }

            if (settings == null)
            {
                onFinished();
                return;
            }

            if (timeMultiplier <= 0f)
            {
                ApplyFinal();
                onFinished();
                return;
            }

            IsPlaying = true;
            skipRequested = false;
            Coroutine started = StartCoroutine(PlayRoutine(timeMultiplier, onFinished));
            if (IsPlaying && routine == null)
            {
                routine = started;
            }
        }

        /// <summary>남은 연출을 건너뛰고 최종 모습 + 결과 창으로. 연출 중이 아니면 무시함.</summary>
        public void Skip()
        {
            if (IsPlaying)
            {
                skipRequested = true;
            }
        }

        /// <summary>멈추고 콜백을 부르지 않음 (런을 버릴 때 · 새 런). 어두워진 모습 · 멎은 자세까지 원래대로 돌림.</summary>
        public void Cancel()
        {
            if (routine != null)
            {
                StopCoroutine(routine);
                routine = null;
            }

            bool wasPlaying = IsPlaying;
            IsPlaying = false;

            // 끝난 뒤에도 금속 걸림 소리의 꼬리가 남아 있을 수 있음 — 연출 중이 아니어도 실패 소리는 늘 멈춤 (교차검토 #42)
            if (sounds != null)
            {
                sounds.StopRunFailSounds();
            }

            if (wasPlaying && machine != null)
            {
                machine.StopClearMotion();
            }

            if (wasPlaying || shutDown)
            {
                RestoreLooks();
            }
        }

        private IEnumerator PlayRoutine(float multiplier, Action onFinished)
        {
            // 마지막 뽑기의 조각 · 숫자가 끝나기를 기다림 (자르지 않음). 너무 길면 상한까지만
            float waited = 0f;
            float maxWait = settings.MaxSettleWait * multiplier;
            while (!skipRequested && waited < maxWait && IsLastDrawBusy())
            {
                yield return null;
                waited += Time.deltaTime;
            }

            float cutAt = settings.UnstableSeconds * multiplier;
            float coastAt = cutAt + settings.CutSeconds * multiplier;
            float coastEnd = coastAt + settings.CoastSeconds * multiplier;
            float total = coastEnd + settings.StillSeconds * multiplier;
            float leakSeconds = settings.LeakSeconds * multiplier;
            float leakEnd = 0f;
            bool motorStarted = false;
            int nextEvent = StutterEvent;
            float elapsed = 0f;
            // 이번 반복에서 시간이 흘렀는가. 일시정지 (deltaTime 0) 중에는 밀린 일도 새로 하지 않음 (교차검토 #41 과 같은 규칙)
            bool timeMoved = true;
            while (!skipRequested)
            {
                ApplyAt(elapsed, multiplier);

                // 소리 · 증기 · 걸림은 차례대로 · 시간이 흐른 프레임에 하나까지 (긴 프레임으로 밀려도 한 프레임에 몰려 나오지 않게)
                if (timeMoved && nextEvent < EventCount && elapsed >= GetEventTime(nextEvent, multiplier))
                {
                    FireEvent(nextEvent, multiplier);
                    if (nextEvent == CutEvent)
                    {
                        motorStarted = true;
                    }
                    else if (nextEvent == LeakEvent && machine != null && settings.LeakScale > 0f)
                    {
                        leakEnd = elapsed + leakSeconds;
                    }

                    nextEvent++;
                }

                if (motorStarted && timeMoved && sounds != null)
                {
                    // 모터 소리는 차단부터 관성 정지 끝까지 점점 작아짐
                    float fade = Mathf.Clamp01((elapsed - cutAt) / (coastEnd - cutAt));
                    sounds.SetRunFailMotorVolumeScale((1f - fade) * (1f - fade));
                }

                if (elapsed >= total && nextEvent >= EventCount && elapsed >= leakEnd)
                {
                    break;
                }

                yield return null;
                float delta = Time.deltaTime;
                elapsed += delta;
                timeMoved = delta > 0f;
            }

            routine = null;
            IsPlaying = false;
            if (skipRequested)
            {
                // 스킵: 마지막 뽑기의 조각 · 숫자를 최종 모습으로 · 잔압 증기 · 기어 걸림 · 이번 연출 소리를 지움
                CompleteLastDraw();
                if (machine != null)
                {
                    machine.StopClearMotion();
                }

                if (sounds != null)
                {
                    sounds.StopRunFailSounds();
                }
            }
            else if (sounds != null)
            {
                // 끝: 모터는 이미 0 · 걸림 소리 꼬리는 남김
                sounds.StopRunFailMotor();
            }

            ApplyFinal();
            onFinished();
        }

        private bool IsLastDrawBusy()
        {
            // 숫자 세기가 끝나도 기계식 계수기 바퀴는 조금 더 따라감 — 바퀴까지 멈춘 뒤 시작함 (교차검토 #42)
            return (harvestTray != null && harvestTray.FlyingCount > 0)
                || (productionCounter != null && productionCounter.IsAnimating)
                || (mechanicalCounter != null && mechanicalCounter.IsMoving);
        }

        private void CompleteLastDraw()
        {
            // GrayBoxRunScreen 이 다음 뽑기 전에 하는 것과 같은 순서 (조각을 앉혀 숫자를 올린 뒤 숫자를 최종 값으로 · 바퀴도 같이 맞춤)
            if (harvestTray != null)
            {
                harvestTray.CompleteFlights();
            }

            if (productionCounter != null)
            {
                productionCounter.CompleteNow();
            }
        }

        private float GetEventTime(int index, float multiplier)
        {
            float cutAt = settings.UnstableSeconds * multiplier;
            float coastAt = cutAt + settings.CutSeconds * multiplier;
            switch (index)
            {
                case StutterEvent:
                    return 0f;
                case CutEvent:
                    return cutAt;
                case LeakEvent:
                    return cutAt + settings.LeakDelay * multiplier;
                default:
                    return coastAt + settings.ClankAt * multiplier;
            }
        }

        private void FireEvent(int index, float multiplier)
        {
            switch (index)
            {
                case StutterEvent:
                    if (sounds != null)
                    {
                        sounds.PlayRunFailStutter();
                    }

                    break;
                case CutEvent:
                    // 출력 쪽 빛 (공개 등급 테두리) 이 먼저 꺼짐. 표시등은 ApplyAt 이 시간차로 끔
                    if (machine != null)
                    {
                        machine.ClearGradeFrame();
                    }

                    if (sounds != null)
                    {
                        sounds.PlayRunFailBreaker();
                        sounds.StartRunFailMotor();
                    }

                    break;
                case LeakEvent:
                    if (machine != null && settings.LeakScale > 0f)
                    {
                        machine.PlayLeak(settings.LeakScale, settings.LeakSeconds * multiplier);
                    }

                    break;
                default:
                    if (sounds != null)
                    {
                        sounds.PlayRunFailClank();
                    }

                    // 기어가 뒤로 살짝 걸림 (부품 덜컹 — 흔들림 끄기면 없음)
                    if (machine != null && shakeEnabled && settings.ClankGearDegrees > 0f)
                    {
                        machine.PlayWaitTick(-settings.ClankGearDegrees, settings.ClankGearSeconds * multiplier);
                    }

                    break;
            }
        }

        // t = 연출 시작부터 실제 초 (배율이 곱해진 시간). 이 시각의 모습을 그림 — 같은 t 면 같은 모습
        private void ApplyAt(float t, float multiplier)
        {
            float unstable = settings.UnstableSeconds * multiplier;
            float cutAt = unstable;
            float coastAt = cutAt + settings.CutSeconds * multiplier;
            float coastEnd = coastAt + settings.CoastSeconds * multiplier;

            if (machine != null)
            {
                machine.SetFailPose(GearSpeedAt(t, unstable, cutAt, coastEnd), NeedleLiftAt(t, multiplier, unstable, cutAt, coastAt),
                    PistonAt(t, multiplier, coastAt), BodyOffsetAt(t, multiplier, cutAt));
            }

            for (int i = 0; i < lightStages.Length; i++)
            {
                if (lightStages[i] == null || i >= lightRestColors.Length)
                {
                    continue;
                }

                // 출력 창 테두리 다음부터 차례로 꺼짐
                float start = cutAt + settings.LightStagger * multiplier * (i + 1);
                float off = Mathf.Clamp01((t - start) / (settings.LightFadeSeconds * multiplier));
                float brightness = Mathf.Lerp(1f, settings.GetLightOffBrightness(i), OutQuad(off));
                if (flashEnabled && t < cutAt)
                {
                    // 출력 불안정: 마지막 표시등이 한두 번 짧게 어두워짐 (앞 표시등은 절반만). 화면 전체를 번쩍이지 않음
                    float u = t / unstable;
                    float blink = Mathf.Max(Window(u, 0.28f, 0.08f), Window(u, 0.62f, 0.06f));
                    float depth = (1f - settings.FlickerBrightness) * (i == lightStages.Length - 1 ? 1f : 0.5f);
                    brightness *= 1f - depth * blink;
                }

                lightStages[i].color = Scale(lightRestColors[i], brightness);
            }

            float dim = Mathf.Lerp(1f, settings.BodyDim, Smooth(Mathf.Clamp01((t - cutAt) / (coastEnd - cutAt))));
            for (int i = 0; i < dimTargets.Length; i++)
            {
                if (dimTargets[i] != null && i < dimRestColors.Length)
                {
                    dimTargets[i].color = Scale(dimRestColors[i], dim);
                }
            }
        }

        private float GearSpeedAt(float t, float unstable, float cutAt, float coastEnd)
        {
            float strain = settings.StrainGearSpeed;
            if (t < cutAt)
            {
                // 힘겹게 빨라지다가 두 번 힘이 빠짐
                float u = t / unstable;
                float baseSpeed = Mathf.Lerp(1f, strain, Smooth(Mathf.Clamp01(u / 0.25f)));
                float dip = Mathf.Min(1f, Bump(u, 0.3f, 0.18f) + Bump(u, 0.65f, 0.18f));
                return baseSpeed * (1f - settings.GearDipDepth * dip);
            }

            // 동력이 끊긴 뒤에는 그 속도에서 감속하며 멎음 (바로 얼어붙지 않음)
            float s = Mathf.Clamp01((t - cutAt) / (coastEnd - cutAt));
            return strain * (1f - s) * (1f - s);
        }

        private float NeedleLiftAt(float t, float multiplier, float unstable, float cutAt, float coastAt)
        {
            float maxLift = settings.NeedleLiftDegrees;
            float dropStart = coastAt + settings.NeedleDelay * multiplier;
            float drop = settings.NeedleDropSeconds * multiplier;
            float lift;
            if (t < cutAt)
            {
                lift = maxLift * Smooth(t / unstable);
            }
            else if (t < dropStart)
            {
                lift = maxLift;
            }
            else
            {
                // 힘이 빠진 바늘은 점점 빠르게 떨어짐
                float v = Mathf.Clamp01((t - dropStart) / drop);
                lift = maxLift * (1f - v * v);
            }

            if (!shakeEnabled)
            {
                return lift;
            }

            if (t < coastAt)
            {
                // 떨림: 불안정 동안 커지고 차단 동안 잦아듦. 주파수는 실제 초 기준 (빠름에서도 같은 떨림)
                float envelope = t < cutAt ? Smooth(t / unstable) : 1f - (t - cutAt) / (coastAt - cutAt);
                lift += settings.NeedleJitterDegrees * envelope * Mathf.Sin(2f * Mathf.PI * settings.NeedleJitterHz * t);
            }

            float landed = dropStart + drop;
            float bounce = settings.NeedleBounceSeconds * multiplier;
            if (t >= landed && t < landed + bounce)
            {
                // 무출력 자리에 닿고 한 번 위로 튄 뒤 멈춤 (멈춤 핀 아래로는 가지 않음)
                float v = (t - landed) / bounce;
                lift += settings.NeedleBounceDegrees * Mathf.Sin(Mathf.PI * v) * (1f - v);
            }

            return Mathf.Max(0f, lift);
        }

        private float PistonAt(float t, float multiplier, float coastAt)
        {
            float start = coastAt + settings.PistonDelay * multiplier;
            if (t < start)
            {
                return 0f;
            }

            float v = Mathf.Clamp01((t - start) / (settings.PistonSettleSeconds * multiplier));
            float sag = settings.PistonSagPixels * OutQuad(v);
            if (!shakeEnabled)
            {
                return sag;
            }

            // 몸통보다 늦게 · 몸통과 다른 방향으로 두 번 덜컹하고 잦아듦
            return sag + settings.PistonRattlePixels * (1f - v) * (1f - v) * Mathf.Sin(2f * Mathf.PI * 2f * v);
        }

        private float BodyOffsetAt(float t, float multiplier, float cutAt)
        {
            // 위로 튀지 않고 살짝 내려앉은 채 남음. 흔들림 끄기면 GachaMachineView 가 몸통을 제자리에 둠
            if (t < cutAt)
            {
                return 0f;
            }

            return -settings.SinkPixels * OutQuad(Mathf.Clamp01((t - cutAt) / (settings.SinkSeconds * multiplier)));
        }

        private void ApplyFinal()
        {
            // 모든 단계가 끝난 시각의 모습 = 멎은 기계 (기어 정지 · 바늘 무출력 · 피스톤 처짐 · 몸통 내려앉음 · 소등 · 어두움)
            ApplyAt(FinalTime, 1f);
            if (machine != null)
            {
                machine.ClearGradeFrame();
            }

            shutDown = true;
        }

        private void RestoreLooks()
        {
            for (int i = 0; i < lightStages.Length && i < lightRestColors.Length; i++)
            {
                if (lightStages[i] != null)
                {
                    lightStages[i].color = lightRestColors[i];
                }
            }

            for (int i = 0; i < dimTargets.Length && i < dimRestColors.Length; i++)
            {
                if (dimTargets[i] != null)
                {
                    dimTargets[i].color = dimRestColors[i];
                }
            }

            if (machine != null)
            {
                machine.ClearFailPose();
            }

            shutDown = false;
        }

        private static Color[] ReadColors(Image[] images)
        {
            Color[] colors = new Color[images.Length];
            for (int i = 0; i < images.Length; i++)
            {
                colors[i] = images[i] != null ? images[i].color : Color.white;
            }

            return colors;
        }

        private static Color Scale(Color color, float brightness)
        {
            return new Color(color.r * brightness, color.g * brightness, color.b * brightness, color.a);
        }

        // start ~ start + width 동안 1 (짧은 깜빡임)
        private static float Window(float u, float start, float width)
        {
            return u >= start && u < start + width ? 1f : 0f;
        }

        // start ~ start + width 동안 0 → 1 → 0 (힘이 빠졌다 돌아옴)
        private static float Bump(float u, float start, float width)
        {
            if (u < start || u >= start + width)
            {
                return 0f;
            }

            return Mathf.Sin(Mathf.PI * (u - start) / width);
        }

        private static float Smooth(float x)
        {
            float c = Mathf.Clamp01(x);
            return c * c * (3f - 2f * c);
        }

        private static float OutQuad(float x)
        {
            float rest = 1f - Mathf.Clamp01(x);
            return 1f - rest * rest;
        }
    }
}

using System;
using System.Collections.Generic;
using UnityEngine;

namespace Overclock.Presentation
{
    // 새 런을 시작할 때 오래된 기계의 패널판이 화면 밖에서 레일을 타고 '촤르륵' 들어와 '덜컥' 자리 잡는 등장 연출 (11/07 · v3).
    // 패널마다 한 동작으로 이어진다 (속도가 끊기거나 튀지 않음):
    //   끌려 나옴 (천천히 · 패널 끝이 화면에 드러남) → 풀려 들어옴 (가장 빠름) → 제동 → 최종 자리를 지나며 완충 장치에 닿음 (도착음)
    //   → 그 속도로 눌리며 멈춤 (가장 멀리) → 살짝 속도가 붙어 돌아와 감속하며 멎음. 반동은 한 번뿐, 판은 크기 · 회전이 바뀌지 않는다.
    // 패널은 적힌 순서대로 조금씩 겹쳐 출발한다. 마지막 패널이 도착하면 조작 안내가 나타나고, 마지막 복귀가 끝나면 끝 콜백에서 화면이 입력을 연다.
    // 시계는 Time.deltaTime 이라 일시정지 (timeScale 0) 중에는 멈춘다. 전용 소리는 SoundPlayer 의 등장 전용 소스만 쓴다.
    // 끝 콜백은 자연히 끝남 · Skip · 즉시에서 정확히 한 번. Cancel · 꺼짐은 콜백 없이 최종 모습으로만 맞춘다
    public sealed class RunIntroView : MonoBehaviour
    {
        [SerializeField] private RunIntroSettings settings;
        [Tooltip("출발 순서대로 (라운드 정보 → 확률표 → 장착 모듈)")]
        [SerializeField] private IntroPanelView[] panels;
        [Tooltip("마지막에 나타나는 조작 안내 (레버 '당기기 (Space)'). 비어 있으면 안내 단계 없음")]
        [SerializeField] private CanvasGroup guide;
        [Tooltip("비어 있으면 소리 없이 움직이기만 한다")]
        [SerializeField] private SoundPlayer sounds;
        [Tooltip("화면 전체 (Canvas). 출발 자리 (화면 밖) 를 이 크기로 계산한다. 비어 있으면 패널에 저장된 값을 쓴다")]
        [SerializeField] private RectTransform screenArea;
        [Tooltip("패널이 지나칠 때도 가리면 안 되는 것 (기계 · 출력 창 등). 이 아래 보이는 Graphic 까지의 틈으로 지나치는 거리를 줄인다")]
        [SerializeField] private RectTransform[] keepClear;

        // 끌려 나옴이 끝날 때 속도 = 그 구간 평균 속도의 이 배 (출발은 0)
        private const float PullEndSpeedRatio = 1.5f;
        // 최종 자리를 지나는 속도의 상한 (진행률 / 진행 시간). 이보다 빠르면 제동 구간이 거꾸로 가속처럼 보인다
        private const float MaxArrivalSlope = 1.5f;

        private readonly List<int> arrived = new List<int>();
        private Action finished;
        private float elapsed;
        private float multiplier;
        private bool bounceEnabled = true;
        private bool slideSoundStarted;
        private float lastArrivalTime = float.NegativeInfinity;
        private bool playing;

        public bool IsPlaying => playing;

        /// <summary>마지막 끝이 스킵이었는가.</summary>
        public bool LastFinishSkipped { get; private set; }

        /// <summary>흔들림 끄기 = 진입은 그대로, 지나침 · 되돌아옴을 생략한다 (다음 Play 부터).</summary>
        public void SetMotionOptions(bool shakeEnabled)
        {
            bounceEnabled = shakeEnabled;
        }

        /// <summary>
        /// 패널을 화면 밖 출발 자리에 두고 들어오기 시작한다.
        /// timeMultiplier 0 (즉시) 이면 움직임 · 전용 소리 없이 최종 모습으로 두고 이 호출 안에서 끝 콜백을 부른다.
        /// </summary>
        public void Play(float timeMultiplier, Action onFinished)
        {
            Cancel();
            multiplier = Mathf.Max(0f, timeMultiplier);
            finished = onFinished;
            playing = true;
            if (settings == null || multiplier <= 0f || panels == null || panels.Length == 0)
            {
                Finish(false);
                return;
            }

            for (int i = 0; i < panels.Length; i++)
            {
                if (panels[i] != null)
                {
                    panels[i].Prepare(screenArea, keepClear, settings.OffscreenMargin, settings.ClearanceMargin);
                }
            }

            elapsed = 0f;
            slideSoundStarted = false;
            lastArrivalTime = float.NegativeInfinity;
            arrived.Clear();
            ApplyAt(0f);
        }

        /// <summary>모든 패널을 정확한 최종 자리에 두고 남은 전용 소리를 멈춘 뒤 끝 콜백을 부른다.</summary>
        public void Skip()
        {
            if (playing)
            {
                Finish(true);
            }
        }

        /// <summary>콜백 없이 멈추고 최종 모습으로 맞춘다 (런 이탈 · 새 런 · 이어하기). 등장 전용 소리는 진행 여부와 상관없이 멈춘다.</summary>
        public void Cancel()
        {
            playing = false;
            finished = null;
            ShowFinal();
            if (sounds != null)
            {
                sounds.StopRunIntroSounds();
            }
        }

        /// <summary>움직임 없이 최종 모습 (정확한 최종 자리 · 안내 보임).</summary>
        public void ShowFinal()
        {
            if (panels != null)
            {
                for (int i = 0; i < panels.Length; i++)
                {
                    if (panels[i] != null)
                    {
                        panels[i].ShowFinal();
                    }
                }
            }

            if (guide != null)
            {
                guide.alpha = 1f;
            }
        }

        private void OnDisable()
        {
            // 위치 · 투명도만 바꾸므로 꺼지는 중에 맞춰도 된다. 다음 시작에 화면 밖에 남지 않게 한다
            Cancel();
        }

        private void Update()
        {
            if (!playing)
            {
                return;
            }

            float delta = Time.deltaTime;
            if (delta <= 0f)
            {
                return;
            }

            elapsed += delta;
            if (!slideSoundStarted && elapsed >= PanelStart(0))
            {
                // 레일 소리는 한 번 (패널마다 반복하지 않는다)
                slideSoundStarted = true;
                if (sounds != null)
                {
                    sounds.StartRunIntroSlide();
                }
            }

            PlayArrivals(elapsed);
            if (elapsed >= TotalSeconds())
            {
                Finish(false);
                return;
            }

            if (slideSoundStarted && sounds != null)
            {
                // 레일 소리는 마지막 패널이 완충 장치에 닿는 순간에 끝난다 (그 직전에 줄인다 · 빠름이면 줄이는 시간도 절반)
                float untilLastArrival = ArrivalTime(panels.Length - 1) - elapsed;
                if (untilLastArrival <= 0f)
                {
                    sounds.StopRunIntroSlide();
                }
                else
                {
                    sounds.SetRunIntroSlideVolumeScale(untilLastArrival / (settings.SlideSoundFadeSeconds * multiplier));
                }
            }

            ApplyAt(elapsed);
        }

        private void PlayArrivals(float time)
        {
            // 최종 자리를 지나며 완충 장치에 닿는 순간 (눌리며 멈추기 시작) 마다 '덜컥' 한 번. 앞 도착음이 아직 울리는 중이면 줄여서 겹쳐 커지지 않게 한다
            for (int i = 0; i < panels.Length; i++)
            {
                if (panels[i] == null || arrived.Contains(i) || time < ArrivalTime(i))
                {
                    continue;
                }

                arrived.Add(i);
                float scale = ArrivalTime(i) - lastArrivalTime < settings.ArrivalOverlapSeconds * multiplier ? settings.ArrivalOverlapVolume : 1f;
                lastArrivalTime = ArrivalTime(i);
                if (sounds != null)
                {
                    sounds.PlayRunIntroArrival(scale);
                }
            }
        }

        private void ApplyAt(float time)
        {
            for (int i = 0; i < panels.Length; i++)
            {
                if (panels[i] == null)
                {
                    continue;
                }

                float local = time - PanelStart(i);
                float travel = Seconds(settings.TravelSeconds);
                float over = Seconds(settings.OvershootSeconds);
                float settle = Seconds(settings.SettleSeconds);
                bool bounce = bounceEnabled && panels[i].Bounce > 0f;
                if (local <= 0f)
                {
                    panels[i].Place(0f, 0f);
                }
                else if (local < travel)
                {
                    panels[i].Place(Rail(local / travel, ArrivalSlope(i, bounce)), 0f);
                }
                else if (bounce && local < travel + over)
                {
                    // 완충 장치에 눌린다: 최종 자리를 지나던 속도에서 시작해 줄어들며 가장 멀리에서 멈춘다
                    float t = (local - travel) / over;
                    panels[i].Place(1f, 1f - (1f - t) * (1f - t));
                }
                else if (bounce && local < travel + over + settle)
                {
                    // 멈춘 자리에서 살짝 속도가 붙어 돌아오고 최종 자리에서 감속하며 멎는다 (한 번뿐 · 반대쪽으로 다시 튕기지 않는다)
                    float t = (local - travel - over) / settle;
                    panels[i].Place(1f, 1f - t * t * (3f - 2f * t));
                }
                else
                {
                    panels[i].ShowFinal();
                }
            }

            if (guide != null)
            {
                guide.alpha = Mathf.Clamp01((time - ArrivalTime(panels.Length - 1)) / Seconds(settings.GuideFadeSeconds));
            }
        }

        private float ArrivalSlope(int index, bool bounce)
        {
            // 최종 자리를 지나는 순간의 속도 (진행률 / 진행 시간). 지나침 첫 속도 (2 × 지나치는 거리 / 지나침 시간) 와 같게 해서
            // 이동과 반동이 한 동작으로 이어지게 한다. 반동이 없으면 0 = 끝에서 감속해 멈춘다
            float distance = panels[index].EntryOffset.magnitude;
            if (!bounce || distance <= 0f)
            {
                return 0f;
            }

            float slope = 2f * panels[index].Bounce * settings.TravelSeconds / (distance * settings.OvershootSeconds);
            return Mathf.Min(slope, MaxArrivalSlope);
        }

        private float Rail(float progress, float arrivalSlope)
        {
            // 두 구간을 기울기 (속도) 가 이어지게 붙인다 (3차 Hermite)
            //   끌려 나옴: 0 에서 출발해 조금씩 속도가 붙는다 (끝 속도 = 이 구간 평균의 1.5 배)
            //   풀려 들어옴 → 제동: 가운데서 가장 빠르고, 끝에서 arrivalSlope 까지 줄어든다
            float pullTime = settings.PullTimeFraction;
            float pullDistance = settings.PullDistanceFraction;
            float pullEndSlope = PullEndSpeedRatio * pullDistance / pullTime;
            if (progress < pullTime)
            {
                return Hermite(progress / pullTime, 0f, 0f, pullDistance, pullEndSlope * pullTime);
            }

            float span = 1f - pullTime;
            return Hermite((progress - pullTime) / span, pullDistance, pullEndSlope * span, 1f, arrivalSlope * span);
        }

        private static float Hermite(float t, float startValue, float startTangent, float endValue, float endTangent)
        {
            float t2 = t * t;
            float t3 = t2 * t;
            return (2f * t3 - 3f * t2 + 1f) * startValue
                + (t3 - 2f * t2 + t) * startTangent
                + (-2f * t3 + 3f * t2) * endValue
                + (t3 - t2) * endTangent;
        }

        private float Seconds(float value)
        {
            return value * multiplier;
        }

        private float PanelStart(int index)
        {
            return Seconds(settings.FirstDelaySeconds + settings.StaggerSeconds * index);
        }

        private float ArrivalTime(int index)
        {
            return PanelStart(index) + Seconds(settings.TravelSeconds);
        }

        private float PanelEnd(int index)
        {
            bool bounce = bounceEnabled && panels[index] != null && panels[index].Bounce > 0f;
            return ArrivalTime(index) + (bounce ? Seconds(settings.OvershootSeconds + settings.SettleSeconds) : 0f);
        }

        private float TotalSeconds()
        {
            float total = 0f;
            for (int i = 0; i < panels.Length; i++)
            {
                total = Mathf.Max(total, PanelEnd(i));
            }

            if (guide != null)
            {
                total = Mathf.Max(total, ArrivalTime(panels.Length - 1) + Seconds(settings.GuideFadeSeconds));
            }

            return total;
        }

        private void Finish(bool skipped)
        {
            playing = false;
            ShowFinal();
            if (sounds != null)
            {
                if (skipped)
                {
                    sounds.StopRunIntroSounds();
                }
                else
                {
                    // 자연히 끝남: 레일 소리는 이미 멈췄다 (도착음 꼬리는 남긴다) · 즉시: 소리 없음
                    sounds.StopRunIntroSlide();
                }
            }

            LastFinishSkipped = skipped;
            Action callback = finished;
            finished = null;
            callback?.Invoke();
        }
    }
}

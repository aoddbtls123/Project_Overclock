using System.Collections.Generic;
using Overclock.Core;
using UnityEngine;

namespace Overclock.Presentation
{
    // 효과음 재생. 씬에 하나 두고 GrayBoxRunScreen · DrawRevealView 가 [SerializeField] 로 직접 부른다.
    // 소스 3개 (Awake 에서 만든다): 효과음 (PlayOneShot · 예약) / SR 충전 (중간에 멈춰야 해서 따로) / UI (일시정지 중에도 들리게 ignoreListenerPause).
    // 무작위 변형은 이 클래스의 난수로 고른다. 게임 난수(RandomService)를 쓰면 같은 시드의 판이 달라진다.
    // 예약 소리의 시계는 Time.deltaTime 이라 일시정지(timeScale 0) 중에는 흐르지 않는다.
    // 순서는 시작 시각으로만 맞춘다. 앞 소리가 끝나기를 기다리지 않으므로 꼬리는 겹친다 (끝을 기다리면 SR 잔향 3초 동안 돌파음이 밀린다 · 10/29 결정)
    public sealed class SoundPlayer : MonoBehaviour
    {
        [SerializeField] private GameSoundSet soundSet;

        private readonly List<ScheduledSound> scheduled = new List<ScheduledSound>();
        private readonly int[] lastRevealIndex = { -1, -1, -1 };
        private readonly System.Random random = new System.Random();
        private AudioSource effectsSource;
        private AudioSource chargeSource;
        private AudioSource uiSource;
        private float clock;
        private bool soundEnabled = true;

        private bool CanPlay => soundEnabled && soundSet != null;

        private void Awake()
        {
            effectsSource = CreateSource(false);
            chargeSource = CreateSource(false);
            uiSource = CreateSource(true);
        }

        private void OnDestroy()
        {
            // 일시정지 중에 씬이 내려가도 소리가 멈춘 채 남지 않게 한다 (AudioListener.pause 는 전역 값)
            AudioListener.pause = false;
        }

        private void Update()
        {
            clock += Time.deltaTime;

            int index = 0;
            while (index < scheduled.Count)
            {
                if (scheduled[index].PlayTime <= clock)
                {
                    AudioClip clip = scheduled[index].Clip;
                    scheduled.RemoveAt(index);
                    PlayEffect(clip);
                }
                else
                {
                    index++;
                }
            }
        }

        /// <summary>효과음 설정. 끄면 지금 나는 소리 · 예약 · 충전음을 모두 멈춘다.</summary>
        public void SetSoundEnabled(bool enabled)
        {
            soundEnabled = enabled;
            if (!enabled)
            {
                StopRunSounds();
                uiSource.Stop();
            }
        }

        /// <summary>일시정지 창. 게임 소리는 멈추고 UI 소리만 남긴다.</summary>
        public void SetPaused(bool paused)
        {
            AudioListener.pause = paused;
        }

        /// <summary>런을 떠나거나 새 런을 시작할 때. 예약 · 충전음 · 지금 나는 효과음을 멈춘다 (UI 소리는 그대로).</summary>
        public void StopRunSounds()
        {
            scheduled.Clear();
            chargeSource.Stop();
            effectsSource.Stop();
        }

        public void PlayDrawLever()
        {
            if (CanPlay)
            {
                PlayEffect(soundSet.DrawLever);
            }
        }

        public void PlayRatchetStop()
        {
            if (CanPlay)
            {
                PlayEffect(soundSet.RatchetStop);
            }
        }

        public void PlayTrayLand()
        {
            if (CanPlay)
            {
                PlayEffect(soundSet.TrayLand);
            }
        }

        public void StartCharge()
        {
            if (!CanPlay || soundSet.SuperRareCharge == null)
            {
                return;
            }

            chargeSource.clip = soundSet.SuperRareCharge;
            chargeSource.volume = soundSet.ChargeVolume;
            chargeSource.Play();
        }

        public void StopCharge()
        {
            // 씬을 내릴 때 DrawRevealView.OnDisable 이 부를 수 있다. 소스가 이미 없으면 할 일이 없다
            if (chargeSource != null)
            {
                chargeSource.Stop();
            }
        }

        /// <summary>등급 공개음은 바로, 3연속이면 그 뒤에 3연속 소리를 순서대로 예약한다.</summary>
        public void PlayReveal(Grade grade, bool streakTriggered)
        {
            if (!CanPlay)
            {
                return;
            }

            PlayEffect(PickReveal(grade));

            AudioClip[] hits = soundSet.StreakHits;
            if (streakTriggered && hits != null)
            {
                for (int i = 0; i < hits.Length; i++)
                {
                    Schedule(clock + soundSet.StreakDelay + soundSet.StreakInterval * i, hits[i], false);
                }
            }
        }

        public void PlayBreakthrough()
        {
            if (CanPlay)
            {
                ScheduleAfterLast(soundSet.BreakthroughDelay, soundSet.Breakthrough, true);
            }
        }

        public void PlayCardAppear()
        {
            if (CanPlay)
            {
                ScheduleAfterLast(soundSet.CardAppearDelay, soundSet.CardAppear, true);
            }
        }

        public void PlayRunFailed()
        {
            if (CanPlay)
            {
                ScheduleAfterLast(soundSet.RunEndDelay, soundSet.RunFailed, false);
            }
        }

        public void PlayModuleEquip()
        {
            if (!CanPlay)
            {
                return;
            }

            // 카드를 빨리 고르면 아직 나지 않은 돌파음 · 카드 출현음이 장착음 뒤에 나지 않게 지운다.
            // 3연속 소리는 이미 끝난 뽑기의 결과라 남긴다 (교차검토 #30)
            scheduled.RemoveAll(sound => sound.CancelOnEquip);
            PlayEffect(soundSet.ModuleEquip);
        }

        public void PlayUiClick()
        {
            if (CanPlay)
            {
                PlayUi(soundSet.UiClick);
            }
        }

        public void PlayUiOpen()
        {
            if (CanPlay)
            {
                PlayUi(soundSet.UiOpen);
            }
        }

        /// <summary>
        /// 직전에 고른 번호를 빼고 고른다. 처음(직전 없음)이거나 하나뿐이면 아무거나.
        /// </summary>
        internal static int PickIndex(int count, int previous, System.Random random)
        {
            if (count <= 1)
            {
                return 0;
            }

            if (previous < 0 || previous >= count)
            {
                return random.Next(count);
            }

            int index = random.Next(count - 1);
            return index >= previous ? index + 1 : index;
        }

        private AudioClip PickReveal(Grade grade)
        {
            AudioClip[] clips = soundSet.GetReveal(grade);
            if (clips == null || clips.Length == 0)
            {
                return null;
            }

            int index = PickIndex(clips.Length, lastRevealIndex[(int)grade], random);
            lastRevealIndex[(int)grade] = index;
            return clips[index];
        }

        private void ScheduleAfterLast(float delay, AudioClip clip, bool cancelOnEquip)
        {
            // 예약된 소리 중 시작이 가장 늦은 것의 시작 시각 + delay 에 둔다 (끝나기를 기다리지 않음).
            // 돌파하는 뽑기에서 시작 순서가 공개음 → 3연속 → 돌파 → 카드 출현이 된다
            float start = clock;
            for (int i = 0; i < scheduled.Count; i++)
            {
                start = Mathf.Max(start, scheduled[i].PlayTime);
            }

            Schedule(start + delay, clip, cancelOnEquip);
        }

        private void Schedule(float time, AudioClip clip, bool cancelOnEquip)
        {
            // 클립이 비어 있어도 자리는 잡는다. 빠진 소리가 있어도 다른 소리의 타이밍은 같다
            scheduled.Add(new ScheduledSound(time, clip, cancelOnEquip));
        }

        private void PlayEffect(AudioClip clip)
        {
            // 새로 클론한 저장소에는 구매 음원이 없다. 빈 칸은 소리만 나지 않는다.
            // 켬/끔은 부르는 쪽(Play~)이 본다. 끄면 예약이 지워지므로 Update 에서 오는 소리도 켬 상태다
            if (clip != null)
            {
                effectsSource.PlayOneShot(clip, soundSet.EffectsVolume);
            }
        }

        private void PlayUi(AudioClip clip)
        {
            if (clip != null)
            {
                uiSource.PlayOneShot(clip, soundSet.UiVolume);
            }
        }

        private AudioSource CreateSource(bool ignoreListenerPause)
        {
            AudioSource source = gameObject.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.ignoreListenerPause = ignoreListenerPause;
            return source;
        }

        private readonly struct ScheduledSound
        {
            public ScheduledSound(float time, AudioClip clip, bool cancelOnEquip)
            {
                PlayTime = time;
                Clip = clip;
                CancelOnEquip = cancelOnEquip;
            }

            public float PlayTime { get; }
            public AudioClip Clip { get; }

            /// <summary>모듈을 장착하면 지울 소리 (돌파 · 카드 출현). 3연속 · 런 실패는 false.</summary>
            public bool CancelOnEquip { get; }
        }
    }
}

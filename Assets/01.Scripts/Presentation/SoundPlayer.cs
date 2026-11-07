using System;
using System.Collections.Generic;
using Overclock.Core;
using UnityEngine;

namespace Overclock.Presentation
{
    // 효과음 재생. 씬에 하나 두고 GrayBoxRunScreen · DrawRevealView 가 [SerializeField] 로 직접 부른다.
    // 소스 3개 (Awake 에서 만든다): 효과음 (PlayOneShot · 예약) / SR 충전 (중간에 멈춰야 해서 따로) / UI (일시정지 중에도 들리게 ignoreListenerPause).
    // 런 실패 연출 (11/07) 은 소스 2개를 따로 쓴다 (짧은 소리 · 모터) — 스킵하면 그 연출의 소리만 멈출 수 있게. 크기는 효과음 볼륨을 따른다.
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
        private AudioSource previewSource;
        private AudioSource failSource;
        private AudioSource failMotorSource;
        private float failMotorVolumeScale = 1f;
        private float clock;
        private float lastPieceLandTime = float.NegativeInfinity;
        private float effectsVolume = 1f;
        private float uiVolume = 1f;
        private float musicVolume = 1f;
        private float chargeVolumeScale = 1f;
        private SoundChannel previewChannel = SoundChannel.Ui;

        private bool CanPlay => soundSet != null;

        /// <summary>BGM 실제 크기 (전체 × BGM). 아직 BGM 재생이 없어 값만 둔다 — BGM 을 넣으면 이 값을 쓴다.</summary>
        public float MusicVolume => musicVolume;

        private void Awake()
        {
            effectsSource = CreateSource(false);
            chargeSource = CreateSource(false);
            uiSource = CreateSource(true);
            previewSource = CreateSource(true);
            failSource = CreateSource(false);
            failMotorSource = CreateSource(false);
            ApplySourceVolumes();
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

        /// <summary>
        /// 볼륨 설정 (11/04 · 효과음 켬/끔 대신). 실제 크기 = 전체 × 종류. 소스 볼륨으로 걸어서 지금 나는 소리 · 충전음에도 바로 반영된다.
        /// 0 이어도 재생 · 예약은 그대로 한다 (들리지만 않음) — 다시 올리면 박자가 어긋나지 않게
        /// </summary>
        public void ApplyVolumes(GameSettings settings)
        {
            if (settings == null)
            {
                throw new ArgumentNullException(nameof(settings));
            }

            effectsVolume = settings.GetEffectiveVolume(SoundChannel.Effects);
            uiVolume = settings.GetEffectiveVolume(SoundChannel.Ui);
            musicVolume = settings.GetEffectiveVolume(SoundChannel.Music);
            ApplySourceVolumes();
        }

        /// <summary>
        /// 볼륨 슬라이더를 놓았을 때 그 크기로 한 번 들려준다. 일시정지 중에도 들리는 소스로 낸다.
        /// 효과음 = 레버 소리 · 전체 · UI = 버튼 소리 · BGM = 아직 없음
        /// </summary>
        public void PlayPreview(SoundChannel channel)
        {
            if (!CanPlay || previewSource == null)
            {
                return;
            }

            AudioClip clip;
            float clipVolume;
            switch (channel)
            {
                case SoundChannel.Effects:
                    clip = soundSet.DrawLever;
                    clipVolume = soundSet.EffectsVolume;
                    previewChannel = SoundChannel.Effects;
                    break;
                case SoundChannel.Music:
                    return;
                default:
                    clip = soundSet.UiClick;
                    clipVolume = soundSet.UiVolume;
                    previewChannel = SoundChannel.Ui;
                    break;
            }

            // 크기는 소스 볼륨으로 건다 — 들려주는 도중에 슬라이더를 다시 움직여도 바로 따라간다 (교차검토 #36)
            ApplySourceVolumes();
            if (clip != null)
            {
                previewSource.PlayOneShot(clip, clipVolume);
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
            StopRunFailSounds();
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

        public void PlayShutterClose()
        {
            if (CanPlay)
            {
                PlayEffect(soundSet.ShutterClose);
            }
        }

        public void PlayShutterOpen(Grade grade)
        {
            if (CanPlay)
            {
                PlayEffect(soundSet.GetShutterOpen(grade));
            }
        }

        public void PlayRatchetTick()
        {
            if (CanPlay)
            {
                PlayEffect(soundSet.RatchetTick);
            }
        }

        /// <summary>조각 착지음. 간격(PieceLandInterval)보다 촘촘하면 건너뛴다.</summary>
        public void PlayPieceLand()
        {
            if (!CanPlay || clock - lastPieceLandTime < soundSet.PieceLandInterval)
            {
                return;
            }

            lastPieceLandTime = clock;
            PlayEffect(soundSet.PieceLand);
        }

        public void StartCharge()
        {
            // 새 충전은 늘 제 크기로 시작한다 (클리어 연출이 줄여 둔 크기가 다음 SR 에 남지 않게)
            chargeVolumeScale = 1f;
            if (!CanPlay || soundSet.SuperRareCharge == null)
            {
                return;
            }

            chargeSource.clip = soundSet.SuperRareCharge;
            ApplySourceVolumes();
            chargeSource.Play();
        }

        /// <summary>충전음 크기 배율 (클리어 기 모으기에서 점점 키우고 버틸 때 눌러 줌 · 11/06). StartCharge 가 1 로 되돌린다.</summary>
        public void SetChargeVolumeScale(float scale)
        {
            chargeVolumeScale = Mathf.Clamp01(scale);
            ApplySourceVolumes();
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

        /// <summary>돌파음을 예약 없이 바로 낸다 (클리어 연출 · 11/06). 시각은 부르는 쪽이 잡는다 —
        /// 예약에 남지 않으니 연출을 건너뛰면 뒤늦게 나지 않고, 빠름 배율도 부르는 쪽 시간에 그대로 따른다.</summary>
        public void PlayBreakthroughNow()
        {
            if (CanPlay)
            {
                PlayEffect(soundSet.Breakthrough);
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

        /// <summary>런 실패 연출 A — 출력 불안정 (짧은 버벅임). 빈 칸이면 소리 없음.</summary>
        public void PlayRunFailStutter()
        {
            if (CanPlay)
            {
                PlayRunFail(soundSet.RunFailStutter);
            }
        }

        /// <summary>런 실패 연출 B — 동력 차단 (차단기 하나).</summary>
        public void PlayRunFailBreaker()
        {
            if (CanPlay)
            {
                PlayRunFail(soundSet.RunFailBreaker);
            }
        }

        /// <summary>런 실패 연출 C 끝 — 멎으며 걸리는 작은 금속 소리.</summary>
        public void PlayRunFailClank()
        {
            if (CanPlay)
            {
                PlayRunFail(soundSet.RunFailClank);
            }
        }

        /// <summary>런 실패 연출 — 동력이 끊긴 뒤 모터가 멎는 소리 (런 실패 칸). 크기는 SetRunFailMotorVolumeScale 로 줄여 간다.</summary>
        public void StartRunFailMotor()
        {
            failMotorVolumeScale = 1f;
            if (!CanPlay || soundSet.RunFailed == null || failMotorSource == null)
            {
                return;
            }

            failMotorSource.clip = soundSet.RunFailed;
            ApplySourceVolumes();
            failMotorSource.Play();
        }

        /// <summary>모터 소리 크기 배율 (0~1). StartRunFailMotor 가 1 로 되돌린다.</summary>
        public void SetRunFailMotorVolumeScale(float scale)
        {
            failMotorVolumeScale = Mathf.Clamp01(scale);
            ApplySourceVolumes();
        }

        /// <summary>모터 소리만 멈춘다 (연출이 끝났을 때 · 짧은 소리의 꼬리는 남긴다).</summary>
        public void StopRunFailMotor()
        {
            if (failMotorSource != null)
            {
                failMotorSource.Stop();
            }
        }

        /// <summary>런 실패 연출의 소리를 모두 멈춘다 (스킵 · 런 이탈). 다른 효과음 · 예약은 그대로.</summary>
        public void StopRunFailSounds()
        {
            // 씬을 내릴 때 RunFailView.OnDisable 이 부를 수 있다. 소스가 이미 없으면 할 일이 없다
            if (failSource != null)
            {
                failSource.Stop();
            }

            StopRunFailMotor();
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

        public void PlayOfferLatch()
        {
            // 다시 받기 회전 끝 (자연히 · 스킵 · 즉시). 예약하지 않고 바로 낸다 — 런 이탈로 회전이 취소되면 부르지 않는다
            if (CanPlay && soundSet.OfferLatch != null)
            {
                effectsSource.PlayOneShot(soundSet.OfferLatch, soundSet.EffectsVolume * soundSet.OfferLatchVolume);
            }
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

        private void PlayRunFail(AudioClip clip)
        {
            if (clip != null && failSource != null)
            {
                failSource.PlayOneShot(clip, soundSet.EffectsVolume);
            }
        }

        private void PlayUi(AudioClip clip)
        {
            if (clip != null)
            {
                uiSource.PlayOneShot(clip, soundSet.UiVolume);
            }
        }

        private void ApplySourceVolumes()
        {
            // Awake 전에 불려도 값은 남기고, 소스가 생기면 Awake 가 다시 건다
            if (effectsSource == null)
            {
                return;
            }

            effectsSource.volume = effectsVolume;
            chargeSource.volume = effectsVolume * (soundSet != null ? soundSet.ChargeVolume : 1f) * chargeVolumeScale;
            uiSource.volume = uiVolume;
            previewSource.volume = previewChannel == SoundChannel.Effects ? effectsVolume : uiVolume;
            failSource.volume = effectsVolume;
            failMotorSource.volume = effectsVolume * (soundSet != null ? soundSet.EffectsVolume : 1f) * failMotorVolumeScale;
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

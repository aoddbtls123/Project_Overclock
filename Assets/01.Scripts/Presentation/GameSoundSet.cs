using Overclock.Core;
using UnityEngine;

namespace Overclock.Presentation
{
    // 상황별 효과음 클립 · 음량 · 소리 사이 간격. 재생은 SoundPlayer 가 한다.
    // 구매 음원(Assets/05.Audio/Licensed)은 저장소에 없다. 새로 클론하면 그 칸이 비고, 그 소리만 나지 않는다
    [CreateAssetMenu(fileName = "GameSoundSet", menuName = "Overclock/Game Sound Set")]
    public sealed class GameSoundSet : ScriptableObject
    {
        [Header("뽑기")]
        [Tooltip("뽑기가 받아들여졌을 때만 (버튼 · Space). 기계가 있으면 레버가 바닥에 닿는 순간")]
        [SerializeField] private AudioClip drawLever;
        [Tooltip("여러 개면 무작위 (직전과 같은 것 제외)")]
        [SerializeField] private AudioClip[] normalReveal;
        [Tooltip("여러 개면 무작위 (직전과 같은 것 제외)")]
        [SerializeField] private AudioClip[] rareReveal;
        [Tooltip("여러 개면 무작위 (직전과 같은 것 제외)")]
        [SerializeField] private AudioClip[] superRareReveal;
        [Tooltip("SR 대기 시작부터 공개 순간까지. 공개 · 스킵되면 멈춘다. 연출 속도 '즉시' 면 시작하지 않는다")]
        [SerializeField] private AudioClip superRareCharge;

        [Header("기계 타격감 (11/04) — 멈추는 순간에 난다")]
        [Tooltip("공개 반응의 래칫 한 칸이 딱 멈출 때 (스킵 · 즉시면 없음)")]
        [SerializeField] private AudioClip ratchetStop;
        [Tooltip("수확물이 트레이에 앉을 때 (스킵 · 즉시면 없음)")]
        [SerializeField] private AudioClip trayLand;

        [Header("3연속")]
        [Tooltip("적힌 순서대로 한 번씩 (철컥 · 철컥 · 쿵)")]
        [SerializeField] private AudioClip[] streakHits;
        [Tooltip("공개음이 시작된 뒤 첫 소리가 시작될 때까지 (초)")]
        [SerializeField, Min(0f)] private float streakDelay = 0.15f;
        [Tooltip("3연속 소리의 시작 간격 (초). 클립보다 짧으면 겹치며 이어진다")]
        [SerializeField, Min(0f)] private float streakInterval = 0.18f;

        [Header("돌파")]
        [SerializeField] private AudioClip breakthrough;
        [Tooltip("공개음이 시작된 뒤 (3연속이 있으면 마지막 소리가 시작된 뒤) (초). 앞 소리가 끝나기를 기다리지 않는다")]
        [SerializeField, Min(0f)] private float breakthroughDelay = 0.1f;
        [Tooltip("카드 패널이 열릴 때")]
        [SerializeField] private AudioClip cardAppear;
        [Tooltip("돌파 성공음이 시작된 뒤 (초). 이어하기로 카드가 열리면 그 순간부터")]
        [SerializeField, Min(0f)] private float cardAppearDelay = 0.35f;
        [Tooltip("카드를 골라 장착할 때")]
        [SerializeField] private AudioClip moduleEquip;

        [Header("런 끝")]
        [SerializeField] private AudioClip runFailed;
        [Tooltip("마지막 공개음이 시작된 뒤 (3연속이 있으면 마지막 소리가 시작된 뒤) (초). 앞 소리가 끝나기를 기다리지 않는다")]
        [SerializeField, Min(0f)] private float runEndDelay = 0.2f;
        [Tooltip("자리만 — 아직 재생 코드 없음")]
        [SerializeField] private AudioClip runCleared;

        [Header("UI")]
        [Tooltip("일반 버튼 (뽑기 버튼 · 카드 선택은 자기 소리)")]
        [SerializeField] private AudioClip uiClick;
        [Tooltip("일시정지 창 · 설정 창 · 모드 선택 패널이 열릴 때")]
        [SerializeField] private AudioClip uiOpen;

        [Header("음량")]
        [SerializeField, Range(0f, 1f)] private float effectsVolume = 1f;
        [SerializeField, Range(0f, 1f)] private float chargeVolume = 1f;
        [SerializeField, Range(0f, 1f)] private float uiVolume = 1f;

        [Header("자리만 — 아직 재생 코드 없음")]
        [SerializeField] private AudioClip[] electricHum;
        [SerializeField] private AudioClip factoryAmbience;
        [SerializeField] private AudioClip music;

        public AudioClip DrawLever => drawLever;
        public AudioClip SuperRareCharge => superRareCharge;
        public AudioClip RatchetStop => ratchetStop;
        public AudioClip TrayLand => trayLand;
        public AudioClip[] StreakHits => streakHits;
        public float StreakDelay => streakDelay;
        public float StreakInterval => streakInterval;
        public AudioClip Breakthrough => breakthrough;
        public float BreakthroughDelay => breakthroughDelay;
        public AudioClip CardAppear => cardAppear;
        public float CardAppearDelay => cardAppearDelay;
        public AudioClip ModuleEquip => moduleEquip;
        public AudioClip RunFailed => runFailed;
        public float RunEndDelay => runEndDelay;
        public AudioClip UiClick => uiClick;
        public AudioClip UiOpen => uiOpen;
        public float EffectsVolume => effectsVolume;
        public float ChargeVolume => chargeVolume;
        public float UiVolume => uiVolume;

        // 자리만 잡아 둔 칸. 아직 재생하는 곳이 없다 (런 클리어 · 전자기 험 · 공장 앰비언스 · 배경 음악)
        public AudioClip RunCleared => runCleared;
        public AudioClip[] ElectricHum => electricHum;
        public AudioClip FactoryAmbience => factoryAmbience;
        public AudioClip Music => music;

        public AudioClip[] GetReveal(Grade grade)
        {
            switch (grade)
            {
                case Grade.R:
                    return rareReveal;
                case Grade.SR:
                    return superRareReveal;
                default:
                    return normalReveal;
            }
        }
    }
}

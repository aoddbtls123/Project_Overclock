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
    // 쉬는 상태 = 열 0 · 피스톤 · 바늘 제자리 · 증기 투명 (기어는 천천히 계속 돈다). 레버는 따로 — 아래 StopReactions 참고
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

        private float gearAngle;
        private float ratchetOffset;
        private float heat;
        private float needleHeat;
        private float pistonHeatOffset;
        private float pistonKick;
        private float leverAngle;
        private Vector2 restPistonPosition;
        private Sequence revealSequence;
        private Sequence leverSequence;
        private Tween[] puffTweens;
        private Vector2[] puffStartPositions;
        private int nextPuff;

        private void Awake()
        {
            if (piston != null)
            {
                restPistonPosition = piston.anchoredPosition;
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

            ApplyPose();
        }

        private void Update()
        {
            // 기어는 늘 돈다. 열이 오를수록 빨라진다 (처음엔 천천히, 끝에 갈수록 빠르게)
            float speed = Mathf.Lerp(settings.IdleGearSpeed, settings.HeatGearSpeed, heat * heat);
            // 값이 한없이 커지지 않게 되돌린다. 360 × 3600 이면 배율이 소수 둘째 자리까지일 때 되돌리는 순간에도 각도가 튀지 않는다
            gearAngle = Mathf.Repeat(gearAngle + speed * Time.deltaTime, 360f * 3600f);
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
            KillPuffs();
        }

        /// <summary>뽑기가 받아들여졌을 때 레버 손잡이를 내렸다 올린다. 연출 · 스킵과 관계없이 끝까지 움직인다.</summary>
        public void PullLever()
        {
            if (leverHandle == null)
            {
                return;
            }

            KillLever();
            leverSequence = DOTween.Sequence();
            leverSequence.Append(DOTween.To(() => leverAngle, SetLeverAngle, settings.LeverPulledAngle, settings.LeverDownSeconds).SetEase(Ease.OutQuad));
            leverSequence.Append(DOTween.To(() => leverAngle, SetLeverAngle, 0f, settings.LeverUpSeconds).SetEase(Ease.OutBack));
            leverSequence.OnComplete(() => leverSequence = null);
        }

        /// <summary>SR 기다림 진행률(0~1)에 맞춰 기어를 가속하고 피스톤을 연타하고 바늘을 올린다. 같은 진행률이면 피스톤 · 바늘은 같은 모습이다.</summary>
        public void SetHeat(float progress)
        {
            float clamped = Mathf.Clamp01(progress);
            heat = clamped;
            needleHeat = clamped;

            // 진행률을 시계로 쓰는 오르내림 — 스킵 · 일시정지와 늘 맞는다. 열이 오를수록 깊게 내려간다
            float wave = 0.5f - 0.5f * Mathf.Cos(2f * Mathf.PI * clamped * settings.HeatPistonCycles);
            pistonHeatOffset = settings.PistonTravel * wave * clamped;
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

            revealSequence = DOTween.Sequence();
            revealSequence.Insert(0f, DOTween.To(() => ratchetOffset, SetRatchetOffset, ratchetOffset + settings.RatchetAngle, settings.RatchetSeconds).SetEase(Ease.OutBack));

            if (grade != Grade.N)
            {
                float half = settings.PistonSeconds * 0.5f;
                revealSequence.Insert(0f, DOTween.To(() => pistonKick, SetPistonKick, settings.PistonTravel, half).SetEase(Ease.OutQuad));
                revealSequence.Insert(half, DOTween.To(() => pistonKick, SetPistonKick, 0f, half).SetEase(Ease.InQuad));
            }

            if (grade == Grade.R)
            {
                Puff(0, settings.SmallSteamScale, 0f);
            }
            else if (grade == Grade.SR)
            {
                revealSequence.Insert(0f, DOTween.To(() => needleHeat, SetNeedleHeat, 0f, settings.NeedleReturnSeconds).SetEase(Ease.OutQuad));
                for (int i = 0; i < settings.SuperRareSteamPuffs; i++)
                {
                    Puff(i, settings.BigSteamScale, 0f);
                }
            }

            revealSequence.OnComplete(() => revealSequence = null);
        }

        /// <summary>할당량 돌파 — 긴 증기 배출. 스킵 · 즉시여도 낸다 (10/30 결정: 공개가 아니라 라운드 사건).</summary>
        public void PlayBreakthrough()
        {
            for (int i = 0; i < settings.BreakthroughSteamPuffs; i++)
            {
                Puff(i, settings.BigSteamScale, i * settings.BreakthroughSteamInterval);
            }
        }

        /// <summary>
        /// 기다림 · 공개 · 돌파 반응을 멈추고 쉬는 상태로 돌린다 (새 연출 · 스킵 · Cancel · 모듈 선택 뒤).
        /// 레버는 건드리지 않는다 — 클릭 순서가 PullLever → Draw → Play(여기를 부름) 라서 레버까지 멈추면 당기자마자 끊긴다.
        /// </summary>
        public void StopReactions()
        {
            KillReveal();
            KillPuffs();
            heat = 0f;
            needleHeat = 0f;
            pistonHeatOffset = 0f;
            pistonKick = 0f;
            ApplyPose();
        }

        /// <summary>레버까지 포함해 모두 쉬는 상태로 (런 이탈 · 비활성화).</summary>
        public void ResetAll()
        {
            KillLever();
            leverAngle = 0f;
            StopReactions();
        }

        private void Puff(int index, float endScale, float delay)
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

            puffTweens[slot] = DOTween.To(t => ApplyPuff(slot, endScale, t), 0f, 1f, settings.SteamSeconds)
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
                piston.anchoredPosition = restPistonPosition + new Vector2(0f, -(pistonHeatOffset + pistonKick));
            }

            if (needle != null)
            {
                needle.localEulerAngles = new Vector3(0f, 0f, Mathf.Lerp(settings.NeedleRestAngle, settings.NeedleMaxAngle, needleHeat));
            }

            if (leverHandle != null)
            {
                leverHandle.localEulerAngles = new Vector3(0f, 0f, leverAngle);
            }
        }

        private void SetLeverAngle(float value)
        {
            leverAngle = value;
            ApplyPose();
        }

        private void SetRatchetOffset(float value)
        {
            ratchetOffset = value;
        }

        private void SetPistonKick(float value)
        {
            pistonKick = value;
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
        }

        private void KillLever()
        {
            if (leverSequence != null && leverSequence.IsActive())
            {
                leverSequence.Kill();
            }

            leverSequence = null;
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

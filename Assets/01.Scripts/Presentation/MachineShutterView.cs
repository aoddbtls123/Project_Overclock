using System;
using DG.Tweening;
using Overclock.Core;
using UnityEngine;
using UnityEngine.UI;

namespace Overclock.Presentation
{
    // 출력 창 셔터 (기계연출 구상 §14-5 "철 · 커 · 착"). 뽑기를 기다리는 동안 출력 창을 가리고 공개 순간에 열린다.
    // 언제 닫고 열지는 DrawRevealView 가 정한다 (뜸들이기 시간 안에서만 움직이고 시간을 늘리지 않는다). 게임 상태는 바꾸지 않는다.
    // 판(plate)은 창 크기이고, 부모의 마스크(RectMask2D)가 창 밖을 가린다 — 열림 = 판이 창 높이만큼 위로 · 닫힘 = 창 자리.
    // 시간은 scaled time (일시정지면 멈춤). 떨림 · 틈새 증기 자리는 값으로만 정한다 (난수 없음)
    // 철판 (11/16): 판 안의 철판들은 떨어지는 동안 사이가 벌어져 있다가 닿는 순간부터 아래부터 차례로 포개지고, 열 때는 위부터 벌어진다.
    // 철판이 없으면 판 한 장으로 움직인다 (이전과 같음)
    public sealed class MachineShutterView : MonoBehaviour
    {
        [SerializeField] private ShutterSettings settings;
        [Tooltip("셔터 판. 창과 같은 크기 · 가운데 피벗 · 마스크 안 자식")]
        [SerializeField] private RectTransform plate;
        [Tooltip("셔터 아래 틈에서 새는 증기 이미지 풀 (마스크 밖 · 이 오브젝트의 자식). 차례로 돌려 쓴다")]
        [SerializeField] private Image[] gapPuffs = Array.Empty<Image>();
        [Tooltip("판 안의 철판 (위 → 아래 순). 판 크기로 늘린 이미지 · 위치는 코드가 정한다")]
        [SerializeField] private RectTransform[] slats = Array.Empty<RectTransform>();

        /// <summary>셔터가 끝까지 내려와 닫힌 순간 (닫힘 소리 · 덜컹).</summary>
        public event Action Closed;

        private float closedAmount;
        private float jitter;
        private float plateHeight;
        private Tween moveTween;
        private Tween settleTween;
        private Vector2[] slatRestPositions = Array.Empty<Vector2>();
        // 철판 사이 벌어짐 진행: opening = 열 때 (0 → 1 위부터 벌어짐) · 아니면 포개짐 (0 = 다 벌어짐 → 1 = 다 포개짐)
        private bool slatsOpening;
        private float slatProgress = 1f;
        private Tween[] puffTweens = Array.Empty<Tween>();
        private int nextPuff;

        public ShutterSettings Settings => settings;

        /// <summary>0 = 열림 · 1 = 닫힘.</summary>
        public float ClosedAmount => closedAmount;

        public bool IsClosed => closedAmount >= 1f;

        /// <summary>닫히거나 열리는 중인가.</summary>
        public bool IsMoving => moveTween != null;

        private void Awake()
        {
            plateHeight = plate != null ? plate.sizeDelta.y : 0f;
            slatRestPositions = new Vector2[slats.Length];
            for (int i = 0; i < slats.Length; i++)
            {
                slatRestPositions[i] = slats[i] != null ? slats[i].anchoredPosition : Vector2.zero;
            }
            puffTweens = new Tween[gapPuffs.Length];
            for (int i = 0; i < gapPuffs.Length; i++)
            {
                if (gapPuffs[i] != null)
                {
                    gapPuffs[i].raycastTarget = false;
                    HidePuff(i);
                }
            }

            Apply();
        }

        private void OnDisable()
        {
            ShowOpen();
        }

        private void OnDestroy()
        {
            KillMove();
            KillPuffs();
        }

        /// <summary>delay 초 뒤 seconds 동안 가속하며 내려와 쾅 닫힌다 (닫히는 순간 Closed). seconds 0 이면 바로 닫힘.</summary>
        public void Close(float delay, float seconds)
        {
            KillMove();
            jitter = 0f;
            Apply();
            float start = closedAmount;
            if (seconds <= 0f && delay <= 0f)
            {
                SetSlats(false, 1f);
                SetClosed(1f);
                Closed?.Invoke();
                return;
            }

            // 떨어지는 동안은 철판 사이가 벌어진 채 (닿은 뒤 포개지는 시간은 닫히는 시간과 같은 배율)
            SetSlats(false, 0f);
            float settle = settings.SlatSettleSeconds * Mathf.Max(seconds, 0.0001f) / Mathf.Max(settings.CloseSeconds, 0.0001f);
            moveTween = DOTween.To(t => SetClosed(start + (1f - start) * InCubic(t)), 0f, 1f, Mathf.Max(seconds, 0.0001f))
                .SetDelay(delay)
                .SetEase(Ease.Linear)
                .OnComplete(() =>
                {
                    moveTween = null;
                    SetClosed(1f);
                    Closed?.Invoke();
                    settleTween = DOTween.To(p =>
                        {
                            SetSlats(false, p);
                            Apply();
                        }, 0f, 1f, settle)
                        .SetEase(Ease.Linear)
                        .OnComplete(() => settleTween = null);
                });
        }

        /// <summary>
        /// 공개 순간 (끝까지 본 공개만). N · R = 빠르게 열림 · SR = 튕겨 올라가 위에 부딪히고 살짝 내려왔다 다시 올라감.
        /// 닫히는 도중이면 그 자리에서 연다. timeScale = 연출 속도 배율 (빠름 0.5 = 반 시간).
        /// </summary>
        public void Open(Grade grade, float timeScale = 1f)
        {
            float scale = Mathf.Max(timeScale, 0.0001f);
            KillMove();
            jitter = 0f;
            Apply();
            float start = closedAmount;
            float lead = settings.SlatOpenLead;
            if (grade != Grade.SR)
            {
                moveTween = DOTween.To(t =>
                    {
                        SetSlats(true, Mathf.Clamp01(t / lead));
                        SetClosed(start * (1f - OutQuad(t)));
                    }, 0f, 1f, settings.OpenSeconds * scale)
                    .SetEase(Ease.Linear)
                    .OnComplete(FinishOpen);
                return;
            }

            float up = settings.SuperRareOpenSeconds * scale;
            float bounce = settings.SuperRareBounceSeconds * scale;
            float total = up + bounce;
            float amount = settings.SuperRareBounceAmount;
            moveTween = DOTween.To(t =>
                {
                    float elapsed = t * total;
                    SetSlats(true, Mathf.Clamp01(elapsed / (up * lead)));
                    SetClosed(elapsed < up
                        ? start * (1f - OutQuad(elapsed / up))
                        : amount * Mathf.Sin(Mathf.PI * Mathf.Clamp01((elapsed - up) / bounce)));
                }, 0f, 1f, total)
                .SetEase(Ease.Linear)
                .OnComplete(FinishOpen);
        }

        /// <summary>바로 열린 자세 (스킵 · 즉시 · 새 연출 · 런 이탈). 이벤트 없음 · 틈새 증기도 지운다.</summary>
        public void ShowOpen()
        {
            KillMove();
            KillPuffs();
            jitter = 0f;
            SetSlats(false, 1f);
            SetClosed(0f);
        }

        /// <summary>SR 기다림 떨림 (가로 px). 닫혀 있을 때만 보인다.</summary>
        public void SetJitter(float pixels)
        {
            jitter = IsClosed ? pixels : 0f;
            Apply();
        }

        /// <summary>셔터 아래 틈에서 증기 한 줄기 (index 차례로 자리를 돌려 쓴다). 풀이 없으면 무시.</summary>
        public void PuffGap(int index)
        {
            if (gapPuffs.Length == 0)
            {
                return;
            }

            int slot = nextPuff;
            nextPuff = (nextPuff + 1) % gapPuffs.Length;
            KillPuff(slot);
            if (gapPuffs[slot] == null)
            {
                return;
            }

            Vector2 start = new Vector2(settings.GetGapPuffOffsetX(index), -plateHeight * 0.5f);
            ApplyPuff(slot, start, 0f);
            puffTweens[slot] = DOTween.To(t => ApplyPuff(slot, start, t), 0f, 1f, settings.GapPuffSeconds)
                .SetEase(Ease.OutQuad)
                .OnComplete(() =>
                {
                    puffTweens[slot] = null;
                    HidePuff(slot);
                });
        }

        private void FinishOpen()
        {
            moveTween = null;
            SetSlats(false, 1f);
            SetClosed(0f);
        }

        private void SetSlats(bool opening, float progress)
        {
            slatsOpening = opening;
            slatProgress = progress;
        }

        // 철판 b (아래부터 0) 는 그 아래 사이 b 개가 벌어진 만큼 위로 간다.
        // 사이 k (k 번째 철판과 그 아래 사이 · 1 부터) 는 포갤 때 아래부터 (k 가 작은 것부터) 닫히고, 열 때는 위부터 (k 가 큰 것부터) 벌어진다
        private float GetGapOpen(int gap, int gapCount)
        {
            if (slatsOpening)
            {
                return Mathf.Clamp01(slatProgress * gapCount - (gapCount - gap));
            }

            return 1f - Mathf.Clamp01(slatProgress * gapCount - (gap - 1));
        }

        private void SetClosed(float value)
        {
            closedAmount = value;
            Apply();
        }

        private void Apply()
        {
            if (plate != null)
            {
                plate.anchoredPosition = new Vector2(jitter, (1f - closedAmount) * plateHeight);
            }

            int count = slats.Length;
            int gapCount = count - 1;
            float lift = 0f;
            // 아래 철판부터 올라가며 벌어진 사이를 누적한다
            for (int b = 0; b < count; b++)
            {
                if (b > 0)
                {
                    lift += settings.SlatGap * GetGapOpen(b, gapCount);
                }

                RectTransform slat = slats[count - 1 - b];
                if (slat != null)
                {
                    slat.anchoredPosition = slatRestPositions[count - 1 - b] + new Vector2(0f, lift);
                }
            }
        }

        private void ApplyPuff(int slot, Vector2 start, float t)
        {
            Image puff = gapPuffs[slot];
            float scale = Mathf.Lerp(settings.GapPuffStartScale, settings.GapPuffEndScale, t);
            puff.rectTransform.localScale = new Vector3(scale, scale, 1f);
            puff.rectTransform.anchoredPosition = start + new Vector2(0f, settings.GapPuffRise * t);
            Color color = settings.GapPuffColor;
            color.a *= 1f - t;
            puff.color = color;
        }

        private void HidePuff(int slot)
        {
            Color color = settings.GapPuffColor;
            color.a = 0f;
            gapPuffs[slot].color = color;
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

        private void KillMove()
        {
            if (moveTween != null && moveTween.IsActive())
            {
                moveTween.Kill();
            }

            moveTween = null;
            if (settleTween != null && settleTween.IsActive())
            {
                settleTween.Kill();
            }

            settleTween = null;
        }

        private void KillPuffs()
        {
            for (int i = 0; i < puffTweens.Length; i++)
            {
                KillPuff(i);
                if (gapPuffs[i] != null)
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

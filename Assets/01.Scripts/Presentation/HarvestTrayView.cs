using System;
using System.Collections.Generic;
using DG.Tweening;
using Overclock.Core;
using UnityEngine;
using UnityEngine.UI;

namespace Overclock.Presentation
{
    // 출력 창 아래 트레이의 수확물 더미. 이번 라운드에 공개된 부품을 아래 줄부터 쌓고, 돌파하면 쏟아 비운다.
    // 언제 쌓고 비울지는 GrayBoxRunScreen 이 정한다. 게임 상태는 바꾸지 않는다 (기계 · 연출 효과와 같은 약속).
    // 시간은 scaled time — 일시정지면 떨어지던 부품도 멈춘다. 자리 · 기울기는 칸 순번으로 정한 고정 표 (난수 없음).
    // 아이콘은 미리 만든 풀만 쓴다 (공개 때 생성 · 할당 없음). 풀보다 많이 들어오면 넘친 것은 그리지 않는다
    public sealed class HarvestTrayView : MonoBehaviour
    {
        [SerializeField] private HarvestDisplaySettings settings;
        [Tooltip("트레이 칸 아이콘 풀 (라운드 최대 뽑기 20 이상). 트레이 오브젝트의 자식")]
        [SerializeField] private Image[] slotIcons = Array.Empty<Image>();

        private Tween[] dropTweens;
        private Tween dumpTween;
        private int count;

        /// <summary>트레이에 그려진 부품 수 (쏟는 중이면 쏟기 전 수).</summary>
        public int Count => count;

        /// <summary>쏟는 중인가.</summary>
        public bool IsDumping => dumpTween != null;

        private void Awake()
        {
            dropTweens = new Tween[slotIcons.Length];
            for (int i = 0; i < slotIcons.Length; i++)
            {
                // 더미가 레버 · 오버클럭 버튼 클릭을 막지 않게 한다 (씬 설정과 관계없이)
                if (slotIcons[i] != null)
                {
                    slotIcons[i].raycastTarget = false;
                    slotIcons[i].enabled = false;
                }
            }
        }

        private void OnDisable()
        {
            Clear();
        }

        private void OnDestroy()
        {
            KillAll();
        }

        /// <summary>
        /// 공개된 부품 하나를 다음 칸에 놓는다. animate = 칸 위에서 떨어짐 · false = 제자리에 바로 (스킵 · 즉시).
        /// 쏟는 중이면 먼저 비운다.
        /// </summary>
        public void Add(PartData part, bool animate)
        {
            if (part == null)
            {
                throw new ArgumentNullException(nameof(part));
            }

            if (IsDumping)
            {
                Clear();
            }

            int slot = count;
            count++;
            if (slot >= slotIcons.Length || slotIcons[slot] == null)
            {
                return;
            }

            Image icon = slotIcons[slot];
            Color gradeColor = GradeStyle.GetColor(part.Grade);
            float scale = part.Grade == Grade.SR ? settings.SuperRareScale : 1f;
            icon.sprite = part.Icon;
            icon.color = gradeColor;
            icon.enabled = true;
            icon.rectTransform.localScale = new Vector3(scale, scale, 1f);
            icon.rectTransform.localEulerAngles = new Vector3(0f, 0f, settings.GetSlotTilt(slot));

            KillDrop(slot);
            Vector2 rest = GetSlotPosition(slot);
            if (!animate)
            {
                icon.rectTransform.anchoredPosition = rest;
                return;
            }

            Vector2 start = rest + new Vector2(0f, settings.DropHeight);
            icon.rectTransform.anchoredPosition = start;
            // OutBack 은 끝에서 칸 아래로 살짝 넘어갔다 돌아온다 (툭 튐). 넘어간 값을 자르지 않게 직접 섞는다
            dropTweens[slot] = DOTween.To(t => icon.rectTransform.anchoredPosition = new Vector2(start.x + (rest.x - start.x) * t, start.y + (rest.y - start.y) * t), 0f, 1f, settings.DropSeconds)
                .SetEase(Ease.OutBack)
                .OnComplete(() => dropTweens[slot] = null);
        }

        /// <summary>이어하기 · 런 시작: 목록대로 바로 놓는다 (떨어지는 모습 없음).</summary>
        public void ShowParts(IReadOnlyList<PartData> parts)
        {
            Clear();
            if (parts == null)
            {
                return;
            }

            foreach (PartData part in parts)
            {
                Add(part, false);
            }
        }

        /// <summary>
        /// 돌파 — 잠시 뒤 더미를 아래로 쏟으며 흐리게 지우고 비운다. 스킵 · 즉시여도 쏟는다 (돌파 증기와 같은 이유: 라운드 사건).
        /// 기다리는 동안 마지막 부품은 계속 떨어진다. 쏟기가 실제로 시작되는 순간의 자리에서 쏟는다 (교차검토 #32)
        /// </summary>
        public void Dump()
        {
            if (IsDumping)
            {
                return;
            }

            Vector2[] starts = null;
            dumpTween = DOTween.To(t =>
                {
                    if (starts == null)
                    {
                        starts = BeginDump();
                    }

                    ApplyDump(starts, t);
                }, 0f, 1f, settings.DumpSeconds)
                .SetDelay(settings.DumpDelay)
                .SetEase(Ease.InQuad)
                .OnComplete(() =>
                {
                    dumpTween = null;
                    Clear();
                });
        }

        /// <summary>바로 비운다 (새 라운드 · 런 이탈).</summary>
        public void Clear()
        {
            KillAll();
            count = 0;
            for (int i = 0; i < slotIcons.Length; i++)
            {
                if (slotIcons[i] != null)
                {
                    slotIcons[i].enabled = false;
                }
            }
        }

        /// <summary>칸의 쉬는 자리 (트레이 안 좌표). 아래 줄 가운데 정렬부터 채우고 위로.</summary>
        public Vector2 GetSlotPosition(int slot)
        {
            int columns = settings.Columns;
            int column = slot % columns;
            int row = slot / columns;
            float x = (column - (columns - 1) * 0.5f) * settings.ColumnSpacing + settings.GetSlotOffsetX(slot);
            float y = settings.BottomRowY + row * settings.RowSpacing + settings.GetSlotOffsetY(slot);
            return new Vector2(x, y);
        }

        private Vector2[] BeginDump()
        {
            // 아직 떨어지는 중이면 (기다림 < 낙하 시간으로 값을 바꾼 경우) 지금 자리에서 멈추고 쏟는다
            int shown = Math.Min(count, slotIcons.Length);
            var starts = new Vector2[shown];
            for (int i = 0; i < shown; i++)
            {
                KillDrop(i);
                if (slotIcons[i] != null)
                {
                    starts[i] = slotIcons[i].rectTransform.anchoredPosition;
                }
            }

            return starts;
        }

        private void ApplyDump(Vector2[] starts, float t)
        {
            for (int i = 0; i < starts.Length; i++)
            {
                Image icon = slotIcons[i];
                if (icon == null)
                {
                    continue;
                }

                icon.rectTransform.anchoredPosition = starts[i] + new Vector2(0f, -settings.DumpDistance * t);
                Color color = icon.color;
                color.a = 1f - t;
                icon.color = color;
            }
        }

        private void KillAll()
        {
            if (dropTweens != null)
            {
                for (int i = 0; i < dropTweens.Length; i++)
                {
                    KillDrop(i);
                }
            }

            if (dumpTween != null && dumpTween.IsActive())
            {
                dumpTween.Kill();
            }

            dumpTween = null;
        }

        private void KillDrop(int slot)
        {
            Tween tween = dropTweens[slot];
            if (tween != null && tween.IsActive())
            {
                tween.Kill();
            }

            dropTweens[slot] = null;
        }
    }
}

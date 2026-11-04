using System;
using System.Collections.Generic;
using DG.Tweening;
using Overclock.Core;
using UnityEngine;
using UnityEngine.UI;

namespace Overclock.Presentation
{
    // 출력 창 아래 트레이의 수확물 더미 (11/04 ① — 생산량만큼 쌓임).
    // 공개 순간 출력 창에서 조각(그 부품 아이콘 축소판)이 이번 생산량 수만큼 튀어나와 트레이에 쌓인다. 조각 1개 = 생산량 1.
    // 라운드 동안 더미가 생산량에 비례해 커지고, 돌파하면 마지막 조각이 앉은 뒤 쏟아 비운다. 실패 · 클리어는 그대로 둔다.
    // 언제 쌓고 비울지는 GrayBoxRunScreen 이 정한다. 게임 상태는 바꾸지 않는다. 시간은 scaled time (일시정지면 멈춤).
    // 자리 · 기울기 · 튀어나오는 방향은 순번으로 정한 고정 표 (난수 없음). 조각 이미지는 Awake 에서 한 번 만든 풀만 쓴다.
    // 더미 칸(기본 108 = R8 할당량)을 넘는 조각은 날아와 앉은 것으로 세지만 그리지 않는다 (더미가 가득 찬 모습)
    public sealed class HarvestTrayView : MonoBehaviour
    {
        [SerializeField] private HarvestDisplaySettings settings;
        [Tooltip("조각 틀로 쓸 이미지. 비어 있으면 아래 Slot Icons 의 첫 칸을 틀로 쓴다")]
        [SerializeField] private Image pieceTemplate;
        [Tooltip("11/03 부품 칸 (20). 11/04 부터 쓰지 않고 숨긴다 — 첫 칸은 조각 틀로 쓸 수 있다")]
        [SerializeField] private Image[] slotIcons = Array.Empty<Image>();

        /// <summary>날아온 조각이 더미에 앉은 순간 (바로 놓을 때는 없음). firstOfDraw = 이번 뽑기의 첫 조각 (덜컹 · 착지음).</summary>
        public event Action<bool> PieceLanded;

        private Image[] pileImages = Array.Empty<Image>();
        private Image[] flyingImages = Array.Empty<Image>();
        private Tween[] flyingTweens = Array.Empty<Tween>();
        private PartData[] flyingParts = Array.Empty<PartData>();
        private readonly List<Tween> launchDelays = new List<Tween>();
        private readonly List<PartData> pendingParts = new List<PartData>();
        private readonly Queue<WaitingPiece> waitingForImage = new Queue<WaitingPiece>();
        private Tween dumpTween;
        private int count;
        private int flyingCount;
        private bool dumpRequested;

        /// <summary>더미에 앉은 조각 수 (= 이번 라운드에 보여 준 생산량). 그려진 수는 더미 칸까지.</summary>
        public int Count => count;

        /// <summary>날아가는 중이거나 아직 출발하지 않은 조각 수.</summary>
        public int FlyingCount => flyingCount;

        /// <summary>쏟기를 기다리거나 쏟는 중인가.</summary>
        public bool IsDumping => dumpTween != null || dumpRequested;

        /// <summary>더미에 그릴 수 있는 조각 수 (칸 수).</summary>
        public int PileCapacity => pileImages.Length;

        private void Awake()
        {
            for (int i = 0; i < slotIcons.Length; i++)
            {
                if (slotIcons[i] != null)
                {
                    slotIcons[i].raycastTarget = false;
                    slotIcons[i].enabled = false;
                }
            }

            Image template = pieceTemplate != null ? pieceTemplate : (slotIcons.Length > 0 ? slotIcons[0] : null);
            if (template == null)
            {
                return;
            }

            pileImages = CreatePool(template, settings.PileColumns * settings.PileRows);
            flyingImages = CreatePool(template, settings.FlyingPoolSize);
            flyingTweens = new Tween[flyingImages.Length];
            flyingParts = new PartData[flyingImages.Length];
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
        /// 공개된 부품의 생산량만큼 조각을 더미에 더한다. animate = 출력 창에서 튀어나와 날아와 앉음 · false = 바로 더미에 (스킵 · 즉시).
        /// 쏟는 중이면 먼저 비운다. 생산량 0 (오버클럭 N) 이면 아무것도 하지 않는다.
        /// </summary>
        public void AddOutput(PartData part, int output, bool animate)
        {
            if (part == null)
            {
                throw new ArgumentNullException(nameof(part));
            }

            if (IsDumping)
            {
                Clear();
            }

            if (output <= 0)
            {
                return;
            }

            if (!animate)
            {
                for (int i = 0; i < output; i++)
                {
                    PlaceOnPile(part);
                }

                return;
            }

            // 많이 나와도 뿌리는 시간은 상한까지만 (SR 에 배수가 붙으면 수십 개)
            float interval = Mathf.Min(settings.LaunchInterval, settings.SprayMaxSeconds / output);
            for (int i = 0; i < output; i++)
            {
                Launch(part, i, i * interval, i == 0);
            }
        }

        /// <summary>
        /// 이어하기 · 런 시작: 이번 라운드 부품과 생산량으로 더미를 바로 만든다 (날아오는 모습 없음).
        /// 부품마다 생산량을 저장하지 않으므로 기본 생산량 비율로 나눠 조각 수 합 = 생산량이 되게 한다.
        /// </summary>
        public void ShowPile(IReadOnlyList<PartData> parts, int production)
        {
            Clear();
            if (parts == null || parts.Count == 0 || production <= 0)
            {
                return;
            }

            int[] shares = SplitByBaseOutput(parts, production);
            for (int i = 0; i < parts.Count; i++)
            {
                for (int k = 0; k < shares[i]; k++)
                {
                    PlaceOnPile(parts[i]);
                }
            }
        }

        /// <summary>날아가는 조각을 모두 바로 더미에 앉힌다 (다음 뽑기 · 숫자를 최종 값으로 맞출 때). 착지 이벤트는 내지 않는다.</summary>
        public void CompleteFlights()
        {
            for (int i = 0; i < launchDelays.Count; i++)
            {
                Kill(launchDelays[i]);
            }

            launchDelays.Clear();
            for (int i = 0; i < flyingImages.Length; i++)
            {
                if (flyingParts[i] == null)
                {
                    continue;
                }

                Kill(flyingTweens[i]);
                flyingTweens[i] = null;
                PartData part = flyingParts[i];
                flyingParts[i] = null;
                flyingImages[i].enabled = false;
                PlaceOnPile(part);
            }

            // 빈 이미지를 기다리던 조각 → 출발 전이던 조각 순서로 앉힌다
            while (waitingForImage.Count > 0)
            {
                PlaceOnPile(waitingForImage.Dequeue().Part);
            }

            for (int i = 0; i < pendingParts.Count; i++)
            {
                PlaceOnPile(pendingParts[i]);
            }

            pendingParts.Clear();
            flyingCount = 0;
            StartDumpIfRequested();
        }

        /// <summary>
        /// 돌파 — 날아가는 조각이 다 앉은 뒤 잠시 기다렸다 더미를 아래로 쏟으며 흐리게 지우고 비운다.
        /// 스킵 · 즉시여도 쏟는다 (돌파 증기와 같은 이유: 라운드 사건).
        /// </summary>
        public void Dump()
        {
            if (IsDumping)
            {
                return;
            }

            dumpRequested = true;
            StartDumpIfRequested();
        }

        /// <summary>바로 비운다 (새 라운드 · 런 이탈).</summary>
        public void Clear()
        {
            KillAll();
            count = 0;
            flyingCount = 0;
            dumpRequested = false;
            pendingParts.Clear();
            waitingForImage.Clear();
            for (int i = 0; i < pileImages.Length; i++)
            {
                pileImages[i].enabled = false;
            }

            for (int i = 0; i < flyingImages.Length; i++)
            {
                flyingImages[i].enabled = false;
                flyingParts[i] = null;
            }
        }

        /// <summary>더미 칸의 쉬는 자리 (트레이 안 좌표). 아래 줄부터 채우고 위로. 줄마다 반 칸씩 엇갈려 벽돌처럼 쌓인다.</summary>
        public Vector2 GetPilePosition(int slot)
        {
            int columns = settings.PileColumns;
            int column = slot % columns;
            int row = slot / columns;
            float stagger = (row % 2 == 1 ? 0.25f : -0.25f) * settings.PileColumnSpacing;
            float x = (column - (columns - 1) * 0.5f) * settings.PileColumnSpacing + stagger + settings.GetSlotOffsetX(slot);
            float y = settings.PileBottomY + row * settings.PileRowSpacing + settings.GetSlotOffsetY(slot);
            return new Vector2(x, y);
        }

        /// <summary>i 번째 조각이 출력 창에서 튀어나오는 자리 (트레이 안 좌표).</summary>
        public Vector2 GetSpawnPosition(int index)
        {
            return settings.SpawnPoint + new Vector2(settings.GetSpawnOffsetX(index), 0f);
        }

        internal static int[] SplitByBaseOutput(IReadOnlyList<PartData> parts, int production)
        {
            // 큰 나머지부터 1씩 — 같으면 앞 부품부터 (난수 없음). 기본 생산량 0 은 1 로 본다
            var shares = new int[parts.Count];
            var remainders = new double[parts.Count];
            double total = 0;
            for (int i = 0; i < parts.Count; i++)
            {
                total += Math.Max(1, parts[i].BaseOutput);
            }

            int assigned = 0;
            for (int i = 0; i < parts.Count; i++)
            {
                double exact = production * Math.Max(1, parts[i].BaseOutput) / total;
                shares[i] = (int)Math.Floor(exact);
                remainders[i] = exact - shares[i];
                assigned += shares[i];
            }

            for (int left = production - assigned; left > 0; left--)
            {
                int best = 0;
                for (int i = 1; i < parts.Count; i++)
                {
                    if (remainders[i] > remainders[best])
                    {
                        best = i;
                    }
                }

                shares[best]++;
                remainders[best] = -1;
            }

            return shares;
        }

        private void Launch(PartData part, int index, float delay, bool firstOfDraw)
        {
            flyingCount++;
            if (delay <= 0f)
            {
                Fly(part, index, firstOfDraw);
                return;
            }

            pendingParts.Add(part);
            Tween wait = null;
            wait = DOTween.To(_ => { }, 0f, 1f, delay)
                .SetEase(Ease.Linear)
                .OnComplete(() =>
                {
                    launchDelays.Remove(wait);
                    pendingParts.Remove(part);
                    Fly(part, index, firstOfDraw);
                });
            launchDelays.Add(wait);
        }

        private void Fly(PartData part, int index, bool firstOfDraw)
        {
            if (flyingImages.Length == 0)
            {
                // 조각 틀이 없어 그릴 수 없으면 바로 앉힌다 (세는 수 · 이벤트는 같다)
                Land(part, firstOfDraw);
                return;
            }

            // 날아가는 이미지가 모자라면 빈 이미지가 생길 때까지 출발을 미룬다.
            // 바로 앉히면 먼저 출발한 조각보다 먼저 칸을 차지하고 숫자 · 소리가 앞질러진다 (교차검토 #34). 이미 기다리는 조각이 있으면 그 뒤에 선다
            int slot = FreeFlyingSlot();
            if (slot < 0 || waitingForImage.Count > 0)
            {
                waitingForImage.Enqueue(new WaitingPiece(part, index, firstOfDraw));
                return;
            }

            StartFlight(part, index, firstOfDraw, slot);
        }

        private void StartFlight(PartData part, int index, bool firstOfDraw, int slot)
        {
            int target = count + CountFlyingSlotsInUse();
            Image piece = flyingImages[slot];
            flyingParts[slot] = part;
            Style(piece, part);
            Vector2 start = GetSpawnPosition(index);
            Vector2 end = target < pileImages.Length ? GetPilePosition(target) : GetPilePosition(pileImages.Length - 1);
            float arc = settings.ArcHeight;
            piece.rectTransform.anchoredPosition = start;
            piece.rectTransform.localEulerAngles = new Vector3(0f, 0f, settings.GetSlotTilt(index));
            piece.enabled = true;

            // 위로 튀었다가 가속하며 더미에 앉는다 (포물선)
            flyingTweens[slot] = DOTween.To(t =>
                {
                    float x = start.x + (end.x - start.x) * t;
                    float y = start.y + (end.y - start.y) * t * t + arc * 4f * t * (1f - t);
                    piece.rectTransform.anchoredPosition = new Vector2(x, y);
                }, 0f, 1f, settings.FlightSeconds)
                .SetEase(Ease.Linear)
                .OnComplete(() =>
                {
                    flyingTweens[slot] = null;
                    flyingParts[slot] = null;
                    piece.enabled = false;
                    Land(part, firstOfDraw);
                    LaunchWaiting();
                });
        }

        private void LaunchWaiting()
        {
            // 앉은 조각이 이미지를 돌려줬다 — 기다리던 조각을 차례로 출발시킨다
            while (waitingForImage.Count > 0)
            {
                int slot = FreeFlyingSlot();
                if (slot < 0)
                {
                    return;
                }

                WaitingPiece next = waitingForImage.Dequeue();
                StartFlight(next.Part, next.Index, next.FirstOfDraw, slot);
            }
        }

        private void Land(PartData part, bool firstOfDraw)
        {
            flyingCount = Math.Max(0, flyingCount - 1);
            PlaceOnPile(part);
            PieceLanded?.Invoke(firstOfDraw);
            StartDumpIfRequested();
        }

        private void PlaceOnPile(PartData part)
        {
            int slot = count;
            count++;
            if (slot >= pileImages.Length)
            {
                return;
            }

            Image image = pileImages[slot];
            Style(image, part);
            image.rectTransform.anchoredPosition = GetPilePosition(slot);
            image.rectTransform.localEulerAngles = new Vector3(0f, 0f, settings.GetSlotTilt(slot));
            image.enabled = true;
        }

        private void Style(Image image, PartData part)
        {
            float scale = part.Grade == Grade.SR ? settings.SuperRareScale : 1f;
            image.sprite = part.Icon;
            image.color = GradeStyle.GetColor(part.Grade);
            image.rectTransform.localScale = new Vector3(scale, scale, 1f);
        }

        private void StartDumpIfRequested()
        {
            if (!dumpRequested || flyingCount > 0 || dumpTween != null)
            {
                return;
            }

            dumpRequested = false;
            int shown = Math.Min(count, pileImages.Length);
            var starts = new Vector2[shown];
            for (int i = 0; i < shown; i++)
            {
                starts[i] = pileImages[i].rectTransform.anchoredPosition;
            }

            dumpTween = DOTween.To(t => ApplyDump(starts, t), 0f, 1f, settings.DumpSeconds)
                .SetDelay(settings.DumpDelay)
                .SetEase(Ease.InQuad)
                .OnComplete(() =>
                {
                    dumpTween = null;
                    Clear();
                });
        }

        private void ApplyDump(Vector2[] starts, float t)
        {
            for (int i = 0; i < starts.Length; i++)
            {
                Image image = pileImages[i];
                image.rectTransform.anchoredPosition = starts[i] + new Vector2(0f, -settings.DumpDistance * t);
                Color color = image.color;
                color.a = 1f - t;
                image.color = color;
            }
        }

        private int FreeFlyingSlot()
        {
            for (int i = 0; i < flyingParts.Length; i++)
            {
                if (flyingParts[i] == null)
                {
                    return i;
                }
            }

            return -1;
        }

        private int CountFlyingSlotsInUse()
        {
            int inUse = 0;
            for (int i = 0; i < flyingParts.Length; i++)
            {
                if (flyingParts[i] != null)
                {
                    inUse++;
                }
            }

            return inUse;
        }

        private Image[] CreatePool(Image template, int size)
        {
            var pool = new Image[size];
            for (int i = 0; i < size; i++)
            {
                Image image = Instantiate(template, transform);
                image.raycastTarget = false;
                image.enabled = false;
                image.rectTransform.sizeDelta = new Vector2(settings.PieceSize, settings.PieceSize);
                pool[i] = image;
            }

            return pool;
        }

        private void KillAll()
        {
            for (int i = 0; i < launchDelays.Count; i++)
            {
                Kill(launchDelays[i]);
            }

            launchDelays.Clear();
            for (int i = 0; i < flyingTweens.Length; i++)
            {
                Kill(flyingTweens[i]);
                flyingTweens[i] = null;
            }

            Kill(dumpTween);
            dumpTween = null;
        }

        private readonly struct WaitingPiece
        {
            public readonly PartData Part;
            public readonly int Index;
            public readonly bool FirstOfDraw;

            public WaitingPiece(PartData part, int index, bool firstOfDraw)
            {
                Part = part;
                Index = index;
                FirstOfDraw = firstOfDraw;
            }
        }

        private static void Kill(Tween tween)
        {
            if (tween != null && tween.IsActive())
            {
                tween.Kill();
            }
        }
    }
}

using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Overclock.Presentation
{
    public enum IntroEntryEdge
    {
        Top,
        Bottom,
        Left,
        Right
    }

    // 런 시작 때 화면 밖에서 레일을 타고 들어오는 패널 하나 (11/07 · v3). 두 겹으로 나눈다 —
    //   이 오브젝트 (배치) : 최종 자리 · 크기. 움직이지 않는다
    //   Slide (이동)       : 패널판 · 테두리 · 볼트 같은 장식 + 글자 · 숫자 · 목록. 한 덩어리로 움직인다 (안쪽만 따로 드러나지 않는다)
    // 패널 이미지를 바꿀 때는 Slide 아래 판 · 테두리만 바꾼다. 출발 자리 · 지나치는 거리는 실제 Canvas · 패널 크기로 매번 계산하므로
    // 장식이 더 튀어나오면 (Slide 아래 Graphic 이 커지면) 그대로 따라간다.
    // 크기는 보이는 Graphic 사각형 + 테두리 · 그림자 효과 (Outline · Shadow) 거리, 가림막 (RectMask2D · Mask) 밖으로 잘린 부분은 뺀다
    public sealed class IntroPanelView : MonoBehaviour
    {
        [Tooltip("판 · 장식 · 안쪽 정보를 함께 움직이는 겹")]
        [SerializeField] private RectTransform slide;
        [Tooltip("어느 화면 끝 밖에서 들어오나")]
        [SerializeField] private IntroEntryEdge enterFrom = IntroEntryEdge.Top;
        [Tooltip("최종 자리를 한 번 지나치는 거리 (UI 단위). 기계 · 다른 화면 요소와 여유가 모자라면 자동으로 줄인다")]
        [SerializeField, Min(0f)] private float overshoot = 20f;

        private Vector2 restPosition;
        private bool restCaptured;
        private Vector2 entryOffset;
        private Vector2 inward;
        private float bounce;
        private string limitedBy = "";

        /// <summary>이번 등장에 실제로 쓰는 지나치는 거리 (여유에 맞춰 줄인 값 · Slide 부모 좌표).</summary>
        public float Bounce => bounce;

        /// <summary>지나치는 거리를 정한 가장 가까운 것 (이름 · 틈). 막는 것이 없으면 빈 문자열. 배치 확인용.</summary>
        public string LimitedBy => limitedBy;

        /// <summary>최종 자리 기준 출발 자리 (Slide 부모 좌표).</summary>
        public Vector2 EntryOffset => entryOffset;

        /// <summary>지나치는 방향 (Slide 부모 좌표 · 길이 1). 들어오던 방향과 같다.</summary>
        public Vector2 OvershootDirection => inward;

        /// <summary>
        /// 출발 자리와 지나치는 거리를 지금 화면에 맞춰 계산한다. 출발 자리 = 패널 전체 (Slide 아래 보이는 Graphic 모두 + 테두리) 가 화면 밖.
        /// 지나치는 거리 = 원래 값과 (가리면 안 되는 것까지의 틈 − 안전 여유) 중 작은 값. 최종 자리에서 재므로 먼저 최종 자리로 둔다.
        /// screen 이 비면 계산하지 않고 이전 값을 쓴다.
        /// </summary>
        public void Prepare(RectTransform screen, IReadOnlyList<RectTransform> keepClear, float screenMargin, float clearanceMargin)
        {
            ShowFinal();
            if (slide == null || screen == null)
            {
                return;
            }

            Rect panel = GraphicBounds(slide, screen);
            if (panel.width <= 0f || panel.height <= 0f)
            {
                return;
            }

            float distance = OffscreenDistance(ScreenRect(screen, screen), panel, enterFrom, screenMargin);

            List<Rect> obstacles = new List<Rect>();
            List<Graphic> obstacleGraphics = new List<Graphic>();
            if (keepClear != null)
            {
                for (int i = 0; i < keepClear.Count; i++)
                {
                    if (keepClear[i] != null && !keepClear[i].IsChildOf(transform))
                    {
                        CollectGraphicRects(keepClear[i], screen, obstacles, obstacleGraphics);
                    }
                }
            }

            float clearance = Clearance(panel, enterFrom, obstacles, out int nearest);
            limitedBy = nearest >= 0 ? obstacleGraphics[nearest].name + " (틈 " + clearance.ToString("0.#") + ")" : "";
            float allowed = Mathf.Max(0f, clearance - clearanceMargin);

            // 화면 좌표 (UI 단위) → Slide 부모 좌표. Canvas 와 사이에 크기 · 회전이 있어도 맞게 바꾼다
            Vector2 inwardOnScreen = Inward(enterFrom);
            Transform parent = slide.parent;
            Vector3 worldOffset = screen.TransformVector(-(Vector3)(inwardOnScreen * distance));
            Vector3 worldInward = screen.TransformVector((Vector3)inwardOnScreen);
            entryOffset = parent.InverseTransformVector(worldOffset);
            Vector2 localInward = parent.InverseTransformVector(worldInward);
            float unit = localInward.magnitude;
            inward = unit > 0f ? localInward / unit : Vector2.zero;
            bounce = Mathf.Min(overshoot, allowed) * unit;
        }

        /// <summary>
        /// 최종 자리 기준으로 놓는다. travel 0 = 출발 자리 (화면 밖) · 1 = 최종 자리.
        /// pastFraction 0 ~ 1 = 최종 자리를 Bounce 의 몇 배만큼 지나쳤나 (들어오던 방향으로).
        /// </summary>
        public void Place(float travel, float pastFraction)
        {
            if (slide == null)
            {
                return;
            }

            CaptureRest();
            slide.anchoredPosition = restPosition + entryOffset * (1f - travel) + inward * (bounce * pastFraction);
        }

        public void ShowFinal()
        {
            if (slide == null)
            {
                return;
            }

            CaptureRest();
            slide.anchoredPosition = restPosition;
        }

        private void CaptureRest()
        {
            // 최종 자리는 처음 움직이기 전 값을 기억한다 (씬 배치가 바뀌어도 그 자리로 돌아온다)
            if (!restCaptured)
            {
                restPosition = slide.anchoredPosition;
                restCaptured = true;
            }
        }

        /// <summary>패널 전체가 화면 끝 밖으로 나가는 데 필요한 이동 거리 (화면 UI 단위 · 여유 포함).</summary>
        internal static float OffscreenDistance(Rect screen, Rect panel, IntroEntryEdge edge, float margin)
        {
            switch (edge)
            {
                case IntroEntryEdge.Top:
                    return Mathf.Max(0f, screen.y + screen.height - panel.y) + margin;
                case IntroEntryEdge.Bottom:
                    return Mathf.Max(0f, panel.y + panel.height - screen.y) + margin;
                case IntroEntryEdge.Left:
                    return Mathf.Max(0f, panel.x + panel.width - screen.x) + margin;
                default:
                    return Mathf.Max(0f, screen.x + screen.width - panel.x) + margin;
            }
        }

        /// <summary>
        /// 최종 자리에서 들어오던 방향으로 더 나아갈 때 가장 먼저 닿는 것까지의 틈 (UI 단위). 닿을 것이 없으면 float.MaxValue.
        /// 이미 겹쳐 있으면 0 이하 (지나치지 않는다).
        /// </summary>
        internal static float Clearance(Rect panel, IntroEntryEdge edge, IList<Rect> obstacles)
        {
            return Clearance(panel, edge, obstacles, out int _);
        }

        internal static float Clearance(Rect panel, IntroEntryEdge edge, IList<Rect> obstacles, out int nearest)
        {
            float best = float.MaxValue;
            nearest = -1;
            bool vertical = edge == IntroEntryEdge.Top || edge == IntroEntryEdge.Bottom;
            for (int i = 0; i < obstacles.Count; i++)
            {
                Rect other = obstacles[i];
                bool sideBySide = vertical
                    ? other.x < panel.x + panel.width && panel.x < other.x + other.width
                    : other.y < panel.y + panel.height && panel.y < other.y + other.height;
                if (!sideBySide)
                {
                    continue;
                }

                // 들어오던 방향의 앞쪽 (지나치는 쪽) 에 있는 것만 본다. 뒤쪽 (출발한 쪽) 은 닿지 않는다
                float gap;
                switch (edge)
                {
                    case IntroEntryEdge.Top:
                        if (other.y + other.height > panel.y + panel.height)
                        {
                            continue;
                        }

                        gap = panel.y - (other.y + other.height);
                        break;
                    case IntroEntryEdge.Bottom:
                        if (other.y < panel.y)
                        {
                            continue;
                        }

                        gap = other.y - (panel.y + panel.height);
                        break;
                    case IntroEntryEdge.Left:
                        if (other.x < panel.x)
                        {
                            continue;
                        }

                        gap = other.x - (panel.x + panel.width);
                        break;
                    default:
                        if (other.x + other.width > panel.x + panel.width)
                        {
                            continue;
                        }

                        gap = panel.x - (other.x + other.width);
                        break;
                }

                if (gap < best)
                {
                    best = gap;
                    nearest = i;
                }
            }

            return best;
        }

        private static Vector2 Inward(IntroEntryEdge edge)
        {
            switch (edge)
            {
                case IntroEntryEdge.Top:
                    return new Vector2(0f, -1f);
                case IntroEntryEdge.Bottom:
                    return new Vector2(0f, 1f);
                case IntroEntryEdge.Left:
                    return new Vector2(1f, 0f);
                default:
                    return new Vector2(-1f, 0f);
            }
        }

        private static Rect GraphicBounds(RectTransform root, RectTransform screen)
        {
            List<Rect> rects = new List<Rect>();
            CollectGraphicRects(root, screen, rects, null);
            if (rects.Count == 0)
            {
                return new Rect(0f, 0f, 0f, 0f);
            }

            float xMin = float.MaxValue;
            float yMin = float.MaxValue;
            float xMax = float.MinValue;
            float yMax = float.MinValue;
            for (int i = 0; i < rects.Count; i++)
            {
                xMin = Mathf.Min(xMin, rects[i].x);
                yMin = Mathf.Min(yMin, rects[i].y);
                xMax = Mathf.Max(xMax, rects[i].x + rects[i].width);
                yMax = Mathf.Max(yMax, rects[i].y + rects[i].height);
            }

            return new Rect(xMin, yMin, xMax - xMin, yMax - yMin);
        }

        private static void CollectGraphicRects(Transform root, RectTransform screen, List<Rect> into, List<Graphic> owners)
        {
            // 보이는 Graphic (Image · 글자) 만 센다. 빈 정리용 오브젝트 · 꺼진 것 · 투명한 것은 자리를 차지하지 않는다.
            // 가림막 안에 숨어 있는 부분 (예: 열린 셔터판) 은 가림막 사각형으로 잘라서 센다
            Graphic[] graphics = root.GetComponentsInChildren<Graphic>(false);
            for (int i = 0; i < graphics.Length; i++)
            {
                Graphic graphic = graphics[i];
                if (!graphic.enabled || graphic.color.a <= 0.01f)
                {
                    continue;
                }

                Rect rect = Grow(ScreenRect(graphic.rectTransform, screen), EffectReach(graphic));
                if (!ClipToMasks(graphic.transform, screen, ref rect))
                {
                    continue;
                }

                into.Add(rect);
                if (owners != null)
                {
                    owners.Add(graphic);
                }
            }
        }

        private static float EffectReach(Graphic graphic)
        {
            // Outline · Shadow 는 Graphic 사각형 밖으로 이만큼 더 그린다
            float reach = 0f;
            Shadow[] effects = graphic.GetComponents<Shadow>();
            for (int i = 0; i < effects.Length; i++)
            {
                if (effects[i].enabled)
                {
                    Vector2 distance = effects[i].effectDistance;
                    reach = Mathf.Max(reach, Mathf.Max(Mathf.Abs(distance.x), Mathf.Abs(distance.y)));
                }
            }

            return reach;
        }

        private static bool ClipToMasks(Transform target, RectTransform screen, ref Rect rect)
        {
            // 자기 자신의 Mask 는 자기 모양을 자르지 않는다. 부모 쪽 가림막만 본다 (화면 Canvas 까지)
            for (Transform parent = target.parent; parent != null && parent != screen; parent = parent.parent)
            {
                RectTransform area = parent as RectTransform;
                if (area == null || !IsMasking(parent))
                {
                    continue;
                }

                if (!Intersect(rect, ScreenRect(area, screen), out rect))
                {
                    return false;
                }
            }

            return true;
        }

        private static bool IsMasking(Transform target)
        {
            RectMask2D rectMask = target.GetComponent<RectMask2D>();
            if (rectMask != null && rectMask.enabled)
            {
                return true;
            }

            Mask mask = target.GetComponent<Mask>();
            return mask != null && mask.enabled;
        }

        /// <summary>두 사각형이 겹치는 부분. 넓이가 없으면 false.</summary>
        internal static bool Intersect(Rect a, Rect b, out Rect result)
        {
            float xMin = Mathf.Max(a.x, b.x);
            float yMin = Mathf.Max(a.y, b.y);
            float xMax = Mathf.Min(a.x + a.width, b.x + b.width);
            float yMax = Mathf.Min(a.y + a.height, b.y + b.height);
            result = new Rect(xMin, yMin, Mathf.Max(0f, xMax - xMin), Mathf.Max(0f, yMax - yMin));
            return xMax > xMin && yMax > yMin;
        }

        private static Rect ScreenRect(RectTransform target, RectTransform screen)
        {
            Vector3[] corners = new Vector3[4];
            target.GetWorldCorners(corners);
            Vector3 a = screen.InverseTransformPoint(corners[0]);
            Vector3 b = screen.InverseTransformPoint(corners[2]);
            return new Rect(Mathf.Min(a.x, b.x), Mathf.Min(a.y, b.y), Mathf.Abs(b.x - a.x), Mathf.Abs(b.y - a.y));
        }

        private static Rect Grow(Rect rect, float amount)
        {
            return new Rect(rect.x - amount, rect.y - amount, rect.width + amount * 2f, rect.height + amount * 2f);
        }
    }
}

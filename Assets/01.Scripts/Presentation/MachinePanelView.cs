using System.Collections.Generic;
using Overclock.Core;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Overclock.Presentation
{
    // 장착 모듈을 기계 슬롯(아이콘)으로 보여 줌. 같은 모듈은 한 슬롯에 ×n 으로 묶음.
    // 슬롯에 마우스를 올리면 설명 팝업, 클릭하면 고정, 다른 곳을 클릭하면 닫힘.
    public sealed class MachinePanelView : MonoBehaviour
    {
        [SerializeField] private TMP_Text titleText;
        [SerializeField] private RectTransform slotContainer;
        [SerializeField] private MachineSlotView slotTemplate;
        [SerializeField] private RectTransform tooltip;
        [SerializeField] private TMP_Text tooltipText;

        private readonly List<EquippedModuleGroup> groups = new List<EquippedModuleGroup>();
        private readonly List<MachineSlotView> slots = new List<MachineSlotView>();
        private readonly Vector3[] tooltipCorners = new Vector3[4];
        private bool interactable = true;
        private int pinnedIndex = -1;
        private Vector2 pinnedPosition;
        private int shownIndex = -1;

        private void Awake()
        {
            slotTemplate.gameObject.SetActive(false);
            tooltip.gameObject.SetActive(false);
        }

        public void Show(IReadOnlyList<ModuleData> equippedModules)
        {
            groups.Clear();
            groups.AddRange(Group(equippedModules));
            titleText.text = "장착 모듈 " + equippedModules.Count;

            // 슬롯은 필요한 만큼만 만들고 다시 씀. 템플릿은 꺼 둔 채라 레이아웃에 들어가지 않음
            while (slots.Count < groups.Count)
            {
                slots.Add(Instantiate(slotTemplate, slotContainer));
            }

            for (int i = 0; i < slots.Count; i++)
            {
                if (i < groups.Count)
                {
                    slots[i].Show(groups[i].Module, groups[i].Count);
                }
                else
                {
                    slots[i].Hide();
                }
            }

            pinnedIndex = -1;
            HideTooltip();
        }

        public void SetInteractable(bool value)
        {
            interactable = value;
            if (!value)
            {
                pinnedIndex = -1;
                HideTooltip();
            }
        }

        private void Update()
        {
            Mouse mouse = Mouse.current;
            if (!interactable || mouse == null)
            {
                return;
            }

            Vector2 pointer = mouse.position.ReadValue();
            int hoveredIndex = FindHoveredGroup(pointer);

            if (mouse.leftButton.wasPressedThisFrame)
            {
                pinnedIndex = hoveredIndex;
                pinnedPosition = pointer;
            }

            int index = hoveredIndex >= 0 ? hoveredIndex : pinnedIndex;
            if (index < 0)
            {
                HideTooltip();
                return;
            }

            if (index != shownIndex)
            {
                ShowTooltip(index, hoveredIndex >= 0 ? pointer : pinnedPosition);
            }
        }

        private int FindHoveredGroup(Vector2 pointer)
        {
            // 레이캐스트를 쓰지 않고 사각형으로 판정함. 모듈 선택 오버레이가 위를 덮어도 판정이 같음.
            // 캔버스가 Screen Space - Overlay 라 카메라는 null
            for (int i = 0; i < groups.Count; i++)
            {
                if (RectTransformUtility.RectangleContainsScreenPoint(slots[i].Rect, pointer, null))
                {
                    return i;
                }
            }

            return -1;
        }

        private void ShowTooltip(int index, Vector2 position)
        {
            tooltipText.text = BuildTooltipText(groups[index]);
            // Overlay 캔버스에서는 화면 좌표가 그대로 위치임
            tooltip.position = position;
            KeepTooltipOnScreen();
            tooltip.gameObject.SetActive(true);
            shownIndex = index;
        }

        private void KeepTooltipOnScreen()
        {
            // 0 = 왼쪽 아래, 2 = 오른쪽 위 (Overlay 캔버스라 화면 픽셀 좌표)
            tooltip.GetWorldCorners(tooltipCorners);
            Vector3 shift = Vector3.zero;

            if (tooltipCorners[0].x < 0f)
            {
                shift.x = -tooltipCorners[0].x;
            }
            else if (tooltipCorners[2].x > Screen.width)
            {
                shift.x = Screen.width - tooltipCorners[2].x;
            }

            if (tooltipCorners[0].y < 0f)
            {
                shift.y = -tooltipCorners[0].y;
            }
            else if (tooltipCorners[2].y > Screen.height)
            {
                shift.y = Screen.height - tooltipCorners[2].y;
            }

            tooltip.position += shift;
        }

        private void HideTooltip()
        {
            tooltip.gameObject.SetActive(false);
            shownIndex = -1;
        }

        internal static List<EquippedModuleGroup> Group(IReadOnlyList<ModuleData> modules)
        {
            var result = new List<EquippedModuleGroup>();
            foreach (ModuleData module in modules)
            {
                EquippedModuleGroup found = null;
                foreach (EquippedModuleGroup group in result)
                {
                    if (group.Module == module)
                    {
                        found = group;
                        break;
                    }
                }

                if (found == null)
                {
                    result.Add(new EquippedModuleGroup(module));
                }
                else
                {
                    found.Count++;
                }
            }

            return result;
        }

        internal static string BuildTooltipText(EquippedModuleGroup group)
        {
            string label = group.Count > 1 ? group.Module.DisplayName + " ×" + group.Count : group.Module.DisplayName;
            string family = FamilyStyle.IsShown(group.Module.Family) ? "  " + FamilyStyle.Colorize(group.Module.Family) : "";
            return "<b>" + label + "</b>" + family + "\n" + group.Module.Description;
        }

        internal sealed class EquippedModuleGroup
        {
            public EquippedModuleGroup(ModuleData module)
            {
                Module = module;
                Count = 1;
            }

            public ModuleData Module { get; }
            public int Count { get; set; }
        }
    }
}

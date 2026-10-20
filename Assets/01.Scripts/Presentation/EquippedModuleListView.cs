using System.Collections.Generic;
using System.Text;
using Overclock.Core;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Overclock.Presentation
{
    // 장착 모듈을 "이름 ×개수" 로 보여 주고, 이름에 마우스를 올리면 설명 팝업을 띄운다.
    // 이름을 클릭하면 팝업이 고정되고, 다른 곳을 클릭하면 사라진다.
    public sealed class EquippedModuleListView : MonoBehaviour
    {
        [SerializeField] private TMP_Text listText;
        [SerializeField] private RectTransform tooltip;
        [SerializeField] private TMP_Text tooltipText;

        private readonly List<EquippedModuleGroup> groups = new List<EquippedModuleGroup>();
        private readonly Vector3[] tooltipCorners = new Vector3[4];
        private bool interactable = true;
        private int pinnedIndex = -1;
        private Vector2 pinnedPosition;
        private int shownIndex = -1;

        private void Awake()
        {
            tooltip.gameObject.SetActive(false);
        }

        public void Show(IReadOnlyList<ModuleData> equippedModules)
        {
            groups.Clear();
            groups.AddRange(Group(equippedModules));
            listText.text = BuildListText(groups, equippedModules.Count);
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
            // 캔버스가 Screen Space - Overlay 라 카메라는 null.
            // 링크는 그룹 순서대로 하나씩 넣으므로 링크 번호 = 그룹 번호
            int linkIndex = TMP_TextUtilities.FindIntersectingLink(listText, pointer, null);
            if (linkIndex < 0 || linkIndex >= groups.Count)
            {
                return -1;
            }

            return linkIndex;
        }

        private void ShowTooltip(int index, Vector2 position)
        {
            tooltipText.text = BuildTooltipText(groups[index]);
            // Overlay 캔버스에서는 화면 좌표가 그대로 위치다
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

        internal static string BuildListText(IReadOnlyList<EquippedModuleGroup> groups, int totalCount)
        {
            var builder = new StringBuilder("장착 모듈 " + totalCount);
            for (int i = 0; i < groups.Count; i++)
            {
                builder.Append("\n- <link=\"").Append(i).Append("\">").Append(Label(groups[i])).Append("</link>");
            }

            return builder.ToString();
        }

        internal static string BuildTooltipText(EquippedModuleGroup group)
        {
            return "<b>" + Label(group) + "</b>\n" + group.Module.Description;
        }

        private static string Label(EquippedModuleGroup group)
        {
            return group.Count > 1 ? group.Module.DisplayName + " ×" + group.Count : group.Module.DisplayName;
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

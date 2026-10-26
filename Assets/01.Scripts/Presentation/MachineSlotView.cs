using Overclock.Core;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Overclock.Presentation
{
    // 기계 패널의 슬롯 1칸. 패널이 이 오브젝트를 복제해서 쓴다
    public sealed class MachineSlotView : MonoBehaviour
    {
        [SerializeField] private Image iconImage;
        [SerializeField] private TMP_Text countText;

        public RectTransform Rect => (RectTransform)transform;

        public void Show(ModuleData module, int count)
        {
            iconImage.sprite = module.Icon;
            iconImage.enabled = module.Icon != null;
            countText.text = count > 1 ? "×" + count : "";
            gameObject.SetActive(true);
        }

        public void Hide()
        {
            gameObject.SetActive(false);
        }
    }
}

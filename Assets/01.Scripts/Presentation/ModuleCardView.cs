using System;
using Overclock.Core;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Overclock.Presentation
{
    public sealed class ModuleCardView : MonoBehaviour
    {
        [SerializeField] private TMP_Text nameText;
        [SerializeField] private TMP_Text descriptionText;
        [SerializeField] private TMP_Text familyText;
        [SerializeField] private TMP_Text previewText;
        [SerializeField] private Image iconImage;
        [SerializeField] private Button selectButton;

        private ModuleData module;
        private Action<ModuleData> selected;

        private void Awake()
        {
            selectButton.onClick.AddListener(OnSelectClicked);
        }

        public void Show(ModuleData module, string preview, Action<ModuleData> selected)
        {
            this.module = module;
            this.selected = selected;
            nameText.text = module.DisplayName;
            descriptionText.text = module.Description;
            familyText.text = FamilyStyle.GetLabel(module.Family);
            familyText.color = FamilyStyle.GetColor(module.Family);
            previewText.text = preview;
            iconImage.sprite = module.Icon;
            iconImage.enabled = module.Icon != null;
            gameObject.SetActive(true);
        }

        public void Hide()
        {
            module = null;
            selected = null;
            gameObject.SetActive(false);
        }

        private void OnSelectClicked()
        {
            if (module != null && selected != null)
            {
                selected(module);
            }
        }
    }
}

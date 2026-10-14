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
        [SerializeField] private TMP_Text previewText;
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
            previewText.text = preview;
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

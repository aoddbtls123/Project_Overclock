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

        [Header("다시 받기 회전 (11/07) — 셋 중 하나라도 비면 돌지 않고 바로 바뀐다")]
        [Tooltip("세로축으로 도는 안쪽 판 (앞면 · 뒷면의 부모). 카드 자리 · 크기를 정하는 바깥 (이 오브젝트) 은 돌지 않는다")]
        [SerializeField] private RectTransform flipRoot;
        [Tooltip("배경 · 테두리 · 글자 · 아이콘 · 선택 버튼을 담은 앞면")]
        [SerializeField] private GameObject frontFace;
        [Tooltip("글자 없는 금속판 뒷면 (Flip Root 기준 180° 돌려 둔다). 90° ~ 270° 동안만 켠다")]
        [SerializeField] private GameObject backFace;

        private ModuleData module;
        private Action<ModuleData> selected;

        public bool CanFlip => flipRoot != null && frontFace != null && backFace != null;

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

        /// <summary>선택 버튼을 켜고 끈다. 회전 중에는 꺼서 마우스 · 키보드 제출 모두 이전 카드를 고르지 못하게 한다.</summary>
        public void SetSelectable(bool selectable)
        {
            selectButton.interactable = selectable;
        }

        /// <summary>안쪽 판을 세로축으로 degrees 만큼 돌린다. 90° 이상 270° 미만이면 뒷면만, 그 밖이면 앞면만 보인다.</summary>
        public void SetFlipAngle(float degrees)
        {
            if (!CanFlip)
            {
                return;
            }

            flipRoot.localEulerAngles = new Vector3(0f, degrees, 0f);

            // UI 셰이더는 뒷면을 버리지 않는다. 앞면을 끄지 않으면 뒤집힌 글자가 비쳐 보인다
            bool showBack = degrees >= 90f && degrees < 270f;
            if (frontFace.activeSelf == showBack)
            {
                frontFace.SetActive(!showBack);
            }

            if (backFace.activeSelf != showBack)
            {
                backFace.SetActive(showBack);
            }
        }

        public void ResetFlip()
        {
            SetFlipAngle(0f);
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

using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Overclock.Presentation
{
    // 런 결과 창. 무엇을 보여 주고 버튼이 무엇을 할지는 GrayBoxRunScreen 이 정하고,
    // 이 창은 글자를 채우고 버튼 두 개를 연결하고 켜고 끄기만 한다 (PauseMenuView 와 같은 방식)
    public sealed class ResultPanelView : MonoBehaviour
    {
        [SerializeField] private TMP_Text titleText;
        [SerializeField] private TMP_Text detailText;
        [SerializeField] private Button retryButton;
        [SerializeField] private Button titleButton;

        public bool IsOpen => gameObject.activeSelf;

        public void Bind(Action onRetry, Action onTitle)
        {
            retryButton.onClick.AddListener(() => onRetry());
            titleButton.onClick.AddListener(() => onTitle());
        }

        public void Show(string title, string detail)
        {
            titleText.text = title;
            detailText.text = detail;
            gameObject.SetActive(true);
        }

        public void Hide()
        {
            gameObject.SetActive(false);
        }
    }
}

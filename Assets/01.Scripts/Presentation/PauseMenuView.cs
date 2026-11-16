using System;
using UnityEngine;
using UnityEngine.UI;

namespace Overclock.Presentation
{
    // ESC 일시정지 창. 화면 전체를 덮어 뒤의 버튼과 카드 클릭을 막음.
    // 무엇을 할지는 GrayBoxRunScreen 이 정하고, 이 창은 버튼 세 개를 연결하고 켜고 끄기만 함
    public sealed class PauseMenuView : MonoBehaviour
    {
        [SerializeField] private Button resumeButton;
        [SerializeField] private Button modeSelectButton;
        [SerializeField] private Button quitButton;

        public bool IsOpen => gameObject.activeSelf;

        public void Bind(Action onResume, Action onModeSelect, Action onQuit)
        {
            resumeButton.onClick.AddListener(() => onResume());
            modeSelectButton.onClick.AddListener(() => onModeSelect());
            quitButton.onClick.AddListener(() => onQuit());
        }

        public void Open()
        {
            gameObject.SetActive(true);
        }

        public void Close()
        {
            gameObject.SetActive(false);
        }
    }
}

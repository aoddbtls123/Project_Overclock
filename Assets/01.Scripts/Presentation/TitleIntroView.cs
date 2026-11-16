using UnityEngine;
using UnityEngine.InputSystem;
using TMPro;

namespace Overclock.Presentation
{
    // 타이틀 등장 (11/16): 제목이 한 글자씩 타이핑되고, 끝나면 버튼이 차례로 떠오른다.
    // 타이틀 패널이 켜질 때마다 처음부터. 아무 키 · 클릭이면 바로 끝 모습 (버튼은 다 나타나기 전에는 눌리지 않는다).
    // 일시정지에서 '처음으로' 로 올 수 있어 시간은 unscaled 로 잰다. 게임 상태는 바꾸지 않는다
    public sealed class TitleIntroView : MonoBehaviour
    {
        [SerializeField] private TMP_Text title;
        [Tooltip("나타나는 순서대로 (시작 → 설정 → 종료)")]
        [SerializeField] private RectTransform[] buttons = new RectTransform[0];

        [SerializeField, Min(0f)] private float startDelay = 0.3f;
        [Tooltip("한 글자가 찍히는 간격")]
        [SerializeField, Min(0.01f)] private float charSeconds = 0.09f;
        [Tooltip("타이핑이 끝나고 첫 버튼이 나타나기까지")]
        [SerializeField, Min(0f)] private float buttonsDelay = 0.25f;
        [SerializeField, Min(0f)] private float buttonStagger = 0.12f;
        [SerializeField, Min(0.01f)] private float buttonFadeSeconds = 0.25f;
        [Tooltip("버튼이 아래에서 떠오르는 거리 (px)")]
        [SerializeField] private float buttonRise = 16f;

        private CanvasGroup[] groups = new CanvasGroup[0];
        private Vector2[] restPositions = new Vector2[0];
        private float elapsed;
        private bool playing;
        private bool prepared;

        private void Prepare()
        {
            if (prepared)
            {
                return;
            }

            prepared = true;
            groups = new CanvasGroup[buttons.Length];
            restPositions = new Vector2[buttons.Length];
            for (int i = 0; i < buttons.Length; i++)
            {
                if (buttons[i] == null)
                {
                    continue;
                }

                restPositions[i] = buttons[i].anchoredPosition;
                groups[i] = buttons[i].GetComponent<CanvasGroup>();
                if (groups[i] == null)
                {
                    groups[i] = buttons[i].gameObject.AddComponent<CanvasGroup>();
                }
            }
        }

        private void OnEnable()
        {
            Prepare();
            elapsed = 0f;
            playing = true;
            Apply();
        }

        private void OnDisable()
        {
            Finish();
        }

        private void Update()
        {
            if (!playing)
            {
                return;
            }

            // 켜진 그 프레임의 클릭 (시작 화면으로 돌아온 클릭) 은 스킵으로 보지 않는다
            if (elapsed > 0f && IsSkipPressed())
            {
                Finish();
                return;
            }

            elapsed += Time.unscaledDeltaTime;
            Apply();
        }

        private static bool IsSkipPressed()
        {
            Keyboard keyboard = Keyboard.current;
            Mouse mouse = Mouse.current;
            return (keyboard != null && keyboard.anyKey.wasPressedThisFrame) || (mouse != null && mouse.leftButton.wasPressedThisFrame);
        }

        private float TypingEnd => startDelay + (title != null ? title.text.Length : 0) * charSeconds;

        private void Apply()
        {
            if (title != null)
            {
                int visible = Mathf.Clamp(Mathf.FloorToInt((elapsed - startDelay) / charSeconds) + 1, 0, title.text.Length);
                title.maxVisibleCharacters = elapsed < startDelay ? 0 : visible;
            }

            float buttonsStart = TypingEnd + buttonsDelay;
            bool done = true;
            for (int i = 0; i < buttons.Length; i++)
            {
                float p = Mathf.Clamp01((elapsed - buttonsStart - i * buttonStagger) / buttonFadeSeconds);
                SetButton(i, p);
                done &= p >= 1f;
            }

            if (done && elapsed >= TypingEnd)
            {
                playing = false;
            }
        }

        private void Finish()
        {
            playing = false;
            if (title != null)
            {
                title.maxVisibleCharacters = 99999;
            }

            for (int i = 0; i < buttons.Length; i++)
            {
                SetButton(i, 1f);
            }
        }

        private void SetButton(int index, float progress)
        {
            if (index >= groups.Length || groups[index] == null)
            {
                return;
            }

            float eased = 1f - (1f - progress) * (1f - progress);
            groups[index].alpha = eased;
            groups[index].interactable = progress >= 1f;
            groups[index].blocksRaycasts = progress >= 1f;
            buttons[index].anchoredPosition = restPositions[index] - new Vector2(0f, buttonRise * (1f - eased));
        }
    }
}

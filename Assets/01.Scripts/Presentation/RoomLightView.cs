using UnityEngine;
using UnityEngine.UI;

namespace Overclock.Presentation
{
    // 배경 방의 조명 (11/16). 새 런 등장 때: 암전 → 어두운 방이 드러남 → 전등이 두 번 깜빡이며 켜지고 방이 밝아짐.
    // 타이틀 = 어두운 방 (전등 꺼짐) · 이어하기 · 스킵 · 즉시 = 바로 밝은 방. 게임 상태는 바꾸지 않음.
    // 방 그림은 밝게 그려져 있고 어두움은 위에 덮은 검은 판의 불투명도로만 만듦 (켜진 전등은 그 판보다 앞이라 늘 밝음).
    // 시계는 Time.deltaTime 이라 일시정지 중에는 멈춤 (등장 패널과 같음). 시간에는 연출 속도 배율을 곱함
    public sealed class RoomLightView : MonoBehaviour
    {
        [Tooltip("화면 전체 검은 판 (캔버스 맨 앞 · 클릭 통과). 등장 처음에만 씀")]
        [SerializeField] private Image blackout;
        [Tooltip("방 위에 덮는 검은 판 (방 · 꺼진 전등 앞, 켜진 전등 뒤)")]
        [SerializeField] private Image darkness;
        [Tooltip("켜진 전등 그림")]
        [SerializeField] private Image lampOn;
        [Tooltip("기계 · 패널 앞에 덮는 검은 판 (ShakeRoot 바로 뒤 · 모듈 선택 · 결과 · 일시정지 창보다 뒤). 방과 같이 어두웠다 밝아짐")]
        [SerializeField] private Image machineShade;

        [Header("밝기 (검은 판 불투명도)")]
        [SerializeField, Range(0f, 1f)] private float darkAlpha = 0.85f;
        [SerializeField, Range(0f, 1f)] private float litAlpha = 0.5f;
        [Tooltip("전등이 꺼져 있을 때 기계 · 패널 위 검은 판 (켜지면 0)")]
        [SerializeField, Range(0f, 1f)] private float machineDarkAlpha = 0.7f;
        [Tooltip("첫 깜빡임 순간의 밝기 (0 = 꺼짐 · 1 = 다 켜짐). 그 뒤 계속 켜지면서 1 까지 오름")]
        [SerializeField, Range(0f, 1f)] private float flickerLight = 0.7f;

        [Header("등장 시간 (초 · 연출 속도 배율을 곱한다)")]
        [Tooltip("완전히 깜깜한 채로 있는 시간")]
        [SerializeField, Min(0f)] private float blackHoldSeconds = 0.35f;
        [Tooltip("깜깜함이 걷혀 어두운 방이 드러나는 시간")]
        [SerializeField, Min(0.01f)] private float blackFadeSeconds = 0.45f;
        [Tooltip("등장 시작부터 전등이 처음 켜지는 때")]
        [SerializeField, Min(0f)] private float lampOnAt = 1.2f;
        [Tooltip("켜짐 · 꺼짐 깜빡임 한 칸 (두 번 깜빡인 뒤 계속 켜짐)")]
        [SerializeField, Min(0.01f)] private float flickerSeconds = 0.06f;
        [Tooltip("전등이 계속 켜진 뒤 방이 밝아지는 시간")]
        [SerializeField, Min(0.01f)] private float brightenSeconds = 0.3f;

        private bool playing;
        private float elapsed;
        private float multiplier = 1f;

        private void Awake()
        {
            ShowDark();
        }

        /// <summary>타이틀 · 모드 선택: 전등 꺼진 어두운 방.</summary>
        public void ShowDark()
        {
            playing = false;
            Apply(0f, false, 0f);
        }

        /// <summary>이어하기 · 스킵 · 즉시: 전등 켜진 밝은 방.</summary>
        public void ShowLit()
        {
            playing = false;
            Apply(0f, true, 1f);
        }

        /// <summary>새 런 등장. timeMultiplier 0 이하 (즉시) 면 바로 밝은 방.</summary>
        public void PlayLightsOn(float timeMultiplier)
        {
            if (timeMultiplier <= 0f)
            {
                ShowLit();
                return;
            }

            multiplier = timeMultiplier;
            elapsed = 0f;
            playing = true;
            Evaluate();
        }

        private void OnDisable()
        {
            // 꺼질 때 깜깜한 판이 남아 화면을 가리지 않게
            if (playing)
            {
                ShowLit();
            }
        }

        private void Update()
        {
            if (!playing)
            {
                return;
            }

            elapsed += Time.deltaTime;
            Evaluate();
        }

        private void Evaluate()
        {
            float t = elapsed / multiplier;
            float black = t < blackHoldSeconds ? 1f : 1f - Mathf.Clamp01((t - blackHoldSeconds) / blackFadeSeconds);

            // 켜짐 → 꺼짐 → 켜짐 (한 칸씩) 뒤로는 계속 켜짐. 켜진 순간 방 · 기계가 같이 밝아지고 (깜빡임도 같이), 계속 켜지면 다 밝아짐
            float sinceOn = t - lampOnAt;
            bool lit = sinceOn >= 0f && !(sinceOn >= flickerSeconds && sinceOn < flickerSeconds * 2f);
            float steadyAt = flickerSeconds * 2f;
            float brighten = sinceOn < steadyAt ? 0f : Mathf.Clamp01((sinceOn - steadyAt) / brightenSeconds);
            float eased = 1f - (1f - brighten) * (1f - brighten);
            float light = lit ? Mathf.Lerp(flickerLight, 1f, eased) : 0f;
            Apply(black, lit, light);

            if (brighten >= 1f && black <= 0f)
            {
                playing = false;
            }
        }

        // light 0 = 전등 꺼진 어두운 방 · 1 = 다 켜진 방
        private void Apply(float blackAlpha, bool lampLit, float light)
        {
            SetAlpha(blackout, blackAlpha);
            SetAlpha(darkness, Mathf.Lerp(darkAlpha, litAlpha, light));
            SetAlpha(machineShade, Mathf.Lerp(machineDarkAlpha, 0f, light));
            SetAlpha(lampOn, lampLit ? 1f : 0f);
        }

        private static void SetAlpha(Image image, float alpha)
        {
            if (image == null)
            {
                return;
            }

            Color color = image.color;
            color.a = alpha;
            image.color = color;
        }
    }
}

using DG.Tweening;
using Overclock.Core;
using UnityEngine;
using UnityEngine.UI;

namespace Overclock.Presentation
{
    // 뽑기 공개 연출의 겉모습 (SR 달아오름 · 흔들림 · 플래시 · 아이콘 빛 · 펀치).
    // 언제 무엇을 할지는 DrawRevealView 가 정한다. 이 창은 그리기만 하고 결과 · 콜백은 모른다.
    // 달아오름은 DrawRevealView 의 시계(SetHeat)로 그려 스킵 · 일시정지와 늘 맞고,
    // 공개 뒤 효과만 DOTween 으로 흘린다 (scaled time — 일시정지면 같이 멈춤).
    // 흔들림은 달아오름 · 공개 순간 모두 Perlin 노이즈로 만든다 (DOTween 의 Shake 는 UnityEngine.Random 을 씀).
    // 흔들림 · 플래시는 값을 넣을 때마다 접근성 스위치를 확인한다 — 효과 도중에 꺼도 바로 멈춘다.
    // 쉬는 상태 = 흔들림 0 · 테두리 · 플래시 · 빛 투명 · 아이콘 원래 크기. StopAll 은 언제나 이 상태로 돌린다
    public sealed class RevealEffectsView : MonoBehaviour
    {
        private const int GlowTextureSize = 128;
        private const int EdgeTextureSize = 64;
        private const float EdgeWidth = 0.35f;
        private const float KickNoiseCycles = 8f;

        [SerializeField] private RevealEffectSettings settings;
        [SerializeField] private RectTransform shakeRoot;
        [SerializeField] private Image heatFrame;
        [SerializeField] private Image flashImage;
        [SerializeField] private Image iconGlow;
        [SerializeField] private RectTransform partIcon;

        private bool shakeEnabled = true;
        private bool flashEnabled = true;
        private Vector2 restShakePosition;
        private Vector3 restIconScale;
        private Sequence revealSequence;
        private Texture2D glowTexture;
        private Texture2D edgeTexture;
        private Sprite glowSprite;
        private Sprite edgeSprite;

        private void Awake()
        {
            restShakePosition = shakeRoot.anchoredPosition;
            restIconScale = partIcon.localScale;

            // 빛 모양은 코드로 만든다 (에셋 0원 · 파일 없음)
            glowTexture = CreateRadialTexture(GlowTextureSize);
            edgeTexture = CreateEdgeTexture(EdgeTextureSize);
            glowSprite = CreateSprite(glowTexture);
            edgeSprite = CreateSprite(edgeTexture);
            iconGlow.sprite = glowSprite;
            heatFrame.sprite = edgeSprite;

            // 화면 전체를 덮는 효과 이미지는 투명해도 클릭을 막는다. 씬 설정과 관계없이 클릭을 통과시킨다
            heatFrame.raycastTarget = false;
            flashImage.raycastTarget = false;
            iconGlow.raycastTarget = false;

            ResetToRest();
        }

        private void OnDisable()
        {
            StopAll();
        }

        private void OnDestroy()
        {
            KillRevealSequence();
            Destroy(glowSprite);
            Destroy(edgeSprite);
            Destroy(glowTexture);
            Destroy(edgeTexture);
        }

        /// <summary>접근성: 흔들림 · 섬광(화면 플래시)을 끈다. 달아오름 색과 아이콘 빛은 남는다.</summary>
        public void SetMotionOptions(bool shake, bool flash)
        {
            shakeEnabled = shake;
            flashEnabled = flash;
            if (!shakeEnabled)
            {
                shakeRoot.anchoredPosition = restShakePosition;
            }

            if (!flashEnabled)
            {
                flashImage.color = Transparent(settings.FlashColor);
            }
        }

        /// <summary>SR 연출 진행률(0~1)에 맞춰 테두리를 달구고 화면을 흔든다. 같은 진행률이면 같은 모습이다.</summary>
        public void SetHeat(float progress)
        {
            float clamped = Mathf.Clamp01(progress);
            float ramp = Mathf.Clamp01(clamped / settings.HeatRampRatio);

            // 처음엔 천천히, 끝에 갈수록 빠르게 달아오른다
            float heat = ramp * ramp;
            heatFrame.color = Color.Lerp(settings.HeatStartColor, settings.HeatEndColor, heat);

            if (shakeEnabled)
            {
                // 난수 대신 Perlin 노이즈 — 진행률이 같으면 흔들림도 같다
                float t = clamped * settings.ShakeCycles;
                float x = Mathf.PerlinNoise(t, 0.31f) * 2f - 1f;
                float y = Mathf.PerlinNoise(0.73f, t) * 2f - 1f;
                shakeRoot.anchoredPosition = restShakePosition + new Vector2(x, y) * (settings.ShakeStrength * heat);
            }
        }

        /// <summary>
        /// 공개 순간 효과. R = 아이콘 펀치 · SR = 펀치 + 아이콘 빛 + 플래시 + 짧은 흔들림 + 테두리 잔광 · N = 없음.
        /// 끝나면 쉬는 상태로 돌아간다. 연출을 스킵했을 때는 부르지 않는다 (DrawRevealView).
        /// </summary>
        public void PlayReveal(Grade grade)
        {
            // 달아오른 테두리 색은 잔광으로 서서히 꺼야 하므로 여기서는 흔들림 · 크기만 되돌린다
            KillRevealSequence();
            shakeRoot.anchoredPosition = restShakePosition;
            partIcon.localScale = restIconScale;
            iconGlow.rectTransform.localScale = Vector3.one;

            if (grade == Grade.N)
            {
                ResetToRest();
                return;
            }

            bool superRare = grade == Grade.SR;
            float punch = superRare ? settings.SuperRarePunch : settings.RarePunch;
            revealSequence = DOTween.Sequence();
            revealSequence.Insert(0f, partIcon.DOPunchScale(Vector3.one * punch, settings.PunchSeconds, 8, 0.5f));

            if (superRare)
            {
                iconGlow.color = settings.IconGlowColor;
                iconGlow.rectTransform.localScale = Vector3.one * settings.IconGlowStartScale;
                revealSequence.Insert(0f, iconGlow.DOFade(0f, settings.AfterglowSeconds).SetEase(Ease.OutQuad));
                revealSequence.Insert(0f, iconGlow.rectTransform.DOScale(settings.IconGlowEndScale, settings.AfterglowSeconds).SetEase(Ease.OutQuad));
                revealSequence.Insert(0f, heatFrame.DOFade(0f, settings.AfterglowSeconds));

                // 플래시 · 짧은 흔들림은 스위치가 꺼져 있으면 값을 넣지 않는다 (ApplyFlash · ApplyKick)
                ApplyFlash(settings.FlashColor.a);
                revealSequence.Insert(0f, DOTween.To(() => settings.FlashColor.a, ApplyFlash, 0f, settings.FlashSeconds));
                revealSequence.Insert(0f, DOTween.To(() => 0f, ApplyKick, 1f, settings.KickSeconds));
            }

            revealSequence.OnComplete(OnRevealComplete);
        }

        /// <summary>
        /// 짧은 흔들림만 (클리어 분출 · 11/06). 플래시 · 아이콘 · 테두리는 건드리지 않는다 — 평소 SR 연출을 되풀이하지 않게.
        /// 흔들림 끄기면 값을 넣지 않는다. timeMultiplier = 시간 배율 (빠름 0.5)
        /// </summary>
        public void PlayKick(float timeMultiplier)
        {
            KillRevealSequence();
            shakeRoot.anchoredPosition = restShakePosition;
            float multiplier = timeMultiplier > 0f ? timeMultiplier : 1f;
            revealSequence = DOTween.Sequence();
            revealSequence.Insert(0f, DOTween.To(() => 0f, ApplyKick, 1f, settings.KickSeconds * multiplier));
            revealSequence.OnComplete(OnRevealComplete);
        }

        /// <summary>진행 중인 효과를 모두 멈추고 쉬는 상태로 돌린다 (새 연출 · 스킵 · 런 버림 · 모듈 선택 뒤).</summary>
        public void StopAll()
        {
            KillRevealSequence();
            ResetToRest();
        }

        private void ApplyFlash(float alpha)
        {
            if (!flashEnabled)
            {
                return;
            }

            Color color = settings.FlashColor;
            color.a = alpha;
            flashImage.color = color;
        }

        private void ApplyKick(float progress)
        {
            if (!shakeEnabled)
            {
                return;
            }

            // 줄어들며 원래 위치에서 끝나는 짧은 흔들림
            float strength = settings.KickStrength * (1f - Mathf.Clamp01(progress));
            float t = progress * KickNoiseCycles;
            float x = Mathf.PerlinNoise(t, 0.57f) * 2f - 1f;
            float y = Mathf.PerlinNoise(0.19f, t) * 2f - 1f;
            shakeRoot.anchoredPosition = restShakePosition + new Vector2(x, y) * strength;
        }

        private void OnRevealComplete()
        {
            revealSequence = null;
            ResetToRest();
        }

        private void KillRevealSequence()
        {
            if (revealSequence != null && revealSequence.IsActive())
            {
                revealSequence.Kill();
            }

            revealSequence = null;
        }

        private void ResetToRest()
        {
            // 씬을 내릴 때(Play 종료 등)는 다른 오브젝트가 먼저 파괴될 수 있다. 파괴된 대상은 건드리지 않는다
            if (shakeRoot == null || partIcon == null || heatFrame == null || flashImage == null || iconGlow == null)
            {
                return;
            }

            shakeRoot.anchoredPosition = restShakePosition;
            partIcon.localScale = restIconScale;
            heatFrame.color = Transparent(settings.HeatStartColor);
            flashImage.color = Transparent(settings.FlashColor);
            iconGlow.color = Transparent(settings.IconGlowColor);
            iconGlow.rectTransform.localScale = Vector3.one;
        }

        private static Color Transparent(Color color)
        {
            color.a = 0f;
            return color;
        }

        private static Texture2D CreateRadialTexture(int size)
        {
            // 가운데가 가장 밝고 가장자리로 갈수록 투명해지는 둥근 빛
            var pixels = new Color32[size * size];
            float half = (size - 1) * 0.5f;
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dx = (x - half) / half;
                    float dy = (y - half) / half;
                    float alpha = Mathf.Clamp01(1f - Mathf.Sqrt(dx * dx + dy * dy));
                    pixels[y * size + x] = new Color32(255, 255, 255, (byte)Mathf.RoundToInt(alpha * alpha * 255f));
                }
            }

            return CreateTexture(size, pixels);
        }

        private static Texture2D CreateEdgeTexture(int size)
        {
            // 화면 가장자리가 가장 진하고 안쪽으로 갈수록 투명해지는 테두리 (전체 화면에 늘려 쓴다)
            var pixels = new Color32[size * size];
            float half = (size - 1) * 0.5f;
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float toEdge = Mathf.Min(Mathf.Min(x, size - 1 - x), Mathf.Min(y, size - 1 - y)) / half;
                    float alpha = Mathf.Clamp01(1f - toEdge / EdgeWidth);
                    pixels[y * size + x] = new Color32(255, 255, 255, (byte)Mathf.RoundToInt(alpha * alpha * 255f));
                }
            }

            return CreateTexture(size, pixels);
        }

        private static Texture2D CreateTexture(int size, Color32[] pixels)
        {
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
            texture.wrapMode = TextureWrapMode.Clamp;
            texture.SetPixels32(pixels);
            texture.Apply();
            return texture;
        }

        private static Sprite CreateSprite(Texture2D texture)
        {
            return Sprite.Create(texture, new Rect(0f, 0f, texture.width, texture.height), new Vector2(0.5f, 0.5f), 100f);
        }
    }
}

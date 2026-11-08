using DG.Tweening;
using Overclock.Core;
using UnityEngine;
using UnityEngine.UI;

namespace Overclock.Presentation
{
    // 기계 몸통의 계수기 창 (11/06 · 기계연출 구상 §13-4). 생산량을 자리 바퀴로 보여 준다 (예: 0042).
    // 값은 ProductionCounterView 가 넘긴다 — 이 뷰는 보이는 값을 부드럽게 따라가 바퀴를 굴릴 뿐 게임 상태를 모른다.
    // 윗자리는 아랫자리가 9 → 0 으로 넘어가는 동안에만 같이 돈다 (실제 계수기와 같은 올림).
    // 다 세었을 때의 테두리 색은 바퀴가 목표에 멈춘 뒤에 켠다 (바퀴가 도는 중에 색이 먼저 끝나지 않게).
    // 값이 0 으로 돌아가면 (새 라운드 · 새 런) 계수기는 뒤로 돌지 않고 바퀴를 앞으로 촤르륵 돌려 0000 에 멈춘다 (11/06 사용자).
    // 시간은 scaled time (일시정지면 멈춤)
    public sealed class MechanicalCounterView : MonoBehaviour
    {
        [SerializeField] private HarvestDisplaySettings settings;
        [Tooltip("자리 바퀴의 숫자 띠. 아랫자리부터 (일 · 십 · 백 · 천). 띠 안에는 0~9 다음에 0 이 한 번 더, 위에서 아래로 Digit Height 간격으로 놓인다")]
        [SerializeField] private RectTransform[] digitStrips;
        [Tooltip("숫자 한 칸의 높이 (px). 띠를 이만큼 올리면 다음 숫자가 창 가운데에 온다")]
        [SerializeField, Min(1f)] private float digitHeight = 58f;
        [Tooltip("계수기 테두리. 다 세었을 때 등급 색으로 잠깐 바뀐다 (튀는 등급만). 비어 있으면 색 변화 없음")]
        [SerializeField] private Image frame;

        private float playbackSpeed = 1f;
        private float shownValue;
        private float targetValue;
        private Color frameRestColor = Color.white;
        private Tween flashTween;
        private bool flashPending;
        private Grade pendingFlashGrade;
        private bool flashLocked;
        private bool resetting;
        private float resetElapsed;
        private float[] resetStart;
        private float[] resetEnd;

        private void Awake()
        {
            if (frame != null)
            {
                frameRestColor = frame.color;
            }

            ApplyWheels();
        }

        private void OnDisable()
        {
            CompleteNow();
        }

        private void OnDestroy()
        {
            KillFlash();
        }

        private void Update()
        {
            if (resetting)
            {
                resetElapsed += Time.deltaTime * playbackSpeed;
                ApplyResetWheels();
                if (resetElapsed >= ResetSeconds)
                {
                    StopReset();
                }

                return;
            }

            if (shownValue == targetValue)
            {
                return;
            }

            // 남은 거리의 일정 비율씩 따라간다 — 멀면 빨리, 가까우면 천천히 멈춘다
            float follow = settings != null ? settings.CounterFollowSeconds : 0.06f;
            float step = 1f - Mathf.Exp(-Time.deltaTime * playbackSpeed / follow);
            shownValue += (targetValue - shownValue) * step;
            if (Mathf.Abs(targetValue - shownValue) < 0.01f)
            {
                shownValue = targetValue;
            }

            ApplyWheels();
            if (shownValue == targetValue && flashPending)
            {
                flashPending = false;
                StartFlash(pendingFlashGrade);
            }
        }

        /// <summary>바퀴가 도는 중인가 (따라가는 중 · 0000 으로 돌아가는 중). 런 실패 연출이 마지막 숫자를 기다릴 때 쓴다.</summary>
        public void SetPlaybackSpeed(float speed)
        {
            playbackSpeed = Mathf.Max(0f, speed);
            if (playbackSpeed == 0f) CompleteNow();
        }

        public bool IsMoving => resetting || shownValue != targetValue;

        /// <summary>
        /// 이 값까지 바퀴를 굴린다. 계수기는 뒤로 돌지 않는다 — 0 으로 돌아가면 바퀴를 앞으로 돌려 0000 에 멈추고,
        /// 0 이 아닌 작은 값 (이어하기) 이면 바로 맞춘다.
        /// </summary>
        public void SetTarget(float value)
        {
            float clamped = Mathf.Clamp(value, 0f, MaxValue);
            if (resetting)
            {
                if (clamped <= 0f)
                {
                    return;
                }

                StopReset();
            }

            targetValue = clamped;
            if (clamped < shownValue)
            {
                if (clamped <= 0f)
                {
                    StartReset();
                    return;
                }

                shownValue = clamped;
                ApplyWheels();
            }
        }

        /// <summary>따라가기 · 테두리 색만 바로 끝낸다. 0000 으로 돌아가는 바퀴는 그대로 둔다 (같은 0 을 다시 받는 Refresh 가 끊지 않게).</summary>
        public void CompleteFollow()
        {
            if (resetting)
            {
                return;
            }

            CompleteNow();
        }

        /// <summary>따라가는 중 · 색이 바뀐 중이면 바로 최종 모습으로 (다음 뽑기 시작 · 스킵 · 런 이탈). 기다리던 테두리 색은 취소한다.</summary>
        public void CompleteNow()
        {
            resetting = false;
            flashPending = false;
            KillFlash();
            if (frame != null)
            {
                frame.color = frameRestColor;
            }

            shownValue = targetValue;
            ApplyWheels();
        }

        /// <summary>테두리를 등급 색으로 바꿨다가 돌려놓는다. 바퀴가 아직 도는 중이면 멈춘 뒤에 켠다. 잠겨 있으면 (클리어 연출) 무시한다.</summary>
        public void Flash(Grade grade)
        {
            if (frame == null || flashLocked)
            {
                return;
            }

            if (shownValue != targetValue)
            {
                flashPending = true;
                pendingFlashGrade = grade;
                return;
            }

            StartFlash(grade);
        }

        /// <summary>
        /// 바퀴를 기다리지 않고 바로 번쩍인다 (클리어 연출 · 11/06). 기다리던 생산량 번쩍임은 버린다.
        /// lockOthers = true 면 UnlockFlash 까지 생산량 쪽 Flash 를 받지 않는다 — 마지막 생산량의 등급 색이 금색을 덮지 않게
        /// </summary>
        public void FlashNow(Grade grade, bool lockOthers)
        {
            flashPending = false;
            flashLocked = lockOthers;
            if (frame != null)
            {
                StartFlash(grade);
            }
        }

        /// <summary>FlashNow 의 잠금을 푼다 (클리어 연출 끝 · 취소).</summary>
        public void UnlockFlash()
        {
            flashLocked = false;
        }

        private void StartFlash(Grade grade)
        {
            KillFlash();
            Color gradeColor = GradeStyle.GetColor(grade);
            float seconds = settings != null ? settings.PunchSeconds : 0.25f;
            frame.color = gradeColor;
            flashTween = DOTween.To(t => frame.color = Color.Lerp(gradeColor, frameRestColor, t), 0f, 1f, seconds)
                .OnComplete(() =>
                {
                    flashTween = null;
                    frame.color = frameRestColor;
                });
        }

        private float ResetSeconds => settings != null ? settings.CounterResetSeconds : 0.7f;

        private void StartReset()
        {
            if (digitStrips == null || digitStrips.Length == 0)
            {
                shownValue = 0f;
                return;
            }

            // 기다리던 테두리 색 · 켜진 색은 앞 라운드 것이라 끈다
            flashPending = false;
            KillFlash();
            if (frame != null)
            {
                frame.color = frameRestColor;
            }

            int count = digitStrips.Length;
            if (resetStart == null || resetStart.Length != count)
            {
                resetStart = new float[count];
                resetEnd = new float[count];
            }

            int turns = settings != null ? settings.CounterResetTurns : 1;
            float place = 1f;
            for (int i = 0; i < count; i++)
            {
                // 지금 자리 (0 이상 10 미만) 에서 앞으로 돌아 10 의 배수 (= 0) 에 멈춘다
                resetStart[i] = GetDigitPosition(shownValue, place, i == 0);
                resetEnd[i] = 10f * (1 + turns);
                place *= 10f;
            }

            resetting = true;
            resetElapsed = 0f;
            shownValue = 0f;
            targetValue = 0f;
        }

        private void StopReset()
        {
            resetting = false;
            shownValue = targetValue;
            ApplyWheels();
        }

        private void ApplyResetWheels()
        {
            int count = digitStrips.Length;
            float stagger = settings != null ? settings.CounterResetStagger : 0.4f;
            for (int i = 0; i < count; i++)
            {
                if (digitStrips[i] == null)
                {
                    continue;
                }

                // 윗자리일수록 먼저 멈춘다 (일의 자리 = 전체 시간)
                float share = count > 1 ? (float)i / (count - 1) : 0f;
                float seconds = ResetSeconds * (1f - stagger * share);
                float t = Mathf.Clamp01(resetElapsed / seconds);
                float eased = 1f - (1f - t) * (1f - t) * (1f - t);
                float position = resetStart[i] + (resetEnd[i] - resetStart[i]) * eased;
                digitStrips[i].anchoredPosition = new Vector2(0f, Mathf.Repeat(position, 10f) * digitHeight);
            }
        }

        // 바퀴 수만큼의 최댓값 (4자리면 9999). 넘는 값은 9999 에서 멈춘다
        private float MaxValue
        {
            get
            {
                int digits = digitStrips != null ? digitStrips.Length : 0;
                float max = 1f;
                for (int i = 0; i < digits; i++)
                {
                    max *= 10f;
                }

                return max - 1f;
            }
        }

        private void ApplyWheels()
        {
            if (digitStrips == null)
            {
                return;
            }

            float place = 1f;
            for (int i = 0; i < digitStrips.Length; i++)
            {
                if (digitStrips[i] != null)
                {
                    float position = GetDigitPosition(shownValue, place, i == 0);
                    digitStrips[i].anchoredPosition = new Vector2(0f, position * digitHeight);
                }

                place *= 10f;
            }
        }

        // 0 이상 10 미만. 정수 부분 = 창에 보이는 숫자, 소수 부분 = 다음 숫자로 넘어간 정도 (10 근처 = 띠 끝의 0)
        private static float GetDigitPosition(float value, float place, bool lowest)
        {
            if (lowest)
            {
                return Mathf.Repeat(value, 10f);
            }

            float digit = Mathf.Repeat(Mathf.Floor(value / place), 10f);
            float belowPlace = Mathf.Repeat(value, place);
            // 아랫자리가 전부 9 를 지나 0 으로 넘어가는 마지막 1 동안만 같이 돈다 (예: 십의 자리는 x9.0 → x10.0 사이)
            float carry = Mathf.Max(0f, belowPlace - (place - 1f));
            return digit + carry;
        }

        private void KillFlash()
        {
            if (flashTween != null && flashTween.IsActive())
            {
                flashTween.Kill();
            }

            flashTween = null;
        }
    }
}

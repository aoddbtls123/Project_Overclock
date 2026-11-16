using System;
using System.Collections.Generic;
using UnityEngine;

namespace Overclock.Presentation
{
    // 다시 받기한 카드를 세로축으로 한 바퀴 돌려 새 제안을 보여 줌 (11/07 · 금속 명판이 돌아 걸쇠에 멎는 느낌).
    // 새 제안은 부르는 쪽 (GrayBoxRunScreen) 이 이미 확정하고 저장했음. 여기서는 언제 새 내용을 보여 줄지만 정함 —
    // 각 카드는 뒷면이 보이기 시작할 때 (90°) 내용을 바꾸고, 270° 부터 새 앞면이 나와 정면 (360°) 에서 멈춤.
    // 카드 선택 잠금도 여기서 걺 · 풂 (회전을 시작할 때 걸고, 끝 · 스킵 · 취소 · 꺼짐 복구 어느 길로든 풂).
    // 시계는 Time.deltaTime 이라 일시정지 (timeScale 0) 중에는 회전 · 시작 지연이 그대로 멈춤.
    // 끝 콜백은 자연히 끝남 · Skip · 즉시에서 정확히 한 번. Cancel · 꺼짐은 콜백 없이 새 카드 · 정면 · 선택 가능으로만 되돌림
    public sealed class OfferFlipView : MonoBehaviour
    {
        private const float FullTurnDegrees = 360f;
        private const float BackFaceStartDegrees = 90f;

        [SerializeField] private OfferFlipSettings settings;

        private readonly List<Slot> slots = new List<Slot>();
        private readonly List<ModuleCardView> lockedCards = new List<ModuleCardView>();
        private Action finished;
        private float elapsed;
        private float spinSeconds;
        private float staggerSeconds;
        private bool playing;
        private bool restorePending;

        public bool IsPlaying => playing;

        /// <summary>마지막 끝이 스킵이었는가. 스킵이면 연출 소리 (걸림) 를 내지 않음.</summary>
        public bool LastFinishSkipped { get; private set; }

        /// <summary>
        /// cards[i] 를 돌리고 뒷면 구간에서 showNewContent[i] 로 새 내용을 넣음. 꺼져 있거나 돌 수 없는 카드는 바로 새 내용을 넣음.
        /// 끝날 때까지 cards 의 선택 버튼을 끔. timeMultiplier 0 (즉시) 이면 돌지 않고 이 호출 안에서 끝 콜백까지 부름.
        /// </summary>
        public void Play(IReadOnlyList<ModuleCardView> cards, IReadOnlyList<Action> showNewContent, float timeMultiplier, Action onFinished)
        {
            Cancel();
            finished = onFinished;
            for (int i = 0; i < cards.Count; i++)
            {
                cards[i].SetSelectable(false);
                lockedCards.Add(cards[i]);
                if (cards[i].gameObject.activeSelf && cards[i].CanFlip)
                {
                    slots.Add(new Slot(cards[i], showNewContent[i]));
                }
                else
                {
                    showNewContent[i]();
                }
            }

            // 화면 왼쪽 카드부터 돎. 씬 배열 순서 (오른쪽부터 저장돼 있음) 에 기대지 않음
            slots.Sort((a, b) => a.Card.transform.position.x.CompareTo(b.Card.transform.position.x));

            float multiplier = Mathf.Max(0f, timeMultiplier);
            spinSeconds = settings != null ? settings.SpinSeconds * multiplier : 0f;
            staggerSeconds = settings != null ? settings.StaggerSeconds * multiplier : 0f;
            elapsed = 0f;
            playing = true;
            if (spinSeconds <= 0f || slots.Count == 0)
            {
                Finish(false);
            }
        }

        /// <summary>남은 회전 · 시작 지연을 건너뛰고 새 카드를 정면으로 둔 뒤 끝 콜백을 부름 (LastFinishSkipped = true).</summary>
        public void Skip()
        {
            if (playing)
            {
                Finish(true);
            }
        }

        /// <summary>콜백 없이 멈춤 (런 이탈 · 새 런 · 카드 다시 표시). 이미 확정된 새 내용 · 정면 · 선택 가능으로 맞춤.</summary>
        public void Cancel()
        {
            playing = false;
            finished = null;
            RestoreCards();
        }

        private void OnDisable()
        {
            // 패널이 꺼지는 중에는 자식의 켜고 끄기를 바꾸지 않음. 회전 · 콜백만 버리고, 다시 켜진 뒤 첫 Update 에서 맞춤
            if (playing)
            {
                playing = false;
                finished = null;
                restorePending = true;
            }
        }

        private void Update()
        {
            if (restorePending)
            {
                RestoreCards();
            }

            if (!playing)
            {
                return;
            }

            float delta = Time.deltaTime;
            if (delta <= 0f)
            {
                return;
            }

            elapsed += delta;
            bool allStopped = true;
            for (int i = 0; i < slots.Count; i++)
            {
                Slot slot = slots[i];
                float progress = (elapsed - staggerSeconds * i) / spinSeconds;
                if (progress <= 0f)
                {
                    allStopped = false;
                    continue;
                }

                if (progress >= 1f)
                {
                    slot.ShowNewContent();
                    slot.Card.ResetFlip();
                    continue;
                }

                // 각도를 매 프레임 진행률로 계산함 (0° 와 360° 가 같은 자세라도 한 바퀴를 실제로 돎).
                // 긴 프레임이 뒷면 구간을 건너뛰어도 내용은 앞면이 다시 보이기 전에 바뀜 (내용 → 각도 순서)
                float angle = FullTurnDegrees * Ease(progress);
                if (angle >= BackFaceStartDegrees)
                {
                    slot.ShowNewContent();
                }

                slot.Card.SetFlipAngle(angle);
                allStopped = false;
            }

            if (allStopped)
            {
                Finish(false);
            }
        }

        private float Ease(float progress)
        {
            // 처음엔 빠르게, 끝으로 갈수록 감속. 끝 속도를 조금 남겨 정면에서 딱 걸려 멈추게 함 (튕김 없음)
            float latch = settings != null ? settings.LatchSpeed : 0f;
            float remaining = 1f - progress;
            float easeOut = 1f - remaining * remaining * remaining;
            return latch * progress + (1f - latch) * easeOut;
        }

        private void Finish(bool skipped)
        {
            RestoreCards();
            playing = false;
            LastFinishSkipped = skipped;
            Action callback = finished;
            finished = null;
            callback?.Invoke();
        }

        private void RestoreCards()
        {
            // 아직 바꾸지 않은 카드에 새 내용을 넣고, 정면 자세 · 선택 가능으로 둠 (어느 길로 끝나든 같은 모습)
            restorePending = false;
            for (int i = 0; i < slots.Count; i++)
            {
                slots[i].ShowNewContent();
                slots[i].Card.ResetFlip();
            }

            for (int i = 0; i < lockedCards.Count; i++)
            {
                lockedCards[i].SetSelectable(true);
            }

            slots.Clear();
            lockedCards.Clear();
        }

        private sealed class Slot
        {
            private readonly Action showNewContent;
            private bool contentShown;

            public Slot(ModuleCardView card, Action showNewContent)
            {
                Card = card;
                this.showNewContent = showNewContent;
            }

            public ModuleCardView Card { get; }

            public void ShowNewContent()
            {
                if (!contentShown)
                {
                    contentShown = true;
                    showNewContent();
                }
            }
        }
    }
}

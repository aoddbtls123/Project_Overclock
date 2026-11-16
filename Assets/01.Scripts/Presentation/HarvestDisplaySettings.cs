using Overclock.Core;
using UnityEngine;

namespace Overclock.Presentation
{
    // 수확물 더미(출력 창 아래 트레이 · 조각 1개 = 생산량 1)와 생산량 숫자 롤업의 겉모습 값. 시간은 scaled time(초) · 거리는 px · 각도는 Z 회전(도).
    // 등급 반응 크기는 연출 예산 N 10 / R 20 / SR 70% 를 따름 — N 은 작게, SR 은 크게
    [CreateAssetMenu(fileName = "HarvestDisplaySettings", menuName = "Overclock/Harvest Display Settings")]
    public sealed class HarvestDisplaySettings : ScriptableObject
    {
        [Header("더미 (조각 1개 = 생산량 1 · 11/04)")]
        [Tooltip("더미 한 줄 칸 수")]
        [SerializeField, Min(1)] private int pileColumns = 12;
        [Tooltip("더미 줄 수. 칸 수 × 줄 수 = 그릴 수 있는 조각 수 (기본 108 = R8 할당량). 넘는 조각은 세지만 그리지 않음")]
        [SerializeField, Min(1)] private int pileRows = 9;
        [SerializeField, Min(0f)] private float pileColumnSpacing = 22f;
        [Tooltip("줄 세로 간격 (px). 조각보다 작으면 겹쳐 쌓인 모습")]
        [SerializeField, Min(0f)] private float pileRowSpacing = 11f;
        [Tooltip("맨 아래 줄 중심의 트레이 안 y (px)")]
        [SerializeField] private float pileBottomY = -56f;
        [Tooltip("조각 크기 (px)")]
        [SerializeField, Min(1f)] private float pieceSize = 20f;
        [Tooltip("칸마다 차례로 돌려 쓰는 기울기 (도). 난수 없이 더미가 흐트러져 보이게 함")]
        [SerializeField] private float[] slotTilts = { -8f, 5f, -3f, 9f, -6f, 2f, 7f, -4f, 4f, -9f, 6f, -2f };
        [Tooltip("칸마다 차례로 돌려 쓰는 가로 어긋남 (px)")]
        [SerializeField] private float[] slotOffsetsX = { 3f, -4f, 2f, -2f, 5f, -3f, 1f };
        [Tooltip("칸마다 차례로 돌려 쓰는 세로 어긋남 (px)")]
        [SerializeField] private float[] slotOffsetsY = { 0f, 3f, -2f, 4f, -1f, 2f, -3f, 1f, -4f };
        [Tooltip("SR 아이콘 크기 배율")]
        [SerializeField, Min(0.1f)] private float superRareScale = 1.3f;

        [Header("튀어나오기 · 쏟기")]
        [Tooltip("조각이 튀어나오는 자리 (트레이 안 좌표 · 출력 창 아래쪽)")]
        [SerializeField] private Vector2 spawnPoint = new Vector2(85f, 95f);
        [Tooltip("조각마다 차례로 돌려 쓰는 튀어나오는 가로 어긋남 (px)")]
        [SerializeField] private float[] spawnOffsetsX = { 0f, -30f, 25f, -12f, 40f, -45f, 12f, -22f, 32f };
        [Tooltip("포물선 높이 (px)")]
        [SerializeField, Min(0f)] private float arcHeight = 60f;
        [Tooltip("조각 하나가 날아가 앉는 시간")]
        [SerializeField, Min(0.01f)] private float flightSeconds = 0.25f;
        [Tooltip("조각이 튀어나오는 간격")]
        [SerializeField, Min(0f)] private float launchInterval = 0.03f;
        [Tooltip("한 번 공개에 조각을 뿌리는 시간 상한 (많이 나오면 간격이 줄어듦)")]
        [SerializeField, Min(0.01f)] private float sprayMaxSeconds = 0.45f;
        [Tooltip("동시에 날 수 있는 조각 수. 넘으면 앞 조각이 앉아 이미지가 빌 때까지 출발을 미룸")]
        [SerializeField, Min(1)] private int flyingPoolSize = 24;
        [Tooltip("돌파 때 마지막 조각이 앉은 뒤 쏟기 시작까지 기다리는 시간")]
        [SerializeField, Min(0f)] private float dumpDelay = 0.2f;
        [Tooltip("쏟을 때 아래로 떨어지는 거리 (px)")]
        [SerializeField, Min(0f)] private float dumpDistance = 160f;
        [SerializeField, Min(0.01f)] private float dumpSeconds = 0.4f;

        [Header("생산량 롤업")]
        [SerializeField, Min(0.01f)] private float rollSecondsN = 0.15f;
        [SerializeField, Min(0.01f)] private float rollSecondsR = 0.3f;
        [SerializeField, Min(0.01f)] private float rollSecondsSR = 0.5f;
        [Tooltip("다 세었을 때 숫자가 튀는 배율. 1 이하면 튀지 않음")]
        [SerializeField, Min(1f)] private float punchScaleN = 1f;
        [SerializeField, Min(1f)] private float punchScaleR = 1.15f;
        [SerializeField, Min(1f)] private float punchScaleSR = 1.35f;
        [Tooltip("튀었다 돌아오는 시간. 그동안 숫자가 등급 색임")]
        [SerializeField, Min(0.01f)] private float punchSeconds = 0.25f;

        [Header("할당량 초과분 +n (11/05)")]
        [Tooltip("돌파 뽑기의 마지막 조각에서 +n 이 튀는 배율")]
        [SerializeField, Min(1f)] private float overflowPunchScale = 1.4f;
        [Tooltip("초과분이 할당량의 이 비율 이상이면 더 크게 튐")]
        [SerializeField, Min(0f)] private float bigOverflowRatio = 0.5f;
        [SerializeField, Min(1f)] private float bigOverflowPunchScale = 1.8f;

        [Header("기계식 카운터 창 (11/06)")]
        [Tooltip("바퀴가 보이는 생산량을 따라가는 빠르기 (시간 상수 · 초). 남은 거리가 이 시간마다 약 37% 로 줄어듦 — 0.06 이면 한 칸이 약 0.28초에 멈춤. 조각마다 1씩 오를 때도 바퀴가 끊기지 않고 굴러가게 함")]
        [SerializeField, Min(0.01f)] private float counterFollowSeconds = 0.06f;
        [Tooltip("생산량이 0 으로 돌아갈 때 (새 라운드 · 새 런) 바퀴가 앞으로 돌아 0000 에 멈추는 시간. 일의 자리가 가장 늦게 멈춤")]
        [SerializeField, Min(0.01f)] private float counterResetSeconds = 0.7f;
        [Tooltip("0 으로 돌아갈 때 바퀴마다 더 도는 바퀴 수 (0 이면 가장 가까운 0 까지만)")]
        [SerializeField, Min(0)] private int counterResetTurns = 1;
        [Tooltip("윗자리가 먼저 멈추는 정도 (0 = 다 같이 · 0.4 = 천의 자리가 시간의 60% 에서 멈춤)")]
        [SerializeField, Range(0f, 0.9f)] private float counterResetStagger = 0.4f;

        public int PileColumns => pileColumns;
        public int PileRows => pileRows;
        public float PileColumnSpacing => pileColumnSpacing;
        public float PileRowSpacing => pileRowSpacing;
        public float PileBottomY => pileBottomY;
        public float PieceSize => pieceSize;
        public float SuperRareScale => superRareScale;
        public Vector2 SpawnPoint => spawnPoint;
        public float ArcHeight => arcHeight;
        public float FlightSeconds => flightSeconds;
        public float LaunchInterval => launchInterval;
        public float SprayMaxSeconds => sprayMaxSeconds;
        public int FlyingPoolSize => flyingPoolSize;
        public float DumpDelay => dumpDelay;
        public float DumpDistance => dumpDistance;
        public float DumpSeconds => dumpSeconds;
        public float PunchSeconds => punchSeconds;
        public float OverflowPunchScale => overflowPunchScale;
        public float BigOverflowRatio => bigOverflowRatio;
        public float BigOverflowPunchScale => bigOverflowPunchScale;
        public float CounterFollowSeconds => counterFollowSeconds;
        public float CounterResetSeconds => counterResetSeconds;
        public int CounterResetTurns => counterResetTurns;
        public float CounterResetStagger => counterResetStagger;

        public float GetSlotTilt(int index) => Pick(slotTilts, index);
        public float GetSlotOffsetX(int index) => Pick(slotOffsetsX, index);
        public float GetSlotOffsetY(int index) => Pick(slotOffsetsY, index);
        public float GetSpawnOffsetX(int index) => Pick(spawnOffsetsX, index);

        public float GetRollSeconds(Grade grade)
        {
            switch (grade)
            {
                case Grade.SR:
                    return rollSecondsSR;
                case Grade.R:
                    return rollSecondsR;
                default:
                    return rollSecondsN;
            }
        }

        public float GetPunchScale(Grade grade)
        {
            switch (grade)
            {
                case Grade.SR:
                    return punchScaleSR;
                case Grade.R:
                    return punchScaleR;
                default:
                    return punchScaleN;
            }
        }

        private static float Pick(float[] table, int index)
        {
            return table == null || table.Length == 0 ? 0f : table[index % table.Length];
        }
    }
}

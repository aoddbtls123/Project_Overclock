using Overclock.Core;
using UnityEngine;

namespace Overclock.Presentation
{
    // 수확물 더미(출력 창 아래 트레이)와 생산량 숫자 롤업의 겉모습 값. 시간은 scaled time(초) · 거리는 px · 각도는 Z 회전(도).
    // 등급 반응 크기는 연출 예산 N 10 / R 20 / SR 70% 를 따른다 — N 은 작게, SR 은 크게
    [CreateAssetMenu(fileName = "HarvestDisplaySettings", menuName = "Overclock/Harvest Display Settings")]
    public sealed class HarvestDisplaySettings : ScriptableObject
    {
        [Header("트레이 칸")]
        [Tooltip("한 줄 칸 수. 칸 수 × 줄 수가 아이콘 풀 크기(라운드 최대 뽑기 20) 이상이어야 넘치지 않는다")]
        [SerializeField, Min(1)] private int columns = 7;
        [Tooltip("칸 가로 간격 (px)")]
        [SerializeField, Min(0f)] private float columnSpacing = 40f;
        [Tooltip("줄 세로 간격 (px). 아래 줄부터 채우고 위로 쌓는다")]
        [SerializeField, Min(0f)] private float rowSpacing = 36f;
        [Tooltip("맨 아래 줄 중심의 트레이 안 y (px)")]
        [SerializeField] private float bottomRowY = -40f;
        [Tooltip("칸마다 차례로 돌려 쓰는 기울기 (도). 난수 없이 더미가 흐트러져 보이게 한다")]
        [SerializeField] private float[] slotTilts = { -8f, 5f, -3f, 9f, -6f, 2f, 7f, -4f, 4f, -9f, 6f, -2f };
        [Tooltip("칸마다 차례로 돌려 쓰는 가로 어긋남 (px)")]
        [SerializeField] private float[] slotOffsetsX = { 3f, -4f, 2f, -2f, 5f, -3f, 1f };
        [Tooltip("칸마다 차례로 돌려 쓰는 세로 어긋남 (px)")]
        [SerializeField] private float[] slotOffsetsY = { 0f, 3f, -2f, 4f, -1f, 2f, -3f, 1f, -4f };
        [Tooltip("SR 아이콘 크기 배율")]
        [SerializeField, Min(0.1f)] private float superRareScale = 1.3f;

        [Header("들어오기 · 쏟기")]
        [Tooltip("공개 순간 칸 위 이만큼에서 떨어진다 (px)")]
        [SerializeField, Min(0f)] private float dropHeight = 120f;
        [SerializeField, Min(0.01f)] private float dropSeconds = 0.2f;
        [Tooltip("돌파 때 쏟기 시작까지 기다리는 시간. 마지막 부품이 떨어진 뒤에 쏟는다")]
        [SerializeField, Min(0f)] private float dumpDelay = 0.2f;
        [Tooltip("쏟을 때 아래로 떨어지는 거리 (px)")]
        [SerializeField, Min(0f)] private float dumpDistance = 160f;
        [SerializeField, Min(0.01f)] private float dumpSeconds = 0.4f;

        [Header("생산량 롤업")]
        [SerializeField, Min(0.01f)] private float rollSecondsN = 0.15f;
        [SerializeField, Min(0.01f)] private float rollSecondsR = 0.3f;
        [SerializeField, Min(0.01f)] private float rollSecondsSR = 0.5f;
        [Tooltip("다 세었을 때 숫자가 튀는 배율. 1 이하면 튀지 않는다")]
        [SerializeField, Min(1f)] private float punchScaleN = 1f;
        [SerializeField, Min(1f)] private float punchScaleR = 1.15f;
        [SerializeField, Min(1f)] private float punchScaleSR = 1.35f;
        [Tooltip("튀었다 돌아오는 시간. 그동안 숫자가 등급 색이다")]
        [SerializeField, Min(0.01f)] private float punchSeconds = 0.25f;

        public int Columns => columns;
        public float ColumnSpacing => columnSpacing;
        public float RowSpacing => rowSpacing;
        public float BottomRowY => bottomRowY;
        public float SuperRareScale => superRareScale;
        public float DropHeight => dropHeight;
        public float DropSeconds => dropSeconds;
        public float DumpDelay => dumpDelay;
        public float DumpDistance => dumpDistance;
        public float DumpSeconds => dumpSeconds;
        public float PunchSeconds => punchSeconds;

        public float GetSlotTilt(int index) => Pick(slotTilts, index);
        public float GetSlotOffsetX(int index) => Pick(slotOffsetsX, index);
        public float GetSlotOffsetY(int index) => Pick(slotOffsetsY, index);

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

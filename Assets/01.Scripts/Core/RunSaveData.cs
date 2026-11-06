using System;

namespace Overclock.Core
{
    // 저장 파일에 그대로 들어가는 값 묶음이다. JsonUtility 가 public 필드를 읽으므로 필드로 둔다.
    // 값이 맞는지는 RunSession.Restore 가 전부 검사한다
    [Serializable]
    public sealed class RunSaveData
    {
        public const int CurrentVersion = 1;

        public int version;
        public RunMode mode;
        public uint seed;
        public uint randomState;
        public int drawsWithoutSuperRare;
        public int round;
        public int production;
        public int drawsRemaining;
        public RunState state;
        public Grade streakGrade;
        public int streakCount;
        // 오버클럭 추가 전 세이브에는 없는 값 → false / 0 (= 이번 라운드 안 켬) 으로 읽힌다
        public bool overclockUsed;
        public int overclockDrawsLeft;
        // 이번 라운드에 뽑은 부품 id (뽑은 순서 · 수확물 더미 복원용) 와, 그 목록이 라운드 처음부터 빠짐없는지.
        // 추가 전 세이브에는 둘 다 없음 → false · 빈 목록 = 이번 라운드 부품을 모름 (빈 더미 · 다음 라운드부터 저장)
        public bool roundPartsRecorded;
        public string[] roundPartIds = new string[0];
        public string[] equippedModuleIds = new string[0];
        public string[] offeredModuleIds = new string[0];
        // 열린 제안을 이미 다시 받았는가 (돌파마다 1번 · 11/06). 추가 전 세이브에는 없음 → false (= 아직 안 받음)
        public bool offerRerolled;
    }
}

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
        public string[] equippedModuleIds = new string[0];
        public string[] offeredModuleIds = new string[0];
    }
}

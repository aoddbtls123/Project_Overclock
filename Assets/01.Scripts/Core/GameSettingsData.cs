using System;

namespace Overclock.Core
{
    // 설정 파일에 그대로 들어가는 값 묶음이다. JsonUtility 가 public 필드를 읽으므로 필드로 둔다.
    // 값이 맞는지는 GameSettings.FromData 가 전부 검사한다
    [Serializable]
    public sealed class GameSettingsData
    {
        public const int CurrentVersion = 1;

        public int version;
        public bool shakeEnabled;
        public bool flashEnabled;
        public int revealSpeed;
    }
}

using System;

namespace Overclock.Core
{
    // 설정 파일에 그대로 들어가는 값 묶음이다. JsonUtility 가 public 필드를 읽으므로 필드로 둔다.
    // 값이 맞는지는 GameSettings.FromData 가 전부 검사한다.
    // 버전 2 (10/29) 에서 soundEnabled 가 생겼다. 버전 1 파일에는 이 값이 없어 false 로 읽히므로 FromData 가 켬으로 바꾼다.
    // 버전 3 (11/04) 에서 켬/끔 대신 볼륨 4개 (0~1) 가 생겼다. soundEnabled 는 버전 2 파일을 읽을 때만 쓰고 버전 3 은 쓰지 않는다
    [Serializable]
    public sealed class GameSettingsData
    {
        public const int FirstVersion = 1;
        public const int SoundToggleVersion = 2;
        public const int CurrentVersion = 3;

        public int version;
        public bool shakeEnabled;
        public bool flashEnabled;
        public int revealSpeed;
        public bool soundEnabled;
        public float masterVolume;
        public float effectsVolume;
        public float uiVolume;
        public float musicVolume;
    }
}

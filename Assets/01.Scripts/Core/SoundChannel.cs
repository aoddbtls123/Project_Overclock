namespace Overclock.Core
{
    /// <summary>
    /// 볼륨 슬라이더 종류. 실제 소리 크기 = 전체 × 종류 (11/04).
    /// 효과음 = 뽑기 · 기계 · 돌파 등 게임 소리 (SR 충전음 포함) · UI = 버튼 클릭 · 창 열기 · BGM = 배경 음악 (아직 재생 없음 · 값만 저장)
    /// </summary>
    public enum SoundChannel
    {
        Master = 0,
        Effects = 1,
        Ui = 2,
        Music = 3,
    }
}

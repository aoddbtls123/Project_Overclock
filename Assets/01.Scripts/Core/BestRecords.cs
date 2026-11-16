using System;

namespace Overclock.Core
{
    /// <summary>
    /// 모드별 최고 도달 라운드. 런이 끝날 때 Submit 으로 갱신함. 0 라운드 = 기록 없음.
    /// </summary>
    public sealed class BestRecords
    {
        private int normalBestRound;
        private bool normalCleared;
        private int endlessBestRound;

        public int BestRound(RunMode mode)
        {
            return ValidMode(mode) == RunMode.Normal ? normalBestRound : endlessBestRound;
        }

        /// <summary>일반모드에서 R8 을 돌파한 적이 있는가. 무한모드는 클리어가 없음.</summary>
        public bool IsCleared(RunMode mode)
        {
            return ValidMode(mode) == RunMode.Normal && normalCleared;
        }

        /// <summary>
        /// 끝난 런의 결과를 넣음. 지금 기록보다 좋으면 바꾸고 true 를 돌려줌 (같으면 false).
        /// </summary>
        public bool Submit(RunSession session)
        {
            if (session == null)
            {
                throw new ArgumentNullException(nameof(session));
            }

            RunState state = session.RunManager.State;
            if (state != RunState.RunFailed && state != RunState.RunCleared)
            {
                throw new InvalidOperationException("끝나지 않은 런입니다: " + state);
            }

            return Submit(session.Mode, session.RunManager.CurrentRound, state == RunState.RunCleared);
        }

        internal bool Submit(RunMode mode, int finalRound, bool cleared)
        {
            CheckRecord(mode, finalRound, cleared);
            if (mode == RunMode.Endless)
            {
                if (finalRound <= endlessBestRound)
                {
                    return false;
                }

                endlessBestRound = finalRound;
                return true;
            }

            // 같은 R8 이라도 클리어가 실패보다 위임
            if (Rank(finalRound, cleared) <= Rank(normalBestRound, normalCleared))
            {
                return false;
            }

            normalBestRound = finalRound;
            normalCleared = cleared;
            return true;
        }

        public BestRecordData ToData()
        {
            return new BestRecordData
            {
                version = BestRecordData.CurrentVersion,
                normalBestRound = normalBestRound,
                normalCleared = normalCleared,
                endlessBestRound = endlessBestRound,
            };
        }

        /// <summary>
        /// 파일에서 읽은 값으로 기록을 만듦. 게임이 만들 수 없는 값이면 ArgumentException.
        /// </summary>
        public static BestRecords FromData(BestRecordData data)
        {
            if (data == null)
            {
                throw new ArgumentNullException(nameof(data));
            }

            if (data.version != BestRecordData.CurrentVersion)
            {
                throw new ArgumentException("지원하지 않는 기록 버전입니다: " + data.version, nameof(data));
            }

            var records = new BestRecords();
            if (data.normalBestRound != 0 || data.normalCleared)
            {
                CheckRecord(RunMode.Normal, data.normalBestRound, data.normalCleared);
            }

            if (data.endlessBestRound != 0)
            {
                CheckRecord(RunMode.Endless, data.endlessBestRound, false);
            }

            records.normalBestRound = data.normalBestRound;
            records.normalCleared = data.normalCleared;
            records.endlessBestRound = data.endlessBestRound;
            return records;
        }

        private static int Rank(int round, bool cleared)
        {
            return round * 2 + (cleared ? 1 : 0);
        }

        private static void CheckRecord(RunMode mode, int finalRound, bool cleared)
        {
            ValidMode(mode);
            int maxRound = mode == RunMode.Normal ? RunSession.NormalRoundCount : int.MaxValue;
            if (finalRound < 1 || finalRound > maxRound)
            {
                throw new ArgumentOutOfRangeException(nameof(finalRound), mode + " 라운드 " + finalRound);
            }

            // 클리어는 일반모드 마지막 라운드에서만 생김
            if (cleared && (mode != RunMode.Normal || finalRound != RunSession.NormalRoundCount))
            {
                throw new ArgumentException("클리어할 수 없는 기록입니다: " + mode + " R" + finalRound, nameof(cleared));
            }
        }

        private static RunMode ValidMode(RunMode mode)
        {
            if (mode != RunMode.Normal && mode != RunMode.Endless)
            {
                throw new ArgumentOutOfRangeException(nameof(mode));
            }

            return mode;
        }
    }
}

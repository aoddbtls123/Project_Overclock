using Overclock.Core;

namespace Overclock.Presentation
{
    // 최고 기록 · 런 결과 문구. 모드 선택 화면과 결과 창이 같은 문구를 쓴다
    public static class RecordText
    {
        public static string Best(BestRecords records, RunMode mode)
        {
            if (records.IsCleared(mode))
            {
                return "최고 기록 클리어";
            }

            int round = records.BestRound(mode);
            return round > 0 ? "최고 기록 R" + round : "기록 없음";
        }

        public static string ResultTitle(RunState state)
        {
            return state == RunState.RunCleared ? "클리어!" : "라인 셧다운";
        }

        public static string ResultDetail(RunSession session, BestRecords records, bool newRecord)
        {
            RunManager runManager = session.RunManager;
            string modeName = session.Mode == RunMode.Endless ? "무한 모드" : "일반 모드";
            string outcome = runManager.State == RunState.RunCleared
                ? "R" + runManager.CurrentRound + " 클리어"
                : "R" + runManager.CurrentRound + " 미달";

            return modeName + " · " + outcome + "\n" +
                Best(records, session.Mode) + (newRecord ? " · 신기록!" : "") + "\n" +
                "장착 모듈 " + runManager.EquippedModules.Count + "개\n" +
                "시드 " + session.Seed;
        }
    }
}

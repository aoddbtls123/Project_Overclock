using Overclock.Core;
using UnityEngine;

namespace Overclock.Presentation
{
    // 2차 테스트 기록용 Player.log 줄 (시트 §11 "2차 테스트에 추가할 기록"). 모든 줄은 "[기록]" 으로 시작한다.
    // 시간은 호출하는 쪽이 넘기는 Time.time 이다 — 일시정지(timeScale 0) 동안은 늘지 않는다. 게임 상태는 바꾸지 않는다
    public sealed class PlaytestLog
    {
        private const string Prefix = "[기록] ";

        private bool running;
        private float runStartTime;
        private float lastDrawTime;
        private int drawCount;
        private int skipCount;
        private int firstSkipDraw;

        public void RunStarted(RunSession session, RevealSpeed speed, bool continued, float now)
        {
            running = true;
            runStartTime = now;
            lastDrawTime = now;
            drawCount = 0;
            skipCount = 0;
            firstSkipDraw = 0;
            Write("런 시작 seed=" + session.Seed + " mode=" + session.Mode + (continued ? " (이어하기 R" + session.RunManager.CurrentRound + ")" : "")
                + " 연출=" + speed);
        }

        public void Draw(DrawResult result, RunManager runManager, bool overclockActive, float now)
        {
            if (!running)
            {
                return;
            }

            drawCount++;
            Write("뽑기 #" + drawCount + " R" + runManager.CurrentRound + " " + result.Part.Grade + " +" + result.Output
                + (overclockActive ? " (오버클럭)" : "") + " 남은 " + runManager.DrawsRemaining
                + " 간격 " + (now - lastDrawTime).ToString("0.00") + "s");
            lastDrawTime = now;
        }

        public void Skipped(float now)
        {
            if (!running)
            {
                return;
            }

            skipCount++;
            if (firstSkipDraw == 0)
            {
                firstSkipDraw = drawCount;
            }

            Write("스킵 #" + drawCount + " (뽑은 뒤 " + (now - lastDrawTime).ToString("0.00") + "s)");
        }

        public void OverclockActivated(RunManager runManager, int drawsUsedInRound)
        {
            if (!running)
            {
                return;
            }

            Write("오버클럭 R" + runManager.CurrentRound + " 라운드 " + drawsUsedInRound + "회 뒤 · 남은 " + runManager.DrawsRemaining
                + " · 생산량 " + runManager.Production + " / " + runManager.Quota);
        }

        public void ModuleSelected(int clearedRound, ModuleData module)
        {
            if (!running)
            {
                return;
            }

            Write("모듈 R" + clearedRound + " 돌파 → " + module.Id);
        }

        public void OfferRerolled(int clearedRound)
        {
            if (!running)
            {
                return;
            }

            Write("다시 받기 R" + clearedRound + " 돌파");
        }

        public void SpeedChanged(RevealSpeed speed)
        {
            Write("연출 속도 " + speed + (running ? " (런 중 #" + drawCount + ")" : ""));
        }

        public void RunEnded(RunManager runManager, float now)
        {
            if (!running)
            {
                return;
            }

            running = false;
            Write("런 끝 " + runManager.State + " R" + runManager.CurrentRound + " " + Summary(now));
        }

        public void RunLeft(RunManager runManager, float now)
        {
            if (!running)
            {
                return;
            }

            running = false;
            Write("런 나감 R" + runManager.CurrentRound + " " + Summary(now));
        }

        private string Summary(float now)
        {
            int seconds = Mathf.RoundToInt(now - runStartTime);
            return "시간 " + seconds / 60 + ":" + (seconds % 60).ToString("00") + " 뽑기 " + drawCount + " 스킵 " + skipCount
                + (firstSkipDraw > 0 ? " (첫 스킵 #" + firstSkipDraw + ")" : "");
        }

        private static void Write(string line)
        {
            Debug.Log(Prefix + line);
        }
    }
}

namespace Overclock.Core
{
    public sealed class ModulePreview
    {
        private readonly ModifierEngine before;
        private readonly ModifierEngine after;
        private readonly int baseDrawsPerRound;

        internal ModulePreview(ModifierEngine before, ModifierEngine after, int baseDrawsPerRound)
        {
            this.before = before;
            this.after = after;
            this.baseDrawsPerRound = baseDrawsPerRound;
        }

        public int DrawsPerRoundBefore => before.DrawsPerRound(baseDrawsPerRound);
        public int DrawsPerRoundAfter => after.DrawsPerRound(baseDrawsPerRound);

        public void GetProbabilitiesBefore(out double n, out double r, out double sr)
        {
            before.GetProbabilities(out n, out r, out sr);
        }

        public void GetProbabilitiesAfter(out double n, out double r, out double sr)
        {
            after.GetProbabilities(out n, out r, out sr);
        }

        public int OutputBefore(Grade grade, int baseOutput, bool streakTriggered)
        {
            return before.CalculateOutput(grade, baseOutput, streakTriggered);
        }

        public int OutputAfter(Grade grade, int baseOutput, bool streakTriggered)
        {
            return after.CalculateOutput(grade, baseOutput, streakTriggered);
        }
    }
}

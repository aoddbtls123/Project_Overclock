namespace Overclock.Core
{
    public readonly struct DrawResult
    {
        public DrawResult(PartData part, int output, bool streakTriggered)
        {
            Part = part;
            Output = output;
            StreakTriggered = streakTriggered;
        }

        public PartData Part { get; }
        public int Output { get; }
        public bool StreakTriggered { get; }
    }
}

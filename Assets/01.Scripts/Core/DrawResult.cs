namespace Overclock.Core
{
    public readonly struct DrawResult
    {
        public DrawResult(PartData part, int output, bool streakTriggered)
            : this(part, output, streakTriggered, false)
        {
        }

        public DrawResult(PartData part, int output, bool streakTriggered, bool overclocked)
        {
            Part = part;
            Output = output;
            StreakTriggered = streakTriggered;
            Overclocked = overclocked;
        }

        public PartData Part { get; }
        public int Output { get; }
        public bool StreakTriggered { get; }
        public bool Overclocked { get; }
    }
}

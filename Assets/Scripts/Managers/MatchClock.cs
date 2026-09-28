using System;
using System.Diagnostics;

namespace AMath.Managers
{
    /// <summary>Clock abstraction used to keep elapsed match time monotonic and testable.</summary>
    public interface IMatchClock
    {
        long UtcNowTicks { get; }
        double MonotonicSeconds { get; }
    }

    /// <summary>Production clock. UTC timestamps are descriptive; elapsed time uses Stopwatch.</summary>
    public sealed class SystemMatchClock : IMatchClock
    {
        public static readonly SystemMatchClock Shared = new();

        private SystemMatchClock() { }

        public long UtcNowTicks => DateTime.UtcNow.Ticks;

        public double MonotonicSeconds =>
            (double)Stopwatch.GetTimestamp() / Stopwatch.Frequency;
    }
}

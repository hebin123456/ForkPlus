using System.Diagnostics;

namespace ForkPlus
{
    [DebuggerDisplay("{Start}...{End}")]
    public struct Range
    {
        public static Range Zero = new Range(0, 0);

        public int Start { get; }
        public int End { get; }
        public int Length => End - Start;
        public bool IsEmpty => Start == End;

        [DebuggerStepThrough]
        public Range(int start, int end)
        {
            Start = start;
            End = end;
        }
    }
}

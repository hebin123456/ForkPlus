using System.Diagnostics;

namespace ForkPlus.Git
{
    [DebuggerDisplay("{DebugString}")]
    public struct UpstreamStatus
    {
        public static UpstreamStatus Invalid => new UpstreamStatus(-1, -1);

        public int Behind { get; }
        public int Ahead { get; }

        public bool IsValid => Behind != -1;

        public UpstreamStatus(int behind, int ahead)
        {
            Behind = behind;
            Ahead = ahead;
        }

        private string DebugString
        {
            get
            {
                if (!IsValid)
                {
                    return "[removed]";
                }
                if (Behind == 0 && Ahead == 0)
                {
                    return "";
                }
                if (Behind > 0)
                {
                    if (Ahead > 0)
                    {
                        return $"{Behind}\u2193 {Ahead}\u2191";
                    }
                    return $"{Behind}\u2193";
                }
                return $"{Ahead}\u2191";
            }
        }

        public string ToShortDescription()
        {
            if (!IsValid)
            {
                return "";
            }
            if (Ahead > 0)
            {
                if (Behind > 0)
                {
                    return $"{Ahead}\u2191 {Behind}\u2193";
                }
                return $"{Ahead}\u2191";
            }
            if (Behind > 0)
            {
                return $"{Behind}\u2193";
            }
            return "";
        }
    }
}

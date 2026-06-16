using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace ForkPlus
{
    /// <summary>
    /// Cross-platform natural string comparer. Splits strings into digit and
    /// non-digit runs and compares runs of the same kind numerically or
    /// ordinally. This replaces the Windows-only shlwapi!StrCmpLogicalW.
    /// </summary>
    public sealed class NaturalStringComparer : IComparer<string>
    {
        public static readonly NaturalStringComparer Instance = new NaturalStringComparer();

        private static readonly Regex TokenRegex = new Regex(@"(\d+)|(\D+)", RegexOptions.Compiled);

        public int Compare(string x, string y)
        {
            if (ReferenceEquals(x, y)) return 0;
            if (x == null) return -1;
            if (y == null) return 1;

            var xTokens = TokenRegex.Matches(x);
            var yTokens = TokenRegex.Matches(y);
            int count = System.Math.Min(xTokens.Count, yTokens.Count);

            for (int i = 0; i < count; i++)
            {
                var xt = xTokens[i].Value;
                var yt = yTokens[i].Value;

                bool xIsDigit = char.IsDigit(xt[0]);
                bool yIsDigit = char.IsDigit(yt[0]);

                int cmp;
                if (xIsDigit && yIsDigit)
                {
                    // Compare by numeric value but fall back to length first
                    // (so "02" sorts before "10" the natural way).
                    cmp = xt.Length.CompareTo(yt.Length);
                    if (cmp == 0)
                    {
                        cmp = string.CompareOrdinal(xt, yt);
                    }
                }
                else
                {
                    cmp = string.CompareOrdinal(xt, yt);
                }

                if (cmp != 0) return cmp;
            }

            return x.Length.CompareTo(y.Length);
        }
    }
}

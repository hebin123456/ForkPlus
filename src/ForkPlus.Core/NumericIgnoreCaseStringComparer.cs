using System;

namespace ForkPlus
{
    /// <summary>
    /// Cross-platform numeric (natural) and case-insensitive string comparer.
    /// Backed by the managed NaturalStringComparer; equality and hash codes use
    /// the standard ordinal ignore-case implementation.
    /// </summary>
    public sealed class NumericIgnoreCaseStringComparer : StringComparer
    {
        public static readonly NumericIgnoreCaseStringComparer Comparer = new NumericIgnoreCaseStringComparer();

        public override int Compare(string x, string y)
        {
            return NaturalStringComparer.Instance.Compare(x, y);
        }

        public override bool Equals(string x, string y)
        {
            return StringComparer.OrdinalIgnoreCase.Equals(x, y);
        }

        public override int GetHashCode(string obj)
        {
            return StringComparer.OrdinalIgnoreCase.GetHashCode(obj);
        }
    }
}

using System;

namespace ForkPlus.Git
{
    public class BisectMark : Reference
    {
        public bool IsGood { get; }
        public string ShortName { get; }

        public BisectMark(Sha sha, string fullReference, string fullName, string shortName, bool isGood, DateTime committerDate)
            : base(sha, fullReference, fullName, committerDate)
        {
            IsGood = isGood;
            ShortName = shortName;
        }
    }
}

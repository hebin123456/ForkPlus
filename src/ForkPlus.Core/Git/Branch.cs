using System;

namespace ForkPlus.Git
{
    public class Branch : Reference
    {
        public Branch(Sha sha, string fullReference, string fullName, DateTime committerDate)
            : base(sha, fullReference, fullName, committerDate)
        {
        }
    }
}

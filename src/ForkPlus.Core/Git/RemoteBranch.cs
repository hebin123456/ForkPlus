using System;

namespace ForkPlus.Git
{
    public class RemoteBranch : Branch
    {
        public string ShortName { get; }
        public string Remote { get; }

        public RemoteBranch(Sha sha, string fullReference, string fullName, string shortName, string remote, DateTime committerDate)
            : base(sha, fullReference, fullName, committerDate)
        {
            ShortName = shortName;
            Remote = remote;
        }
    }
}

using System;

namespace ForkPlus.Git
{
    public class StashRevision : Revision
    {
        public Sha FirstParent { get; }
        public string ReflogName { get; }

        public StashRevision(Sha sha, Sha[] parents, string subject, UserIdentity author, DateTime authorDate, string reflogName)
            : base(sha, new RevisionHeader(author, authorDate, subject, hasBody: false))
        {
            FirstParent = parents[0];
            ReflogName = reflogName;
        }
    }
}

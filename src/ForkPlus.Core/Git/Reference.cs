using System;
using System.Diagnostics;

namespace ForkPlus.Git
{
    [DebuggerDisplay("{DebuggerDisplayString}")]
    public class Reference : IGitPoint
    {
        public Sha Sha { get; }
        public string FullReference { get; }
        public string Name { get; }
        public DateTime CommitterDate { get; }

        protected virtual string DebuggerDisplayString => Name;

        string IGitPoint.ObjectName => FullReference;
        string IGitPoint.FriendlyName => Name;

        public Reference(Sha sha, string fullReference, string name, DateTime committerDate)
        {
            Sha = sha;
            FullReference = fullReference;
            Name = name;
            CommitterDate = committerDate;
        }

        public bool ReferenceEquals(Reference other)
        {
            if (Sha == other.Sha && FullReference == other.FullReference && (this as LocalBranch)?.UpstreamFullReference == (other as LocalBranch)?.UpstreamFullReference)
            {
                return (this as LocalBranch)?.IsActive == (other as LocalBranch)?.IsActive;
            }
            return false;
        }

        [Null]
        internal static Reference Create(Sha sha, bool isHead, string fullReference, [Null] string upstream, [Null] string dereferencedShaString, DateTime committerDate)
        {
            if (fullReference.StartsWith("refs/heads/"))
            {
                string fullName = fullReference.Substring("refs/heads/".Length);
                return new LocalBranch(sha, fullReference, fullName, isHead, upstream, committerDate);
            }
            if (fullReference.StartsWith("refs/tags/"))
            {
                string fullName2 = fullReference.Substring("refs/tags/".Length);
                Sha? targetObjectSha = null;
                if (dereferencedShaString != null)
                {
                    Sha? sha2 = Sha.Parse(dereferencedShaString);
                    if (sha2.HasValue)
                    {
                        Sha valueOrDefault = sha2.GetValueOrDefault();
                        targetObjectSha = sha;
                        sha = valueOrDefault;
                    }
                }
                return new Tag(sha, fullReference, fullName2, targetObjectSha, committerDate);
            }
            if (fullReference.StartsWith("refs/remotes/"))
            {
                string text = fullReference.Substring("refs/remotes/".Length);
                int num = text.IndexOf('/');
                if (num != -1 && num + 1 < text.Length)
                {
                    string remote = text.Substring(0, num);
                    string shortName = text.Substring(num + 1);
                    return new RemoteBranch(sha, fullReference, text, shortName, remote, committerDate);
                }
            }
            else if (fullReference.StartsWith("refs/bisect/"))
            {
                string text2 = fullReference.Substring("refs/bisect/".Length);
                int length = ((text2.Length - 33 > 0) ? (text2.Length - 33) : text2.Length);
                string text3 = text2.Substring(0, length);
                bool isGood = text3.StartsWith("good") || text3.StartsWith("old");
                return new BisectMark(sha, fullReference, text2, text3, isGood, committerDate);
            }
            return null;
        }

        internal static Branch CreateBranch(Sha sha, bool isHead, string fullReference, [Null] string upstream, DateTime committerDate)
        {
            if (fullReference.StartsWith("refs/heads/"))
            {
                string fullName = fullReference.Substring("refs/heads/".Length);
                return new LocalBranch(sha, fullReference, fullName, isHead, upstream, committerDate);
            }
            if (fullReference.StartsWith("refs/remotes/"))
            {
                string text = fullReference.Substring("refs/remotes/".Length);
                int num = text.IndexOf('/');
                if (num != -1 && num + 1 < text.Length)
                {
                    string remote = text.Substring(0, num);
                    string shortName = text.Substring(num + 1);
                    return new RemoteBranch(sha, fullReference, text, shortName, remote, committerDate);
                }
            }
            return null;
        }
    }
}

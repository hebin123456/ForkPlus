using System;

namespace ForkPlus.Git
{
	public class LocalBranch : Branch
	{
		public bool IsActive { get; }

		[Null]
		public string UpstreamFullReference { get; }

		protected override string DebuggerDisplayString => (IsActive ? "* " : string.Empty) + base.FullReference;

		public string UpstreamFullName => UpstreamFullReference?.Substring("refs/remotes/".Length);

		public LocalBranch(Sha sha, string fullReference, string fullName, bool isActive, [Null] string upstreamFullReference, DateTime committerDate)
			: base(sha, fullReference, fullName, committerDate)
		{
			IsActive = isActive;
			UpstreamFullReference = upstreamFullReference;
		}
	}
}

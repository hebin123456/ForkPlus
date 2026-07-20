using System;

namespace ForkPlus.Git
{
	public class Tag : Reference
	{
		public Sha? TargetObjectSha { get; }

		public Tag(Sha sha, string fullReference, string fullName, Sha? targetObjectSha, DateTime committerDate)
			: base(sha, fullReference, fullName, committerDate)
		{
			TargetObjectSha = targetObjectSha;
		}
	}
}

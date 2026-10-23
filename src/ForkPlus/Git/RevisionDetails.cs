using System;

namespace ForkPlus.Git
{
	public class RevisionDetails : Revision, IGitPoint
	{
		public UserIdentity Committer { get; }

		public DateTime CommitterDate { get; }

		public Sha[] Parents { get; }

		// WS：提交签名状态与签名者（git %G? / %GS）。未签名为 None + 空串，UI 不显示徽标。
		public CommitSignatureStatus SignatureStatus { get; }

		public string Signer { get; }

		string IGitPoint.FriendlyName
		{
			get
			{
				MessageParts(out var subject, out var _);
				return subject;
			}
		}

		public RevisionDetails(Sha sha, UserIdentity author, DateTime authorDate, UserIdentity committer, DateTime committerDate, Sha[] parents, string message, CommitSignatureStatus signatureStatus = CommitSignatureStatus.None, string signer = null)
			: base(sha, new RevisionHeader(author, authorDate, message, hasBody: false))
		{
			Committer = committer;
			CommitterDate = committerDate;
			Parents = parents;
			SignatureStatus = signatureStatus;
			Signer = signer ?? string.Empty;
		}
	}
}

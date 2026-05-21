using System;
using System.Diagnostics;

namespace ForkPlus.Git
{
	[DebuggerDisplay("{Sha.ToAbbreviatedString(),nq} {Subject}")]
	public class Revision : IGitPoint
	{
		private readonly RevisionHeader _header;

		public Sha Sha { get; }

		public UserIdentity Author => _header.Author;

		public DateTime AuthorDate => _header.AuthorDate;

		public string Message => _header.Message;

		string IGitPoint.ObjectName => Sha.ToString();

		string IGitPoint.FriendlyName => Message;

		public Revision(Sha sha, RevisionHeader header)
		{
			Sha = sha;
			_header = header;
		}

		public void MessageParts(out string subject, out string description)
		{
			int num = Message.IndexOf("\n");
			if (num == -1)
			{
				subject = Message.Trim();
				description = string.Empty;
			}
			else
			{
				subject = Message.Substring(0, num);
				description = Message.Substring(num + 1).Trim('\n');
			}
		}
	}
}

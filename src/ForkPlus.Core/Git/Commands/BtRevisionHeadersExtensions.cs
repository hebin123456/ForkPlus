using System;
using ForkPlus.Biturbo;

namespace ForkPlus.Git.Commands
{
	internal static class BtRevisionHeadersExtensions
	{
		public static GitCommandResult<RevisionHeader[]> Into(this ref BtRevisionHeaders btRevisionHeaders)
		{
			UserIdentity[] identities = btRevisionHeaders.identities.GetStructArray(btRevisionHeaders.identities_len, (BtIdentity btIdentity) => new UserIdentity(btIdentity.name.GetUtf8String(), btIdentity.email.GetUtf8String()));
			return GitCommandResult<RevisionHeader[]>.Success(btRevisionHeaders.revisions.GetStructArray(btRevisionHeaders.revisions_len, (int i, BtRevisionHeader btRevisionHeader) => btRevisionHeader.Into(identities)));
		}

		private static RevisionHeader Into(this BtRevisionHeader btRevisionHeader, UserIdentity[] identities)
		{
			UserIdentity author = identities[btRevisionHeader.author_index];
			DateTime localDateTime = DateTimeOffset.FromUnixTimeSeconds(btRevisionHeader.author_time).LocalDateTime;
			string utf8String = btRevisionHeader.subject.GetUtf8String();
			bool hasBody = btRevisionHeader.has_body == 1;
			return new RevisionHeader(author, localDateTime, utf8String, hasBody);
		}
	}
}

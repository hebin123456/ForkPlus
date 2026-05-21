using System;
using ForkPlus.Biturbo;

namespace ForkPlus.Git.Commands
{
	public class GetRevisionsGitCommand
	{
		public GitCommandResult<Revision[]> Execute(GitModule gitModule, Sha[] shas)
		{
			Benchmarker benchmarker = new Benchmarker("bt_get_revision_headers");
			BtOid[] array = shas.Map((Sha x) => x.ToBtOid());
			BtRevisionHeaders out_result = default(BtRevisionHeaders);
			BtResult btResult = Bt.bt_get_revision_headers(gitModule.Path, gitModule.GitDir(), array, array.Length, ref out_result);
			if (btResult != 0)
			{
				return GitCommandResult<Revision[]>.Failure(btResult.ToGitCommandError());
			}
			UserIdentity[] identities = out_result.identities.GetStructArray(out_result.identities_len, (BtIdentity btIdentity) => new UserIdentity(btIdentity.name.GetUtf8String(), btIdentity.email.GetUtf8String()));
			Revision[] structArray = out_result.revisions.GetStructArray(out_result.revisions_len, (int i, BtRevisionHeader btRevisionHeader) => ToRevision(shas[i], btRevisionHeader, identities));
			Bt.bt_release_revision_headers(ref out_result);
			benchmarker.ReportElapsed();
			return GitCommandResult<Revision[]>.Success(structArray);
		}

		private static Revision ToRevision(Sha sha, BtRevisionHeader btRevisionHeader, UserIdentity[] identities)
		{
			UserIdentity author = identities[btRevisionHeader.author_index];
			DateTime localDateTime = DateTimeOffset.FromUnixTimeSeconds(btRevisionHeader.author_time).LocalDateTime;
			string utf8String = btRevisionHeader.subject.GetUtf8String();
			bool hasBody = btRevisionHeader.has_body == 1;
			return new Revision(sha, new RevisionHeader(author, localDateTime, utf8String, hasBody));
		}
	}
}

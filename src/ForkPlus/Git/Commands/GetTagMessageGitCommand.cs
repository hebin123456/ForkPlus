using System;
using ForkPlus.Biturbo;
using ForkPlus.Git.Interaction;

namespace ForkPlus.Git.Commands
{
	internal sealed class GetTagMessageGitCommand
	{
		public GitCommandResult<string> Execute(GitModule gitModule, string tagName)
		{
			GitRequestResult gitRequestResult = new GitRequest(gitModule).Command("for-each-ref", "--format=\"%(taggername) %(taggeremail) %(taggerdate)%0a%0a%(contents)\"", "refs/tags/" + tagName).Execute(silent: true);
			if (gitRequestResult.Success)
			{
				return GitCommandResult<string>.Success(gitRequestResult.Stdout.Trim());
			}
			return GitCommandResult<string>.Success("");
		}

		public GitCommandResult<AnnotatedTagDetails> Execute(GitModule gitModule, Sha tagSha)
		{
			BtTagDetails out_result = default(BtTagDetails);
			BtResult btResult = Bt.bt_get_tag_details(gitModule.GitDir(), tagSha.ToBtOid(), ref out_result);
			if (btResult != 0)
			{
				return GitCommandResult<AnnotatedTagDetails>.Failure(btResult.ToGitCommandError());
			}
			string utf8String = out_result.name.GetUtf8String();
			string utf8String2 = out_result.tagger_name.GetUtf8String();
			string utf8String3 = out_result.tagger_email.GetUtf8String();
			DateTime localDateTime = DateTimeOffset.FromUnixTimeSeconds(out_result.tagger_time).LocalDateTime;
			string utf8String4 = out_result.message.GetUtf8String();
			Bt.bt_release_tag_details(ref out_result);
			UserIdentity tagger = new UserIdentity(utf8String2, utf8String3);
			return GitCommandResult<AnnotatedTagDetails>.Success(new AnnotatedTagDetails(utf8String, tagger, localDateTime, utf8String4));
		}
	}
}

using System;
using ForkPlus.Biturbo;
using ForkPlus.Git.Interaction;

namespace ForkPlus.Git.Commands
{
	public class GetHeadGitCommand
	{
		public GitCommandResult<Head> Execute(GitModule gitModule)
		{
			BtHead out_result = default(BtHead);
			BtResult btResult = Bt.bt_get_head(gitModule.GitDir(), ref out_result);
			if (btResult != 0)
			{
				return GitCommandResult<Head>.Failure(btResult.ToGitCommandError());
			}
			Head result = ((!(out_result.Reference != IntPtr.Zero)) ? new Head(out_result.DetachedHead.ToSha()) : new Head(out_result.Reference.GetUtf8String()));
			Bt.bt_release_head(ref out_result);
			return GitCommandResult<Head>.Success(result);
		}

		public GitCommandResult<Sha> ExecuteOld(GitModule gitModule)
		{
			GitRequestResult gitRequestResult = new GitRequest(gitModule).Command("rev-parse", "HEAD").Execute(silent: true);
			if (!gitRequestResult.Success)
			{
				return GitCommandResult<Sha>.Failure(gitRequestResult.ToGitCommandError());
			}
			string text = gitRequestResult.Stdout.Trim();
			if (!Sha.TryParse(text, out var result))
			{
				GitCommandResult<Sha>.Failure(new GitCommandError.ParseError("Failed to parse HEAD SHA in '" + text + "'"));
			}
			return GitCommandResult<Sha>.Success(result);
		}
	}
}

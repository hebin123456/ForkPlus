using ForkPlus.Git.Interaction;

namespace ForkPlus.Git.Commands
{
	/// <summary>读取提交签名密钥（git config user.signingkey，local 覆盖 global 的有效值）。
	/// 键未设置时返回空串（git config 退出码 1）。</summary>
	public class GetSigningKeyGitCommand
	{
		public GitCommandResult<string> Execute(GitModule gitModule)
		{
			GitRequestResult gitRequestResult = new GitRequest(gitModule).Command("config", "user.signingkey").Execute();
			if (!gitRequestResult.Success || gitRequestResult.Stdout == null)
			{
				return GitCommandResult<string>.Success(string.Empty);
			}
			return GitCommandResult<string>.Success(gitRequestResult.Stdout.Trim());
		}
	}
}

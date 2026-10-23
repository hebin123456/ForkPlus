using ForkPlus.Git.Interaction;

namespace ForkPlus.Git.Commands
{
	/// <summary>读取提交签名开关（git config commit.gpgsign）。
	/// WS：签名状态落 git config 而非会话状态——squash、AI WIP 拆分、amend 等所有
	/// git commit 路径由 git 自身读取该配置，天然继承，无需逐调用方传参。
	/// 键未设置时 git config 以退出码 1 结束（stdout 为空），视为"未开启签名"。</summary>
	public class GetCommitSigningGitCommand
	{
		public GitCommandResult<bool> Execute(GitModule gitModule)
		{
			GitRequestResult gitRequestResult = new GitRequest(gitModule).Command("config", "commit.gpgsign").Execute();
			return GitCommandResult<bool>.Success(IsEnabled(gitRequestResult));
		}

		/// <summary>读取全局默认签名开关（Preferences → Commit 的"默认签名提交"复选框）。</summary>
		public GitCommandResult<bool> ExecuteGlobal()
		{
			GitRequestResult gitRequestResult = default(GitRequest).Command("config", "--global", "commit.gpgsign").Execute();
			return GitCommandResult<bool>.Success(IsEnabled(gitRequestResult));
		}

		private static bool IsEnabled(GitRequestResult result)
		{
			if (!result.Success || string.IsNullOrWhiteSpace(result.Stdout))
			{
				return false;
			}
			return string.Equals(result.Stdout.Trim(), "true", System.StringComparison.OrdinalIgnoreCase);
		}
	}
}

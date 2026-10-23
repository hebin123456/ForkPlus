using ForkPlus.Git.Interaction;

namespace ForkPlus.Git.Commands
{
	/// <summary>写入提交签名配置（git config）。三个落点：
	/// 1. Execute/ExecuteGlobal：commit.gpgsign 开关（--local 优先于 --global，"本仓库关闭"语义可遮蔽全局默认）。
	/// 2. SetSigningKey：SSH 签名密钥——user.signingkey + gpg.format=ssh（选择 GPG 密钥的用户无需经过这里）。
	/// 3. UnsetSigningKey：清除本仓库的 user.signingkey。</summary>
	public class SetCommitSigningGitCommand
	{
		public GitCommandResult Execute(GitModule gitModule, bool enable)
		{
			GitRequestResult gitRequestResult = new GitRequest(gitModule).Command("config", "--local", "commit.gpgsign", enable ? "true" : "false").Execute();
			if (!gitRequestResult.Success)
			{
				return GitCommandResult.Failure(gitRequestResult.ToGitCommandError());
			}
			return GitCommandResult.Success();
		}

		public GitCommandResult ExecuteGlobal(bool enable)
		{
			GitRequestResult gitRequestResult = default(GitRequest).Command("config", "--global", "--replace-all", "commit.gpgsign", enable ? "true" : "false").Execute();
			if (!gitRequestResult.Success)
			{
				return GitCommandResult.Failure(gitRequestResult.ToGitCommandError());
			}
			return GitCommandResult.Success();
		}

		public GitCommandResult SetSigningKey(GitModule gitModule, string sshKeyPath)
		{
			GitRequestResult gitRequestResult = new GitRequest(gitModule).Command("config", "--local", "user.signingkey", sshKeyPath.Quotify()).Execute();
			if (!gitRequestResult.Success)
			{
				return GitCommandResult.Failure(gitRequestResult.ToGitCommandError());
			}
			GitRequestResult gitRequestResult2 = new GitRequest(gitModule).Command("config", "--local", "gpg.format", "ssh").Execute();
			if (!gitRequestResult2.Success)
			{
				return GitCommandResult.Failure(gitRequestResult2.ToGitCommandError());
			}
			return GitCommandResult.Success();
		}

		public GitCommandResult UnsetSigningKey(GitModule gitModule)
		{
			GitRequestResult gitRequestResult = new GitRequest(gitModule).Command("config", "--local", "--unset", "user.signingkey").Execute();
			if (!gitRequestResult.Success)
			{
				return GitCommandResult.Failure(gitRequestResult.ToGitCommandError());
			}
			return GitCommandResult.Success();
		}
	}
}

using System.Threading.Tasks;
using ForkPlus.Git.Interaction;

namespace ForkPlus.Git.Commands
{
	internal class GetGlobalUserIdentityGitCommand
	{
		public GitCommandResult<UserIdentity> Execute()
		{
			// 优化（2026-09-18，"偏好设置打开有点慢"）：user.name/user.email 两个 git config
			// 子进程并行执行（原串行 spawn 两次 ≈ 2× 耗时；并行后 ≈ 最慢单次）。GitRequest.Execute
			// 是纯进程 I/O 无 UI 依赖，可安全在线程池执行；结果语义不变。
			Task<GitRequestResult> nameTask = Task.Run(delegate
			{
				return default(GitRequest).Command("config", "--global", "user.name").Execute();
			});
			Task<GitRequestResult> emailTask = Task.Run(delegate
			{
				return default(GitRequest).Command("config", "--global", "user.email").Execute();
			});
			string name = nameTask.Result.Stdout?.Trim();
			string email = emailTask.Result.Stdout?.Trim();
			return GitCommandResult<UserIdentity>.Success(new UserIdentity(name, email));
		}
	}
}

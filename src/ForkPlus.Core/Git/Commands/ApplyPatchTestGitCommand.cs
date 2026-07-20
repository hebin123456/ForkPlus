using ForkPlus.Git.Interaction;

namespace ForkPlus.Git.Commands
{
	public class ApplyPatchTestGitCommand
	{
		public enum TestResult
		{
			Success,
			Conflict
		}

		public GitCommandResult<TestResult> Execute(GitModule gitModule, string patchPath)
		{
			if (!new GitRequest(gitModule).Command("apply", "--check", patchPath).ExecuteBt(null, silent: true).Success)
			{
				return GitCommandResult<TestResult>.Success(TestResult.Conflict);
			}
			return GitCommandResult<TestResult>.Success(TestResult.Success);
		}

		public GitCommandResult<TestResult> Execute(GitModule gitModule, byte[] patchData)
		{
			if (!new GitRequest(gitModule).Command("apply", "--check").Stdin(patchData).ExecuteBt(null, silent: true)
				.Success)
			{
				return GitCommandResult<TestResult>.Success(TestResult.Conflict);
			}
			return GitCommandResult<TestResult>.Success(TestResult.Success);
		}
	}
}

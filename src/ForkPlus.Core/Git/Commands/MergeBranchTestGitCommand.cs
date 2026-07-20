using ForkPlus.Git.Interaction;

namespace ForkPlus.Git.Commands
{
	public class MergeBranchTestGitCommand
	{
		public enum TestResult
		{
			Success,
			Conflict,
			Unknown
		}

		public GitCommandResult<TestResult> Execute(GitModule gitModule, Reference source, LocalBranch destination)
		{
			GitRequestResult gitRequestResult = new GitRequest(gitModule).Command("merge-base", source.Name, destination.Name).Execute();
			if (!gitRequestResult.Success)
			{
				return GitCommandResult<TestResult>.Success(TestResult.Unknown);
			}
			string text = gitRequestResult.Stdout.Trim();
			if (text.Length != 40)
			{
				return GitCommandResult<TestResult>.Success(TestResult.Unknown);
			}
			try
			{
				GitRequestResult gitRequestResult2 = new GitRequest(gitModule).Command("merge-tree", text, source.Name, destination.Name).Execute();
				if (!gitRequestResult2.Success)
				{
					return GitCommandResult<TestResult>.Success(TestResult.Unknown);
				}
				string stdout = gitRequestResult2.Stdout;
				if (stdout.Contains("+>>>>>>>") || stdout.Contains("+<<<<<<<") || stdout.Contains("->>>>>>>") || stdout.Contains("-<<<<<<<"))
				{
					return GitCommandResult<TestResult>.Success(TestResult.Conflict);
				}
			}
			catch
			{
				return GitCommandResult<TestResult>.Success(TestResult.Unknown);
			}
			return GitCommandResult<TestResult>.Success(TestResult.Success);
		}
	}
}

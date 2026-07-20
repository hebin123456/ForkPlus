using ForkPlus.Git.Interaction;

namespace ForkPlus.Git.Commands
{
	public class RebaseTestGitCommand
	{
		public enum TestResult
		{
			Success,
			Conflict,
			Unknown
		}

		public GitCommandResult<TestResult> Execute(GitModule gitModule, Reference src, string dst)
		{
			GitRequestResult gitRequestResult = new GitRequest(gitModule).Command("merge-base", src.FullReference, dst).Execute();
			if (!gitRequestResult.Success)
			{
				return GitCommandResult<TestResult>.Success(TestResult.Unknown);
			}
			Sha? sha = Sha.Parse(gitRequestResult.Stdout.Trim());
			if (sha.HasValue)
			{
				Sha valueOrDefault = sha.GetValueOrDefault();
				if (src.Sha == valueOrDefault)
				{
					return GitCommandResult<TestResult>.Success(TestResult.Success);
				}
				GitRequestResult gitRequestResult2 = new GitRequest(gitModule).Command("replay", "--onto", dst, valueOrDefault.ToString() + ".." + src.Sha).Execute();
				if (!gitRequestResult2.Success)
				{
					if (gitRequestResult2.Stderr.Trim() == "")
					{
						return GitCommandResult<TestResult>.Success(TestResult.Conflict);
					}
					return GitCommandResult<TestResult>.Failure(gitRequestResult2.ToGitCommandError());
				}
				return GitCommandResult<TestResult>.Success(TestResult.Success);
			}
			return GitCommandResult<TestResult>.Success(TestResult.Unknown);
		}
	}
}

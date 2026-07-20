namespace ForkPlus.Git.Interaction
{
	public class GitRequestResult : ShellRequestResult
	{
		public string Stdout { get; }

		public GitRequestResult(int exitCode, string stdout, string stderr)
			: base(exitCode, stderr)
		{
			Stdout = stdout;
		}

		public override string FullReadableOutput()
		{
			if (!string.IsNullOrWhiteSpace(Stdout) && !string.IsNullOrWhiteSpace(base.Stderr))
			{
				return Stdout + "\n" + base.Stderr;
			}
			if (!string.IsNullOrWhiteSpace(Stdout))
			{
				return Stdout;
			}
			return base.FullReadableOutput();
		}
	}
}

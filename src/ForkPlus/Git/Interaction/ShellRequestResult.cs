namespace ForkPlus.Git.Interaction
{
	public abstract class ShellRequestResult
	{
		public int ExitCode { get; }

		public string Stderr { get; }

		public bool Success => ExitCode == 0;

		public ShellRequestResult(int exitCode, string stderr)
		{
			ExitCode = exitCode;
			Stderr = stderr;
		}

		public virtual string FullReadableOutput()
		{
			return Stderr;
		}
	}
}

using System.IO;

namespace ForkPlus.Git.Interaction
{
	public class ShellRequestBinaryResult : ShellRequestResult
	{
		public MemoryStream Stdout { get; }

		public ShellRequestBinaryResult(int exitCode, MemoryStream stdout, string stderr)
			: base(exitCode, stderr)
		{
			Stdout = stdout;
		}
	}
}

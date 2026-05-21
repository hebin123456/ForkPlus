namespace ForkPlus
{
	public struct ExecuteWithCallbackResult
	{
		public bool Success => ExitCode == 0;

		public int ExitCode { get; }

		public ExecuteWithCallbackResult(int exitCode)
		{
			ExitCode = exitCode;
		}
	}
}

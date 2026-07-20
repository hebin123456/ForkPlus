namespace ForkPlus.Git.Interaction
{
	public struct ExecuteWithCallbackResponse
	{
		private readonly Result<ExecuteWithCallbackResult, ISpawnError> _processExitCode;

		[Null]
		public ISpawnError Error => _processExitCode.Error;

		public ExecuteWithCallbackResult Result => _processExitCode.Value;

		private ExecuteWithCallbackResponse(Result<ExecuteWithCallbackResult, ISpawnError> processResult)
		{
			_processExitCode = processResult;
		}

		public static ExecuteWithCallbackResponse Create(int exitCode)
		{
			return new ExecuteWithCallbackResponse(Result<ExecuteWithCallbackResult, ISpawnError>.Ok(new ExecuteWithCallbackResult(exitCode)));
		}

		public static ExecuteWithCallbackResponse Failure(ISpawnError error)
		{
			return new ExecuteWithCallbackResponse(Result<ExecuteWithCallbackResult, ISpawnError>.Err(error));
		}
	}
}

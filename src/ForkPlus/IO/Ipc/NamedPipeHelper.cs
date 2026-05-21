using System.Diagnostics;
using System.IO.Pipes;
using System.Security.Principal;

namespace ForkPlus.IO.Ipc
{
	public class NamedPipeHelper
	{
		public static readonly string AskPassPipeName = "AskPass";

		public static readonly string DefaultPipeName = "Default";

		public static string CreatePipeName(string name, string processId)
		{
			return "Fork_Pipe" + processId + "_" + name;
		}

		public static NamedPipeClientStream CreatePipeClient(string name, Process process)
		{
			return CreatePipeClient(name, process.Id.ToString());
		}

		public static NamedPipeClientStream CreatePipeClient(string name, string processId)
		{
			string pipeName = CreatePipeName(name, processId);
			return new NamedPipeClientStream(".", pipeName, PipeDirection.InOut, PipeOptions.None, TokenImpersonationLevel.Impersonation);
		}
	}
}

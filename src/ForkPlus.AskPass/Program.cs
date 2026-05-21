using System;
using System.Diagnostics;
using System.IO;
using System.IO.Pipes;
using System.Security.Principal;
using System.Text;

namespace ForkPlus.AskPass
{
	internal static class Program
	{
		private const string AskPassPipeName = "AskPass";
		private const string ForkPlusProcessIdVariable = "FORK_PLUS_PROCESS_ID";
		private const string RepositoryPathVariable = "FORK_PLUS_REPOSITORY_PATH";
		private const string NoPromptVariable = "NO_PROMPT";

		private static int Main(string[] args)
		{
			try
			{
				string processId = Environment.GetEnvironmentVariable(ForkPlusProcessIdVariable);
				if (string.IsNullOrWhiteSpace(processId))
				{
					return 1;
				}
				string repositoryPath = Environment.GetEnvironmentVariable(RepositoryPathVariable) ?? "";
				bool noPrompt = string.Equals(Environment.GetEnvironmentVariable(NoPromptVariable), "1", StringComparison.OrdinalIgnoreCase);
				bool credentialHelperMode = args.Length > 0 && IsCredentialHelperAction(args[0]);
				string request = credentialHelperMode ? Console.In.ReadToEnd() : string.Join(" ", args);
				string mode = credentialHelperMode ? (noPrompt ? "3" : "2") : (noPrompt ? "1" : "0");
				using (NamedPipeClientStream pipe = CreatePipeClient(AskPassPipeName, processId))
				{
					pipe.Connect(30000);
					WriteString(pipe, mode + "\0" + repositoryPath + "\0" + request);
					string response = ReadString(pipe) ?? "";
					if (!string.IsNullOrEmpty(response))
					{
						Console.Out.Write(response);
						if (!response.EndsWith(Environment.NewLine, StringComparison.Ordinal))
						{
							Console.Out.WriteLine();
						}
					}
				}
				return 0;
			}
			catch (Exception ex)
			{
				Debug.WriteLine(ex);
				return 1;
			}
		}

		private static bool IsCredentialHelperAction(string value)
		{
			return string.Equals(value, "get", StringComparison.OrdinalIgnoreCase)
				|| string.Equals(value, "store", StringComparison.OrdinalIgnoreCase)
				|| string.Equals(value, "erase", StringComparison.OrdinalIgnoreCase);
		}

		private static NamedPipeClientStream CreatePipeClient(string name, string processId)
		{
			return new NamedPipeClientStream(".", "Fork_Pipe" + processId + "_" + name, PipeDirection.InOut, PipeOptions.None, TokenImpersonationLevel.Impersonation);
		}

		private static string ReadString(PipeStream stream)
		{
			byte[] lengthBytes = new byte[4];
			if (stream.Read(lengthBytes, 0, lengthBytes.Length) != lengthBytes.Length)
			{
				return null;
			}
			int length = BitConverter.ToInt32(lengthBytes, 0);
			byte[] buffer = new byte[length];
			int offset = 0;
			while (offset < length)
			{
				int read = stream.Read(buffer, offset, length - offset);
				if (read <= 0)
				{
					break;
				}
				offset += read;
			}
			return Encoding.Unicode.GetString(buffer, 0, offset);
		}

		private static void WriteString(PipeStream stream, string value)
		{
			byte[] bytes = Encoding.Unicode.GetBytes(value ?? "");
			byte[] lengthBytes = BitConverter.GetBytes(bytes.Length);
			stream.Write(lengthBytes, 0, lengthBytes.Length);
			stream.Write(bytes, 0, bytes.Length);
			stream.Flush();
		}
	}
}

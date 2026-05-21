using System;
using System.Diagnostics;
using System.IO;
using System.IO.Pipes;
using System.Security.Principal;
using System.Text;

namespace ForkPlus.RI
{
	internal static class Program
	{
		private const string RebasePipeName = "RI";
		private const string ForkPlusProcessIdVariable = "FORK_PLUS_PROCESS_ID";
		private const string PrepareTodoCommand = "prepareTodoListForRebase ";

		private static int Main(string[] args)
		{
			try
			{
				string processId = Environment.GetEnvironmentVariable(ForkPlusProcessIdVariable);
				if (string.IsNullOrWhiteSpace(processId) || args.Length == 0 || string.IsNullOrWhiteSpace(args[0]))
				{
					return 1;
				}
				string todoListPath = Path.GetFullPath(args[0]);
				using (NamedPipeClientStream pipe = CreatePipeClient(RebasePipeName, processId))
				{
					pipe.Connect(30000);
					WriteString(pipe, PrepareTodoCommand + todoListPath);
					string response = ReadString(pipe) ?? "";
					return string.Equals(response, "start", StringComparison.OrdinalIgnoreCase) ? 0 : 1;
				}
			}
			catch (Exception ex)
			{
				Debug.WriteLine(ex);
				return 1;
			}
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

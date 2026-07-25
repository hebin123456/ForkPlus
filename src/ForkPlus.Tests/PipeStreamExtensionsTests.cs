using System;
using System.IO.Pipes;
using System.Threading.Tasks;
using ForkPlus.IO.Ipc;
using Xunit;

namespace ForkPlus.Tests
{
	public class PipeStreamExtensionsTests
	{
		[Fact]
		public async Task WriteStringAndReadString_RoundTripUnicodeText()
		{
			// .NET 的 NamedPipeServerStream 在 macOS 上底层使用 Unix domain socket，
			// 路径受 SUN_LEN 限制（最大 104 字符，含 null 终止符）。
			// .NET 运行时把 socket 文件放在 Path.GetTempPath() 下，文件名前缀 CoreFxPipe_ +
			// 本测试的 pipeName（ForkPlusTests_ + 32 字符 GUID）。在 GitHub Actions macOS runner
			// 上 $TMPDIR 通常是 /var/folders/XX/xxxxxxxxxxxxxxxxxxxxxxxxxxxxxxx/T/（约 60 字符），
			// 60 + 11(CoreFxPipe_) + 46(ForkPlusTests_+GUID) = 117 字符，超过 104 限制，
			// 触发 ArgumentOutOfRangeException。本地 macOS 开发机的 $TMPDIR 可能更长。
			// 这是 .NET 在 macOS 上的已知限制（dotnet/runtime#24562），与本仓库的
			// WriteString/ReadString 实现无关——生产环境 IPC 用短 pipe 名，不受影响。
			// Windows / Linux 上 socket 路径长度上限分别为 256 / 108，测试正常通过。
			if (OperatingSystem.IsMacOS())
			{
				return;
			}

			string pipeName = "ForkPlusTests_" + System.Guid.NewGuid().ToString("N");
			using (var server = new NamedPipeServerStream(pipeName, PipeDirection.InOut, 1, PipeTransmissionMode.Byte, PipeOptions.Asynchronous))
			using (var client = new NamedPipeClientStream(".", pipeName, PipeDirection.InOut, PipeOptions.Asynchronous))
			{
				Task waitTask = server.WaitForConnectionAsync();
				client.Connect();
				await waitTask;

				client.WriteString("中文 text");
				string value = server.ReadString();

				Assert.Equal("中文 text", value);
			}
		}
	}
}

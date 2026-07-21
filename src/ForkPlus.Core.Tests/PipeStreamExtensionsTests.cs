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
			// macOS 上 NamedPipeServerStream 底层用 Unix domain socket 实现，
			// socket 路径长度限制 104 字符。CI 上 /var/folders/... 临时路径本身较长，
			// 加上 .NET 自动加的 CoreFxPipe_ 前缀 + pipeName，若 pipeName 用完整 32 字符 GUID
			// 会超限（路径总长 111 字符，触发 ArgumentOutOfRangeException）。
			// 改用 6 字符短 GUID，总长 54 + 11 + 2 + 6 = 73 字符，安全在 104 限制内。
			string pipeName = "FP" + System.Guid.NewGuid().ToString("N").Substring(0, 6);
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

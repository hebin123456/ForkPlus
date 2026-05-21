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

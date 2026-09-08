using System;
using System.IO;
using System.IO.Pipes;
using System.Threading;
using ForkPlus.IO.Ipc;

namespace ForkPlus
{
	internal class IpcServer : IDisposable
	{
		private readonly IpcMessageHandler _messageHandler;

		private readonly CancellationTokenSource _cancellationToken;

		private readonly Thread _thread;

		private NamedPipeServerStream _pipeServer;

		private string CurrentProcessPipeName { get; }

		public IpcServer(string name, IpcMessageHandler messageHandler)
		{
			_messageHandler = messageHandler;
			CurrentProcessPipeName = NamedPipeHelper.CreatePipeName(name, App.ProcessId.ToString());
			int maxNumberOfServerInstances = 10;
			// Migration note：PipeTransmissionMode.Message 仅 Windows 支持，Linux/macOS 抛 PlatformNotSupportedException。
			// 协议本身用 4 字节长度前缀分帧（PipeStreamExtensions.ReadString），不依赖消息边界，Byte 模式完全等价。
			PipeTransmissionMode transmissionMode = global::System.OperatingSystem.IsWindows() ? PipeTransmissionMode.Message : PipeTransmissionMode.Byte;
			_pipeServer = new NamedPipeServerStream(CurrentProcessPipeName, PipeDirection.InOut, maxNumberOfServerInstances, transmissionMode, PipeOptions.Asynchronous);
			_cancellationToken = new CancellationTokenSource();
			_thread = new Thread((ThreadStart)delegate
			{
				EventLoop(_cancellationToken, _pipeServer);
			});
			_thread.Start();
		}

		public void Dispose()
		{
			if (_pipeServer == null)
			{
				return;
			}
			_cancellationToken.Cancel();
			_pipeServer.Dispose();
			_pipeServer = null;
			using (new NamedPipeClientStream(CurrentProcessPipeName))
			{
			}
		}

		private void EventLoop(CancellationTokenSource cancel, NamedPipeServerStream pipeServer)
		{
			Log.Info("Start IPC server " + CurrentProcessPipeName);
			do
			{
				int num = new Random().Next(0, 1000);
				Log.Debug($"{CurrentProcessPipeName}: waiting for next event '{num}'");
				try
				{
					pipeServer.WaitForConnection();
				}
				catch (Exception ex)
				{
					if (!pipeServer.IsConnected)
					{
						Log.Info("Stop ipc server " + CurrentProcessPipeName);
						break;
					}
					Log.Warn("Waiting for IPC connection failed", ex);
				}
				Log.Debug($"{CurrentProcessPipeName}: received event '{num}'");
				try
				{
					_messageHandler(pipeServer);
				}
				catch (IOException ex2)
				{
					Log.Error($"Failed to handle event '{num}", ex2);
				}
				finally
			{
				try
				{
					// Migration note：WaitForPipeDrain 仅 Windows 实现——Unix 上抛 PlatformNotSupportedException，
					// 而下面的 catch 只接 IOException，此前二次启动实例发起 IPC 连接（如命令行传仓库路径、
					// 文件管理器双击打开）时服务线程未捕获该异常，把整个进程带崩（Linux 实测复现：
					// 运行中的主实例直接退出）。Unix 下跳过即可：协议是长度前缀分帧 + 单请求-响应，
					// 响应在 Disconnect 前已写入内核缓冲，客户端 ReadString 收满即返回，无需 drain。
					if (global::System.OperatingSystem.IsWindows())
					{
						pipeServer.WaitForPipeDrain();
					}
				}
				catch (IOException)
				{
					// Pipe already broken — nothing to drain
				}
				if (pipeServer.IsConnected)
				{
					pipeServer.Disconnect();
				}
			}
			}
			while (!cancel.IsCancellationRequested);
		}
	}
}

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
			// PipeTransmissionMode.Message 仅 Windows 支持，Linux/macOS 抛 PlatformNotSupportedException。
			// ReadString/WriteString 已用 4 字节长度前缀做帧定界，Byte 模式下同样能正确收发消息，
			// 故非 Windows 平台回退到 Byte 模式即可。
			PipeTransmissionMode transmissionMode = OperatingSystem.IsWindows()
				? PipeTransmissionMode.Message
				: PipeTransmissionMode.Byte;
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
					// WaitForPipeDrain 仅 Windows 支持（Message 模式下刷管道缓冲），
					// Linux/macOS 的 Byte 模式下 Flush 已同步写出，无需 drain。
					if (OperatingSystem.IsWindows())
					{
						try
						{
							pipeServer.WaitForPipeDrain();
						}
						catch (IOException)
						{
							// Pipe already broken — nothing to drain
						}
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

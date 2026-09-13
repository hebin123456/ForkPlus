using System;
using System.IO;
using System.IO.Pipes;
using System.Text;

namespace ForkPlus.AutoUpdater
{
	/// <summary>
	/// 进度上报管道客户端：把 AutoUpdater 的阶段/下载进度/失败消息写给主程序。
	/// 帧格式与主程序 ForkPlus.IO.Ipc.PipeStreamExtensions 完全一致
	/// （4 字节小端长度前缀 + UTF-16LE 无 BOM），主程序侧用同一套 ReadString 读取。
	/// 连不上管道（主程序已退出/未启动监听）时静默降级为无进度上报，
	/// 更新流程继续走（进度条不可用不阻断更新本体）。
	/// </summary>
	internal sealed class UpdateProgressPipeClient : IDisposable
	{
		private NamedPipeClientStream _pipe;

		private readonly object _writeLock = new object();

		/// <summary>管道不可用时本实例是否仍可用（写消息变 no-op）。</summary>
		public bool IsConnected => _pipe != null && _pipe.IsConnected;

		/// <summary>
		/// 连接主程序进度管道。pipeName 形如 "Fork_Pipe{pid}_Update"。
		/// timeoutMs 后连不上则放弃（返回后本实例处于无管道模式，不抛异常）。
		/// </summary>
		public bool TryConnect(string pipeName, int timeoutMs)
		{
			try
			{
				_pipe = new NamedPipeClientStream(".", pipeName, PipeDirection.InOut, PipeOptions.Asynchronous);
				_pipe.Connect(timeoutMs);
				return _pipe.IsConnected;
			}
			catch (Exception)
			{
				// 超时 / 管道不存在 / 权限——进度上报是增强能力，缺失不阻断更新
				TryDisposePipe();
				return false;
			}
		}

		/// <summary>写一条消息。管道断开（主程序被杀/退出）时静默转为无管道模式。</summary>
		public void Write(string message)
		{
			lock (_writeLock)
			{
				NamedPipeClientStream pipe = _pipe;
				if (pipe == null || !pipe.IsConnected)
				{
					return;
				}
				try
				{
					WriteString(pipe, message);
				}
				catch (Exception)
				{
					// 主程序侧先关了管道（正常关闭路径）——后续消息不再上报
					TryDisposePipe();
				}
			}
		}

		private void TryDisposePipe()
		{
			try
			{
				_pipe?.Dispose();
			}
			catch (Exception)
			{
			}
			_pipe = null;
		}

		/// <summary>
		/// 帧写入：4 字节小端长度前缀 + UTF-16LE。与主程序 PipeStreamExtensions.WriteString
		/// 逐字节一致（UnicodeEncoding() 默认即 UTF-16LE 且 GetBytes(string) 不带 BOM），
		/// 此处显式构造 UnicodeEncoding(false, false) 表达同一语义并避免误解。
		/// </summary>
		private static void WriteString(PipeStream stream, string message)
		{
			byte[] payload = new UnicodeEncoding(false, false).GetBytes(message);
			int length = payload.Length;
			stream.Write(new byte[4] { (byte)(length & 0xFF), (byte)((length >> 8) & 0xFF), (byte)((length >> 16) & 0xFF), (byte)((length >> 24) & 0xFF) }, 0, 4);
			stream.Write(payload, 0, length);
			stream.Flush();
		}

		public void Dispose()
		{
			TryDisposePipe();
		}
	}
}

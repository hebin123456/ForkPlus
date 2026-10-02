using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;

namespace ForkPlus.Plugins
{
	/// <summary>
	/// v4.5.0：与一个插件宿主进程的活动会话。一个插件 = 一个进程，请求串行化。
	///
	/// 隔离契约（本类存在的理由）：插件的一切——GPL 依赖、原生库、异常、内存爆掉、
	/// 挂死——都发生在它自己的进程里。宿主只通过 stdin/stdout 交换 JSON。
	/// 任何一次交互失败（超时/崩溃/报文损坏）都只把本会话标记为死亡并返回错误，
	/// 绝不向上抛异常，也绝不拖垮调用方（UI 线程）。
	/// </summary>
	public sealed class PluginSession : IDisposable
	{
		private readonly PluginManifest _manifest;

		private readonly Process _process;

		private readonly PluginChannel _channel;

		private readonly object _sync = new object();

		private readonly StringBuilder _stderrTail = new StringBuilder();

		private int _nextId;

		private bool _dead;

		private bool _disposed;

		public PluginManifest Manifest => _manifest;

		/// <summary>握手时插件自报的元数据；握手失败为 null。</summary>
		public HelloResult Hello { get; private set; }

		public string LastError { get; private set; }

		public bool IsAlive
		{
			get
			{
				if (_dead || _disposed)
				{
					return false;
				}
				try
				{
					return !_process.HasExited;
				}
				catch (InvalidOperationException)
				{
					return false;
				}
			}
		}

		private PluginSession(PluginManifest manifest, Process process, PluginChannel channel)
		{
			_manifest = manifest;
			_process = process;
			_channel = channel;
		}

		/// <summary>
		/// 启动插件宿主进程并完成 hello 握手。失败返回 null 并给出原因（不会抛异常）。
		/// </summary>
		public static PluginSession Start(PluginManifest manifest, string hostName, string hostVersion, int timeoutMs, out string error)
		{
			error = null;
			if (manifest == null)
			{
				error = "Plugin manifest is null.";
				return null;
			}
			string executable = manifest.ResolveHostExecutable();
			if (string.IsNullOrWhiteSpace(executable))
			{
				error = "Plugin manifest has no host executable.";
				return null;
			}
			if (!File.Exists(executable))
			{
				error = "Plugin host executable not found: " + executable;
				return null;
			}
			List<string> argumentList = new List<string>();
			string fileName = executable;
			// 插件仓可以选择发布 apphost（无扩展名 / .exe）或纯托管 dll；
			// 后者需要经 dotnet 拉起，这里替插件作者兜住，省得每人都踩一遍。
			if (string.Equals(Path.GetExtension(executable), ".dll", StringComparison.OrdinalIgnoreCase))
			{
				fileName = "dotnet";
				argumentList.Add(executable);
			}
			argumentList.AddRange(manifest.Host.ArgumentsOrEmpty);

			ProcessStartInfo startInfo = new ProcessStartInfo
			{
				FileName = fileName,
				WorkingDirectory = manifest.BaseDirectory ?? Directory.GetCurrentDirectory(),
				UseShellExecute = false,
				CreateNoWindow = true,
				RedirectStandardInput = true,
				RedirectStandardOutput = true,
				RedirectStandardError = true
			};
			foreach (string argument in argumentList)
			{
				startInfo.ArgumentList.Add(argument);
			}

			Process process;
			try
			{
				process = Process.Start(startInfo);
			}
			catch (Exception ex)
			{
				error = "Failed to start plugin host '" + fileName + "': " + ex.Message;
				return null;
			}
			if (process == null)
			{
				error = "Failed to start plugin host '" + fileName + "'.";
				return null;
			}

			PluginSession session = new PluginSession(manifest, process, new PluginChannel(process.StandardOutput.BaseStream, ownsStream: true));
			session.AttachStderrCapture();
			if (!session.Handshake(hostName, hostVersion, timeoutMs))
			{
				error = session.LastError;
				session.Dispose();
				return null;
			}
			return session;
		}

		/// <summary>发起一次渲染；失败返回 null 并给出错误（超时/崩溃/插件报错）。</summary>
		public RenderResult Render(RenderParams parameters, int timeoutMs, out PluginError error)
		{
			error = null;
			string responseJson = Exchange(JsonConvert.SerializeObject(PluginProtocol.CreateRender(NextId(), parameters)), timeoutMs, out error);
			if (responseJson == null)
			{
				return null;
			}
			PluginResponse response = ParseResponse(responseJson, out error);
			if (response == null)
			{
				return null;
			}
			error = response.Error;
			if (response.Error != null)
			{
				return null;
			}
			try
			{
				RenderResult result = response.Result?.ToObject<RenderResult>();
				if (result == null || result.Frames == null || result.Frames.Count == 0)
				{
					error = new PluginError { Code = "empty-result", Message = "Plugin returned no frames." };
					return null;
				}
				return result;
			}
			catch (JsonException ex)
			{
				error = new PluginError { Code = "bad-result", Message = "Plugin returned an unreadable render result: " + ex.Message };
				return null;
			}
		}

		private bool Handshake(string hostName, string hostVersion, int timeoutMs)
		{
			PluginError transportError;
			string responseJson = Exchange(JsonConvert.SerializeObject(PluginProtocol.CreateHello(NextId(), hostName, hostVersion)), timeoutMs, out transportError);
			if (responseJson == null)
			{
				LastError = transportError?.ToString();
				return false;
			}
			PluginError parseError;
			PluginResponse response = ParseResponse(responseJson, out parseError);
			if (response == null)
			{
				LastError = parseError?.ToString();
				return false;
			}
			if (response.Error != null)
			{
				LastError = response.Error.ToString();
				return false;
			}
			HelloResult hello;
			try
			{
				hello = response.Result?.ToObject<HelloResult>();
			}
			catch (JsonException ex)
			{
				LastError = "Plugin returned an unreadable hello result: " + ex.Message;
				return false;
			}
			if (hello == null)
			{
				LastError = "Plugin returned an empty hello result.";
				return false;
			}
			if (hello.ProtocolVersion != PluginProtocol.Version)
			{
				LastError = $"Plugin speaks protocol {hello.ProtocolVersion}, but this host implements {PluginProtocol.Version}.";
				return false;
			}
			Hello = hello;
			return true;
		}

		/// <summary>写请求、读响应。所有失败路径统一转成 PluginError 并标记会话状态。</summary>
		private string Exchange(string requestJson, int timeoutMs, out PluginError error)
		{
			error = null;
			lock (_sync)
			{
				if (_dead || _disposed)
				{
					error = new PluginError { Code = "session-dead", Message = LastError ?? "Plugin session is no longer usable." };
					return null;
				}
				try
				{
					_channel.Write(requestJson);
				}
				catch (Exception ex) when (ex is IOException || ex is ObjectDisposedException || ex is InvalidOperationException)
				{
					return Fail("Plugin host closed its input: " + ex.Message, out error);
				}

				// 同步读无法设超时；把阻塞读挪到线程池，超时则杀掉插件进程——
				// 进程一死，阻塞读立刻以 EOF 返回，不会留下挂起的线程。
				Task<string> readTask = Task.Run(() =>
				{
					try
					{
						return _channel.Read();
					}
					catch (Exception)
					{
						return null;
					}
				});
				if (!readTask.Wait(timeoutMs))
				{
					return Fail($"Plugin did not respond within {timeoutMs} ms; the host process was terminated.", out error, kill: true);
				}
				string responseJson = readTask.Result;
				if (responseJson == null)
				{
					return Fail("Plugin host exited unexpectedly." + DescribeStderr(), out error, kill: true);
				}
				return responseJson;
			}
		}

		private string Fail(string message, out PluginError error, bool kill = false)
		{
			if (kill)
			{
				KillProcess();
			}
			_dead = true;
			LastError = message;
			error = new PluginError { Code = "plugin-unavailable", Message = message };
			return null;
		}

		private PluginResponse ParseResponse(string json, out PluginError error)
		{
			error = null;
			try
			{
				PluginResponse response = JsonConvert.DeserializeObject<PluginResponse>(json);
				if (response == null)
				{
					error = new PluginError { Code = "bad-response", Message = "Plugin sent an empty response." };
				}
				return response;
			}
			catch (JsonException ex)
			{
				error = new PluginError { Code = "bad-response", Message = "Plugin sent unreadable JSON: " + ex.Message };
				return null;
			}
		}

		private int NextId()
		{
			return ++_nextId;
		}

		private void AttachStderrCapture()
		{
			_process.ErrorDataReceived += delegate(object sender, DataReceivedEventArgs args)
			{
				if (args.Data == null)
				{
					return;
				}
				lock (_stderrTail)
				{
					// 只留尾部：插件打日志是它的自由，但宿主不该因此无限增长内存。
					if (_stderrTail.Length > 8192)
					{
						_stderrTail.Remove(0, _stderrTail.Length - 4096);
					}
					_stderrTail.AppendLine(args.Data);
				}
			};
			try
			{
				_process.BeginErrorReadLine();
			}
			catch (InvalidOperationException)
			{
				// 进程已退出；错误流不再可用，不影响主链路。
			}
		}

		private string DescribeStderr()
		{
			lock (_stderrTail)
			{
				string tail = _stderrTail.ToString().Trim();
				return tail.Length == 0 ? string.Empty : " Plugin stderr: " + tail;
			}
		}

		private void KillProcess()
		{
			try
			{
				if (!_process.HasExited)
				{
					_process.Kill(entireProcessTree: true);
				}
			}
			catch (Exception ex) when (ex is InvalidOperationException || ex is NotSupportedException)
			{
				// 进程已退出或不支持 Kill；无需处理。
			}
		}

		public void Dispose()
		{
			if (_disposed)
			{
				return;
			}
			_disposed = true;
			// 先礼节性通知插件退出，再兜底杀进程——插件不响应 shutdown 或已挂死都收得住。
			try
			{
				if (!_dead)
				{
					lock (_sync)
					{
						_channel.Write(JsonConvert.SerializeObject(new PluginRequest(NextId(), PluginProtocol.MethodShutdown, null)));
					}
				}
			}
			catch (Exception ex) when (ex is IOException || ex is ObjectDisposedException || ex is InvalidOperationException)
			{
				// 插件已不在；直接进入杀进程。
			}
			KillProcess();
			try
			{
				_process.Dispose();
			}
			catch (InvalidOperationException)
			{
				// 已释放。
			}
			_channel.Dispose();
		}
	}
}
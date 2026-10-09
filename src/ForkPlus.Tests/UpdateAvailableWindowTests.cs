// E2E 测试（v4.1.0，自动更新全链路）：UpdateAvailableWindow 的 Download 按钮不再开
// 浏览器——经 AutoUpdateRunner 复制并拉起真实 ForkPlus.AutoUpdater 子进程（测试输出
// 目录的四件套由 csproj Copy 任务备齐），对本地桩服务器完成 下载 → 解压 → 等待退出
// → 备份替换安装目录 全链路；进度经命名管道（Fork_Pipe{pid}_Update）回流驱动 UI。
// 覆盖三条用户可感知路径：
//   ① 成功：进度面板切换 → 阶段文案推进 → waiting-exit 触发应用关闭钩子 →
//      安装目录内容被新包整体替换（旧文件移入 backup）→ updater 退出码 0；
//   ② 下载中取消：Footer Cancel 复用为"取消下载"（不关窗）→ Kill updater →
//      恢复结果区可重试（Submit 复现、Cancel 语义还原 Later）→ 安装目录未被触碰；
//   ③ 下载失败：重试耗尽后 Footer 状态区展示"更新失败：{原因}"（现有弹窗错误的
//      标准展示位）→ 结果区恢复可重试 → updater 退出码 1。
// 注入点：RunnerFactoryForTests（临时安装目录 + 死 wait-pid + --no-restart，生产用
// 真实安装目录/本进程 pid/自动重启）与 WaitingExitActionForTests（替代真正 Shutdown）。
// 另含 AutoUpdatePackageUrl.Resolve 纯单元（zip 直链透传 / Release 页地址按
// 版本+平台构造规则下载地址 / 缺参回退 null 走浏览器打开）。
// 环境适配：apphost 定位运行时需 DOTNET_ROOT（GitHub CI 的 setup-dotnet 会设；
// 本地跑测试未设时从当前共享运行时位置推导补齐，SetEnvironmentVariable 进程级
// 生效、子进程可见）。
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Avalonia.VisualTree;
using ForkPlus.Settings;
using ForkPlus.UI.Dialogs;
using ForkPlus.UI.UserControls;
using Xunit;

namespace ForkPlus.Tests
{
	[Collection("HeadlessAvalonia")]
	public class UpdateAvailableWindowTests : IDisposable
	{
		/// <summary>桩服务器：GET 任意路径返回固定状态码 + 二进制 zip 载荷，可延迟响应
		///（把 updater 卡在收响应头阶段以测"下载中取消"）。与 UpdateDownloaderTests 的
		/// 口径一致：裸 TcpListener 直接绑 port 0（内核分配端口且 listener 立即持有，
		/// 无"先探测再绑定"的 TOCTOU 竞态），updater 下载重试会重复请求——每次请求都
		/// 回同一响应（非队列式）。帧式 HTTP/1.1 原始应答，二进制体不经字符串编码。</summary>
		private sealed class ZipStubServer : IDisposable
		{
			private readonly TcpListener _listener;

			private readonly Thread _thread;

			private volatile bool _running = true;

			private readonly int _statusCode;

			private readonly byte[] _payload;

			private readonly int _delayMilliseconds;

			private ZipStubServer(TcpListener listener, int port, int statusCode, byte[] payload, int delayMilliseconds)
			{
				_listener = listener;
				_statusCode = statusCode;
				_payload = payload;
				_delayMilliseconds = delayMilliseconds;
				BaseUrl = "http://127.0.0.1:" + port;
				_thread = new Thread((ThreadStart)delegate
				{
					AcceptLoop();
				})
				{
					IsBackground = true,
					Name = "UpdateE2eZipStubServer"
				};
				_thread.Start();
			}

			public string BaseUrl { get; }

			public static ZipStubServer Start(int statusCode, byte[] payload, int delayMilliseconds)
			{
				var listener = new TcpListener(IPAddress.Loopback, 0);
				listener.Start();
				int port = ((IPEndPoint)listener.LocalEndpoint).Port;
				return new ZipStubServer(listener, port, statusCode, payload, delayMilliseconds);
			}

			private void AcceptLoop()
			{
				while (_running)
				{
					TcpClient client;
					try
					{
						client = _listener.AcceptTcpClient();
					}
					catch (Exception)
					{
						break; // listener 已 Stop（Dispose 路径）
					}
					ThreadPool.QueueUserWorkItem(delegate (object state)
					{
						Handle((TcpClient)state);
					}, client);
				}
			}

			private void Handle(TcpClient client)
			{
				try
				{
					using (client)
					using (NetworkStream stream = client.GetStream())
					{
						ReadRequestHeaders(stream);
						if (_delayMilliseconds > 0)
						{
							Thread.Sleep(_delayMilliseconds);
						}
						byte[] body = _statusCode == 200 ? _payload : new byte[0];
						string reason = _statusCode == 200 ? "OK" : "Error";
						byte[] header = Encoding.ASCII.GetBytes(
							"HTTP/1.1 " + _statusCode + " " + reason + "\r\n"
							+ "Content-Type: application/zip\r\n"
							+ "Content-Length: " + body.Length + "\r\n"
							+ "Connection: close\r\n\r\n");
						stream.Write(header, 0, header.Length);
						stream.Write(body, 0, body.Length);
						stream.Flush();
					}
				}
				catch (Exception)
				{
					// updater 被 Kill 后写已断开的 socket——忽略
				}
			}

			/// <summary>读到空行（\r\n\r\n）即认为请求头结束（HttpClient GET 无请求体）。</summary>
			private static void ReadRequestHeaders(NetworkStream stream)
			{
				byte[] buffer = new byte[4096];
				var received = new StringBuilder();
				while (!received.ToString().EndsWith("\r\n\r\n", StringComparison.Ordinal))
				{
					int read = stream.Read(buffer, 0, buffer.Length);
					if (read <= 0)
					{
						return;
					}
					received.Append(Encoding.ASCII.GetString(buffer, 0, read));
				}
			}

			public void Dispose()
			{
				_running = false;
				try
				{
					_listener.Stop();
				}
				catch (Exception)
				{
				}
			}
		}

		/// <summary>每用例独立的更新会话：被替换的"安装目录"（预置旧标记文件）、
		/// ≥1MB 的平台包 zip（updater 的最小字节数校验要求 zip 本体过兆——用随机
		/// 数据填充防压缩）、死 wait-pid（updater 判定"主程序已退出"立即放行，
		/// 生产等真实主进程）。restartCommand 只需非空（--no-restart 下不执行）。</summary>
		private sealed class UpdateSession : IDisposable
		{
			public readonly string Root;

			public readonly string InstallDir;

			public readonly string RestartCommand;

			public readonly int WaitPid;

			public readonly byte[] ZipPayload;

			public UpdateSession()
			{
				Root = Path.Combine(Path.GetTempPath(), "fpe2e-upd-" + Guid.NewGuid().ToString("N").Substring(0, 10));
				InstallDir = Path.Combine(Root, "install");
				Directory.CreateDirectory(InstallDir);
				File.WriteAllText(Path.Combine(InstallDir, "old-marker.txt"), "old-version-4.1.0");
				// restart-command：任意非空路径即可（no-restart 下不执行），用 updater 自身路径
				RestartCommand = Path.Combine(AppContext.BaseDirectory, Consts.ForkPlus.AutoUpdaterFilename);
				WaitPid = CreateDeadProcessId();
				ZipPayload = BuildUpdateZip();
			}

			public void Dispose()
			{
				try
				{
					Directory.Delete(Root, recursive: true);
				}
				catch (Exception)
				{
				}
			}
		}

		private readonly UpdateSession _session;

		public UpdateAvailableWindowTests()
		{
			EnsureRuntimeRootForApphost();
			_session = new UpdateSession();
		}

		public void Dispose()
		{
			_session.Dispose();
		}

		[Fact]
		public void Submit_WithZipUrl_AutoDownloadsExtractsAndReplacesInstallDir()
		{
			HeadlessAppBootstrap.Run(delegate
			{
				string originalLanguage = ForkPlusSettings.Default.UiLanguage;
				try
				{
					ForkPlusSettings.Default.UiLanguage = "zh-Hans";
					AutoUpdateRunner capturedRunner = null;
					int downloadProgressMessages = 0;
					int waitingExitCount = 0;
					using (ZipStubServer server = ZipStubServer.Start(200, _session.ZipPayload, 0))
					{
						var window = CreateTestWindow(server.BaseUrl + "/update.zip", delegate (UpdateInfo info)
						{
							capturedRunner = new AutoUpdateRunner(
								AppContext.BaseDirectory, _session.InstallDir, _session.RestartCommand,
								_session.WaitPid, noRestart: true);
							// 挂自己的观察者（先于窗口订阅）：统计 download 进度消息是否真实流转
							capturedRunner.Progress += delegate (AutoUpdateProgress progress)
							{
								if (progress.TotalBytes > 0)
								{
									downloadProgressMessages++;
								}
							};
							return capturedRunner;
						}, delegate
						{
							waitingExitCount++;
						});
						try
						{
							ForkPlusDialogFooter footer = FindFooter(window);
							// 初始形态：结果区 + Download/Later 双按钮
							Assert.True(window.ContentPanel.IsVisible);
							Assert.False(window.DownloadPanel.IsVisible);
							Assert.Equal("下载", footer.SubmitButton.Content);
							Assert.Equal("稍后", footer.CancelButton.Content);

							footer.SubmitButton.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
							Dispatcher.UIThread.RunJobs();

							// 点击即切入下载进度面板：Submit 收起、Cancel 语义切"取消下载"
							Assert.True(window.DownloadPanel.IsVisible, "应显示下载进度面板");
							Assert.False(window.ContentPanel.IsVisible);
							Assert.False(footer.SubmitButton.IsVisible);
							Assert.Equal("取消下载", footer.CancelButton.Content);

							// 全链路：下载 → 解压 → waiting-exit（真实子进程 + 管道回流）
							Assert.True(WaitFor(delegate { return waitingExitCount > 0; }, 60000),
								"等待 waiting-exit 信号超时（updater 未完成下载+解压）");
							// 安装目录被新包整体替换：新文件就位、旧文件移入 backup
							Assert.True(WaitFor(delegate
							{
								return File.Exists(Path.Combine(_session.InstallDir, "new-marker.txt"));
							}, 30000), "安装目录未被新版本替换");
							Assert.Equal("updated-to-9.9.9", File.ReadAllText(Path.Combine(_session.InstallDir, "new-marker.txt")));
							Assert.False(File.Exists(Path.Combine(_session.InstallDir, "old-marker.txt")), "旧文件应移入 backup 目录");
							Assert.True(File.Exists(Path.Combine(_session.InstallDir, "pad.bin")), "子目录外的包内容应全部就位");
							Assert.True(Directory.Exists(Path.Combine(_session.InstallDir, "Docs")), "包内子目录应被搬入安装目录");

							// updater 成功退出（退出码 0）；download 进度消息至少流转过一条
							Assert.True(WaitFor(delegate { return capturedRunner.ExitCode == 0; }, 30000),
								"updater 进程未以退出码 0 结束");
							Assert.True(downloadProgressMessages >= 1, "下载进度消息应经管道回流到 UI");

							// waiting-exit 之后 UI 终态：等应用关闭交由 updater——不恢复结果区、
							// Cancel 收起（后续阶段不可安全中断）、状态文本停在"正在安装"
							Assert.False(window.ContentPanel.IsVisible);
							Assert.False(footer.CancelButton.IsVisible);
							Assert.Equal("正在安装更新...", window.DownloadPanel.StatusTextBlock.Text);
						}
						finally
						{
							window.Close();
							Dispatcher.UIThread.RunJobs();
						}
					}
				}
				finally
				{
					ForkPlusSettings.Default.UiLanguage = originalLanguage;
				}
			});
		}

		[Fact]
		public void CancelButton_DuringDownload_KillsUpdaterAndRestoresRetryUi()
		{
			HeadlessAppBootstrap.Run(delegate
			{
				string originalLanguage = ForkPlusSettings.Default.UiLanguage;
				try
				{
					ForkPlusSettings.Default.UiLanguage = "zh-Hans";
					AutoUpdateRunner capturedRunner = null;
					// 延迟 10s 响应：把 updater 卡在收响应头阶段（下载进行中、可安全取消）
					using (ZipStubServer server = ZipStubServer.Start(200, _session.ZipPayload, 10000))
					{
						var window = CreateTestWindow(server.BaseUrl + "/update.zip", delegate (UpdateInfo info)
						{
							capturedRunner = new AutoUpdateRunner(
								AppContext.BaseDirectory, _session.InstallDir, _session.RestartCommand,
								_session.WaitPid, noRestart: true);
							return capturedRunner;
						}, null);
						try
						{
							ForkPlusDialogFooter footer = FindFooter(window);
							footer.SubmitButton.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
							Dispatcher.UIThread.RunJobs();

							// 下载阶段到达（phase:downloading 经管道回流；延迟响应下无进度消息覆盖文本）
							Assert.True(WaitFor(delegate
							{
								return window.DownloadPanel.StatusTextBlock.Text == "正在下载更新...";
							}, 30000), "下载阶段未开始（updater 未上报 phase:downloading）");
							Assert.True(capturedRunner.IsRunning);

							// Footer Cancel = 取消下载（不关窗、Kill updater）
							footer.CancelButton.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
							Dispatcher.UIThread.RunJobs();

							// 取消后：恢复结果区可重试（Download 复现、Cancel 语义还原 Later）
							Assert.True(WaitFor(delegate { return window.ContentPanel.IsVisible; }, 15000),
								"取消后未恢复结果区");
							Assert.False(window.DownloadPanel.IsVisible);
							Assert.True(footer.SubmitButton.IsVisible);
							Assert.Equal("下载", footer.SubmitButton.Content);
							Assert.Equal("稍后", footer.CancelButton.Content);
							Assert.False(capturedRunner.IsRunning, "updater 应已被 Kill");
							Assert.True(footer.StatusMessageTextBlock.IsVisible == false, "取消非失败，不应展示错误状态");

							// 安装目录未被触碰（下载阶段取消无任何副作用）
							Assert.True(File.Exists(Path.Combine(_session.InstallDir, "old-marker.txt")));
							Assert.False(File.Exists(Path.Combine(_session.InstallDir, "new-marker.txt")));
						}
						finally
						{
							window.Close();
							Dispatcher.UIThread.RunJobs();
						}
					}
				}
				finally
				{
					ForkPlusSettings.Default.UiLanguage = originalLanguage;
				}
			});
		}

		[Fact]
		public void DownloadFailure_ShowsErrorInFooterStatus_KeepsWindowOpenForRetry()
		{
			HeadlessAppBootstrap.Run(delegate
			{
				string originalLanguage = ForkPlusSettings.Default.UiLanguage;
				try
				{
					ForkPlusSettings.Default.UiLanguage = "zh-Hans";
					AutoUpdateRunner capturedRunner = null;
					// 404：updater 下载重试 3 次耗尽 → error 消息 → 退出码 1
					using (ZipStubServer server = ZipStubServer.Start(404, new byte[0], 0))
					{
						var window = CreateTestWindow(server.BaseUrl + "/update.zip", delegate (UpdateInfo info)
						{
							capturedRunner = new AutoUpdateRunner(
								AppContext.BaseDirectory, _session.InstallDir, _session.RestartCommand,
								_session.WaitPid, noRestart: true);
							return capturedRunner;
						}, null);
						try
						{
							ForkPlusDialogFooter footer = FindFooter(window);
							footer.SubmitButton.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
							Dispatcher.UIThread.RunJobs();

							// 失败：Footer 状态区（现有弹窗操作失败的标准展示位）展示"更新失败：{原因}"
							Assert.True(WaitFor(delegate { return footer.StatusMessageTextBlock.IsVisible; }, 30000),
								"失败状态未在 Footer 状态区展示");
							Assert.StartsWith("更新失败：", footer.StatusMessageTextBlock.Text);
							Assert.Contains("404", footer.StatusMessageTextBlock.Text);

							// 结果区恢复：可重试 Download 或关闭 Later（窗口保持打开）
							Assert.True(window.ContentPanel.IsVisible);
							Assert.False(window.DownloadPanel.IsVisible);
							Assert.True(footer.SubmitButton.IsVisible);

							// updater 以失败码退出
							Assert.True(WaitFor(delegate { return capturedRunner.ExitCode == 1; }, 15000),
								"updater 未以失败退出码 1 结束");
						}
						finally
						{
							window.Close();
							Dispatcher.UIThread.RunJobs();
						}
					}
				}
				finally
				{
					ForkPlusSettings.Default.UiLanguage = originalLanguage;
				}
			});
		}

		// ===== AutoUpdatePackageUrl.Resolve 纯单元（无 UI / 无子进程） =====

		[Fact]
		public void AutoUpdatePackageUrl_ZipDirectLink_IsPassedThrough()
		{
			string url = "https://example.com/ForkPlus-9.9.9-linux-x64.zip";
			Assert.Equal(url, AutoUpdatePackageUrl.Resolve(url, "9.9.9", "linux-x64"));
		}

		[Fact]
		public void AutoUpdatePackageUrl_ReleasePageUrl_ConstructsRuleDownloadAddress()
		{
			// Release 页地址（资产缺失回退）+ 版本号 + 平台 → 规则下载地址（v 前缀归一）
			Assert.Equal(
				AutoUpdatePackageUrl.ReleaseDownloadBase + "v9.9.9/ForkPlus-9.9.9-win-x64.zip",
				AutoUpdatePackageUrl.Resolve("https://github.com/hebin123456/ForkPlus/releases/tag/v9.9.9", " v9.9.9 ", "win-x64"));
			Assert.Equal(
				AutoUpdatePackageUrl.ReleaseDownloadBase + "v4.1.0/ForkPlus-4.1.0-macos-arm64.zip",
				AutoUpdatePackageUrl.Resolve(null, "4.1.0", "macos-arm64"));
		}

		[Fact]
		public void AutoUpdatePackageUrl_MissingVersionOrPlatform_ReturnsNull()
		{
			Assert.Null(AutoUpdatePackageUrl.Resolve(null, null, "linux-x64")); // 无版本无地址
			Assert.Null(AutoUpdatePackageUrl.Resolve(null, "9.9.9", null)); // 无平台
			Assert.Null(AutoUpdatePackageUrl.Resolve(null, "  ", "linux-x64")); // 版本空白
			// 非 http(s) 地址不透传，但版本+平台可用时仍构造规则下载地址（下载入口不受坏地址拖累）
			Assert.Equal(
				AutoUpdatePackageUrl.ReleaseDownloadBase + "v9.9.9/ForkPlus-9.9.9-linux-x64.zip",
				AutoUpdatePackageUrl.Resolve("ftp://example.com/a.zip", "9.9.9", "linux-x64"));
		}

		// ===== 修复回归（2026-10-08，v5.0.6）：Download 按钮"闪一下、原地踏步" =====
		// 症状拆两半：① Windows 上 --install-dir 值以 \ 结尾 → 吃掉结束引号 → 吞掉
		// --restart-command 整段 → updater 参数解析失败、连管道前退出码 1（无任何消息）；
		// ② 主窗口把"无消息早退"当用户取消静默复位，于是用户看不到任何反馈。

		/// <summary>Windows 命令行切分（CommandLineToArgvW / .NET apphost 子进程的实际
		/// 解析口径）。Linux 上 ProcessStartInfo.Arguments 走另一套更宽松的切分，所以
		/// "参数在 Windows 上是否被吞"必须用这套规则断言，否则 CI（Linux）测不出来。</summary>
		private static string[] WindowsCommandLineTokens(string commandLine)
		{
			var tokens = new List<string>();
			var current = new StringBuilder();
			bool inQuotes = false;
			bool hasToken = false;
			int index = 0;
			while (index < commandLine.Length)
			{
				char c = commandLine[index];
				if (c == '\\')
				{
					int slashes = 0;
					while (index < commandLine.Length && commandLine[index] == '\\')
					{
						slashes++;
						index++;
					}
					if (index < commandLine.Length && commandLine[index] == '"')
					{
						// N 个反斜杠 + 引号：产出 N/2 个字面反斜杠；N 为奇数时引号是字面量，
						// 偶数时引号才起"开/合引号"作用（这正是结尾单反斜杠会吃掉结束引号的由来）。
						current.Append('\\', slashes / 2);
						if (slashes % 2 == 1)
						{
							current.Append('"');
						}
						else
						{
							inQuotes = !inQuotes;
						}
						index++;
					}
					else
					{
						current.Append('\\', slashes);
					}
					hasToken = true;
					continue;
				}
				if (c == '"')
				{
					inQuotes = !inQuotes;
					hasToken = true;
					index++;
					continue;
				}
				if (!inQuotes && (c == ' ' || c == '\t'))
				{
					if (hasToken)
					{
						tokens.Add(current.ToString());
						current.Clear();
						hasToken = false;
					}
					index++;
					continue;
				}
				current.Append(c);
				hasToken = true;
				index++;
			}
			if (hasToken)
			{
				tokens.Add(current.ToString());
			}
			return tokens.ToArray();
		}

		private static string ArgumentValue(string[] tokens, string key)
		{
			for (int i = 0; i + 1 < tokens.Length; i++)
			{
				if (tokens[i] == key)
				{
					return tokens[i + 1];
				}
			}
			return null;
		}

		/// <summary>Windows 安装目录（AppContext.BaseDirectory 必然以 \ 结尾）经
		/// BuildArguments 后必须原样还原、且 --restart-command 不被吞——修复前
		/// `"C:\...\"` 的结束引号被转义，install-dir 值吞掉后随的 --restart-command
		/// 段，updater 侧更新检查项认不出 → 退出码 1 → 用户看到"点了没反应"。</summary>
		[Fact]
		public void BuildArguments_WindowsInstallDirWithTrailingBackslash_SurvivesWindowsParsing()
		{
			HeadlessAppBootstrap.Run(delegate
			{
				string installDir = "C:\\Users\\me\\AppData\\Local\\ForkPlus\\";
				string restartCommand = "C:\\Users\\me\\AppData\\Local\\ForkPlus\\ForkPlus.exe";
				string zipUrl = "https://example.com/ForkPlus-5.0.6-windows-x64.zip";
				using (var runner = new AutoUpdateRunner(installDir, installDir, restartCommand, waitPid: 4242, noRestart: false))
				{
					string commandLine = runner.BuildArguments(zipUrl);
					string[] tokens = WindowsCommandLineTokens(commandLine);

					Assert.Equal(zipUrl, ArgumentValue(tokens, "--url"));
					Assert.Equal(installDir, ArgumentValue(tokens, "--install-dir"));
					Assert.Equal(restartCommand, ArgumentValue(tokens, "--restart-command"));
					Assert.Equal("4242", ArgumentValue(tokens, "--wait-pid"));
					Assert.Equal(App.ProcessId.ToString(), ArgumentValue(tokens, "--pipe-pid"));

					// 参数个数与键集合必须是 updater 认识的形态：多出来的 token（被吞后
					// 挤出的裸路径）会让 UpdateOptions.Parse 命中未知键 → 退出码 1。
					Assert.Equal(10, tokens.Length);
					string[] knownKeys = { "--url", "--install-dir", "--restart-command", "--pipe-pid", "--wait-pid" };
					Assert.All(tokens.Where(delegate (string t) { return t.StartsWith("--"); }),
						delegate (string key) { Assert.Contains(key, knownKeys); });
				}
			});
		}

		/// <summary>无结尾反斜杠的值（Linux/macOS 路径、Windows 可执行文件路径）输出
		/// 必须与修复前逐字节一致——本次修复只动"结尾连续反斜杠"这一种输入。</summary>
		[Theory]
		[InlineData("/opt/forkplus/")]
		[InlineData("/opt/forkplus")]
		[InlineData("C:\\ForkPlus")]
		[InlineData("C:\\ForkPlus\\ForkPlus.exe")]
		[InlineData("D:\\Program Files\\ForkPlus")]
		public void Quote_ValueWithoutTrailingBackslash_IsUnchanged(string value)
		{
			Assert.Equal("\"" + value + "\"", AutoUpdateRunner.Quote(value));
		}

		[Fact]
		public void Quote_TrailingBackslashes_AreDoubled()
		{
			// 值本身不变；只是把结尾 \ 翻倍，让结束引号不被转义（Windows 解析规则要求）
			Assert.Equal("\"C:\\ForkPlus\\\\\"", AutoUpdateRunner.Quote("C:\\ForkPlus\\"));
			Assert.Equal("\"C:\\ForkPlus\\\\\\\\\"", AutoUpdateRunner.Quote("C:\\ForkPlus\\\\"));
			Assert.Equal("\"\"", AutoUpdateRunner.Quote(null));
			Assert.Equal("\"a'b\"", AutoUpdateRunner.Quote("a\"b"));
		}

		/// <summary>updater 起进程即早退、且一条消息都不发（复现"参数被吞 / 连不上管道"）：
		/// 修复前窗口静默复位（用户看到"闪一下、原地踏步"），修复后必须按退出码报错。
		/// 桩 updater 是 sh 脚本（测试套件只在 Linux 跑；Windows 下自行跳过）。</summary>
		[Fact]
		public void UpdaterEarlyExitWithoutPipeMessage_ReportsErrorInsteadOfSilentRestore()
		{
			if (OperatingSystem.IsWindows())
			{
				return;
			}
			HeadlessAppBootstrap.Run(delegate
			{
				string originalLanguage = ForkPlusSettings.Default.UiLanguage;
				string stubDir = Path.Combine(Path.GetTempPath(), "fpe2e-upd-stub-" + Guid.NewGuid().ToString("N").Substring(0, 8));
				try
				{
					ForkPlusSettings.Default.UiLanguage = "zh-Hans";
					Directory.CreateDirectory(stubDir);
					string stubPath = Path.Combine(stubDir, Consts.ForkPlus.AutoUpdaterFilename);
					File.WriteAllText(stubPath, "#!/bin/sh\nexit 1\n");
					File.SetUnixFileMode(stubPath,
						UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);

					var window = CreateTestWindow("http://127.0.0.1:1/update.zip", delegate (UpdateInfo info)
					{
						// 桩：启动即退出码 1、从不连进度管道
						return new AutoUpdateRunner(stubDir, _session.InstallDir, _session.RestartCommand, _session.WaitPid, noRestart: true);
					}, null);
					try
					{
						ForkPlusDialogFooter footer = FindFooter(window);
						footer.SubmitButton.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
						Dispatcher.UIThread.RunJobs();

						// 修复后：不再静默复位——Footer 状态区（现有弹窗错误的标准展示位）报退出码
						Assert.True(WaitFor(delegate { return footer.StatusMessageTextBlock.IsVisible; }, 15000),
							"updater 无消息早退必须报错，不能静默恢复结果区（原症状：闪一下、原地踏步）");
						Assert.StartsWith("更新失败：", footer.StatusMessageTextBlock.Text);
						Assert.Contains("code 1", footer.StatusMessageTextBlock.Text);

						// 结果区恢复：可重试 Download 或关闭 Later
						Assert.True(window.ContentPanel.IsVisible);
						Assert.False(window.DownloadPanel.IsVisible);
						Assert.True(footer.SubmitButton.IsVisible);
					}
					finally
					{
						window.Close();
						Dispatcher.UIThread.RunJobs();
					}
				}
				finally
				{
					ForkPlusSettings.Default.UiLanguage = originalLanguage;
					try
					{
						Directory.Delete(stubDir, recursive: true);
					}
					catch (Exception)
					{
					}
				}
			});
		}

		/// <summary>构造被测窗口：注入 Runner 工厂与 waiting-exit 替代动作（生产真正 Shutdown）。</summary>
		private static UpdateAvailableWindow CreateTestWindow(
			string downloadUrl,
			Func<UpdateInfo, AutoUpdateRunner> runnerFactory,
			Action waitingExitAction)
		{
			var info = new UpdateInfo
			{
				LatestVersion = "9.9.9",
				CurrentVersion = "4.1.0",
				HasUpdate = true,
				ReleaseName = "v9.9.9",
				ReleaseNotes = "e2e release notes",
				DownloadUrl = downloadUrl
			};
			var window = new UpdateAvailableWindow(info)
			{
				RunnerFactoryForTests = runnerFactory,
				WaitingExitActionForTests = waitingExitAction
			};
			window.Show();
			Dispatcher.UIThread.RunJobs();
			return window;
		}

		private static ForkPlusDialogFooter FindFooter(UpdateAvailableWindow window)
		{
			ForkPlusDialogFooter footer = window.GetVisualDescendants().OfType<ForkPlusDialogFooter>().FirstOrDefault();
			Assert.NotNull(footer);
			return footer;
		}

		/// <summary>UI 线程上的轮询等待：RunJobs 处理管道回流 Post 到调度器的进度事件，
		/// 短睡眠让子进程与 IpcServer 线程推进（Run 的委托跑在 UI 线程，标准 headless 模式）。</summary>
		private static bool WaitFor(Func<bool> condition, int timeoutMilliseconds)
		{
			DateTime deadline = DateTime.UtcNow.AddMilliseconds(timeoutMilliseconds);
			while (true)
			{
				Dispatcher.UIThread.RunJobs();
				if (condition())
				{
					return true;
				}
				if (DateTime.UtcNow >= deadline)
				{
					return false;
				}
				Thread.Sleep(20);
			}
		}

		/// <summary>单根目录平台包 zip（ForkPlus-9.9.9-test/）：新标记文件 + Docs 子目录 +
		/// 随机 pad（不可压缩，保证 zip 本体 ≥1MB 过 updater 最小字节数校验）。</summary>
		private static byte[] BuildUpdateZip()
		{
			byte[] pad = new byte[1200 * 1024];
			new Random(42).NextBytes(pad);
			using (var buffer = new MemoryStream())
			{
				using (var archive = new ZipArchive(buffer, ZipArchiveMode.Create))
				{
					AddZipEntry(archive, "ForkPlus-9.9.9-test/new-marker.txt", Encoding.UTF8.GetBytes("updated-to-9.9.9"));
					AddZipEntry(archive, "ForkPlus-9.9.9-test/Docs/RELEASE_NOTE.md", Encoding.UTF8.GetBytes("## v9.9.9\ne2e"));
					AddZipEntry(archive, "ForkPlus-9.9.9-test/pad.bin", pad);
				}
				return buffer.ToArray();
			}
		}

		private static void AddZipEntry(ZipArchive archive, string entryName, byte[] content)
		{
			ZipArchiveEntry entry = archive.CreateEntry(entryName);
			using (Stream stream = entry.Open())
			{
				stream.Write(content, 0, content.Length);
			}
		}

		/// <summary>产出"已死 pid"（&gt;0 且不存活）：updater 的 --wait-pid 校验拒绝 ≤0，
		/// 死 pid 让"等待主程序退出"立即通过（生产等真实主进程退出后才替换）。
		/// 复用 updater 自身：无参启动即用法错误退出（退出码 1），pid 天然跨平台。</summary>
		private static int CreateDeadProcessId()
		{
			string updaterPath = Path.Combine(AppContext.BaseDirectory, Consts.ForkPlus.AutoUpdaterFilename);
			using (Process probe = Process.Start(new ProcessStartInfo
			{
				FileName = updaterPath,
				Arguments = "--url", // 缺值：解析失败立即退出
				UseShellExecute = false,
				CreateNoWindow = true
			}))
			{
				probe.WaitForExit(10000);
				return probe.Id;
			}
		}

		/// <summary>apphost 子进程定位 .NET 运行时需要 DOTNET_ROOT（GitHub CI 的
		/// actions/setup-dotnet 自动设置）。本地/沙箱未设置时从当前共享运行时位置推导：
		/// System.Private.CoreLib.dll 位于 &lt;root&gt;/shared/Microsoft.NETCore.App/&lt;ver&gt;/，
		/// 向上四级即 dotnet 根。进程级 SetEnvironmentVariable 对后续 spawn 的子进程可见。</summary>
		private static void EnsureRuntimeRootForApphost()
		{
			if (!string.IsNullOrEmpty(Environment.GetEnvironmentVariable("DOTNET_ROOT")))
			{
				return;
			}
			string coreLib = typeof(object).Assembly.Location;
			if (string.IsNullOrEmpty(coreLib))
			{
				return;
			}
			string directory = Path.GetDirectoryName(coreLib);
			for (int i = 0; i < 3 && !string.IsNullOrEmpty(directory); i++)
			{
				directory = Path.GetDirectoryName(directory);
			}
			// directory = &lt;root&gt;/shared —— 再上一级为 dotnet 根；校验存在才设置（自包含布局则跳过）
			string root = Path.GetDirectoryName(directory);
			if (!string.IsNullOrEmpty(root) && Directory.Exists(Path.Combine(root, "shared")))
			{
				Environment.SetEnvironmentVariable("DOTNET_ROOT", root);
			}
		}
	}
}

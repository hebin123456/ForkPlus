// E2E 测试（v4.1.1，"重置此版本"全链路）：UpdateCheckWindow 的"重置此版本"超链接入口
// 不再是摆设——点击后经两次确认（①重置当前版本 ②是否重置设置），随后按当前版本构造
// GitHub 规则直链，复用 AutoUpdateRunner 子进程完成 下载 → 解压 → 等待退出 → 备份替换
// 安装目录 全链路（进度经命名管道 Fork_Pipe{pid}_Update 回流驱动 UI，与
// UpdateAvailableWindowTests 的自动更新流同一套测试基础设施：测试输出目录的四件套由
// csproj Copy 任务备齐 + 本地 zip 桩服务器 + 死 wait-pid + --no-restart）。
// 覆盖五条用户可感知路径：
//   ① 确认重置 + 重置设置：两次弹窗 → runner 带 --reset-settings/--settings-file 启动 →
//      全链路成功后设置文件被删除、安装目录被新包替换、updater 退出码 0；
//   ② 确认重置 + 保留设置：runner 不带重置设置参数 → 设置文件原样保留、安装目录被替换；
//   ③ 第一次确认取消：不启动重置流、下载面板不出现、安装目录未触碰；
//   ④ 第二次确认取消：等价于"保留设置"（重置进行但设置保留）；
//   ⑤ 下载失败：Footer 状态区展示"更新失败：{原因}"、窗口保持可重试、设置文件保留。
// 注入点：CheckerForTests（快失败桩 checker 避免真实出网与落盘）、
// ConfirmResetDialogForTests / ConfirmResetSettingsDialogForTests（替代两次确认弹窗）、
// ResetRunnerFactoryForTests（临时安装目录 + 临时设置文件 + 死 wait-pid + --no-restart，
// 生产用真实安装目录/本进程 pid/自动重启）、ResetPackageUrlForTests（桩服务器地址）、
// WaitingExitActionForTests（替代真正 Shutdown）。
using System;
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
	public class UpdateCheckWindowResetTests : IDisposable
	{
		/// <summary>桩服务器（与 UpdateAvailableWindowTests 同款）：GET 任意路径返回固定
		/// 状态码 + 二进制 zip 载荷，可延迟响应。裸 TcpListener 绑 port 0（内核分配端口、
		/// 无 TOCTOU），每次请求都回同一响应。帧式 HTTP/1.1 原始应答，二进制体不经字符串编码。</summary>
		private sealed class ZipStubServer : IDisposable
		{
			private readonly TcpListener _listener;

			private readonly Thread _thread;

			private volatile bool _running = true;

			private readonly int _statusCode;

			private readonly byte[] _payload;

			private ZipStubServer(TcpListener listener, int port, int statusCode, byte[] payload)
			{
				_listener = listener;
				_statusCode = statusCode;
				_payload = payload;
				BaseUrl = "http://127.0.0.1:" + port;
				_thread = new Thread((ThreadStart)delegate
				{
					AcceptLoop();
				})
				{
					IsBackground = true,
					Name = "UpdateCheckE2eZipStubServer"
				};
				_thread.Start();
			}

			public string BaseUrl { get; }

			public static ZipStubServer Start(int statusCode, byte[] payload)
			{
				var listener = new TcpListener(IPAddress.Loopback, 0);
				listener.Start();
				int port = ((IPEndPoint)listener.LocalEndpoint).Port;
				return new ZipStubServer(listener, port, statusCode, payload);
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
						break;
					}
					ThreadPool.QueueUserWorkItem(delegate (object state)
					{
						HandleDl((TcpClient)state);
					}, client);
				}
			}

			private void HandleDl(TcpClient client)
			{
				try
				{
					using (client)
					using (NetworkStream stream = client.GetStream())
					{
						ReadRequestHeaders(stream);
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
				}
			}

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

		/// <summary>每用例独立的重置会话：被替换的"安装目录"（预置旧标记文件）、临时设置文件、
		/// ≥1MB 平台包 zip（过 updater 最小字节数校验）、死 wait-pid、非空 restartCommand。</summary>
		private sealed class ResetSession : IDisposable
		{
			public readonly string Root;

			public readonly string InstallDir;

			public readonly string SettingsPath;

			public readonly string InstallerPath;

			public readonly int WaitPid;

			public readonly byte[] ZipPayload;

			public ResetSession()
			{
				Root = Path.Combine(Path.GetTempPath(), "fpe2e-updchk-" + Guid.NewGuid().ToString("N").Substring(0, 10));
				InstallDir = Path.Combine(Root, "install");
				Directory.CreateDirectory(InstallDir);
				File.WriteAllText(Path.Combine(InstallDir, "old-marker.txt"), "old-version-4.1.1");
				// 模拟主程序 settings.json（重置设置流删除；保留设置流必须原样保留）
				SettingsPath = Path.Combine(Root, "settings.json");
				File.WriteAllText(SettingsPath, "{\"uiLanguage\":\"en\"}");
				InstallerPath = Path.Combine(AppContext.BaseDirectory, Consts.ForkPlus.AutoUpdaterFilename);
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

		private readonly ResetSession _session;

		public UpdateCheckWindowResetTests()
		{
			EnsureRuntimeRootForApphost();
			_session = new ResetSession();
		}

		public void Dispose()
		{
			_session.Dispose();
		}

		// ===== ① 确认重置 + 重置设置：设置删除 + 安装目录替换 + 退出码 0 =====

		[Fact]
		public void ResetWithSettings_ConfirmedTwice_DownloadsReplacesInstallAndDeletesSettings()
		{
			HeadlessAppBootstrap.Run(delegate
			{
				string originalLanguage = ForkPlusSettings.Default.UiLanguage;
				try
				{
					ForkPlusSettings.Default.UiLanguage = "zh-Hans";
					bool confirmReset = false;
					bool confirmResetSettings = false;
					bool? capturedResetSettings = null;
					AutoUpdateRunner capturedRunner = null;
					int waitingExitCount = 0;
					using (ZipStubServer server = ZipStubServer.Start(200, _session.ZipPayload))
					{
						UpdateCheckWindow window = CreateTestWindow(
							ConfirmResetDialogForTests: () => { confirmReset = true; return true; },
							ConfirmResetSettingsDialogForTests: () => { confirmResetSettings = true; return true; },
							runnerFactory: (resetSettings) =>
							{
								capturedResetSettings = resetSettings;
								capturedRunner = new AutoUpdateRunner(
									AppContext.BaseDirectory, _session.InstallDir, _session.InstallerPath,
									_session.WaitPid, noRestart: true,
									resetSettings: resetSettings,
									settingsFile: resetSettings ? _session.SettingsPath : null);
								return capturedRunner;
							},
							resetUrl: server.BaseUrl + "/ForkPlus-4.1.1-linux-x64.zip",
							waitingExit: () => waitingExitCount++);
						try
						{
							Assert.True(window.ResetVersionButton.IsVisible, "重置入口初始可见");
							Assert.False(window.DownloadPanel.IsVisible, "初始不显示下载面板");

							RaiseResetClick(window);

							// 两次确认都被调起（先版本后设置），runner 带重置设置语义启动
							Assert.True(confirmReset, "第一次确认（重置当前版本）应被调起");
							Assert.True(confirmResetSettings, "第二次确认（重置设置）应被调起");
							Assert.True(capturedResetSettings.Value, "应请求重置设置");
							Assert.True(window.DownloadPanel.IsVisible, "重置启动后显示下载进度面板");
							Assert.False(window.ResetVersionButton.IsVisible, "重置中隐藏重置入口");

							// 全链路：下载 → 解压 → waiting-exit（真实子进程 + 管道回流）
							Assert.True(WaitFor(delegate { return waitingExitCount > 0; }, 60000),
								"等待 waiting-exit 信号超时（updater 未完成下载+解压）");
							// 安装目录被新包替换、旧文件移入 backup
							Assert.True(WaitFor(delegate
							{
								return File.Exists(Path.Combine(_session.InstallDir, "new-marker.txt"));
							}, 30000), "安装目录未被新版本替换");
							Assert.Equal("updated-to-4.1.1", File.ReadAllText(Path.Combine(_session.InstallDir, "new-marker.txt")));
							Assert.False(File.Exists(Path.Combine(_session.InstallDir, "old-marker.txt")), "旧文件应移入 backup 目录");

							// 重置设置语义：文件替换成功后设置文件被删除
							Assert.True(WaitFor(delegate { return !File.Exists(_session.SettingsPath); }, 30000),
								"重置设置流成功后 settings.json 应被删除");

							// updater 成功退出
							Assert.True(WaitFor(delegate { return capturedRunner.ExitCode == 0; }, 30000),
								"updater 未以退出码 0 结束");
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

		// ===== ② 确认重置 + 保留设置：设置保留 + 安装目录替换 =====

		[Fact]
		public void ResetKeepSettings_ConfirmedResetButNotSettings_ReplacesInstallKeepsSettings()
		{
			HeadlessAppBootstrap.Run(delegate
			{
				string originalLanguage = ForkPlusSettings.Default.UiLanguage;
				try
				{
					ForkPlusSettings.Default.UiLanguage = "zh-Hans";
					bool? capturedResetSettings = null;
					int waitingExitCount = 0;
					using (ZipStubServer server = ZipStubServer.Start(200, _session.ZipPayload))
					{
						UpdateCheckWindow window = CreateTestWindow(
							ConfirmResetDialogForTests: () => true,
							ConfirmResetSettingsDialogForTests: () => false, // 二次确认点"保留设置"
							runnerFactory: (resetSettings) =>
							{
								capturedResetSettings = resetSettings;
								return new AutoUpdateRunner(
									AppContext.BaseDirectory, _session.InstallDir, _session.InstallerPath,
									_session.WaitPid, noRestart: true,
									resetSettings: resetSettings,
									settingsFile: resetSettings ? _session.SettingsPath : null);
							},
							resetUrl: server.BaseUrl + "/ForkPlus-4.1.1-linux-x64.zip",
							waitingExit: () => waitingExitCount++);
						try
						{
							RaiseResetClick(window);

							Assert.False(capturedResetSettings.Value, "保留设置：runner 不应请求重置设置");

							// 重置照常进行（下载 → 解压 → waiting-exit → 安装目录替换）
							Assert.True(WaitFor(delegate { return waitingExitCount > 0; }, 60000),
								"等待 waiting-exit 信号超时");
							Assert.True(WaitFor(delegate
							{
								return File.Exists(Path.Combine(_session.InstallDir, "new-marker.txt"));
							}, 30000), "安装目录未被新版本替换");
							Assert.False(File.Exists(Path.Combine(_session.InstallDir, "old-marker.txt")));

							// 保留设置语义：设置文件必须原样保留
							Assert.True(File.Exists(_session.SettingsPath), "保留设置流 settings.json 不应被删除");
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

		// ===== ③ 第一次确认取消：不启动重置流 =====

		[Fact]
		public void FirstConfirmation_Cancelled_DoesNotStartReset()
		{
			HeadlessAppBootstrap.Run(delegate
			{
				string originalLanguage = ForkPlusSettings.Default.UiLanguage;
				try
				{
					ForkPlusSettings.Default.UiLanguage = "zh-Hans";
					bool runnerCreated = false;
					using (ZipStubServer server = ZipStubServer.Start(404, new byte[0]))
					{
						UpdateCheckWindow window = null;
						try
						{
							// 不注入 Runner 工厂（生产路径）；第一次确认弹窗替代动作返回 false（用户取消）
							window = new UpdateCheckWindow
							{
								CheckerForTests = new UpdateChecker("http://127.0.0.1:9/", 2), // 快失败桩（避免真实出网）
								ConfirmResetDialogForTests = () => false,
								ResetRunnerFactoryForTests = (resetSettings) =>
								{
									runnerCreated = true;
									return null; // 不应被调用（生产为 CreateForVersionReset）
								},
								ResetPackageUrlForTests = () => server.BaseUrl + "/x.zip"
							};
							window.Show();
							Dispatcher.UIThread.RunJobs();

							RaiseResetClick(window);

							// 取消第一次确认：不启动重置流、下载面板不出现、runner 工厂不被调用
							Assert.False(runnerCreated, "用户取消第一次确认后不应创建 runner");
							Assert.False(window.DownloadPanel.IsVisible, "取消确认后不应出现下载面板");
							Assert.True(window.ResetVersionButton.IsVisible, "取消确认后重置入口仍可见");
						}
						finally
						{
							window?.Close();
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

		// ===== ⑤ 下载失败：Footer 状态区展示错误、窗口保持可重试、设置文件保留 =====

		[Fact]
		public void ResetDownloadFailure_ShowsErrorInFooterStatus_KeepsSettingsIntact()
		{
			HeadlessAppBootstrap.Run(delegate
			{
				string originalLanguage = ForkPlusSettings.Default.UiLanguage;
				try
				{
					ForkPlusSettings.Default.UiLanguage = "zh-Hans";
					AutoUpdateRunner capturedRunner = null;
					using (ZipStubServer server = ZipStubServer.Start(404, new byte[0]))
					{
						UpdateCheckWindow window = CreateTestWindow(
							ConfirmResetDialogForTests: () => true,
							ConfirmResetSettingsDialogForTests: () => true,
							runnerFactory: (resetSettings) =>
							{
								capturedRunner = new AutoUpdateRunner(
									AppContext.BaseDirectory, _session.InstallDir, _session.InstallerPath,
									_session.WaitPid, noRestart: true,
									resetSettings: resetSettings,
									settingsFile: resetSettings ? _session.SettingsPath : null);
								return capturedRunner;
							},
							resetUrl: server.BaseUrl + "/ForkPlus-4.1.1-linux-x64.zip",
							waitingExit: null);
						try
						{
							RaiseResetClick(window);

							// 失败：Footer 状态区展示"更新失败：{原因}"
							ForkPlusDialogFooter footer = FindFooter(window);
							Assert.True(WaitFor(delegate { return footer.StatusMessageTextBlock.IsVisible; }, 30000),
								"失败状态未在 Footer 状态区展示");
							Assert.StartsWith("更新失败：", footer.StatusMessageTextBlock.Text);
							Assert.Contains("404", footer.StatusMessageTextBlock.Text);

							// 重置入口恢复、下载面板收起（可重试或关闭）
							Assert.True(window.ResetVersionButton.IsVisible);
							Assert.False(window.DownloadPanel.IsVisible);

							// 失败路径：设置文件必须原样保留
							Assert.True(File.Exists(_session.SettingsPath), "更新失败路径 settings.json 应保留");

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

		// ===== 辅助 =====

		private static void RaiseResetClick(UpdateCheckWindow window)
		{
			window.ResetVersionButton.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
			Dispatcher.UIThread.RunJobs();
		}

		private static UpdateCheckWindow CreateTestWindow(
			Func<bool> ConfirmResetDialogForTests,
			Func<bool> ConfirmResetSettingsDialogForTests,
			Func<bool, AutoUpdateRunner> runnerFactory,
			string resetUrl,
			Action waitingExit)
		{
			var window = new UpdateCheckWindow
			{
				CheckerForTests = new UpdateChecker("http://127.0.0.1:1/", 2), // 快失败桩（避免真实出网）
				ConfirmResetDialogForTests = ConfirmResetDialogForTests,
				ConfirmResetSettingsDialogForTests = ConfirmResetSettingsDialogForTests,
				ResetRunnerFactoryForTests = runnerFactory,
				ResetPackageUrlForTests = () => resetUrl,
				WaitingExitActionForTests = waitingExit
			};
			window.Show();
			Dispatcher.UIThread.RunJobs();
			return window;
		}

		private static ForkPlusDialogFooter FindFooter(UpdateCheckWindow window)
		{
			ForkPlusDialogFooter footer = window.GetVisualDescendants().OfType<ForkPlusDialogFooter>().FirstOrDefault();
			Assert.NotNull(footer);
			return footer;
		}

		/// <summary>UI 线程轮询等待：RunJobs 处理管道回流 Post 到调度器的进度事件。</summary>
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

		/// <summary>单根目录平台包 zip（ForkPlus-4.1.1-test/）：新标记文件 + Docs 子目录 +
		/// 随机 pad（不可压缩，保证 zip 本体 ≥1MB 过 updater 最小字节数校验）。</summary>
		private static byte[] BuildUpdateZip()
		{
			byte[] pad = new byte[1200 * 1024];
			new Random(42).NextBytes(pad);
			using (var buffer = new MemoryStream())
			{
				using (var archive = new ZipArchive(buffer, ZipArchiveMode.Create))
				{
					AddZipEntry(archive, "ForkPlus-4.1.1-test/new-marker.txt", Encoding.UTF8.GetBytes("updated-to-4.1.1"));
					AddZipEntry(archive, "ForkPlus-4.1.1-test/Docs/RELEASE_NOTE.md", Encoding.UTF8.GetBytes("## v4.1.1\ne2e"));
					AddZipEntry(archive, "ForkPlus-4.1.1-test/pad.bin", pad);
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

		private static int CreateDeadProcessId()
		{
			string updaterPath = Path.Combine(AppContext.BaseDirectory, Consts.ForkPlus.AutoUpdaterFilename);
			using (Process probe = Process.Start(new ProcessStartInfo
			{
				FileName = updaterPath,
				Arguments = "--url",
				UseShellExecute = false,
				CreateNoWindow = true
			}))
			{
				probe.WaitForExit(10000);
				return probe.Id;
			}
		}

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
			string root = Path.GetDirectoryName(directory);
			if (!string.IsNullOrEmpty(root) && Directory.Exists(Path.Combine(root, "shared")))
			{
				Environment.SetEnvironmentVariable("DOTNET_ROOT", root);
			}
		}
	}
}
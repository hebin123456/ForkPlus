// v4.0.5（2026-09-09）崩溃兜底日志回归测试。
// 背景："UI 经常无响应崩溃，有兜底的日志机制吗，有的话我到时候可以把崩溃前的日志反应给你"。
// 机制：CrashDumper 独立写 logs/crash-时间戳.log（同步落盘，不经 NLog 缓冲）+
//       fork.log 改滚动归档（不再启动即删）+ UI 异常置 Handled=true 应用存活。
//
// 用例：
//   1) Dump 写文件：版本/系统/Kind/异常全链（含 Inner）与堆栈均在；
//   2) 保留策略：超过 10 份 crash-*.log 时按文件名时间序清理最旧；
//   3) 端到端：向 Dispatcher.Post 一个抛异常的操作——真实路由到
//      App_DispatcherUnhandledException → 落 crash 转储 + Handled=true 进程存活
//      （本用例能继续执行断言本身就是"存活"的证明）。
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using Avalonia.Threading;
using Xunit;
using Xunit.Abstractions;

namespace ForkPlus.Tests
{
	[Collection("HeadlessAvalonia")]
	public class CrashLoggingBottomLineTests
	{
		private readonly ITestOutputHelper _output;

		public CrashLoggingBottomLineTests(ITestOutputHelper output)
		{
			_output = output;
		}

		private static List<string> SnapshotCrashFiles()
		{
			try
			{
				string dir = CrashDumper.CrashLogDirectory;
				return (Directory.Exists(dir) ? Directory.GetFiles(dir, "crash-*.log") : Array.Empty<string>()).ToList();
			}
			catch
			{
				return new List<string>();
			}
		}

		private static void CleanupNewFiles(List<string> before)
		{
			foreach (string file in SnapshotCrashFiles())
			{
				if (!before.Contains(file))
				{
					try { File.Delete(file); } catch { }
				}
			}
		}

		[Fact]
		public void CrashDumper_Dump_WritesFileWithFullContext()
		{
			List<string> before = SnapshotCrashFiles();
			try
			{
				Exception exception;
				try
				{
					throw new InvalidOperationException("outer-crash-marker", new ArgumentException("inner-crash-marker"));
				}
				catch (Exception ex)
				{
					exception = ex;
				}
				CrashDumper.Dump("unit-test", exception, "context-marker-42");

				string file = SnapshotCrashFiles().FirstOrDefault((string x) => !before.Contains(x) && File.ReadAllText(x).Contains("outer-crash-marker"));
				Assert.NotNull(file);
				string content = File.ReadAllText(file);
				_output.WriteLine(file);
				Assert.Contains("Kind:      unit-test", content);
				Assert.Contains("Context:   context-marker-42", content);
				Assert.Contains("Version:   ", content);
				Assert.Contains("OS:        ", content);
				Assert.Contains("InvalidOperationException", content);
				Assert.Contains("outer-crash-marker", content);
				Assert.Contains("ArgumentException", content);
				Assert.Contains("inner-crash-marker", content);
				Assert.Contains("StackTrace:", content);
				Assert.Contains(nameof(CrashLoggingBottomLineTests), content);
			}
			finally
			{
				CleanupNewFiles(before);
			}
		}

		[Fact]
	public void CrashDumper_PrunesOldDumps_RetainsTen()
	{
		// 密封验证（v4.0.5 修订）：保留策略断言要求完全掌控目录内容。原实现断言真实
		// 数据目录的文件数——但其他用例的 UI/后台异常会异步落转储（UnobservedTaskException
		// 在 finalizer 线程触发，时机不受控），跨用例残留 9 份时"伪造 12+新 1=13→清理后
		// 恰好 10"的算术被打破（fake[11] 被挤掉）。改用临时目录隔离：
		//   1) GC 冲刷先引爆此前用例埋下的未观测任务异常（转储落真实目录，不进沙箱）；
		//   2) 重定向转储目录到空临时目录：12 伪造 + 1 新 = 13 → 清理后恰 10，确定性成立。
		GC.Collect();
		GC.WaitForPendingFinalizers();
		GC.Collect();
		GC.WaitForPendingFinalizers();
		string sandbox = Path.Combine(Path.GetTempPath(), "fp-crash-prune-" + Guid.NewGuid().ToString("N"));
		try
		{
			CrashDumper.CrashLogDirectoryOverrideForTests = sandbox;
			Assert.Equal(sandbox, CrashDumper.CrashLogDirectory);
			Directory.CreateDirectory(sandbox);
			// 12 份伪造旧转储（2020 时间戳，文件名序早于任何真实转储）
			List<string> fake = new List<string>();
			for (int i = 0; i < 12; i++)
			{
				string path = Path.Combine(sandbox, $"crash-20200101-0000{i:00}-000.log");
				File.WriteAllText(path, "fake");
				fake.Add(path);
			}
			CrashDumper.Dump("prune-test", new InvalidOperationException("prune-marker"), null);
			string[] remaining = Directory.GetFiles(sandbox, "crash-*.log");
			_output.WriteLine($"crash files after dump: {remaining.Length}");
			// 12 + 1 = 13 → 上限 10：清理最旧 3 份（fake[0..2]），保留新转储 + fake[3..11]
			Assert.Equal(10, remaining.Length);
			Assert.Contains(remaining, (string x) => File.ReadAllText(x).Contains("prune-marker"));
			Assert.False(File.Exists(fake[0]), "最旧转储应被清理");
			Assert.False(File.Exists(fake[1]), "次旧转储应被清理");
			Assert.False(File.Exists(fake[2]), "第三旧转储应被清理");
			Assert.True(File.Exists(fake[11]), "最新伪造转储应保留");
		}
		finally
		{
			CrashDumper.CrashLogDirectoryOverrideForTests = null;
			try { Directory.Delete(sandbox, true); } catch { }
		}
	}

		[Fact]
		public void UiException_RoutesToDumpAndAppSurvives()
		{
			List<string> before = SnapshotCrashFiles();
			try
			{
				HeadlessAppBootstrap.Run(delegate
				{
					// 经 Dispatcher.Post 触发真实路由：UI 线程操作抛异常 →
					// Dispatcher.UnhandledException → App_DispatcherUnhandledException
					// （CrashDumper 落盘 + e.Handled=true 吞掉异常）。
					Dispatcher.UIThread.Post(delegate
					{
						throw new InvalidOperationException("ui-crash-e2e-marker-7f3a");
					});
				});
				// 进程存活到此处即证明 Handled=true 生效（否则未处理异常会终止 testhost）。
				bool dumped = SpinWait.SpinUntil(delegate
				{
					return SnapshotCrashFiles().Any((string x) => !before.Contains(x) && SafeReadContains(x, "ui-crash-e2e-marker-7f3a"));
				}, 15000);
				Assert.True(dumped, "UI 异常应在 15s 内路由到 CrashDumper 落盘（否则该机制未生效）");
				_output.WriteLine("UI 异常已兜底落盘，进程存活");
			}
			finally
			{
				CleanupNewFiles(before);
			}
		}

		private static bool SafeReadContains(string path, string marker)
		{
			try
			{
				return File.ReadAllText(path).Contains(marker);
			}
			catch
			{
				return false;
			}
		}
	}
}

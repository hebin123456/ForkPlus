using System;
using System.IO;
using System.Linq;
using ForkPlus.UI.Commands;
using Xunit;

namespace ForkPlus.Tests
{
	// v4.0.6（2026-09-10）UI 冻结看门狗 + native 转储 + 诊断包导出回归测试。
	// 看门狗线程本体不在单测中启动（需要真实 Dispatcher 泵），直测其纯函数核心、
	// 报告落盘/保留策略与 NativeDumps 的环境变量/清理逻辑。
	public class UiFreezeWatchdogTests
	{
		[Fact]
		public void IsFrozen_RecentHeartbeat_IsNotFrozen()
		{
			long now = Environment.TickCount64;
			Assert.False(UiFreezeWatchdog.IsFrozen(now - 1000, now, UiFreezeWatchdog.FreezeThresholdMs));
		}

		[Fact]
		public void IsFrozen_HeartbeatOlderThanThreshold_IsFrozen()
		{
			long now = Environment.TickCount64;
			Assert.True(UiFreezeWatchdog.IsFrozen(now - (UiFreezeWatchdog.FreezeThresholdMs + 1000), now, UiFreezeWatchdog.FreezeThresholdMs));
		}

		[Fact]
		public void IsFrozen_ExactlyAtThreshold_IsNotFrozen()
		{
			long now = Environment.TickCount64;
			Assert.False(UiFreezeWatchdog.IsFrozen(now - UiFreezeWatchdog.FreezeThresholdMs, now, UiFreezeWatchdog.FreezeThresholdMs));
		}

		[Fact]
		public void BuildFreezeReport_ContainsVersionSystemAndJobSection()
		{
			string report = UiFreezeWatchdog.BuildFreezeReport(6123);
			Assert.Contains("ForkPlus UI freeze report", report, StringComparison.Ordinal);
			Assert.Contains("FrozenFor:  6.1s", report, StringComparison.Ordinal);
			Assert.Contains("Version:    " + App.Version, report, StringComparison.Ordinal);
			Assert.Contains("Running background jobs", report, StringComparison.Ordinal);
		}

		[Fact]
		public void WriteFreezeReport_WritesFileToDirectory_AndPrunesToTen()
		{
			string directory = Path.Combine(Path.GetTempPath(), "fpfreeze-" + Guid.NewGuid().ToString("N"));
			Directory.CreateDirectory(directory);
			string previous = UiFreezeWatchdog.FreezeLogDirectoryOverrideForTests;
			UiFreezeWatchdog.FreezeLogDirectoryOverrideForTests = directory;
			try
			{
				for (int i = 0; i < 12; i++)
				{
					string path = UiFreezeWatchdog.WriteFreezeReport(6000 + i);
					Assert.False(string.IsNullOrEmpty(path), "report path expected");
					Assert.True(File.Exists(path), "freeze report file should exist: " + path);
					// 文件名时间戳精度毫秒，快速连写保证互不重名。
					System.Threading.Thread.Sleep(2);
				}
				string[] files = Directory.GetFiles(directory, "freeze-*.log");
				Assert.Equal(10, files.Length);
			}
			finally
			{
				UiFreezeWatchdog.FreezeLogDirectoryOverrideForTests = previous;
				try
				{
					Directory.Delete(directory, recursive: true);
				}
				catch
				{
				}
			}
		}

		[Fact]
		public void NativeDumps_PruneOldDumps_KeepsFiveNewest()
		{
			string directory = Path.Combine(Path.GetTempPath(), "fpdumps-" + Guid.NewGuid().ToString("N"));
			Directory.CreateDirectory(directory);
			string previous = NativeDumps.DumpDirectoryOverrideForTests;
			NativeDumps.DumpDirectoryOverrideForTests = directory;
			try
			{
				for (int i = 1; i <= 7; i++)
				{
					File.WriteAllText(Path.Combine(directory, "dump-" + i.ToString("D3") + ".dmp"), "x");
				}
				NativeDumps.PruneOldDumps();
				string[] remaining = Directory.GetFiles(directory, "dump-*.dmp").Select(Path.GetFileName).OrderBy((string x) => x, StringComparer.Ordinal).ToArray();
				Assert.Equal(new[] { "dump-003.dmp", "dump-004.dmp", "dump-005.dmp", "dump-006.dmp", "dump-007.dmp" }, remaining);
			}
			finally
			{
				NativeDumps.DumpDirectoryOverrideForTests = previous;
				try
				{
					Directory.Delete(directory, recursive: true);
				}
				catch
				{
				}
			}
		}

		[Fact]
		public void NativeDumps_EnsureEnvVariable_DoesNotOverrideExistingValue()
		{
			string name = "FORKPLUS_TEST_DUMP_ENV_" + Guid.NewGuid().ToString("N");
			try
			{
				Environment.SetEnvironmentVariable(name, null);
				NativeDumps.EnsureEnvVariable(name, "default-value");
				Assert.Equal("default-value", Environment.GetEnvironmentVariable(name));
				// 用户已显式设置的值优先，不被默认值覆盖。
				Environment.SetEnvironmentVariable(name, "user-value");
				NativeDumps.EnsureEnvVariable(name, "default-value");
				Assert.Equal("user-value", Environment.GetEnvironmentVariable(name));
			}
			finally
			{
				Environment.SetEnvironmentVariable(name, null);
			}
		}

		[Fact]
		public void ExportDiagnostics_ExportTo_CollectsLogs_WritesReadme_SkipsOversized()
		{
			string logsDirectory = Path.Combine(Path.GetTempPath(), "fpdiag-" + Guid.NewGuid().ToString("N"));
			Directory.CreateDirectory(logsDirectory);
			string targetZip = Path.Combine(Path.GetTempPath(), "fpdiag-" + Guid.NewGuid().ToString("N") + ".zip");
			try
			{
				File.WriteAllText(Path.Combine(logsDirectory, "fork.log"), "app log line");
				File.WriteAllText(Path.Combine(logsDirectory, "crash-20260910-120000-000.log"), "crash report");
				File.WriteAllText(Path.Combine(logsDirectory, "freeze-20260910-120500-000.log"), "freeze report");
				// 超过 100MB 上限的大转储：应被跳过并计入 SkippedFiles。
				File.WriteAllBytes(Path.Combine(logsDirectory, "dump-1234.dmp"), new byte[101 * 1024 * 1024]);

				ExportDiagnosticsCommand.ExportResult result = ExportDiagnosticsCommand.ExportTo(logsDirectory, targetZip);
				Assert.Null(result.Error);
				Assert.True(File.Exists(targetZip), "zip should exist");
				Assert.Equal(3, result.IncludedCount);
				Assert.Single(result.SkippedFiles);
				Assert.Contains("dump-1234.dmp", result.SkippedFiles[0], StringComparison.Ordinal);

				using (var archive = System.IO.Compression.ZipFile.OpenRead(targetZip))
				{
					var names = archive.Entries.Select(e => e.FullName).OrderBy(x => x, StringComparer.Ordinal).ToArray();
					Assert.Contains("fork.log", names);
					Assert.Contains("crash-20260910-120000-000.log", names);
					Assert.Contains("freeze-20260910-120500-000.log", names);
					Assert.Contains("README.txt", names);
					Assert.DoesNotContain(names, (string x) => x == "dump-1234.dmp");
					var readme = archive.GetEntry("README.txt");
					using (var reader = new StreamReader(readme.Open()))
					{
						string content = reader.ReadToEnd();
						Assert.Contains("ForkPlus diagnostics package", content, StringComparison.Ordinal);
						Assert.Contains("Version:  " + App.Version, content, StringComparison.Ordinal);
					}
				}
			}
			finally
			{
				try
				{
					Directory.Delete(logsDirectory, recursive: true);
				}
				catch
				{
				}
				try
				{
					File.Delete(targetZip);
				}
				catch
				{
				}
			}
		}
	}
}

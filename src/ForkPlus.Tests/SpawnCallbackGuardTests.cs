using System;
using System.IO;
using System.Linq;
using Xunit;

namespace ForkPlus.Tests
{
	// v4.0.6（2026-09-10）reverse P/Invoke 回调防护回归测试：
	// HandleCallback 由 biturbo 读管道线程直接进入托管代码，stdout/stderr 处理器
	// 抛出的异常若未被边界拦截，将展开穿过 native 栈帧触发 CLR FailFast——进程
	// 无声消失，任何 App 级兜底失效。本测试用会抛异常的处理器跑真实 git 命令：
	// 防护生效 = 进程存活（测试能继续断言）+ 命令正常结束 + 异常落 crash-*.log
	// （kind=SpawnCallback）。
	public class SpawnCallbackGuardTests
	{
		[Fact]
		public void ThrowingStdoutHandler_IsContainedAtBoundary_CommandCompletes()
		{
			string directory = Path.Combine(Path.GetTempPath(), "fpcallback-" + Guid.NewGuid().ToString("N"));
			Directory.CreateDirectory(directory);
			string previous = CrashDumper.CrashLogDirectoryOverrideForTests;
			CrashDumper.CrashLogDirectoryOverrideForTests = directory;
			try
			{
				var inner = new SpawnWithCallbackInner(
					path: "git",
					workingDirectory: null,
					arguments: new[] { "--version" },
					env: Array.Empty<string>(),
					stdin: null,
					stdoutLineHandler: delegate (string line)
					{
						throw new InvalidOperationException("intentional parse failure for line: " + line);
					},
					stderrLineHandler: null,
					monitor: null);
				Result<int, ISpawnError> result = inner.Spawn();
				// 命令本身必须正常结束（git --version 退出码 0）——处理器异常只丢一行日志，不影响进程结果。
				Assert.True(result.IsOk, "spawn should succeed, got error: " + result.Error);
				Assert.Equal(0, result.Value);
				// 异常被边界拦截并落崩溃转储（kind=SpawnCallback）。
				string[] crashLogs = Directory.GetFiles(directory, "crash-*.log");
				Assert.True(crashLogs.Length > 0, "expected a crash-*.log written by the boundary guard");
				string report = File.ReadAllText(crashLogs[0]);
				Assert.Contains("Kind:      SpawnCallback", report, StringComparison.Ordinal);
				Assert.Contains("intentional parse failure", report, StringComparison.Ordinal);
			}
			finally
			{
				CrashDumper.CrashLogDirectoryOverrideForTests = previous;
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
		public void HealthyHandlers_AreUnaffected()
		{
			var seenLines = new System.Collections.Generic.List<string>();
			var inner = new SpawnWithCallbackInner(
				path: "git",
				workingDirectory: null,
				arguments: new[] { "--version" },
				env: Array.Empty<string>(),
				stdin: null,
				stdoutLineHandler: delegate (string line) { seenLines.Add(line); },
				stderrLineHandler: null,
				monitor: null);
			Result<int, ISpawnError> result = inner.Spawn();
			Assert.True(result.IsOk, "spawn should succeed, got error: " + result.Error);
			Assert.Equal(0, result.Value);
			Assert.True(seenLines.Count > 0, "expected at least one stdout line");
			Assert.Contains("git version", seenLines[0], StringComparison.Ordinal);
		}
	}
}

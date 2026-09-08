// 回归测试（2026-09-07，"Linux 持久化失效，每次启动弹引导窗"）：
// 根因——FileHelper.WriteFile 原先用 Path.GetTempFileName() 在 TMPDIR（/tmp）建临时文件再
// rename 到目标目录。/tmp 在 Linux 上多为独立 tmpfs，与目标目录（$HOME 所在磁盘）跨设备时
// rename(2) 抛 EXDEV "Invalid cross-device link"：File.Replace 无回退 → settings.json
// 目标已存在后的每次保存静默失败。真实用户机上 git 实例配置窗的保存先创建了 settings.json，
// 引导窗的 Guid 保存走 Replace → EXDEV → Guid 永久丢失 → 每次启动都弹引导窗。
// 修复——临时文件建在目标同目录（与 UndoIndexStore 同款 POSIX 标准原子写法）。
//
// 本测试类两层保障：
//   1) 跨设备实证（Linux 环境自动探测，探测不到则跳过）：目标目录与 TMPDIR 不同设备时，
//      AtomicWrite 必须成功（修复前在此场景抛 EXDEV 失败）；
//   2) 可移植不变量：内容往返、目标已存在时覆盖、无 *.tmp 残留、Unix 权限 0600
//      （accounts.json 含凭据，权限不得比原 GetTempFileName 语义放宽）。
using System;
using System.IO;
using System.Linq;
using Xunit;
using Xunit.Abstractions;

namespace ForkPlus.Tests
{
	public class FileHelperAtomicWriteCrossDeviceTests
	{
		private readonly ITestOutputHelper _output;

		public FileHelperAtomicWriteCrossDeviceTests(ITestOutputHelper output)
		{
			_output = output;
		}

		/// <summary>
		/// 探测一个与 TMPDIR 不同设备且可写的目录（直接用 File.Replace 是否抛 EXDEV 判定，
		/// 不依赖 stat/shell）。找不到返回 null（同设备机器上跳过跨设备断言）。
		/// </summary>
		private string? FindCrossDeviceWritableDirectory()
		{
			if (OperatingSystem.IsWindows())
			{
				return null;
			}
			string tmpDir = Path.GetTempPath();
			// 候选：测试程序集目录（仓库所在盘，真实 Linux 开发机上多为与 /tmp 不同的设备）、
			// 当前目录、/var/tmp、/root、/home。
			string[] candidates =
			{
				AppContext.BaseDirectory,
				Directory.GetCurrentDirectory(),
				"/var/tmp",
				"/root",
				"/home"
			};
			foreach (string candidate in candidates)
			{
				try
				{
					if (string.IsNullOrWhiteSpace(candidate) || !Directory.Exists(candidate))
					{
						continue;
					}
					string probe = Path.Combine(candidate, "fp-dev-probe-" + Guid.NewGuid().ToString("N").Substring(0, 8));
					string tmp = Path.GetTempFileName();
					try
					{
						File.WriteAllText(probe, "probe");
						try
						{
							File.Replace(tmp, probe, null);
							// 未抛异常 → 同设备，不适用
						}
						catch (IOException ex) when (ex.Message.Contains("cross-device", StringComparison.OrdinalIgnoreCase))
						{
							return candidate; // EXDEV → 跨设备，正中靶心
						}
					}
					finally
					{
						try { File.Delete(probe); } catch { }
						try { File.Delete(tmp); } catch { }
					}
				}
				catch
				{
					// 候选目录不可写或异常，试下一个
				}
			}
			return null;
		}

		[Fact]
		public void AtomicWrite_SucceedsWhenTargetDirectoryCrossDeviceFromTmp()
		{
			string? crossDeviceDir = FindCrossDeviceWritableDirectory();
			if (crossDeviceDir == null)
			{
				_output.WriteLine("SKIP：未找到与 TMPDIR 跨设备的可写目录（本机 /tmp 与各候选目录同设备），跨设备断言不适用。");
				return;
			}
			_output.WriteLine($"cross-device dir = {crossDeviceDir}, TMPDIR = {Path.GetTempPath()}");
			string targetDir = Path.Combine(crossDeviceDir, "fp-atomic-write-crossdev-" + Guid.NewGuid().ToString("N").Substring(0, 8));
			string target = Path.Combine(targetDir, "settings.json");
			try
			{
				Directory.CreateDirectory(targetDir);
				// 修复前：首次写（目标缺失，走 File.Move 跨设备复制回退）可能成功，
				// 但第二次（目标已存在，走 File.Replace）抛 EXDEV → AtomicWrite 返回 false。
				Assert.True(global::ForkPlus.FileHelper.AtomicWrite(target, "first"), "跨设备首次写入应成功");
				Assert.Equal("first", File.ReadAllText(target));
				Assert.True(global::ForkPlus.FileHelper.AtomicWrite(target, "second"), "跨设备覆盖写入应成功（修复前此处 EXDEV 静默失败）");
				Assert.Equal("second", File.ReadAllText(target));
				_output.WriteLine("cross-device write verified: first→second round-trip OK");
			}
			finally
			{
				try { Directory.Delete(targetDir, recursive: true); } catch { }
			}
		}

		[Fact]
		public void AtomicWrite_RoundTripsAndLeavesNoTempFiles()
		{
			string targetDir = Path.Combine(Path.GetTempPath(), "fp-atomic-write-" + Guid.NewGuid().ToString("N").Substring(0, 8));
			string target = Path.Combine(targetDir, "settings.json");
			try
			{
				Directory.CreateDirectory(targetDir);
				Assert.True(global::ForkPlus.FileHelper.AtomicWrite(target, "{\"a\":1}"), "首次写入应成功");
				Assert.True(global::ForkPlus.FileHelper.AtomicWrite(target, "{\"a\":2}"), "覆盖写入应成功");
				Assert.Equal("{\"a\":2}", File.ReadAllText(target));
				string[] leftovers = Directory.GetFiles(targetDir, "*.tmp");
				Assert.Empty(leftovers); // 临时文件必须被 rename 消费或失败时删除，不得残留
			}
			finally
			{
				try { Directory.Delete(targetDir, recursive: true); } catch { }
			}
		}

		[Fact]
		public void AtomicWrite_UnixPermissionsStayOwnerOnly()
		{
			if (OperatingSystem.IsWindows())
			{
				return; // Windows 无 Unix 权限语义
			}
			string targetDir = Path.Combine(Path.GetTempPath(), "fp-atomic-perm-" + Guid.NewGuid().ToString("N").Substring(0, 8));
			string target = Path.Combine(targetDir, "accounts.json");
			try
			{
				Directory.CreateDirectory(targetDir);
				Assert.True(global::ForkPlus.FileHelper.AtomicWrite(target, "{}"));
				global::System.IO.UnixFileMode mode = File.GetUnixFileMode(target);
				_output.WriteLine($"mode = {mode}");
				Assert.True(mode.HasFlag(global::System.IO.UnixFileMode.UserRead), "属主可读");
				Assert.True(mode.HasFlag(global::System.IO.UnixFileMode.UserWrite), "属主可写");
				Assert.False(mode.HasFlag(global::System.IO.UnixFileMode.GroupRead), "同组不可读（原 GetTempFileName 0600 语义，accounts.json 含凭据）");
				Assert.False(mode.HasFlag(global::System.IO.UnixFileMode.OtherRead), "其他不可读");
			}
			finally
			{
				try { Directory.Delete(targetDir, recursive: true); } catch { }
			}
		}
	}
}

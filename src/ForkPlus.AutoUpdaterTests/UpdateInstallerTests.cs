using System;
using System.IO;
using System.IO.Compression;
using System.Threading;
using ForkPlus.AutoUpdater;
using Xunit;

namespace ForkPlus.AutoUpdaterTests
{
	/// <summary>
	/// 更新安装器测试：zip 解压（单根目录/平铺两形态）、安装目录备份替换、
	/// 失败回滚（安装目录不留半新半旧态）、等待进程退出、历史残留清理。
	/// 全部在临时目录内操作，进程等待用当前测试进程 pid（恒存活）验证超时语义。
	/// </summary>
	public class UpdateInstallerTests : IDisposable
	{
		private readonly string _root;

		public UpdateInstallerTests()
		{
			_root = Path.Combine(Path.GetTempPath(), "fpau-tests-" + Guid.NewGuid().ToString("N").Substring(0, 10));
			Directory.CreateDirectory(_root);
		}

		public void Dispose()
		{
			try
			{
				Directory.Delete(_root, recursive: true);
			}
			catch (Exception)
			{
			}
		}

		private string CreateZip(string relativePath, params string[] entries)
		{
			string zipPath = Path.Combine(_root, relativePath);
			using (FileStream stream = File.Create(zipPath))
			using (ZipArchive archive = new ZipArchive(stream, ZipArchiveMode.Create))
			{
				foreach (string entry in entries)
				{
					ZipArchiveEntry archiveEntry = archive.CreateEntry(entry);
					using (StreamWriter writer = new StreamWriter(archiveEntry.Open()))
					{
						writer.Write("content-of-" + entry);
					}
				}
			}
			return zipPath;
		}

		[Fact]
		public void Extract_SingleRootZip_ReturnsRootDirectory()
		{
			string zipPath = CreateZip("single.zip", "ForkPlus-4.1.0-windows-x64/ForkPlus.exe", "ForkPlus-4.1.0-windows-x64/ForkPlus.dll");
			UpdateInstaller installer = new UpdateInstaller(Path.Combine(_root, "install"), "unused");
			string extracted = installer.Extract(zipPath, Path.Combine(_root, "staging"));
			Assert.Equal(Path.Combine(_root, "staging", "ForkPlus-4.1.0-windows-x64"), extracted);
			Assert.True(File.Exists(Path.Combine(extracted, "ForkPlus.exe")));
			Assert.True(File.Exists(Path.Combine(extracted, "ForkPlus.dll")));
		}

		[Fact]
		public void Extract_FlatZip_ReturnsExtractDirectory()
		{
			string zipPath = CreateZip("flat.zip", "ForkPlus.exe", "README.md");
			UpdateInstaller installer = new UpdateInstaller(Path.Combine(_root, "install"), "unused");
			string extracted = installer.Extract(zipPath, Path.Combine(_root, "staging"));
			Assert.Equal(Path.Combine(_root, "staging"), extracted);
			Assert.True(File.Exists(Path.Combine(extracted, "ForkPlus.exe")));
			Assert.True(File.Exists(Path.Combine(extracted, "README.md")));
		}

		[Fact]
		public void Extract_ReplacesExistingStagingDirectory()
		{
			string staging = Path.Combine(_root, "staging");
			Directory.CreateDirectory(staging);
			File.WriteAllText(Path.Combine(staging, "stale-file.txt"), "old");
			string zipPath = CreateZip("flat.zip", "new.txt");
			UpdateInstaller installer = new UpdateInstaller(Path.Combine(_root, "install"), "unused");
			string extracted = installer.Extract(zipPath, staging);
			Assert.False(File.Exists(Path.Combine(staging, "stale-file.txt")), "旧解压目录应被清空重建");
			Assert.True(File.Exists(Path.Combine(extracted, "new.txt")));
		}

		[Fact]
		public void Replace_MovesOldToBackupAndNewToInstall()
		{
			string installDir = Path.Combine(_root, "install");
			string newDir = Path.Combine(_root, "new");
			string backupDir = Path.Combine(_root, "backup");
			Directory.CreateDirectory(installDir);
			Directory.CreateDirectory(newDir);
			File.WriteAllText(Path.Combine(installDir, "old-app.txt"), "old");
			File.WriteAllText(Path.Combine(newDir, "new-app.txt"), "new");
			Directory.CreateDirectory(Path.Combine(newDir, "sub"));
			File.WriteAllText(Path.Combine(newDir, "sub", "nested.txt"), "nested");

			UpdateInstaller installer = new UpdateInstaller(installDir, "unused");
			installer.Replace(newDir, backupDir);

			Assert.True(File.Exists(Path.Combine(installDir, "new-app.txt")), "新文件应就位");
			Assert.True(File.Exists(Path.Combine(installDir, "sub", "nested.txt")), "新子目录应就位");
			Assert.False(File.Exists(Path.Combine(installDir, "old-app.txt")), "旧文件不应残留");
			Assert.True(File.Exists(Path.Combine(backupDir, "old-app.txt")), "旧文件应在备份里");
		}

		[Fact]
		public void Replace_NewFilesDirMissing_ThrowsAndRollsBackInstall()
		{
			string installDir = Path.Combine(_root, "install");
			string backupDir = Path.Combine(_root, "backup");
			string missingNewDir = Path.Combine(_root, "does-not-exist");
			Directory.CreateDirectory(installDir);
			File.WriteAllText(Path.Combine(installDir, "app.txt"), "original");

			UpdateInstaller installer = new UpdateInstaller(installDir, "unused");
			Assert.ThrowsAny<Exception>(delegate
			{
				installer.Replace(missingNewDir, backupDir);
			});

			// 回滚后安装目录必须完整恢复（不留半新半旧态）
			Assert.True(File.Exists(Path.Combine(installDir, "app.txt")));
			Assert.Equal("original", File.ReadAllText(Path.Combine(installDir, "app.txt")));
		}

		[Fact]
		public void Replace_EmptyInstallDir_IsHandled()
		{
			string installDir = Path.Combine(_root, "install-empty");
			string newDir = Path.Combine(_root, "new");
			string backupDir = Path.Combine(_root, "backup");
			Directory.CreateDirectory(installDir);
			Directory.CreateDirectory(newDir);
			File.WriteAllText(Path.Combine(newDir, "app.txt"), "new");
			UpdateInstaller installer = new UpdateInstaller(installDir, "unused");
			installer.Replace(newDir, backupDir);
			Assert.True(File.Exists(Path.Combine(installDir, "app.txt")));
		}

		[Fact]
		public void WaitForMainProcessExit_NonExistentPid_ReturnsTrue()
		{
			UpdateInstaller installer = new UpdateInstaller(Path.Combine(_root, "install"), "unused");
			Assert.True(installer.WaitForMainProcessExit(99999999, 1000));
		}

		[Fact]
		public void WaitForMainProcessExit_InvalidPid_ReturnsTrue()
		{
			UpdateInstaller installer = new UpdateInstaller(Path.Combine(_root, "install"), "unused");
			Assert.True(installer.WaitForMainProcessExit(0, 1000));
			Assert.True(installer.WaitForMainProcessExit(-5, 1000));
		}

		[Fact]
		public void WaitForMainProcessExit_AliveProcess_TimesOut()
		{
			UpdateInstaller installer = new UpdateInstaller(Path.Combine(_root, "install"), "unused");
			// 当前测试进程恒存活 → 短超时必返回 false
			Assert.False(installer.WaitForMainProcessExit(Environment.ProcessId, 600));
		}

		[Fact]
		public void CleanupStaleWorkDirs_RemovesOnlyOldDirectories()
		{
			string workRoot = Path.Combine(_root, "workroot");
			string oldDir = Path.Combine(workRoot, "u-old");
			string freshDir = Path.Combine(workRoot, "u-fresh");
			Directory.CreateDirectory(oldDir);
			Directory.CreateDirectory(freshDir);
			// 先写文件再回拨目录时间戳：向目录写入条目会把目录 LastWriteTime 拉回当前
			File.WriteAllText(Path.Combine(oldDir, "leftover.zip"), "x");
			Directory.SetLastWriteTimeUtc(oldDir, DateTime.UtcNow.AddDays(-3));
			Directory.SetLastWriteTimeUtc(freshDir, DateTime.UtcNow.AddMinutes(-1));

			UpdateInstaller.CleanupStaleWorkDirs(workRoot, TimeSpan.FromDays(1));

			Assert.False(Directory.Exists(oldDir), "超过阈值的残留应被清理");
			Assert.True(Directory.Exists(freshDir), "新目录不应被清理");
		}

		[Fact]
		public void CleanupStaleWorkDirs_MissingRoot_DoesNotThrow()
		{
			UpdateInstaller.CleanupStaleWorkDirs(Path.Combine(_root, "no-such-root"), TimeSpan.FromDays(1));
		}
	}
}

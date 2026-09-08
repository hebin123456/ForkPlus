using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Runtime.InteropServices;

namespace ForkPlus
{
	internal static class FileHelper
	{
		[Flags]
		private enum MoveFileFlags
		{
			None = 0,
			ReplaceExisting = 1,
			CopyAllowed = 2,
			DelayUntilReboot = 4,
			WriteThrough = 8,
			CreateHardlink = 0x10,
			FailIfNotTrackable = 0x20
		}

		public static long? GetFileSize(string filePath)
		{
			try
			{
				FileInfo fileInfo = new FileInfo(filePath);
				if (fileInfo.Exists)
				{
					return fileInfo.Length;
				}
			}
			catch (Exception ex)
			{
				Log.Error(ex.Message);
			}
			return null;
		}

		public static string GetReadableFileSize(long fileSize, bool addSizeInBytes = true)
		{
			string text = FileSizeFormatter.Format(fileSize);
			string text2;
			if (!addSizeInBytes)
			{
				text2 = text;
				if (text2 == null)
				{
					return "";
				}
			}
			else
			{
				text2 = text + " (" + GetReadableFileSizeInBytes(fileSize) + ")";
			}
			return text2;
		}

		public static string GetReadableFileSizeInBytes(long fileSize)
		{
			NumberFormatInfo numberFormatInfo = new NumberFormatInfo();
			numberFormatInfo.NumberGroupSizes = new int[1] { 3 };
			numberFormatInfo.NumberGroupSeparator = ",";
			NumberFormatInfo numberFormatInfo2 = numberFormatInfo;
			return fileSize.ToString("N0", numberFormatInfo2) + " B";
		}

		public static bool AtomicWrite(string filepath, string content)
		{
			for (int i = 0; i < 3; i++)
			{
				try
				{
					WriteFile(filepath, content);
				}
				catch (Exception ex)
				{
					Log.Error($"Failed to write to '{filepath}' {i}", ex);
					continue;
				}
				return true;
			}
			return false;
		}

		public static void OpenInWindowsExplorer(string absolutePath)
		{
			// Migration note：跨平台文件管理器定位。原 Windows 专用 explorer.exe /select 在
			// Unix 上静默失败（Process.Start 抛 Win32Exception 被下方 catch 吞掉）。
			// - macOS: open -R（Reveal in Finder，等价 /select 选中文件）
			// - Linux: xdg-open 父目录（org.freedesktop.FileManager1.ShowItems 依赖桌面环境，
			//   无通用"选中"机制，打开所在目录是稳妥等价物）
			try
			{
				if (File.Exists(absolutePath) || Directory.Exists(absolutePath))
				{
					if (OperatingSystem.IsWindows())
					{
						string arguments = BuildWindowsExplorerArguments(absolutePath, File.Exists(absolutePath));
						Process.Start(new ProcessStartInfo("explorer.exe", arguments) { UseShellExecute = true });
					}
					else if (OperatingSystem.IsMacOS())
					{
						Process.Start(new ProcessStartInfo("open", "-R \"" + absolutePath + "\""));
					}
					else
					{
						string target = Directory.Exists(absolutePath) ? absolutePath : Path.GetDirectoryName(absolutePath);
						Process.Start(new ProcessStartInfo("xdg-open", "\"" + target + "\""));
					}
				}
			}
			catch (Exception ex)
			{
				Log.Error("Failed to show file in file manager", ex);
			}
		}

		// 兑现 ShowFileInFileExplorerCommand 迁移注释"Windows 分隔符交给 FileHelper 内部处理"的承诺
		// （此前是空头承诺：git 相对路径恒为正斜杠，Path.Combine 后产生混合分隔符路径，如
		// C:\repo\src/App.cs——.NET 的 File.Exists 接受正斜杠使上方守卫通过，但 explorer.exe
		// 解析不了 /select 里的正斜杠，Windows 会忽略 /select 直接打开"文档"库，即用户报告的
		// "在文件资源管理器中显示，一直是打开文档目录"。WPF 原版在命令层 Replace("/", "\\")，
		// 迁移时误删；等价规范化收敛到本层，且只在 Windows 分支调用——Unix 上反斜杠是合法
		// 文件名字符，不能替换。纯函数抽出供 Linux CI 回归测试（无法执行 Windows 分支本身）。
		internal static string BuildWindowsExplorerArguments(string absolutePath, bool isFile)
		{
			string normalized = absolutePath.Replace('/', '\\');
			// explorer /select 语法要求逗号后紧跟路径，中间不能有空格，否则新版 Windows
			// 会忽略 /select 直接打开"文档"库而非选中目标文件。
			return isFile ? "/select,\"" + normalized + "\"" : normalized;
		}

		private static void WriteFile(string filePath, string content)
		{
			// Migration note（2026-09-07 修复，"Linux 持久化失效，每次启动弹引导窗"）：原实现用
			// Path.GetTempFileName() 在 TMPDIR（Unix 通常 /tmp）建临时文件再 rename 到目标目录。
			// /tmp 在真实 Linux 桌面上多为独立 tmpfs，与目标目录（$HOME 所在 ext4/btrfs）跨设备时
			// rename(2) 抛 EXDEV "Invalid cross-device link"：File.Replace 无回退 → 目标已存在后的
			// 每次 AtomicWrite 全部静默失败（沙箱 Xvfb 真机复现实证：settings.json 首次写入走
			// File.Move 的跨设备复制回退侥幸成功，其后 6 连败 EXDEV；真实用户机上 git 实例配置窗
			// 的保存先创建了 settings.json，引导窗的 Guid 保存即走 Replace → Guid 永久丢失 →
			// 每次启动都弹引导）。settings.json / accounts.json / custom-commands.json 三处调用方全中。
			// 修法与 UndoIndexStore 同款（POSIX 原子写标准做法，git 的 *.lock 同理）：临时文件建在
			// 目标同目录，rename 恒同设备；加随机后缀保留 GetTempFileName 的并发唯一性语义。
			// 权限保持原 Path.GetTempFileName 的 0600（accounts.json 含凭据，不能放宽到 umask 默认）。
			string tempFileName = filePath + "." + Guid.NewGuid().ToString("N").Substring(0, 8) + ".tmp";
			try
			{
				using (StreamWriter streamWriter = new StreamWriter(tempFileName))
				{
					streamWriter.Write(content);
				}
				if (!OperatingSystem.IsWindows())
				{
					try
					{
						File.SetUnixFileMode(tempFileName, System.IO.UnixFileMode.UserRead | System.IO.UnixFileMode.UserWrite);
					}
					catch
					{
						// chmod 失败不阻断写入（非 Unix 或文件系统不支持）；文件内容不变
					}
				}
				// Migration note：原子写跨平台。原 Windows 专用 MoveFileEx(ReplaceExisting) P/Invoke
				// 在 Linux/macOS 抛 DllNotFoundException（Kernel32.dll 不存在），settings.json
				// 等所有原子写全失败。Unix 用 File.Replace（rename(2) 同语义：原子覆盖）。
				if (OperatingSystem.IsWindows())
				{
					MoveFileEx(tempFileName, filePath, MoveFileFlags.ReplaceExisting | MoveFileFlags.CopyAllowed | MoveFileFlags.WriteThrough);
				}
				else
				{
					if (File.Exists(filePath))
					{
						File.Replace(tempFileName, filePath, null);
					}
					else
					{
						File.Move(tempFileName, filePath);
					}
				}
			}
			catch (Exception)
			{
				File.Delete(tempFileName);
				throw;
			}
		}

		[DllImport("Kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
		private static extern bool MoveFileEx([In] string lpExistingFileName, [In] string lpNewFileName, [In] MoveFileFlags dwFlags);
	}
}

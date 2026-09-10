using System;
using System.IO;
using System.IO.Compression;
using System.Text;
using Avalonia.Input;
using ForkPlus.UI.Dialogs;
using Microsoft.Win32;

namespace ForkPlus.UI.Commands
{
	/// <summary>
	/// v4.0.6 诊断包一键导出：把日志目录（fork.log 滚动归档、crash-*.log 崩溃转储、
	/// freeze-*.log 冻结报告、dump-*.dmp / freeze-*.dmp native 转储）打包成单个 zip
	/// 供用户直接反馈——此前 v4.0.5 已把崩溃现场全部落盘，但用户很难自己找到
	/// %LOCALAPPDATA%\ForkPlus\logs 并逐个收集。超大文件（&gt;100MB，通常是 WithHeap
	/// 转储）默认跳过并在结果里列明（打包含数百 MB 二进制会卡住文件对话框），
	/// 提示用户该文件可手动附上。zip 内附 README.txt（版本/系统/导出时间）免得
	/// 反馈脱离上下文。
	/// </summary>
	public class ExportDiagnosticsCommand : IUICommand, IForkPlusCommand
	{
		/// <summary>超过此大小的单个文件不进 zip（WithHeap 转储常达数百 MB）。</summary>
		private const long MaxFileBytes = 100L * 1024 * 1024;

		public string Title => "Export Diagnostics...";

		public KeyGesture Shortcut => null;

		public KeyGesture SecondaryShortcut => null;

		public void Execute()
		{
			string logsDirectory = CrashDumper.CrashLogDirectory;
			if (!Directory.Exists(logsDirectory))
			{
				new MessageBoxWindow("Export Diagnostics", "No diagnostics directory found at:\n" + logsDirectory, "OK", showCancelButton: false, width: 560.0).ShowDialog();
				return;
			}
			var dialog = new SaveFileDialog
			{
				Title = "Export Diagnostics",
				Filter = "Zip archive (*.zip)|*.zip",
				FileName = "ForkPlus-diagnostics-" + App.Version + "-" + DateTime.Now.ToString("yyyyMMdd-HHmmss") + ".zip"
			};
			if (dialog.ShowDialog() != true)
			{
				return;
			}
			string targetPath = dialog.FileName;
			if (string.IsNullOrWhiteSpace(targetPath))
			{
				return;
			}
			ExportResult result = ExportTo(logsDirectory, targetPath);
			if (result.Error != null)
			{
				new MessageBoxWindow("Export Diagnostics", result.Error, "OK", showCancelButton: false, showWarningIcon: true, width: 560.0).ShowDialog();
				return;
			}
			string message = "Collected " + result.IncludedCount + " file(s) into:\n" + targetPath
				+ (result.SkippedFiles.Length > 0
					? "\n\nSkipped (over " + (MaxFileBytes / (1024 * 1024)) + "MB, attach manually if needed):\n" + string.Join("\n", result.SkippedFiles)
					: string.Empty);
			new MessageBoxWindow("Export Diagnostics", message, "OK", showCancelButton: false, width: 560.0).ShowDialog();
		}

		/// <summary>打包核心（internal：回归测试直测文件收集/跳过/README 生成）。</summary>
		internal static ExportResult ExportTo(string logsDirectory, string targetPath)
		{
			try
			{
				string[] files = Directory.GetFiles(logsDirectory, "*", SearchOption.TopDirectoryOnly);
				Array.Sort(files, StringComparer.OrdinalIgnoreCase);
				if (File.Exists(targetPath))
				{
					File.Delete(targetPath);
				}
				string targetDirectory = Path.GetDirectoryName(Path.GetFullPath(targetPath));
				Directory.CreateDirectory(targetDirectory);
				var skipped = new System.Collections.Generic.List<string>();
				int includedCount = 0;
				using (ZipArchive archive = ZipFile.Open(targetPath, ZipArchiveMode.Create))
				{
					foreach (string file in files)
					{
						var fileInfo = new FileInfo(file);
						if (fileInfo.Length > MaxFileBytes)
						{
							skipped.Add(Path.GetFileName(file) + " (" + (fileInfo.Length / (1024.0 * 1024.0)).ToString("F0") + "MB)");
							continue;
						}
						archive.CreateEntryFromFile(file, Path.GetFileName(file), CompressionLevel.Fastest);
						includedCount++;
					}
					string readme = BuildReadMe();
					ZipArchiveEntry readmeEntry = archive.CreateEntry("README.txt", CompressionLevel.Fastest);
					using (StreamWriter writer = new StreamWriter(readmeEntry.Open(), new UTF8Encoding(false)))
					{
						writer.Write(readme);
					}
				}
				return new ExportResult(includedCount, skipped.ToArray(), null);
			}
			catch (Exception ex)
			{
				return new ExportResult(0, Array.Empty<string>(), "Export failed: " + ex.Message);
			}
		}

		private static string BuildReadMe()
		{
			StringBuilder builder = new StringBuilder(512);
			builder.AppendLine("ForkPlus diagnostics package");
			builder.AppendLine("Exported: " + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss zzz"));
			builder.AppendLine("Version:  " + App.Version);
			builder.AppendLine("OS:       " + System.Runtime.InteropServices.RuntimeInformation.OSDescription
				+ " (" + System.Runtime.InteropServices.RuntimeInformation.OSArchitecture + ")");
			builder.AppendLine("PID:      " + App.ProcessId);
			builder.AppendLine();
			builder.AppendLine("Contents:");
			builder.AppendLine("  fork.log / fork.log.*   - rolling application log (NLog)");
			builder.AppendLine("  crash-*.log             - managed crash dumps (CrashDumper)");
			builder.AppendLine("  freeze-*.log            - UI freeze reports (UiFreezeWatchdog)");
			builder.AppendLine("  dump-*.dmp              - native crash dumps (createdump)");
			builder.AppendLine("  freeze-*.dmp            - UI freeze dumps (createdump)");
			return builder.ToString();
		}

		internal readonly struct ExportResult
		{
			public readonly int IncludedCount;

			public readonly string[] SkippedFiles;

			public readonly string Error;

			public ExportResult(int includedCount, string[] skippedFiles, string error)
			{
				IncludedCount = includedCount;
				SkippedFiles = skippedFiles;
				Error = error;
			}
		}
	}
}

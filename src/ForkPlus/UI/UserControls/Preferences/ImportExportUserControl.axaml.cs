using System;
using System.IO;
using System.IO.Compression;
using Avalonia;
using Avalonia.Controls;
using ForkPlus.Settings;
using ForkPlus.UI.Dialogs;
using Avalonia.Layout;
using Avalonia.Styling;
using Avalonia.Interactivity;

namespace ForkPlus.UI.UserControls.Preferences
{
	/// <summary>
	/// v3.7.0：偏好设置"导入/导出"页 — 把 ForkPlus 配置打包成 zip 供换机迁移。
	/// 打包内容：%LocalAppData%\ForkPlus\ 下的 settings.json / custom-commands.json / accounts.json。
	/// accounts.json 含 API token 等敏感凭据，导出时复选框可排除。
	/// 导入会覆盖当前配置并提示重启生效。
	/// </summary>
	public partial class ImportExportUserControl : UserControl
	{
		private const string SettingsFileName = "settings.json";
		private const string CustomCommandsFileName = "custom-commands.json";
		private const string AccountsFileName = "accounts.json";

		public ImportExportUserControl()
		{
			InitializeComponent();
		}

		public void Initialize(ForkPlusDialogWindow parentWindow)
		{
			// 无需额外初始化，UI 在构造时已就绪
		}

		private void ExportButton_Click(object sender, RoutedEventArgs e)
		{
			try
			{
				bool includeAccounts = IncludeAccountsCheckBox.IsChecked == true;
				string forkDir = App.ForkDirectoryPath;

				// 校验至少有一个可导出的文件
				bool hasSettings = File.Exists(Path.Combine(forkDir, SettingsFileName));
				bool hasCustomCommands = File.Exists(Path.Combine(forkDir, CustomCommandsFileName));
				bool hasAccounts = includeAccounts && File.Exists(Path.Combine(forkDir, AccountsFileName));
				if (!hasSettings && !hasCustomCommands && !hasAccounts)
				{
					SetStatus(false, "No configuration files found to export.");
					return;
				}

				// 选保存路径
				string defaultName = "ForkPlus-config-" + DateTime.Now.ToString("yyyyMMdd-HHmmss") + ".zip";
				var dialog = new Microsoft.Win32.SaveFileDialog
				{
					FileName = defaultName,
					Filter = "Zip archive (*.zip)|*.zip|All files (*.*)|*.*",
					OverwritePrompt = true
				};
				if (dialog.ShowDialog() != true)
				{
					return;
				}
				string zipPath = dialog.FileName;

				// 先保存当前配置，确保导出的是最新内容
				try { ForkPlusSettings.Default.Save(); } catch { }

				// 逐文件打包（不用 CreateFromDirectory，避免把整个 ForkDir 里的无关文件也打进去）
				if (File.Exists(zipPath))
				{
					File.Delete(zipPath);
				}
				using (var archive = ZipFile.Open(zipPath, ZipArchiveMode.Create))
				{
					AddFileToArchive(archive, forkDir, SettingsFileName);
					AddFileToArchive(archive, forkDir, CustomCommandsFileName);
					if (includeAccounts)
					{
						AddFileToArchive(archive, forkDir, AccountsFileName);
					}
				}

				string detail = (hasSettings ? "settings " : "") + (hasCustomCommands ? "commands " : "") + (hasAccounts ? "accounts" : "");
				SetStatus(true, "Exported to: " + zipPath + " (" + detail.Trim() + ")");
			}
			catch (Exception ex)
			{
				Log.Error("Export config failed", ex);
				SetStatus(false, "Export failed: " + ex.Message);
			}
		}

		private void ImportButton_Click(object sender, RoutedEventArgs e)
		{
			try
			{
				var dialog = new Microsoft.Win32.OpenFileDialog
				{
					Filter = "Zip archive (*.zip)|*.zip|All files (*.*)|*.*",
					Title = "Select ForkPlus configuration zip"
				};
				if (dialog.ShowDialog() != true)
				{
					return;
				}
				string zipPath = dialog.FileName;

				// 二次确认：导入会覆盖当前配置（提交 = 继续导入，取消 = 中止）
				var confirm = new MessageBoxWindow(
					"Confirm Import",
					"Importing will overwrite your current ForkPlus configuration.\nForkPlus will restart after import.\n\nContinue?",
					"Import",
					"Cancel",
					showCancelButton: true,
					showWarningIcon: true).ShowDialog();
				if (confirm != true)
				{
					return;
				}

				string forkDir = App.ForkDirectoryPath;
				if (!Directory.Exists(forkDir))
				{
					Directory.CreateDirectory(forkDir);
				}

				// 解压前先校验 zip 内只含白名单文件，防止恶意 zip 路径穿越
				int importedCount = 0;
				bool hasSettings = false;
				using (var archive = ZipFile.OpenRead(zipPath))
				{
					foreach (var entry in archive.Entries)
					{
						if (string.IsNullOrEmpty(entry.Name))
						{
							continue; // 目录条目
						}
						string name = Path.GetFileName(entry.FullName);
						if (name != SettingsFileName && name != CustomCommandsFileName && name != AccountsFileName)
						{
							continue; // 跳过非白名单文件
						}
						string destPath = Path.Combine(forkDir, name);
						// 路径较长时加 \\?\ 前缀绕过 MAX_PATH(260) 限制（参考 GetCodeLineStatsGitCommand）
						string longPath = destPath;
						if (longPath.Length > 248 && !longPath.StartsWith(@"\\?\"))
						{
							longPath = @"\\?\" + longPath;
						}
						entry.ExtractToFile(longPath, overwrite: true);
						importedCount++;
						if (name == SettingsFileName) hasSettings = true;
					}
				}

				if (importedCount == 0)
				{
					SetStatus(false, "No valid configuration files found in the zip.");
					return;
				}

				SetStatus(true, "Imported " + importedCount + " file(s). ForkPlus will restart to apply changes.");

				// 提示重启
				new MessageBoxWindow(
					"Import Complete",
					"Configuration imported successfully.\nForkPlus will now restart to apply the new settings.",
					"OK",
					showCancelButton: false).ShowDialog();

				// 重启应用以加载新配置
				// Migration note：WPF Application.ResourceAssembly.Location → 当前进程可执行路径。
				System.Diagnostics.Process.Start(System.Environment.ProcessPath ?? global::Avalonia.Application.Current?.GetType().Assembly.Location ?? "");
				global::ForkPlus.UI.WpfCompat.WpfApp.Shutdown();
			}
			catch (Exception ex)
			{
				Log.Error("Import config failed", ex);
				SetStatus(false, "Import failed: " + ex.Message);
			}
		}

		private static void AddFileToArchive(ZipArchive archive, string sourceDir, string fileName)
		{
			string sourcePath = Path.Combine(sourceDir, fileName);
			if (!File.Exists(sourcePath))
			{
				return;
			}
			// 用扁平文件名，避免 zip 内带绝对路径
			archive.CreateEntryFromFile(sourcePath, fileName);
		}

		private void SetStatus(bool success, string message)
		{
			StatusText.Text = message;
			StatusText.Foreground = success
				? global::Avalonia.Media.Brushes.Green
				: global::Avalonia.Media.Brushes.OrangeRed;
		}
	}
}

using System;
using System.IO;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Platform.Storage;
using ForkPlus.Settings;
using ForkPlus.UI.UserControls.Preferences;

namespace ForkPlus.UI
{
	/// <summary>
	/// 阶段 4 里程碑 4.7-d：WPF Microsoft.WindowsAPICodePack.Dialogs.CommonOpenFileDialog/CommonSaveFileDialog →
	/// Avalonia StorageProvider.OpenFilePickerAsync / SaveFilePickerAsync / OpenFolderPickerAsync。
	/// IFileSystemDialogService 接口是同步的（bool + out），Avalonia API 是异步的（Task），
	/// 用 GetAwaiter().GetResult() 同步阻塞。桌面平台原生对话框在 UI 线程上可安全同步等待。
	/// </summary>
	internal static class OpenDialog
	{
		public static bool SelectDirectory([Null] Window parent, string title, string initialDirectory, out string directoryPath)
		{
			try
			{
				IStorageFolder folder = PickFolderAsync(parent, Translate(title), initialDirectory).GetAwaiter().GetResult();
				if (folder != null)
				{
					directoryPath = folder.Path.LocalPath;
					return true;
				}
			}
			catch (Exception ex)
			{
				Log.Error("Failed to show open directory dialog", ex);
			}
			directoryPath = null;
			return false;
		}

		public static bool SelectExecutableFile([Null] Window parent, string title, string initialDirectory, out string filePath)
		{
			return SelectFile(parent, title, initialDirectory, "Applications", "*.exe", out filePath);
		}

		public static bool SelectFile([Null] Window parent, string title, string initialDirectory, string fileTypeName, string extensionPattern, out string filePath)
		{
			try
			{
				IStorageFile file = PickFileAsync(parent, Translate(title), initialDirectory, Translate(fileTypeName), extensionPattern).GetAwaiter().GetResult();
				if (file != null)
				{
					filePath = file.Path.LocalPath;
					return true;
				}
			}
			catch (Exception ex)
			{
				Log.Error("Failed to show open file dialog", ex);
			}
			filePath = null;
			return false;
		}

		public static bool SelectPatchSaveLocation([Null] Window parent, string title, string initialDirectory, string defaultFileName, out string filePath)
		{
			try
			{
				// 阶段 4 里程碑 4.7-d：WPF CommonSaveFileDialog.DefaultFileName + AlwaysAppendDefaultExtension →
				// Avalonia FilePickerSaveOptions.SuggestedFileName + DefaultExtension。
				IStorageFile file = SaveFileAsync(parent, Translate(title), initialDirectory, defaultFileName,
					Translate("Patches"), "*" + Consts.Git.PatchFileExtension).GetAwaiter().GetResult();
				if (file != null)
				{
					filePath = file.Path.LocalPath;
					if (!filePath.EndsWith(Consts.Git.PatchFileExtension, StringComparison.CurrentCultureIgnoreCase))
					{
						filePath += Consts.Git.PatchFileExtension;
					}
					return true;
				}
			}
			catch (Exception ex)
			{
				Log.Error("Failed to show save file dialog", ex);
			}
			filePath = null;
			return false;
		}

		public static bool SelectFileSaveLocation([Null] Window parent, string title, string initialDirectory, string defaultFileName, out string resultFilePath)
		{
			try
			{
				string extension = Path.GetExtension(defaultFileName);
				// 阶段 4 里程碑 4.7-d：WPF CommonFileDialogFilter 接受 ".txt" 或 "txt" 或 "*.txt"，
				// Avalonia FilePickerFileType.Patterns 严格要求 glob 形式 "*.txt"。统一规范化。
				string pattern = string.IsNullOrEmpty(extension) ? "*.*" : "*" + extension;
				IStorageFile file = SaveFileAsync(parent, Translate(title), initialDirectory, defaultFileName,
					string.Format(Translate("*{0} files"), extension), pattern).GetAwaiter().GetResult();
				if (file != null)
				{
					resultFilePath = file.Path.LocalPath;
					return true;
				}
			}
			catch (Exception ex)
			{
				Log.Error("Failed to show save file dialog", ex);
			}
			resultFilePath = null;
			return false;
		}

		// 阶段 4 里程碑 4.7-d：Avalonia StorageProvider 异步 picker 方法封装。
		// parent 为 null 时用 MainWindow.Instance；ShowDialog 的父窗口逻辑通过
		// FilePickerOpenOptions/FolderPickerOpenOptions 的 Title 字段设置标题。
		// 阶段 4 里程碑 4.7-d：原 WPF OpenDialog.ShowDialog 在 parent==MainWindow.Instance 时
		// 调用 PreventRefreshAfterChildDialogClose 防止对话框关闭后触发自动刷新。
		// Avalonia StorageProvider picker 是模态的，窗口激活行为可能不同，保留此逻辑以防回归。
		private static async Task<IStorageFolder> PickFolderAsync(Window parent, string title, string initialDirectory)
		{
			IStorageProvider storage = GetStorageProvider(parent, out MainWindow mainWindow);
			if (storage == null) return null;

			mainWindow?.PreventRefreshAfterChildDialogClose("Open Folder Picker");
			try
			{
				FolderPickerOpenOptions options = new FolderPickerOpenOptions
				{
					Title = title,
					AllowMultiple = false
				};
				if (!string.IsNullOrEmpty(initialDirectory) && Directory.Exists(initialDirectory))
				{
					options.SuggestedStartLocation = await storage.TryGetFolderFromPathAsync(initialDirectory);
				}
				var results = await storage.OpenFolderPickerAsync(options);
				return results.Count > 0 ? results[0] : null;
			}
			finally
			{
				// PreventRefreshAfterChildDialogClose 在窗口 Activated 事件中自动清除
			}
		}

		private static async Task<IStorageFile> PickFileAsync(Window parent, string title, string initialDirectory, string fileTypeName, string extensionPattern)
		{
			IStorageProvider storage = GetStorageProvider(parent, out MainWindow mainWindow);
			if (storage == null) return null;

			mainWindow?.PreventRefreshAfterChildDialogClose("Open File Picker");
			try
			{
				FilePickerOpenOptions options = new FilePickerOpenOptions
				{
					Title = title,
					AllowMultiple = false,
					FileTypeFilter = new[]
					{
						new FilePickerFileType(fileTypeName) { Patterns = new[] { extensionPattern } }
					}
				};
				if (!string.IsNullOrEmpty(initialDirectory) && Directory.Exists(initialDirectory))
				{
					options.SuggestedStartLocation = await storage.TryGetFolderFromPathAsync(initialDirectory);
				}
				var results = await storage.OpenFilePickerAsync(options);
				return results.Count > 0 ? results[0] : null;
			}
			finally
			{
			}
		}

		private static async Task<IStorageFile> SaveFileAsync(Window parent, string title, string initialDirectory, string defaultFileName, string fileTypeName, string extensionPattern)
		{
			IStorageProvider storage = GetStorageProvider(parent, out MainWindow mainWindow);
			if (storage == null) return null;

			mainWindow?.PreventRefreshAfterChildDialogClose("Save File Picker");
			try
			{
				FilePickerSaveOptions options = new FilePickerSaveOptions
				{
					Title = title,
					SuggestedFileName = defaultFileName,
					FileTypeChoices = new[]
					{
						new FilePickerFileType(fileTypeName) { Patterns = new[] { extensionPattern } }
					}
				};
				if (!string.IsNullOrEmpty(initialDirectory) && Directory.Exists(initialDirectory))
				{
					options.SuggestedStartLocation = await storage.TryGetFolderFromPathAsync(initialDirectory);
				}
				return await storage.SaveFilePickerAsync(options);
			}
			finally
			{
			}
		}

		private static IStorageProvider GetStorageProvider(Window parent, out MainWindow mainWindow)
		{
			Window window = parent ?? MainWindow.Instance;
			mainWindow = window as MainWindow;
			return window?.StorageProvider;
		}

		private static string Translate(string text)
		{
			return PreferencesLocalization.Translate(text, ForkPlusSettings.Default.UiLanguage);
		}
	}
}

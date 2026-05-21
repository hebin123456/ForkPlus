using System;
using System.IO;
using System.Windows;
using ForkPlus.Settings;
using ForkPlus.UI.UserControls.Preferences;
using Microsoft.WindowsAPICodePack.Dialogs;

namespace ForkPlus.UI
{
	internal static class OpenDialog
	{
		public static bool SelectDirectory([Null] Window parent, string title, string initialDirectory, out string directoryPath)
		{
			try
			{
				CommonOpenFileDialog commonOpenFileDialog = new CommonOpenFileDialog
				{
					IsFolderPicker = true,
					Title = Translate(title),
					InitialDirectory = initialDirectory
				};
				if (ShowDialog(commonOpenFileDialog, parent) == CommonFileDialogResult.Ok)
				{
					directoryPath = commonOpenFileDialog.FileName;
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
				CommonOpenFileDialog commonOpenFileDialog = new CommonOpenFileDialog
				{
					IsFolderPicker = false,
					Title = Translate(title),
					InitialDirectory = initialDirectory,
					Multiselect = false
				};
				commonOpenFileDialog.Filters.Add(new CommonFileDialogFilter(Translate(fileTypeName), extensionPattern));
				if (ShowDialog(commonOpenFileDialog, parent) == CommonFileDialogResult.Ok)
				{
					filePath = commonOpenFileDialog.FileName;
					return true;
				}
			}
			catch (Exception ex)
			{
				Log.Error("Failed to show open directory dialog", ex);
			}
			filePath = null;
			return false;
		}

		public static bool SelectPatchSaveLocation([Null] Window parent, string title, string initialDirectory, string defaultFileName, out string filePath)
		{
			try
			{
				CommonSaveFileDialog commonSaveFileDialog = new CommonSaveFileDialog
				{
					Title = Translate(title),
					InitialDirectory = initialDirectory,
					DefaultFileName = defaultFileName,
					AlwaysAppendDefaultExtension = true
				};
				commonSaveFileDialog.Filters.Add(new CommonFileDialogFilter(Translate("Patches"), "*" + Consts.Git.PatchFileExtension));
				if (ShowDialog(commonSaveFileDialog, parent) == CommonFileDialogResult.Ok)
				{
					filePath = commonSaveFileDialog.FileName;
					if (!filePath.EndsWith(Consts.Git.PatchFileExtension, StringComparison.CurrentCultureIgnoreCase))
					{
						filePath += Consts.Git.PatchFileExtension;
					}
					return true;
				}
			}
			catch (Exception ex)
			{
				Log.Error("Failed to show open directory dialog", ex);
			}
			filePath = null;
			return false;
		}

		public static bool SelectFileSaveLocation([Null] Window parent, string title, string initialDirectory, string defaultFileName, out string resultFilePath)
		{
			try
			{
				string extension = Path.GetExtension(defaultFileName);
				CommonSaveFileDialog commonSaveFileDialog = new CommonSaveFileDialog
				{
					Title = Translate(title),
					DefaultFileName = defaultFileName,
					InitialDirectory = initialDirectory
				};
				commonSaveFileDialog.Filters.Add(new CommonFileDialogFilter(string.Format(Translate("*{0} files"), extension), extension));
				if (ShowDialog(commonSaveFileDialog, parent) == CommonFileDialogResult.Ok)
				{
					resultFilePath = commonSaveFileDialog.FileName;
					return true;
				}
			}
			catch (Exception ex)
			{
				Log.Error("Failed to show open directory dialog", ex);
			}
			resultFilePath = null;
			return false;
		}

		private static CommonFileDialogResult ShowDialog(CommonFileDialog dialog, [Null] Window parent)
		{
			if (parent != null)
			{
				if (parent == MainWindow.Instance)
				{
					MainWindow.Instance.PreventRefreshAfterChildDialogClose("Open File Dialog");
				}
				return dialog.ShowDialog(parent);
			}
			return dialog.ShowDialog();
		}

		private static string Translate(string text)
		{
			return PreferencesLocalization.Translate(text, ForkPlusSettings.Default.UiLanguage);
		}
	}
}

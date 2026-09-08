using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using Avalonia;
using Avalonia.Input;
using ForkPlus.Git;
using ForkPlus.Git.Commands;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Styling;

namespace ForkPlus.UI.Commands
{
	public class OpenFileInDefaultEditorCommand : IUICommand, IForkPlusCommand
	{
		[Flags]
		public enum AssocF
		{
			None = 0,
			Init_NoRemapCLSID = 1,
			Init_ByExeName = 2,
			Open_ByExeName = 2,
			Init_DefaultToStar = 4,
			Init_DefaultToFolder = 8,
			NoUserSettings = 0x10,
			NoTruncate = 0x20,
			Verify = 0x40,
			RemapRunDll = 0x80,
			NoFixUps = 0x100,
			IgnoreBaseClass = 0x200,
			Init_IgnoreUnknown = 0x400,
			Init_Fixed_ProgId = 0x800,
			Is_Protocol = 0x1000,
			Init_For_File = 0x2000
		}

		public enum AssocStr
		{
			Command = 1,
			Executable,
			FriendlyDocName,
			FriendlyAppName,
			NoOpen,
			ShellNewValue,
			DDECommand,
			DDEIfExec,
			DDEApplication,
			DDETopic,
			InfoTip,
			QuickTip,
			TileInfo,
			ContentType,
			DefaultIcon,
			ShellExtension,
			DropTarget,
			DelegateExecute,
			Supported_Uri_Protocols,
			ProgID,
			AppID,
			AppPublisher,
			AppIconReference,
			Max
		}

		public string Title => "Open";

		public KeyGesture Shortcut { get; } = new KeyGesture(Key.O, global::Avalonia.Input.KeyModifiers.Alt | global::Avalonia.Input.KeyModifiers.Control | global::Avalonia.Input.KeyModifiers.Shift);


		public KeyGesture SecondaryShortcut => null;

		public void Execute(GitModule gitModule, string filePath)
		{
			if (gitModule == null || filePath == null || !IsEditorAvailable(gitModule, filePath))
			{
				return;
			}
			try
			{
				string text = gitModule.MakePath(filePath);
				if (File.Exists(text))
				{
					// Process.Start(string) 在 .NET 10 下默认 UseShellExecute=false，无法走
					// shell 关联唤起默认编辑器，必须显式置 true。
					Process.Start(new ProcessStartInfo(text) { UseShellExecute = true });
				}
			}
			catch (Exception ex)
			{
				Log.Error("Failed to open '" + filePath + "' in default editor", ex);
			}
		}

		public void Execute(GitModule gitModule, string sha, ChangedFile changedFile)
		{
			if (gitModule == null || sha == null || changedFile == null || !IsEditorAvailable(gitModule, changedFile.Path))
			{
				return;
			}
			try
			{
				string text = SaveToTempDestination(gitModule, sha, changedFile);
				if (text != null && File.Exists(text))
				{
					// 同上：历史版本临时文件需走 shell 关联打开，必须 UseShellExecute=true。
					Process.Start(new ProcessStartInfo(text) { UseShellExecute = true });
				}
			}
			catch (Exception ex)
			{
				Log.Error("Failed to save '" + changedFile.Path + "' to temporary location", ex);
			}
		}

		public bool IsEditorAvailable(GitModule gitModule, string filePath)
		{
			if (gitModule == null || filePath == null)
			{
				return false;
			}
			try
			{
				string text = gitModule.MakePath(filePath);
				Log.Info("Looking for an associated editor for '" + text + "'");
				string extension = Path.GetExtension(text);
				if (string.IsNullOrEmpty(extension))
				{
					return false;
				}
				// Migration note：AssocQueryString（Shlwapi.dll）是 Windows 专属的 shell 关联查询，
				// Unix 上抛 DllNotFoundException（被 catch 吞掉后命令静默不可用）。
				// Unix 退化为探测 xdg-open（xdg-open 在 PATH 即视为有默认编辑器，
				// 实际打开由 Process.Start(UseShellExecute=true) 走 xdg-open 关联）。
				if (!OperatingSystem.IsWindows())
				{
					string xdgOpen = FindOnPath("xdg-open");
					if (xdgOpen == null)
					{
						Log.Info("Can't find editor for '" + text + "' (xdg-open not on PATH)");
						return false;
					}
					Log.Info("File can be edited with '" + xdgOpen + "'");
					return true;
				}
				string text2 = AssocQueryString(AssocStr.Executable, extension);
				if (text2 == "%1" || text2.EndsWith("OpenWith.exe"))
				{
					Log.Info("Can't find editor for '" + text + "'");
					return false;
				}
				Log.Info("File can be edited with '" + text2 + "'");
				return true;
			}
			catch (Exception ex)
			{
				Log.Error("Failed to get associated editor for '" + filePath + "'", ex);
				return false;
			}
		}

		private static string FindOnPath(string tool)
		{
			string path = Environment.GetEnvironmentVariable("PATH") ?? "";
			string[] array = path.Split(Path.PathSeparator);
			foreach (string text in array)
			{
				if (string.IsNullOrEmpty(text))
				{
					continue;
				}
				try
				{
					string text2 = Path.Combine(text, tool);
					if (File.Exists(text2))
					{
						return text2;
					}
				}
				catch (Exception)
				{
				}
			}
			return null;
		}

		private static string AssocQueryString(AssocStr association, string extension)
		{
			uint pcchOut = 0u;
			if (AssocQueryString(AssocF.None, association, extension, null, null, ref pcchOut) != 1)
			{
				throw new InvalidOperationException("Could not determine associated string");
			}
			StringBuilder stringBuilder = new StringBuilder((int)pcchOut);
			if (AssocQueryString(AssocF.None, association, extension, null, stringBuilder, ref pcchOut) != 0)
			{
				throw new InvalidOperationException("Could not determine associated string");
			}
			return stringBuilder.ToString();
		}

		[Null]
		private string SaveToTempDestination(GitModule gitModule, string sha, ChangedFile changedFile)
		{
			TempFileManager tempFileManager = Application.Current.ActiveRepositoryUserControl()?.TempFileManager;
			if (tempFileManager == null)
			{
				return null;
			}
			GitCommandResult<DiffContent> binaryContent = new GetRevisionFileChangesGitCommand().GetBinaryContent(gitModule, changedFile, sha, null);
			if (!binaryContent.Succeeded || !(binaryContent.Result is BinaryDiffContent binaryDiffContent))
			{
				Log.Error(binaryContent.Error.FriendlyDescription);
				return null;
			}
			return tempFileManager.CreateTemporaryFile(changedFile.Path, binaryDiffContent.DstData, sha.Substring(0, 7));
		}

		[DllImport("Shlwapi.dll", CharSet = CharSet.Unicode)]
		public static extern uint AssocQueryString(AssocF flags, AssocStr str, string pszAssoc, string pszExtra, [Out] StringBuilder pszOut, ref uint pcchOut);
	}
}

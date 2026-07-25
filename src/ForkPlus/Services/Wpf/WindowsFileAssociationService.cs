using System;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using System.Text;

namespace ForkPlus.Services.Wpf
{
	/// <summary>
	/// Windows 平台的 <see cref="IFileAssociationService"/> 实现，
	/// 封装 <c>Shlwapi.dll!AssocQueryString</c> P/Invoke。
	/// 阶段 5：非 Windows 平台返回 null/false（无文件关联查询能力）。
	/// </summary>
	public class WindowsFileAssociationService : IFileAssociationService
	{
		[Flags]
		private enum AssocF
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

		private enum AssocStr
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

		[DllImport("Shlwapi.dll", CharSet = CharSet.Unicode)]
		[SupportedOSPlatform("windows")]
		private static extern uint AssocQueryString(AssocF flags, AssocStr str, string pszAssoc, string pszExtra, [System.Runtime.InteropServices.Out] StringBuilder pszOut, ref uint pcchOut);

		public string GetAssociatedExecutable(string extension)
		{
			if (string.IsNullOrEmpty(extension))
			{
				return null;
			}
			// 阶段 5：非 Windows 平台无文件关联查询 API（Linux/macOS 的 mime 类型查询走不同机制）。
			if (!OperatingSystem.IsWindows())
			{
				return null;
			}
			try
			{
				return GetAssociatedExecutableWindows(extension);
			}
			catch (System.Exception ex)
			{
				Log.Error("Failed to get associated executable for '" + extension + "'", ex);
				return null;
			}
		}

		[SupportedOSPlatform("windows")]
		private static string GetAssociatedExecutableWindows(string extension)
		{
			uint pcchOut = 0u;
			if (AssocQueryString(AssocF.None, AssocStr.Executable, extension, null, null, ref pcchOut) != 1)
			{
				return null;
			}
			StringBuilder sb = new StringBuilder((int)pcchOut);
			if (AssocQueryString(AssocF.None, AssocStr.Executable, extension, null, sb, ref pcchOut) != 0)
			{
				return null;
			}
			return sb.ToString();
		}

		public bool IsEditorAvailable(string extension)
		{
			if (string.IsNullOrEmpty(extension))
			{
				return false;
			}
			// 阶段 5：非 Windows 平台无法判断编辑器可用性，保守返回 true 让 UI 提供默认编辑器入口。
			if (!OperatingSystem.IsWindows())
			{
				return true;
			}
			string executable = GetAssociatedExecutable(extension);
			if (string.IsNullOrEmpty(executable))
			{
				return false;
			}
			// "%1" 表示无关联（由 OpenWith 处理），OpenWith.exe 表示系统回退到"打开方式"对话框
			return executable != "%1" && !executable.EndsWith("OpenWith.exe");
		}
	}
}

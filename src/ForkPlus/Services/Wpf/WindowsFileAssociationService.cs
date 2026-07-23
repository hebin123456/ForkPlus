using System.Text;
using ForkPlus.UI.Commands;

namespace ForkPlus.Services.Wpf
{
	/// <summary>
	/// Windows 平台的 <see cref="IFileAssociationService"/> 实现，委托给
	/// <see cref="OpenFileInDefaultEditorCommand"/> 中的 <c>Shlwapi.dll!AssocQueryString</c> P/Invoke。
	/// 阶段 0 仅注册，<see cref="OpenFileInDefaultEditorCommand"/> 调用点将在阶段 2 迁移到此接口。
	/// </summary>
	public class WindowsFileAssociationService : IFileAssociationService
	{
		public string GetAssociatedExecutable(string extension)
		{
			if (string.IsNullOrEmpty(extension))
			{
				return null;
			}
			try
			{
				uint pcchOut = 0u;
				if (OpenFileInDefaultEditorCommand.AssocQueryString(
						OpenFileInDefaultEditorCommand.AssocF.None,
						OpenFileInDefaultEditorCommand.AssocStr.Executable,
						extension, null, null, ref pcchOut) != 1)
				{
					return null;
				}
				StringBuilder sb = new StringBuilder((int)pcchOut);
				if (OpenFileInDefaultEditorCommand.AssocQueryString(
						OpenFileInDefaultEditorCommand.AssocF.None,
						OpenFileInDefaultEditorCommand.AssocStr.Executable,
						extension, null, sb, ref pcchOut) != 0)
				{
					return null;
				}
				return sb.ToString();
			}
			catch (System.Exception ex)
			{
				Log.Error("Failed to get associated executable for '" + extension + "'", ex);
				return null;
			}
		}

		public bool IsEditorAvailable(string extension)
		{
			if (string.IsNullOrEmpty(extension))
			{
				return false;
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

// 阶段 4.5：WPF→Avalonia 迁移。
// - using System.Windows.Media → using Avalonia.Media
// - ImageSource → IImage（Avalonia.Media.IImage；Theme.FolderIcon/WarningIcon 返回 IImage）
using System;
using Avalonia.Media;

namespace ForkPlus.UI.UserControls.Preferences
{
	public class SrcDirViewModel
	{
		public string Path { get; }

		public IImage SrcFolderIcon { get; }

		public string SrcFolderIconTooltip { get; }

		public SrcDirViewModel(string path)
		{
			Path = path;
			if (SrcFolderIsValid(path))
			{
				SrcFolderIcon = Theme.FolderIcon;
				SrcFolderIconTooltip = null;
			}
			else
			{
				SrcFolderIcon = Theme.WarningIcon;
				SrcFolderIconTooltip = "'" + path + "' should not be used as a source directory. Please choose a subfolder instead";
			}
		}

		private static bool SrcFolderIsValid(string path)
		{
			string value = Environment.ExpandEnvironmentVariables("%userprofile%");
			if (!path.TrimEnd("\\").Equals("c:", StringComparison.OrdinalIgnoreCase))
			{
				return !path.TrimEnd("\\").Equals(value, StringComparison.OrdinalIgnoreCase);
			}
			return false;
		}
	}
}

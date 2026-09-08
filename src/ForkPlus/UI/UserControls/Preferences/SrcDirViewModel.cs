using System;
using Avalonia.Media;

namespace ForkPlus.UI.UserControls.Preferences
{
	public class SrcDirViewModel
	{
		public string Path { get; }

		public global::Avalonia.Media.IImage SrcFolderIcon { get; }

		public string SrcFolderIconTooltip { get; }

		public SrcDirViewModel(string path)
		{
			Path = path;
			if (SrcFolderIsValid(path))
			{
				SrcFolderIcon = global::ForkPlus.UI.Theme.FolderIcon;
				SrcFolderIconTooltip = null;
			}
			else
			{
				SrcFolderIcon = global::ForkPlus.UI.Theme.WarningIcon;
				SrcFolderIconTooltip = "'" + path + "' should not be used as a source directory. Please choose a subfolder instead";
			}
		}

		private static bool SrcFolderIsValid(string path)
		{
			string value = SystemEnvironment.UserProfileDirectory;
			if (!path.TrimEnd("\\").Equals("c:", StringComparison.OrdinalIgnoreCase))
			{
				return !path.TrimEnd("\\").Equals(value, StringComparison.OrdinalIgnoreCase);
			}
			return false;
		}
	}
}

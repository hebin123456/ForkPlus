using System.IO;
using Avalonia.Media;
using ForkPlus.UI.UserControls;

namespace ForkPlus.UI.QuickLaunch
{
	public class RepositoryFileItem : CommandProviderItem
	{
		public override global::Avalonia.Media.IImage Icon => IconTools.GetImageSourceForExtension(Path.GetExtension(FilePath));

		public override global::Avalonia.Media.IImage SelectedIcon => IconTools.GetImageSourceForExtension(Path.GetExtension(FilePath));

		public string FilePath { get; }

		public RepositoryFileItem(string filePath)
			: base(filePath, GetFileName(filePath), filePath)
		{
			FilePath = filePath;
		}

		private static string GetFileName(string filePath)
		{
			try
			{
				return Path.GetFileName(filePath);
			}
			catch
			{
				return "";
			}
		}
	}
}

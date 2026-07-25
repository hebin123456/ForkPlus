using System.IO;
using System.Windows.Media;
using ForkPlus.UI.UserControls;
using Avalonia.Media;

namespace ForkPlus.UI.QuickLaunch
{
	public class RepositoryFileItem : CommandProviderItem
	{
		public override IImage Icon => IconTools.GetImageSourceForExtension(Path.GetExtension(FilePath));

		public override IImage SelectedIcon => IconTools.GetImageSourceForExtension(Path.GetExtension(FilePath));

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

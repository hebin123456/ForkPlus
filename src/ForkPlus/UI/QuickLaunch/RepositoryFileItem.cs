using System.IO;
using System.Windows.Media;
using ForkPlus.UI.UserControls;

namespace ForkPlus.UI.QuickLaunch
{
	public class RepositoryFileItem : CommandProviderItem
	{
		public override ImageSource Icon => IconTools.GetImageSourceForExtension(Path.GetExtension(FilePath));

		public override ImageSource SelectedIcon => IconTools.GetImageSourceForExtension(Path.GetExtension(FilePath));

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

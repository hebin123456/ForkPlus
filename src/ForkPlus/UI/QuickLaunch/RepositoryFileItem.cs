// 阶段 4.5：WPF→Avalonia 迁移。
// - using System.Windows.Media → using Avalonia.Media
// - ImageSource → IImage（Avalonia.Media）
// - IconTools.GetImageSourceForExtension 已返回 IImage，可直接赋值
using System.IO;
using Avalonia.Media;
using ForkPlus.UI.UserControls;

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

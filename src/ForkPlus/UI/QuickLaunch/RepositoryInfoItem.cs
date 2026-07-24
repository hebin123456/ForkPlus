// 阶段 4.5：WPF→Avalonia 迁移。
// - using System.Windows → using Avalonia
// - using System.Windows.Media → using Avalonia.Media
// - ImageSource → IImage
// - Application.Current.TryFindResource(key) as ImageSource → Theme.FindImage(key)
using Avalonia;
using Avalonia.Media;

namespace ForkPlus.UI.QuickLaunch
{
	public class RepositoryInfoItem : CommandProviderItem
	{
		public override IImage Icon => Theme.FindImage("RepositoryIcon");

		public override IImage SelectedIcon => Theme.FindImage("RepositoryEmphasizedIcon");

		public RepositoryManager.Repository Repository { get; }

		public RepositoryInfoItem(RepositoryManager.Repository repository)
			: base(repository, repository.Name(), repository.Path)
		{
			Repository = repository;
		}
	}
}

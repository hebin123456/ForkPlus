using Avalonia;
using Avalonia.Media;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Styling;

namespace ForkPlus.UI.QuickLaunch
{
	public class RepositoryInfoItem : CommandProviderItem
	{
		public override global::Avalonia.Media.IImage Icon => Application.Current.TryFindResource("RepositoryIcon") as global::Avalonia.Media.IImage;

		public override global::Avalonia.Media.IImage SelectedIcon => Application.Current.TryFindResource("RepositoryEmphasizedIcon") as global::Avalonia.Media.IImage;

		public RepositoryManager.Repository Repository { get; }

		public RepositoryInfoItem(RepositoryManager.Repository repository)
			: base(repository, repository.Name(), repository.Path)
		{
			Repository = repository;
		}
	}
}

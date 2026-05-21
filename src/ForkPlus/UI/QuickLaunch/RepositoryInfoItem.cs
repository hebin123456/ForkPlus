using System.Windows;
using System.Windows.Media;

namespace ForkPlus.UI.QuickLaunch
{
	public class RepositoryInfoItem : CommandProviderItem
	{
		public override ImageSource Icon => Application.Current.TryFindResource("RepositoryIcon") as ImageSource;

		public override ImageSource SelectedIcon => Application.Current.TryFindResource("RepositoryEmphasizedIcon") as ImageSource;

		public RepositoryManager.Repository Repository { get; }

		public RepositoryInfoItem(RepositoryManager.Repository repository)
			: base(repository, repository.Name(), repository.Path)
		{
			Repository = repository;
		}
	}
}

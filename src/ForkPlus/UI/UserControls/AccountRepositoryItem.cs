using System.Windows.Media;
using ForkPlus.Accounts;

namespace ForkPlus.UI.UserControls
{
	public class AccountRepositoryItem : AccountItem
	{
		public ImageSource Icon { get; }

		public string Tooltip { get; }

		public GitServiceRepository Repository { get; }

		public AccountRepositoryItem(GitServiceRepository repository, ImageSource icon)
			: base(repository.Name)
		{
			Repository = repository;
			Icon = icon;
			Tooltip = "Clone '" + repository.Name + "'";
		}
	}
}

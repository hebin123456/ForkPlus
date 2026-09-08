using Avalonia.Media;
using ForkPlus.Accounts;

namespace ForkPlus.UI.UserControls
{
	public class AccountRepositoryItem : AccountItem
	{
		public global::Avalonia.Media.IImage Icon { get; }

		public string Tooltip { get; }

		public GitServiceRepository Repository { get; }

		public AccountRepositoryItem(GitServiceRepository repository, global::Avalonia.Media.IImage icon)
			: base(repository.Name)
		{
			Repository = repository;
			Icon = icon;
			Tooltip = "Clone '" + repository.Name + "'";
		}
	}
}

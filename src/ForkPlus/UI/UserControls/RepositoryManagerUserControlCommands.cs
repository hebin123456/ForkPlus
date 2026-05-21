using ForkPlus.UI.Commands;
using ForkPlus.UI.Commands.RepositoryManager;

namespace ForkPlus.UI.UserControls
{
	public class RepositoryManagerUserControlCommands : CommandContainer
	{
		private RenameRepositoryCommand _renameRepositoryCommand;

		private RescanRepositoriesCommand _rescanRepositoriesCommand;

		private RemoveRepositoryCommand _removeRepositoryCommand;

		private ForkPlus.UI.Commands.RepositoryManager.OpenRepositoryCommand _openRepositoryCommand;

		private OpenRepositoriesCommand _openRepositoriesCommand;

		public RenameRepositoryCommand RenameRepository => CommandContainer.Lazy(ref _renameRepositoryCommand);

		public RescanRepositoriesCommand RescanRepositories => CommandContainer.Lazy(ref _rescanRepositoriesCommand);

		public RemoveRepositoryCommand RemoveRepository => CommandContainer.Lazy(ref _removeRepositoryCommand);

		public ForkPlus.UI.Commands.RepositoryManager.OpenRepositoryCommand OpenRepository => CommandContainer.Lazy(ref _openRepositoryCommand);

		public OpenRepositoriesCommand OpenRepositoriesCommand => CommandContainer.Lazy(ref _openRepositoriesCommand);
	}
}

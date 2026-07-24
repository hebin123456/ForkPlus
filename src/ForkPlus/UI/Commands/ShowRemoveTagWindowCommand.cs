using Avalonia.Input;
using ForkPlus.Git;
using ForkPlus.UI.Dialogs;
using ForkPlus.UI.UserControls;

namespace ForkPlus.UI.Commands
{
	public class ShowRemoveTagWindowCommand : IUICommand, IForkPlusCommand
	{
		public static CommandDescriptor[] PublicCommands = new CommandDescriptor[1]
		{
			new CommandDescriptor("Delete Tag...", new Argument[1]
			{
				new Argument(ArgumentType.Tag, "tag to delete")
			}, delegate(object[] arguments, RepositoryUserControl repositoryUserControl)
			{
				if (arguments[0] is Tag tag)
				{
					RepositoryUserControl.Commands.ShowRemoveTagWindow.Execute(repositoryUserControl, new Tag[1] { tag });
				}
			})
		};

		public string Title => "Delete Tag";

		public KeyGesture Shortcut { get; } = new KeyGesture(Key.Delete);


		public KeyGesture SecondaryShortcut => null;

		public void Execute(RepositoryUserControl repositoryUserControl, Tag[] tags)
		{
			RepositoryReferences repositoryReferences = repositoryUserControl.RepositoryData?.References;
			if (repositoryReferences == null || tags.FirstItem() == null)
			{
				return;
			}
			RemoveTagWindow removeTagWindow = new RemoveTagWindow(repositoryUserControl, tags, repositoryReferences);
			if (removeTagWindow.ShowDialog().GetValueOrDefault())
			{
				if (!removeTagWindow.GitResult.Succeeded)
				{
					new ErrorWindow(repositoryUserControl, removeTagWindow.GitResult.Error).ShowDialog();
				}
				repositoryUserControl.InvalidateAndRefresh(SubDomain.Revisions | SubDomain.References, new RevisionSelector.Sha(tags.FirstItem().Sha));
			}
		}
	}
}

using System;
using System.Text;
using System.Windows.Input;
using ForkPlus.Settings;
using ForkPlus.UI.Dialogs;
using ForkPlus.UI.UserControls;
using ForkPlus.Services;

namespace ForkPlus.UI.Commands.RepositoryManager
{
	public class RemoveRepositoryCommand : IUICommand, IForkPlusCommand
	{
		public string Title => "Remove...";

		public KeyGesture Shortcut { get; } = new KeyGesture(Key.Delete);


		public KeyGesture SecondaryShortcut => null;

		public void Execute(RepositoryManagerUserControl repositoryManager, RepositoryManagerTreeViewItem[] items)
		{
			if (items.Length == 0)
			{
				return;
			}
			if (items[0] is RepositoryManagerRepositoryItem)
			{
				if (!RepositoryPromptConfirmation(items.Length))
				{
					return;
				}
				string[] repositoriesToDelete = items.CompactMap((RepositoryManagerTreeViewItem x) => (x as RepositoryManagerRepositoryItem)?.Repository.Path);
				ForkPlus.RepositoryManager.Instance.DeleteRepositories(repositoriesToDelete);
				ForkPlus.RepositoryManager.Instance.Save();
			}
			else
			{
				if (!(items[0] is RepositoryManagerRepositoryFolderItem))
				{
					throw new InvalidOperationException();
				}
				if (!DirectoryPromptConfirmation())
				{
					return;
				}
				string[] foldersToDelete = items.CompactMap((RepositoryManagerTreeViewItem x) => x as RepositoryManagerRepositoryFolderItem).Map((RepositoryManagerRepositoryFolderItem x) => RelativePath(x));
				ForkPlus.RepositoryManager.Instance.DeleteFolders(foldersToDelete);
				ForkPlus.RepositoryManager.Instance.Save();
			}
			repositoryManager.Refresh();
			repositoryManager.SelectFirstRepository();
		}

		private bool RepositoryPromptConfirmation(int count)
		{
			string title = ((count == 0) ? Translate("Do you want to remove the selected repository from Fork?") : string.Format(Translate("Do you want to remove {0} selected repositories from Fork?"), count));
			string description = ((count == 0) ? Translate("The repository will remain on disk") : Translate("The repositories will remain on disk"));
			return new MessageBoxWindow(title, description, Translate("Remove"), Translate("Cancel"), showCancelButton: true, 560.0).ShowDialog().GetValueOrDefault();
		}

		private bool DirectoryPromptConfirmation()
		{
			return new MessageBoxWindow(Translate("Do you want to remove the selected folders from Fork?"), Translate("Included repositories will be removed from Fork."), Translate("Remove"), Translate("Cancel"), showCancelButton: true, 560.0).ShowDialog().GetValueOrDefault();
		}

		private string RelativePath(RepositoryManagerRepositoryFolderItem item)
		{
			StringBuilder stringBuilder = new StringBuilder();
			while (item != null)
			{
				stringBuilder.Insert(0, item.Title);
				item = item.Parent as RepositoryManagerRepositoryFolderItem;
				if (item != null)
				{
					stringBuilder.Insert(0, "\\");
				}
			}
			return stringBuilder.ToString();
		}

		private static string Translate(string text)
		{
			return ServiceLocator.Localization.Translate(text, ForkPlusSettings.Default.UiLanguage);
		}
	}
}

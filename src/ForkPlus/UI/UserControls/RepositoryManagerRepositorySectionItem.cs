using System.Windows;
using ForkPlus.Git.Commands;

namespace ForkPlus.UI.UserControls
{
	public class RepositoryManagerRepositorySectionItem : RepositoryManagerSectionItem
	{
		private readonly RepositoryManagerUserControl _repositoryManagerUserControl;

		public RepositoryManagerRepositorySectionItem(RepositoryManagerTreeViewItem parent, string title, RepositoryManagerUserControl repositoryManagerUserControl)
			: base(parent, title)
		{
			_repositoryManagerUserControl = repositoryManagerUserControl;
		}

		public override DragDropEffects GetDropEffect(DragEventArgs e, int index)
		{
			if (e.Data.GetData(DataFormats.FileDrop) is string[])
			{
				return DragDropEffects.Move;
			}
			return DragDropEffects.None;
		}

		public override void Drop(DragEventArgs e, int index)
		{
			if (e.Data.GetData(DataFormats.FileDrop) is string[] source)
			{
				string[] paths = source.CompactMap((string path) => (new ValidateRepositoryPathGitCommand().Execute(path) == RepositoryValidState.ValidRepository) ? path : null);
				RepositoryManager.Instance.AddRepositories(paths);
				RepositoryManager.Instance.Save();
				if (!base.IsExpanded)
				{
					base.IsExpanded = true;
				}
				_repositoryManagerUserControl.Refresh();
			}
		}
	}
}

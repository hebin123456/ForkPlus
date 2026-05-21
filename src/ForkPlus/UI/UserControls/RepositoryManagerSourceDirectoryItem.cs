using System.Windows;
using ForkPlus.Git;
using ForkPlus.Git.Commands;

namespace ForkPlus.UI.UserControls
{
	public class RepositoryManagerSourceDirectoryItem : RepositoryManagerTreeViewItem
	{
		public RepositoryManagerUserControl RepositoryManagerUserControl { get; }

		public string SourceDir { get; }

		public RepositoryManagerSourceDirectoryItem(RepositoryManagerUserControl repositoryManagerUserControl, string title, string sourceDir, RepositoryManagerTreeViewItem parent)
			: base(parent)
		{
			RepositoryManagerUserControl = repositoryManagerUserControl;
			base.Title = title;
			SourceDir = sourceDir;
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
			if (!(e.Data.GetData(DataFormats.FileDrop) is string[] array))
			{
				return;
			}
			string[] array2 = array;
			foreach (string path in array2)
			{
				if (GitMmUserControl.IsGitMmWorkspace(path))
				{
					RepositoryManager.Instance.AddOrUpdateLastOpened(path);
					continue;
				}
				GitCommandResult<GitModule> gitCommandResult = new OpenGitRepositoryGitCommand().Execute(path);
				if (gitCommandResult.Succeeded)
				{
					RepositoryManager.Instance.AddOrUpdateLastOpened(gitCommandResult.Result);
				}
			}
			RepositoryManager.Instance.Save();
			RepositoryManagerUserControl.Refresh();
		}

		protected override void OnExpanding()
		{
			base.OnExpanding();
			RepositoryManagerUserControl.OnDirectoryItemIsExpandedChanged();
		}

		protected override void OnCollapsing()
		{
			base.OnCollapsing();
			RepositoryManagerUserControl.OnDirectoryItemIsExpandedChanged();
		}
	}
}

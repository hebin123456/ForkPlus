// 阶段 4.5：WPF→Avalonia 迁移。
// - using System.Windows → using Avalonia.Input（DragEventArgs / DragDropEffects 在 Avalonia.Input）
// - e.Data.GetData(DataFormats.FileDrop) → e.Data.GetFiles()（Avalonia 文件拖放 API；参考 RepositoryUserControl.OnDrop）
using System.Linq;
using Avalonia.Input;
using Avalonia.Platform.Storage;
using ForkPlus.Git;
using ForkPlus.Git.Commands;

namespace ForkPlus.UI.UserControls
{
	public class RepositoryManagerRepositoryFolderItem : RepositoryManagerTreeViewItem
	{
		public RepositoryManagerUserControl RepositoryManagerUserControl { get; }

		public RepositoryManagerRepositoryFolderItem(RepositoryManagerUserControl repositoryManagerUserControl, string title, RepositoryManagerTreeViewItem parent)
			: base(parent)
		{
			RepositoryManagerUserControl = repositoryManagerUserControl;
			base.Title = title;
		}

		public override DragDropEffects GetDropEffect(DragEventArgs e, int index)
		{
			if (e.Data.GetFiles() != null)
			{
				return DragDropEffects.Move;
			}
			return DragDropEffects.None;
		}

		public override void Drop(DragEventArgs e, int index)
		{
			string[] array = e.Data.GetFiles()?.Select(f => f.Path.LocalPath).ToArray();
			if (array == null)
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

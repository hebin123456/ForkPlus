// 阶段 4.5：WPF→Avalonia 迁移。
// - using System.Windows → using Avalonia.Input（DragEventArgs / DragDropEffects 在 Avalonia.Input）
// - e.Data.GetData(DataFormats.FileDrop) → e.Data.GetFiles()（Avalonia 文件拖放 API；参考 RepositoryUserControl.OnDrop）
using System.Linq;
using Avalonia.Input;
using Avalonia.Platform.Storage;
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
			if (e.Data.GetFiles() != null)
			{
				return DragDropEffects.Move;
			}
			return DragDropEffects.None;
		}

		public override void Drop(DragEventArgs e, int index)
		{
			string[] source = e.Data.GetFiles()?.Select(f => f.Path.LocalPath).ToArray();
			if (source != null)
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

using Avalonia.Controls;
using System;
using System.Collections.Generic;
using ForkPlus.Git;
using ForkPlus.UI.Commands;
using ForkPlus.UI.UserControls;
using ForkPlus.UI.UserControls.Preferences;
using Avalonia.Input;

namespace ForkPlus.UI.Dialogs.RepositoryOverview
{
	public partial class RepositoryOverviewCommitsUserControl : UserControl
	{
		private string _filepath = "";

		private RepositoryUserControl RepositoryUserControl { get; set; }

		public RepositoryOverviewCommitsUserControl()
		{
			InitializeComponent();
		}

		public void Initialize(RepositoryUserControl repositoryUserControl)
		{
			RepositoryUserControl = repositoryUserControl;
		}

		public void UpdateData(string path, Revision[] revisions)
		{
			_filepath = path;
			RevisionsListBox.ItemsSource = revisions.Map((Revision x) => new RepositoryOverviewCommitViewModel(x));
		}

		private void RevisionsListBoxItem_MouseDoubleClick(object sender, PointerPressedEventArgs e)
		{
			e.Handled = true;
			if (ItemsControl.ContainerFromElement(sender as ListBox, e.OriginalSource as DependencyObject) is ListBoxItem { DataContext: RepositoryOverviewCommitViewModel dataContext } && RepositoryUserControl != null)
			{
				GitModule gitModule = RepositoryUserControl.GitModule;
				if (gitModule != null)
				{
					RevealRevision(gitModule, dataContext.Sha, _filepath);
				}
			}
		}

		private void RevisionsListBox_ContextMenuOpening(object sender, ContextRequestedEventArgs e)
		{
			if (!(ItemsControl.ContainerFromElement(sender as ListBox, e.OriginalSource as DependencyObject) is ListBoxItem { DataContext: var dataContext }))
			{
				return;
			}
			RepositoryOverviewCommitViewModel item = dataContext as RepositoryOverviewCommitViewModel;
			if (item == null)
			{
				return;
			}
			GitModule gitModule = RepositoryUserControl.GitModule;
			if (gitModule != null)
			{
				List<Control> list = new List<Control>();
				MenuItem item2 = RepositoryUserControl.Commands.ShowRevisionInSeparateWindow.CreateMenuItem(delegate
				{
					RevisionDiffTarget.Revision target = new RevisionDiffTarget.Revision(item.Sha);
					RepositoryUserControl.Commands.ShowRevisionInSeparateWindow.Execute(RepositoryUserControl, target, _filepath);
				}, isEnabled: true, showShortcut: false);
				list.Add(item2);
				MenuItem menuItem = new MenuItem();
				menuItem.Header = PreferencesLocalization.MenuHeader("Reveal in Fork");
				menuItem.Click += delegate
				{
					RevealRevision(gitModule, item.Sha, _filepath);
				};
				list.Add(menuItem);
				list.Add(new Separator());
				list.AddRange(CreateRevisionContextMenuItems(item.Revision));
				RevisionsListBox.ContextMenu.SetItems(list);
			}
		}

		private void RevealRevision(GitModule gitModule, Sha sha, string filePath)
		{
			Application.Current.MainWindow.Activate();
			if (MainWindow.ActiveRepositoryUserControl?.GitModule != gitModule)
			{
				Application.Current.TabManager()?.OpenRepository(gitModule.Path);
			}
			MainWindow.ActiveRepositoryUserControl?.SelectRevision(sha, filePath);
		}

		private IEnumerable<Control> CreateRevisionContextMenuItems(Revision item)
		{
			yield return RepositoryUserControl.Commands.CopyRevisionSha.CreateMenuItem(delegate
			{
				RepositoryUserControl.Commands.CopyRevisionSha.Execute(new Revision[1] { item });
			});
			yield return RepositoryUserControl.Commands.CopyRevisionInfo.CreateMenuItem(delegate
			{
				RepositoryUserControl.Commands.CopyRevisionInfo.Execute(new Revision[1] { item });
			});
		}

	}
}

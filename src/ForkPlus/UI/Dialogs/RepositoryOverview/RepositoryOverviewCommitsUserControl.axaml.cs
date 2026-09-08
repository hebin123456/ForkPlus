using System;
using System.Collections.Generic;
using System.ComponentModel;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Markup;
using ForkPlus.Git;
using ForkPlus.UI;
using ForkPlus.UI.Commands;
using ForkPlus.UI.UserControls;
using ForkPlus.UI.UserControls.Preferences;
using Avalonia.Layout;
using Avalonia.Styling;

namespace ForkPlus.UI.Dialogs.RepositoryOverview
{
	public partial class RepositoryOverviewCommitsUserControl : UserControl
	{
		private string _filepath = "";

		private RepositoryUserControl RepositoryUserControl { get; set; }

		public RepositoryOverviewCommitsUserControl()
		{
			InitializeComponent();
			RevisionsListBox.AddHandler(
				InputElement.PointerPressedEvent,
				new EventHandler<PointerPressedEventArgs>(RevisionsListBox_ContextMenuPointerPressed),
				global::Avalonia.Interactivity.RoutingStrategies.Tunnel | global::Avalonia.Interactivity.RoutingStrategies.Bubble,
				handledEventsToo: true);
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

		private void RevisionsListBoxItem_MouseDoubleClick(object sender, global::Avalonia.Input.TappedEventArgs e)
		{
			e.Handled = true;
			if ((sender as ListBox)?.ContainerFromElement(e.Source as global::Avalonia.Visual) /* Migration note：WPF 双参静态方法 → 扩展方法实例调用 */ is ListBoxItem { DataContext: RepositoryOverviewCommitViewModel dataContext } && RepositoryUserControl != null)
			{
				GitModule gitModule = RepositoryUserControl.GitModule;
				if (gitModule != null)
				{
					RevealRevision(gitModule, dataContext.Sha, _filepath);
				}
			}
		}

		private void RevisionsListBox_ContextMenuOpening(object sender, global::Avalonia.Input.ContextRequestedEventArgs e)
		{
			if (OpenContextMenuForItem(e.Source, null))
			{
				e.Handled = true;
			}
			else
			{
				RevisionsListBox.ContextMenu?.Close();
				e.Handled = true;
			}
		}

		private void RevisionsListBox_ContextMenuPointerPressed(object sender, PointerPressedEventArgs e)
		{
			PointerPointProperties properties = e.GetCurrentPoint(RevisionsListBox).Properties;
			if (!properties.IsRightButtonPressed && properties.PointerUpdateKind != PointerUpdateKind.RightButtonPressed)
			{
				return;
			}
			e.Handled = true;
			RevisionsListBox.ContextMenu?.Close();
			if (!OpenContextMenuForItem(e.Source, e.GetPosition(RevisionsListBox)))
			{
				RevisionsListBox.ContextMenu?.Close();
			}
		}

		private bool OpenContextMenuForItem(object source, Point? position)
		{
			ListBoxItem container = RevisionsListBox.ContainerFromElement(source as global::Avalonia.Visual) as ListBoxItem;
			if (container == null && position.HasValue)
			{
				container = RevisionsListBox.GetContainerAtPoint<ListBoxItem>(position.Value);
			}
			if (container?.DataContext is not RepositoryOverviewCommitViewModel item)
			{
				return false;
			}
			RevisionsListBox.SelectedItem = item;
			RevisionsListBox.SelectedIndex = RevisionsListBox.Items.IndexOf(item);
			container.IsSelected = true;
			container.InvalidateVisual();
			GitModule gitModule = RepositoryUserControl.GitModule;
			if (gitModule == null)
			{
				return false;
			}
			List<Control> list = new List<Control>();
			MenuItem item2 = RepositoryUserControl.Commands.ShowRevisionInSeparateWindow.CreateMenuItem(delegate
			{
				RevisionDiffTarget.Revision target = new RevisionDiffTarget.Revision(item.Sha);
				RepositoryUserControl.Commands.ShowRevisionInSeparateWindow.Execute(RepositoryUserControl, target, _filepath);
			}, isEnabled: true, showShortcut: false);
			list.Add(item2);
			MenuItem menuItem = new MenuItem();
			menuItem.Header = PreferencesLocalization.MenuHeader("Reveal in ForkPlus");
			menuItem.Click += delegate
			{
				RevealRevision(gitModule, item.Sha, _filepath);
			};
			list.Add(menuItem);
			list.Add(new Separator());
			list.AddRange(CreateRevisionContextMenuItems(item.Revision));
			RevisionsListBox.ContextMenu.PlacementTarget = RevisionsListBox;
			RevisionsListBox.ContextMenu.SetItems(list);
			global::ForkPlus.UI.WpfCompat.ContextMenuCompat.AttachAutoDismiss(RevisionsListBox.ContextMenu, RevisionsListBox);
			RevisionsListBox.ContextMenu.Open();
			return true;
		}

		private void RevealRevision(GitModule gitModule, Sha sha, string filePath)
		{
			(global::Avalonia.Application.Current?.ApplicationLifetime as global::Avalonia.Controls.ApplicationLifetimes.IClassicDesktopStyleApplicationLifetime)?.MainWindow.Activate();
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

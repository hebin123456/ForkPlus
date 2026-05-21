using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Markup;
using System.Windows.Media.Animation;
using ForkPlus.Git;
using ForkPlus.Git.Commands;
using ForkPlus.Jobs;
using ForkPlus.UI.Controls;
using ForkPlus.UI.UserControls.Preferences;

namespace ForkPlus.UI.UserControls
{
	public partial class RevisionGraphTooltipUserControl : UserControl
	{
		private readonly RevisionsDataSource _revisionsDataSource = new RevisionsDataSource();

		private readonly RepositoryUserControl _repositoryUserControl;

		private readonly Sha _sha;

		public event EventHandler<EventArgs<double>> HeightChanged;

		public RevisionGraphTooltipUserControl(RepositoryUserControl repositoryUserControl, Sha sha)
		{
			_repositoryUserControl = repositoryUserControl;
			_sha = sha;
			InitializeComponent();
			RevisionListView.ItemsSource = _revisionsDataSource;
			WeakEventManager<NotificationCenter, EventArgs<ThemeType>>.AddHandler(NotificationCenter.Current, "ApplicationThemeChanged", ApplicationThemeChanged);
			Refresh();
		}

		private void Refresh()
		{
			FallbackMessageTextBlock.Collapse();
			RevisionListView.Collapse();
			BusyIndicator.Show();
			GitModule gitModule = _repositoryUserControl.GitModule;
			RepositoryData fullRepositoryData = _repositoryUserControl.RepositoryData;
			Sha sha = _sha;
			_repositoryUserControl.JobQueue.Add(PreferencesLocalization.Current("Get Revision Storage"), delegate
			{
				GitCommandResult<RevisionStorage> revisionStorageResponse = new GetRevisionStorageGitCommand().Execute(gitModule, sha);
				base.Dispatcher.Async(delegate
				{
					if (!revisionStorageResponse.Succeeded)
					{
						BusyIndicator.Collapse();
						RevisionListView.Collapse();
						FallbackMessageTextBlock.Show();
						FallbackMessageTextBlock.Text = PreferencesLocalization.FormatCurrent("Error: {0}", revisionStorageResponse.Error.FriendlyDescription);
					}
					else
					{
						BusyIndicator.Collapse();
						FallbackMessageTextBlock.Collapse();
						RevisionListView.Show();
						RepositoryReferences references = RepositoryReferences.New(fullRepositoryData.References.ReferenceStorage.WithHead(_sha), new string[0], new string[0], new string[0], hideTags: false);
						RevisionStorage result = revisionStorageResponse.Result;
						RepositoryRemotes remotes = fullRepositoryData.Remotes;
						RepositoryWorktrees worktrees = fullRepositoryData.Worktrees;
						CollapseState collapseState = new CollapseState(collapseAllMode: true, new HashSet<Sha>(new Sha[1] { _sha }));
						RevisionListView.SelectedIndex = -1;
						_revisionsDataSource.Reload(_repositoryUserControl.JobQueue, result, RepositoryStashes.Empty, references, remotes, worktrees, showStashesInRevisionList: false, reflog: false, collapseState, UserColors.Empty, gitModule);
						RefreshHeight();
					}
				});
			}, JobFlags.Hidden);
		}

		private void RefreshHeight()
		{
			int num = Math.Min(10, _revisionsDataSource.Count) * 23 + 22 + 16;
			this.HeightChanged?.Invoke(this, new EventArgs<double>(num));
			DoubleAnimation doubleAnimation = new DoubleAnimation(base.ActualHeight, num, TimeSpan.FromSeconds(0.05));
			doubleAnimation.EasingFunction = new QuadraticEase
			{
				EasingMode = EasingMode.EaseOut
			};
			BeginAnimation(FrameworkElement.HeightProperty, doubleAnimation);
		}

		private void RevisionListView_SizeChanged(object sender, SizeChangedEventArgs e)
		{
			RevisionListView.UpdateResizableColumnWidth(0);
		}

		private void GraphCellView_ExpandToggle(object sender, EventArgs e)
		{
			if ((sender as GraphCellView)?.DataContext is DecoratedRevision decoratedRevision)
			{
				if (decoratedRevision.IsCollapsed)
				{
					ExpandMergeRevision(decoratedRevision.Sha);
				}
				else
				{
					CollapseMergeRevision(decoratedRevision.Sha);
				}
			}
		}

		private void ApplicationThemeChanged(object sender, EventArgs<ThemeType> e)
		{
			_revisionsDataSource.RefreshTheme();
		}

		private void CollapseMergeRevision(Sha sha)
		{
			RepositoryData repositoryData = _repositoryUserControl.RepositoryData;
			if (repositoryData != null)
			{
				RevisionListView.SelectedIndex = -1;
				_revisionsDataSource.Reload(_repositoryUserControl.JobQueue, repositoryData.RevisionStorage, repositoryData.Stashes, repositoryData.References, repositoryData.Remotes, repositoryData.Worktrees, showStashesInRevisionList: false, reflog: false, _revisionsDataSource.CollapseState.Collapse(sha), repositoryData.UserColors, _revisionsDataSource.GitModule);
			}
		}

		private void ExpandMergeRevision(Sha sha)
		{
			RepositoryData repositoryData = _repositoryUserControl.RepositoryData;
			if (repositoryData != null)
			{
				RevisionListView.SelectedIndex = -1;
				_revisionsDataSource.Reload(_repositoryUserControl.JobQueue, repositoryData.RevisionStorage, repositoryData.Stashes, repositoryData.References, repositoryData.Remotes, repositoryData.Worktrees, showStashesInRevisionList: false, reflog: false, _revisionsDataSource.CollapseState.Expand(sha), repositoryData.UserColors, _revisionsDataSource.GitModule);
			}
		}

	}
}

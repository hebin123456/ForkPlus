// 阶段 4.5：WPF→Avalonia 迁移。
// - using System.Windows → using Avalonia + using Avalonia.Interactivity（RoutedEventArgs）
// - using System.Windows.Controls → using Avalonia.Controls（UserControl/SizeChangedEventArgs）
// - using System.Windows.Markup → 移除
// - using System.Windows.Media.Animation → using Avalonia.Animation（Transitions/DoubleTransition）+ using Avalonia.Animation.Easings（QuadraticEaseOut）
// - 新增 using Avalonia.Layout（Layoutable.HeightProperty）、using Avalonia.Threading（Dispatcher.UIThread）
// - WeakEventManager<NotificationCenter,EventArgs<ThemeType>>.AddHandler(...,"ApplicationThemeChanged",h)
//   → NotificationCenter.Current.ApplicationThemeChanged += h（参考 StatisticsUserControl）
// - base.Dispatcher.Async → Dispatcher.UIThread.Post（参考 RevisionDetailsUserControl）
// - DoubleAnimation + BeginAnimation(FrameworkElement.HeightProperty, anim) → Transitions + DoubleTransition + 修改 Height（参考 RevisionDetailsUserControl）
// - WPF QuadraticEase { EasingMode = EaseOut } → Avalonia QuadraticEaseOut（参考 RevisionDetailsUserControl）
// - base.ActualHeight → Bounds.Height（参考 RevisionDetailsUserControl）
using System;
using System.Collections.Generic;
using System.ComponentModel;
using Avalonia;
using Avalonia.Animation;
using Avalonia.Animation.Easings;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Threading;
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
			// 阶段 4.5：WeakEventManager → 直接事件订阅（参考 StatisticsUserControl）。
			NotificationCenter.Current.ApplicationThemeChanged += ApplicationThemeChanged;
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
				// 阶段 4.5：WPF base.Dispatcher.Async → Avalonia Dispatcher.UIThread.Post（参考 RevisionDetailsUserControl）。
				Dispatcher.UIThread.Post(delegate
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
			// 阶段 4.5：WPF DoubleAnimation + BeginAnimation(FrameworkElement.HeightProperty, anim) → Avalonia Transitions + DoubleTransition + 修改 Height（参考 RevisionDetailsUserControl）。
			Transitions = new Transitions
			{
				new DoubleTransition
				{
					Property = Layoutable.HeightProperty,
					Duration = TimeSpan.FromSeconds(0.05),
					Easing = new QuadraticEaseOut()
				}
			};
			// 阶段 4.5：WPF base.ActualHeight → Avalonia Bounds.Height（参考 RevisionDetailsUserControl）。
			// 设置目标 Height 触发过渡（从当前 Bounds.Height 动画到 num）。
			Height = num;
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

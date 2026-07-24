// 阶段 4.5：WPF→Avalonia 迁移。
// - using System.Windows → using Avalonia（GridLength/GridUnitType/Grid 静态方法）
// - using System.Windows.Controls → using Avalonia.Controls（UserControl）
// - using System.Windows.Markup → 移除
// - WeakEventManager<NotificationCenter, EventArgs<T>>.AddHandler(NotificationCenter.Current, "EventName", handler)
//   → NotificationCenter.Current.EventName += handler（参考 SubmoduleDiffUserControl/StatisticsUserControl）
// - Grid.SetRow/SetColumn/SetRowSpan/SetColumnSpan API 兼容（Avalonia.Controls.Grid）
// - GridLength/GridUnitType.Pixel API 兼容（Avalonia 命名空间）
using System;
using System.Collections.Generic;
using System.ComponentModel;
using Avalonia;
using Avalonia.Controls;
using ForkPlus.Git;
using ForkPlus.Settings;
using ForkPlus.UI.Controls;
using ForkPlus.UI.UserControls.Preferences;

namespace ForkPlus.UI.UserControls
{
	public partial class RepositoryContentUserControl : UserControl, ForkPlus.UI.ILocalizableControl
	{
		private bool _isLoaded;

		private DecoratedRevision[] _selectedRevisions;

		private bool _handleRevisionListViewSelectioChangedEvent = true;

		private RepositoryUserControl _repositoryUserControl { get; set; }

		public RepositoryContentUserControl()
		{
			InitializeComponent();
			base.Loaded += delegate
			{
				if (!_isLoaded)
				{
					_isLoaded = true;
					RestoreRevisionListViewColumnWidth();
					CommitUserControl.UpdateCommitMode();
				}
			};
		}

		public void Initialize(RepositoryUserControl repositoryUserControl, SearchTabItem sidebarSearchTabItem)
		{
			_repositoryUserControl = repositoryUserControl;
			RevisionListStatusBarUserControl.RepositoryUserControl = _repositoryUserControl;
			RevisionDetails.Initialize(_repositoryUserControl, RevisionDetailsUserControlMode.MainWindow);
			CommitUserControl.Initialize(_repositoryUserControl);
			RevisionListViewUserControl.Initialize(_repositoryUserControl, sidebarSearchTabItem);
			RevisionListViewUserControl.SearchQueryChanged += RevisionListViewUserControl_SearchRequestChanged;
			RevisionListViewUserControl.SelectionChanged += RevisionListViewUserControl_SelectionChanged;
			RevisionListViewUserControl.RevisionDoubleClick += RevisionListViewUserControl_RevisionDoubleClick;
			RevisionListViewUserControl.BranchDoubleClick += RevisionListViewUserControl_BranchDoubleClick;
			// 阶段 4.5：WeakEventManager → 直接事件订阅（参考 SubmoduleDiffUserControl/StatisticsUserControl）。
			NotificationCenter.Current.RevisionListOrientatioChanged += RevisionListOrientationChanged;
			NotificationCenter.Current.CompactBranchLabelsChanged += CompactBranchLabelsChanged;
			VerticalGridSplitter.DragCompleted += delegate
			{
				SaveRevisionListViewColumnWidth();
			};
			UpdateRevisionListOrientation(ForkPlusSettings.Default.RevisionListOrientation);
		}

		public void ApplyLocalization()
		{
			PreferencesLocalization.Apply(this, ForkPlusSettings.Default.UiLanguage);
			CommitUserControl.ApplyLocalization();
			RevisionDetails.ApplyLocalization();
		}

		public void SetRepositoryViewMode(RepositoryViewMode viewMode)
		{
			switch (viewMode)
			{
			case RepositoryViewMode.RevisionViewMode:
				RevisionView.Show();
				CommitView.Collapse();
				break;
			case RepositoryViewMode.CommitViewMode:
				RevisionView.Collapse();
				CommitView.Show();
				break;
			default:
				throw new InvalidOperationException();
			}
		}

		public void RefreshRevisionItems([Null] RepositoryData oldRepositoryData, RepositoryData repositoryData, RevisionContextSearch? contextSearch, [Null] RevisionSelector select)
		{
			bool flag = oldRepositoryData != null && oldRepositoryData.RevisionStorage != repositoryData.RevisionStorage && oldRepositoryData.RevisionStorage.Timestamp == repositoryData.RevisionStorage.Timestamp && oldRepositoryData.References == repositoryData.References && oldRepositoryData.Stashes == repositoryData.Stashes;
			if (oldRepositoryData == repositoryData)
			{
				return;
			}
			if (flag)
			{
				RevisionListViewUserControl.RevisionsDataSource.Extend(repositoryData.RevisionStorage, contextSearch);
				RevisionListViewUserControl.RevisionSearchPanelUserControl.UpdateMatchesCount(contextSearch?.MatchCount);
				return;
			}
			bool num = select == null;
			int selectedIndex = RevisionListViewUserControl.SelectedIndex;
			Sha[] array = RevisionListViewUserControl.SelectedRevisions.Map((DecoratedRevision x) => x.Sha);
			RevisionListViewUserControl.UpdateRepositoryData(repositoryData);
			NoUIAutomationListView.SelectOptions selectOptions = ((!num) ? ((NoUIAutomationListView.SelectOptions)3) : NoUIAutomationListView.SelectOptions.None);
			select = select ?? ((array.Length != 0) ? ((RevisionSelector)new RevisionSelector.Sha(array)) : ((RevisionSelector)new RevisionSelector.Head()));
			RevisionListViewUserControl.Select(select, selectOptions, selectedIndex);
			_repositoryUserControl.CancelActiveFetchRevisionsJobs();
		}

		public void SelectRevisions(IReadOnlyList<Sha> shas, NoUIAutomationListView.SelectOptions selectOptions, [Null] string filePath = null)
		{
			int count = shas.Count;
			if (count == 1)
			{
				SelectRevisions(new RevisionDiffTarget.Revision(shas[0]), selectOptions, filePath);
			}
			else if (count == 2)
			{
				SelectRevisions(new RevisionDiffTarget.Range(shas[0], shas[1]), selectOptions, filePath);
			}
			else if (count > 2)
			{
				SelectRevisions(new RevisionDiffTarget.MultipleRevisions(shas), selectOptions, filePath);
			}
		}

		public void SelectRevisions(RevisionDiffTarget target, NoUIAutomationListView.SelectOptions selectOptions, [Null] string filePath = null)
		{
			if (_repositoryUserControl.ViewMode != 0)
			{
				_repositoryUserControl.ActivateRevisionView();
			}
			if (filePath != null)
			{
				_handleRevisionListViewSelectioChangedEvent = false;
			}
			RevisionSelector select = ((!(target is RevisionDiffTarget.Range range)) ? ((!(target is RevisionDiffTarget.MultipleRevisions multipleRevisions)) ? new RevisionSelector.Sha(target.Sha) : new RevisionSelector.Sha(multipleRevisions.AllShas)) : new RevisionSelector.Sha(new Sha[2] { range.Sha, range.OtherSha }));
			bool num = RevisionListViewUserControl.Select(select, selectOptions);
			_handleRevisionListViewSelectioChangedEvent = true;
			if (!num || filePath != null)
			{
				RevisionDetails.ShowRevisionDetails(target, filePath);
			}
		}

		private void RevisionListViewUserControl_SearchRequestChanged(object sender, EventArgs<RevisionSearchQuery> e)
		{
			RevisionDetails.HighlightSearchMatches(e.Value);
		}

		private void RevisionListViewUserControl_SelectionChanged(object sender, EventArgs<DecoratedRevision[]> e)
		{
			if (!_handleRevisionListViewSelectioChangedEvent)
			{
				return;
			}
			DecoratedRevision[] value = e.Value;
			if (value == null)
			{
				return;
			}
			_selectedRevisions = value;
			DecoratedRevision decoratedRevision = _selectedRevisions.SingleItem();
			if (decoratedRevision != null)
			{
				RevisionDetails.ShowRevisionDetails(new RevisionDiffTarget.Revision(decoratedRevision.Sha));
			}
			else if (_selectedRevisions.Length == 2)
			{
				RevisionDetails.ShowRevisionDetails(new RevisionDiffTarget.Range(_selectedRevisions[0].Sha, _selectedRevisions[1].Sha));
			}
			else if (_selectedRevisions.Length != 0)
			{
				RevisionDetails.ShowRevisionDetails(new RevisionDiffTarget.MultipleRevisions(_selectedRevisions.Map((DecoratedRevision x) => x.Sha)));
			}
		}

		private void RevisionListViewUserControl_RevisionDoubleClick(object sender, EventArgs<DecoratedRevision> e)
		{
			DecoratedRevision value = e.Value;
			if (value.IsStash())
			{
				RepositoryUserControl.Commands.ShowApplyStashWindow.Execute(_repositoryUserControl, value.AsStashRevision());
				return;
			}
			Branch branch = value.References?.CompactMap((ReferenceViewModel x) => x.Reference as Branch).FirstItem();
			if (!(branch is LocalBranch { IsActive: not false }))
			{
				if (branch != null)
				{
					RepositoryUserControl.Commands.ShowCheckoutBranchWindow.Execute(_repositoryUserControl, branch);
				}
				else
				{
					RepositoryUserControl.Commands.ShowCheckoutRevisionWindow.Execute(_repositoryUserControl, value.ToRevision(), value.Sha);
				}
			}
		}

		private void RevisionListViewUserControl_BranchDoubleClick(object sender, EventArgs<Branch> e)
		{
			Branch value = e.Value;
			RepositoryUserControl.Commands.ShowCheckoutBranchWindow.Execute(_repositoryUserControl, value);
		}

		private void RevisionListOrientationChanged(object sender, EventArgs<RevisionListOrientation> e)
		{
			UpdateRevisionListOrientation(e.Value);
		}

		private void CompactBranchLabelsChanged(object sender, EventArgs<bool> e)
		{
			RepositoryData repositoryData = _repositoryUserControl.RepositoryData;
			if (repositoryData != null)
			{
				RefreshRevisionItems(null, repositoryData, null, null);
			}
		}

		private void UpdateRevisionListOrientation(RevisionListOrientation orientation)
		{
			switch (orientation)
			{
			case RevisionListOrientation.Vertical:
				Grid.SetRowSpan(RevisionListViewUserControl, 2);
				Grid.SetColumnSpan(RevisionListViewUserControl, 1);
				Grid.SetRow(RevisionDetails, 1);
				Grid.SetRowSpan(RevisionDetails, 2);
				Grid.SetColumn(RevisionDetails, 1);
				Grid.SetColumnSpan(RevisionDetails, 2);
				VerticalGridSplitter.Show();
				FirstColumnHorizontalGridSplitter.Collapse();
				SecondColumnHorizontalGridSplitter.Collapse();
				ThirdColumnHorizontalGridSplitter.Collapse();
				break;
			case RevisionListOrientation.Horizontal:
				Grid.SetRowSpan(RevisionListViewUserControl, 1);
				Grid.SetColumnSpan(RevisionListViewUserControl, 3);
				Grid.SetRow(RevisionDetails, 2);
				Grid.SetRowSpan(RevisionDetails, 1);
				Grid.SetColumn(RevisionDetails, 0);
				Grid.SetColumnSpan(RevisionDetails, 3);
				FirstColumnHorizontalGridSplitter.Show();
				SecondColumnHorizontalGridSplitter.Show();
				ThirdColumnHorizontalGridSplitter.Show();
				VerticalGridSplitter.Collapse();
				break;
			}
		}

		private void RestoreRevisionListViewColumnWidth()
		{
			double verticalLayoutRevisionListViewWidth = ForkPlusSettings.Default.VerticalLayoutRevisionListViewWidth;
			RevisionView.ColumnDefinitions[0].Width = new GridLength(verticalLayoutRevisionListViewWidth, GridUnitType.Pixel);
		}

		private void SaveRevisionListViewColumnWidth()
		{
			double value = RevisionView.ColumnDefinitions[0].Width.Value;
			ForkPlusSettings.Default.VerticalLayoutRevisionListViewWidth = value;
			ForkPlusSettings.Default.Save();
		}

	}
}

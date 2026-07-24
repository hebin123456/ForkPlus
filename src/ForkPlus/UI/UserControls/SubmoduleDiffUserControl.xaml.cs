// 阶段 4.5：WPF→Avalonia 迁移。
// - using System.Windows → using Avalonia + using Avalonia.Interactivity（RoutedEventArgs）
// - using System.Windows.Controls → using Avalonia.Controls（UserControl/SizeChangedEventArgs）
// - using System.Windows.Input → using Avalonia.Input（PointerWheelEventArgs）
// - using System.Windows.Markup → 移除
// - 新增 using Avalonia.Threading（Dispatcher.UIThread）
// - MouseWheelEventHandler → EventHandler<PointerWheelEventArgs>（参考 FileDiffControl）
// - RevisionListView.PreviewMouseWheel → RevisionListView.PointerWheelChanged（参考 FileDiffControl）
// - WeakEventManager<NotificationCenter,EventArgs<ThemeType>>.AddHandler(...,"ApplicationThemeChanged",h)
//   → NotificationCenter.Current.ApplicationThemeChanged += h（参考 StatisticsUserControl）
// - repositoryUserControl.Dispatcher.Invoke → Dispatcher.UIThread.Post（参考 RevisionDetailsUserControl）
using System;
using System.ComponentModel;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using ForkPlus.Git;
using ForkPlus.Git.Commands;
using ForkPlus.Jobs;
using ForkPlus.UI.Controls;
using ForkPlus.UI.Dialogs;
using ForkPlus.UI.UserControls.Preferences;

namespace ForkPlus.UI.UserControls
{
	public partial class SubmoduleDiffUserControl : UserControl, DiffControlContainer.IFileDiffControlSubControl
	{
		private readonly RevisionsDataSource _revisionsDataSource = new RevisionsDataSource();

		[Null]
		private SubmoduleDiffContent _content;

		[Null]
		private RepositoryUserControl _repositoryUserControl;

		public Sha? SrcSha => _content?.SrcSha;

		public Sha? DstSha => _content?.DstSha;

		// 阶段 4.5：WPF MouseWheelEventHandler → Avalonia EventHandler<PointerWheelEventArgs>（参考 FileDiffControl）。
		public event EventHandler<PointerWheelEventArgs> RevisionListViewPreviewMouseWheel
		{
			add
			{
				// 阶段 4.5：WPF PreviewMouseWheel → Avalonia PointerWheelChanged（参考 FileDiffControl）。
				RevisionListView.PointerWheelChanged += value;
			}
			remove
			{
				RevisionListView.PointerWheelChanged -= value;
			}
		}

		public SubmoduleDiffUserControl()
		{
			InitializeComponent();
			RevisionListView.ItemsSource = _revisionsDataSource;
			UpdateSubmoduleButton.Collapse();
			OpenSubmoduleButton.Click += OpenSubmoduleButton_Click;
			UpdateSubmoduleButton.Click += UpdateSubmoduleButton_Click;
			// 阶段 4.5：WeakEventManager → 直接事件订阅（参考 StatisticsUserControl）。
			NotificationCenter.Current.ApplicationThemeChanged += ApplicationThemeChanged;
		}

		public void ControlWillBeRemovedFromFileDiffControl()
		{
		}

		public void Update(RepositoryUserControl repositoryUserControl, SubmoduleDiffContent content, ViewMode viewMode = ViewMode.Diff)
		{
			_repositoryUserControl = repositoryUserControl;
			_content = content;
			TitleTextBlock.Text = PreferencesLocalization.FormatCurrent("Submodule '{0}' changed", content.Submodule.FriendlyName);
			BehindAheadTextBlock.Text = GetBehindAheadString(content.BehindAheadCount);
			RefreshUncommittedChangesTextBlock(content.ChangedFilePaths);
			if (viewMode == ViewMode.Commmit && content.ChangedFilePaths.Length == 0)
			{
				UpdateSubmoduleButton.Show();
			}
			RevisionHeaderUserControl.SetSubmoduleRevisions(content);
			RevisionListView.SelectedIndex = -1;
			_revisionsDataSource.Reload(repositoryUserControl.JobQueue, content.RevisionStorage, RepositoryStashes.Empty, content.References, content.Remotes, RepositoryWorktrees.Empty, showStashesInRevisionList: false, reflog: false, CollapseState.Empty, UserColors.Empty, content.GitModule);
		}

		private void ApplicationThemeChanged(object sender, EventArgs<ThemeType> e)
		{
			_revisionsDataSource.RefreshTheme();
		}

		private void RevisionListView_SizeChanged(object sender, SizeChangedEventArgs e)
		{
			RevisionListView.UpdateResizableColumnWidth(0);
		}

		private void OpenSubmoduleButton_Click(object sender, RoutedEventArgs e)
		{
			SubmoduleDiffContent content = _content;
			if (content != null)
			{
				RepositoryUserControl repositoryUserControl = _repositoryUserControl;
				if (repositoryUserControl != null)
				{
					RepositoryUserControl.Commands.OpenSubmodule.Execute(repositoryUserControl, content.ParentGitModule, new Submodule[1] { content.Submodule });
				}
			}
		}

		private void UpdateSubmoduleButton_Click(object sender, RoutedEventArgs e)
		{
			if (_content == null)
			{
				return;
			}
			RepositoryUserControl repositoryUserControl = _repositoryUserControl;
			if (repositoryUserControl == null)
			{
				return;
			}
			repositoryUserControl.JobQueue.Add(PreferencesLocalization.Current("Updating submodule..."), delegate(JobMonitor monitor)
			{
				monitor.Update(0.0, _content.Submodule.FriendlyName);
				GitCommandResult updateSubmodulesResult = new UpdateSubmodulesGitCommand().Execute(_content.ParentGitModule, new Submodule[1] { _content.Submodule }, monitor);
				// 阶段 4.5：WPF repositoryUserControl.Dispatcher.Invoke → Avalonia Dispatcher.UIThread.Post（参考 RevisionDetailsUserControl）。
				Dispatcher.UIThread.Post(delegate
				{
					if (!updateSubmodulesResult.Succeeded)
					{
						new ErrorWindow(repositoryUserControl, updateSubmodulesResult.Error).ShowDialog();
					}
					repositoryUserControl.InvalidateAndRefresh(SubDomain.Status);
				});
			});
		}

		private void RefreshUncommittedChangesTextBlock(string[] changedFilePaths)
		{
		 int num = changedFilePaths.Length;
		 if (num > 0)
		 {
		  string text = (num == 1)
		   ? PreferencesLocalization.FormatCurrent("{0} uncommitted file", num)
		   : PreferencesLocalization.FormatCurrent("{0} uncommitted files", num);
		  string text2 = string.Join("\n", changedFilePaths);
		  UncommittedFilesTextBlock.Show();
		  UncommittedFilesTextBlock.Text = text;
		  UncommittedFilesTextBlock.ToolTip = text + ":\n" + text2;
		 }
			else
			{
				UncommittedFilesTextBlock.Text = "";
				UncommittedFilesTextBlock.Collapse();
			}
		}

		private string GetBehindAheadString(BehindAheadCount behindAheadCount)
		{
			int left = behindAheadCount.Left;
			int right = behindAheadCount.Right;
			if (right > 0)
			{
				if (left > 0)
				{
					return $"{left}↓ {right}↑";
				}
				return $"{right}↑";
			}
			if (left > 0)
			{
				return $"{left}↓";
			}
			return "";
		}

	}
}

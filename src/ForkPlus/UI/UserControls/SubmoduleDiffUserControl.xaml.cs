using System;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Markup;
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

		public event MouseWheelEventHandler RevisionListViewPreviewMouseWheel
		{
			add
			{
				RevisionListView.PreviewMouseWheel += value;
			}
			remove
			{
				RevisionListView.PreviewMouseWheel -= value;
			}
		}

		public SubmoduleDiffUserControl()
		{
			InitializeComponent();
			RevisionListView.ItemsSource = _revisionsDataSource;
			UpdateSubmoduleButton.Collapse();
			OpenSubmoduleButton.Click += OpenSubmoduleButton_Click;
			UpdateSubmoduleButton.Click += UpdateSubmoduleButton_Click;
			WeakEventManager<NotificationCenter, EventArgs<ThemeType>>.AddHandler(NotificationCenter.Current, "ApplicationThemeChanged", ApplicationThemeChanged);
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
				repositoryUserControl.Dispatcher.Invoke(delegate
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
				string arg = ((num == 1) ? "file" : "files");
				string text = $"{num} uncommitted {arg}";
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

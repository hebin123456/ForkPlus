using System;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Markup;
using System.Windows.Media;
using System.Windows.Media.Animation;
using ForkPlus.Git;
using ForkPlus.Git.Commands;
using ForkPlus.Jobs;
using ForkPlus.Settings;
using ForkPlus.UI.Controls;
using ForkPlus.UI.Dialogs;
using ForkPlus.UI.UserControls.Preferences;

namespace ForkPlus.UI.UserControls
{
	public partial class RevisionDetailsUserControl : UserControl, ForkPlus.UI.ILocalizableControl
	{
		private enum RevisionDetailsTab
		{
			Commit,
			Changes,
			FileTree
		}

		private static readonly double RevisionDetailsHeaderNormalHeight = 25.0;

		private static readonly double RevisionDetailsHeaderDoubleHeight = 48.0;

		[Null]
		private RevisionDiffTarget _target;

		private bool _isTabIndicatorInitialized;

		private RevisionDetailsTab _activeTab = RevisionDetailsTab.Commit;

		private double _indicatorWidth;

		[Null]
		private Job _loadFullRevisionDetailsJob;

		[Null]
		public FullRevisionDetails FullRevisionDetails { get; private set; }

		[Null]
		public GitModule GitModule => RepositoryUserControl.GitModule;

		public RepositoryUserControl RepositoryUserControl { get; private set; }

		public RevisionDetailsUserControlMode Mode { get; private set; }

		public event EventHandler<RevisionDetails> RevisionDetailsUpdated;

		public RevisionDetailsUserControl()
		{
			InitializeComponent();
			RevisionDetailsHeaderUserControl.Height = 0.0;
			SummaryUserControl.RevisionDetailsUserControl = this;
			ChangesUserControl.RevisionDetailsUserControl = this;
			FileTreeUserControl.RevisionDetailsUserControl = this;
			ShowOnlyTab(_activeTab);
			CommitRadioButton.IsChecked = true;
			RevisionDetailsHeaderUserControl.SwapRevisionsButton.Click += delegate
			{
				if (_target is RevisionDiffTarget.Range range)
				{
					ShowRevisionDetails(range.Swap());
				}
			};
			ShowRevisionInSeparateWindowButton.Click += delegate
			{
				if (_target != null)
				{
					string fileToSelect = null;
					if (_activeTab == RevisionDetailsTab.Changes)
					{
						fileToSelect = ChangesUserControl.SelectedFile?.Path;
					}
					RepositoryUserControl.Commands.ShowRevisionInSeparateWindow.Execute(RepositoryUserControl.GitModule, _target, fileToSelect);
				}
			};
			ApplyLocalization();
		}

		public void ApplyLocalization()
		{
			CommitRadioButton.Content = PreferencesLocalization.Translate("Commit", ForkPlusSettings.Default.UiLanguage);
			ChangesRadioButton.Content = PreferencesLocalization.Translate("Changes", ForkPlusSettings.Default.UiLanguage);
			FileTreeRadioButton.Content = PreferencesLocalization.Translate("File Tree", ForkPlusSettings.Default.UiLanguage);
			ShowRevisionInSeparateWindowButton.ToolTip = PreferencesLocalization.Translate("Open in separate window", ForkPlusSettings.Default.UiLanguage);
			SummaryUserControl.ApplyLocalization();
			ChangesUserControl.ApplyLocalization();
		}

		public void Initialize(RepositoryUserControl repositoryUserControl, RevisionDetailsUserControlMode mode)
		{
			RepositoryUserControl = repositoryUserControl;
			Mode = mode;
			RefreshLayout();
		}

		public void ShowRevisionDetails(RevisionDiffTarget target, [Null] string fileToSelect = null)
		{
			_target = target;
			if (target is RevisionDiffTarget.MultipleRevisions multipleRevisions)
			{
				string title = "Select two commits to see difference between them";
				string message = $"{multipleRevisions.AllShas.Count} commits selected";
				ContentContainer.ShowFallback(title, message);
			}
			else
			{
				RefreshRevisionDetails(RepositoryUserControl.GitModule, target, fileToSelect);
			}
		}

		public void HighlightSearchMatches([Null] RevisionSearchQuery searchQuery)
		{
			SummaryUserControl.HighlightSearchMatches(searchQuery);
		}

		public void ShowInFileTreeTab(string filePath)
		{
			SelectTab(RevisionDetailsTab.FileTree);
			FileTreeUserControl.ShowRevisionDetails(filePath);
		}

		private void TabRadioButton_Checked(object sender, RoutedEventArgs e)
		{
			if (sender == CommitRadioButton)
			{
				SelectTab(RevisionDetailsTab.Commit);
			}
			else if (sender == ChangesRadioButton)
			{
				SelectTab(RevisionDetailsTab.Changes);
			}
			else if (sender == FileTreeRadioButton)
			{
				SelectTab(RevisionDetailsTab.FileTree);
			}
		}

		private void RefreshShowRevisionInSeparateWindowButton()
		{
			if (Mode == RevisionDetailsUserControlMode.DetachedWindow || Mode == RevisionDetailsUserControlMode.AiReview)
			{
				ShowRevisionInSeparateWindowButton.Hide();
			}
			else if (_target is RevisionDiffTarget.Revision || _target is RevisionDiffTarget.Range)
			{
				ShowRevisionInSeparateWindowButton.Show();
			}
			else
			{
				ShowRevisionInSeparateWindowButton.Hide();
			}
		}

		private void RefreshLayout()
		{
			switch (Mode)
			{
			case RevisionDetailsUserControlMode.MainWindow:
				SetHeaderPosition(top: false);
				FileTreeRadioButton.Show();
				break;
			case RevisionDetailsUserControlMode.DetachedWindow:
			case RevisionDetailsUserControlMode.AiReview:
				SetHeaderPosition(top: true);
				FileTreeRadioButton.Show();
				break;
			case RevisionDetailsUserControlMode.InteractiveRebase:
				SetHeaderPosition(top: false);
				FileTreeRadioButton.Hide();
				if (_activeTab == RevisionDetailsTab.FileTree)
				{
					CommitRadioButton.IsChecked = true;
				}
				break;
			}
			RefreshShowRevisionInSeparateWindowButton();
		}

		private void SetHeaderPosition(bool top)
		{
			if (top)
			{
				Grid.SetRow(TabHeaderContainer, 1);
				Grid.SetRow(RevisionDetailsHeaderUserControl, 0);
				RevisionDetailsHeaderUserControl.BorderThickness = new Thickness(0.0);
				RevisionDetailsHeaderUserControl.Margin = new Thickness(0.0, 0.0, 0.0, 4.0);
			}
			else
			{
				Grid.SetRow(TabHeaderContainer, 0);
				Grid.SetRow(RevisionDetailsHeaderUserControl, 1);
				RevisionDetailsHeaderUserControl.BorderThickness = new Thickness(0.0, 1.0, 0.0, 0.0);
				RevisionDetailsHeaderUserControl.Margin = new Thickness(0.0);
			}
		}

		private void RefreshRevisionDetails(GitModule gitModule, RevisionDiffTarget target, [Null] string fileToSelect)
		{
			if (RepositoryUserControl.RepositoryData == null)
			{
				return;
			}
			_loadFullRevisionDetailsJob?.Monitor.Cancel();
			Submodule[] submodules = RepositoryUserControl.RepositoryData.Submodules.Items;
			_loadFullRevisionDetailsJob = RepositoryUserControl.JobQueue.Add(PreferencesLocalization.Current("Revision details"), delegate(JobMonitor monitor)
			{
				if (!monitor.IsCanceled)
				{
					GitCommandResult<FullRevisionDetails> fullRevisionDetailsResponse = GetFullRevisionDetails(gitModule, submodules, target, monitor);
					if (!monitor.IsCanceled)
					{
						base.Dispatcher.Async(delegate
						{
							if (!monitor.IsCanceled)
							{
								_loadFullRevisionDetailsJob = null;
								if (!fullRevisionDetailsResponse.Succeeded)
								{
									GitCommandError error = fullRevisionDetailsResponse.Error;
									GitCommandError.UnsafeRepository unsafeRepositoryError = error as GitCommandError.UnsafeRepository;
									if (unsafeRepositoryError != null)
									{
										ContentContainer.ShowFallback("Error", unsafeRepositoryError.FriendlyDescription, isMonospase: true, "Mark repository as safe", delegate
										{
											AddRepositoryToSafeDirectoriesList(RepositoryUserControl, unsafeRepositoryError, target);
										});
									}
									else
									{
										ContentContainer.ShowFallback("Error", error.FriendlyDescription);
									}
								}
								else
								{
									ContentContainer.ShowContent();
									FullRevisionDetails = fullRevisionDetailsResponse.Result;
									_target = target;
									if (target is RevisionDiffTarget.Revision)
									{
										CommitRadioButton.Enable();
										FileTreeRadioButton.Enable();
									}
									else if (target is RevisionDiffTarget.Range || target is RevisionDiffTarget.WorkingDirectory)
									{
										CommitRadioButton.Disable();
										FileTreeRadioButton.Disable();
										SwitchToChangesTab();
									}
									RepositoryData repositoryData = RepositoryUserControl.RepositoryData;
									if (repositoryData != null)
									{
										BugtrackerLinkDefinition[] bugtrackers = repositoryData.Bugtrackers;
										FullRevisionDetails fullRevisionDetails = FullRevisionDetails;
										if (!(fullRevisionDetails is FullRevisionDetailsRange fullRevisionDetailsRange))
										{
											if (!(fullRevisionDetails is FullRevisionDetailsWorkingDirectory))
											{
												if (fullRevisionDetails != null)
												{
													if (_activeTab == RevisionDetailsTab.Changes || _activeTab == RevisionDetailsTab.FileTree)
													{
														ExpandRevisionDetailsHeader(isDoubleHeight: false);
													}
													else if (Mode == RevisionDetailsUserControlMode.DetachedWindow || Mode == RevisionDetailsUserControlMode.AiReview)
													{
														ExpandRevisionDetailsHeader(isDoubleHeight: false, animate: false);
													}
													else
													{
														CollapseRevisionDetailsHeader();
													}
													RevisionDetailsHeaderUserControl.SetRevisions(FullRevisionDetails.RevisionDetails, bugtrackers);
													this.RevisionDetailsUpdated?.Invoke(this, FullRevisionDetails.RevisionDetails);
												}
											}
											else
											{
												ExpandRevisionDetailsHeader(isDoubleHeight: true);
												RevisionDetailsHeaderUserControl.SetRevisions(FullRevisionDetails.RevisionDetails, bugtrackers, null, compareToWorkingDirectory: true);
											}
										}
										else
										{
											ExpandRevisionDetailsHeader(isDoubleHeight: true);
											RevisionDetailsHeaderUserControl.SetRevisions(FullRevisionDetails.RevisionDetails, bugtrackers, fullRevisionDetailsRange.SrcRevisionDetails);
										}
										RefreshShowRevisionInSeparateWindowButton();
										if (fileToSelect != null)
										{
											SwitchToChangesTab();
										}
										UpdateTabContent(_activeTab, fileToSelect);
									}
								}
							}
						});
					}
				}
			}, JobFlags.Hidden);
		}

		private void UpdateTabContent(RevisionDetailsTab tab, [Null] string fileToSelect)
		{
			RepositoryData repositoryData = RepositoryUserControl.RepositoryData;
			if (repositoryData == null || RepositoryUserControl.GitModule == null || FullRevisionDetails == null)
			{
				return;
			}
			RevisionDiffTarget target = _target;
			if (target == null)
			{
				return;
			}
			BugtrackerLinkDefinition[] bugtrackers = repositoryData.Bugtrackers;
			switch (tab)
			{
			case RevisionDetailsTab.Commit:
				if (Mode == RevisionDetailsUserControlMode.MainWindow || Mode == RevisionDetailsUserControlMode.InteractiveRebase)
				{
					CollapseRevisionDetailsHeader();
				}
				SummaryUserControl.Refresh(target.Sha, bugtrackers, repositoryData.UserColors);
				break;
			case RevisionDetailsTab.Changes:
				ExpandRevisionDetailsHeader(_target is RevisionDiffTarget.Range || _target is RevisionDiffTarget.WorkingDirectory);
				ChangesUserControl.Refresh(_target, fileToSelect);
				break;
			case RevisionDetailsTab.FileTree:
				if (_target is RevisionDiffTarget.Revision revision)
				{
					ExpandRevisionDetailsHeader(isDoubleHeight: false);
					FileTreeUserControl.Refresh(revision.Sha);
				}
				break;
			}
		}

		private void SelectTab(RevisionDetailsTab nextTab)
		{
			if (_activeTab == nextTab)
			{
				ShowOnlyTab(nextTab);
				if (_target != null)
				{
					UpdateTabContent(nextTab, null);
				}
				return;
			}
			ShowOnlyTab(nextTab);
			AnimateTabButtons(nextTab);
			_activeTab = nextTab;
			if (_target != null)
			{
				UpdateTabContent(nextTab, null);
			}
		}

		private void ShowOnlyTab(RevisionDetailsTab tab)
		{
			SummaryUserControl.Visibility = ((tab == RevisionDetailsTab.Commit) ? Visibility.Visible : Visibility.Collapsed);
			ChangesUserControl.Visibility = ((tab == RevisionDetailsTab.Changes) ? Visibility.Visible : Visibility.Collapsed);
			FileTreeUserControl.Visibility = ((tab == RevisionDetailsTab.FileTree) ? Visibility.Visible : Visibility.Collapsed);
		}

		private static GitCommandResult<FullRevisionDetails> GetFullRevisionDetails(GitModule gitModule, Submodule[] submodules, RevisionDiffTarget target, JobMonitor monitor)
		{
			if (monitor.IsCanceled)
			{
				return GitCommandResult<FullRevisionDetails>.Failure(new GitCommandError.Cancelled());
			}
			if (!(target is RevisionDiffTarget.Revision))
			{
				if (!(target is RevisionDiffTarget.Range range))
				{
					if (target is RevisionDiffTarget.WorkingDirectory)
					{
						GitCommandResult<RevisionDetails> gitCommandResult = new GetRevisionDetailsGitCommand().Execute(gitModule, target.Sha, monitor);
						if (monitor.IsCanceled)
						{
							return GitCommandResult<FullRevisionDetails>.Failure(new GitCommandError.Cancelled());
						}
						if (!gitCommandResult.Succeeded)
						{
							return GitCommandResult<FullRevisionDetails>.Failure(gitCommandResult.Error);
						}
						GitCommandResult<ChangedFile[]> gitCommandResult2 = new GetRevisionChangedFilesGitCommand().Execute(gitModule, target, submodules);
						if (monitor.IsCanceled)
						{
							return GitCommandResult<FullRevisionDetails>.Failure(new GitCommandError.Cancelled());
						}
						if (!gitCommandResult2.Succeeded)
						{
							return GitCommandResult<FullRevisionDetails>.Failure(gitCommandResult2.Error);
						}
						return GitCommandResult<FullRevisionDetails>.Success(new FullRevisionDetailsWorkingDirectory(gitCommandResult.Result, gitCommandResult2.Result));
					}
					return GitCommandResult<FullRevisionDetails>.Failure(new GitCommandError.Bug("Unknown RevisionDiffTarget type"));
				}
				GitCommandResult<RevisionDetails> gitCommandResult3 = new GetRevisionDetailsGitCommand().Execute(gitModule, target.Sha, monitor);
				if (monitor.IsCanceled)
				{
					return GitCommandResult<FullRevisionDetails>.Failure(new GitCommandError.Cancelled());
				}
				if (!gitCommandResult3.Succeeded)
				{
					return GitCommandResult<FullRevisionDetails>.Failure(gitCommandResult3.Error);
				}
				GitCommandResult<RevisionDetails> gitCommandResult4 = new GetRevisionDetailsGitCommand().Execute(gitModule, range.OtherSha, monitor);
				if (monitor.IsCanceled)
				{
					return GitCommandResult<FullRevisionDetails>.Failure(new GitCommandError.Cancelled());
				}
				if (!gitCommandResult4.Succeeded)
				{
					return GitCommandResult<FullRevisionDetails>.Failure(gitCommandResult4.Error);
				}
				GitCommandResult<ChangedFile[]> gitCommandResult5 = new GetRevisionChangedFilesGitCommand().Execute(gitModule, target, submodules);
				if (monitor.IsCanceled)
				{
					return GitCommandResult<FullRevisionDetails>.Failure(new GitCommandError.Cancelled());
				}
				if (!gitCommandResult5.Succeeded)
				{
					return GitCommandResult<FullRevisionDetails>.Failure(gitCommandResult5.Error);
				}
				return GitCommandResult<FullRevisionDetails>.Success(new FullRevisionDetailsRange(gitCommandResult4.Result, gitCommandResult3.Result, gitCommandResult5.Result));
			}
			GitCommandResult<FullRevisionDetails> gitCommandResult6 = new GetFullRevisionDetailsGitCommand().Execute(gitModule, target.Sha, submodules, monitor);
			if (monitor.IsCanceled)
			{
				return GitCommandResult<FullRevisionDetails>.Failure(new GitCommandError.Cancelled());
			}
			if (!gitCommandResult6.Succeeded)
			{
				return GitCommandResult<FullRevisionDetails>.Failure(gitCommandResult6.Error);
			}
			return GitCommandResult<FullRevisionDetails>.Success(gitCommandResult6.Result);
		}

		private void SwitchToChangesTab()
		{
			if (ChangesRadioButton.IsChecked != true)
			{
				ChangesRadioButton.IsChecked = true;
			}
		}

		private void CollapseRevisionDetailsHeader()
		{
			DoubleAnimation doubleAnimation = new DoubleAnimation(RevisionDetailsHeaderUserControl.ActualHeight, 0.0, TimeSpan.FromSeconds(0.18));
			doubleAnimation.EasingFunction = new QuadraticEase
			{
				EasingMode = EasingMode.EaseOut
			};
			RevisionDetailsHeaderUserControl.BeginAnimation(FrameworkElement.HeightProperty, doubleAnimation);
			RevisionDetailsHeaderUserControl.SwapRevisionsButton.Hide();
		}

		private void ExpandRevisionDetailsHeader(bool isDoubleHeight, bool animate = true)
		{
			double num = (isDoubleHeight ? RevisionDetailsHeaderDoubleHeight : RevisionDetailsHeaderNormalHeight);
			if (animate)
			{
				DoubleAnimation doubleAnimation = new DoubleAnimation(RevisionDetailsHeaderUserControl.ActualHeight, num, TimeSpan.FromSeconds(0.18));
				doubleAnimation.EasingFunction = new QuadraticEase
				{
					EasingMode = EasingMode.EaseOut
				};
				RevisionDetailsHeaderUserControl.BeginAnimation(FrameworkElement.HeightProperty, doubleAnimation);
			}
			else
			{
				RevisionDetailsHeaderUserControl.Height = num;
			}
			if (isDoubleHeight && Mode != RevisionDetailsUserControlMode.AiReview)
			{
				RevisionDetailsHeaderUserControl.SwapRevisionsButton.Show();
			}
			else
			{
				RevisionDetailsHeaderUserControl.SwapRevisionsButton.Hide();
			}
		}

		private void AnimateTabButtons(RevisionDetailsTab nextTab)
		{
			TranslateTransform translateTransform = new TranslateTransform();
			IndicatorBorder.RenderTransform = translateTransform;
			double tabXCoordinate = GetTabXCoordinate(_activeTab);
			double tabXCoordinate2 = GetTabXCoordinate(nextTab);
			DoubleAnimation animation = new DoubleAnimation(tabXCoordinate, tabXCoordinate2, TimeSpan.FromSeconds(0.2))
			{
				EasingFunction = new QuadraticEase
				{
					EasingMode = EasingMode.EaseOut
				}
			};
			translateTransform.BeginAnimation(TranslateTransform.XProperty, animation);
			double tabWidth = GetTabWidth(nextTab);
			DoubleAnimation animation2 = new DoubleAnimation(_indicatorWidth, tabWidth, TimeSpan.FromSeconds(0.2))
			{
				EasingFunction = new QuadraticEase
				{
					EasingMode = EasingMode.EaseOut
				}
			};
			if (_isTabIndicatorInitialized)
			{
				IndicatorBorder.BeginAnimation(FrameworkElement.WidthProperty, animation2);
			}
			_indicatorWidth = tabWidth;
		}

		private double GetTabXCoordinate(RevisionDetailsTab tab)
		{
			switch (tab)
			{
			case RevisionDetailsTab.Commit:
				return 0.0;
			case RevisionDetailsTab.Changes:
				return CommitRadioButton.ActualWidth;
			case RevisionDetailsTab.FileTree:
				return CommitRadioButton.ActualWidth + ChangesRadioButton.ActualWidth;
			default:
				return 0.0;
			}
		}

		private double GetTabWidth(RevisionDetailsTab tab)
		{
			switch (tab)
			{
			case RevisionDetailsTab.Commit:
				return CommitRadioButton.ActualWidth;
			case RevisionDetailsTab.Changes:
				return ChangesRadioButton.ActualWidth;
			case RevisionDetailsTab.FileTree:
				return FileTreeRadioButton.ActualWidth;
			default:
				return 0.0;
			}
		}

		private void TabControl_SizeChanged(object sender, SizeChangedEventArgs e)
		{
			if (!_isTabIndicatorInitialized)
			{
				InitializeTabIndicator();
			}
			else
			{
				SyncTabIndicator();
			}
		}

		private void InitializeTabIndicator()
		{
			SyncTabIndicator();
			_isTabIndicatorInitialized = true;
		}

		private void SyncTabIndicator()
		{
			IndicatorBorder.BeginAnimation(FrameworkElement.WidthProperty, null);
			TranslateTransform translateTransform = IndicatorBorder.RenderTransform as TranslateTransform;
			if (translateTransform == null)
			{
				translateTransform = new TranslateTransform();
				IndicatorBorder.RenderTransform = translateTransform;
			}
			translateTransform.X = GetTabXCoordinate(_activeTab);
			IndicatorBorder.Width = GetTabWidth(_activeTab);
			_indicatorWidth = IndicatorBorder.Width;
		}

		private void AddRepositoryToSafeDirectoriesList(RepositoryUserControl repositoryUserControl, GitCommandError.UnsafeRepository unsafeRepositoryError, RevisionDiffTarget target)
		{
			GitCommandResult gitCommandResult = new AddRepositoryToSafeDirectoriesListGitCommand().Execute(unsafeRepositoryError.ProposedRepositoryPath);
			if (!gitCommandResult.Succeeded)
			{
				new ErrorWindow(null, gitCommandResult.Error).ShowDialog();
			}
			repositoryUserControl.InvalidateAndRefresh(SubDomain.All);
			ShowRevisionDetails(target);
		}

	}
}

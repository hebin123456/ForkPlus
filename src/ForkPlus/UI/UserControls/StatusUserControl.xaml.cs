using System;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Markup;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;
using ForkPlus.Git;
using ForkPlus.Jobs;
using ForkPlus.Settings;
using ForkPlus.UI.UserControls.Preferences;

namespace ForkPlus.UI.UserControls
{
	public partial class StatusUserControl : UserControl, ForkPlus.UI.ILocalizableControl
	{
		private static readonly TimeSpan AnimationDuration = TimeSpan.FromSeconds(0.4);

		private static readonly char[] DirtyWorkingDirectoryMark = new char[1] { '*' };

		private static readonly string BranchFilterOnIconName = "BranchFilterOnIcon";

		private static readonly string BranchFilterOffIconName = "BranchFilterOffIcon";

		private readonly DispatcherTimer _refreshTimer = new DispatcherTimer();

		private bool _hovered;

		private bool _isJobManagerPopopOpen;

		[Null]
		private RepositoryUserControl _oldRepositoryUserControl;

		public StatusUserControl()
		{
			InitializeComponent();
			ApplyLocalization();
			_refreshTimer.Interval = TimeSpan.FromMilliseconds(200.0);
			_refreshTimer.Tick += _refreshTimer_Tick;
			_refreshTimer.Start();
			ActivityManagerPopup.Opened += delegate
			{
				ActivityManagerPopup.PopupAnimation = PopupAnimation.Fade;
				ShowActivityManagerToggleButton.Disable();
				ActivityManagerUserControl.Start();
				_isJobManagerPopopOpen = true;
			};
			ActivityManagerPopup.Closed += delegate
			{
				ActivityManagerPopup.PopupAnimation = PopupAnimation.Slide;
				ShowActivityManagerToggleButton.Enable();
				ActivityManagerUserControl.Stop();
				_isJobManagerPopopOpen = false;
			};
			base.MouseEnter += delegate
			{
				_hovered = true;
			};
			base.MouseLeave += delegate
			{
				_hovered = false;
			};
		}

		private void _refreshTimer_Tick(object sender, EventArgs e)
		{
			Refresh();
		}

		private void Refresh()
		{
			GitMmUserControl activeGitMmUserControl = MainWindow.Instance?.TabManager.ActiveGitMmUserControl;
			RepositoryUserControl activeRepositoryUserControl = MainWindow.ActiveRepositoryUserControl;
			if (activeRepositoryUserControl == null)
			{
				if (activeGitMmUserControl != null)
				{
					string gitMmTitle = activeGitMmUserControl.ActiveRepositoryUserControl?.RepositoryTitle;
					TitleTextBlock.Text = !string.IsNullOrWhiteSpace(gitMmTitle) ? gitMmTitle : (activeGitMmUserControl.SelectedSubrepoTitle ?? activeGitMmUserControl.WorkspaceTitle);
					SecondaryTitleTextBlock.Text = "";
					DescriptionTextBlock.Text = GitMmDescription(activeGitMmUserControl, activeGitMmUserControl.ActiveRepositoryUserControl?.GitModule?.Path ?? activeGitMmUserControl.WorkspacePath ?? "");
					CancelButton.Collapse();
					DescriptionIcon.Hide();
					BusyIndicator.Hide();
					StatusProgressBar.Hide();
					FilterButton.Collapse();
					_oldRepositoryUserControl = null;
					return;
				}
				TitleTextBlock.Text = Translate("Welcome to ForkPlus!");
				DescriptionTextBlock.Text = Translate("Open a repository to start");
				CancelButton.Collapse();
				DescriptionIcon.Hide();
				BusyIndicator.Hide();
				StatusProgressBar.Hide();
				FilterButton.Collapse();
				_oldRepositoryUserControl = null;
				return;
			}
			BusyIndicator.Hide(activeRepositoryUserControl.JobQueue.IsIdle);
			SecondaryBusyIndicator.Hide(activeRepositoryUserControl.JobQueue.IsIdle);
			Job primaryJob = activeRepositoryUserControl.JobQueue.PrimaryJob;
			if (primaryJob != null)
			{
				UpdateTitle(activeRepositoryUserControl, primaryJob.Name);
				if (primaryJob.Monitor.IsCanceled)
				{
					CancelButton.Collapse();
					DescriptionTextBlock.Text = ((primaryJob.Status == JobStatus.Finished) ? Translate("Canceled") : Translate("Canceling..."));
				}
				else
				{
					CancelButton.Visibility = ((primaryJob.Status == JobStatus.Finished) ? Visibility.Collapsed : Visibility.Visible);
					DescriptionTextBlock.Text = Translate(primaryJob.Monitor.ProgressMessage ?? "");
				}
				double? progress = primaryJob.Monitor.Progress;
				if (progress.HasValue)
				{
					double valueOrDefault = progress.GetValueOrDefault();
					StatusProgressBar.ShowWithProgress(5.0 + valueOrDefault * 0.95);
				}
				else
				{
					StatusProgressBar.Hide();
				}
				DescriptionIcon.Hide();
				FilterButton.Collapse();
				_oldRepositoryUserControl = activeRepositoryUserControl;
				return;
			}
			CancelButton.Hide();
			StatusProgressBar.Hide();
			string repositoryTitle = activeRepositoryUserControl.RepositoryTitle;
			UpdateTitle(activeRepositoryUserControl, !string.IsNullOrWhiteSpace(repositoryTitle) ? repositoryTitle : activeGitMmUserControl?.SelectedSubrepoTitle);
			RepositoryData repositoryData = activeRepositoryUserControl.RepositoryData;
			if (repositoryData == null)
			{
				DescriptionTextBlock.Text = Translate("loading...");
				DescriptionIcon.Hide();
				FilterButton.Collapse();
				return;
			}
			LocalBranch activeBranch = repositoryData.References.ActiveBranch;
			if (activeBranch == null)
			{
				DescriptionTextBlock.Text = Translate("detached HEAD");
				DescriptionIcon.Hide();
				return;
			}
			DescriptionIcon.Show();
			DescriptionTextBlock.Text = activeGitMmUserControl != null ? GitMmDescription(activeGitMmUserControl, activeBranch.Name) : activeBranch.Name;
			if (repositoryData.References.FilterReferences.Length != 0)
			{
				FilterButton.Show();
				FilterButton.ToolTip = Translate("Clear Branch Filter");
				FilterButtonImage.SetResourceReference(Image.SourceProperty, BranchFilterOnIconName);
			}
			else if (_hovered && repositoryData.References.ActiveBranch != null)
			{
				FilterButton.Show();
				FilterButton.ToolTip = Translate("Filter by Active Branch");
				FilterButtonImage.SetResourceReference(Image.SourceProperty, BranchFilterOffIconName);
			}
			else
			{
				FilterButton.Collapse();
			}
			_oldRepositoryUserControl = activeRepositoryUserControl;
		}

		private static string GitMmDescription(GitMmUserControl gitMmUserControl, string baseDescription)
		{
			string summary = gitMmUserControl?.StagedDiffSummary;
			if (string.IsNullOrWhiteSpace(summary))
			{
				return baseDescription ?? "";
			}
			return string.IsNullOrWhiteSpace(baseDescription) ? summary : summary + "  " + baseDescription;
		}

		public void ApplyLocalization()
		{
			ShowActivityManagerToggleButton.ToolTip = PreferencesLocalization.Translate("Activity Manager", ForkPlusSettings.Default.UiLanguage);
			ActivityManagerUserControl.ApplyLocalization();
		}

		private void UpdateTitle(RepositoryUserControl repositoryUserControl, string newValue)
		{
			newValue = newValue ?? "";
			string currentTitle = TitleTextBlock.Text ?? "";
			string currentSecondaryTitle = SecondaryTitleTextBlock.Text ?? "";
			if (!(currentTitle == newValue) && !(currentSecondaryTitle == newValue))
			{
				if (_oldRepositoryUserControl != repositoryUserControl || currentTitle.TrimEnd(DirtyWorkingDirectoryMark) == newValue.TrimEnd(DirtyWorkingDirectoryMark))
				{
					TitleTextBlock.Text = newValue;
					SecondaryTitleTextBlock.Text = "";
				}
				else if (newValue == repositoryUserControl.RepositoryTitle)
				{
					Grid.SetRow(SecondaryTitleGrid, 0);
					Grid.SetRow(TitleGrid, 1);
					TitleTextBlock.Text = newValue;
					RunAnimation(TitleContainerTranslateTransform, 8.0, -8.0, AnimationDuration, newValue, 1, 0);
				}
				else
				{
					Grid.SetRow(SecondaryTitleGrid, 0);
					Grid.SetRow(TitleGrid, 1);
					SecondaryTitleTextBlock.Text = newValue;
					RunAnimation(TitleContainerTranslateTransform, -8.0, 8.0, AnimationDuration, newValue, 0, 1);
				}
			}
		}

		private void CancelButton_Click(object sender, RoutedEventArgs e)
		{
			MainWindow.ActiveRepositoryUserControl?.JobQueue.PrimaryJob?.Monitor.Cancel();
		}

		private void FilterButton_Click(object sender, RoutedEventArgs e)
		{
			RepositoryUserControl activeRepositoryUserControl = MainWindow.ActiveRepositoryUserControl;
			if (activeRepositoryUserControl != null)
			{
				RepositoryUserControl.Commands.UpdateReferenceFilter.ToggleActiveBranchFilter(activeRepositoryUserControl);
			}
		}

		private void DescriptionTextBlock_MouseUp(object sender, MouseButtonEventArgs e)
		{
			ShowJobManager();
		}

		private void TitleTextBlock_MouseUp(object sender, MouseButtonEventArgs e)
		{
			ShowJobManager();
		}

		private void ShowJobManager()
		{
			if (!_isJobManagerPopopOpen && MainWindow.ActiveRepositoryUserControl != null)
			{
				ShowActivityManagerToggleButton.IsChecked = !ShowActivityManagerToggleButton.IsChecked;
			}
		}

		private static string Translate(string text)
		{
			return PreferencesLocalization.Translate(text, ForkPlusSettings.Default.UiLanguage);
		}

		public bool RunAnimation(TranslateTransform transform, double from, double to, TimeSpan duration, string newValue, int titleRow, int secondaryTitleRow)
		{
			DoubleAnimation doubleAnimation = new DoubleAnimation(from, to, duration);
			doubleAnimation.EasingFunction = new QuadraticEase
			{
				EasingMode = EasingMode.EaseOut
			};
			doubleAnimation.Completed += delegate
			{
				SecondaryTitleTextBlock.Text = newValue;
				TitleTextBlock.Text = newValue;
				Grid.SetRow(TitleGrid, titleRow);
				Grid.SetRow(SecondaryTitleGrid, secondaryTitleRow);
			};
			transform.BeginAnimation(TranslateTransform.YProperty, doubleAnimation);
			return true;
		}

	}
}

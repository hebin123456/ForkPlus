// 阶段 4.5：WPF→Avalonia 迁移。
// - using System.Windows → using Avalonia + using Avalonia.Interactivity（RoutedEventArgs）
// - using System.Windows.Controls → using Avalonia.Controls
// - using System.Windows.Controls.Primitives → using Avalonia.Controls.Primitives（Popup/ToggleButton）
// - using System.Windows.Input → using Avalonia.Input（PointerReleasedEventArgs）
// - using System.Windows.Markup → 移除
// - using System.Windows.Media → using Avalonia.Media（TranslateTransform）
// - using System.Windows.Media.Animation → using Avalonia.Animation + using Avalonia.Animation.Easings（DoubleTransition/QuadraticEaseOut）
// - using System.Windows.Threading → using Avalonia.Threading（DispatcherTimer）
// - DispatcherTimer（命名空间差异，API 兼容）
// - PopupAnimation.Fade/Slide → 移除（Avalonia 无等价，参考 GraphCellView）
// - base.MouseEnter/MouseLeave → base.PointerEntered/PointerLeave（参考 CommandHyperlink）
// - PointerPressedEventArgs → PointerReleasedEventArgs（MouseUp → PointerReleased；XAML 需同步迁移）
// - Visibility.Collapsed/Visible → Avalonia.Layout.Visibility（需 using Avalonia.Layout）
// - image.SetResourceReference(Image.SourceProperty, key) → image.Source = Theme.FindImage(key)
// - DoubleAnimation + BeginAnimation + Completed → Transitions + DoubleTransition + DispatcherTimer.RunOnce（参考 ModernTabControl）
using System;
using System.ComponentModel;
using Avalonia;
using Avalonia.Animation;
using Avalonia.Animation.Easings;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
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
				// 阶段 4.5：WPF PopupAnimation.Fade → Avalonia 无等价（参考 GraphCellView），移除。
				ShowActivityManagerToggleButton.Disable();
				ActivityManagerUserControl.Start();
				_isJobManagerPopopOpen = true;
			};
			ActivityManagerPopup.Closed += delegate
			{
				// 阶段 4.5：WPF PopupAnimation.Slide → Avalonia 无等价（参考 GraphCellView），移除。
				ShowActivityManagerToggleButton.Enable();
				ActivityManagerUserControl.Stop();
				_isJobManagerPopopOpen = false;
			};
			// 阶段 4.5：WPF MouseEnter → Avalonia PointerEntered（参考 CommandHyperlink）。
			base.PointerEntered += delegate
			{
				_hovered = true;
			};
			// 阶段 4.5：WPF MouseLeave → Avalonia PointerLeave（参考 ChunkSelectionLayer）。
			base.PointerLeave += delegate
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
				// 阶段 4.5：WPF SetResourceReference(Image.SourceProperty, key) → Source = Theme.FindImage(key)。
				FilterButtonImage.Source = Theme.FindImage(BranchFilterOnIconName);
			}
			else if (_hovered && repositoryData.References.ActiveBranch != null)
			{
				FilterButton.Show();
				FilterButton.ToolTip = Translate("Filter by Active Branch");
				// 阶段 4.5：WPF SetResourceReference(Image.SourceProperty, key) → Source = Theme.FindImage(key)。
				FilterButtonImage.Source = Theme.FindImage(BranchFilterOffIconName);
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
		// v3.4.1：Job.Name 可能是调用方硬编码的英文（如 "Stage"/"Reset File"/"Delete 'X'"），
		// 这里统一走 Translate 二次翻译，配合 PreferencesLocalization 的字典/模式匹配救回大部分情况。
		newValue = Translate(newValue);
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

		// 阶段 4.5：WPF MouseUp + PointerPressedEventArgs → Avalonia PointerReleased + PointerReleasedEventArgs（XAML 需同步迁移）。
		private void DescriptionTextBlock_MouseUp(object sender, PointerReleasedEventArgs e)
		{
			ShowJobManager();
		}

		// 阶段 4.5：WPF MouseUp + PointerPressedEventArgs → Avalonia PointerReleased + PointerReleasedEventArgs（XAML 需同步迁移）。
		private void TitleTextBlock_MouseUp(object sender, PointerReleasedEventArgs e)
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

		// 阶段 4.5：WPF DoubleAnimation + BeginAnimation + EasingFunction + Completed
		// → Avalonia Transitions + DoubleTransition + Easing + DispatcherTimer.RunOnce（参考 ModernTabControl）。
		// WPF QuadraticEase { EasingMode = EaseOut } → Avalonia QuadraticEaseOut。
		// WPF transform.BeginAnimation(TranslateTransform.YProperty, animation) → 设置 Transitions 后修改 Y 触发过渡。
		// WPF DoubleAnimation.Completed → DispatcherTimer.RunOnce 延迟执行完成回调（时长 = duration）。
		public bool RunAnimation(TranslateTransform transform, double from, double to, TimeSpan duration, string newValue, int titleRow, int secondaryTitleRow)
		{
			transform.Transitions = new Transitions
			{
				new DoubleTransition
				{
					Property = TranslateTransform.YProperty,
					Duration = duration,
					Easing = new QuadraticEaseOut()
				}
			};
			// 阶段 4.5：先设置到起始位置，再在下一帧设置目标位置以触发过渡（参考 ModernTabControl）。
			transform.Y = from;
			_ = Dispatcher.UIThread.Post(() =>
			{
				transform.Y = to;
			});
			// 阶段 4.5：WPF DoubleAnimation.Completed → DispatcherTimer.RunOnce 延迟执行完成回调。
			DispatcherTimer.RunOnce(delegate
			{
				SecondaryTitleTextBlock.Text = newValue;
				TitleTextBlock.Text = newValue;
				Grid.SetRow(TitleGrid, titleRow);
				Grid.SetRow(SecondaryTitleGrid, secondaryTitleRow);
			}, duration);
			return true;
		}

	}
}

using System;
using System.Linq;
using ForkPlus.UI.WpfCompat;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup;
using Avalonia.Media;
using Avalonia.Threading;
using Avalonia.VisualTree;
using ForkPlus.Jobs;
using ForkPlus.Settings;
using ForkPlus.UI.Controls;
using ForkPlus.UI.Controls.Editor;
using ForkPlus.UI.UserControls.Preferences;
using ForkPlus.UI.Helpers;
using Avalonia.Layout;
using Avalonia.Styling;
using Avalonia.Interactivity;

namespace ForkPlus.UI.UserControls
{
	public partial class ActivityManagerUserControl : UserControl, ForkPlus.UI.ILocalizableControl
	{
		public class JobViewModel : INotifyPropertyChanged
		{
			private DateTime? _finishTime;

			private string _message;

			private double _currentProgress;

			private bool _finishTimeTextBlockVisibility;

			private bool _jobProgressProgressBarVisibility;

			private bool _warningImageVisibility;

			private bool _cancelButtonVisibility;

			private bool _busyIndicatorVisibility;

			public Job Job { get; }

			public string Name => Job.Name;

			public bool IsGitMmJob => Job.Name != null && Job.Name.StartsWith("git mm", StringComparison.OrdinalIgnoreCase);

			public bool GitMmBadgeVisibility => IsGitMmJob ? true : false;

			public string GitMmCategoryKey
			{
				get
				{
					if (!IsGitMmJob)
					{
						return "";
					}
					string name = Job.Name ?? "";
					if (name.StartsWith("git mm scan", StringComparison.OrdinalIgnoreCase))
					{
						return "Scan";
					}
					if (name.StartsWith("git mm status", StringComparison.OrdinalIgnoreCase))
					{
						return "Status";
					}
					if (name.StartsWith("git mm sync", StringComparison.OrdinalIgnoreCase))
					{
						return "Sync";
					}
					if (name.StartsWith("git mm upload", StringComparison.OrdinalIgnoreCase))
					{
						return "Upload";
					}
					if (name.StartsWith("git mm start", StringComparison.OrdinalIgnoreCase))
					{
						return "Start";
					}
					return "git mm";
				}
			}

			public string GitMmCategoryText => Translate(GitMmCategoryKey);

			public bool IsGitMmScanJob => IsGitMmJob && GitMmCategoryKey == "Scan";

			public bool IsGitMmStatusJob => IsGitMmJob && GitMmCategoryKey == "Status";

			public bool IsGitMmSyncJob => IsGitMmJob && GitMmCategoryKey == "Sync";

			public bool IsGitMmUploadJob => IsGitMmJob && GitMmCategoryKey == "Upload";

			public bool IsGitMmStartJob => IsGitMmJob && GitMmCategoryKey == "Start";

			public DateTime? FinishTime
			{
				get
				{
					return _finishTime;
				}
				set
				{
					if (!(_finishTime == value))
					{
						_finishTime = value;
						this.PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("FinishTime"));
					}
				}
			}

			public string Message
			{
				get
				{
					return _message;
				}
				set
				{
					if (!(_message == value))
					{
						_message = value;
						this.PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("Message"));
					}
				}
			}

			public double CurrentProgress
			{
				get
				{
					return _currentProgress;
				}
				set
				{
					double num = 5.0 + value * 0.95;
					if (_currentProgress != num)
					{
						_currentProgress = num;
						this.PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("CurrentProgress"));
					}
				}
			}

			public bool FinishTimeTextBlockVisibility
			{
				get
				{
					return _finishTimeTextBlockVisibility;
				}
				set
				{
					if (_finishTimeTextBlockVisibility != value)
					{
						_finishTimeTextBlockVisibility = value;
						this.PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("FinishTimeTextBlockVisibility"));
					}
				}
			}

			public bool JobProgressProgressBarVisibility
			{
				get
				{
					return _jobProgressProgressBarVisibility;
				}
				set
				{
					if (_jobProgressProgressBarVisibility != value)
					{
						_jobProgressProgressBarVisibility = value;
						this.PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("JobProgressProgressBarVisibility"));
					}
				}
			}

			public bool WarningImageVisibility
			{
				get
				{
					return _warningImageVisibility;
				}
				set
				{
					if (_warningImageVisibility != value)
					{
						_warningImageVisibility = value;
						this.PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("WarningImageVisibility"));
					}
				}
			}

			public bool CancelButtonVisibility
			{
				get
				{
					return _cancelButtonVisibility;
				}
				set
				{
					if (_cancelButtonVisibility != value)
					{
						_cancelButtonVisibility = value;
						this.PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("CancelButtonVisibility"));
					}
				}
			}

			public bool BusyIndicatorVisibility
			{
				get
				{
					return _busyIndicatorVisibility;
				}
				set
				{
					if (_busyIndicatorVisibility != value)
					{
						_busyIndicatorVisibility = value;
						this.PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("BusyIndicatorVisibility"));
					}
				}
			}

			public event PropertyChangedEventHandler PropertyChanged;

			public JobViewModel(Job job)
			{
				Job = job;
				Refresh();
			}

			public void Refresh()
			{
				Message = PreferencesLocalization.Current(Job.Monitor.ProgressMessage ?? "");
				CurrentProgress = Job.Monitor.Progress.GetValueOrDefault();
				FinishTime = Job.FinishTime?.ToLocalTime();
				JobProgressProgressBarVisibility = ((Job.Status != JobStatus.Running) ? false : true);
				BusyIndicatorVisibility = ((Job.Status != JobStatus.Running) ? false : true);
				FinishTimeTextBlockVisibility = ((Job.Status != JobStatus.Finished) ? false : true);
				if (Job.Monitor.IsCanceled)
				{
					CancelButtonVisibility = false;
				}
				else
				{
					CancelButtonVisibility = ((Job.Status != JobStatus.Running) ? false : true);
				}
				if (Job.Status == JobStatus.Finished && (Job.Monitor.State == JobMonitorState.Failed || Job.Monitor.State == JobMonitorState.Canceled))
				{
					WarningImageVisibility = true;
				}
				else
				{
					WarningImageVisibility = false;
				}
			}
		}

		private readonly DispatcherTimer _refreshTimer = new DispatcherTimer();

		private readonly ObservableCollection<JobViewModel> _jobs = new ObservableCollection<JobViewModel>();

		// 2026-09-10：git mm 命令输出收编到活动管理器——独立"git-mm"标签页。
		// 仅 git mm 仓库显示该标签（单仓/普通仓隐藏），Sync 里按 ActiveGitMmUserControl != null 切换可见性。
		private TabItem _gitMmViewTab;

		private TabItem CreateGitMmViewTab()
		{
			return new TabItem
			{
				Header = Translate("git-mm"),
				Tag = ActivityManagerViewMode.GitMm,
				Height = 27.0,
				// 默认隐藏，Sync 根据当前活动仓库是否 git mm 仓再决定显隐。
				IsVisible = false
			};
		}

		/// <summary>按 Tag 查找视图模式 tab（ItemCollection 无 OfType，手动遍历）。</summary>
		private TabItem FindViewTab(ActivityManagerViewMode mode)
		{
			foreach (object item in ViewModeTabControl.Items)
			{
				if (item is TabItem tab && tab.Tag is ActivityManagerViewMode m && m == mode)
				{
					return tab;
				}
			}
			return null;
		}

		private uint _userJobsVersion;

		private int _selectedOutputJobId = -1;

		private int _selectedOutputLength = -1;

		public ActivityManagerUserControl()
		{
			InitializeComponent();
			ApplyLocalization();
			JobDetailsOutputEditor.Options.EnableHyperlinks = true;
			JobDetailsOutputEditor.Options.RequireControlModifierForHyperlinkClick = false;
			JobDetailsOutputEditor.TextArea.TextView.LineTransformers.Add(new GitOutputColorizer());
			_refreshTimer.Interval = TimeSpan.FromMilliseconds(200.0);
			_refreshTimer.Tick += _refreshTimer_Tick;
			JobListBox.ItemsSource = _jobs;
			RefreshTheme();
			WeakEventManager<NotificationCenter, EventArgs<ThemeType>>.AddHandler(NotificationCenter.Current, "ApplicationThemeChanged", ApplicationThemeChanged);
			ViewModeTabControl.Items.Add(new TabItem
			{
				Header = Translate("All"),
				Tag = ActivityManagerViewMode.All,
				Height = 27.0
			});
			ViewModeTabControl.Items.Add(new TabItem
			{
				Header = Translate("User"),
				Tag = ActivityManagerViewMode.User,
				Height = 27.0
			});
			ViewModeTabControl.Items.Add(new TabItem
			{
				Header = Translate("Background"),
				Tag = ActivityManagerViewMode.Background,
				Height = 27.0
			});
			// 2026-09-10：git mm 命令输出收编到活动管理器——独立"git-mm"标签页。
		// 仅 git mm 仓库显示该标签（单仓/普通仓隐藏），Sync 里按 ActiveGitMmUserControl != null 切换可见性。
		_gitMmViewTab = CreateGitMmViewTab();
		ViewModeTabControl.Items.Add(_gitMmViewTab);
		base.Loaded += delegate
		{
			// 2026-09-10：保存的视图模式可能是 git-mm，但当前仓库不是 git mm 仓（tab 已隐藏），
			// 此时选不到 git-mm tab → 回退到 All，避免无选中 tab。
			ActivityManagerViewMode saved = ForkPlusSettings.Default.ActivityManagerViewMode;
			bool isGitMmRepo = MainWindow.Instance?.TabManager.ActiveGitMmUserControl != null;
			if (saved == ActivityManagerViewMode.GitMm && !isGitMmRepo)
			{
				saved = ActivityManagerViewMode.All;
			}
			FindViewTab(saved)?.IsSelected = true;
		};
		}

		public void Start()
		{
			Sync();
			JobListBox.Focus();
			JobListBox.SelectedIndex = 0;
			JobListBox.FocusRow(0);
			_refreshTimer.Start();
		}

		public void Stop()
		{
			_refreshTimer.Stop();
		}

		public void ApplyLocalization()
		{
			PreferencesLocalization.Apply(this, ForkPlusSettings.Default.UiLanguage);
			foreach (object item in ViewModeTabControl.Items)
			{
				if (item is TabItem tabItem && tabItem.Tag is ActivityManagerViewMode viewMode)
				{
					tabItem.Header = Translate(viewMode.ToString());
				}
			}
		}

		private void ApplicationThemeChanged(object sender, EventArgs<ThemeType> e)
		{
			RefreshTheme();
		}

		private void _refreshTimer_Tick(object sender, EventArgs e)
		{
			Sync();
		}

		private void JobListBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
		{
			RefreshSelectedItem();
		}

		private void CancelButton_Click(object sender, RoutedEventArgs e)
		{
			if ((sender as global::Avalonia.Controls.Control)?.Parent<global::Avalonia.Controls.Control>()?.Parent<global::Avalonia.Controls.Control>()?.DataContext is JobViewModel jobViewModel)
			{
				jobViewModel.Job.Monitor.Cancel();
			}
		}

		private void ViewModeTabControl_SelectionChanged(object sender, SelectionChangedEventArgs e)
		{
			if (e.AddedItems.Count >= 1 && e.AddedItems[0] is TabItem tabItem)
			{
				if ((ActivityManagerViewMode)tabItem.Tag != 0)
				{
					ForkPlusSettings.Default.ActivityManagerViewMode = (ActivityManagerViewMode)tabItem.Tag;
					ForkPlusSettings.Default.Save();
				}
				_userJobsVersion = 0u;
				Sync();
				// git-mm 视图隐藏了作业列表，不应聚焦/选中它（否则聚焦隐藏控件异常）。
				if ((ActivityManagerViewMode)tabItem.Tag != ActivityManagerViewMode.GitMm)
				{
					JobListBox.Focus();
					JobListBox.SelectedIndex = 0;
					JobListBox.FocusRow(0);
				}
			}
		}

		private void RefreshTheme()
		{
			JobDetailsOutputEditor.TextArea.TextView.LinkTextForegroundBrush = Application.Current.TryFindResource("CodeEditorLinkForeground") as Brush;
		}

	private void Sync()
	{
		// 2026-09-10：git-mm 标签页仅在 git mm 仓库显示。当前活动仓库不是 git mm 仓时
		// （ActiveGitMmUserControl == null）隐藏该标签，避免单仓/普通仓的活动管理器出现无用的 git-mm tab。
		// 若用户正停在 git-mm 视图而切到非 git mm 仓，下面会把它切回 All 视图。
		bool isGitMmRepo = MainWindow.Instance?.TabManager.ActiveGitMmUserControl != null;
		if (_gitMmViewTab != null)
		{
			_gitMmViewTab.IsVisible = isGitMmRepo;
		}
		ActivityManagerViewMode viewMode = (ActivityManagerViewMode)((TabItem)ViewModeTabControl.SelectedItem).Tag;
		// 当前不是 git mm 仓却停在 git-mm 视图 → 切回 All，避免停在隐藏的 tab 上。
		if (viewMode == ActivityManagerViewMode.GitMm && !isGitMmRepo)
		{
			TabItem allTab = FindViewTab(ActivityManagerViewMode.All);
			if (allTab != null)
			{
				allTab.IsSelected = true;
				viewMode = ActivityManagerViewMode.All;
			}
		}
		// 2026-09-10：git-mm 视图是独立内容区，直接展示当前活动 GitMmUserControl 的命令输出，
		// 不走作业列表筛选（与 All/User/Background 按 JobFlags 筛选不同）。
		if (viewMode == ActivityManagerViewMode.GitMm)
		{
			SyncGitMmOutput();
			return;
		}
			// 非 git-mm 视图：恢复作业列表可见（从 git-mm 切回时之前隐藏了它）。
			JobListBox.IsVisible = true;
			JobQueue jobQueue = MainWindow.Instance?.TabManager.ActiveGitMmUserControl?.JobQueue ?? MainWindow.ActiveRepositoryUserControl?.JobQueue;
			if (jobQueue == null)
			{
				_jobs.Clear();
				_userJobsVersion = 0u;
				JobListFallBack.Show();
				RefreshSelectedItem();
				return;
			}
			uint jobLogVersion = jobQueue.JobLogVersion;
			if (_userJobsVersion != jobLogVersion)
			{
				Job[] jobHistory = jobQueue.GetJobHistory((ActivityManagerViewMode)((TabItem)ViewModeTabControl.SelectedItem).Tag switch
				{
					ActivityManagerViewMode.Debug => (Job x) => true, 
					ActivityManagerViewMode.All => (Job x) => (x.Flags & JobFlags.SaveToLog) != 0, 
					ActivityManagerViewMode.User => (Job x) => (x.Flags & JobFlags.SaveToLog) != 0 && (x.Flags & JobFlags.Background) == 0, 
					ActivityManagerViewMode.Background => (Job x) => (x.Flags & JobFlags.SaveToLog) != 0 && (x.Flags & JobFlags.Background) != 0, 
					_ => throw new Exception("Cannot reach here"), 
				});
				int num = 0;
				int i = 0;
				while (num < _jobs.Count && i < jobHistory.Length)
				{
					int num2 = _jobs[num].Job.Id.CompareTo(jobHistory[i].Id);
					if (num2 > 0)
					{
						_jobs.RemoveAt(num);
					}
					else if (num2 < 0)
					{
						_jobs.Insert(num, new JobViewModel(jobHistory[i]));
						num++;
						i++;
					}
					else
					{
						num++;
						i++;
					}
				}
				while (num < _jobs.Count)
				{
					_jobs.RemoveAt(num);
				}
				for (; i < jobHistory.Length; i++)
				{
					_jobs.Insert(num, new JobViewModel(jobHistory[i]));
					num++;
				}
				_userJobsVersion = jobLogVersion;
			}
			foreach (JobViewModel job in _jobs)
			{
				job.Refresh();
			}
			JobListFallBack.Hide(_jobs.Count > 0);
			RefreshSelectedItem();
		}

		/// <summary>
		/// 2026-09-10：git-mm 视图（独立内容区）——直接展示当前活动 GitMmUserControl 的命令输出，
		/// 不走作业列表。隐藏左侧作业列表，右侧输出面板显示 git mm 输出文本（经
		/// GitMmUserControl.GetOutputText() 读取，_refreshTimer 周期刷新）。
		/// v4.0.12（2026-09-12，"命令输出不会自动滚到最下面"）：CodeEditor 的 Text 整体替换会
		/// 重置视口，命令运行中新输出到来时用户停留在旧位置看不到最新内容。终端式
		/// stick-to-bottom：更新前视口已在底部附近（或初次显示/刚从其他视图切回）→ 更新后
		/// 滚到底跟随最新输出；用户上翻查看历史时不打断，滚回底部后恢复跟随。
		/// </summary>
		private bool _gitMmOutputWasEmpty = true;

		private bool IsOutputEditorNearBottom()
		{
			// 经模板 PART_ScrollViewer 判定（与 CodeEditor.SetScrollPosition 同通道；
			// AvaloniaEdit 12 的 TextView 不暴露 Extent/Viewport，ScrollViewer 才有）。
			ScrollViewer sv = JobDetailsOutputEditor.GetVisualDescendants().OfType<ScrollViewer>()
				.FirstOrDefault((ScrollViewer x) => x.Name == "PART_ScrollViewer");
			if (sv == null)
			{
				return true; // 找不到滚动器（模板未套用）：保守视为在底部，保持跟随
			}
			double maxOffset = sv.Extent.Height - sv.Viewport.Height;
			// maxOffset<=0：内容不足一屏（恒在底部）；容差 24≈一行，避免像素级判断漏判。
			return maxOffset <= 0.0 || sv.Offset.Y >= maxOffset - 24.0;
		}

		private void SyncGitMmOutput()
		{
			GitMmUserControl gitMm = MainWindow.Instance?.TabManager.ActiveGitMmUserControl;
			// 隐藏作业列表与空态占位（git-mm 视图不走作业列表）
			JobListBox.IsVisible = false;
			JobListFallBack.IsVisible = false;
			JobDetailsNameTextBlock.Text = Translate("git-mm");
			JobDetailsFinishTimeTextBlock.Text = string.Empty;
			if (gitMm == null || !gitMm.HasOutput)
			{
				JobDetailsFallBack.IsVisible = true;
				JobDetailsOutputEditor.Text = string.Empty;
				_gitMmOutputWasEmpty = true;
				return;
			}
			JobDetailsFallBack.IsVisible = false;
			string output = gitMm.GetOutputText();
			// 仅在内容变化时赋值，避免每次 tick 重置光标/滚动位置
			if (JobDetailsOutputEditor.Text != output)
			{
				bool stickToBottom = _gitMmOutputWasEmpty || IsOutputEditorNearBottom();
				JobDetailsOutputEditor.Text = output;
				if (stickToBottom)
				{
					// Render 优先级在布局之后执行：Text 整体替换引起的高度变化先落进
					// Extent，大偏移经 TextView clamp 到新文档底部（同步调用会用旧高度）。
					Dispatcher.UIThread.Post(delegate
					{
						JobDetailsOutputEditor.SetScrollPosition(double.MaxValue);
					}, DispatcherPriority.Render);
				}
				_gitMmOutputWasEmpty = false;
			}
		}

		private void RefreshSelectedItem()
		{
			if (!(JobListBox.SelectedItem is JobViewModel jobViewModel))
			{
				_selectedOutputJobId = -1;
				_selectedOutputLength = -1;
				JobDetailsFallBack.Show();
				return;
			}
			if (_selectedOutputJobId != jobViewModel.Job.Id)
			{
				_selectedOutputJobId = jobViewModel.Job.Id;
				_selectedOutputLength = -1;
			}
			JobDetailsFallBack.Hide();
			JobDetailsNameTextBlock.Text = jobViewModel.Name;
			JobStatus status = jobViewModel.Job.Status;
			JobMonitorState state = jobViewModel.Job.Monitor.State;
			string text = "";
			text = status switch
			{
				JobStatus.Running => (state != JobMonitorState.Canceled) ? Translate("running") : Translate("canceling..."), 
				JobStatus.Finished => state switch
				{
					JobMonitorState.Canceled => Translate("canceled"), 
					JobMonitorState.Failed => Translate("failed"), 
					JobMonitorState.Succeeded => Translate("succeeded"), 
					_ => Translate("succeeded"), 
				}, 
				_ => Translate("succeeded"), 
			} + jobViewModel.FinishTime?.ToString(" d MMM yyyy HH:mm:ss");
			JobDetailsFinishTimeTextBlock.Text = text;
		int outputLength = jobViewModel.Job.Monitor.OutputLength;
		if (_selectedOutputLength != outputLength)
		{
			string output = jobViewModel.Job.Monitor.Output;
			if (JobDetailsOutputEditor.Text != output)
			{
				JobDetailsOutputEditor.Text = output;
				// 普通作业视图占用了输出编辑器：下次 git-mm 视图刷新视为初次 → 滚到底
				//（用户切回 git-mm 视图时直接看到最新输出，而不是普通视图残留的滚动位置）。
				_gitMmOutputWasEmpty = true;
			}
			_selectedOutputLength = outputLength;
		}
	}

		private static string Translate(string text)
		{
			return PreferencesLocalization.Translate(text, ForkPlusSettings.Default.UiLanguage);
		}

	}
}

using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Markup;
using System.Windows.Media;
using System.Windows.Threading;
using ForkPlus.Jobs;
using ForkPlus.Settings;
using ForkPlus.UI.Controls;
using ForkPlus.UI.Controls.Editor;
using ForkPlus.UI.UserControls.Preferences;
using ForkPlus.UI.Helpers;

namespace ForkPlus.UI.UserControls
{
	public partial class ActivityManagerUserControl : UserControl, ForkPlus.UI.ILocalizableControl
	{
		public class JobViewModel : INotifyPropertyChanged
		{
			private DateTime? _finishTime;

			private string _message;

			private double _currentProgress;

			private Visibility _finishTimeTextBlockVisibility;

			private Visibility _jobProgressProgressBarVisibility;

			private Visibility _warningImageVisibility;

			private Visibility _cancelButtonVisibility;

			private Visibility _busyIndicatorVisibility;

			public Job Job { get; }

			public string Name => Job.Name;

			public bool IsGitMmJob => Job.Name != null && Job.Name.StartsWith("git mm", StringComparison.OrdinalIgnoreCase);

			public Visibility GitMmBadgeVisibility => IsGitMmJob ? Visibility.Visible : Visibility.Collapsed;

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

			public Visibility FinishTimeTextBlockVisibility
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

			public Visibility JobProgressProgressBarVisibility
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

			public Visibility WarningImageVisibility
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

			public Visibility CancelButtonVisibility
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

			public Visibility BusyIndicatorVisibility
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
				JobProgressProgressBarVisibility = ((Job.Status != JobStatus.Running) ? Visibility.Collapsed : Visibility.Visible);
				BusyIndicatorVisibility = ((Job.Status != JobStatus.Running) ? Visibility.Collapsed : Visibility.Visible);
				FinishTimeTextBlockVisibility = ((Job.Status != JobStatus.Finished) ? Visibility.Collapsed : Visibility.Visible);
				if (Job.Monitor.IsCanceled)
				{
					CancelButtonVisibility = Visibility.Collapsed;
				}
				else
				{
					CancelButtonVisibility = ((Job.Status != JobStatus.Running) ? Visibility.Collapsed : Visibility.Visible);
				}
				if (Job.Status == JobStatus.Finished && (Job.Monitor.State == JobMonitorState.Failed || Job.Monitor.State == JobMonitorState.Canceled))
				{
					WarningImageVisibility = Visibility.Visible;
				}
				else
				{
					WarningImageVisibility = Visibility.Collapsed;
				}
			}
		}

		private readonly DispatcherTimer _refreshTimer = new DispatcherTimer();

		private readonly ObservableCollection<JobViewModel> _jobs = new ObservableCollection<JobViewModel>();

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
			base.Loaded += delegate
			{
				ViewModeTabControl.Items.FirstItem((TabItem x) => (ActivityManagerViewMode)x.Tag == ForkPlusSettings.Default.ActivityManagerViewMode).IsSelected = true;
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
			if ((sender as FrameworkElement)?.Parent<FrameworkElement>()?.Parent<FrameworkElement>()?.DataContext is JobViewModel jobViewModel)
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
				JobListBox.Focus();
				JobListBox.SelectedIndex = 0;
				JobListBox.FocusRow(0);
			}
		}

		private void RefreshTheme()
		{
			JobDetailsOutputEditor.TextArea.TextView.LinkTextForegroundBrush = Application.Current.TryFindResource("CodeEditorLinkForeground") as Brush;
		}

		private void Sync()
		{
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

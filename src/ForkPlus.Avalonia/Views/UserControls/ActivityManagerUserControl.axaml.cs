using System;
using System.Collections.ObjectModel;
using System.Linq;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Threading;
using ForkPlus.Jobs;
using ForkPlus.Services;
using ForkPlus.Settings;
using ForkPlus.UI;

namespace ForkPlus.Avalonia.Views.UserControls
{
    // Avalonia 版 ActivityManagerUserControl（spike 简化新建版）。
    //
    // 对照 WPF 工程 src/ForkPlus/UI/UserControls/ActivityManagerUserControl.xaml.cs（485 行）：
    //   - JobViewModel : INotifyPropertyChanged（Job + Name + IsGitMmJob + GitMmBadgeVisibility +
    //     GitMmCategoryKey + GitMmCategoryText + IsGitMmScanJob/Status/Sync/Upload/StartJob +
    //     FinishTime + Message + CurrentProgress + 5 个 Visibility 属性 + Refresh()）
    //   - DispatcherTimer 200ms Tick → Sync()
    //   - ViewModeTabControl（All/User/Background 3 个 TabItem + Tag = ActivityManagerViewMode）
    //   - JobListBox.ItemsSource = ObservableCollection<JobViewModel>
    //   - JobDetailsOutputEditor（CodeEditor）显示选中 Job 输出
    //   - Sync()：MainWindow.Instance?.TabManager.ActiveGitMmUserControl?.JobQueue
    //             ?? MainWindow.ActiveRepositoryUserControl?.JobQueue
    //   - RefreshSelectedItem()：根据 JobStatus + JobMonitorState 显示状态文本 + FinishTime
    //   - CancelButton_Click：jobViewModel.Job.Monitor.Cancel()
    //   - ApplyLocalization / RefreshTheme
    //
    // Avalonia 版差异（spike 简化）：
    //   - WPF JobViewModel : INotifyPropertyChanged → spike POCO（重设 ItemsSource 触发刷新）
    //   - WPF CodeEditor + GitOutputColorizer → spike TextBox（spike 不实现语法高亮）
    //   - WPF Visibility.Collapsed/Visible → bool（spike 直接用 bool，IsVisible Binding）
    //   - WPF Image PNG 图标 → TextBlock emoji（⚠/✕）
    //   - WPF DispatcherTimer 200ms → spike 保留 DispatcherTimer
    //   - WPF NotificationCenter.ApplicationThemeChanged → spike 跳过
    //   - WPF ViewModeTabControl（ModernTabControl）→ Avalonia 原生 TabControl
    //   - spike 简化：JobViewModel 用 POCO，每 200ms 整体重建 ItemsSource 触发刷新
    public partial class ActivityManagerUserControl : UserControl
    {
        // ===== JobViewModel POCO（对照 WPF JobViewModel : INotifyPropertyChanged，254 行）=====
        // spike 简化为 POCO：spike 不需要细粒度 PropertyChanged，每 200ms 整体重建
        public class JobViewModel
        {
            public Job Job { get; }

            // 名称（对照 WPF: public string Name => Job.Name）
            public string Name => Job.Name ?? string.Empty;

            // IsGitMmJob（对照 WPF: Job.Name.StartsWith("git mm", OrdinalIgnoreCase)）
            public bool IsGitMmJob => !string.IsNullOrEmpty(Job.Name) &&
                Job.Name.StartsWith("git mm", StringComparison.OrdinalIgnoreCase);

            // GitMmCategoryKey（对照 WPF：Scan/Status/Sync/Upload/Start/git mm）
            public string GitMmCategoryKey
            {
                get
                {
                    if (!IsGitMmJob) return "";
                    string name = Job.Name ?? "";
                    if (name.StartsWith("git mm scan", StringComparison.OrdinalIgnoreCase)) return "Scan";
                    if (name.StartsWith("git mm status", StringComparison.OrdinalIgnoreCase)) return "Status";
                    if (name.StartsWith("git mm sync", StringComparison.OrdinalIgnoreCase)) return "Sync";
                    if (name.StartsWith("git mm upload", StringComparison.OrdinalIgnoreCase)) return "Upload";
                    if (name.StartsWith("git mm start", StringComparison.OrdinalIgnoreCase)) return "Start";
                    return "git mm";
                }
            }

            public bool IsGitMmScanJob => IsGitMmJob && GitMmCategoryKey == "Scan";
            public bool IsGitMmStatusJob => IsGitMmJob && GitMmCategoryKey == "Status";
            public bool IsGitMmSyncJob => IsGitMmJob && GitMmCategoryKey == "Sync";
            public bool IsGitMmUploadJob => IsGitMmJob && GitMmCategoryKey == "Upload";
            public bool IsGitMmStartJob => IsGitMmJob && GitMmCategoryKey == "Start";

            // 对照 WPF: public DateTime? FinishTime（ToLocaleTime()）
            public string FinishTimeText => Job.FinishTime?.ToLocalTime().ToString("HH:mm:ss") ?? string.Empty;
            public bool FinishTimeVisible => Job.Status == JobStatus.Finished;

            // Message（对照 WPF: PreferencesLocalization.Current(Job.Monitor.ProgressMessage ?? "")）
            // spike 简化：直接返回 ProgressMessage，不经过 Localization
            public string Message => Job.Monitor.ProgressMessage ?? "";

            // CurrentProgress（对照 WPF: 5.0 + value * 0.95）
            public double CurrentProgress
            {
                get
                {
                    double p = Job.Monitor.Progress ?? 0;
                    return 5.0 + p * 0.95;
                }
            }

            public bool ProgressVisible => Job.Status == JobStatus.Running;
            public bool WarningVisible => Job.Status == JobStatus.Finished &&
                (Job.Monitor.State == JobMonitorState.Failed || Job.Monitor.State == JobMonitorState.Canceled);
            public bool CancelVisible => Job.Status == JobStatus.Running && !Job.Monitor.IsCanceled;
            public bool BusyVisible => Job.Status == JobStatus.Running;

            public JobViewModel(Job job)
            {
                Job = job;
            }
        }

        // ===== 私有字段（对照 WPF）=====
        private readonly DispatcherTimer _refreshTimer = new DispatcherTimer();
        private readonly ObservableCollection<JobViewModel> _jobs = new ObservableCollection<JobViewModel>();
        private uint _userJobsVersion;
        private int _selectedOutputJobId = -1;
        private int _selectedOutputLength = -1;
        private Func<JobQueue> _getJobQueue;

        // ===== 构造函数（对照 WPF）=====
        public ActivityManagerUserControl()
        {
            InitializeComponent();
            ApplyLocalization();
            JobListBox.ItemsSource = _jobs;
            _refreshTimer.Interval = TimeSpan.FromMilliseconds(200);
            _refreshTimer.Tick += RefreshTimer_Tick;
        }

        // ===== Initialize（spike 新增，注入 getJobQueue 回调）=====
        // 对照 WPF: MainWindow.Instance?.TabManager.ActiveGitMmUserControl?.JobQueue
        //           ?? MainWindow.ActiveRepositoryUserControl?.JobQueue
        // spike 版: 调用方注入 getJobQueue 回调
        public void Initialize(Func<JobQueue> getJobQueue)
        {
            _getJobQueue = getJobQueue;
        }

        // ===== Start / Stop（对照 WPF）=====
        // 对照 WPF: public void Start()
        //   WPF: Sync(); JobListBox.Focus(); SelectedIndex = 0; _refreshTimer.Start();
        public void Start()
        {
            Sync();
            if (JobListBox.ItemCount > 0)
            {
                JobListBox.SelectedIndex = 0;
            }
            _refreshTimer.Start();
        }

        // 对照 WPF: public void Stop()
        public void Stop()
        {
            _refreshTimer.Stop();
        }

        // ===== ApplyLocalization（对照 WPF）=====
        // 对照 WPF: PreferencesLocalization.Apply(this, ForkPlusSettings.Default.UiLanguage);
        //           foreach (TabItem tabItem in ViewModeTabControl.Items) tabItem.Header = Translate(viewMode);
        // spike 版: 简化（spike 不调用 PreferencesLocalization.Apply）
        public void ApplyLocalization()
        {
            // spike 简化：ViewMode 标签使用静态文本（"All"/"User"/"Background"）
            // 真实翻译由调用方在语言切换时调用 SetViewModeHeaders 实现
        }

        // ===== SetViewModeHeaders（spike 新增，调用方注入翻译）=====
        public void SetViewModeHeaders(string allText, string userText, string backgroundText)
        {
            if (ViewModeTabControl?.Items != null && ViewModeTabControl.Items.Count >= 3)
            {
                if (ViewModeTabControl.Items[0] is TabItem t0) t0.Header = allText;
                if (ViewModeTabControl.Items[1] is TabItem t1) t1.Header = userText;
                if (ViewModeTabControl.Items[2] is TabItem t2) t2.Header = backgroundText;
            }
        }

        // ===== RefreshTimer_Tick（对照 WPF _refreshTimer_Tick）=====
        private void RefreshTimer_Tick(object sender, EventArgs e)
        {
            Sync();
        }

        // ===== JobListBox_SelectionChanged（对照 WPF）=====
        private void JobListBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            RefreshSelectedItem();
        }

        // ===== CancelButton_Click（对照 WPF）=====
        // 对照 WPF: if (sender.Parent.Parent.DataContext is JobViewModel vm) vm.Job.Monitor.Cancel();
        // spike 版: 通过 Button.DataContext 取 JobViewModel（Avalonia 数据绑定直接传 DataContext）
        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.DataContext is JobViewModel vm)
            {
                vm.Job.Monitor.Cancel();
            }
        }

        // ===== ViewModeTabControl_SelectionChanged（对照 WPF）=====
        // 对照 WPF: if (e.AddedItems[0] is TabItem tabItem)
        //           { if ((ActivityManagerViewMode)tabItem.Tag != 0) Save + Sync + SelectedIndex = 0; }
        // spike 版: 仅触发 Sync（spike 不持久化 ViewMode）
        private void ViewModeTabControl_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (e.AddedItems.Count >= 1 && e.AddedItems[0] is TabItem tabItem)
            {
                _userJobsVersion = 0;
                Sync();
                if (JobListBox.ItemCount > 0)
                {
                    JobListBox.SelectedIndex = 0;
                }
            }
        }

        // ===== Sync（对照 WPF private void Sync()）=====
        // 对照 WPF:
        //   JobQueue jobQueue = MainWindow.Instance?.TabManager.ActiveGitMmUserControl?.JobQueue
        //                       ?? MainWindow.ActiveRepositoryUserControl?.JobQueue;
        //   if (jobQueue == null) { _jobs.Clear(); JobListFallBack.Show(); return; }
        //   按 viewMode 过滤 GetJobHistory → 增量更新 _jobs
        //   foreach (JobViewModel job in _jobs) job.Refresh();
        //   JobListFallBack.Hide(_jobs.Count > 0);
        //   RefreshSelectedItem();
        // spike 版: spike 简化为整体重建（不做增量更新）
        private void Sync()
        {
            JobQueue jobQueue = _getJobQueue?.Invoke();
            if (jobQueue == null)
            {
                _jobs.Clear();
                _userJobsVersion = 0;
                if (JobListFallBack != null) JobListFallBack.IsVisible = true;
                RefreshSelectedItem();
                return;
            }

            uint jobLogVersion = jobQueue.JobLogVersion;
            if (_userJobsVersion != jobLogVersion)
            {
                ActivityManagerViewMode viewMode = GetSelectedViewMode();
                Job[] jobHistory = jobQueue.GetJobHistory(job => viewMode switch
                {
                    ActivityManagerViewMode.Debug => true,
                    ActivityManagerViewMode.All => (job.Flags & JobFlags.SaveToLog) != 0,
                    ActivityManagerViewMode.User => (job.Flags & JobFlags.SaveToLog) != 0 && (job.Flags & JobFlags.Background) == 0,
                    ActivityManagerViewMode.Background => (job.Flags & JobFlags.SaveToLog) != 0 && (job.Flags & JobFlags.Background) != 0,
                    _ => true,
                });

                _jobs.Clear();
                foreach (Job job in jobHistory)
                {
                    _jobs.Add(new JobViewModel(job));
                }
                _userJobsVersion = jobLogVersion;
            }

            if (JobListFallBack != null) JobListFallBack.IsVisible = _jobs.Count == 0;
            RefreshSelectedItem();
        }

        // ===== RefreshSelectedItem（对照 WPF private void RefreshSelectedItem()）=====
        // 对照 WPF:
        //   if (JobListBox.SelectedItem is JobViewModel vm) {
        //     JobDetailsFallBack.Hide();
        //     JobDetailsNameTextBlock.Text = vm.Name;
        //     text = (JobStatus + JobMonitorState).Translate() + vm.FinishTime?.ToString(" d MMM yyyy HH:mm:ss");
        //     JobDetailsFinishTimeTextBlock.Text = text;
        //     output = vm.Job.Monitor.Output;
        //     if (JobDetailsOutputEditor.Text != output) JobDetailsOutputEditor.Text = output;
        //   } else { JobDetailsFallBack.Show(); }
        // spike 版: 同样逻辑
        private void RefreshSelectedItem()
        {
            if (!(JobListBox.SelectedItem is JobViewModel vm))
            {
                _selectedOutputJobId = -1;
                _selectedOutputLength = -1;
                if (JobDetailsFallBack != null) JobDetailsFallBack.IsVisible = true;
                return;
            }
            if (JobDetailsFallBack != null) JobDetailsFallBack.IsVisible = false;

            if (JobDetailsNameTextBlock != null) JobDetailsNameTextBlock.Text = vm.Name;

            JobStatus status = vm.Job.Status;
            JobMonitorState state = vm.Job.Monitor.State;
            string text;
            if (status == JobStatus.Running)
            {
                text = state != JobMonitorState.Canceled ? Translate("running") : Translate("canceling...");
            }
            else if (status == JobStatus.Finished)
            {
                text = state switch
                {
                    JobMonitorState.Canceled => Translate("canceled"),
                    JobMonitorState.Failed => Translate("failed"),
                    JobMonitorState.Succeeded => Translate("succeeded"),
                    _ => Translate("succeeded"),
                };
            }
            else
            {
                text = Translate("succeeded");
            }
            string finishTime = vm.Job.FinishTime?.ToLocalTime().ToString(" d MMM yyyy HH:mm:ss") ?? "";
            if (JobDetailsFinishTimeTextBlock != null) JobDetailsFinishTimeTextBlock.Text = text + finishTime;

            int outputLength = vm.Job.Monitor.OutputLength;
            if (_selectedOutputJobId != vm.Job.Id)
            {
                _selectedOutputJobId = vm.Job.Id;
                _selectedOutputLength = -1;
            }
            if (_selectedOutputLength != outputLength)
            {
                string output = vm.Job.Monitor.Output;
                if (JobDetailsOutputEditor != null && JobDetailsOutputEditor.Text != output)
                {
                    JobDetailsOutputEditor.Text = output;
                }
                _selectedOutputLength = outputLength;
            }
        }

        // ===== 辅助方法 =====

        // 对照 WPF: (ActivityManagerViewMode)((TabItem)ViewModeTabControl.SelectedItem).Tag
        // spike 版: 解析 TabItem.Tag 字符串
        private ActivityManagerViewMode GetSelectedViewMode()
        {
            if (ViewModeTabControl?.SelectedItem is TabItem tabItem && tabItem.Tag is string tagStr)
            {
                return tagStr switch
                {
                    "All" => ActivityManagerViewMode.All,
                    "User" => ActivityManagerViewMode.User,
                    "Background" => ActivityManagerViewMode.Background,
                    _ => ActivityManagerViewMode.All,
                };
            }
            return ActivityManagerViewMode.All;
        }

        // 对照 WPF: private static string Translate(string text)
        //   WPF: PreferencesLocalization.Translate(text, ForkPlusSettings.Default.UiLanguage)
        //   spike: ServiceLocator.Localization.Translate
        private static string Translate(string text)
        {
            if (string.IsNullOrEmpty(text)) return text;
            if (ServiceLocator.Localization == null) return text;
            try
            {
                return ServiceLocator.Localization.Translate(text, ForkPlusSettings.Default.UiLanguage);
            }
            catch
            {
                return text;
            }
        }
    }
}

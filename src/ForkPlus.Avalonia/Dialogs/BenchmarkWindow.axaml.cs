using System;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Threading;
using ForkPlus.Git;
using ForkPlus.Git.Commands;
using ForkPlus.Jobs;
using ForkPlus.Services;

namespace ForkPlus.Avalonia.Dialogs
{
    // Phase 4.44b：Avalonia 版 BenchmarkWindow（真实迁移版，对照 WPF BenchmarkWindow.xaml.cs 168 行）。
    //
    // 对照 WPF 工程 src/ForkPlus/UI/Dialogs/BenchmarkWindow.xaml.cs：
    //   - public partial class BenchmarkWindow : ForkPlusDialogWindow
    //   - 字段: RepositoryUserControl _repositoryUserControl / Job _activeJob / string _benchmarkLog
    //   - 构造函数：ShowLogo=false / ShowFooter=false / ShowHeader=false / MeasurementsContainer.Hide()
    //   - StartButton_Click → StartButton.Collapse() + MeasurementsContainer.Show() + Refresh()
    //   - RefreshButton_Click → Refresh()
    //   - CopyResultButton_Click → ServiceLocator.Clipboard.SetText(_benchmarkLog)
    //   - Refresh():
    //     * _activeJob?.Monitor.Cancel()
    //     * _repositoryUserControl.JobQueue.Add("Benchmark", delegate(JobMonitor monitor) {
    //         GitCommandResult<BenchmarkResult> response = new BenchmarkGitCommand().Execute(gitModule, monitor, callback)
    //       })
    //   - RefreshControls(monitor, benchmark): 更新各 TextBlock + ProgressBar + ScoreTextBlock + ScoreProgressBar
    //   - GetElapsedBrush / GetScoreBrush: < low → Green / < high → Yellow / >= → Red
    //   - OnClosing: _activeJob?.Monitor.Cancel()
    //
    // Avalonia 版差异：
    //   1. spike 模式：构造函数 SetFooter/Footer 注入（保留 Footer 显示 Close 按钮便于关闭）
    //   2. RepositoryUserControl 参数 → 注入 GitModule + Action<GitCommandResult>? onCompleted 回调
    //   3. JobQueue + Dispatcher.Async → Task.Run + Dispatcher.UIThread.Post + JobMonitor
    //   4. Image.Show()/Hide() → IsVisible = true/false
    //   5. Collapse()/Show() → IsVisible = false/true
    //   6. Theme.ApplicationColors.GreenBrush / YellowBrush / RedBrush → spike 用 Avalonia.Media.Brushes 静态属性
    //   7. ProgressBar Value 用 double? 转 double（GetValueOrDefault）
    //   8. OnClosing → Avalonia Closed 事件（取消 _activeJob.Monitor.Cancel）
    //   9. spike 版省略 ErrorWindow 弹窗（用 SetStatus Error 显示错误信息）
    public partial class BenchmarkWindow : global::ForkPlus.Avalonia.Dialogs.ForkPlusDialogWindow
    {
        private readonly GitModule _gitModule;
        private readonly Action<GitCommandResult>? _onCompleted;

        // 对照 WPF: private Job _activeJob;
        private volatile JobMonitor _activeMonitor;

        // 对照 WPF: private string _benchmarkLog;
        private string _benchmarkLog;

        public BenchmarkWindow(GitModule gitModule, Action<GitCommandResult>? onCompleted = null)
        {
            ShowFooter = true;
            InitializeComponent();
            SetFooter(Footer);
            SetTitleTextBlock(TitleTextBlock);
            SetDescriptionTextBlock(DescriptionTextBlock);

            _gitModule = gitModule ?? throw new ArgumentNullException(nameof(gitModule));
            _onCompleted = onCompleted;
            _benchmarkLog = "";

            // 对照 WPF: ShowLogo=false / ShowHeader=false
            // spike 版用基类 ShowFooter=true（提供 Close 按钮便于关闭）
            string title = Translate("Performance Benchmark");
            Title = title;
            DialogTitle = title;
            DialogDescription = Translate("Measure repository performance.");
            ShowSubmitButton = false;
            CancelButtonTitle = Translate("Close");

            // 对照 WPF: MeasurementsContainer.Hide();
            MeasurementsContainer.IsVisible = false;

            // 对照 WPF: OnClosing → _activeJob?.Monitor.Cancel()
            Closed += BenchmarkWindow_Closed;
        }

        // 对照 WPF: private void CopyResultButton_Click
        public void CopyReportButton_Click(object? sender, RoutedEventArgs e)
        {
            ServiceLocator.Clipboard?.SetText(_benchmarkLog ?? "");
        }

        // 对照 WPF: private void StartButton_Click
        public void StartButton_Click(object? sender, RoutedEventArgs e)
        {
            StartButton.IsVisible = false;
            MeasurementsContainer.IsVisible = true;
            Refresh();
        }

        // 对照 WPF: private void RefreshButton_Click
        public void RefreshButton_Click(object? sender, RoutedEventArgs e)
        {
            Refresh();
        }

        // 对照 WPF: private void Refresh()
        private void Refresh()
        {
            _activeMonitor?.Cancel();
            RefreshControls(null, null);

            GitModule gitModule = _gitModule;
            if (gitModule == null)
            {
                return;
            }

            JobMonitor monitor = new JobMonitor();
            _activeMonitor = monitor;

            // 对照 WPF: _repositoryUserControl.JobQueue.Add(Translate("Benchmark"), delegate(JobMonitor monitor) {
            //     BenchmarkGitCommand().Execute(gitModule, monitor, callback) → Dispatcher.Async(RefreshControls)
            //   })
            // Avalonia: Task.Run + Dispatcher.UIThread.Post + JobMonitor
            Task.Run(delegate
            {
                Action<BenchmarkResult> callback = benchmark =>
                {
                    Dispatcher.UIThread.Post(delegate
                    {
                        RefreshControls(monitor, benchmark);
                    });
                };
                GitCommandResult<BenchmarkResult> response = new BenchmarkGitCommand().Execute(gitModule, monitor, callback);
                Dispatcher.UIThread.Post(delegate
                {
                    if (response.Succeeded)
                    {
                        _benchmarkLog = response.Result.BenchmarkLog ?? "";
                        ProgressBar.IsVisible = false;
                        IBrush scoreBrush = GetScoreBrush(response.Result.Score, 20.0, 60.0);
                        ScoreTextBlock.Foreground = scoreBrush;
                        ScoreTextBlock.Text = string.Format("{0:0.0}", response.Result.Score);
                        ScoreProgressBar.IsVisible = true;
                        ScoreProgressBar.Value = response.Result.Score.GetValueOrDefault();
                        ScoreProgressBar.Foreground = scoreBrush;
                        RefreshButton.IsVisible = true;
                        CopyReportButton.IsVisible = true;
                        SetStatus(ForkPlusDialogStatus.Success, string.Format(FormatTranslate("Score: {0:0.0}"), response.Result.Score));
                    }
                    else if (!monitor.IsCanceled)
                    {
                        SetStatus(ForkPlusDialogStatus.Error, response.Error?.FriendlyDescription ?? Translate("Benchmark failed"));
                    }
                    try
                    {
                        _onCompleted?.Invoke(response.ToGitCommandResult());
                    }
                    catch (Exception ex)
                    {
                        Log.Error("BenchmarkWindow onCompleted callback failed", ex);
                    }
                });
            });
        }

        // 对照 WPF: private void RefreshControls(JobMonitor monitor, BenchmarkResult benchmark)
        private void RefreshControls(JobMonitor monitor, BenchmarkResult benchmark)
        {
            SystemLatencyTextBlock.Foreground = GetElapsedBrush(benchmark?.SystemLatency, 70.0, 100.0);
            SystemLatencyTextBlock.Text = (benchmark != null && benchmark.SystemLatency.HasValue)
                ? string.Format("{0:0.000}s", benchmark.SystemLatency)
                : "--";
            StatusTextBlock.Foreground = GetElapsedBrush(benchmark?.Status, 0.3, 0.6);
            StatusTextBlock.Text = (benchmark != null && benchmark.Status.HasValue)
                ? string.Format("{0:0.000}s", benchmark.Status)
                : "--";
            ReferencesTextBlock.Foreground = GetElapsedBrush(benchmark?.References, 0.25, 0.5);
            ReferencesTextBlock.Text = (benchmark != null && benchmark.References.HasValue)
                ? string.Format("{0:0.000}s", benchmark.References)
                : "--";
            RevisionsTextBlock.Foreground = GetElapsedBrush(benchmark?.Revisions, 0.3, 0.6);
            RevisionsTextBlock.Text = (benchmark != null && benchmark.Revisions.HasValue)
                ? string.Format("{0:0.000}s", benchmark.Revisions)
                : "--";
            ProgressBar.IsVisible = true;
            ProgressBar.Value = monitor?.Progress ?? 0;
            RefreshButton.IsVisible = false;
            CopyReportButton.IsVisible = false;
            ScoreProgressBar.IsVisible = false;
            ScoreTextBlock.Text = "--";
            ScoreTextBlock.Foreground = GetElapsedBrush(null, 0, 0);
        }

        // 对照 WPF: private static Brush GetElapsedBrush(double? value, double low, double high)
        // spike 版：用 Avalonia.Media.Brushes 静态属性替代 Theme.ApplicationColors.GreenBrush 等
        private static IBrush GetElapsedBrush(double? value, double low, double high)
        {
            if (value.HasValue)
            {
                double v = value.GetValueOrDefault();
                if (v < low) return Brushes.Green;
                if (v < high) return Brushes.Orange;
                return Brushes.Red;
            }
            return Brushes.Gray;
        }

        // 对照 WPF: private static Brush GetScoreBrush(double? value, double low, double high)
        // spike 版：Score 越高越好（与 Elapsed 相反）
        private static IBrush GetScoreBrush(double? value, double low, double high)
        {
            if (value.HasValue)
            {
                double v = value.GetValueOrDefault();
                if (v < low) return Brushes.Red;
                if (v < high) return Brushes.Orange;
                return Brushes.Green;
            }
            return Brushes.Gray;
        }

        // 对照 WPF: protected override void OnClosing(CancelEventArgs e) → Avalonia Closed 事件
        private void BenchmarkWindow_Closed(object? sender, EventArgs e)
        {
            _activeMonitor?.Cancel();
            _activeMonitor = null;
        }

        // 对照 WPF: PreferencesLocalization.Translate(text, ForkPlusSettings.Default.UiLanguage)
        private static string Translate(string text)
        {
            var localization = ServiceLocator.Localization;
            var userSettings = ServiceLocator.UserSettings;
            if (localization != null && userSettings != null)
            {
                return localization.Translate(text, userSettings.UiLanguage);
            }
            return text;
        }

        // 对照 WPF: PreferencesLocalization.FormatCurrent(text, args)
        private static string FormatTranslate(string text, params object[] args)
        {
            var localization = ServiceLocator.Localization;
            var userSettings = ServiceLocator.UserSettings;
            if (localization != null && userSettings != null)
            {
                return localization.FormatCurrent(text, args);
            }
            return string.Format(text, args);
        }
    }
}

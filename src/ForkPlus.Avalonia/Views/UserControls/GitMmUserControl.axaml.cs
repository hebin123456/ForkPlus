using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Markup.Xaml;
using Avalonia.Media;
using Avalonia.Threading;
using Avalonia.VisualTree;
using ForkPlus.Git;
using ForkPlus.Git.Commands;
using ForkPlus.Git.Interaction;
using ForkPlus.Jobs;
using ForkPlus.Services;
using ForkPlus.Settings;
using Microsoft.Extensions.DependencyInjection;

namespace ForkPlus.Avalonia.Views.UserControls
{
    // Phase 5.3：Avalonia 版 GitMmUserControl（完整业务逻辑迁移 spike 版）。
    //
    // 对照 WPF 工程 src/ForkPlus/UI/UserControls/GitMmUserControl.xaml.cs（2787 行）+
    //              src/ForkPlus/UI/UserControls/GitMmUserControl.Output.cs（471 行）：
    //
    // 公共 API（全部迁移）：
    //   - 静态方法：IsGitMmWorkspace / FindAncestorGitMmWorkspace / CountSubrepos
    //   - 实例方法：Refresh / Save / ApplyLocalization / ContainsSubrepoPath
    //   - 实例方法：OpenWorkspace（spike 新增，替代 WPF 构造函数 workspacePath 参数）
    //   - 公共属性：JobQueue / WorkspacePath / WorkspaceTitle / ActiveRepositoryUserControl /
    //     SelectedSubrepoTitle / StagedDiffSummary
    //
    // 核心业务逻辑（全部迁移）：
    //   - RunGitMm：执行 git mm 命令 + 输出捕获 + 命令历史 + upload links 提取
    //   - RefreshSubrepos：扫描子仓 + 创建 TabItem + 运行时状态刷新
    //   - ScanSubrepos：文件系统递归 + .gitmodules 解析 + .mm/projects 遍历
    //   - GetSubrepoRuntimeState：git status -b --porcelain -z 解析（分支/ahead/behind/冲突/变更）
    //   - ParseBranchHeader：解析 `## branch...origin/branch [ahead N, behind M]` 头部
    //   - GetDefaultBranch：symbolic-ref refs/remotes/origin/HEAD（带缓存）
    //   - GetStagedDiffStats：diff --cached --numstat 统计
    //   - Settings 管理：Save/Restore（workspaces/activeSubrepo/visibleSubrepos/subrepoOrders/
    //     commandOutputCollapsed/Height/commandHistory/uploadLinks/dialogOptions）
    //   - 子仓过滤：summary filter（conflicts/nonDefault/ahead/behind/loaded/hidden）
    //   - 输出处理：AppendOutput/ClearOutput/FlushOutput + ANSI 转义剥离 + URL 提取
    //   - MigrateRuntimeState：子仓重扫后旧实例运行态迁移到新实例
    //
    // Avalonia 版差异（spike 简化策略）：
    //   1. 构造函数签名 (IServiceProvider) 替代 WPF (string workspacePath)，
    //      新增 OpenWorkspace(string) 方法延迟设置工作区路径
    //   2. Dispatcher.Async/BeginInvoke/Invoke → Dispatcher.UIThread.Post/InvokeAsync
    //   3. Visibility.Collapsed/Visible → IsVisible = false/true
    //   4. PreferencesLocalization → ServiceLocator.Localization
    //   5. MainWindow.Instance → 注入回调 OnOpenStandaloneRepository(Action<string>)
    //   6. NotificationCenter.Current.RaiseActiveTabChanged → 跳过（spike 无 NotificationCenter）
    //   7. RichTextBox（FlowDocument + ANSI 颜色 + Hyperlink）→ TextBox（纯文本 + ANSI 剥离）
    //   8. ModernTabControl 自定义样式 → 原生 TabControl
    //   9. TabItem 自定义 Header（DockPanel + Ellipse + EditableTextBlock）→ 简化 TextBlock Header
    //   10. TabItem 拖拽排序 → 跳过（Avalonia DnD 复杂，spike 不迁移）
    //   11. Popup 子仓过滤器 → 简化（直接调 SubrepoFilterButton_Click 弹出 ContextMenu）
    //   12. PNG 图标 → emoji
    //   13. EditableTextBlock → TextBlock（不支持重命名）
    //   14. RepositoryColorsUserControl → 跳过（颜色选择器）
    //   15. FrameworkElement → Control（Avalonia 基类）
    //   16. WarnIfGitMmUnavailable → 跳过（依赖 App.GitMmPath，WPF-only）
    //   17. NotificationManager.SendWindowsNotification → 跳过（Windows-only toast XML）
    //   18. PerformanceTelemetry.Record → 跳过（spike 不接入遥测）
    //   19. GitMmSubrepoItem 内部类：spike 版含完整运行态属性（Path/Name/IsRootRepository/
    //       IsSubmodule/CommandState/HasLocalChanges/ChangedFilesCount/HasConflicts/
    //       ConflictFilesCount/IsNonDefaultBranch/CurrentBranch/DefaultBranch/AheadCount/
    //       BehindCount/StagedAdded/StagedDeleted/RuntimeStateUpdatedAtUtc/RepositoryControl）
    //       调用 GitMmStartWindow 时转换为 Dialogs.GitMmSubrepoItem stub
    public partial class GitMmUserControl : UserControl
    {
        // ===== 常量（对照 WPF）=====

        private const int SubrepoScanDepth = 4;

        private const double SubrepoTabMinWidth = 140.0;

        private static readonly TimeSpan RuntimeStateCacheTtl = TimeSpan.FromSeconds(60.0);

        private static readonly TimeSpan DefaultBranchCacheTtl = TimeSpan.FromMinutes(30.0);

        private static readonly Dictionary<string, Tuple<string, DateTime>> _defaultBranchCache =
            new Dictionary<string, Tuple<string, DateTime>>(StringComparer.OrdinalIgnoreCase);

        // ===== 输出处理常量（对照 WPF GitMmUserControl.Output.cs）=====

        private const int MaxOutputLineCount = 4000;

        private static readonly Regex UrlRegex = new Regex(@"https?://[^\s<>""']+", RegexOptions.Compiled);

        private static readonly Regex AnsiSgrRegex = new Regex(@"\x1B\[([0-9;]*)m", RegexOptions.Compiled);

        private static readonly Regex AnsiEscapeRegex = new Regex(@"\x1B\[[0-?]*[ -/]*[@-~]", RegexOptions.Compiled);

        // ===== 字段（对照 WPF）=====

        private readonly IServiceProvider _serviceProvider;

        private readonly DelayedAction<object> _saveSettingsAction;

        private readonly JobQueue _jobQueue = new JobQueue();

        private GitMmWorkspaceItem _workspace;

        private HashSet<string> _submoduleSubrepoPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        private bool _restoringSettings;

        private bool _isBusy;

        private double _expandedCommandOutputHeight = 150.0;

        private HashSet<string> _visibleSubrepoPaths;

        private bool _hasPersistedVisibleSubrepoFilter;

        private HashSet<string> _knownSubrepoPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        private readonly Dictionary<string, Button> _summaryButtons =
            new Dictionary<string, Button>(StringComparer.OrdinalIgnoreCase);

        private bool _filterNonDefaultBranchOnly;

        private bool _filterFailedOnly;

        private string _activeSummaryFilterMode;

        private int _runtimeStateRequestId;

        private Job _activeJob;

        private Job _activeStatusRefreshJob;

        // ===== 输出处理字段（对照 WPF Output.cs）=====

        private readonly object _outputLock = new object();

        private readonly List<string> _pendingOutputLines = new List<string>();

        private bool _outputFlushScheduled;

        private string[] _latestUploadLinks = new string[0];

        // ===== spike 注入回调（替代 WPF MainWindow.Instance 依赖）=====

        // 对照 WPF: MainWindow.Instance?.TabManager?.OpenRepository(subrepo.Path)
        // spike: 调用方注入，参数为子仓路径
        public Action<string> OnOpenStandaloneRepository { get; set; }

        // 对照 WPF: MainWindow.Instance?.TabManager.ActiveTab
        // spike: 跳过（NotificationCenter.RaiseActiveTabChanged 在 spike 版不可用）

        // ===== 公共属性（对照 WPF）=====

        public JobQueue JobQueue => _jobQueue;

        public string WorkspacePath => _workspace?.Path;

        public string WorkspaceTitle => "git mm: " +
            (FindRepositoryName(_workspace?.Path) ?? _workspace?.Name ?? "");

        public RepositoryUserControl ActiveRepositoryUserControl =>
            _workspace?.SelectedSubrepo?.RepositoryControl as RepositoryUserControl;

        public string SelectedSubrepoTitle => _workspace?.SelectedSubrepo?.DisplayName;

        public bool IsGitMmEnabled => _workspace != null;

        public string CurrentBranch => _workspace?.SelectedSubrepo?.CurrentBranch ?? "";

        public string GitMmStatus => StatusTextBlock?.Text ?? "";

        public string StagedDiffSummary
        {
            get
            {
                if (_workspace == null) return "";
                int added = _workspace.Subrepos.Sum(s => s.StagedAdded);
                int deleted = _workspace.Subrepos.Sum(s => s.StagedDeleted);
                return added == 0 && deleted == 0 ? "" : $"+{added} -{deleted}";
            }
        }

        // ===== 构造函数（spike: IServiceProvider 签名）=====

        public GitMmUserControl(IServiceProvider serviceProvider)
        {
            _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
            InitializeComponent();

            // _workspace 在 OpenWorkspace 时创建
            _saveSettingsAction = new DelayedAction<object>(_ => SaveSettingsImmediate(), 1.0);
            SetBusy(isBusy: false);
        }

        // ===== OpenWorkspace（spike 新增，替代 WPF 构造函数 workspacePath 参数）=====

        public void OpenWorkspace(string workspacePath)
        {
            if (string.IsNullOrWhiteSpace(workspacePath))
            {
                throw new ArgumentNullException(nameof(workspacePath));
            }

            _workspace?.PropertyChanged -= Workspace_PropertyChanged;
            var newWorkspace = new GitMmWorkspaceItem(workspacePath);
            _workspace = newWorkspace;
            _workspace.PropertyChanged += Workspace_PropertyChanged;

            RefreshCommandButtonTooltips();
            SetBusy(isBusy: false);
            RestoreSettings();
        }

        // 对照 WPF: public void Initialize(RepositoryUserControl repositoryUserControl)
        // spike: 注入父 RepositoryUserControl（用于 Refresh 等）
        public void Initialize(RepositoryUserControl repositoryUserControl)
        {
            // spike: 父 RepositoryUserControl 引用暂存（WPF 中通过 ActiveRepositoryUserControl
            // 从 SelectedSubrepo.RepositoryControl 获取，spike 版可选注入）
        }

        // 对照 WPF: public void ShowGitMmPanel(bool show)
        public void ShowGitMmPanel(bool show)
        {
            IsVisible = show;
        }

        // ===== 静态方法（对照 WPF）=====

        public static bool IsGitMmWorkspace(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                return false;
            }
            return Directory.Exists(System.IO.Path.Combine(path, ".repo"))
                || Directory.Exists(System.IO.Path.Combine(path, ".mm"));
        }

        public static string FindAncestorGitMmWorkspace(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                return null;
            }
            try
            {
                string current = System.IO.Path.GetFullPath(path)
                    .TrimEnd(System.IO.Path.DirectorySeparatorChar, System.IO.Path.AltDirectorySeparatorChar);
                while (!string.IsNullOrEmpty(current))
                {
                    if (IsGitMmWorkspace(current))
                    {
                        return current;
                    }
                    string parent = System.IO.Path.GetDirectoryName(current);
                    if (parent == null || string.Equals(parent, current, StringComparison.OrdinalIgnoreCase))
                    {
                        break;
                    }
                    current = parent;
                }
            }
            catch (Exception ex)
            {
                Log.Error("FindAncestorGitMmWorkspace failed", ex);
            }
            return null;
        }

        public static int CountSubrepos(string workspacePath)
        {
            return ScanSubrepos(workspacePath, SubrepoScanDepth).Count;
        }

        // ===== 公共实例方法（对照 WPF）=====

        public void Refresh()
        {
            if (_isBusy) return;
            RepositoryUserControl ruc = ActiveRepositoryUserControl;
            if (ruc == null) return;
            ruc.InvalidateAndRefresh(SubDomain.DefaultRefresh);
        }

        public void Save()
        {
            SaveSettings();
        }

        public void ApplyLocalization()
        {
            RefreshCommandButtonTooltips();
            RefreshSubreposTitle();
            RefreshSubrepoTabHeaders();
            if (_workspace == null) return;
            foreach (GitMmSubrepoItem subrepo in _workspace.Subrepos)
            {
                if (subrepo.RepositoryControl is RepositoryUserControl ruc)
                {
                    ruc.ApplyLocalization();
                }
            }
        }

        public bool ContainsSubrepoPath(string path)
        {
            if (_workspace == null) return false;
            string normalizedPath = NormalizePath(path);
            if (normalizedPath == null) return false;
            return _workspace.Subrepos.Any(subrepo =>
            {
                string subrepoPath = NormalizePath(subrepo.Path);
                return subrepoPath != null
                    && (string.Equals(subrepoPath, normalizedPath, StringComparison.OrdinalIgnoreCase)
                        || normalizedPath.StartsWith(subrepoPath + System.IO.Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)
                        || normalizedPath.StartsWith(subrepoPath + System.IO.Path.AltDirectorySeparatorChar, StringComparison.OrdinalIgnoreCase));
            });
        }

        // ===== 命令按钮事件处理（对照 WPF）=====

        private void StartButton_Click(object sender, RoutedEventArgs e)
        {
            if (_workspace == null) return;
            SaveSettings();
            if (IsCtrlDown(e))
            {
                RunGitMm(CreateQuickStartArgs());
                return;
            }
            // 对照 WPF: new GitMmStartWindow(_workspace.Subrepos, _workspace.SelectedSubrepo)
            // spike: 转换为 Dialogs.GitMmSubrepoItem stub（Path/Name）
            var dialogSubrepos = _workspace.Subrepos
                .Select(s => new global::ForkPlus.Avalonia.Dialogs.GitMmSubrepoItem(s.Path, s.BaseDisplayName))
                .ToList();
            var selectedStub = _workspace.SelectedSubrepo != null
                ? new global::ForkPlus.Avalonia.Dialogs.GitMmSubrepoItem(
                    _workspace.SelectedSubrepo.Path, _workspace.SelectedSubrepo.BaseDisplayName)
                : null;
            var window = new global::ForkPlus.Avalonia.Dialogs.GitMmStartWindow(dialogSubrepos, selectedStub);
            if (ShowDialog(window) == true)
            {
                RunGitMm(window.StartArgs);
            }
        }

        private void SyncButton_Click(object sender, RoutedEventArgs e)
        {
            if (_workspace == null) return;
            SaveSettings();
            if (IsCtrlDown(e))
            {
                RunGitMm(CreateQuickSyncArgs());
                return;
            }
            var window = new global::ForkPlus.Avalonia.Dialogs.GitMmSyncWindow(_workspace.Path);
            if (ShowDialog(window) == true)
            {
                RunGitMm(window.SyncArgs);
            }
        }

        private void UploadButton_Click(object sender, RoutedEventArgs e)
        {
            if (_workspace == null) return;
            SaveSettings();
            if (IsCtrlDown(e))
            {
                RunGitMm(CreateQuickUploadArgs());
                return;
            }
            var window = new global::ForkPlus.Avalonia.Dialogs.GitMmUploadWindow(_workspace.Path);
            if (ShowDialog(window) == true)
            {
                RunGitMm(window.UploadArgs);
            }
        }

        private void GitMmHelpButton_Click(object sender, RoutedEventArgs e)
        {
            var window = new global::ForkPlus.Avalonia.Dialogs.GitMmReferenceWindow();
            ShowDialog(window);
        }

        // ===== Quick 命令参数（对照 WPF）=====

        private static string[] CreateQuickStartArgs()
        {
            return new string[5] { "start", "develop", "-j", "8", "--all" };
        }

        private static string[] CreateQuickSyncArgs()
        {
            ForkPlusSettings.GitMmSettings settings = ForkPlusSettings.Default.GitMm;
            string checkoutJobs = string.IsNullOrWhiteSpace(settings.SyncJobs) ? "4" : settings.SyncJobs;
            string fetchJobs = settings.GetDialogOption("sync.fetchJobs", "8");
            return new string[5] { "sync", "-J", checkoutJobs, "-j", string.IsNullOrWhiteSpace(fetchJobs) ? "8" : fetchJobs };
        }

        private static string[] CreateQuickUploadArgs()
        {
            return new string[2] { "upload", "-y" };
        }

        // ===== 命令按钮 tooltip（对照 WPF）=====

        private void RefreshCommandButtonTooltips()
        {
            if (StartButton != null) ToolTip.SetTip(StartButton, Translate("Start") + Environment.NewLine + Translate("Hold Ctrl for Quick Start"));
            if (SyncButton != null) ToolTip.SetTip(SyncButton, Translate("Sync") + Environment.NewLine + Translate("Hold Ctrl for Quick Sync"));
            if (UploadButton != null) ToolTip.SetTip(UploadButton, Translate("Upload") + Environment.NewLine + Translate("Hold Ctrl for Quick Upload"));
        }

        // ===== RunGitMm（对照 WPF 核心方法）=====

        private void RunGitMm(string[] args, byte[] stdin = null)
        {
            if (_workspace == null) return;
            string commandText = FormatCommand(args);
            ClearOutput();
            SetStatus(commandText);
            SaveCommandHistory(commandText);
            SetCommandStateForVisibleSubrepos(GitMmSubrepoCommandState.Running);
            RunBackground(commandText, monitor =>
            {
                GitCommand command = new GitCommand("mm");
                command.AddRange(args);
                GitRequest request = default(GitRequest)
                    .CurrentDir(_workspace.Path)
                    .Command(command)
                    .Env(new (string, string)[1] { ("GIT_TERMINAL_PROMPT", "0") });
                GitRequestResult result;
                if (stdin != null)
                {
                    result = request.Stdin(stdin).ExecuteBt(monitor);
                    AppendOutputText(result.Stdout);
                    AppendOutputText(result.Stderr);
                }
                else
                {
                    result = request.ExecuteLong(
                        line => AppendOutput(line),
                        line => AppendOutput(line),
                        monitor);
                }
                Dispatcher.UIThread.Post(() =>
                {
                    AppendOutput("");
                    AppendOutput(string.Format(Translate("Exit code: {0}"), result.ExitCode));
                    if (args.FirstItem() == "upload")
                    {
                        SaveUploadLinks(ExtractUrls(result.FullReadableOutput()));
                    }
                    SetCommandStateForVisibleSubrepos(result.Success
                        ? GitMmSubrepoCommandState.Success
                        : GitMmSubrepoCommandState.Failed);
                    SetStatus(result.Success
                        ? Translate("git mm command finished")
                        : Translate("git mm command finished with errors"));
                    // 对照 WPF: NotificationManager.SendWindowsNotification(...)
                    // spike: 跳过 Windows toast（Windows-only）
                });
                if (!monitor.IsCanceled)
                {
                    if (ShouldRescanSubreposAfterCommand(args))
                    {
                        List<string> paths = ScanSubrepos(_workspace.Path, SubrepoScanDepth, out var submodulePaths);
                        Dispatcher.UIThread.Post(() =>
                        {
                            _submoduleSubrepoPaths = submodulePaths;
                            _workspace.PreferredSubrepoPath = _workspace.SelectedSubrepo?.Path ?? _workspace.PreferredSubrepoPath;
                            List<GitMmSubrepoItem> oldSubrepos = _workspace.Subrepos;
                            _workspace.Subrepos = CreateSubrepoItems(paths, _workspace.Path);
                            MigrateRuntimeState(oldSubrepos, _workspace.Subrepos);
                            EnsureVisibleSubrepos();
                            RebuildSubrepoTabs();
                            RefreshSubreposTitle();
                            RefreshSubrepoRuntimeState();
                            SaveSettings();
                        });
                    }
                    else
                    {
                        Dispatcher.UIThread.Post(RefreshLoadedSubrepoControls);
                        Dispatcher.UIThread.Post(() => RefreshSubrepoRuntimeState());
                    }
                }
            });
        }

        private static bool ShouldRescanSubreposAfterCommand(string[] args)
        {
            return args.FirstItem() == "sync";
        }

        // ===== 命令历史（对照 WPF）=====

        private void SaveCommandHistory(string commandText)
        {
            if (string.IsNullOrWhiteSpace(commandText)) return;
            ForkPlusSettings.GitMmSettings settings = ForkPlusSettings.Default.GitMm;
            string[] commandHistory = new string[1] { commandText }
                .Concat(settings.CommandHistory ?? new string[0])
                .Where(c => !string.IsNullOrWhiteSpace(c))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Take(20)
                .ToArray();
            ForkPlusSettings.Default.GitMm = CreateGitMmSettings(settings, commandHistory: commandHistory);
            ForkPlusSettings.Default.Save();
        }

        // ===== Upload Links（对照 WPF）=====

        private void SaveUploadLinks(string[] links)
        {
            if (links == null || links.Length == 0) return;
            if (_workspace == null) return;
            ForkPlusSettings.GitMmSettings settings = ForkPlusSettings.Default.GitMm;
            Dictionary<string, string[]> uploadLinksByWorkspace =
                new Dictionary<string, string[]>(settings.UploadLinksByWorkspace, StringComparer.OrdinalIgnoreCase);
            Dictionary<string, string> dialogOptions =
                new Dictionary<string, string>(settings.DialogOptions, StringComparer.OrdinalIgnoreCase);
            string[] uploadLinks = links
                .Concat(settings.GetUploadLinks(_workspace.Path))
                .Select(CleanUrl)
                .Where(link => TryCreateHttpUri(link, out _))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Take(20)
                .ToArray();
            uploadLinksByWorkspace[_workspace.Path] = uploadLinks;
            dialogOptions[UploadLinksCollapsedOptionKey()] = "false";
            ForkPlusSettings.Default.GitMm = CreateGitMmSettings(settings,
                uploadLinks: uploadLinks,
                uploadLinksByWorkspace: uploadLinksByWorkspace,
                dialogOptions: dialogOptions);
            ForkPlusSettings.Default.Save();
            RefreshUploadLinksPanel(uploadLinks);
        }

        // ===== RunBackground / 后台任务（对照 WPF）=====

        private void RefreshLoadedSubrepoControls()
        {
            if (_workspace == null) return;
            foreach (GitMmSubrepoItem subrepo in _workspace.Subrepos)
            {
                if (subrepo.RepositoryControl is RepositoryUserControl ruc)
                {
                    ruc.InvalidateAndRefresh(SubDomain.DefaultRefresh);
                }
            }
        }

        private void RunBackground(string title, Action<JobMonitor> action)
        {
            _activeJob?.Monitor.Cancel();
            SetBusy(isBusy: true);
            Job job = null;
            job = _jobQueue.Add(title, monitor =>
            {
                try
                {
                    action(monitor);
                }
                catch (Exception ex)
                {
                    Dispatcher.UIThread.Post(() =>
                    {
                        AppendOutput(ex.ToString());
                        SetStatus(ex.Message);
                    });
                }
                finally
                {
                    Dispatcher.UIThread.Post(() =>
                    {
                        if (_activeJob == job)
                        {
                            _activeJob = null;
                        }
                        SetBusy(isBusy: false);
                    });
                }
            });
            _activeJob = job;
        }

        private void CancelStatusRefresh()
        {
            _activeStatusRefreshJob?.Monitor.Cancel();
            _activeStatusRefreshJob = null;
            _runtimeStateRequestId++;
        }

        private void SetBusy(bool isBusy)
        {
            _isBusy = isBusy;
            if (BusyProgressBar != null) BusyProgressBar.IsVisible = isBusy;
            if (StartButton != null) StartButton.IsEnabled = !isBusy;
            if (SyncButton != null) SyncButton.IsEnabled = !isBusy;
            if (UploadButton != null) UploadButton.IsEnabled = !isBusy;
            if (CancelCommandButton != null) CancelCommandButton.IsVisible = isBusy;
            if (SubreposTabControl != null) SubreposTabControl.IsEnabled = !isBusy;
            if (SubrepoFilterButton != null) SubrepoFilterButton.IsEnabled = !isBusy;
        }

        private void SetStatus(string text)
        {
            if (StatusTextBlock != null)
            {
                StatusTextBlock.Text = text ?? "";
            }
        }

        // ===== 命令输出面板折叠/展开（对照 WPF）=====

        private void CancelCommandButton_Click(object sender, RoutedEventArgs e)
        {
            _activeJob?.Monitor.Cancel();
            SetStatus(Translate("Canceling..."));
        }

        private void ToggleCommandOutputButton_Click(object sender, RoutedEventArgs e)
        {
            if (CommandOutputPanel != null)
            {
                SetCommandOutputCollapsed(CommandOutputPanel.IsVisible, save: true);
            }
        }

        private void SetCommandOutputCollapsed(bool isCollapsed, bool save)
        {
            if (isCollapsed)
            {
                if (RootGrid.RowDefinitions.Count > 2 && RootGrid.RowDefinitions[2].ActualHeight > 0.0)
                {
                    _expandedCommandOutputHeight = RootGrid.RowDefinitions[2].ActualHeight;
                }
                if (CommandOutputPanel != null) CommandOutputPanel.IsVisible = false;
                if (CommandOutputGridSplitter != null) CommandOutputGridSplitter.IsVisible = false;
                if (ExpandCommandOutputButton != null) ExpandCommandOutputButton.IsVisible = true;
                if (RootGrid.RowDefinitions.Count > 1) RootGrid.RowDefinitions[1].Height = new GridLength(0, GridUnitType.Auto);
                if (RootGrid.RowDefinitions.Count > 2) RootGrid.RowDefinitions[2].Height = new GridLength(0);
            }
            else
            {
                if (CommandOutputPanel != null) CommandOutputPanel.IsVisible = true;
                if (CommandOutputGridSplitter != null) CommandOutputGridSplitter.IsVisible = true;
                if (ExpandCommandOutputButton != null) ExpandCommandOutputButton.IsVisible = false;
                if (RootGrid.RowDefinitions.Count > 1) RootGrid.RowDefinitions[1].Height = new GridLength(0, GridUnitType.Auto);
                if (RootGrid.RowDefinitions.Count > 2)
                    RootGrid.RowDefinitions[2].Height = _expandedCommandOutputHeight > 0.0
                        ? new GridLength(_expandedCommandOutputHeight)
                        : new GridLength(150);
            }
            if (save)
            {
                SaveSettings();
            }
        }

        private bool IsCommandOutputCollapsed()
        {
            return CommandOutputPanel == null || !CommandOutputPanel.IsVisible;
        }

        private double CommandOutputHeight()
        {
            if (!IsCommandOutputCollapsed() && RootGrid.RowDefinitions.Count > 2
                && RootGrid.RowDefinitions[2].ActualHeight > 0.0)
            {
                return RootGrid.RowDefinitions[2].ActualHeight;
            }
            return _expandedCommandOutputHeight > 0.0 ? _expandedCommandOutputHeight : 150.0;
        }

        // ===== Settings Save/Restore（对照 WPF）=====

        private void RestoreSettings()
        {
            if (_workspace == null) return;
            _restoringSettings = true;
            try
            {
                ForkPlusSettings.GitMmSettings settings = ForkPlusSettings.Default.GitMm;
                _workspace.PreferredSubrepoPath = settings.GetActiveSubrepo(_workspace.Path);
                _activeSummaryFilterMode = settings.GetDialogOption(SummaryFilterOptionKey(), null);
                if (_activeSummaryFilterMode == "changed")
                {
                    _activeSummaryFilterMode = null;
                }
                string[] visibleSubrepoPaths = settings.GetVisibleSubrepos(_workspace.Path);
                if (visibleSubrepoPaths != null)
                {
                    _visibleSubrepoPaths = new HashSet<string>(
                        visibleSubrepoPaths.Select(NormalizePath).Where(p => !string.IsNullOrWhiteSpace(p)),
                        StringComparer.OrdinalIgnoreCase);
                    _knownSubrepoPaths = new HashSet<string>(_visibleSubrepoPaths, StringComparer.OrdinalIgnoreCase);
                    _hasPersistedVisibleSubrepoFilter = true;
                }
                _expandedCommandOutputHeight = settings.CommandOutputHeight;
                SetCommandOutputCollapsed(settings.CommandOutputCollapsed, save: false);
                RefreshUploadLinksPanel(settings.GetUploadLinks(_workspace.Path), autoHide: false);
                if (UploadLinksCollapsed())
                {
                    HideUploadLinksPanel(save: false);
                }
            }
            finally
            {
                _restoringSettings = false;
            }
            RefreshSubrepos();
        }

        private void SaveSettings()
        {
            if (_restoringSettings) return;
            _saveSettingsAction.InvokeWithDelay(null);
        }

        private void SaveSettingsImmediate()
        {
            if (_restoringSettings || _workspace == null) return;
            string[] workspaces = (ForkPlusSettings.Default.GitMm.Workspaces ?? new string[0])
                .Concat(new string[1] { _workspace.Path })
                .Where(p => !string.IsNullOrWhiteSpace(p))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();
            Dictionary<string, string> activeSubrepos =
                new Dictionary<string, string>(ForkPlusSettings.Default.GitMm.ActiveSubrepos, StringComparer.OrdinalIgnoreCase);
            if (_workspace.SelectedSubrepo?.Path != null)
            {
                activeSubrepos[_workspace.Path] = _workspace.SelectedSubrepo.Path;
            }
            Dictionary<string, string[]> subrepoOrders =
                new Dictionary<string, string[]>(ForkPlusSettings.Default.GitMm.SubrepoOrders, StringComparer.OrdinalIgnoreCase);
            if (_workspace.Subrepos.Count > 0)
            {
                subrepoOrders[_workspace.Path] = _workspace.Subrepos.Map(s => s.Path);
            }
            Dictionary<string, string[]> visibleSubrepos =
                new Dictionary<string, string[]>(ForkPlusSettings.Default.GitMm.VisibleSubrepos, StringComparer.OrdinalIgnoreCase);
            if (_visibleSubrepoPaths != null)
            {
                visibleSubrepos[_workspace.Path] = _workspace.Subrepos
                    .Where(IsSubrepoVisible)
                    .Select(s => s.Path)
                    .ToArray();
            }
            ForkPlusSettings.Default.GitMm = new ForkPlusSettings.GitMmSettings(
                workspaces,
                _workspace.Path,
                _workspace.SelectedSubrepo?.Path,
                activeSubrepos,
                subrepoOrders,
                visibleSubrepos,
                IsCommandOutputCollapsed(),
                CommandOutputHeight(),
                ForkPlusSettings.Default.GitMm.CommandHistory,
                ForkPlusSettings.Default.GitMm.UploadLinks,
                ForkPlusSettings.Default.GitMm.UploadLinksByWorkspace,
                ForkPlusSettings.Default.GitMm.SyncJobs,
                ForkPlusSettings.Default.GitMm.StartBranch,
                ForkPlusSettings.Default.GitMm.InitUrl,
                ForkPlusSettings.Default.GitMm.InitManifest,
                ForkPlusSettings.Default.GitMm.InitBranch,
                ForkPlusSettings.Default.GitMm.InitGroup,
                SaveSummaryFilterMode(ForkPlusSettings.Default.GitMm.DialogOptions));
            ForkPlusSettings.Default.Save();
        }

        private Dictionary<string, string> SaveSummaryFilterMode(Dictionary<string, string> existingOptions)
        {
            Dictionary<string, string> dialogOptions =
                new Dictionary<string, string>(existingOptions, StringComparer.OrdinalIgnoreCase);
            string key = SummaryFilterOptionKey();
            if (string.IsNullOrWhiteSpace(_activeSummaryFilterMode))
            {
                dialogOptions.Remove(key);
            }
            else
            {
                dialogOptions[key] = _activeSummaryFilterMode;
            }
            return dialogOptions;
        }

        private string SummaryFilterOptionKey()
        {
            return "summaryFilter:" + (NormalizePath(_workspace?.Path) ?? _workspace?.Path ?? "");
        }

        private bool UploadLinksCollapsed()
        {
            return string.Equals(
                ForkPlusSettings.Default.GitMm.GetDialogOption(UploadLinksCollapsedOptionKey(), "false"),
                "true", StringComparison.OrdinalIgnoreCase);
        }

        private void SaveUploadLinksCollapsed(bool isCollapsed)
        {
            if (_workspace == null) return;
            ForkPlusSettings.GitMmSettings settings = ForkPlusSettings.Default.GitMm;
            Dictionary<string, string> dialogOptions =
                new Dictionary<string, string>(settings.DialogOptions, StringComparer.OrdinalIgnoreCase);
            dialogOptions[UploadLinksCollapsedOptionKey()] = isCollapsed ? "true" : "false";
            ForkPlusSettings.Default.GitMm = CreateGitMmSettings(settings, dialogOptions: dialogOptions);
            ForkPlusSettings.Default.Save();
        }

        private string UploadLinksCollapsedOptionKey()
        {
            return "uploadLinksCollapsed:" + (NormalizePath(_workspace?.Path) ?? _workspace?.Path ?? "");
        }

        // ===== GitMmSettings 构造辅助（避免重复 18 参数构造）=====

        private static ForkPlusSettings.GitMmSettings CreateGitMmSettings(
            ForkPlusSettings.GitMmSettings settings,
            string[] commandHistory = null,
            string[] uploadLinks = null,
            Dictionary<string, string[]> uploadLinksByWorkspace = null,
            Dictionary<string, string> dialogOptions = null)
        {
            return new ForkPlusSettings.GitMmSettings(
                settings.Workspaces,
                settings.ActiveWorkspace,
                settings.ActiveSubrepo,
                settings.ActiveSubrepos,
                settings.SubrepoOrders,
                settings.VisibleSubrepos,
                settings.CommandOutputCollapsed,
                settings.CommandOutputHeight,
                commandHistory ?? settings.CommandHistory,
                uploadLinks ?? settings.UploadLinks,
                uploadLinksByWorkspace ?? settings.UploadLinksByWorkspace,
                settings.SyncJobs,
                settings.StartBranch,
                settings.InitUrl,
                settings.InitManifest,
                settings.InitBranch,
                settings.InitGroup,
                dialogOptions ?? settings.DialogOptions);
        }

        // ===== Workspace 属性变更（对照 WPF）=====

        private void Workspace_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(GitMmWorkspaceItem.SelectedSubrepo))
            {
                SaveSettings();
            }
        }

        // ===== 子仓 Tab 选择（对照 WPF）=====

        private void SubreposTabControl_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_isBusy || _workspace == null) return;
            if (SubreposTabControl.SelectedItem is TabItem tabItem && tabItem.Tag is GitMmSubrepoItem subrepo)
            {
                _workspace.SelectedSubrepo = subrepo;
                EnsureSubrepoContent(tabItem, subrepo);
            }
        }

        // ===== 子仓 Tab 重建（对照 WPF，简化版）=====

        private void RebuildSubrepoTabs()
        {
            if (_workspace == null || SubreposTabControl == null) return;
            SubreposTabControl.Items.Clear();
            TabItem tabToSelect = null;
            string preferredSubrepoPath = GetPreferredSubrepoPath();
            foreach (GitMmSubrepoItem subrepo in _workspace.Subrepos.Where(IsSubrepoVisible))
            {
                TabItem tabItem = new TabItem
                {
                    Header = CreateSubrepoTabHeader(subrepo),
                    Content = CreateSubrepoPlaceholder(subrepo),
                    Tag = subrepo
                };
                ToolTip.SetTip(tabItem, subrepo.Path);
                SubreposTabControl.Items.Add(tabItem);
                if (IsSamePath(subrepo.Path, preferredSubrepoPath))
                {
                    tabToSelect = tabItem;
                }
            }
            if (tabToSelect == null && SubreposTabControl.Items.Count > 0)
            {
                tabToSelect = SubreposTabControl.Items[0] as TabItem;
            }
            if (tabToSelect != null && tabToSelect.Tag is GitMmSubrepoItem selectedSubrepo)
            {
                SubreposTabControl.SelectedItem = tabToSelect;
                _workspace.SelectedSubrepo = selectedSubrepo;
                EnsureSubrepoContent(tabToSelect, selectedSubrepo);
            }
            else
            {
                _workspace.SelectedSubrepo = null;
            }
        }

        private static string CreateSubrepoTabHeader(GitMmSubrepoItem subrepo)
        {
            // spike 简化：WPF 用 DockPanel + Ellipse + EditableTextBlock，spike 用纯文本
            string prefix = subrepo.IsRootRepository ? "★ " : subrepo.IsSubmodule ? "  " : "  ";
            string status = "";
            if (subrepo.HasConflicts) status += " ⚠";
            else if (subrepo.HasLocalChanges) status += " 📝";
            if (subrepo.CommandState == GitMmSubrepoCommandState.Running) status += " 🔄";
            else if (subrepo.CommandState == GitMmSubrepoCommandState.Success) status += " ✓";
            else if (subrepo.CommandState == GitMmSubrepoCommandState.Failed) status += " ✗";
            if (subrepo.AheadCount > 0) status += $" ↑{subrepo.AheadCount}";
            if (subrepo.BehindCount > 0) status += $" ↓{subrepo.BehindCount}";
            return prefix + subrepo.DisplayName + status;
        }

        private void EnsureSubrepoContent(TabItem tabItem, GitMmSubrepoItem subrepo)
        {
            if (subrepo.RepositoryControl == null)
            {
                subrepo.RepositoryControl = CreateRepositoryContent(subrepo.Path);
            }
            if (tabItem.Content != subrepo.RepositoryControl)
            {
                tabItem.Content = subrepo.RepositoryControl;
            }
            RefreshSubrepoSummary();
        }

        private static Control CreateSubrepoPlaceholder(GitMmSubrepoItem subrepo)
        {
            Application.Current.TryFindResource("ThemeForegroundBrush", out var brush);
            return new TextBlock
            {
                Text = subrepo.DisplayName,
                Margin = new Thickness(10),
                Foreground = brush as IBrush
            };
        }

        // ===== 子仓可见性管理（对照 WPF）=====

        private void EnsureVisibleSubrepos()
        {
            if (_workspace == null) return;
            HashSet<string> currentPaths = new HashSet<string>(
                _workspace.Subrepos.Select(s => NormalizePath(s.Path)).Where(p => !string.IsNullOrWhiteSpace(p)),
                StringComparer.OrdinalIgnoreCase);
            HashSet<string> visibleSubrepoPaths = _visibleSubrepoPaths;
            if (visibleSubrepoPaths == null)
            {
                _visibleSubrepoPaths = new HashSet<string>(currentPaths, StringComparer.OrdinalIgnoreCase);
                _knownSubrepoPaths = currentPaths;
                return;
            }
            visibleSubrepoPaths.RemoveWhere(p => !currentPaths.Contains(p));
            if (!_hasPersistedVisibleSubrepoFilter)
            {
                foreach (string path in currentPaths)
                {
                    if (!_knownSubrepoPaths.Contains(path))
                    {
                        visibleSubrepoPaths.Add(path);
                    }
                }
            }
            _knownSubrepoPaths = currentPaths;
        }

        private bool IsSubrepoVisible(GitMmSubrepoItem subrepo)
        {
            string path = NormalizePath(subrepo.Path);
            return path != null && (_visibleSubrepoPaths == null || _visibleSubrepoPaths.Contains(path));
        }

        private void SetSubrepoVisible(GitMmSubrepoItem subrepo, bool isVisible)
        {
            EnsureVisibleSubrepos();
            string path = NormalizePath(subrepo.Path);
            if (path == null) return;
            if (isVisible)
            {
                _visibleSubrepoPaths.Add(path);
            }
            else
            {
                _visibleSubrepoPaths.Remove(path);
            }
            _hasPersistedVisibleSubrepoFilter = true;
        }

        // ===== 子仓过滤按钮（对照 WPF，简化版）=====

        private void SubrepoFilterButton_Click(object sender, RoutedEventArgs e)
        {
            if (_workspace == null) return;
            EnsureVisibleSubrepos();
            // spike 简化：用 ContextMenu 替代 WPF 的 Popup + TextBox + CheckBox 列表
            var contextMenu = new ContextMenu();
            // Show all
            var showAllItem = new MenuItem { Header = Translate("Show all repositories") };
            showAllItem.Click += (_, _) =>
            {
                _activeSummaryFilterMode = null;
                EnsureVisibleSubrepos();
                foreach (GitMmSubrepoItem subrepo in _workspace.Subrepos)
                {
                    SetSubrepoVisible(subrepo, true);
                }
                RebuildSubrepoTabs();
                RefreshSubreposTitle();
                SaveSettings();
            };
            contextMenu.Items.Add(showAllItem);
            contextMenu.Items.Add(new Separator());
            // Invert selection
            var invertItem = new MenuItem { Header = Translate("Invert repository selection") };
            invertItem.Click += (_, _) =>
            {
                _activeSummaryFilterMode = null;
                EnsureVisibleSubrepos();
                foreach (GitMmSubrepoItem subrepo in _workspace.Subrepos)
                {
                    SetSubrepoVisible(subrepo, !IsSubrepoVisible(subrepo));
                }
                RebuildSubrepoTabs();
                RefreshSubreposTitle();
                SaveSettings();
            };
            contextMenu.Items.Add(invertItem);
            contextMenu.Items.Add(new Separator());
            // Quick filters
            var nonDefaultItem = new MenuItem
            {
                Header = Translate("Non-default branch"),
                ToggleType = MenuItemToggleType.CheckBox,
                IsChecked = _filterNonDefaultBranchOnly
            };
            nonDefaultItem.Click += (_, _) =>
            {
                _filterNonDefaultBranchOnly = !_filterNonDefaultBranchOnly;
                if (_filterNonDefaultBranchOnly)
                {
                    _activeSummaryFilterMode = "nonDefault";
                    TryApplySummaryFilterMode("nonDefault", save: true);
                }
                else
                {
                    if (_activeSummaryFilterMode == "nonDefault") _activeSummaryFilterMode = null;
                    SaveSettings();
                }
            };
            contextMenu.Items.Add(nonDefaultItem);
            var failedItem = new MenuItem
            {
                Header = Translate("Failed repositories"),
                ToggleType = MenuItemToggleType.CheckBox,
                IsChecked = _filterFailedOnly
            };
            failedItem.Click += (_, _) =>
            {
                _filterFailedOnly = !_filterFailedOnly;
                SaveSettings();
            };
            contextMenu.Items.Add(failedItem);
            SubrepoFilterButton.ContextMenu = contextMenu;
            contextMenu.Open();
        }

        // ===== 子仓 Summary 按钮（对照 WPF）=====

        private void RefreshSubrepos()
        {
            if (_workspace == null) return;
            string selectedSubrepoPath = GetPreferredSubrepoPath();
            RunBackground("git mm scan repositories", monitor =>
            {
                List<string> paths = ScanSubrepos(_workspace.Path, SubrepoScanDepth, out var submodulePaths);
                if (monitor.IsCanceled) return;
                Dispatcher.UIThread.Post(() =>
                {
                    if (monitor.IsCanceled) return;
                    _submoduleSubrepoPaths = submodulePaths;
                    _workspace.PreferredSubrepoPath = selectedSubrepoPath;
                    List<GitMmSubrepoItem> oldSubrepos = _workspace.Subrepos;
                    _workspace.Subrepos = CreateSubrepoItems(paths, _workspace.Path);
                    MigrateRuntimeState(oldSubrepos, _workspace.Subrepos);
                    EnsureVisibleSubrepos();
                    RebuildSubrepoTabs();
                    RefreshSubreposTitle();
                    RefreshSubrepoRuntimeState(force: true);
                    SetStatus("");
                    SaveSettings();
                });
            });
        }

        private void RefreshSubreposTitle()
        {
            if (_workspace == null || SubreposTitleTextBlock == null) return;
            SubreposTitleTextBlock.Text = FormatCurrent("{0} repositories", _workspace.Subrepos.Count);
            if (GitMmHelpButton != null) ToolTip.SetTip(GitMmHelpButton, Translate("Show git mm reference"));
            RefreshSubrepoSummary();
            RefreshSubrepoFilterButton();
        }

        private void RefreshSubrepoSummary()
        {
            if (_workspace == null || SubrepoSummaryPanel == null) return;
            int totalCount = _workspace.Subrepos.Count;
            int visibleCount = 0;
            int loadedCount = 0;
            int conflictCount = 0;
            int nonDefaultBranchCount = 0;
            int aheadCount = 0;
            int behindCount = 0;
            for (int i = 0; i < totalCount; i++)
            {
                GitMmSubrepoItem subrepo = _workspace.Subrepos[i];
                if (IsSubrepoVisible(subrepo)) visibleCount++;
                if (subrepo.RepositoryControl != null) loadedCount++;
                if (subrepo.HasConflicts) conflictCount++;
                if (subrepo.IsNonDefaultBranch) nonDefaultBranchCount++;
                if (subrepo.AheadCount > 0) aheadCount++;
                if (subrepo.BehindCount > 0) behindCount++;
            }
            int hiddenCount = totalCount - visibleCount;
            HashSet<string> visibleButtonKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            AddClearFilterSummaryButton(hiddenCount, visibleButtonKeys);
            AddSummaryButton("Conflicts: {0}", conflictCount, "conflicts", visibleButtonKeys);
            AddSummaryButton("Non-default: {0}", nonDefaultBranchCount, "nonDefault", visibleButtonKeys);
            AddSummaryButton("Ahead: {0}", aheadCount, "ahead", visibleButtonKeys);
            AddSummaryButton("Behind: {0}", behindCount, "behind", visibleButtonKeys);
            AddSummaryButton("Loaded: {0}", loadedCount, "loaded", visibleButtonKeys);
            AddSummaryButton("Hidden: {0}", hiddenCount, "hidden", visibleButtonKeys);
            foreach (KeyValuePair<string, Button> item in _summaryButtons)
            {
                item.Value.IsVisible = visibleButtonKeys.Contains(item.Key);
            }
        }

        private void AddClearFilterSummaryButton(int hiddenCount, HashSet<string> visibleButtonKeys)
        {
            if (hiddenCount <= 0) return;
            const string key = "clear";
            visibleButtonKeys.Add(key);
            Button button = GetOrCreateSummaryButton(key);
            button.Tag = key;
            button.Content = Translate("Show all");
            button.Margin = new Thickness(0, 0, 8, 0);
            ToolTip.SetTip(button, Translate("Clear repository filter"));
        }

        private void AddSummaryButton(string format, int value, string filterMode, HashSet<string> visibleButtonKeys)
        {
            if (value <= 0) return;
            visibleButtonKeys.Add(filterMode);
            Button button = GetOrCreateSummaryButton(filterMode);
            button.Tag = filterMode;
            button.Content = FormatCurrent(format, value);
            button.Margin = new Thickness(0, 0, 6, 0);
            ToolTip.SetTip(button, Translate("Click to show matching repositories"));
        }

        private Button GetOrCreateSummaryButton(string key)
        {
            if (_summaryButtons.TryGetValue(key, out Button button))
            {
                return button;
            }
            button = new Button
            {
                FontSize = 12.0,
                Padding = new Thickness(3, 0, 3, 0)
            };
            button.Click += SummaryButton_Click;
            _summaryButtons[key] = button;
            SubrepoSummaryPanel.Children.Add(button);
            return button;
        }

        private void SummaryButton_Click(object sender, RoutedEventArgs e)
        {
            string filterMode = (sender as Control)?.Tag as string;
            if (filterMode == "clear")
            {
                ClearSubrepoFilter();
                return;
            }
            if (!string.IsNullOrWhiteSpace(filterMode))
            {
                TryApplySummaryFilterMode(filterMode, save: true);
            }
        }

        private bool ApplySubrepoSummaryFilter(string filterMode, Func<GitMmSubrepoItem, bool> predicate, bool save)
        {
            if (_workspace == null) return false;
            GitMmSubrepoItem[] matchingSubrepos = _workspace.Subrepos.Where(predicate).ToArray();
            if (matchingSubrepos.Length == 0) return false;
            _activeSummaryFilterMode = filterMode;
            _visibleSubrepoPaths = new HashSet<string>(
                matchingSubrepos.Select(s => NormalizePath(s.Path)).Where(p => !string.IsNullOrWhiteSpace(p)),
                StringComparer.OrdinalIgnoreCase);
            _hasPersistedVisibleSubrepoFilter = true;
            RebuildSubrepoTabs();
            RefreshSubreposTitle();
            if (save) SaveSettings();
            return true;
        }

        private bool TryApplySummaryFilterMode(string filterMode, bool save)
        {
            switch (filterMode)
            {
                case "conflicts":
                    return ApplySubrepoSummaryFilter(filterMode, s => s.HasConflicts, save);
                case "nonDefault":
                    return ApplySubrepoSummaryFilter(filterMode, s => s.IsNonDefaultBranch, save);
                case "ahead":
                    return ApplySubrepoSummaryFilter(filterMode, s => s.AheadCount > 0, save);
                case "behind":
                    return ApplySubrepoSummaryFilter(filterMode, s => s.BehindCount > 0, save);
                case "loaded":
                    return ApplySubrepoSummaryFilter(filterMode, s => s.RepositoryControl != null, save);
                case "hidden":
                    return ApplySubrepoSummaryFilter(null, s => !IsSubrepoVisible(s), save);
                default:
                    return false;
            }
        }

        private void ClearSubrepoFilter()
        {
            if (_workspace == null) return;
            EnsureVisibleSubrepos();
            foreach (GitMmSubrepoItem subrepo in _workspace.Subrepos)
            {
                SetSubrepoVisible(subrepo, true);
            }
            _filterNonDefaultBranchOnly = false;
            _filterFailedOnly = false;
            _activeSummaryFilterMode = null;
            RebuildSubrepoTabs();
            RefreshSubreposTitle();
            SaveSettings();
        }

        private void RefreshSubrepoFilterButton()
        {
            if (_workspace == null || SubrepoFilterButton == null) return;
            int totalCount = _workspace.Subrepos.Count;
            int visibleCount = _workspace.Subrepos.Count(IsSubrepoVisible);
            SubrepoFilterButton.Content = FormatCurrent("{0}/{1} shown", visibleCount, totalCount);
        }

        // ===== 命令历史按钮（对照 WPF）=====

        private void CommandHistoryButton_Click(object sender, RoutedEventArgs e)
        {
            var contextMenu = new ContextMenu();
            string[] history = ForkPlusSettings.Default.GitMm.CommandHistory ?? new string[0];
            if (history.Length == 0)
            {
                var emptyItem = new MenuItem
                {
                    Header = Translate("No command history"),
                    IsEnabled = false
                };
                contextMenu.Items.Add(emptyItem);
            }
            foreach (string command in history)
            {
                var item = new MenuItem { Header = command };
                item.Click += (_, _) =>
                {
                    string[] args = ParseCommandHistory(command);
                    if (args.Length > 0)
                    {
                        RunGitMm(args);
                    }
                };
                contextMenu.Items.Add(item);
            }
            CommandHistoryButton.ContextMenu = contextMenu;
            contextMenu.Open();
        }

        private static string[] ParseCommandHistory(string command)
        {
            if (string.IsNullOrWhiteSpace(command)) return new string[0];
            if (command.StartsWith("git mm "))
            {
                command = command.Substring("git mm ".Length);
            }
            List<string> args = new List<string>();
            bool quoted = false;
            var current = new System.Text.StringBuilder();
            for (int i = 0; i < command.Length; i++)
            {
                char c = command[i];
                if (c == '"')
                {
                    quoted = !quoted;
                    continue;
                }
                if (char.IsWhiteSpace(c) && !quoted)
                {
                    if (current.Length > 0)
                    {
                        args.Add(current.ToString());
                        current.Clear();
                    }
                    continue;
                }
                current.Append(c);
            }
            if (current.Length > 0)
            {
                args.Add(current.ToString());
            }
            return args.ToArray();
        }

        // ===== 子仓 Tab Header 刷新（对照 WPF）=====

        private void RefreshSubrepoTabHeaders()
        {
            if (SubreposTabControl == null || _workspace == null) return;
            foreach (TabItem tabItem in SubreposTabControl.Items.OfType<TabItem>())
            {
                if (tabItem.Tag is GitMmSubrepoItem subrepo)
                {
                    tabItem.Header = CreateSubrepoTabHeader(subrepo);
                }
            }
        }

        private void SetCommandStateForVisibleSubrepos(GitMmSubrepoCommandState commandState)
        {
            if (_workspace == null) return;
            foreach (GitMmSubrepoItem subrepo in _workspace.Subrepos.Where(IsSubrepoVisible))
            {
                subrepo.CommandState = commandState;
            }
            RefreshSubrepoTabHeaders();
        }

        // ===== 子仓运行时状态刷新（对照 WPF）=====

        private void RefreshSubrepoRuntimeState(bool force = false)
        {
            if (_workspace == null) return;
            CancelStatusRefresh();
            int requestId = ++_runtimeStateRequestId;
            GitMmSubrepoItem selectedSubrepo = _workspace.SelectedSubrepo;
            GitMmSubrepoItem[] subrepos = _workspace.Subrepos
                .OrderBy(s => selectedSubrepo == null || !IsSamePath(s.Path, selectedSubrepo.Path))
                .ThenBy(s => s.RepositoryControl == null)
                .ThenBy(s => !IsSubrepoVisible(s))
                .ToArray();
            Job job = null;
            job = _jobQueue.Add("git mm status refresh", monitor =>
            {
                Stopwatch stopwatch = Stopwatch.StartNew();
                GitMmSubrepoRuntimeState[] states = new GitMmSubrepoRuntimeState[subrepos.Length];
                try
                {
                    int total = subrepos.Length;
                    if (total > 0)
                    {
                        int completed = 0;
                        object progressLock = new object();
                        Parallel.For(0, total, new ParallelOptions { MaxDegreeOfParallelism = Math.Min(4, total) }, i =>
                        {
                            if (monitor.IsCanceled) return;
                            if (!force && subrepos[i].RuntimeStateUpdatedAtUtc.HasValue
                                && DateTime.UtcNow - subrepos[i].RuntimeStateUpdatedAtUtc.Value < RuntimeStateCacheTtl)
                            {
                                return;
                            }
                            states[i] = GetSubrepoRuntimeState(subrepos[i], monitor);
                            lock (progressLock)
                            {
                                completed++;
                                monitor.Update(completed * 100.0 / total,
                                    FormatCurrent("Refreshing {0}", subrepos[i].DisplayName));
                            }
                        });
                    }
                }
                finally
                {
                    // 对照 WPF: PerformanceTelemetry.Record(...)
                    // spike: 跳过遥测
                }
                if (monitor.IsCanceled) return;
                monitor.Success(Translate("git mm status refresh finished"));
                Dispatcher.UIThread.Post(() =>
                {
                    if (_activeStatusRefreshJob == job)
                    {
                        _activeStatusRefreshJob = null;
                    }
                    if (monitor.IsCanceled || requestId != _runtimeStateRequestId) return;
                    for (int i = 0; i < subrepos.Length && i < states.Length; i++)
                    {
                        if (states[i] == null) continue;
                        subrepos[i].HasLocalChanges = states[i].HasLocalChanges;
                        subrepos[i].ChangedFilesCount = states[i].ChangedFilesCount;
                        subrepos[i].HasConflicts = states[i].HasConflicts;
                        subrepos[i].ConflictFilesCount = states[i].ConflictFilesCount;
                        subrepos[i].IsNonDefaultBranch = states[i].IsNonDefaultBranch;
                        subrepos[i].CurrentBranch = states[i].CurrentBranch;
                        subrepos[i].DefaultBranch = states[i].DefaultBranch;
                        subrepos[i].AheadCount = states[i].AheadCount;
                        subrepos[i].BehindCount = states[i].BehindCount;
                        subrepos[i].StagedAdded = states[i].StagedAdded;
                        subrepos[i].StagedDeleted = states[i].StagedDeleted;
                        subrepos[i].RuntimeStateUpdatedAtUtc = DateTime.UtcNow;
                    }
                    if (!string.IsNullOrWhiteSpace(_activeSummaryFilterMode)
                        && TryApplySummaryFilterMode(_activeSummaryFilterMode, save: false))
                    {
                        return;
                    }
                    RefreshSubrepoTabHeaders();
                    RefreshSubrepoSummary();
                });
            }, JobFlags.SaveToLog | JobFlags.Background, showMessageWhenDone: false);
            _activeStatusRefreshJob = job;
        }

        // ===== 子仓运行时状态获取（对照 WPF）=====

        private static GitMmSubrepoRuntimeState GetSubrepoRuntimeState(GitMmSubrepoItem subrepo, JobMonitor monitor)
        {
            GitMmSubrepoRuntimeState state = new GitMmSubrepoRuntimeState();
            GitRequestResult statusResult = RunGit(subrepo.Path, new GitCommand(
                "-c", "core.fsmonitor=false",
                "-c", "core.untrackedCache=false",
                "-c", "core.checkStat=default",
                "--no-optional-locks", "status", "-b", "--porcelain", "-z", "--untracked-files=all"), monitor);
            if (!statusResult.Success)
            {
                Log.Warn($"git mm subrepo status failed: path={subrepo.Path}, exitCode={statusResult.ExitCode}, stderr={statusResult.Stderr}");
                return null;
            }
            ParseBranchHeader(statusResult.Stdout, out string currentBranch, out int ahead, out int behind, out string porcelainBody);
            state.CurrentBranch = currentBranch;
            state.AheadCount = ahead;
            state.BehindCount = behind;
            state.ConflictFilesCount = CountConflicts(porcelainBody);
            state.HasConflicts = state.ConflictFilesCount > 0;
            state.ChangedFilesCount = CountPorcelainChangedFiles(porcelainBody);
            state.HasLocalChanges = state.ChangedFilesCount > 0;
            if (monitor.IsCanceled) return state;
            state.DefaultBranch = GetDefaultBranch(subrepo.Path, monitor);
            state.IsNonDefaultBranch = !string.IsNullOrWhiteSpace(state.CurrentBranch)
                && !string.IsNullOrWhiteSpace(state.DefaultBranch)
                && !string.Equals(state.CurrentBranch, state.DefaultBranch, StringComparison.OrdinalIgnoreCase);
            (int added, int deleted)? stagedStats = GetStagedDiffStats(subrepo.Path, monitor);
            if (stagedStats.HasValue)
            {
                state.StagedAdded = stagedStats.Value.added;
                state.StagedDeleted = stagedStats.Value.deleted;
            }
            return state;
        }

        private static void ParseBranchHeader(string statusOutput, out string currentBranch, out int ahead, out int behind, out string porcelainBody)
        {
            currentBranch = "";
            ahead = 0;
            behind = 0;
            if (string.IsNullOrEmpty(statusOutput))
            {
                porcelainBody = "";
                return;
            }
            int firstNul = statusOutput.IndexOf('\0');
            string header = firstNul < 0 ? statusOutput : statusOutput.Substring(0, firstNul);
            porcelainBody = firstNul < 0 ? "" : statusOutput.Substring(firstNul + 1);
            if (!header.StartsWith("## ")) return;
            string info = header.Substring(3);
            if (info.StartsWith("HEAD") && info.Contains("(no branch)"))
            {
                currentBranch = "";
            }
            else if (info.StartsWith("No commits yet on "))
            {
                currentBranch = info.Substring("No commits yet on ".Length).Trim();
            }
            else
            {
                int branchEnd = info.Length;
                int dotIdx = info.IndexOf("...");
                int spaceIdx = info.IndexOf(' ');
                if (dotIdx >= 0 && (spaceIdx < 0 || dotIdx < spaceIdx))
                {
                    branchEnd = dotIdx;
                }
                else if (spaceIdx >= 0)
                {
                    branchEnd = spaceIdx;
                }
                currentBranch = info.Substring(0, branchEnd);
            }
            int bracketIdx = info.IndexOf('[');
            if (bracketIdx >= 0)
            {
                int closeIdx = info.IndexOf(']', bracketIdx);
                if (closeIdx > bracketIdx)
                {
                    string bracket = info.Substring(bracketIdx + 1, closeIdx - bracketIdx - 1);
                    foreach (string part in bracket.Split(','))
                    {
                        string trimmed = part.Trim();
                        if (trimmed.StartsWith("ahead ") && int.TryParse(trimmed.Substring(6), out int a))
                        {
                            ahead = a;
                        }
                        else if (trimmed.StartsWith("behind ") && int.TryParse(trimmed.Substring(7), out int b))
                        {
                            behind = b;
                        }
                    }
                }
            }
        }

        private static int CountPorcelainChangedFiles(string porcelainStatus)
        {
            int count = 0;
            foreach (string entry in porcelainStatus.Split('\0'))
            {
                if (entry.Length >= 3) count++;
            }
            return count;
        }

        private static int CountConflicts(string porcelainStatus)
        {
            int count = 0;
            foreach (string entry in porcelainStatus.Split('\0'))
            {
                if (entry.Length < 2) continue;
                string code = entry.Substring(0, 2);
                if (code.IndexOf('U') >= 0 || code == "AA" || code == "DD") count++;
            }
            return count;
        }

        private static GitRequestResult RunGit(string path, GitCommand command, JobMonitor monitor = null)
        {
            GitRequest request = default(GitRequest)
                .CurrentDir(path)
                .Command(command);
            return monitor == null ? request.Execute(silent: true) : request.Execute(monitor, silent: true, appendOutput: false);
        }

        private static string GetDefaultBranch(string path, JobMonitor monitor = null)
        {
            string normalizedPath = NormalizePath(path);
            if (normalizedPath != null && _defaultBranchCache.TryGetValue(normalizedPath, out Tuple<string, DateTime> cached))
            {
                if (DateTime.UtcNow - cached.Item2 < DefaultBranchCacheTtl)
                {
                    return cached.Item1;
                }
                _defaultBranchCache.Remove(normalizedPath);
            }
            GitRequestResult result = RunGit(path, new GitCommand("symbolic-ref", "--short", "refs/remotes/origin/HEAD"), monitor);
            if (result.Success)
            {
                string value = result.Stdout.Trim();
                const string originPrefix = "origin/";
                if (value.StartsWith(originPrefix, StringComparison.OrdinalIgnoreCase))
                {
                    value = value.Substring(originPrefix.Length);
                }
                if (!string.IsNullOrWhiteSpace(value))
                {
                    if (normalizedPath != null)
                    {
                        _defaultBranchCache[normalizedPath] = Tuple.Create(value, DateTime.UtcNow);
                    }
                    return value;
                }
            }
            return "master";
        }

        private static (int added, int deleted)? GetStagedDiffStats(string path, JobMonitor monitor = null)
        {
            GitRequestResult result = RunGit(path, new GitCommand("diff", "--cached", "--numstat"), monitor);
            if (!result.Success) return null;
            int added = 0;
            int deleted = 0;
            foreach (string line in result.Stdout.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n'))
            {
                if (string.IsNullOrWhiteSpace(line)) continue;
                string[] parts = line.Split('\t');
                if (parts.Length < 2) continue;
                if (int.TryParse(parts[0], out int fileAdded)) added += fileAdded;
                if (int.TryParse(parts[1], out int fileDeleted)) deleted += fileDeleted;
            }
            return (added, deleted);
        }

        // ===== 子仓创建与排序（对照 WPF）=====

        private List<GitMmSubrepoItem> CreateSubrepoItems(IEnumerable<string> paths, string workspacePath)
        {
            List<string> orderedPaths = ApplySavedSubrepoOrder(paths, workspacePath);
            List<GitMmSubrepoItem> items = new List<GitMmSubrepoItem>();
            foreach (string path in orderedPaths)
            {
                items.Add(new GitMmSubrepoItem(path, workspacePath,
                    _submoduleSubrepoPaths.Contains(NormalizePath(path))));
            }
            return items;
        }

        private static void MigrateRuntimeState(List<GitMmSubrepoItem> oldSubrepos, List<GitMmSubrepoItem> newSubrepos)
        {
            if (oldSubrepos == null || newSubrepos == null || oldSubrepos.Count == 0) return;
            Dictionary<string, GitMmSubrepoItem> oldByPath =
                new Dictionary<string, GitMmSubrepoItem>(StringComparer.OrdinalIgnoreCase);
            foreach (GitMmSubrepoItem old in oldSubrepos)
            {
                string key = NormalizePath(old.Path);
                if (!string.IsNullOrEmpty(key)) oldByPath[key] = old;
            }
            foreach (GitMmSubrepoItem n in newSubrepos)
            {
                string key = NormalizePath(n.Path);
                if (key == null || !oldByPath.TryGetValue(key, out GitMmSubrepoItem old) || old == null) continue;
                n.HasLocalChanges = old.HasLocalChanges;
                n.ChangedFilesCount = old.ChangedFilesCount;
                n.HasConflicts = old.HasConflicts;
                n.ConflictFilesCount = old.ConflictFilesCount;
                n.IsNonDefaultBranch = old.IsNonDefaultBranch;
                n.CurrentBranch = old.CurrentBranch;
                n.DefaultBranch = old.DefaultBranch;
                n.AheadCount = old.AheadCount;
                n.BehindCount = old.BehindCount;
                n.StagedAdded = old.StagedAdded;
                n.StagedDeleted = old.StagedDeleted;
                n.RuntimeStateUpdatedAtUtc = old.RuntimeStateUpdatedAtUtc;
            }
        }

        private List<string> ApplySavedSubrepoOrder(IEnumerable<string> paths, string workspacePath)
        {
            List<string> remainingPaths = (paths ?? new string[0]).ToList();
            List<string> orderedPaths = new List<string>();
            int rootIndex = remainingPaths.FindIndex(p => IsSamePath(p, workspacePath));
            if (rootIndex >= 0)
            {
                orderedPaths.Add(remainingPaths[rootIndex]);
                remainingPaths.RemoveAt(rootIndex);
            }
            string[] savedOrder = ForkPlusSettings.Default.GitMm.GetSubrepoOrder(workspacePath);
            foreach (string savedPath in savedOrder)
            {
                if (IsSamePath(savedPath, workspacePath)) continue;
                int index = remainingPaths.FindIndex(p => IsSamePath(p, savedPath));
                if (index >= 0)
                {
                    orderedPaths.Add(remainingPaths[index]);
                    remainingPaths.RemoveAt(index);
                }
            }
            orderedPaths.AddRange(remainingPaths);
            return orderedPaths;
        }

        // ===== 创建 RepositoryUserControl（对照 WPF，spike: 从 DI 解析）=====

        private Control CreateRepositoryContent(string path)
        {
            try
            {
                GitCommandResult<GitModule> result = new OpenGitRepositoryGitCommand().Execute(path);
                if (!result.Succeeded)
                {
                    return new TextBlock
                    {
                        Text = result.Error.FriendlyDescription,
                        Margin = new Thickness(10),
                        TextWrapping = TextWrapping.Wrap
                    };
                }
                // spike: 从 DI 容器解析 RepositoryUserControl（替代 WPF new RepositoryUserControl()）
                RepositoryUserControl ruc = _serviceProvider.GetService<RepositoryUserControl>()
                    ?? new RepositoryUserControl(_serviceProvider);
                ruc.HorizontalAlignment = HorizontalAlignment.Stretch;
                ruc.VerticalAlignment = VerticalAlignment.Stretch;
                ruc.DataContext = null;
                ruc.OpenRepository(result.Result);
                ruc.InvalidateAndRefresh(SubDomain.DefaultRefresh);
                ruc.ApplyLocalization();
                return ruc;
            }
            catch (Exception ex)
            {
                return new TextBlock
                {
                    Text = ex.Message,
                    Margin = new Thickness(10),
                    TextWrapping = TextWrapping.Wrap
                };
            }
        }

        private string GetPreferredSubrepoPath()
        {
            if (_workspace == null) return null;
            return ForkPlusSettings.Default.GitMm.GetActiveSubrepo(_workspace.Path)
                ?? _workspace.SelectedSubrepo?.Path
                ?? _workspace.PreferredSubrepoPath;
        }

        // ===== ScanSubrepos（对照 WPF 完整迁移）=====

        private static List<string> ScanSubrepos(string rootPath, int maxDepth)
        {
            return ScanSubrepos(rootPath, maxDepth, out _);
        }

        private static List<string> ScanSubrepos(string rootPath, int maxDepth, out HashSet<string> submodulePaths)
        {
            List<string> result = new List<string>();
            HashSet<string> seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            HashSet<string> discoveredSubmodulePaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            Dictionary<string, Submodule[]> submodulesByRepositoryPath =
                new Dictionary<string, Submodule[]>(StringComparer.OrdinalIgnoreCase);
            Dictionary<string, string> worktreeByGitDirectory =
                new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            if (string.IsNullOrWhiteSpace(rootPath) || !Directory.Exists(rootPath))
            {
                submodulePaths = discoveredSubmodulePaths;
                return result;
            }
            void AddIfGitWorkTree(string path, int depth, bool isSubmodule)
            {
                string normalizedPath = NormalizePath(path);
                if (normalizedPath == null || !IsGitWorkTree(normalizedPath) || !seen.Add(normalizedPath)) return;
                result.Add(normalizedPath);
                if (isSubmodule) discoveredSubmodulePaths.Add(normalizedPath);
                AddSubmodules(normalizedPath, depth + 1);
            }
            void AddSubmodules(string repositoryPath, int depth)
            {
                if (depth > maxDepth) return;
                string normalizedRepositoryPath = NormalizePath(repositoryPath);
                if (normalizedRepositoryPath == null) return;
                if (!submodulesByRepositoryPath.TryGetValue(normalizedRepositoryPath, out Submodule[] submodules))
                {
                    GitCommandResult<Submodule[]> submodulesResult =
                        new GetSubmodulesGitCommand().Execute(System.IO.Path.Combine(repositoryPath, ".gitmodules"));
                    submodules = submodulesResult.Succeeded ? submodulesResult.Result : new Submodule[0];
                    submodulesByRepositoryPath[normalizedRepositoryPath] = submodules;
                }
                foreach (Submodule submodule in submodules)
                {
                    if (!submodule.IsActive || string.IsNullOrWhiteSpace(submodule.Path)) continue;
                    string submodulePath = System.IO.Path.Combine(repositoryPath, submodule.Path);
                    if (IsGitWorkTree(submodulePath))
                    {
                        AddIfGitWorkTree(submodulePath, depth, true);
                    }
                }
            }
            void Walk(string directory, int depth)
            {
                if (depth > maxDepth) return;
                DirectoryInfo[] directories;
                try
                {
                    directories = new DirectoryInfo(directory).GetDirectories();
                }
                catch
                {
                    return;
                }
                foreach (DirectoryInfo child in directories)
                {
                    if (child.Name == ".git" || child.Name == ".repo" || child.Name == ".mm"
                        || child.Name == "node_modules" || child.Name == "bin" || child.Name == "obj")
                    {
                        continue;
                    }
                    string fullName = child.FullName;
                    if (IsGitWorkTree(fullName))
                    {
                        AddIfGitWorkTree(fullName, depth, false);
                        Walk(fullName, depth + 1);
                        continue;
                    }
                    Walk(fullName, depth + 1);
                }
            }
            void WalkMmProjects(string directory, int depth)
            {
                if (depth > Math.Max(maxDepth, 8) || !Directory.Exists(directory)) return;
                DirectoryInfo[] directories;
                try
                {
                    directories = new DirectoryInfo(directory).GetDirectories();
                }
                catch
                {
                    return;
                }
                foreach (DirectoryInfo child in directories)
                {
                    if (child.Name == ".git" || child.Name == "objects" || child.Name == "refs"
                        || child.Name == "logs" || child.Name == "hooks" || child.Name == "info")
                    {
                        continue;
                    }
                    string fullName = child.FullName;
                    if (IsGitWorkTree(fullName))
                    {
                        AddIfGitWorkTree(fullName, depth, true);
                    }
                    else
                    {
                        if (!worktreeByGitDirectory.TryGetValue(fullName, out string worktreePath))
                        {
                            worktreePath = ResolveWorktreePathFromGitDirectory(fullName);
                            worktreeByGitDirectory[fullName] = worktreePath;
                        }
                        if (worktreePath != null && IsGitWorkTree(worktreePath))
                        {
                            AddIfGitWorkTree(worktreePath, depth, true);
                        }
                    }
                    WalkMmProjects(fullName, depth + 1);
                }
            }
            AddIfGitWorkTree(rootPath, 0, false);
            Walk(rootPath, 0);
            WalkMmProjects(System.IO.Path.Combine(rootPath, ".mm", "projects"), 0);
            result.Sort(StringComparer.OrdinalIgnoreCase);
            int rootIndex = result.FindIndex(p => IsSamePath(p, rootPath));
            if (rootIndex > 0)
            {
                string root = result[rootIndex];
                result.RemoveAt(rootIndex);
                result.Insert(0, root);
            }
            submodulePaths = discoveredSubmodulePaths;
            return result;
        }

        private static bool IsGitWorkTree(string path)
        {
            return Directory.Exists(System.IO.Path.Combine(path, ".git"))
                || File.Exists(System.IO.Path.Combine(path, ".git"));
        }

        private static string ResolveWorktreePathFromGitDirectory(string gitDirectory)
        {
            if (string.IsNullOrWhiteSpace(gitDirectory) || !Directory.Exists(gitDirectory)) return null;
            string configPath = System.IO.Path.Combine(gitDirectory, "config");
            if (!File.Exists(configPath)) return null;
            try
            {
                GitCommandResult<GitConfig> gitConfigResult = new GetGitConfigGitCommand().Execute(configPath);
                if (!gitConfigResult.Succeeded) return null;
                foreach (GitConfig.Section section in gitConfigResult.Result.Sections)
                {
                    if (section.Name != "core") continue;
                    foreach (GitConfig.Variable variable in section.Variables)
                    {
                        if (variable.Name != "worktree" || string.IsNullOrWhiteSpace(variable.Value)) continue;
                        string worktreePath = System.IO.Path.IsPathRooted(variable.Value)
                            ? variable.Value
                            : System.IO.Path.GetFullPath(System.IO.Path.Combine(gitDirectory, variable.Value));
                        return NormalizePath(worktreePath);
                    }
                }
            }
            catch (Exception ex)
            {
                Log.Warn("Failed to resolve worktree for git directory '" + gitDirectory + "'", ex);
            }
            return null;
        }

        // ===== 输出处理（对照 WPF Output.cs，spike: TextBox 纯文本）=====

        private void AppendOutput(string text)
        {
            lock (_outputLock)
            {
                _pendingOutputLines.Add(text ?? "");
                if (_outputFlushScheduled) return;
                _outputFlushScheduled = true;
            }
            Dispatcher.UIThread.Post(FlushOutput);
        }

        private void AppendOutputText(string text)
        {
            if (string.IsNullOrEmpty(text)) return;
            string[] lines = text.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n');
            foreach (string line in lines)
            {
                if (line.Length != 0)
                {
                    AppendOutput(line);
                }
            }
        }

        private static string StripAnsiEscapes(string text)
        {
            return string.IsNullOrEmpty(text) ? text : AnsiEscapeRegex.Replace(text, "");
        }

        private void ClearOutput()
        {
            lock (_outputLock)
            {
                _pendingOutputLines.Clear();
                _outputFlushScheduled = false;
            }
            if (Dispatcher.UIThread.CheckAccess())
            {
                if (OutputTextBox != null) OutputTextBox.Text = "";
            }
            else
            {
                Dispatcher.UIThread.Post(() =>
                {
                    if (OutputTextBox != null) OutputTextBox.Text = "";
                });
            }
        }

        private void FlushOutput()
        {
            List<string> lines;
            lock (_outputLock)
            {
                lines = new List<string>(_pendingOutputLines);
                _pendingOutputLines.Clear();
                _outputFlushScheduled = false;
            }
            if (OutputTextBox == null || lines.Count == 0) return;
            // spike: 纯文本追加 + ANSI 剥离（WPF 用 FlowDocument + 颜色 + 超链接）
            foreach (string line in lines)
            {
                string stripped = StripAnsiEscapes(line);
                if (OutputTextBox.Text.Length > 0)
                {
                    OutputTextBox.Text += Environment.NewLine;
                }
                OutputTextBox.Text += stripped;
            }
            // 限制行数（对照 WPF TrimOutputLines）
            string[] allLines = OutputTextBox.Text.Split('\n');
            if (allLines.Length > MaxOutputLineCount)
            {
                OutputTextBox.Text = string.Join("\n", allLines.Skip(allLines.Length - MaxOutputLineCount));
            }
            // 滚动到底部
            var scrollViewer = OutputTextBox.FindDescendantOfType<ScrollViewer>();
            if (scrollViewer != null)
            {
                scrollViewer.ScrollToEnd();
            }
        }

        // ===== URL 提取与处理（对照 WPF Output.cs）=====

        private static string CleanUrl(string value)
        {
            string text = StripAnsiEscapes(value ?? "").Trim().Trim('\'', '"');
            return TrimUrl(text, out _);
        }

        private static string TrimUrl(string value, out string trailingText)
        {
            value = value ?? "";
            string url = value.TrimEnd('.', ',', ';', ':', ')', ']', '\'', '"');
            trailingText = value.Substring(url.Length);
            return url;
        }

        private static bool TryCreateHttpUri(string link, out Uri uri)
        {
            uri = null;
            string cleaned = CleanUrl(link);
            return Uri.TryCreate(cleaned, UriKind.Absolute, out uri)
                && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps);
        }

        private static void OpenUrl(string link)
        {
            if (!TryCreateHttpUri(link, out Uri uri))
            {
                Log.Warn("Ignoring invalid upload URL: " + link);
                return;
            }
            try
            {
                uri.OpenInBrowser();
            }
            catch (Exception ex)
            {
                Log.Warn("Failed to open upload URL: " + uri, ex);
            }
        }

        private static string[] ExtractUrls(string text)
        {
            if (string.IsNullOrWhiteSpace(text)) return new string[0];
            return UrlRegex.Matches(text)
                .OfType<Match>()
                .Select(m => CleanUrl(m.Value))
                .Where(url => TryCreateHttpUri(url, out _))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();
        }

        // ===== Upload Links 面板（对照 WPF Output.cs）=====

        private void RefreshUploadLinksPanel(string[] links)
        {
            RefreshUploadLinksPanel(links, autoHide: true);
        }

        private void RefreshUploadLinksPanel(string[] links, bool autoHide)
        {
            _latestUploadLinks = (links ?? new string[0])
                .Select(CleanUrl)
                .Where(link => TryCreateHttpUri(link, out _))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();
            if (UploadLinksPanel != null) UploadLinksPanel.Children.Clear();
            if (_latestUploadLinks.Length == 0)
            {
                if (UploadLinksContainer != null) UploadLinksContainer.IsVisible = false;
                if (ShowUploadLinksButton != null) ShowUploadLinksButton.IsVisible = false;
                return;
            }
            if (UploadLinksContainer != null) UploadLinksContainer.IsVisible = true;
            if (ShowUploadLinksButton != null) ShowUploadLinksButton.IsVisible = false;
            foreach (string link in _latestUploadLinks.Subsequence(0, Math.Min(5, _latestUploadLinks.Length)))
            {
                var button = new Button
                {
                    Content = UploadLinkTitle(link),
                    FontSize = 12.0,
                    Padding = new Thickness(6, 1, 6, 1),
                    Margin = new Thickness(0, 0, 8, 0)
                };
                ToolTip.SetTip(button, link);
                string capturedLink = link;
                button.Click += (_, _) => OpenUrl(capturedLink);
                UploadLinksPanel.Children.Add(button);
            }
        }

        private void HideUploadLinksButton_Click(object sender, RoutedEventArgs e)
        {
            HideUploadLinksPanel();
        }

        private static string UploadLinkTitle(string link)
        {
            if (TryCreateHttpUri(link, out Uri uri))
            {
                string path = uri.AbsolutePath.Trim('/');
                if (!string.IsNullOrWhiteSpace(path))
                {
                    return uri.Host + "/" + path;
                }
                return uri.Host;
            }
            return link;
        }

        private void ShowUploadLinksButton_Click(object sender, RoutedEventArgs e)
        {
            SaveUploadLinksCollapsed(false);
            RefreshUploadLinksPanel(_latestUploadLinks, autoHide: true);
        }

        private void HideUploadLinksPanel()
        {
            HideUploadLinksPanel(save: true);
        }

        private void HideUploadLinksPanel(bool save)
        {
            if (UploadLinksContainer != null) UploadLinksContainer.IsVisible = false;
            if (ShowUploadLinksButton != null) ShowUploadLinksButton.IsVisible = _latestUploadLinks.Length > 0;
            if (save && _latestUploadLinks.Length > 0)
            {
                SaveUploadLinksCollapsed(true);
            }
        }

        // ===== 辅助方法（对照 WPF）=====

        private static string FormatCommand(string[] args)
        {
            return "git mm " + string.Join(" ", args.Select(QuoteIfNeeded));
        }

        private static string QuoteIfNeeded(string value)
        {
            if (string.IsNullOrEmpty(value)) return "\"\"";
            if (value.IndexOfAny(new char[2] { ' ', '\t' }) < 0) return value;
            return "\"" + value.Replace("\\", "\\\\").Replace("\"", "\\\"") + "\"";
        }

        internal static bool IsSamePath(string lhs, string rhs)
        {
            string normalizedLhs = NormalizePath(lhs);
            string normalizedRhs = NormalizePath(rhs);
            return !string.IsNullOrWhiteSpace(normalizedLhs)
                && !string.IsNullOrWhiteSpace(normalizedRhs)
                && string.Equals(normalizedLhs, normalizedRhs, StringComparison.OrdinalIgnoreCase);
        }

        private static string NormalizePath(string path)
        {
            if (string.IsNullOrWhiteSpace(path)) return null;
            try
            {
                path = System.IO.Path.GetFullPath(path);
            }
            catch
            {
            }
            return PathHelper.Normalize(path)
                .TrimEnd(System.IO.Path.DirectorySeparatorChar, System.IO.Path.AltDirectorySeparatorChar, '\\', '/');
        }

        // ===== 翻译辅助（对照 WPF: PreferencesLocalization → ServiceLocator.Localization）=====

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

        private static string FormatCurrent(string text, params object[] args)
        {
            var localization = ServiceLocator.Localization;
            var userSettings = ServiceLocator.UserSettings;
            if (localization != null && userSettings != null)
            {
                return localization.FormatCurrent(text, args);
            }
            return string.Format(text, args);
        }

        private static string FindRepositoryName(string path)
        {
            // spike: WPF RepositoryManager.Instance.FindRepositoryName(path) 不可用（RepositoryManager
            // 在 WPF 工程 src/ForkPlus/RepositoryManager.cs，未迁入 Core / Avalonia）。
            // 返回 null 触发 fallback 到目录名（与 RepositoryUserControl 的 spike 策略一致）。
            if (string.IsNullOrWhiteSpace(path)) return null;
            return null;
        }

        // ===== Ctrl 键检测（对照 WPF: KeyboardHelper.IsCtrlDown）=====

        private static bool IsCtrlDown(RoutedEventArgs e)
        {
            if (e is KeyEventArgs keyArgs)
            {
                return (keyArgs.KeyModifiers & KeyModifiers.Control) == KeyModifiers.Control;
            }
            return false;
        }

        // ===== 显示对话框辅助（spike: 获取父 Window 调用 ShowDialog）=====

        private bool? ShowDialog(Window window)
        {
            try
            {
                var parentWindow = this.VisualRoot as Window;
                if (parentWindow != null)
                {
                    return window.ShowDialog<bool?>(parentWindow).GetAwaiter().GetResult();
                }
                window.Show();
                return true;
            }
            catch (Exception ex)
            {
                Log.Warn("Failed to show dialog", ex);
                return null;
            }
        }
    }

    // ===== GitMmWorkspaceItem（对照 WPF，完整迁移）=====

    public sealed class GitMmWorkspaceItem : INotifyPropertyChanged
    {
        private List<GitMmSubrepoItem> _subrepos = new List<GitMmSubrepoItem>();

        private GitMmSubrepoItem _selectedSubrepo;

        public string Path { get; }

        public string Name { get; }

        public string PreferredSubrepoPath { get; set; }

        public List<GitMmSubrepoItem> Subrepos
        {
            get { return _subrepos; }
            set { SetSubrepos(value, selectPreferred: true); }
        }

        public GitMmSubrepoItem SelectedSubrepo
        {
            get { return _selectedSubrepo; }
            set
            {
                if (_selectedSubrepo != value)
                {
                    _selectedSubrepo = value;
                    PreferredSubrepoPath = value?.Path;
                    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(SelectedSubrepo)));
                }
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;

        public GitMmWorkspaceItem(string path)
        {
            Path = path;
            Name = System.IO.Path.GetFileName(path.TrimEnd(
                System.IO.Path.DirectorySeparatorChar, System.IO.Path.AltDirectorySeparatorChar)) ?? path;
        }

        public void SetSubrepos(List<GitMmSubrepoItem> subrepos, bool selectPreferred)
        {
            _subrepos = subrepos ?? new List<GitMmSubrepoItem>();
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Subrepos)));
            if (selectPreferred)
            {
                SelectedSubrepo = _subrepos.FirstOrDefault(item => GitMmUserControl.IsSamePath(item.Path, PreferredSubrepoPath))
                    ?? _subrepos.FirstOrDefault();
            }
        }
    }

    // ===== GitMmSubrepoItem（对照 WPF，完整迁移 — spike 内部完整版）=====
    // 注意：Dialogs.GitMmSubrepoItem 是 stub（Path/Name），此类是完整版含运行态属性。
    // 调用 GitMmStartWindow 时转换为 Dialogs.GitMmSubrepoItem。

    public sealed class GitMmSubrepoItem
    {
        public string Path { get; }

        public string Name { get; }

        public bool IsRootRepository { get; }

        public bool IsSubmodule { get; }

        public GitMmSubrepoCommandState CommandState { get; set; }

        public bool HasLocalChanges { get; set; }

        public int ChangedFilesCount { get; set; }

        public bool HasConflicts { get; set; }

        public int ConflictFilesCount { get; set; }

        public bool IsNonDefaultBranch { get; set; }

        public string CurrentBranch { get; set; }

        public string DefaultBranch { get; set; }

        public int AheadCount { get; set; }

        public int BehindCount { get; set; }

        public int StagedAdded { get; set; }

        public int StagedDeleted { get; set; }

        public DateTime? RuntimeStateUpdatedAtUtc { get; set; }

        public string BaseDisplayName => FindRepositoryAlias(Path) ?? Name;

        public string DisplayName => BaseDisplayName
            + (IsRootRepository ? TranslateCurrent("[Main]")
                : IsSubmodule ? TranslateCurrent("[Submodule]")
                : TranslateCurrent("[Sub]"));

        // spike: Control 替代 WPF FrameworkElement
        public Control RepositoryControl { get; set; }

        public GitMmSubrepoItem(string path, string rootPath, bool isSubmodule)
        {
            Path = path;
            Name = CreateName(path, rootPath);
            IsRootRepository = GitMmUserControl.IsSamePath(path, rootPath);
            IsSubmodule = isSubmodule;
        }

        private static string FindRepositoryAlias(string path)
        {
            // spike: WPF RepositoryManager.Instance.Repositories.FirstItemStruct(...)?.Alias 不可用
            // （RepositoryManager 在 WPF 工程，未迁入 Core / Avalonia）。返回 null 触发 fallback 到 Name。
            return null;
        }

        private static string CreateName(string path, string rootPath)
        {
            string relative = path;
            if (!string.IsNullOrEmpty(rootPath) && path.StartsWith(rootPath, StringComparison.OrdinalIgnoreCase))
            {
                relative = path.Substring(rootPath.Length)
                    .TrimStart(System.IO.Path.DirectorySeparatorChar, System.IO.Path.AltDirectorySeparatorChar);
            }
            if (string.IsNullOrWhiteSpace(relative))
            {
                return System.IO.Path.GetFileName(path.TrimEnd(
                    System.IO.Path.DirectorySeparatorChar, System.IO.Path.AltDirectorySeparatorChar));
            }
            return relative;
        }

        private static string TranslateCurrent(string text)
        {
            var localization = ServiceLocator.Localization;
            var userSettings = ServiceLocator.UserSettings;
            if (localization != null && userSettings != null)
            {
                return localization.Translate(text, userSettings.UiLanguage);
            }
            return text;
        }
    }

    // ===== GitMmSubrepoCommandState 枚举（对照 WPF）=====

    public enum GitMmSubrepoCommandState
    {
        None,
        Running,
        Success,
        Failed
    }

    // ===== GitMmSubrepoRuntimeState（对照 WPF，internal）=====

    internal sealed class GitMmSubrepoRuntimeState
    {
        public bool HasLocalChanges { get; set; }

        public int ChangedFilesCount { get; set; }

        public bool HasConflicts { get; set; }

        public int ConflictFilesCount { get; set; }

        public bool IsNonDefaultBranch { get; set; }

        public string CurrentBranch { get; set; }

        public string DefaultBranch { get; set; }

        public int AheadCount { get; set; }

        public int BehindCount { get; set; }

        public int StagedAdded { get; set; }

        public int StagedDeleted { get; set; }
    }
}

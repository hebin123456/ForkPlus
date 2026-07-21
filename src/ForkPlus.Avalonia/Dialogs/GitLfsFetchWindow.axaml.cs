using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Threading;
using ForkPlus;
using ForkPlus.Git;
using ForkPlus.Git.Commands;
using ForkPlus.Jobs;
using ForkPlus.Services;
using ForkPlus.Settings;

namespace ForkPlus.Avalonia.Dialogs
{
    // Phase 4.44b：Avalonia 版 GitLfsFetchWindow（真实迁移版，对照 WPF GitLfsFetchWindow.xaml.cs 107 行）。
    //
    // 对照 WPF 工程 src/ForkPlus/UI/Dialogs/GitLfsFetchWindow.xaml.cs：
    //   - public partial class GitLfsFetchWindow : ForkPlusDialogWindow
    //   - 字段: RepositoryUserControl _repositoryUserControl / GitModule _gitModule
    //   - 构造函数 (RepositoryUserControl repositoryUserControl, GitModule gitModule)
    //     → 从 _repositoryUserControl.RepositoryData.Remotes 取 remotes
    //   - IsSubmitAllowed: RemotesComboBox.SelectedItem is Remote
    //   - GetCommandPreview: "git lfs fetch " + remote.Name
    //   - OnSubmit: GitLfsFetchGitCommand().Execute(_gitModule, remote, monitor) → Close()
    //   - Refresh: 排序 Remotes，按 upstream 或 default 选择
    //
    // Avalonia 版差异：
    //   1. spike 模式：构造函数 SetFooter/Footer 注入
    //   2. RepositoryUserControl 参数 → GitModule + RepositoryRemotes + RepositoryReferences? + Action onCompleted 回调
    //   3. spike 基类不提供 GetCommandPreview/RefreshCommandPreview → 自行维护 CommandPreviewTextBlock
    //   4. spike 基类不提供 DisableEditableControls → 手动禁用 RemotesComboBox
    //   5. JobQueue + Dispatcher.Async → Task.Run + Dispatcher.UIThread.Post + JobMonitor
    //   6. ErrorWindow + InvalidateAndRefresh → spike 由 onCompleted 回调由调用方处理
    //   7. ComboBox ItemTemplate 用 Image + TextBlock → spike 版用 TextBlock 显示 Remote.Name 简化
    //   8. PreferencesLocalization.Translate → ServiceLocator.Localization.Translate
    public partial class GitLfsFetchWindow : global::ForkPlus.Avalonia.Dialogs.ForkPlusDialogWindow
    {
        private readonly GitModule _gitModule;
        private readonly RepositoryRemotes _remotesSource;
        private readonly RepositoryReferences? _references;
        private readonly Action<GitCommandResult>? _onCompleted;

        // 构造函数签名与 WPF 不同：用 GitModule + RepositoryRemotes + RepositoryReferences? + Action 回调替代 RepositoryUserControl
        // （RepositoryUserControl 在 Avalonia 端尚未迁移，spike 版解耦）
        public GitLfsFetchWindow(
            GitModule gitModule,
            RepositoryRemotes remotes,
            RepositoryReferences? references = null,
            Action<GitCommandResult>? onCompleted = null)
        {
            ShowFooter = true;
            InitializeComponent();
            SetFooter(Footer);
            SetTitleTextBlock(TitleTextBlock);
            SetDescriptionTextBlock(DescriptionTextBlock);

            _gitModule = gitModule ?? throw new ArgumentNullException(nameof(gitModule));
            _remotesSource = remotes ?? RepositoryRemotes.Empty;
            _references = references;
            _onCompleted = onCompleted;

            // 对照 WPF: DialogTitle / DialogDescription / SubmitButtonTitle
            string title = Translate("Fetch");
            Title = title;
            DialogTitle = title;
            DialogDescription = Translate("Download Git LFS objects from the specified remotes");
            SubmitButtonTitle = Translate("Fetch");
            CancelButtonTitle = Translate("Cancel");

            Refresh();
            UpdateSubmitButton();
        }

        // 对照 WPF: protected override bool IsSubmitAllowed
        protected override bool IsSubmitAllowed
        {
            get
            {
                if (RemotesComboBox.SelectedItem is Remote)
                {
                    return base.IsSubmitAllowed;
                }
                return false;
            }
        }

        // 对照 WPF: protected override string GetCommandPreview()
        private string? GetCommandPreview()
        {
            if (!(RemotesComboBox.SelectedItem is Remote remote))
            {
                return null;
            }
            return "git lfs fetch " + remote.Name;
        }

        private void RefreshCommandPreview()
        {
            if (CommandPreviewTextBox == null) return;
            string? preview = GetCommandPreview();
            CommandPreviewTextBox.Text = preview ?? string.Empty;
        }

        // 对照 WPF: protected override void OnSubmit()
        protected override void OnSubmit()
        {
            Remote? remote = RemotesComboBox.SelectedItem as Remote;
            if (remote == null)
            {
                return;
            }

            DisableEditableControls();
            SetStatus(ForkPlusDialogStatus.InProgress, string.Format(Translate("LFS Fetch {0}"), remote.Name));

            // 对照 WPF: _repositoryUserControl.JobQueue.Add(string.Format(Translate("LFS Fetch {0}"), remote.Name), ...)
            // Avalonia: Task.Run + Dispatcher.UIThread.Post + JobMonitor
            Task.Run(delegate
            {
                JobMonitor monitor = new JobMonitor();
                GitCommandResult fetchResult = new GitLfsFetchGitCommand().Execute(_gitModule, remote, monitor);
                GitCommandResult finalResult = fetchResult.Succeeded ? GitCommandResult.Success() : fetchResult;
                Dispatcher.UIThread.Post(delegate
                {
                    try
                    {
                        _onCompleted?.Invoke(finalResult);
                    }
                    catch (Exception ex)
                    {
                        Log.Error("GitLfsFetchWindow onCompleted callback failed", ex);
                    }
                    Close(finalResult);
                });
            });
        }

        // 对照 WPF: RemotesComboBox_SelectionChanged
        public void RemotesComboBox_SelectionChanged(object? sender, SelectionChangedEventArgs e)
        {
            UpdateSubmitButton();
            RefreshCommandPreview();
        }

        // 对照 WPF: private void Refresh()
        private void Refresh()
        {
            Remote[] array = _remotesSource.Items.ToSortedArray(Remote.ComparerIgnoreCaseNumeric);
            if (array.Length == 0)
            {
                RemotesComboBox.ItemsSource = array;
                return;
            }
            // 对照 WPF: 检测 upstream，优先选 upstream remote
            Remote? remote = null;
            string? upstreamFullReference = _references?.ActiveBranch?.UpstreamFullReference;
            if (upstreamFullReference != null && _references != null)
            {
                RemoteBranch? activeUpstream = IReadOnlyListExtensions.FirstItem(_references.RemoteBranches, (RemoteBranch x) => x.FullReference == upstreamFullReference);
                if (activeUpstream != null)
                {
                    remote = IReadOnlyListExtensions.FirstItem(array, (Remote x) => x.Name == activeUpstream.Remote);
                }
            }
            RemotesComboBox.ItemsSource = array;
            // 对照 WPF: SelectedItem = remote ?? origin ?? FirstItem
            Remote? selectedItem = remote
                ?? IReadOnlyListExtensions.FirstItem(array, (Remote x) => x.Name == Consts.Git.DefaultRemoteName)
                ?? IReadOnlyListExtensions.FirstItem(array);
            RemotesComboBox.SelectedItem = selectedItem;

            RefreshCommandPreview();
        }

        // spike 版：手动禁用可编辑控件（基类不提供 DisableEditableControls）
        private void DisableEditableControls()
        {
            RemotesComboBox.IsEnabled = false;
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
    }
}

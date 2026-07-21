using System;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Threading;
using ForkPlus;
using ForkPlus.Git;
using ForkPlus.Git.Commands;
using ForkPlus.Jobs;
using ForkPlus.Services;

namespace ForkPlus.Avalonia.Dialogs
{
    // Phase 4.43b：Avalonia 版 PushMultipleTagsWindow（真实迁移版，对照 WPF PushMultipleTagsWindow.xaml.cs 130 行）。
    //
    // 对照 WPF 工程 src/ForkPlus/UI/Dialogs/PushMultipleTagsWindow.xaml.cs：
    //   - public partial class PushMultipleTagsWindow : ForkPlusDialogWindow
    //   - 字段: RepositoryUserControl _repositoryUserControl / Tag[] _tags / Remote _remoteToSelect
    //   - 构造函数 (RepositoryUserControl, Tag[] tags, Remote remote)
    //   - IsSubmitAllowed: RemotesComboBox.SelectedItem is Remote
    //   - GetCommandPreview: "git push remote.Name tag1.FullReference tag2.FullReference..."
    //   - OnSubmit: PushMultipleTagsGitCommand().Execute(...) → Close()
    //   - Refresh: TagsItemsControl 绑定 tag.Name，排序 Remotes，按 upstream 或 default 选择
    //
    // Avalonia 版差异：
    //   1. spike 模式：构造函数 SetFooter/Footer 注入
    //   2. RepositoryUserControl 参数 → GitModule + Tag[] + Remote[] + Remote? + RepositoryReferences? + Action onCompleted 回调
    //   3. TagsItemsControl 用 Image + TextBlock → spike 版用 TextBlock 显示 tag name 简化（省略 TagIcon）
    //   4. spike 基类不提供 GetCommandPreview/RefreshCommandPreview → 自行维护 CommandPreviewTextBlock
    //   5. spike 基类不提供 DisableEditableControls → 手动禁用 RemotesComboBox
    //   6. JobQueue + Dispatcher.Async → Task.Run + Dispatcher.UIThread.Post + JobMonitor
    //   7. ErrorWindow + InvalidateAndRefresh → spike 由 onCompleted 回调由调用方处理
    //   8. ComboBox ItemTemplate 用 Image + TextBlock → spike 版用 TextBlock 显示 Remote.Name 简化
    //   9. PreferencesLocalization.Translate → ServiceLocator.Localization.Translate
    public partial class PushMultipleTagsWindow : global::ForkPlus.Avalonia.Dialogs.ForkPlusDialogWindow
    {
        private readonly GitModule _gitModule;
        private readonly Tag[] _tags;
        private readonly Remote[] _remotes;
        private readonly Remote? _remoteToSelect;
        private readonly RepositoryReferences? _references;
        private readonly Action<GitCommandResult>? _onCompleted;

        // 构造函数签名与 WPF 不同：用 GitModule + Tag[] + Remote[] + Remote? + RepositoryReferences? + Action 回调
        // 替代 RepositoryUserControl（RepositoryUserControl 在 Avalonia 端尚未迁移，spike 版解耦）
        public PushMultipleTagsWindow(
            GitModule gitModule,
            Tag[] tags,
            Remote[] remotes,
            Remote? remoteToSelect = null,
            RepositoryReferences? references = null,
            Action<GitCommandResult>? onCompleted = null)
        {
            ShowFooter = true;
            InitializeComponent();
            SetFooter(Footer);
            SetTitleTextBlock(TitleTextBlock);
            SetDescriptionTextBlock(DescriptionTextBlock);

            _gitModule = gitModule ?? throw new ArgumentNullException(nameof(gitModule));
            _tags = tags ?? throw new ArgumentNullException(nameof(tags));
            _remotes = remotes ?? Array.Empty<Remote>();
            _remoteToSelect = remoteToSelect;
            _references = references;
            _onCompleted = onCompleted;

            // 对照 WPF: DialogTitle = Translate("Push");
            string title = Translate("Push");
            Title = title;
            DialogTitle = title;
            // 对照 WPF: DialogDescription = string.Format(Translate("Push {0} tags to remote repository"), _tags.Length);
            DialogDescription = string.Format(Translate("Push {0} tags to remote repository"), _tags.Length);
            SubmitButtonTitle = string.Format(Translate("Push {0} tags"), _tags.Length);
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
            Remote? remote = RemotesComboBox.SelectedItem as Remote;
            if (remote == null)
            {
                return null;
            }
            var parts = new System.Collections.Generic.List<string> { "git", "push", remote.Name };
            foreach (Tag tag in _tags)
            {
                parts.Add(tag.FullReference);
            }
            return string.Join(" ", parts);
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
            Tag[] tags = _tags;

            DisableEditableControls();
            SetStatus(ForkPlusDialogStatus.InProgress, string.Format(Translate("Pushing {0} tags to '{1}'..."), tags.Length, remote.Name));

            // 对照 WPF: _repositoryUserControl.JobQueue.Add(...)
            // Avalonia: Task.Run + Dispatcher.UIThread.Post + JobMonitor
            Task.Run(delegate
            {
                JobMonitor monitor = new JobMonitor();
                GitCommandResult pushResult = new PushMultipleTagsGitCommand().Execute(_gitModule, remote.Name, tags, monitor);
                GitCommandResult finalResult = pushResult.Succeeded ? GitCommandResult.Success() : pushResult;
                Dispatcher.UIThread.Post(delegate
                {
                    try
                    {
                        _onCompleted?.Invoke(finalResult);
                    }
                    catch (Exception ex)
                    {
                        Log.Error("PushMultipleTagsWindow onCompleted callback failed", ex);
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
            // 对照 WPF: TagsItemsControl.ItemsSource = _tags.Map((Tag x) => x.Name);
            TagsItemsControl.ItemsSource = IReadOnlyListExtensions.Map(_tags, (Tag x) => x.Name);

            Remote[] array = _remotes.ToSortedArray(Remote.ComparerIgnoreCaseNumeric);
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
            // 对照 WPF: SelectedItem = _remoteToSelect ?? remote ?? origin ?? FirstItem
            Remote? selectedItem = _remoteToSelect
                ?? remote
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

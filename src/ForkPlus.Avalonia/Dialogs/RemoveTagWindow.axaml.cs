using System;
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

namespace ForkPlus.Avalonia.Dialogs
{
    // Phase 4.41b：Avalonia 版 RemoveTagWindow（真实迁移版，对照 WPF RemoveTagWindow.xaml.cs 127 行）。
    //
    // 对照 WPF 工程 src/ForkPlus/UI/Dialogs/RemoveTagWindow.xaml.cs：
    //   - public partial class RemoveTagWindow : ForkPlusDialogWindow
    //   - 字段: RepositoryUserControl _repositoryUserControl / RepositoryReferences _references /
    //           Tag[] _tags / RepositoryRemotes _remotes
    //   - 构造函数 (RepositoryUserControl, Tag[], RepositoryReferences)
    //   - 单个 tag: GitPointView.Value = tag，标题 "Delete Tag"
    //   - 多个 tag: GitPoints.ItemsSource = tags，标题 "Delete Tags" / "Delete {N} tags"
    //   - GetCommandPreview: "git tag -d tag1 tag2..." + 可选 push --delete
    //   - OnSubmit: RemoveTagGitCommand().Execute(...) → 过滤 PinnedReferences/FilterReferences
    //     → Close(Success 或 result)
    //
    // Avalonia 版差异：
    //   1. spike 模式：构造函数 SetFooter/Footer 注入
    //   2. RepositoryUserControl 参数 → GitModule + Tag[] + RepositoryReferences + Remote[] + Action onCompleted 回调
    //   3. GitPointView 自定义控件 → spike 版用 TextBlock 显示 tag name 简化
    //   4. GitPoints (ItemsControl + BindableGitPointView) → spike 版用 ItemsControl + DataTemplate 显示 tag name
    //   5. spike 基类不提供 GetCommandPreview/RefreshCommandPreview → 自行维护 CommandPreviewTextBlock
    //   6. spike 基类不提供 DisableEditableControls → 手动禁用 DeleteFromRemotesCheckBox
    //   7. JobQueue + Dispatcher.Async → Task.Run + Dispatcher.UIThread.Post + JobMonitor
    //   8. Collapse/Show 扩展方法 → IsVisible = false / true
    //   9. PreferencesLocalization.Current/FormatCurrent → ServiceLocator.Localization
    public partial class RemoveTagWindow : global::ForkPlus.Avalonia.Dialogs.ForkPlusDialogWindow
    {
        private readonly GitModule _gitModule;
        private readonly Tag[] _tags;
        private readonly RepositoryReferences? _references;
        private readonly Remote[] _remotes;
        private readonly Action<GitCommandResult>? _onCompleted;

        // 构造函数签名与 WPF 不同：用 GitModule + Tag[] + RepositoryReferences + Remote[] + Action 回调替代 RepositoryUserControl
        // （RepositoryUserControl 在 Avalonia 端尚未迁移，spike 版解耦；
        //  Remote[] 直接由调用方从 RepositoryData.Remotes.Items 注入）
        public RemoveTagWindow(
            GitModule gitModule,
            Tag[] tags,
            RepositoryReferences? references = null,
            Remote[]? remotes = null,
            Action<GitCommandResult>? onCompleted = null)
        {
            ShowFooter = true;
            InitializeComponent();
            SetFooter(Footer);
            SetTitleTextBlock(TitleTextBlock);
            SetDescriptionTextBlock(DescriptionTextBlock);

            _gitModule = gitModule ?? throw new ArgumentNullException(nameof(gitModule));
            _tags = tags ?? throw new ArgumentNullException(nameof(tags));
            _references = references;
            _remotes = remotes ?? Array.Empty<Remote>();
            _onCompleted = onCompleted;

            // 对照 WPF: 单个/多个 tag 分支
            if (_tags.Length == 1)
            {
                // 对照 WPF: GitPointsContainer.Collapse(); GitPointView.Show(); GitPointView.Value = _tags.FirstItem();
                TagsContainer.IsVisible = false;
                SingleTagTextBlock.IsVisible = true;
                SingleTagTextBlock.Text = _tags[0].Name;

                DialogTitle = Translate("Delete Tag");
                DialogDescription = Translate("Delete tag from your repository");
                StartPointTextBlock.Text = Translate("Tag:");
                DeleteFromRemotesCheckBox.Content = Translate("Delete tag from remote repositories");
                SubmitButtonTitle = Translate("Delete");
                Title = Translate("Delete Tag");
            }
            else
            {
                // 对照 WPF: GitPointView.Collapse(); GitPointsContainer.Show(); GitPoints.ItemsSource = _tags;
                SingleTagTextBlock.IsVisible = false;
                TagsContainer.IsVisible = true;
                TagsItemsControl.ItemsSource = _tags;

                DialogTitle = Translate("Delete Tags");
                DialogDescription = Translate("Delete tags from your repository");
                StartPointTextBlock.Text = Translate("Tags:");
                DeleteFromRemotesCheckBox.Content = Translate("Delete tags from remote repositories");
                SubmitButtonTitle = string.Format(Translate("Delete {0} tags"), _tags.Length);
                Title = Translate("Delete Tags");
            }
            CancelButtonTitle = Translate("Cancel");

            RefreshCommandPreview();
        }

        // 对照 WPF: protected override string GetCommandPreview()
        private string? GetCommandPreview()
        {
            if (_tags == null || _tags.Length == 0)
            {
                return null;
            }
            var parts = new System.Collections.Generic.List<string> { "git", "tag", "-d" };
            foreach (Tag t in _tags)
            {
                parts.Add(t.Name);
            }
            string command = string.Join(" ", parts);
            if (DeleteFromRemotesCheckBox.IsChecked.GetValueOrDefault() && _remotes != null)
            {
                foreach (Remote remote in _remotes)
                {
                    var pushParts = new System.Collections.Generic.List<string> { "git", "push", remote.Name, "--delete" };
                    foreach (Tag t in _tags)
                    {
                        pushParts.Add(t.Name);
                    }
                    command += "\n" + string.Join(" ", pushParts);
                }
            }
            return command;
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
            Tag[] tags = _tags;
            bool deleteFromRemotes = DeleteFromRemotesCheckBox.IsChecked.GetValueOrDefault();
            Remote[] remotes = deleteFromRemotes ? _remotes : Array.Empty<Remote>();

            // 对照 WPF: v3.4.1 状态栏标题国际化（之前是硬编码英文）
            string name = (tags.Length > 1)
                ? string.Format(Translate("Delete {0} tags"), tags.Length)
                : string.Format(Translate("Delete '{0}'"), tags[0].Name);

            DisableEditableControls();
            SetStatus(ForkPlusDialogStatus.InProgress, Translate("Deleting..."));

            // 对照 WPF: _repositoryUserControl.JobQueue.Add(name, ...)
            // Avalonia: Task.Run + Dispatcher.UIThread.Post + JobMonitor
            Task.Run(delegate
            {
                JobMonitor monitor = new JobMonitor();
                GitCommandResult removeTagResult = new RemoveTagGitCommand().Execute(_gitModule, tags, remotes, monitor);
                GitCommandResult finalResult;
                if (!removeTagResult.Succeeded)
                {
                    finalResult = removeTagResult;
                }
                else
                {
                    // 对照 WPF: 过滤 PinnedReferences / FilterReferences，移除已删除的 tag
                    if (_references != null)
                    {
                        _gitModule.Settings.PinnedReferences = _references.PinnedReferences
                            .Filter((string p) => !tags.ContainsItem((Tag t) => t.FullReference == p))
                            .ToArray();
                        _gitModule.Settings.FilterReferences = _references.FilterReferences
                            .Filter((string p) => !tags.ContainsItem((Tag t) => t.FullReference == p))
                            .ToArray();
                        _gitModule.Settings.Save();
                    }
                    finalResult = GitCommandResult.Success();
                }
                Dispatcher.UIThread.Post(delegate
                {
                    try
                    {
                        _onCompleted?.Invoke(finalResult);
                    }
                    catch (Exception ex)
                    {
                        Log.Error("RemoveTagWindow onCompleted callback failed", ex);
                    }
                    Close(finalResult);
                });
            });
        }

        // 对照 WPF: DeleteFromRemotesCheckBox_Changed（WPF 用 Checked/Unchecked 两个事件，Avalonia 用 IsCheckedChanged）
        public void DeleteFromRemotesCheckBox_Changed(object? sender, RoutedEventArgs e)
        {
            RefreshCommandPreview();
        }

        // spike 版：手动禁用可编辑控件（基类不提供 DisableEditableControls）
        private void DisableEditableControls()
        {
            DeleteFromRemotesCheckBox.IsEnabled = false;
        }

        // 对照 WPF: PreferencesLocalization.Current(text) / FormatCurrent(text, args)
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

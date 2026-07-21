using System;
using System.Threading.Tasks;
using Avalonia.Threading;
using ForkPlus.Git;
using ForkPlus.Git.Commands;
using ForkPlus.Jobs;
using ForkPlus.Services;

namespace ForkPlus.Avalonia.Dialogs
{
    // Phase 4.36b：Avalonia 版 RemoveStashWindow（真实迁移版，对照 WPF RemoveStashWindow.xaml.cs 104 行）。
    //
    // 对照 WPF 工程 src/ForkPlus/UI/Dialogs/RemoveStashWindow.xaml.cs：
    //   - public partial class RemoveStashWindow : ForkPlusDialogWindow
    //   - 字段: RepositoryUserControl _repositoryUserControl / StashRevision[] _stashes
    //   - 单个 stash: GitPointView.Value = stash，标题 "Delete Stash"
    //   - 多个 stash: GitPoints.ItemsSource = stashes，标题 "Delete Stashes" / "Delete {N} stashes"
    //   - GetCommandPreview override: 多行 "git stash drop {reflogName}" 拼接
    //   - OnSubmit: RemoveStashGitCommand().Execute(gitModule, stashes, monitor) → Close(result)
    //
    // Avalonia 版差异：
    //   1. spike 模式：构造函数 SetFooter/Footer 注入
    //   2. RepositoryUserControl 参数 → GitModule + Action<GitCommandResult>? onCompleted 回调
    //   3. GitPointView 自定义控件 → spike 版用 TextBlock 显示 reflog name 简化
    //   4. GitPoints 自定义控件 → spike 版用 ItemsControl + DataTemplate 显示 reflog name 列表
    //   5. spike 基类不提供 GetCommandPreview/RefreshCommandPreview → 自行维护 CommandPreviewTextBlock
    //   6. spike 基类不提供 DisableEditableControls → 空实现
    //   7. JobQueue + Dispatcher.Async → Task.Run + Dispatcher.UIThread.Post + JobMonitor
    //   8. Collapse/Show 扩展方法 → IsVisible = false / true
    public partial class RemoveStashWindow : global::ForkPlus.Avalonia.Dialogs.ForkPlusDialogWindow
    {
        private readonly GitModule _gitModule;
        private readonly StashRevision[] _stashes;
        private readonly Action<GitCommandResult>? _onCompleted;

        // 构造函数签名与 WPF 不同：用 GitModule + Action 回调替代 RepositoryUserControl
        public RemoveStashWindow(
            GitModule gitModule,
            StashRevision[] stashes,
            Action<GitCommandResult>? onCompleted = null)
        {
            ShowFooter = true;
            InitializeComponent();
            SetFooter(Footer);
            SetTitleTextBlock(TitleTextBlock);
            SetDescriptionTextBlock(DescriptionTextBlock);

            _gitModule = gitModule ?? throw new ArgumentNullException(nameof(gitModule));
            _stashes = stashes ?? throw new ArgumentNullException(nameof(stashes));
            _onCompleted = onCompleted;

            // 对照 WPF: 单个/多个 stash 分支
            if (_stashes.Length == 1)
            {
                // 对照 WPF: GitPointsContainer.Collapse(); GitPointView.Show(); GitPointView.Value = stash;
                StashesItemsControl.IsVisible = false;
                SingleStashTextBlock.IsVisible = true;
                SingleStashTextBlock.Text = _stashes[0].ReflogName ?? _stashes[0].Message ?? "(stash)";

                DialogTitle = Translate("Delete Stash");
                DialogDescription = Translate("Delete stash from your repository");
                StartPointTextBlock.Text = Translate("Stash:");
                SubmitButtonTitle = Translate("Delete");
                Title = Translate("Delete Stash");
            }
            else
            {
                // 对照 WPF: GitPointView.Collapse(); GitPointsContainer.Show(); GitPoints.ItemsSource = stashes;
                SingleStashTextBlock.IsVisible = false;
                StashesItemsControl.IsVisible = true;
                StashesItemsControl.ItemsSource = _stashes;

                DialogTitle = Translate("Delete Stashes");
                DialogDescription = Translate("Delete stashes from your repository");
                StartPointTextBlock.Text = Translate("Stashes:");
                SubmitButtonTitle = string.Format(Translate("Delete {0} stashes"), _stashes.Length);
                Title = Translate("Delete Stashes");
            }
            CancelButtonTitle = Translate("Cancel");

            RefreshCommandPreview();
        }

        // 对照 WPF: protected override string GetCommandPreview()
        private string? GetCommandPreview()
        {
            if (_stashes == null || _stashes.Length == 0)
            {
                return null;
            }
            var lines = new System.Collections.Generic.List<string>(_stashes.Length);
            foreach (StashRevision stash in _stashes)
            {
                if (stash == null || string.IsNullOrEmpty(stash.ReflogName))
                {
                    continue;
                }
                lines.Add("git stash drop " + stash.ReflogName);
            }
            if (lines.Count == 0)
            {
                return null;
            }
            return string.Join("\n", lines);
        }

        private void RefreshCommandPreview()
        {
            CommandPreviewTextBox.Text = GetCommandPreview() ?? "";
        }

        // 对照 WPF: protected override void OnSubmit()
        protected override void OnSubmit()
        {
            StashRevision[] stashes = _stashes;
            string name = (_stashes.Length > 1)
                ? string.Format(Translate("Delete {0} stashes"), _stashes.Length)
                : string.Format(Translate("Delete '{0}'"), _stashes[0].ReflogName);

            DisableEditableControls();
            SetStatus(ForkPlusDialogStatus.InProgress, Translate("Deleting..."));

            // 对照 WPF: _repositoryUserControl.JobQueue.Add(name, ...)
            // Avalonia: Task.Run + Dispatcher.UIThread.Post + JobMonitor
            Task.Run(delegate
            {
                JobMonitor monitor = new JobMonitor();
                GitCommandResult result = new RemoveStashGitCommand().Execute(_gitModule, stashes, monitor);
                GitCommandResult finalResult = result.Succeeded ? GitCommandResult.Success() : result;
                Dispatcher.UIThread.Post(delegate
                {
                    try
                    {
                        _onCompleted?.Invoke(finalResult);
                    }
                    catch (Exception ex)
                    {
                        Log.Error("RemoveStashWindow onCompleted callback failed", ex);
                    }
                    Close(finalResult);
                });
            });
        }

        // spike 版：手动禁用可编辑控件（本对话框无可编辑控件）
        private void DisableEditableControls()
        {
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

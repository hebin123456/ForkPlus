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
    // Phase 4.x：Avalonia 版 RevertRevisionWindow（真实迁移版，对照 WPF RevertRevisionWindow.xaml.cs 181 行）。
    //
    // 对照 WPF 工程 src/ForkPlus/UI/Dialogs/RevertRevisionWindow.xaml.cs：
    //   - public partial class RevertRevisionWindow : ForkPlusDialogWindow
    //   - 字段: RepositoryUserControl _repositoryUserControl / Revision _revision / Sha[] _revisionParents
    //   - 属性: bool MergeRevision => _revisionParents.Length > 1
    //   - 构造函数 (RepositoryUserControl, Revision revision, Sha[] revisionParents)
    //     * 调 GetRevisionsGitCommand 拿 parent revisions 填充 ComboBox（merge commit 情况）
    //     * 调 RevertTestGitCommand 做冲突预检（三态：Success / Warning / Unknown 不显示）
    //   - IsSubmitAllowed: merge commit 时 ComboBox 必须有选中项
    //   - GetCommandPreview: "git revert [--no-commit] [-m N] {sha}"
    //   - OnSubmit: RevertCommitGitCommand + 可选 UpdateSubmodulesGitCommand → Close(result)
    //
    // Avalonia 版差异（spike）：
    //   1. spike 模式：构造函数 SetFooter/Footer 注入
    //   2. RepositoryUserControl 参数 → GitModule + RepositoryReferences? + SubmodulesToUpdate
    //      + Action<GitCommandResult>? onCompleted 回调
    //   3. GitPointView 自定义控件 → spike 版用 TextBlock 显示 revision.FriendlyName 简化
    //   4. BindableGitPointView (ComboBox ItemTemplate) → spike 版用 TextBlock 绑定 Message
    //      （Revision.FriendlyName 是 IGitPoint 显式接口实现，无法直接 Binding；Message 是 public 属性，
    //       Revision 类中 IGitPoint.FriendlyName => Message，两者返回值相同）
    //   5. spike 基类不提供 GetCommandPreview/RefreshCommandPreview → 自行维护 CommandPreviewTextBox
    //   6. spike 基类不提供 DisableEditableControls → 手动禁用 ComboBox + CheckBox
    //   7. JobQueue + Dispatcher.Async → Task.Run + Dispatcher.UIThread.Post + JobMonitor
    //   8. CheckBox Checked/Unchecked 事件 → IsCheckedChanged 事件
    //   9. ComboBox SelectionChanged 事件参数 → Avalonia.Controls.SelectionChangedEventArgs
    public partial class RevertRevisionWindow : global::ForkPlus.Avalonia.Dialogs.ForkPlusDialogWindow
    {
        private readonly GitModule _gitModule;
        private readonly Revision _revision;
        private Sha[] _revisionParents;
        private readonly SubmodulesToUpdate _submodulesToUpdate;
        private readonly Action<GitCommandResult>? _onCompleted;

        private bool MergeRevision => _revisionParents != null && _revisionParents.Length > 1;

        // 构造函数签名与 WPF 不同：注入 GitModule + RepositoryReferences + SubmodulesToUpdate
        // + Action 回调替代 RepositoryUserControl 依赖
        public RevertRevisionWindow(
            GitModule gitModule,
            Revision revision,
            Sha[] revisionParents,
            SubmodulesToUpdate submodulesToUpdate = default,
            Action<GitCommandResult>? onCompleted = null)
        {
            ShowFooter = true;
            InitializeComponent();
            SetFooter(Footer);
            SetTitleTextBlock(TitleTextBlock);
            SetDescriptionTextBlock(DescriptionTextBlock);

            _gitModule = gitModule ?? throw new ArgumentNullException(nameof(gitModule));
            _revision = revision ?? throw new ArgumentNullException(nameof(revision));
            _revisionParents = revisionParents ?? Array.Empty<Sha>();
            _submodulesToUpdate = submodulesToUpdate;
            _onCompleted = onCompleted;

            // 对照 WPF: DialogTitle / DialogDescription / SubmitButtonTitle
            DialogTitle = Translate("Revert");
            DialogDescription = Translate("Revert changes of the individual commit");
            SubmitButtonTitle = Translate("Revert");
            CancelButtonTitle = Translate("Cancel");
            Title = Translate("Revert");

            // 对照 WPF: RevisionGitPointView.Value = revision;
            // Avalonia spike: 用 TextBlock 显示 revision.FriendlyName 简化
            RevisionGitPointTextBlock.Text = ((IGitPoint)revision).FriendlyName ?? revision.Sha.ToAbbreviatedString();

            // 对照 WPF: CommitCheckBox.IsChecked = true;
            CommitCheckBox.IsChecked = true;

            if (MergeRevision)
            {
                // 对照 WPF: GetRevisionsGitCommand 拿 parent revisions 填充 ComboBox
                GitCommandResult<Revision[]> parentsResult = new GetRevisionsGitCommand().Execute(_gitModule, _revisionParents);
                if (!parentsResult.Succeeded)
                {
                    Log.Error(parentsResult.Error.FriendlyDescription);
                    return;
                }
                Revision[] parents = parentsResult.Result;
                if (parents.Length <= 1)
                {
                    return;
                }
                RevisionParentComboBox.ItemsSource = parents;
                RevisionParentComboBox.SelectedIndex = 0;
                // 对照 WPF: RevisionParentTextBlock.Visibility = Visible; RevisionParentComboBox.Visibility = Visible;
                RevisionParentTextBlock.IsVisible = true;
                RevisionParentComboBox.IsVisible = true;
            }
            else
            {
                // 对照 WPF: RevisionParentTextBlock.Visibility = Collapsed; RevisionParentComboBox.Visibility = Collapsed;
                RevisionParentTextBlock.IsVisible = false;
                RevisionParentComboBox.IsVisible = false;
            }

            UpdateSubmitButton();

            // 对照 WPF: Revert 冲突预检（RevertTestGitCommand）
            int? previewParentNumber = MergeRevision ? new int?(1) : null;
            GitCommandResult<RevertTestGitCommand.TestResult> previewResult =
                new RevertTestGitCommand().Execute(_gitModule, _revision.Sha, previewParentNumber);
            if (previewResult.Succeeded)
            {
                if (previewResult.Result == RevertTestGitCommand.TestResult.Success)
                {
                    SetStatus(ForkPlusDialogStatus.Success, Translate("Revert can be done without conflicts"));
                }
                else if (previewResult.Result == RevertTestGitCommand.TestResult.Conflict)
                {
                    SetStatus(ForkPlusDialogStatus.Warning, Translate("Revert will cause conflicts"));
                }
                // Unknown 不显示状态（对照 WPF）
            }

            RefreshCommandPreview();
        }

        // 对照 WPF: protected override bool IsSubmitAllowed
        protected override bool IsSubmitAllowed
        {
            get
            {
                if (MergeRevision)
                {
                    return RevisionParentComboBox.SelectedItem != null && base.IsSubmitAllowed;
                }
                return base.IsSubmitAllowed;
            }
        }

        // 对照 WPF: protected override string GetCommandPreview()
        private string GetCommandPreview()
        {
            if (_revision == null)
            {
                return null;
            }
            var parts = new System.Collections.Generic.List<string> { "git", "revert" };
            bool commit = CommitCheckBox.IsChecked.GetValueOrDefault();
            if (!commit)
            {
                parts.Add("--no-commit");
            }
            if (MergeRevision)
            {
                int parentNumber = RevisionParentComboBox.SelectedIndex + 1;
                if (parentNumber > 0)
                {
                    parts.Add("-m " + parentNumber.ToString());
                }
            }
            parts.Add(_revision.Sha.ToAbbreviatedString());
            return string.Join(" ", parts);
        }

        private void RefreshCommandPreview()
        {
            if (CommandPreviewTextBox == null) return;
            string preview = GetCommandPreview();
            CommandPreviewTextBox.Text = preview ?? string.Empty;
        }

        // 对照 WPF: protected override void OnSubmit()
        protected override void OnSubmit()
        {
            GitModule gitModule = _gitModule;
            if (gitModule == null)
            {
                return;
            }
            Sha shaToRevert = _revision.Sha;
            bool commit = CommitCheckBox.IsChecked.GetValueOrDefault();
            int? parentNumber = MergeRevision ? new int?(RevisionParentComboBox.SelectedIndex + 1) : null;
            SubmodulesToUpdate submodulesToUpdate = _submodulesToUpdate;

            DisableEditableControls();
            SetStatus(ForkPlusDialogStatus.InProgress, Translate("Reverting..."));

            // 对照 WPF: _repositoryUserControl.AddUndoable(FormatTranslate("Revert '{0}'", ...), ...)
            // Avalonia: Task.Run + Dispatcher.UIThread.Post + JobMonitor（spike 版不做 undo）
            Task.Run(delegate
            {
                JobMonitor monitor = new JobMonitor();
                GitCommandResult revertResult = new RevertCommitGitCommand().Execute(gitModule, shaToRevert, commit, parentNumber, monitor);
                GitCommandResult updateSubmodulesResult = GitCommandResult.Success();
                if (submodulesToUpdate.Length > 0)
                {
                    Dispatcher.UIThread.Post(delegate
                    {
                        SetStatus(ForkPlusDialogStatus.InProgress, Translate("Updating submodules..."));
                    });
                    updateSubmodulesResult = new UpdateSubmodulesGitCommand().Execute(gitModule, submodulesToUpdate, monitor);
                }
                GitCommandResult finalResult = revertResult.Succeeded ? updateSubmodulesResult : revertResult;
                Dispatcher.UIThread.Post(delegate
                {
                    try { _onCompleted?.Invoke(finalResult); } catch (Exception ex) { Log.Error("RevertRevisionWindow onCompleted callback failed", ex); }
                    Close(finalResult);
                });
            });
        }

        // 对照 WPF: CommitCheckBox_Changed（WPF 用 Checked/Unchecked 两个事件，Avalonia 用 IsCheckedChanged）
        public void CommitCheckBox_Changed(object? sender, RoutedEventArgs e)
        {
            RefreshCommandPreview();
        }

        // 对照 WPF: RevisionParentComboBox_SelectionChanged
        public void RevisionParentComboBox_SelectionChanged(object? sender, SelectionChangedEventArgs e)
        {
            UpdateSubmitButton();
            RefreshCommandPreview();
        }

        // spike 版：手动禁用可编辑控件（基类不提供 DisableEditableControls）
        private void DisableEditableControls()
        {
            RevisionParentComboBox.IsEnabled = false;
            CommitCheckBox.IsEnabled = false;
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

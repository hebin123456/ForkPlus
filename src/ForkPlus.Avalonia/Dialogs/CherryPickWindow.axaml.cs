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
    // Avalonia spike 版 CherryPickWindow（对照 WPF CherryPickWindow.xaml.cs 230 行）。
    //
    // 对照 WPF 工程 src/ForkPlus/UI/Dialogs/CherryPickWindow.xaml.cs：
    //   - public partial class CherryPickWindow : ForkPlusDialogWindow
    //   - 字段: RepositoryUserControl _repositoryUserControl / Revision[] _revisions / Sha[] _firstRevisionParents
    //   - MergeRevision => _firstRevisionParents.Length > 1
    //   - IsSubmitAllowed: MergeRevision 时要求 RevisionParentComboBox.SelectedItem != null
    //   - 构造函数 (RepositoryUserControl, Revision[] revisions, Sha[] firstRevisionParents)
    //     - 单 commit: GitPointTextBlock "Commit to apply:" + RevisionGitPointView.Show() + GitPointsContainer.Collapse()
    //       + MergeRevision: GetRevisionsGitCommand.Execute(parents) → RevisionParentComboBox.ItemsSource + .Show()
    //     - 多 commit: GitPointTextBlock "Commits to apply:" + RevisionGitPointView.Collapse() + GitPointsContainer.Show()
    //     - AppendOriginShaCheckBox.IsChecked = ForkPlusSettings.Default.CherryPick_AppendOriginSha
    //     - CommitCheckBox.IsChecked = true
    //     - CherryPickTestGitCommand.Execute → SetStatus(Success/Warning)
    //   - GetCommandPreview: "git cherry-pick [--no-commit] [-x] [-m N] <sha>..."
    //   - OnSubmit: CherryPickGitCommand + UpdateSubmodulesGitCommand
    //   - RefreshAppendOriginShaCheckBox: CommitCheckBox unchecked 时禁用 AppendOriginShaCheckBox
    //
    // Avalonia 版差异（spike）：
    //   1. spike 模式：构造函数 SetFooter/Footer 注入
    //   2. RepositoryUserControl 参数 → GitModule + Action<GitCommandResult>? onCompleted 回调
    //   3. GitPointView/BindableGitPointView 自定义控件 → spike 版用 TextBlock 显示 commit subject 简化
    //   4. spike 基类不提供 GetCommandPreview/RefreshCommandPreview → 自行维护 CommandPreviewTextBox
    //   5. spike 基类不提供 DisableEditableControls → 手动禁用 CheckBox + ComboBox
    //   6. JobQueue + Dispatcher.Async → Task.Run + Dispatcher.UIThread.Post + JobMonitor
    //   7. CheckBox Checked/Unchecked 事件 → IsCheckedChanged 事件
    //   8. ComboBox SelectionChanged 参数：Avalonia.Controls.SelectionChangedEventArgs
    //   9. Collapse()/Show() 扩展方法 → IsVisible = false/true
    //  10. spike 简化：跳过 submodule 更新，仅执行核心 CherryPickGitCommand
    public partial class CherryPickWindow : global::ForkPlus.Avalonia.Dialogs.ForkPlusDialogWindow
    {
        private readonly GitModule _gitModule;
        private readonly Revision[] _revisions;
        private readonly Sha[] _firstRevisionParents;
        private readonly Action<GitCommandResult>? _onCompleted;

        // 对照 WPF: MergeRevision => _firstRevisionParents.Length > 1
        private bool MergeRevision => _firstRevisionParents != null && _firstRevisionParents.Length > 1;

        // 对照 WPF: protected override bool IsSubmitAllowed
        protected override bool IsSubmitAllowed
        {
            get
            {
                if (!base.IsSubmitAllowed)
                {
                    return false;
                }
                if (MergeRevision)
                {
                    return RevisionParentComboBox.SelectedItem != null;
                }
                return true;
            }
        }

        // 构造函数签名与 WPF 不同：用 GitModule + Action 回调替代 RepositoryUserControl
        public CherryPickWindow(
            GitModule gitModule,
            Revision[] revisions,
            Sha[] firstRevisionParents,
            Action<GitCommandResult>? onCompleted = null)
        {
            ShowFooter = true;
            InitializeComponent();
            SetFooter(Footer);
            SetTitleTextBlock(TitleTextBlock);
            SetDescriptionTextBlock(DescriptionTextBlock);

            _gitModule = gitModule ?? throw new ArgumentNullException(nameof(gitModule));
            _revisions = revisions ?? throw new ArgumentNullException(nameof(revisions));
            _firstRevisionParents = firstRevisionParents ?? Array.Empty<Sha>();
            _onCompleted = onCompleted;

            // 对照 WPF: DialogTitle / DialogDescription
            DialogTitle = Translate("Cherry Pick");
            DialogDescription = Translate("Apply changes of the individual commit");
            CancelButtonTitle = Translate("Cancel");
            Title = Translate("Cherry Pick");

            // 对照 WPF: AppendOriginShaCheckBox.IsChecked = ForkPlusSettings.Default.CherryPick_AppendOriginSha;
            AppendOriginShaCheckBox.IsChecked = ForkPlusSettings.Default.CherryPick_AppendOriginSha;

            // 对照 WPF: 单 commit / 多 commit 分支处理
            Revision singleRevision = _revisions.SingleItem();
            if (singleRevision != null)
            {
                GitPointTextBlock.Text = Translate("Commit to apply:");
                // 对照 WPF: GitPointsContainer.Collapse(); RevisionGitPointView.Show();
                GitPointsContainer.IsVisible = false;
                RevisionGitPointView.IsVisible = true;
                RevisionGitPointView.Text = GetRevisionDisplay(singleRevision);

                if (MergeRevision)
                {
                    // 对照 WPF: GetRevisionsGitCommand.Execute(gitModule, _firstRevisionParents)
                    var parentsResult = new GetRevisionsGitCommand().Execute(_gitModule, _firstRevisionParents);
                    if (!parentsResult.Succeeded)
                    {
                        Log.Error(parentsResult.Error.FriendlyDescription);
                        return;
                    }
                    Revision[] parentRevisions = parentsResult.Result;
                    if (parentRevisions.Length <= 1)
                    {
                        return;
                    }
                    RevisionParentComboBox.ItemsSource = parentRevisions;
                    RevisionParentComboBox.SelectedIndex = 0;
                    // 对照 WPF: RevisionParentTextBlock.Show(); RevisionParentComboBox.Show();
                    RevisionParentTextBlock.IsVisible = true;
                    RevisionParentComboBox.IsVisible = true;
                }
                SubmitButtonTitle = Translate("Cherry Pick");
            }
            else
            {
                GitPointTextBlock.Text = Translate("Commits to apply:");
                // 对照 WPF: RevisionGitPointView.Collapse(); GitPointsContainer.Show();
                RevisionGitPointView.IsVisible = false;
                RevisionParentTextBlock.IsVisible = false;
                RevisionParentComboBox.IsVisible = false;
                GitPointsContainer.IsVisible = true;
                GitPoints.ItemsSource = _revisions;
                SubmitButtonTitle = string.Format(Translate("Cherry Pick {0} commits"), _revisions.Length);
            }

            // 对照 WPF: CommitCheckBox.IsChecked = true; UpdateSubmitButton();
            CommitCheckBox.IsChecked = true;
            UpdateSubmitButton();

            // 对照 WPF: CherryPickTestGitCommand.Execute(...) 三态预检
            Sha[] previewShas = _revisions.Map((Revision x) => x.Sha);
            int? previewParentNumber = MergeRevision ? new int?(1) : null;
            var previewResult = new CherryPickTestGitCommand().Execute(_gitModule, previewShas, previewParentNumber);
            if (previewResult.Succeeded)
            {
                if (previewResult.Result == CherryPickTestGitCommand.TestResult.Success)
                {
                    SetStatus(ForkPlusDialogStatus.Success, Translate("Cherry-pick can be done without conflicts"));
                }
                else if (previewResult.Result == CherryPickTestGitCommand.TestResult.Conflict)
                {
                    SetStatus(ForkPlusDialogStatus.Warning, Translate("Cherry-pick will cause conflicts"));
                }
            }

            RefreshAppendOriginShaCheckBox();
            RefreshCommandPreview();
        }

        // spike 版：从 Revision 取展示文本（subject 优先，避免 Message 多行污染单行 TextBlock）
        private static string GetRevisionDisplay(Revision revision)
        {
            if (revision == null) return string.Empty;
            revision.MessageParts(out string subject, out _);
            return subject ?? revision.Message ?? string.Empty;
        }

        // 对照 WPF: protected override string GetCommandPreview()
        private string? GetCommandPreview()
        {
            if (_revisions == null || _revisions.Length == 0)
            {
                return null;
            }
            var parts = new List<string> { "git", "cherry-pick" };
            bool commit = CommitCheckBox.IsChecked.GetValueOrDefault();
            bool appendOriginSha = AppendOriginShaCheckBox.IsChecked.GetValueOrDefault();
            if (!commit)
            {
                parts.Add("--no-commit");
            }
            if (appendOriginSha)
            {
                parts.Add("-x");
            }
            if (MergeRevision)
            {
                int parentNumber = RevisionParentComboBox.SelectedIndex + 1;
                if (parentNumber > 0)
                {
                    parts.Add("-m " + parentNumber.ToString());
                }
            }
            Sha[] shas = _revisions.Map((Revision x) => x.Sha);
            Array.Reverse(shas);
            foreach (Sha sha in shas)
            {
                parts.Add(sha.ToAbbreviatedString());
            }
            return string.Join(" ", parts);
        }

        private void RefreshCommandPreview()
        {
            if (CommandPreviewTextBox == null) return;
            CommandPreviewTextBox.Text = GetCommandPreview() ?? string.Empty;
        }

        // 对照 WPF: protected override void OnSubmit()
        protected override void OnSubmit()
        {
            Sha[] shas = _revisions.Map((Revision x) => x.Sha);
            Array.Reverse(shas);
            bool commit = CommitCheckBox.IsChecked.GetValueOrDefault();
            bool appendOriginSha = AppendOriginShaCheckBox.IsChecked.GetValueOrDefault();
            int? parentNumber = MergeRevision ? new int?(RevisionParentComboBox.SelectedIndex + 1) : null;

            // 对照 WPF: ForkPlusSettings.Default.CherryPick_AppendOriginSha = appendOriginSha;
            ForkPlusSettings.Default.CherryPick_AppendOriginSha = appendOriginSha;
            ForkPlusSettings.Default.Save();

            DisableEditableControls();
            SetStatus(ForkPlusDialogStatus.InProgress, Translate("Cherry-picking..."));

            // 对照 WPF: _repositoryUserControl.AddUndoable(Translate("Cherry-pick"), ...) + base.Dispatcher.Async(Close)
            // Avalonia spike: Task.Run + Dispatcher.UIThread.Post + JobMonitor
            // 简化：仅执行核心 CherryPickGitCommand，跳过 submodule 更新
            Task.Run(delegate
            {
                JobMonitor monitor = new JobMonitor();
                GitCommandResult result = new CherryPickGitCommand().Execute(
                    _gitModule, shas, commit, appendOriginSha, parentNumber, monitor);
                Dispatcher.UIThread.Post(delegate
                {
                    try
                    {
                        _onCompleted?.Invoke(result);
                    }
                    catch (Exception ex)
                    {
                        Log.Error("CherryPickWindow onCompleted callback failed", ex);
                    }
                    Close(result);
                });
            });
        }

        // 对照 WPF: CommitCheckBox_Changed
        public void CommitCheckBox_Changed(object? sender, RoutedEventArgs e)
        {
            RefreshAppendOriginShaCheckBox();
            RefreshCommandPreview();
        }

        // 对照 WPF: AppendOriginShaCheckBox_Changed
        public void AppendOriginShaCheckBox_Changed(object? sender, RoutedEventArgs e)
        {
            RefreshCommandPreview();
        }

        // 对照 WPF: RevisionParentComboBox_SelectionChanged
        public void RevisionParentComboBox_SelectionChanged(object? sender, SelectionChangedEventArgs e)
        {
            UpdateSubmitButton();
            RefreshCommandPreview();
        }

        // 对照 WPF: RefreshAppendOriginShaCheckBox
        private void RefreshAppendOriginShaCheckBox()
        {
            if (CommitCheckBox.IsChecked.GetValueOrDefault())
            {
                AppendOriginShaCheckBox.IsEnabled = true;
            }
            else
            {
                AppendOriginShaCheckBox.IsChecked = false;
                AppendOriginShaCheckBox.IsEnabled = false;
            }
        }

        // spike 版：手动禁用可编辑控件
        private void DisableEditableControls()
        {
            CommitCheckBox.IsEnabled = false;
            AppendOriginShaCheckBox.IsEnabled = false;
            RevisionParentComboBox.IsEnabled = false;
        }

        // 对照 WPF: private static string Translate(string text)
        // → PreferencesLocalization.Translate(text, ForkPlusSettings.Default.UiLanguage)
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

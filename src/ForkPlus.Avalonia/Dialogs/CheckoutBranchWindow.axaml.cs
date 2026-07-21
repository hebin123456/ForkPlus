using System;
using System.Threading.Tasks;
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
    // Phase 4.x：Avalonia 版 CheckoutBranchWindow（真实迁移版，对照 WPF CheckoutBranchWindow.xaml.cs 308 行）。
    //
    // 对照 WPF 工程 src/ForkPlus/UI/Dialogs/CheckoutBranchWindow.xaml.cs：
    //   - public partial class CheckoutBranchWindow : ForkPlusDialogWindow
    //   - 字段: RepositoryUserControl _repositoryUserControl / LocalBranch _branch / RemoteBranch _fastForwardTo
    //   - 构造函数 (RepositoryUserControl, LocalBranch branch, RemoteBranch fastForwardTo)
    //   - GetCommandPreview: "git checkout [--force] {branch.Name}" + dirty 时 StashAndReapply 选中前置 "git stash\n"
    //   - OnSubmit: StashAndReapply 选择 → PerformCheckout
    //     (可选 SaveStashGitCommand + CheckoutBranchGitCommand + 可选 FastForwardMergeGitCommand
    //      + 可选 ApplyStashGitCommand + UpdateSubmodulesGitCommand)
    //   - KeyboardHelper.IsShiftDown 切换 StashAndReapply → "Leave as stash"（spike 简化掉）
    //
    // Avalonia 版差异（spike）：
    //   1. spike 模式：构造函数 SetFooter/Footer 注入
    //   2. RepositoryUserControl 参数 → GitModule + RepositoryReferences? + bool workingDirectoryIsDirty
    //      + SubmodulesToUpdate + Action<GitCommandResult>? onCompleted 回调
    //   3. GitPointView 自定义控件 → spike 版用 TextBlock 显示 branch.Name / fastForwardTo.Name 简化
    //   4. StashAndReapply 枚举（WPF ForkPlus.UI.Dialogs）→ spike 版定义本地枚举
    //   5. KeyboardHelper.IsShiftDown shift 切换逻辑 → spike 版省略（"Leave as stash" UX 不迁移）
    //   6. spike 基类不提供 GetCommandPreview/RefreshCommandPreview → 自行维护 CommandPreviewTextBox
    //   7. spike 基类不提供 DisableEditableControls → 手动禁用 RadioButtons
    //   8. JobQueue + Dispatcher.Async → Task.Run + Dispatcher.UIThread.Post + JobMonitor
    //   9. RadioButton Checked 事件 → IsCheckedChanged 事件
    public partial class CheckoutBranchWindow : global::ForkPlus.Avalonia.Dialogs.ForkPlusDialogWindow
    {
        // spike 版：本地定义 StashAndReapply 枚举（WPF 在 ForkPlus.UI.Dialogs 命名空间，Core 不可访问）
        private enum StashAndReapply
        {
            Possible,
            Required,
            Forbidden
        }

        private readonly GitModule _gitModule;
        private readonly RepositoryReferences _references;
        private readonly LocalBranch _branch;
        private readonly RemoteBranch _fastForwardTo;
        private readonly bool _workingDirectoryIsDirty;
        private readonly SubmodulesToUpdate _submodulesToUpdate;
        private readonly Action<GitCommandResult>? _onCompleted;

        // 构造函数签名与 WPF 不同：注入 GitModule + RepositoryReferences + bool workingDirectoryIsDirty
        // + SubmodulesToUpdate + Action 回调替代 RepositoryUserControl 依赖
        public CheckoutBranchWindow(
            GitModule gitModule,
            RepositoryReferences references,
            LocalBranch branch,
            RemoteBranch fastForwardTo = null,
            bool workingDirectoryIsDirty = false,
            SubmodulesToUpdate submodulesToUpdate = default,
            Action<GitCommandResult>? onCompleted = null)
        {
            ShowFooter = true;
            InitializeComponent();
            SetFooter(Footer);
            SetTitleTextBlock(TitleTextBlock);
            SetDescriptionTextBlock(DescriptionTextBlock);

            _gitModule = gitModule ?? throw new ArgumentNullException(nameof(gitModule));
            _references = references;
            _branch = branch ?? throw new ArgumentNullException(nameof(branch));
            _fastForwardTo = fastForwardTo;
            _workingDirectoryIsDirty = workingDirectoryIsDirty;
            _submodulesToUpdate = submodulesToUpdate;
            _onCompleted = onCompleted;

            // 对照 WPF: GitPointView.Value = branch;
            // Avalonia spike: 用 TextBlock 显示 branch.Name 简化
            GitPointTextBlock.Text = branch.Name;

            // 对照 WPF: fastForwardTo != null 分支
            if (fastForwardTo != null)
            {
                DialogTitle = Translate("Checkout and Fast-Forward");
                DialogDescription = Translate("Checkout local branch and fast-forward it to remote branch");
                SubmitButtonTitle = Translate("Checkout and Fast-Forward");
                // 对照 WPF: FastForwardGitPointView.Value = fastForwardTo; FastForwardTextBlock.Show(); FastForwardGitPointView.Show();
                FastForwardGitPointTextBlock.Text = fastForwardTo.Name;
                FastForwardTextBlock.IsVisible = true;
                FastForwardGitPointTextBlock.IsVisible = true;
            }
            else
            {
                DialogTitle = Translate("Checkout Branch");
                DialogDescription = Translate("Switch to another branch");
                SubmitButtonTitle = Translate("Checkout");
                // 对照 WPF: FastForwardTextBlock.Collapse(); FastForwardGitPointView.Collapse();
                FastForwardTextBlock.IsVisible = false;
                FastForwardGitPointTextBlock.IsVisible = false;
            }
            CancelButtonTitle = Translate("Cancel");
            Title = DialogTitle;

            // 对照 WPF: bool checkout_StashAndReapply = ForkPlusSettings.Default.Checkout_StashAndReapply;
            bool checkout_StashAndReapply = ForkPlusSettings.Default.Checkout_StashAndReapply;
            StashAndReapplyRadioButton.IsChecked = checkout_StashAndReapply;
            DoNotChangeRadioButton.IsChecked = !checkout_StashAndReapply;

            // 对照 WPF: if (repositoryUserControl.RepositoryStatus.WorkingDirectoryIsDirty()) {...}
            if (_workingDirectoryIsDirty)
            {
                LocalChangesTextBlock.IsVisible = true;
                LocalChangesOptionsContainer.IsVisible = true;
            }
            else
            {
                LocalChangesTextBlock.IsVisible = false;
                LocalChangesOptionsContainer.IsVisible = false;
            }

            // spike: 跳过 KeyDown/KeyUp shift 检测（spike 版省略 "Leave as stash" UX）

            RefreshCommandPreview();
        }

        // 对照 WPF: protected override string GetCommandPreview()
        private string GetCommandPreview()
        {
            LocalBranch branch = _branch;
            if (branch == null)
            {
                return null;
            }
            var parts = new System.Collections.Generic.List<string> { "git", "checkout" };
            if (DiscardRadioButton.IsChecked.GetValueOrDefault())
            {
                parts.Add("--force");
            }
            parts.Add(branch.Name);
            string command = string.Join(" ", parts);
            if (_workingDirectoryIsDirty && StashAndReapplyRadioButton.IsChecked.GetValueOrDefault())
            {
                command = "git stash\n" + command;
            }
            return command;
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
            RepositoryReferences repositoryReferences = _references;
            if (repositoryReferences == null)
            {
                return;
            }
            LocalBranch branch = _branch;
            RemoteBranch fastForwardTo = _fastForwardTo;
            SubmodulesToUpdate submodulesToUpdate = _submodulesToUpdate;
            string sourceString = repositoryReferences.ActiveBranch?.Name
                ?? repositoryReferences.HeadSha?.ToAbbreviatedString()
                ?? "";

            StashAndReapply checkoutStashAndReapply;
            bool checkoutDiscard;
            // spike: 省略 leaveAsStash（KeyboardHelper.IsShiftDown），固定为 false
            if (StashAndReapplyRadioButton.IsChecked.GetValueOrDefault())
            {
                checkoutStashAndReapply = StashAndReapply.Possible;
                checkoutDiscard = false;
            }
            else if (DoNotChangeRadioButton.IsChecked.GetValueOrDefault())
            {
                checkoutStashAndReapply = StashAndReapply.Forbidden;
                checkoutDiscard = false;
            }
            else if (DiscardRadioButton.IsChecked.GetValueOrDefault())
            {
                checkoutStashAndReapply = StashAndReapply.Forbidden;
                checkoutDiscard = true;
            }
            else
            {
                return;
            }

            // 对照 WPF: ForkPlusSettings.Default.Checkout_StashAndReapply = ...; ForkPlusSettings.Default.Save();
            ForkPlusSettings.Default.Checkout_StashAndReapply = checkoutStashAndReapply == StashAndReapply.Possible;
            ForkPlusSettings.Default.Save();

            DisableEditableControls();

            // 对照 WPF: _repositoryUserControl.AddUndoable(FormatTranslate("Checkout branch '{0}'", branch.Name), ...)
            // Avalonia: Task.Run + Dispatcher.UIThread.Post + JobMonitor（spike 版不做 undo）
            Task.Run(delegate
            {
                JobMonitor monitor = new JobMonitor();
                GitCommandResult result = PerformCheckout(
                    gitModule, branch, fastForwardTo,
                    checkoutStashAndReapply, checkoutDiscard,
                    sourceString, submodulesToUpdate, monitor);
                if (monitor.IsCanceled)
                {
                    Dispatcher.UIThread.Post(delegate
                    {
                        try { _onCompleted?.Invoke(GitCommandResult.Success()); } catch (Exception ex) { Log.Error("CheckoutBranchWindow onCompleted callback failed", ex); }
                        Close(GitCommandResult.Success());
                    });
                    return;
                }
                Dispatcher.UIThread.Post(delegate
                {
                    try { _onCompleted?.Invoke(result); } catch (Exception ex) { Log.Error("CheckoutBranchWindow onCompleted callback failed", ex); }
                    Close(result);
                });
            });
        }

        // 对照 WPF: private GitCommandResult PerformCheckout(...)
        private GitCommandResult PerformCheckout(
            GitModule gitModule,
            LocalBranch branch,
            RemoteBranch fastForwardTo,
            StashAndReapply stashAndReapply,
            bool discardLocalChanges,
            string sourceString,
            SubmodulesToUpdate submodulesToUpdate,
            JobMonitor monitor)
        {
            monitor.SetState(JobMonitorState.InProgress);
            if (stashAndReapply == StashAndReapply.Required)
            {
                Dispatcher.UIThread.Post(delegate
                {
                    SetStatus(ForkPlusDialogStatus.InProgress, Translate("Stashing..."));
                });
                if (monitor.IsCanceled)
                {
                    return GitCommandResult.Failure(new GitCommandError.Cancelled());
                }
                GitCommandResult<bool> stashResult = new SaveStashGitCommand().Execute(
                    gitModule,
                    $"Autostash. Switch from '{sourceString}' to '{branch.Name}' {DateTime.Now}",
                    false,
                    monitor);
                if (!stashResult.Succeeded)
                {
                    return GitCommandResult.Failure(stashResult.Error);
                }
            }
            if (monitor.IsCanceled)
            {
                return GitCommandResult.Failure(new GitCommandError.Cancelled());
            }
            Dispatcher.UIThread.Post(delegate
            {
                SetStatus(ForkPlusDialogStatus.InProgress, Translate("Checkout..."));
            });
            GitCommandResult checkoutResult = new CheckoutBranchGitCommand().Execute(gitModule, branch, monitor, discardLocalChanges);
            if (monitor.IsCanceled)
            {
                return GitCommandResult.Failure(new GitCommandError.Cancelled());
            }
            if (!checkoutResult.Succeeded)
            {
                if (checkoutResult.Error is GitCommandError.CheckoutLocalChangesWouldBeOverwritten
                    && stashAndReapply == StashAndReapply.Possible)
                {
                    monitor.AppendOutputLine("fork: failed to checkout without overwriting local changes. Trying again with stash and reapply...\n");
                    return PerformCheckout(gitModule, branch, fastForwardTo, StashAndReapply.Required, discardLocalChanges, sourceString, submodulesToUpdate, monitor);
                }
                UpdateSubmodulesIfNeeded(gitModule, submodulesToUpdate, monitor);
                return checkoutResult;
            }
            if (fastForwardTo != null)
            {
                if (monitor.IsCanceled)
                {
                    return GitCommandResult.Failure(new GitCommandError.Cancelled());
                }
                GitCommandResult ffResult = new FastForwardMergeGitCommand().Execute(gitModule, fastForwardTo, monitor);
                if (!ffResult.Succeeded)
                {
                    if (ffResult.Error is GitCommandError.MergeLocalChangesWouldBeOverwritten
                        && stashAndReapply == StashAndReapply.Possible)
                    {
                        monitor.AppendOutputLine("fork: failed to fast-forward overwriting touching local changes. Trying again with stash and reapply...\n");
                        return PerformCheckout(gitModule, branch, fastForwardTo, StashAndReapply.Required, discardLocalChanges, sourceString, submodulesToUpdate, monitor);
                    }
                    UpdateSubmodulesIfNeeded(gitModule, submodulesToUpdate, monitor);
                    return ffResult;
                }
            }
            if (stashAndReapply == StashAndReapply.Required)
            {
                if (monitor.IsCanceled)
                {
                    return GitCommandResult.Failure(new GitCommandError.Cancelled());
                }
                GitCommandResult applyResult = new ApplyStashGitCommand().Execute(gitModule, "stash@{0}", true, monitor);
                if (!applyResult.Succeeded)
                {
                    UpdateSubmodulesIfNeeded(gitModule, submodulesToUpdate, monitor);
                    return applyResult;
                }
            }
            GitCommandResult updateSubmodulesResult = UpdateSubmodulesIfNeeded(gitModule, submodulesToUpdate, monitor);
            if (!updateSubmodulesResult.Succeeded)
            {
                return updateSubmodulesResult;
            }
            return checkoutResult;
        }

        // 对照 WPF: private GitCommandResult UpdateSubmodulesIfNeeded(...)
        private GitCommandResult UpdateSubmodulesIfNeeded(GitModule gitModule, SubmodulesToUpdate submodulesToUpdate, JobMonitor monitor)
        {
            if (submodulesToUpdate.Length == 0)
            {
                return GitCommandResult.Success();
            }
            if (monitor.IsCanceled)
            {
                return GitCommandResult.Failure(new GitCommandError.Cancelled());
            }
            Dispatcher.UIThread.Post(delegate
            {
                SetStatus(ForkPlusDialogStatus.InProgress, Translate("Updating submodules..."));
            });
            return new UpdateSubmodulesGitCommand().Execute(gitModule, submodulesToUpdate, monitor);
        }

        // 对照 WPF: LocalChangesOption_Changed（WPF 用 Checked 事件，Avalonia 用 IsCheckedChanged）
        public void LocalChangesOption_Changed(object? sender, RoutedEventArgs e)
        {
            DiscardWarningText.IsVisible = DiscardRadioButton.IsChecked.GetValueOrDefault();
            RefreshCommandPreview();
        }

        // spike 版：手动禁用可编辑控件（基类不提供 DisableEditableControls）
        private void DisableEditableControls()
        {
            DoNotChangeRadioButton.IsEnabled = false;
            StashAndReapplyRadioButton.IsEnabled = false;
            DiscardRadioButton.IsEnabled = false;
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

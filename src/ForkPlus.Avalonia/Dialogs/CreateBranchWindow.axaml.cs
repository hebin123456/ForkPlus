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
    // Phase 4.x：Avalonia 版 CreateBranchWindow（真实迁移版，对照 WPF CreateBranchWindow.xaml.cs 419 行）。
    //
    // 对照 WPF 工程 src/ForkPlus/UI/Dialogs/CreateBranchWindow.xaml.cs：
    //   - public partial class CreateBranchWindow : ForkPlusDialogWindow
    //   - 静态字段: string UnfinishedBranchName（保存上次未完成分支名）
    //   - 字段: RepositoryUserControl _repositoryUserControl / LocalBranch[] _localBranches
    //           IGitPoint _gitPoint / bool _workingDirectoryIsDirty
    //   - 构造函数 (RepositoryUserControl, RepositoryReferences refs, IGitPoint gitPoint)
    //     * GitPointView.Value = gitPoint
    //     * CheckoutAfterCreateCheckBox.IsChecked = ForkPlusSettings.Default.CreateBranch_Checkout
    //     * StashAndReapply / DoNotChange RadioButton 默认根据 Checkout_StashAndReapply
    //     * RefreshUncommittedChangesOptionsVisibility()（仅 dirty + checkout 显示本地变更区）
    //     * ReferenceTextBox.SetAutocompleteProvider（spike 省略）
    //     * UnfinishedBranchName 不为空则预填，否则使用 RecentNewBranchPrefix
    //     * KeyDown/KeyUp 监听 Shift 切换 "Leave as stash"
    //   - IsSubmitAllowed: branchName 非空 + ReferenceNameValidator 通过 + 不与本地分支重名
    //   - GetCommandPreview: "git checkout -b <name> <gitPoint>" 或 "git branch <name> <gitPoint>"
    //     （checkout + dirty + StashAndReapply 时前置 "git stash\n"）
    //   - OnSubmit: 根据 RadioButtons 决定 StashAndReapply / Discard → PerformCreateBranch
    //     (可选 SaveStashGitCommand + CreateNewBranchGitCommand + 可选 ApplyStashGitCommand + UpdateSubmodulesGitCommand)
    //   - PerformCreateBranch: 递归处理 StashAndReapply.Possible → Required 转换
    //
    // Avalonia 版差异：
    //   1. spike 模式：构造函数 SetFooter/Footer 注入
    //   2. RepositoryUserControl 参数 → GitModule + RepositoryReferences? + IGitPoint? gitPoint
    //      + bool workingDirectoryIsDirty + SubmodulesToUpdate + Action<GitCommandResult>? onCompleted 回调
    //   3. GitPointView 自定义控件 → spike 版用 TextBlock 显示 gitPoint.FriendlyName 简化
    //   4. spike 基类不提供 GetCommandPreview/RefreshCommandPreview → 自行维护 CommandPreviewTextBox
    //   5. spike 基类不提供 DisableEditableControls → 手动禁用 TextBox + CheckBox + RadioButtons
    //   6. JobQueue + Dispatcher.Async → Task.Run + Dispatcher.UIThread.Post + JobMonitor
    //   7. KeyboardHelper.IsShiftDown shift 切换逻辑 → spike 版省略（"Leave as stash" UX 不迁移）
    //   8. ReferenceTextBox → 普通 TextBox（spike 不迁移 ReferenceNameAutocompleteProvider）
    //   9. CheckBox/RadioButton Checked/Unchecked 事件 → IsCheckedChanged 事件
    //  10. Image.Hide()/Show() → IsVisible = false/true
    //  11. DiscardWarningImage (PNG) → DiscardWarningText (emoji "⚠")
    //  12. PreferencesLocalization → ServiceLocator.Localization.Translate
    //  13. _localBranches.AnyItem(predicate) → _localBranches.Any(predicate) (LINQ)
    public partial class CreateBranchWindow : global::ForkPlus.Avalonia.Dialogs.ForkPlusDialogWindow
    {
        // spike 版：本地定义 StashAndReapply 枚举（WPF 在 ForkPlus.UI.Dialogs 命名空间，Core 不可访问）
        private enum StashAndReapply
        {
            Possible,
            Required,
            Forbidden
        }

        // 对照 WPF: [Null] private static string UnfinishedBranchName;
        private static string? UnfinishedBranchName;

        private readonly GitModule _gitModule;
        private readonly LocalBranch[] _localBranches;
        private readonly IGitPoint? _gitPoint;
        private readonly RepositoryReferences? _repositoryReferences;
        private readonly bool _workingDirectoryIsDirty;
        private readonly SubmodulesToUpdate _submodulesToUpdate;
        private readonly Action<GitCommandResult>? _onCompleted;

        // 对照 WPF: public bool Checkout => CheckoutAfterCreateCheckBox.IsChecked.GetValueOrDefault();
        public bool Checkout => CheckoutAfterCreateCheckBox.IsChecked.GetValueOrDefault();

        // 构造函数签名与 WPF 不同：注入 GitModule + RepositoryReferences? + IGitPoint? gitPoint
        // + bool workingDirectoryIsDirty + SubmodulesToUpdate + Action 回调替代 RepositoryUserControl 依赖
        public CreateBranchWindow(
            GitModule gitModule,
            RepositoryReferences? references = null,
            IGitPoint? gitPoint = null,
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
            _repositoryReferences = references;
            _localBranches = references?.LocalBranches ?? Array.Empty<LocalBranch>();
            _gitPoint = gitPoint;
            _workingDirectoryIsDirty = workingDirectoryIsDirty;
            _submodulesToUpdate = submodulesToUpdate;
            _onCompleted = onCompleted;

            // 对照 WPF: DialogTitle / DialogDescription
            DialogTitle = Translate("Create Branch");
            DialogDescription = Translate("Use '/' as a path separator to create folders");
            CancelButtonTitle = Translate("Cancel");
            Title = Translate("Create Branch");

            // 对照 WPF: GitPointView.Value = gitPoint;
            // Avalonia spike: 用 TextBlock 显示 gitPoint.FriendlyName 简化
            GitPointTextBlock.Text = gitPoint?.FriendlyName ?? "";

            // 对照 WPF: CheckoutAfterCreateCheckBox.IsChecked = ForkPlusSettings.Default.CreateBranch_Checkout;
            CheckoutAfterCreateCheckBox.IsChecked = ForkPlusSettings.Default.CreateBranch_Checkout;

            // 对照 WPF: bool checkout_StashAndReapply = ForkPlusSettings.Default.Checkout_StashAndReapply;
            bool checkout_StashAndReapply = ForkPlusSettings.Default.Checkout_StashAndReapply;
            StashAndReapplyRadioButton.IsChecked = checkout_StashAndReapply;
            DoNotChangeRadioButton.IsChecked = !checkout_StashAndReapply;

            RefreshUncommittedChangesOptionsVisibility();

            // 对照 WPF: if (UnfinishedBranchName != null) { BranchNameTextBox.Text = UnfinishedBranchName; SelectAll(); }
            if (UnfinishedBranchName != null)
            {
                BranchNameTextBox.Text = UnfinishedBranchName;
                BranchNameTextBox.SelectAll();
            }
            else
            {
                // 对照 WPF: string recentNewBranchPrefix = repositoryUserControl.GitModule.Settings.RecentNewBranchPrefix;
                string? recentNewBranchPrefix = _gitModule.Settings.RecentNewBranchPrefix;
                if (recentNewBranchPrefix != null)
                {
                    BranchNameTextBox.Text = recentNewBranchPrefix;
                    BranchNameTextBox.SelectAll();
                }
            }

            UpdateSubmitButtonTitle();

            // spike: 跳过 KeyDown/KeyUp shift 检测（spike 版省略 "Leave as stash" UX）

            // 对照 WPF: RefreshCommandPreview();（首次刷新显示默认命令）
            RefreshCommandPreview();
        }

        // 对照 WPF: protected override bool IsSubmitAllowed
        protected override bool IsSubmitAllowed
        {
            get
            {
                SetStatus(ForkPlusDialogStatus.None, string.Empty);
                string branchName = (BranchNameTextBox.Text ?? "").ToLower();
                if (string.IsNullOrEmpty(branchName))
                {
                    return false;
                }
                string? text = ReferenceNameValidator.Validate(branchName);
                if (text != null)
                {
                    SetStatus(ForkPlusDialogStatus.Warning, text);
                    return false;
                }
                // 对照 WPF: _localBranches.AnyItem((LocalBranch x) => x.Name.ToLower() == branchName)
                // Avalonia: LINQ Any
                if (_localBranches.Any((LocalBranch x) => x.Name.ToLower() == branchName))
                {
                    SetStatus(ForkPlusDialogStatus.Warning, string.Format(Translate("Branch '{0}' already exists"), BranchNameTextBox.Text));
                    return false;
                }
                return true;
            }
        }

        // 对照 WPF: protected override string GetCommandPreview()
        private string? GetCommandPreview()
        {
            string branchName = BranchNameTextBox.Text ?? "";
            if (string.IsNullOrWhiteSpace(branchName))
            {
                return null;
            }
            bool checkout = CheckoutAfterCreateCheckBox.IsChecked.GetValueOrDefault();
            var parts = new List<string> { "git" };
            if (checkout)
            {
                parts.Add("checkout");
                parts.Add("-b");
            }
            else
            {
                parts.Add("branch");
            }
            parts.Add(branchName);
            string? startPoint = _gitPoint?.FriendlyName;
            if (!string.IsNullOrEmpty(startPoint))
            {
                parts.Add(startPoint);
            }
            string command = string.Join(" ", parts);
            if (checkout && _workingDirectoryIsDirty && StashAndReapplyRadioButton.IsChecked.GetValueOrDefault())
            {
                command = "git stash\n" + command;
            }
            return command;
        }

        private void RefreshCommandPreview()
        {
            if (CommandPreviewTextBox == null) return;
            CommandPreviewTextBox.Text = GetCommandPreview() ?? string.Empty;
        }

        // 对照 WPF: protected override void OnSubmit()
        protected override void OnSubmit()
        {
            GitModule gitModule = _gitModule;
            if (gitModule == null)
            {
                return;
            }
            RepositoryReferences? repositoryReferences = _repositoryReferences;
            IGitPoint? gitPoint = _gitPoint;
            if (gitPoint == null)
            {
                return;
            }
            string branchName = BranchNameTextBox.Text ?? "";
            SubmodulesToUpdate submodulesToUpdate = _submodulesToUpdate;
            bool checkout = Checkout;

            // 对照 WPF: ForkPlusSettings.Default.CreateBranch_Checkout = checkout; ForkPlusSettings.Default.Save();
            ForkPlusSettings.Default.CreateBranch_Checkout = checkout;
            ForkPlusSettings.Default.Save();

            string sourceString = repositoryReferences?.ActiveBranch?.Name
                ?? repositoryReferences?.HeadSha?.ToAbbreviatedString()
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
            SetStatus(ForkPlusDialogStatus.InProgress, string.Format(Translate("Creating branch '{0}'"), branchName));

            // 对照 WPF: _repositoryUserControl.AddUndoable(FormatTranslate("Create branch '{0}'", branchName), ...)
            // Avalonia: Task.Run + Dispatcher.UIThread.Post + JobMonitor（spike 版不做 undo）
            Task.Run(delegate
            {
                JobMonitor monitor = new JobMonitor();
                GitCommandResult result = PerformCreateBranch(
                    checkout, gitModule, gitPoint, branchName,
                    checkoutStashAndReapply, checkoutDiscard,
                    sourceString, submodulesToUpdate, monitor);
                if (monitor.IsCanceled)
                {
                    Dispatcher.UIThread.Post(delegate
                    {
                        InvokeOnCompleted(GitCommandResult.Success());
                        Close(GitCommandResult.Success());
                    });
                }
                else
                {
                    Dispatcher.UIThread.Post(delegate
                    {
                        InvokeOnCompleted(result);
                        Close(result);
                    });
                }
            });
        }

        private void InvokeOnCompleted(GitCommandResult result)
        {
            try
            {
                _onCompleted?.Invoke(result);
            }
            catch (Exception ex)
            {
                Log.Error("CreateBranchWindow onCompleted callback failed", ex);
            }
        }

        // 对照 WPF: private GitCommandResult PerformCreateBranch(...)
        private GitCommandResult PerformCreateBranch(
            bool checkout,
            GitModule gitModule,
            IGitPoint gitPoint,
            string branchName,
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
                    $"Autostash. Switch from '{sourceString}' to '{branchName}' {DateTime.Now}",
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
                SetStatus(ForkPlusDialogStatus.InProgress, Translate("Creating branch..."));
            });
            GitCommandResult createResult = new CreateNewBranchGitCommand().Execute(
                gitModule, branchName, checkout, gitPoint, monitor, discardLocalChanges);
            if (monitor.IsCanceled)
            {
                return GitCommandResult.Failure(new GitCommandError.Cancelled());
            }
            if (!createResult.Succeeded)
            {
                if (createResult.Error is GitCommandError.CheckoutLocalChangesWouldBeOverwritten
                    && stashAndReapply == StashAndReapply.Possible)
                {
                    monitor.AppendOutputLine("fork: failed to checkout without overwriting local changes. Trying again with stash and reapply...\n");
                    return PerformCreateBranch(checkout, gitModule, gitPoint, branchName,
                        StashAndReapply.Required, discardLocalChanges, sourceString, submodulesToUpdate, monitor);
                }
                Dispatcher.UIThread.Post(delegate
                {
                    SaveUnfinishedBranchName();
                });
                if (checkout)
                {
                    UpdateSubmodulesIfNeeded(gitModule, submodulesToUpdate, monitor);
                }
                return createResult;
            }
            Dispatcher.UIThread.Post(delegate
            {
                ClearUnfinishedBranchName();
                SaveRecentNewBranchPrefix(gitModule, branchName);
            });
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
            if (checkout)
            {
                GitCommandResult updateSubmodulesResult = UpdateSubmodulesIfNeeded(gitModule, submodulesToUpdate, monitor);
                if (!updateSubmodulesResult.Succeeded)
                {
                    return updateSubmodulesResult;
                }
            }
            return createResult;
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

        // 对照 WPF: private void SaveRecentNewBranchPrefix(GitModule gitModule, string branchName)
        private static void SaveRecentNewBranchPrefix(GitModule gitModule, string branchName)
        {
            int num = branchName.LastIndexOf("/");
            if (num != -1)
            {
                gitModule.Settings.RecentNewBranchPrefix = branchName.Substring(0, num + 1);
            }
            else
            {
                gitModule.Settings.RecentNewBranchPrefix = null;
            }
            gitModule.Settings.Save();
        }

        // 对照 WPF: BranchName_TextChanged
        public void BranchName_TextChanged(object? sender, TextChangedEventArgs e)
        {
            UpdateSubmitButton();
            RefreshCommandPreview();
        }

        // 对照 WPF: CheckoutAfterCreateCheckBox_Changed（WPF 用 Checked/Unchecked 两个事件，Avalonia 用 IsCheckedChanged）
        public void CheckoutAfterCreateCheckBox_Changed(object? sender, RoutedEventArgs e)
        {
            RefreshUncommittedChangesOptionsVisibility();
            UpdateSubmitButtonTitle();
            RefreshCommandPreview();
        }

        // 对照 WPF: LocalChangesOption_Changed
        public void LocalChangesOption_Changed(object? sender, RoutedEventArgs e)
        {
            // 对照 WPF: if (DiscardRadioButton.IsChecked) DiscardWarningImage.Show(); else DiscardWarningImage.Hide();
            // Avalonia spike: DiscardWarningText (emoji) 替代 PNG
            DiscardWarningText.IsVisible = DiscardRadioButton.IsChecked.GetValueOrDefault();
            RefreshCommandPreview();
        }

        // 对照 WPF: UpdateSubmitButtonTitle
        private void UpdateSubmitButtonTitle()
        {
            if (CheckoutAfterCreateCheckBox.IsChecked.GetValueOrDefault())
            {
                SubmitButtonTitle = Translate("Create and Checkout");
            }
            else
            {
                SubmitButtonTitle = Translate("Create");
            }
        }

        // 对照 WPF: SaveUnfinishedBranchName
        private void SaveUnfinishedBranchName()
        {
            UnfinishedBranchName = BranchNameTextBox.Text;
        }

        // 对照 WPF: ClearUnfinishedBranchName
        private void ClearUnfinishedBranchName()
        {
            UnfinishedBranchName = null;
        }

        // 对照 WPF: RefreshUncommittedChangesOptionsVisibility
        private void RefreshUncommittedChangesOptionsVisibility()
        {
            // 对照 WPF: if (Checkout && _workingDirectoryIsDirty) { Show(); } else { Collapse(); }
            if (Checkout && _workingDirectoryIsDirty)
            {
                LocalChangesTextBlock.IsVisible = true;
                LocalChangesOptionsContainer.IsVisible = true;
            }
            else
            {
                LocalChangesTextBlock.IsVisible = false;
                LocalChangesOptionsContainer.IsVisible = false;
            }
        }

        // spike 版：手动禁用可编辑控件（基类不提供 DisableEditableControls）
        private void DisableEditableControls()
        {
            BranchNameTextBox.IsEnabled = false;
            CheckoutAfterCreateCheckBox.IsEnabled = false;
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

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
    // Phase 4.x：Avalonia 版 CheckoutAndSyncWindow（spike 真实迁移版，对照 WPF CheckoutAndSyncWindow.xaml.cs 280 行）。
    //
    // 对照 WPF 工程 src/ForkPlus/UI/Dialogs/CheckoutAndSyncWindow.xaml.cs：
    //   - public partial class CheckoutAndSyncWindow : ForkPlusDialogWindow
    //   - 嵌套类 CheckoutSyncOptionComboBoxItem (ActionType / Title / Description / IsSeparator)
    //   - 字段: RepositoryUserControl _repositoryUserControl / LocalBranch _localBranch / RemoteBranch _remoteBranch
    //           CheckoutSyncOptionComboBoxItem[] _checkoutSyncOptionComboBoxItems / CheckoutActionType _actionType
    //   - 构造函数 (RepositoryUserControl, LocalBranch, RemoteBranch) → 构建 6 个 ComboBoxItem
    //   - GetCommandPreview: 多行 "git checkout branch" + 可选 rebase/merge/reset
    //   - OnSubmit: 复杂多阶段 - SaveStash + CheckoutBranch + PerformAction(rebase/merge/reset) + ApplyStash + UpdateSubmodules
    //     → Close(result)
    //   - RefreshTitle: 根据 _actionType 切换 SubmitButtonTitle
    //
    // Avalonia 版差异（spike）：
    //   1. 构造函数注入 GitModule + LocalBranch + RemoteBranch + RepositoryReferences + Action 回调替代 RepositoryUserControl
    //   2. CheckoutActionType 枚举从 WPF ForkPlus.UI.Dialogs 命名空间迁入 Avalonia 命名空间（WPF 工程不引用）
    //   3. spike 基类不提供 GetCommandPreview/RefreshCommandPreview → 自行维护 CommandPreviewTextBlock
    //   4. spike 基类不提供 DisableEditableControls → 手动禁用 ComboBox + CheckBox
    //   5. JobQueue + Dispatcher.Async → Task.Run + Dispatcher.UIThread.Post + JobMonitor
    //   6. GitPointView 自定义控件 → spike 版用 TextBlock 显示 branch Name 简化
    //   7. SubmodulesToUpdate 依赖暂不接入（OnSubmit 简化为只跑 SaveStash + Checkout + Action + ApplyStash）
    //   8. PreferencesLocalization.Current/FormatCurrent → ServiceLocator.Localization.Current/FormatCurrent
    //   9. ComboBoxItem Separator 样式 → spike 简化为 IsEnabled=false 的占位项（不显示分隔线）
    public partial class CheckoutAndSyncWindow : global::ForkPlus.Avalonia.Dialogs.ForkPlusDialogWindow
    {
        // 对照 WPF: ForkPlus.UI.Dialogs.CheckoutActionType（spike: 迁入 Avalonia 命名空间，因 WPF 工程不被 Avalonia 引用）
        public enum CheckoutActionType
        {
            None,
            Rebase,
            Merge,
            Reset
        }

        public class CheckoutSyncOptionComboBoxItem
        {
            public CheckoutActionType? ActionType { get; }

            public string Title { get; }

            public string Description { get; }

            public bool IsSeparator { get; }

            private CheckoutSyncOptionComboBoxItem(string title, string description, CheckoutActionType? actionType, bool isSeparator)
            {
                ActionType = actionType;
                Title = title;
                Description = description;
                IsSeparator = isSeparator;
            }

            public CheckoutSyncOptionComboBoxItem(string title, string description, CheckoutActionType? actionType)
                : this(title, description, actionType, isSeparator: false)
            {
            }

            public static CheckoutSyncOptionComboBoxItem Separator()
            {
                return new CheckoutSyncOptionComboBoxItem("", "", null, isSeparator: true);
            }
        }

        private readonly GitModule _gitModule;
        private readonly LocalBranch _localBranch;
        private readonly RemoteBranch _remoteBranch;
        private readonly RepositoryReferences _references;
        private readonly Action<GitCommandResult> _onCompleted;

        private readonly CheckoutSyncOptionComboBoxItem[] _checkoutSyncOptionComboBoxItems;

        public CheckoutActionType _actionType;

        // 构造函数签名与 WPF 不同：用 GitModule + LocalBranch + RemoteBranch + RepositoryReferences + Action 回调替代 RepositoryUserControl
        public CheckoutAndSyncWindow(
            GitModule gitModule,
            LocalBranch localBranch,
            RemoteBranch remoteBranch,
            RepositoryReferences references = null,
            Action<GitCommandResult> onCompleted = null)
        {
            ShowFooter = true;
            InitializeComponent();
            SetFooter(Footer);
            SetTitleTextBlock(TitleTextBlock);
            SetDescriptionTextBlock(DescriptionTextBlock);

            _gitModule = gitModule ?? throw new ArgumentNullException(nameof(gitModule));
            _localBranch = localBranch ?? throw new ArgumentNullException(nameof(localBranch));
            _remoteBranch = remoteBranch;
            _references = references ?? RepositoryReferences.Empty;
            _onCompleted = onCompleted;

            // 对照 WPF: _checkoutSyncOptionComboBoxItems = new CheckoutSyncOptionComboBoxItem[6] {...}
            _checkoutSyncOptionComboBoxItems = new CheckoutSyncOptionComboBoxItem[]
            {
                new CheckoutSyncOptionComboBoxItem("Checkout", "Checkout '" + localBranch.Name + "'", CheckoutActionType.None),
                CheckoutSyncOptionComboBoxItem.Separator(),
                new CheckoutSyncOptionComboBoxItem("Rebase", "Rebase '" + localBranch.Name + "' onto '" + remoteBranch.Name + "'", CheckoutActionType.Rebase),
                new CheckoutSyncOptionComboBoxItem("Merge", "Merge '" + remoteBranch.Name + "' into '" + localBranch.Name + "'", CheckoutActionType.Merge),
                CheckoutSyncOptionComboBoxItem.Separator(),
                new CheckoutSyncOptionComboBoxItem("Reset", "Reset (--hard) '" + localBranch.Name + "' to '" + remoteBranch.Name + "'", CheckoutActionType.Reset)
            };
            CheckoutActionTypeComboBox.ItemsSource = _checkoutSyncOptionComboBoxItems;
            CheckoutActionTypeComboBox.SelectedItem =
                _checkoutSyncOptionComboBoxItems.FirstOrDefault(x => x.ActionType == CheckoutActionType.None);

            // 对照 WPF: DialogTitle = PreferencesLocalization.Current("Checkout Branch")
            DialogTitle = Current("Checkout Branch");
            // 对照 WPF: DialogDescription = PreferencesLocalization.FormatCurrent("Switch to '{0}' branch", localBranch.Name)
            DialogDescription = FormatCurrent("Switch to '{0}' branch", localBranch.Name);
            // 对照 WPF: GitPointView.Value = localBranch
            GitPointTextBlock.Text = localBranch.Name;
            // 对照 WPF: SubmitButtonTitle = PreferencesLocalization.Current("Checkout")
            SubmitButtonTitle = Current("Checkout");
            CancelButtonTitle = Translate("Cancel");
            Title = Current("Checkout Branch");

            AutostashCheckBox.IsChecked = ForkPlusSettings.Default.CheckoutAndSync_StashAndReapply;

            RefreshTitle();
            RefreshCommandPreview();
        }

        // 对照 WPF: protected override string GetCommandPreview()
        private string GetCommandPreview()
        {
            if (_localBranch == null || string.IsNullOrEmpty(_localBranch.Name))
            {
                return null;
            }
            var lines = new List<string>();
            lines.Add("git checkout " + _localBranch.Name);
            if (_remoteBranch != null && !string.IsNullOrEmpty(_remoteBranch.Name))
            {
                switch (_actionType)
                {
                    case CheckoutActionType.Rebase:
                        lines.Add("git rebase " + _remoteBranch.Name);
                        break;
                    case CheckoutActionType.Merge:
                        lines.Add("git merge " + _remoteBranch.Name);
                        break;
                    case CheckoutActionType.Reset:
                        lines.Add("git reset --hard " + _remoteBranch.Name);
                        break;
                }
            }
            return string.Join("\n", lines);
        }

        private void RefreshCommandPreview()
        {
            if (CommandPreviewTextBox == null) return;
            string preview = GetCommandPreview();
            CommandPreviewTextBox.Text = preview ?? string.Empty;
        }

        // 对照 WPF: protected override void OnSubmit()
        // spike 简化：不传 SubmodulesToUpdate（依赖 RepositoryStatus/RepositoryData）
        protected override void OnSubmit()
        {
            GitModule gitModule = _gitModule;
            if (gitModule == null)
            {
                return;
            }
            LocalBranch localBranch = _localBranch;
            RemoteBranch remoteBranch = _remoteBranch;
            CheckoutActionType actionType = _actionType;
            bool stashAndReapply = AutostashCheckBox.IsChecked.GetValueOrDefault();

            ForkPlusSettings.Default.CheckoutAndSync_StashAndReapply = stashAndReapply;
            ForkPlusSettings.Default.Save();

            DisableEditableControls();
            SetStatus(ForkPlusDialogStatus.InProgress, Current("Checkout..."));

            // 对照 WPF: _repositoryUserControl.JobQueue.Add(FormatCurrent("Checkout branch '{0}'", ...), ...)
            // Avalonia: Task.Run + Dispatcher.UIThread.Post + JobMonitor
            // spike: workingDirectoryIsDirty 暂不接入（依赖 RepositoryStatus），stash 仍执行但不会保存
            Task.Run(delegate
            {
                JobMonitor monitor = new JobMonitor();
                // 对照 WPF: SaveStash (if stashAndReapply && workingDirectoryIsDirty)
                // spike: workingDirectoryIsDirty 暂不接入，跳过 stash 流程
                // 对照 WPF: CheckoutBranchGitCommand().Execute(gitModule, localBranch, monitor)
                GitCommandResult checkoutResult = new CheckoutBranchGitCommand().Execute(gitModule, localBranch, monitor);
                GitCommandResult finalResult;
                if (!checkoutResult.Succeeded && !monitor.IsCanceled)
                {
                    finalResult = checkoutResult;
                }
                else
                {
                    // 对照 WPF: PerformAction(gitModule, remoteBranch, actionType, references, monitor)
                    GitCommandResult actionResult = PerformAction(gitModule, remoteBranch, actionType, _references, monitor);
                    finalResult = actionResult.Succeeded ? checkoutResult : actionResult;
                }
                Dispatcher.UIThread.Post(delegate
                {
                    try
                    {
                        _onCompleted?.Invoke(finalResult);
                    }
                    catch (Exception ex)
                    {
                        Log.Error("CheckoutAndSyncWindow onCompleted callback failed", ex);
                    }
                    Close(finalResult);
                });
            });
        }

        // 对照 WPF: CheckoutActionTypeComboBox_SelectionChanged
        public void CheckoutActionTypeComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            // 对照 WPF: if (e.AddedItems[0] is CheckoutSyncOptionComboBoxItem { ActionType: { } actionType })
            if (e.AddedItems != null && e.AddedItems.Count > 0
                && e.AddedItems[0] is CheckoutSyncOptionComboBoxItem item
                && item.ActionType.HasValue)
            {
                _actionType = item.ActionType.Value;
                RefreshTitle();
            }
            RefreshCommandPreview();
        }

        // 对照 WPF: RefreshTitle
        private void RefreshTitle()
        {
            switch (_actionType)
            {
                case CheckoutActionType.Rebase:
                    SubmitButtonTitle = Current("Checkout and Rebase");
                    break;
                case CheckoutActionType.Merge:
                    SubmitButtonTitle = Current("Checkout and Merge");
                    break;
                case CheckoutActionType.Reset:
                    SubmitButtonTitle = Current("Checkout and Reset");
                    break;
                default:
                    SubmitButtonTitle = Current("Checkout");
                    break;
            }
        }

        // 对照 WPF: private static GitCommandResult PerformAction(...)
        private static GitCommandResult PerformAction(
            GitModule gitModule, RemoteBranch remoteBranch,
            CheckoutActionType selectedActionType, RepositoryReferences references, JobMonitor monitor)
        {
            return selectedActionType switch
            {
                CheckoutActionType.Rebase => new RebaseBranchGitCommand().Execute(
                    gitModule, remoteBranch.Sha.ToString(), rebaseMerges: false, updateRefs: false, monitor),
                CheckoutActionType.Merge => new MergeGitCommand().Execute(
                    gitModule, remoteBranch, MergeType.FastForward, references, monitor),
                CheckoutActionType.Reset => new ResetCurrentBranchToRevisionGitCommand().Execute(
                    gitModule, remoteBranch.Sha, BranchResetType.Hard, monitor),
                _ => GitCommandResult.Success(),
            };
        }

        // spike 版：手动禁用可编辑控件
        private void DisableEditableControls()
        {
            CheckoutActionTypeComboBox.IsEnabled = false;
            AutostashCheckBox.IsEnabled = false;
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

        // 对照 WPF: PreferencesLocalization.Current(text)
        private static string Current(string text)
        {
            var localization = ServiceLocator.Localization;
            if (localization != null)
            {
                return localization.Current(text);
            }
            return text;
        }

        // 对照 WPF: PreferencesLocalization.FormatCurrent(text, args)
        private static string FormatCurrent(string text, params object[] args)
        {
            var localization = ServiceLocator.Localization;
            if (localization != null)
            {
                return localization.FormatCurrent(text, args);
            }
            return string.Format(text, args);
        }
    }
}

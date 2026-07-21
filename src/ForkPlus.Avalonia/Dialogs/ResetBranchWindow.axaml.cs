using System;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using ForkPlus;
using ForkPlus.Git;
using ForkPlus.Git.Commands;
using ForkPlus.Jobs;
using ForkPlus.Services;

namespace ForkPlus.Avalonia.Dialogs
{
    // Avalonia spike 版 ResetBranchWindow（对照 WPF ResetBranchWindow.xaml.cs 162 行）。
    //
    // 对照 WPF 工程 src/ForkPlus/UI/Dialogs/ResetBranchWindow.xaml.cs：
    //   - public partial class ResetBranchWindow : ForkPlusDialogWindow
    //   - 字段: RepositoryUserControl _repositoryUserControl / Revision _destination / LocalBranch _branch
    //           / BranchResetType _resetType = BranchResetType.Mixed
    //   - 构造函数 (RepositoryUserControl, LocalBranch activeBranch, Revision destination)
    //     - activeBranch != null: "Reset Current Branch to Revision" / "Move the '{0}' branch HEAD..."
    //       ActiveBranchGitPointView.Value = activeBranch
    //     - activeBranch == null: "Reset HEAD to Revision" / "Move HEAD to the selected revision"
    //       ActiveBranchGitPointView.Value = new SymbolicReference("HEAD")
    //     - DestinationGitPointView.Value = _destination
    //   - GetCommandPreview: "git reset {--soft|--mixed|--hard} <sha>"
    //   - OnKeyDown: S/M/H 切换 ResetTypeCombobox.SelectedIndex
    //   - OnSubmit: ResetCurrentBranchToRevisionGitCommand + (Hard 时) UpdateSubmodulesGitCommand
    //   - ResetTypeCombobox_SelectionChanged: 从 ComboBoxItem.Tag 取 BranchResetType
    //   - GetResetTypeName 静态方法
    //
    // Avalonia 版差异（spike）：
    //   1. spike 模式：构造函数 SetFooter/Footer 注入
    //   2. RepositoryUserControl 参数 → GitModule + Action<GitCommandResult>? onCompleted 回调
    //   3. GitPointView 自定义控件 → spike 版用 TextBlock 显示分支名/SHA 简化
    //   4. spike 基类不提供 GetCommandPreview/RefreshCommandPreview → 自行维护 CommandPreviewTextBox
    //   5. spike 基类不提供 DisableEditableControls → 手动禁用 ComboBox
    //   6. JobQueue + Dispatcher.Async → Task.Run + Dispatcher.UIThread.Post + JobMonitor
    //   7. ComboBox SelectionChanged 参数：Avalonia.Controls.SelectionChangedEventArgs
    //   8. spike 简化：跳过 submodule 更新（RepositoryUserControl.SubmodulesToUpdate 不可用），
    //      仅执行核心 ResetCurrentBranchToRevisionGitCommand
    public partial class ResetBranchWindow : global::ForkPlus.Avalonia.Dialogs.ForkPlusDialogWindow
    {
        private readonly GitModule _gitModule;
        private readonly LocalBranch? _branch;
        private readonly Revision _destination;
        private readonly Action<GitCommandResult>? _onCompleted;
        private BranchResetType _resetType = BranchResetType.Mixed;

        // 构造函数签名与 WPF 不同：用 GitModule + Action 回调替代 RepositoryUserControl
        public ResetBranchWindow(
            GitModule gitModule,
            LocalBranch? activeBranch,
            Revision destination,
            Action<GitCommandResult>? onCompleted = null)
        {
            ShowFooter = true;
            InitializeComponent();
            SetFooter(Footer);
            SetTitleTextBlock(TitleTextBlock);
            SetDescriptionTextBlock(DescriptionTextBlock);

            _gitModule = gitModule ?? throw new ArgumentNullException(nameof(gitModule));
            _branch = activeBranch;
            _destination = destination ?? throw new ArgumentNullException(nameof(destination));
            _onCompleted = onCompleted;

            // 对照 WPF: activeBranch != null / null 分支处理
            if (activeBranch != null)
            {
                DialogTitle = Translate("Reset Current Branch to Revision");
                DialogDescription = FormatTranslate("Move the '{0}' branch HEAD to the selected revision", activeBranch.Name);
                ActiveBranchTextBlock.Text = activeBranch.Name;
            }
            else
            {
                DialogTitle = Translate("Reset HEAD to Revision");
                DialogDescription = Translate("Move HEAD to the selected revision");
                ActiveBranchTextBlock.Text = "HEAD";
            }
            SubmitButtonTitle = Translate("Reset");
            CancelButtonTitle = Translate("Cancel");
            Title = DialogTitle;

            // 对照 WPF: DestinationGitPointView.Value = _destination;
            // Avalonia spike: 用 TextBlock 显示 SHA 简化
            DestinationTextBlock.Text = _destination.Sha.ToAbbreviatedString();

            RefreshCommandPreview();
        }

        // 对照 WPF: protected override string GetCommandPreview()
        private string? GetCommandPreview()
        {
            string? flag = _resetType switch
            {
                BranchResetType.Soft => "--soft",
                BranchResetType.Mixed => "--mixed",
                BranchResetType.Hard => "--hard",
                _ => null
            };
            if (flag == null)
            {
                return null;
            }
            if (_destination == null)
            {
                return null;
            }
            string sha = _destination.Sha.ToAbbreviatedString();
            if (string.IsNullOrEmpty(sha))
            {
                return null;
            }
            return "git reset " + flag + " " + sha;
        }

        private void RefreshCommandPreview()
        {
            if (CommandPreviewTextBox == null) return;
            CommandPreviewTextBox.Text = GetCommandPreview() ?? string.Empty;
        }

        // 对照 WPF: protected override void OnKeyDown(KeyEventArgs e)
        protected override void OnKeyDown(KeyEventArgs e)
        {
            base.OnKeyDown(e);
            if (e.Key == Key.S)
            {
                ResetTypeCombobox.SelectedIndex = 0;
            }
            else if (e.Key == Key.M)
            {
                ResetTypeCombobox.SelectedIndex = 1;
            }
            else if (e.Key == Key.H)
            {
                ResetTypeCombobox.SelectedIndex = 2;
            }
        }

        // 对照 WPF: protected override void OnSubmit()
        protected override void OnSubmit()
        {
            string branchName = _branch?.Name ?? "HEAD";
            BranchResetType resetType = _resetType;
            Sha destinationSha = _destination.Sha;

            DisableEditableControls();
            SetStatus(ForkPlusDialogStatus.InProgress, Translate("Resetting '") + branchName + "'...");

            // 对照 WPF: _repositoryUserControl.AddUndoable(...) + base.Dispatcher.Async(Close)
            // Avalonia spike: Task.Run + Dispatcher.UIThread.Post + JobMonitor
            // 简化：仅执行核心 ResetCurrentBranchToRevisionGitCommand，跳过 submodule 更新
            Task.Run(delegate
            {
                JobMonitor monitor = new JobMonitor();
                GitCommandResult result = new ResetCurrentBranchToRevisionGitCommand().Execute(
                    _gitModule, destinationSha, resetType, monitor);
                Dispatcher.UIThread.Post(delegate
                {
                    try
                    {
                        _onCompleted?.Invoke(result);
                    }
                    catch (Exception ex)
                    {
                        Log.Error("ResetBranchWindow onCompleted callback failed", ex);
                    }
                    Close(result);
                });
            });
        }

        // 对照 WPF: ResetTypeCombobox_SelectionChanged
        public void ResetTypeCombobox_SelectionChanged(object? sender, SelectionChangedEventArgs e)
        {
            if (ResetTypeCombobox.SelectedItem is ComboBoxItem comboBoxItem
                && comboBoxItem.Tag is BranchResetType tag)
            {
                _resetType = tag;
                RefreshCommandPreview();
            }
        }

        // 对照 WPF: public static string GetResetTypeName(BranchResetType resetType)
        public static string GetResetTypeName(BranchResetType resetType)
        {
            return resetType switch
            {
                BranchResetType.Mixed => "mixed",
                BranchResetType.Hard => "hard",
                BranchResetType.Soft => "soft",
                _ => throw new Exception("Cannot reach here"),
            };
        }

        // spike 版：手动禁用可编辑控件
        private void DisableEditableControls()
        {
            ResetTypeCombobox.IsEnabled = false;
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

        // 对照 WPF: PreferencesLocalization.FormatCurrent(text, args)
        private static string FormatTranslate(string text, params object[] args)
        {
            var localization = ServiceLocator.Localization;
            var userSettings = ServiceLocator.UserSettings;
            if (localization != null && userSettings != null)
            {
                return localization.FormatCurrent(text, args);
            }
            return string.Format(text, args);
        }
    }
}

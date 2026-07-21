using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using ForkPlus;
using ForkPlus.Git;
using ForkPlus.Git.Commands;
using ForkPlus.Jobs;
using ForkPlus.Services;
using ForkPlus.Settings;

namespace ForkPlus.Avalonia.Dialogs
{
    // Phase 4.x：Avalonia 版 CheckoutBranchAsWorktreeWindow（真实迁移版，对照 WPF CheckoutBranchAsWorktreeWindow.xaml.cs 178 行）。
    //
    // 对照 WPF 工程 src/ForkPlus/UI/Dialogs/CheckoutBranchAsWorktreeWindow.xaml.cs：
    //   - public partial class CheckoutBranchAsWorktreeWindow : ForkPlusDialogWindow
    //   - 字段: RepositoryUserControl _repositoryUserControl / GitModule _gitModule
    //           LocalBranch _branch / RepositoryWorktrees _worktrees / string _worktreesContainerPath
    //   - 构造函数 (RepositoryUserControl, LocalBranch branch) 计算 _worktreesContainerPath
    //   - IsSubmitAllowed: PathTextBox 非空 + _worktrees.WorktreesByFullReference 不含 _branch.FullReference
    //   - GetCommandPreview: "git worktree add <quotedPath> <branch.Name>"
    //   - OnSubmit: AddWorktreeGitCommand.Execute(... _branch.Name ...) → OpenGitRepositoryGitCommand
    //     → 可选 UpdateSubmodules(result, submodulesToUpdate, gitModule.CommonGitDir, monitor)
    //     → MainWindow.Instance.TabManager.OpenRepository(worktreePath) + Close(result)
    //
    // Avalonia 版差异：
    //   1. spike 模式：构造函数 SetFooter/Footer 注入
    //   2. RepositoryUserControl 参数 → GitModule + LocalBranch + RepositoryWorktrees?
    //      + SubmodulesToUpdate + Action<string>? onRepositoryOpened + Action<GitCommandResult>? onCompleted 回调
    //   3. GitPointView 自定义控件 → spike 版用 TextBlock 显示 branch.Name 简化
    //   4. spike 基类不提供 GetCommandPreview/RefreshCommandPreview → 自行维护 CommandPreviewTextBox
    //   5. spike 基类不提供 DisableEditableControls → 手动禁用 TextBox + BrowseButton
    //   6. JobQueue + Dispatcher.Async → Task.Run + Dispatcher.UIThread.Post + JobMonitor
    //   7. MainWindow.Instance.TabManager.OpenRepository(path) → 注入 Action<string>? onRepositoryOpened
    //   8. OpenDialog.SelectDirectory → StorageProvider.OpenFolderPickerAsync
    public partial class CheckoutBranchAsWorktreeWindow : global::ForkPlus.Avalonia.Dialogs.ForkPlusDialogWindow
    {
        private readonly GitModule _gitModule;
        private readonly LocalBranch _branch;
        private readonly RepositoryWorktrees? _worktrees;
        private readonly SubmodulesToUpdate _submodulesToUpdate;
        private readonly Action<string>? _onRepositoryOpened;
        private readonly Action<GitCommandResult>? _onCompleted;

        private string _worktreesContainerPath;

        // 构造函数签名与 WPF 不同：用 GitModule + LocalBranch + RepositoryWorktrees?
        // + SubmodulesToUpdate + Action 回调替代 RepositoryUserControl 依赖
        public CheckoutBranchAsWorktreeWindow(
            GitModule gitModule,
            LocalBranch branch,
            RepositoryWorktrees? worktrees = null,
            SubmodulesToUpdate submodulesToUpdate = default,
            Action<string>? onRepositoryOpened = null,
            Action<GitCommandResult>? onCompleted = null)
        {
            ShowFooter = true;
            InitializeComponent();
            SetFooter(Footer);
            SetTitleTextBlock(TitleTextBlock);
            SetDescriptionTextBlock(DescriptionTextBlock);

            _gitModule = gitModule ?? throw new ArgumentNullException(nameof(gitModule));
            _branch = branch ?? throw new ArgumentNullException(nameof(branch));
            _worktrees = worktrees;
            _submodulesToUpdate = submodulesToUpdate;
            _onRepositoryOpened = onRepositoryOpened;
            _onCompleted = onCompleted;

            // 对照 WPF: _worktreesContainerPath = Path.Combine(parentDir, repoName + "-worktrees")
            string directoryName = Path.GetDirectoryName(_gitModule.CommonGitDir);
            _worktreesContainerPath = Path.Combine(
                Path.GetDirectoryName(directoryName),
                Path.GetFileName(directoryName) + "-worktrees");

            // 对照 WPF: DialogTitle / DialogDescription / SubmitButtonTitle
            DialogTitle = Translate("Checkout Branch as Worktree");
            DialogDescription = Translate("Checkout branch in separate worktree");
            SubmitButtonTitle = Translate("Create");
            CancelButtonTitle = Translate("Cancel");
            Title = Translate("Checkout Branch as Worktree");

            // 对照 WPF: GitPointView.Value = branch;
            // Avalonia spike: 用 TextBlock 显示 branch.Name 简化
            BranchNameTextBlock.Text = _branch.Name;

            // 对照 WPF: RefreshPath(); UpdateSubmitButton(); RefreshCommandPreview();
            RefreshPath();
            UpdateSubmitButton();
            RefreshCommandPreview();
        }

        // 对照 WPF: protected override bool IsSubmitAllowed
        protected override bool IsSubmitAllowed
        {
            get
            {
                if (string.IsNullOrWhiteSpace(PathTextBox.Text.Trim()))
                {
                    return false;
                }
                if (_worktrees != null && _worktrees.WorktreesByFullReference.ContainsKey(_branch.FullReference))
                {
                    return false;
                }
                return true;
            }
        }

        // 对照 WPF: protected override string GetCommandPreview()
        private string? GetCommandPreview()
        {
            if (_branch == null || string.IsNullOrEmpty(_branch.Name))
            {
                return null;
            }
            string worktreePath = PathTextBox.Text?.Trim() ?? "";
            if (string.IsNullOrEmpty(worktreePath))
            {
                return null;
            }
            string quotedPath = worktreePath.IndexOf(' ') >= 0 ? ("\"" + worktreePath + "\"") : worktreePath;
            return "git worktree add " + quotedPath + " " + _branch.Name;
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
            string worktreePath = PathHelper.NormalizeUnix(PathTextBox.Text.Trim());
            SubmodulesToUpdate submodulesToUpdate = _submodulesToUpdate;
            string commonGitDir = gitModule.CommonGitDir;

            DisableEditableControls();
            SetStatus(ForkPlusDialogStatus.InProgress, Translate("Creating worktree..."));

            // 对照 WPF: _repositoryUserControl.JobQueue.Add(Translate("Checkout Branch As Worktree"), ...)
            // Avalonia: Task.Run + Dispatcher.UIThread.Post + JobMonitor
            Task.Run(delegate
            {
                JobMonitor monitor = new JobMonitor();
                GitCommandResult checkoutBranchAsWorktreeResult = new AddWorktreeGitCommand().Execute(
                    _gitModule, worktreePath, _branch.Name, monitor);
                if (!checkoutBranchAsWorktreeResult.Succeeded)
                {
                    Dispatcher.UIThread.Post(delegate
                    {
                        InvokeOnCompleted(checkoutBranchAsWorktreeResult);
                        Close(checkoutBranchAsWorktreeResult);
                    });
                    return;
                }

                GitCommandResult<GitModule> openWorktreeResult = new OpenGitRepositoryGitCommand().Execute(worktreePath);
                if (!openWorktreeResult.Succeeded)
                {
                    Dispatcher.UIThread.Post(delegate
                    {
                        GitCommandResult failResult = openWorktreeResult.ToGitCommandResult();
                        InvokeOnCompleted(failResult);
                        Close(failResult);
                    });
                    return;
                }

                GitModule result = openWorktreeResult.Result;
                if (submodulesToUpdate.Length > 0)
                {
                    Dispatcher.UIThread.Post(delegate
                    {
                        SetStatus(ForkPlusDialogStatus.InProgress, Translate("Updating submodules..."));
                    });
                    GitCommandResult updateSubmodulesResult = UpdateSubmodules(
                        result, submodulesToUpdate, commonGitDir, monitor);
                    if (!updateSubmodulesResult.Succeeded)
                    {
                        Dispatcher.UIThread.Post(delegate
                        {
                            InvokeOnCompleted(updateSubmodulesResult);
                            Close(updateSubmodulesResult);
                        });
                        return;
                    }
                }
                Dispatcher.UIThread.Post(delegate
                {
                    // 对照 WPF: MainWindow.Instance.TabManager.OpenRepository(worktreePath);
                    try
                    {
                        _onRepositoryOpened?.Invoke(worktreePath);
                    }
                    catch (Exception ex)
                    {
                        Log.Error("CheckoutBranchAsWorktreeWindow onRepositoryOpened callback failed", ex);
                    }
                    InvokeOnCompleted(checkoutBranchAsWorktreeResult);
                    Close(checkoutBranchAsWorktreeResult);
                });
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
                Log.Error("CheckoutBranchAsWorktreeWindow onCompleted callback failed", ex);
            }
        }

        // 对照 WPF: BrowseButton_Click（OpenDialog.SelectDirectory → StorageProvider.OpenFolderPickerAsync）
        public async void BrowseButton_Click(object? sender, RoutedEventArgs e)
        {
            string initialDirectory = Directory.Exists(_worktreesContainerPath)
                ? _worktreesContainerPath
                : Path.GetDirectoryName(_worktreesContainerPath);
            var topLevel = TopLevel.GetTopLevel(this);
            if (topLevel == null) return;

            var options = new FolderPickerOpenOptions
            {
                Title = Translate("Select location"),
            };
            if (initialDirectory != null && Directory.Exists(initialDirectory))
            {
                try
                {
                    var uri = new Uri(Path.GetFullPath(initialDirectory));
                    var folder = await topLevel.StorageProvider.TryGetFolderFromPathAsync(uri);
                    if (folder != null) options.SuggestedStartLocation = folder;
                }
                catch { }
            }

            var result = await topLevel.StorageProvider.OpenFolderPickerAsync(options);
            if (result != null && result.Count > 0)
            {
                _worktreesContainerPath = result[0].Path.LocalPath;
                RefreshPath();
                UpdateSubmitButton();
                RefreshCommandPreview();
            }
        }

        // 对照 WPF: PathTextBox_TextChanged
        public void PathTextBox_TextChanged(object? sender, TextChangedEventArgs e)
        {
            UpdateSubmitButton();
            RefreshCommandPreview();
        }

        // 对照 WPF: RefreshPath
        private void RefreshPath()
        {
            string path = _branch.Name.Replace('/', '-');
            PathTextBox.Text = Path.Combine(_worktreesContainerPath, path);
            PathTextBox.CaretIndex = PathTextBox.Text.Length;
        }

        // 对照 WPF: private static GitCommandResult UpdateSubmodules(...)
        private static GitCommandResult UpdateSubmodules(GitModule gitModule, SubmodulesToUpdate submodulesToUpdate, string referenceGitDir, JobMonitor monitor)
        {
            GitCommandResult gitCommandResult = new UpdateSubmodulesGitCommand().Execute(gitModule, submodulesToUpdate, monitor, referenceGitDir);
            if (!gitCommandResult.Succeeded && gitCommandResult.Error is GitCommandError.UnsafeRepository unsafeRepository)
            {
                GitCommandResult gitCommandResult2 = new AddRepositoryToSafeDirectoriesListGitCommand().Execute(unsafeRepository.ProposedRepositoryPath);
                if (!gitCommandResult2.Succeeded)
                {
                    return gitCommandResult2;
                }
                return new UpdateSubmodulesGitCommand().Execute(gitModule, submodulesToUpdate, monitor, referenceGitDir);
            }
            return gitCommandResult;
        }

        // spike 版：手动禁用可编辑控件
        private void DisableEditableControls()
        {
            PathTextBox.IsEnabled = false;
            BrowseButton.IsEnabled = false;
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

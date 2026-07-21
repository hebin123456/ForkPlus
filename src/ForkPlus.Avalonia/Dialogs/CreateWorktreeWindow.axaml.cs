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
    // Phase 4.x：Avalonia 版 CreateWorktreeWindow（真实迁移版，对照 WPF CreateWorktreeWindow.xaml.cs 220 行）。
    //
    // 对照 WPF 工程 src/ForkPlus/UI/Dialogs/CreateWorktreeWindow.xaml.cs：
    //   - public partial class CreateWorktreeWindow : ForkPlusDialogWindow
    //   - 字段: RepositoryUserControl _repositoryUserControl / GitModule _gitModule
    //           RepositoryWorktrees _worktrees / RepositoryReferences _repositoryReferences
    //           string _worktreesContainerPath
    //   - 构造函数 (RepositoryUserControl, LocalBranch startBranch) 计算 _worktreesContainerPath
    //   - IsSubmitAllowed: branchName 非空 + ReferenceNameValidator 通过 + 不与 worktree / 本地分支重名 + 路径非空
    //   - GetCommandPreview: "git worktree add <quotedPath> <branchName>"
    //   - OnSubmit: AddWorktreeGitCommand.Execute(... startSha ...) → OpenGitRepositoryGitCommand
    //     → 可选 UpdateSubmodules(result, submodulesToUpdate, _gitModule.CommonGitDir, monitor)
    //     → MainWindow.Instance.TabManager.OpenRepository(worktreePath) + Close(result)
    //
    // Avalonia 版差异：
    //   1. spike 模式：构造函数 SetFooter/Footer 注入
    //   2. RepositoryUserControl 参数 → GitModule + RepositoryReferences? + RepositoryWorktrees?
    //      + LocalBranch? startBranch + SubmodulesToUpdate + Action<string>? onRepositoryOpened
    //      + Action<GitCommandResult>? onCompleted 回调
    //   3. spike 基类不提供 GetCommandPreview/RefreshCommandPreview → 自行维护 CommandPreviewTextBox
    //   4. spike 基类不提供 DisableEditableControls → 手动禁用 ComboBox + TextBox + BrowseButton
    //   5. JobQueue + Dispatcher.Async → Task.Run + Dispatcher.UIThread.Post + JobMonitor
    //   6. MainWindow.Instance.TabManager.OpenRepository(path) → 注入 Action<string>? onRepositoryOpened
    //   7. PlaceholderTextBox.Placeholder → TextBox Watermark
    //   8. ReferenceTextBox → 普通 TextBox（spike 不迁移 ReferenceNameAutocompleteProvider）
    //   9. OpenDialog.SelectDirectory → StorageProvider.OpenFolderPickerAsync
    public partial class CreateWorktreeWindow : global::ForkPlus.Avalonia.Dialogs.ForkPlusDialogWindow
    {
        private readonly GitModule _gitModule;
        private readonly RepositoryReferences? _repositoryReferences;
        private readonly RepositoryWorktrees? _worktrees;
        private readonly SubmodulesToUpdate _submodulesToUpdate;
        private readonly Action<string>? _onRepositoryOpened;
        private readonly Action<GitCommandResult>? _onCompleted;

        private string _worktreesContainerPath;

        // 构造函数签名与 WPF 不同：用 GitModule + RepositoryReferences? + RepositoryWorktrees?
        // + LocalBranch? + SubmodulesToUpdate + Action 回调替代 RepositoryUserControl 依赖
        public CreateWorktreeWindow(
            GitModule gitModule,
            RepositoryReferences? references = null,
            RepositoryWorktrees? worktrees = null,
            LocalBranch? startBranch = null,
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
            _repositoryReferences = references;
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
            DialogTitle = Translate("Create Worktree");
            DialogDescription = Translate("Create branch and checkout it in a separate worktree");
            SubmitButtonTitle = Translate("Create");
            CancelButtonTitle = Translate("Cancel");
            Title = Translate("Create Worktree");

            // 对照 WPF: LocalBranchesComboBox.ItemsSource = _repositoryReferences.LocalBranches;
            if (_repositoryReferences != null)
            {
                LocalBranchesComboBox.ItemsSource = _repositoryReferences.LocalBranches;
            }
            // 对照 WPF: LocalBranchesComboBox.SelectedItem = startBranch;
            LocalBranchesComboBox.SelectedItem = startBranch;

            // 对照 WPF: RefreshPath(); UpdateSubmitButton(); RefreshCommandPreview();
            RefreshPath();
            UpdateSubmitButton();
            RefreshCommandPreview();

            // 对照 WPF: base.Loaded += delegate { BranchNameTextBox.Focus(); };
            Loaded += (_, _) => BranchNameTextBox.Focus();
        }

        // 对照 WPF: protected override bool IsSubmitAllowed
        protected override bool IsSubmitAllowed
        {
            get
            {
                SetStatus(ForkPlusDialogStatus.None, string.Empty);
                string branchName = BranchNameTextBox.Text;
                if (string.IsNullOrEmpty(branchName))
                {
                    return false;
                }
                string text = ReferenceNameValidator.Validate(branchName);
                if (text != null)
                {
                    SetStatus(ForkPlusDialogStatus.Warning, text);
                    return false;
                }
                string key = "refs/heads/" + branchName;
                if (_worktrees != null && _worktrees.WorktreesByFullReference.ContainsKey(key))
                {
                    SetStatus(ForkPlusDialogStatus.Warning, "Worktree '" + branchName + "' already exists");
                    return false;
                }
                if (_repositoryReferences != null
                    && _repositoryReferences.LocalBranches.AnyItem((LocalBranch x) => x.Name.ToLower() == branchName.ToLower()))
                {
                    SetStatus(ForkPlusDialogStatus.Warning, "Branch '" + branchName + "' already exists");
                    return false;
                }
                if (string.IsNullOrWhiteSpace(PathTextBox.Text.Trim()))
                {
                    return false;
                }
                return true;
            }
        }

        // 对照 WPF: protected override string GetCommandPreview()
        private string? GetCommandPreview()
        {
            string branchName = BranchNameTextBox.Text;
            string worktreePath = PathTextBox.Text?.Trim() ?? "";
            if (string.IsNullOrEmpty(branchName) || string.IsNullOrEmpty(worktreePath))
            {
                return null;
            }
            string quotedPath = worktreePath.IndexOf(' ') >= 0 ? ("\"" + worktreePath + "\"") : worktreePath;
            return "git worktree add " + quotedPath + " " + branchName;
        }

        private void RefreshCommandPreview()
        {
            if (CommandPreviewTextBox == null) return;
            CommandPreviewTextBox.Text = GetCommandPreview() ?? string.Empty;
        }

        // 对照 WPF: protected override void OnSubmit()
        protected override void OnSubmit()
        {
            LocalBranch selectedBranch = LocalBranchesComboBox.SelectedItem as LocalBranch;
            if (selectedBranch == null)
            {
                return;
            }
            string branchName = BranchNameTextBox.Text;
            string worktreePath = PathHelper.NormalizeUnix(PathTextBox.Text.Trim());
            SubmodulesToUpdate submodulesToUpdate = _submodulesToUpdate;
            string commonGitDir = _gitModule.CommonGitDir;

            DisableEditableControls();
            SetStatus(ForkPlusDialogStatus.InProgress, Translate("Creating worktree..."));

            // 对照 WPF: _repositoryUserControl.JobQueue.Add(Translate("Create Worktree"), ...)
            // Avalonia: Task.Run + Dispatcher.UIThread.Post + JobMonitor
            Task.Run(delegate
            {
                JobMonitor monitor = new JobMonitor();
                GitCommandResult createWorktreeResult = new AddWorktreeGitCommand().Execute(
                    _gitModule, worktreePath, branchName, selectedBranch.Sha, monitor);
                if (!createWorktreeResult.Succeeded)
                {
                    Dispatcher.UIThread.Post(delegate
                    {
                        InvokeOnCompleted(createWorktreeResult);
                        Close(createWorktreeResult);
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
                        Log.Error("CreateWorktreeWindow onRepositoryOpened callback failed", ex);
                    }
                    InvokeOnCompleted(createWorktreeResult);
                    Close(createWorktreeResult);
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
                Log.Error("CreateWorktreeWindow onCompleted callback failed", ex);
            }
        }

        // 对照 WPF: BranchName_TextChanged
        public void BranchName_TextChanged(object? sender, TextChangedEventArgs e)
        {
            RefreshPath();
            UpdateSubmitButton();
            RefreshCommandPreview();
        }

        // 对照 WPF: PathTextBox_TextChanged
        public void PathTextBox_TextChanged(object? sender, TextChangedEventArgs e)
        {
            UpdateSubmitButton();
            RefreshCommandPreview();
        }

        // 对照 WPF: LocalBranchesComboBox 选择变化也需更新命令预览（spike 新增）
        public void LocalBranchesComboBox_SelectionChanged(object? sender, SelectionChangedEventArgs e)
        {
            RefreshCommandPreview();
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

        // 对照 WPF: RefreshPath
        private void RefreshPath()
        {
            string text = (BranchNameTextBox.Text ?? "").Replace('/', '-');
            if (!string.IsNullOrEmpty(text))
            {
                PathTextBox.Text = Path.Combine(_worktreesContainerPath, text);
            }
            else
            {
                PathTextBox.Text = _worktreesContainerPath;
            }
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
            LocalBranchesComboBox.IsEnabled = false;
            BranchNameTextBox.IsEnabled = false;
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

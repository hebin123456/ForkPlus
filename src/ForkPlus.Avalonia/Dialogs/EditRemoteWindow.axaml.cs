using System;
using System.IO;
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
    // Phase 4.x：Avalonia 版 EditRemoteWindow（真实迁移版，对照 WPF EditRemoteWindow.xaml.cs 525 行）。
    //
    // 对照 WPF 工程 src/ForkPlus/UI/Dialogs/EditRemoteWindow.xaml.cs：
    //   - public partial class EditRemoteWindow : ForkPlusDialogWindow
    //   - 嵌套类 AccountItem（INotifyPropertyChanged）+ 枚举 AccountItemType
    //   - 字段: RepositoryUserControl _repositoryUserControl / GitModule _gitModule
    //           / Remote[] _remotes / Remote _remoteToEdit / bool _refreshingUrl
    //   - 构造函数 (RepositoryUserControl, GitModule, Remote remoteToEdit = null)
    //     * new GetRemotesGitCommand().Execute(_gitModule).Result?.Items → _remotes
    //     * InitializeRemoteWindow:
    //       - edit: DialogTitle="Edit Remote" / RepositoryUrlTextBox.Text=url / RemoteNameTextBox.Text=name / SelectAll
    //       - add: DialogTitle="Add Remote" / RemoteNameTextBox.Text="origin" / 检查 clipboard url
    //   - IsSubmitAllowed override: 名字/URL 空 false / 与原值相同 false / Validate 失败 Warning / 重复 Warning
    //   - GetCommandPreview override:
    //     * add: "git remote add <name> <url>"
    //     * edit url changed: "git remote set-url <name> <url>"
    //     * edit name changed: "git remote rename <oldName> <newName>"
    //   - OnSubmit: JobQueue.Add
    //     * edit: 如果 url 变化且 submodule+sync → OpenGitRepositoryGitCommand(parent)
    //             + GetSubmodulesGitCommand + UpdateSubmoduleUrlGitCommand
    //             否则 EditRemoteUrlGitCommand → RenameRemoteGitCommand (如果 name 变化)
    //     * add: AddRemoteGitCommand
    //   - TestButton_Click: TestRemoteRepositoryConnectionGitCommand + 状态显示
    //   - NetworkProtocolDropDownButton + ContextMenu (HTTPS/SSH 切换)
    //   - AccountsComboBox (多账号选择)
    //   - ConfigureSSHKeyButton (Permission denied 时显示)
    //
    // Avalonia 版差异（spike 简化）：
    //   1. spike 模式：构造函数 SetFooter/Footer 注入
    //   2. RepositoryUserControl 参数 → GitModule + Remote? + Action<GitCommandResult>? 回调
    //   3. AccountItem/AccountsComboBox → spike 版不迁移（AccountManager 集成复杂）
    //   4. NetworkProtocolDropDownButton + ContextMenu → spike 版不迁移
    //   5. ConfigureSSHKeyButton → spike 版不迁移（依赖 MainWindow.Commands）
    //   6. StatusImage (PNG) → spike 版用 StatusTextBlock 显示文本
    //   7. BusyIndicator → spike 版用 SetStatus(InProgress, "Connecting...")
    //   8. RemoteNameTextBox.Icon / RepositoryUrlTextBox.Icon → spike 版不迁移
    //   9. spike 基类不提供 GetCommandPreview/RefreshCommandPreview → 自行维护 CommandPreviewTextBox
    //  10. spike 基类不提供 DisableEditableControls → 手动禁用 RemoteNameTextBox + RepositoryUrlTextBox + TestButton
    //  11. JobQueue + Dispatcher.Async → Task.Run + Dispatcher.UIThread.Post + JobMonitor
    //  12. PlaceholderTextBox.Placeholder → TextBox Watermark
    //  13. OnActivated override → spike 版省略（HideStatusControls 在构造函数末尾调用）
    //  14. _remotes.AnyItem → LINQ Any
    public partial class EditRemoteWindow : global::ForkPlus.Avalonia.Dialogs.ForkPlusDialogWindow
    {
        private readonly GitModule _gitModule;
        private readonly Remote[] _remotes;
        private readonly Remote? _remoteToEdit;
        private readonly Action<GitCommandResult>? _onCompleted;
        private bool _refreshingUrl;

        // 构造函数签名与 WPF 不同：用 GitModule + Remote? + Action 回调替代 RepositoryUserControl
        public EditRemoteWindow(
            GitModule gitModule,
            Remote? remoteToEdit = null,
            Action<GitCommandResult>? onCompleted = null)
        {
            ShowFooter = true;
            InitializeComponent();
            SetFooter(Footer);
            SetTitleTextBlock(TitleTextBlock);
            SetDescriptionTextBlock(DescriptionTextBlock);

            _gitModule = gitModule ?? throw new ArgumentNullException(nameof(gitModule));
            _remoteToEdit = remoteToEdit;
            _onCompleted = onCompleted;

            // 对照 WPF: GitCommandResult<RepositoryRemotes> gitCommandResult = new GetRemotesGitCommand().Execute(_gitModule);
            GitCommandResult<RepositoryRemotes> gitCommandResult = new GetRemotesGitCommand().Execute(_gitModule);
            _remotes = gitCommandResult.Result?.Items ?? Array.Empty<Remote>();

            // 对照 WPF: InitializeRemoteWindow();
            InitializeRemoteWindow();
            // 对照 WPF: OnActivated → HideStatusControls();
            HideStatusControls();
            RefreshCommandPreview();
            UpdateSubmitButton();
        }

        // 对照 WPF: protected override bool IsSubmitAllowed
        protected override bool IsSubmitAllowed
        {
            get
            {
                SetStatus(ForkPlusDialogStatus.None, string.Empty);
                string? remoteNameText = RemoteNameTextBox.Text;
                string? urlText = RepositoryUrlTextBox.Text;
                string remoteName = remoteNameText?.Trim() ?? "";
                string text = urlText?.Trim() ?? "";
                if (string.IsNullOrWhiteSpace(remoteName) || string.IsNullOrWhiteSpace(text))
                {
                    return false;
                }
                if (_remoteToEdit?.Name == remoteName && _remoteToEdit?.Url == text)
                {
                    return false;
                }
                string? text2 = ReferenceNameValidator.Validate(remoteName);
                if (text2 != null)
                {
                    SetStatus(ForkPlusDialogStatus.Warning, text2);
                    return false;
                }
                // 对照 WPF: _remotes.AnyItem(x => x.Name == remoteName)
                if (_remoteToEdit?.Name != remoteName && _remotes.Any((Remote x) => x.Name == remoteName))
                {
                    SetStatus(ForkPlusDialogStatus.Warning, "Remote '" + remoteName + "' already exists");
                    return false;
                }
                return true;
            }
        }

        // 对照 WPF: protected override string GetCommandPreview()
        private string? GetCommandPreview()
        {
            string? newNameText = RemoteNameTextBox.Text;
            string? newUrlText = RepositoryUrlTextBox.Text;
            string newName = newNameText?.Trim() ?? "";
            string newUrl = newUrlText?.Trim() ?? "";
            if (string.IsNullOrWhiteSpace(newName) || string.IsNullOrWhiteSpace(newUrl))
            {
                return null;
            }
            string Quote(string s) => s.Contains(" ") ? "\"" + s + "\"" : s;
            if (_remoteToEdit == null)
            {
                return "git remote add " + Quote(newName) + " " + Quote(newUrl);
            }
            if (_remoteToEdit.Url != newUrl)
            {
                return "git remote set-url " + Quote(newName) + " " + Quote(newUrl);
            }
            if (_remoteToEdit.Name != newName)
            {
                return "git remote rename " + Quote(_remoteToEdit.Name) + " " + Quote(newName);
            }
            return null;
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
            string? newNameText = RemoteNameTextBox.Text;
            string? newUrlText = RepositoryUrlTextBox.Text;
            string newName = newNameText?.Trim() ?? "";
            string newUrl = newUrlText?.Trim() ?? "";
            bool edit = _remoteToEdit != null;
            Remote? remoteToEdit = _remoteToEdit;
            bool syncWithParent = SyncCheckBox.IsChecked.GetValueOrDefault();
            GitModule gitModule = _gitModule;
            string name = edit
                ? FormatTranslate("Edit remote '{0}'", _remoteToEdit!.Name)
                : FormatTranslate("Add remote '{0}'", newName);

            DisableEditableControls();
            SetStatus(ForkPlusDialogStatus.InProgress, edit ? "Editing remote..." : "Adding remote...");

            // 对照 WPF: _repositoryUserControl.JobQueue.Add(name, ...)
            // Avalonia: Task.Run + Dispatcher.UIThread.Post + JobMonitor
            Task.Run(delegate
            {
                JobMonitor monitor = new JobMonitor();
                GitCommandResult finalResult;
                if (edit)
                {
                    GitCommandResult result2 = GitCommandResult.Success();
                    if (remoteToEdit!.Url != newUrl)
                    {
                        // 对照 WPF: if (gitModule.Type == ModuleType.Submodule && syncWithParent)
                        if (gitModule.Type == ModuleType.Submodule && syncWithParent)
                        {
                            // 对照 WPF: OpenGitRepositoryGitCommand().Execute(gitModule.ParentRepoPath)
                            GitCommandResult<GitModule> openParentResult = new OpenGitRepositoryGitCommand().Execute(gitModule.ParentRepoPath);
                            if (!openParentResult.Succeeded)
                            {
                                finalResult = openParentResult.ToGitCommandResult();
                                FinishClose(finalResult);
                                return;
                            }
                            GitModule parentModule = openParentResult.Result;
                            // 对照 WPF: GetSubmodulesGitCommand().Execute(parentModule)
                            GitCommandResult<Submodule[]> getSubmodulesResult = new GetSubmodulesGitCommand().Execute(parentModule);
                            if (!getSubmodulesResult.Succeeded)
                            {
                                finalResult = getSubmodulesResult.ToGitCommandResult();
                                FinishClose(finalResult);
                                return;
                            }
                            // 对照 WPF: 找到当前 submodule path
                            string submodule = "";
                            Submodule[] submodules = getSubmodulesResult.Result;
                            foreach (Submodule s in submodules)
                            {
                                if (PathHelper.NormalizeUnix(gitModule.Path).EndsWith(s.Path))
                                {
                                    submodule = s.Path;
                                }
                            }
                            GitCommandResult updateSubmoduleUrlResult = new UpdateSubmoduleUrlGitCommand().Execute(parentModule, submodule, newUrl, monitor);
                            if (!updateSubmoduleUrlResult.Succeeded)
                            {
                                finalResult = updateSubmoduleUrlResult;
                                FinishClose(finalResult);
                                return;
                            }
                        }
                        else
                        {
                            // 对照 WPF: EditRemoteUrlGitCommand().Execute(gitModule, remoteToEdit.Name, newUrl, monitor)
                            result2 = new EditRemoteUrlGitCommand().Execute(gitModule, remoteToEdit.Name, newUrl, monitor);
                            if (!result2.Succeeded)
                            {
                                finalResult = result2;
                                FinishClose(finalResult);
                                return;
                            }
                        }
                    }
                    if (remoteToEdit.Name != newName)
                    {
                        // 对照 WPF: RenameRemoteGitCommand().Execute(gitModule, remoteToEdit.Name, newName, monitor)
                        result2 = new RenameRemoteGitCommand().Execute(gitModule, remoteToEdit.Name, newName, monitor);
                    }
                    finalResult = result2;
                }
                else
                {
                    // 对照 WPF: AddRemoteGitCommand().Execute(gitModule, newName, newUrl, monitor)
                    finalResult = new AddRemoteGitCommand().Execute(gitModule, newName, newUrl, monitor);
                }
                FinishClose(finalResult);
            });

            void FinishClose(GitCommandResult result)
            {
                Dispatcher.UIThread.Post(delegate
                {
                    try { _onCompleted?.Invoke(result); } catch (Exception ex) { Log.Error("EditRemoteWindow onCompleted failed", ex); }
                    Close(result);
                });
            }
        }

        // 对照 WPF: RemoteNameTextBox_TextChanged
        public void RemoteNameTextBox_TextChanged(object? sender, TextChangedEventArgs e)
        {
            UpdateSubmitButton();
            HideStatusControls();
            RefreshCommandPreview();
        }

        // 对照 WPF: RepositoryUrlTextBox_TextChanged
        public void RepositoryUrlTextBox_TextChanged(object? sender, TextChangedEventArgs e)
        {
            UpdateSubmitButton();
            HideStatusControls();
            RefreshSyncCheckBox();
            // 对照 WPF: if (!_refreshingUrl) RefreshAccountsComboBox(); — spike 版省略 Accounts
            RefreshCommandPreview();
        }

        // 对照 WPF: TestButton_Click
        public void TestButton_Click(object? sender, RoutedEventArgs e)
        {
            string? urlText = RepositoryUrlTextBox.Text;
            string newUrl = urlText?.Trim() ?? "";
            DisableEditableControls();
            TestButton.IsVisible = false;
            StatusTextBlock.IsVisible = true;
            StatusTextBlock.Text = Translate("Connecting...");
            SetStatus(ForkPlusDialogStatus.InProgress, Translate("Connecting..."));

            // 对照 WPF: _repositoryUserControl.JobQueue.Add("Test connection", ...)
            // Avalonia: Task.Run + Dispatcher.UIThread.Post
            Task.Run(delegate
            {
                JobMonitor monitor = new JobMonitor();
                GitCommandResult result = new TestRemoteRepositoryConnectionGitCommand().Execute(newUrl, monitor);
                Dispatcher.UIThread.Post(delegate
                {
                    EnableEditableControls();
                    if (!result.Succeeded)
                    {
                        // 对照 WPF: if (result.Error is GitCommandError.CallbackUnknownError callbackUnknownError)
                        if (result.Error is GitCommandError.CallbackUnknownError callbackUnknownError)
                        {
                            if (callbackUnknownError.FullOutput.Contains("Permission denied (publickey)"))
                            {
                                StatusTextBlock.IsVisible = true;
                                StatusTextBlock.Text = Translate("Permission denied (publickey)");
                                // 对照 WPF: ConfigureSSHKeyButton.Show(); TestButton.Collapse(); — spike 版省略 SSH 配置按钮
                                TestButton.IsVisible = false;
                            }
                            else if (callbackUnknownError.FullOutput.Contains("not found"))
                            {
                                StatusTextBlock.IsVisible = true;
                                StatusTextBlock.Text = Translate("Repository not found");
                                TestButton.IsVisible = false;
                            }
                            else
                            {
                                // 对照 WPF: new ErrorWindow(repositoryUserControl, result.Error).ShowDialog();
                                // spike 版用 SetStatus(Error, ...) 显示错误（避免阻塞 + 简化依赖）
                                SetStatus(ForkPlusDialogStatus.Error, callbackUnknownError.FullOutput);
                                StatusTextBlock.IsVisible = false;
                                TestButton.IsVisible = true;
                            }
                        }
                        else
                        {
                            SetStatus(ForkPlusDialogStatus.Error, result.Error?.ToString() ?? "Connection failed");
                            StatusTextBlock.IsVisible = false;
                            TestButton.IsVisible = true;
                        }
                    }
                    else
                    {
                        // 对照 WPF: StatusImage.Source = SuccessIcon; StatusTextBlock.Text = "Connection succeeded";
                        StatusTextBlock.IsVisible = true;
                        StatusTextBlock.Text = Translate("Connection succeeded");
                    }
                });
            });
        }

        // 对照 WPF: private void HideStatusControls()
        private void HideStatusControls()
        {
            StatusTextBlock.IsVisible = false;
            TestButton.IsVisible = true;
            ClearStatus();
        }

        // 对照 WPF: private void RefreshSyncCheckBox()
        private void RefreshSyncCheckBox()
        {
            string? urlText = RepositoryUrlTextBox.Text;
            string url = urlText?.Trim() ?? "";
            // 对照 WPF: if (_remoteToEdit != null && _gitModule.Type == ModuleType.Submodule && url != _remoteToEdit.Url)
            if (_remoteToEdit != null && _gitModule.Type == ModuleType.Submodule && url != _remoteToEdit.Url)
            {
                SyncCheckBox.IsVisible = true;
            }
            else
            {
                SyncCheckBox.IsVisible = false;
            }
        }

        // 对照 WPF: private void InitializeRemoteWindow()
        private void InitializeRemoteWindow()
        {
            if (_remoteToEdit != null)
            {
                DialogTitle = Translate("Edit Remote");
                DialogDescription = Translate("Edit URL of the remote repository");
                SubmitButtonTitle = Translate("Edit");
                Title = Translate("Edit Remote");
                RepositoryUrlTextBox.Text = _remoteToEdit.Url;
                RemoteNameTextBox.Text = _remoteToEdit.Name;
                return;
            }
            DialogTitle = Translate("Add Remote");
            DialogDescription = Translate("Add new remote repository reference");
            SubmitButtonTitle = Translate("Add New Remote");
            Title = Translate("Add Remote");
            RemoteNameTextBox.Text = Consts.Git.DefaultRemoteName;
            // 对照 WPF: string text = ServiceLocator.Clipboard.GetText();
            string? text = ServiceLocator.Clipboard?.GetText();
            if (text != null)
            {
                GitUrl gitUrl = new GitUrl(text.Trim());
                if (gitUrl != null && gitUrl.IsValid)
                {
                    if (_remotes.Length != 0)
                    {
                        RemoteNameTextBox.Text = gitUrl.Host;
                    }
                    RepositoryUrlTextBox.Text = gitUrl.UrlString;
                }
            }
            RefreshSyncCheckBox();
        }

        // spike 版：手动禁用可编辑控件
        private void DisableEditableControls()
        {
            RemoteNameTextBox.IsEnabled = false;
            RepositoryUrlTextBox.IsEnabled = false;
            TestButton.IsEnabled = false;
            SyncCheckBox.IsEnabled = false;
        }

        // 对照 WPF: EnableEditableControls（spike 版手动启用）
        private void EnableEditableControls()
        {
            RemoteNameTextBox.IsEnabled = true;
            RepositoryUrlTextBox.IsEnabled = true;
            TestButton.IsEnabled = true;
            SyncCheckBox.IsEnabled = true;
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

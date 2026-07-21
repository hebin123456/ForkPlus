using System;
using System.IO;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Threading;
using ForkPlus.Git;
using ForkPlus.Git.Commands;
using ForkPlus.Jobs;
using ForkPlus.Services;
using ForkPlus.Settings;

namespace ForkPlus.Avalonia.Dialogs
{
    // Phase 4.39b：Avalonia 版 AddSubmoduleWindow（真实迁移版，对照 WPF AddSubmoduleWindow.xaml.cs 211 行）。
    //
    // 对照 WPF 工程 src/ForkPlus/UI/Dialogs/AddSubmoduleWindow.xaml.cs：
    //   - public partial class AddSubmoduleWindow : ForkPlusDialogWindow
    //   - 字段: GitModule _gitModule / SubmodulesToUpdate _submodulesToUpdate / bool _repositoryPathValid
    //   - 构造函数 (GitModule gitModule, SubmodulesToUpdate submodulesToUpdate)
    //   - IsSubmitAllowed: url + path 非空 && _repositoryPathValid
    //   - GetCommandPreview: "git submodule add <url> <normalizedPath>"
    //   - OnSubmit: AddSubmoduleGitCommand().Execute(_gitModule, url, path, monitor)
    //     → 成功后可选 UpdateSubmodulesGitCommand().Execute(_gitModule, submodulesToUpdate, monitor)
    //     → Close(result)
    //   - PathTextBox_TextChanged: 更新 FinalPathHint + _repositoryPathValid
    //   - TryGetClipboardRepositoryUrl: ServiceLocator.Clipboard.GetText() + GitUrl.IsValid + LooksLikeRepositoryReference
    //
    // Avalonia 版差异：
    //   1. spike 模式：构造函数 SetFooter/Footer 注入
    //   2. spike 基类不提供 GetCommandPreview/RefreshCommandPreview → 自行维护 CommandPreviewTextBox
    //   3. spike 基类不提供 DisableEditableControls → 手动禁用 RepositoryUrlTextBox + PathTextBox + FetchNestedSubmodulesCheckBox
    //   4. JobQueue + Dispatcher.Async → Task.Run + Dispatcher.UIThread.Post + JobMonitor
    //   5. MainWindow.ActiveRepositoryUserControl.JobQueue.Add → 注入 Action<GitCommandResult>? onCompleted 回调
    //   6. PlaceholderTextBox.Placeholder → TextBox Watermark
    //   7. Collapse()/Show() 扩展方法 → IsVisible = false/true
    //   8. TextChangedEventArgs → Avalonia 同名类型
    public partial class AddSubmoduleWindow : global::ForkPlus.Avalonia.Dialogs.ForkPlusDialogWindow
    {
        private readonly GitModule _gitModule;
        private readonly SubmodulesToUpdate _submodulesToUpdate;
        private readonly Action<GitCommandResult> _onCompleted;

        private bool _repositoryPathValid;

        // 构造函数签名与 WPF 不同：增加 Action<GitCommandResult>? onCompleted 回调
        // （MainWindow.ActiveRepositoryUserControl.JobQueue.Add 在 Avalonia 端尚未迁移，spike 版解耦）
        public AddSubmoduleWindow(
            GitModule gitModule,
            SubmodulesToUpdate submodulesToUpdate,
            Action<GitCommandResult> onCompleted = null)
        {
            ShowFooter = true;
            InitializeComponent();
            SetFooter(Footer);
            SetTitleTextBlock(TitleTextBlock);
            SetDescriptionTextBlock(DescriptionTextBlock);

            _gitModule = gitModule ?? throw new ArgumentNullException(nameof(gitModule));
            _submodulesToUpdate = submodulesToUpdate;
            _onCompleted = onCompleted;

            // 对照 WPF: DialogTitle / DialogDescription / SubmitButtonTitle
            DialogTitle = Translate("Add Submodule");
            DialogDescription = Translate("Add new submodule repository reference");
            SubmitButtonTitle = Translate("Add Submodule");
            CancelButtonTitle = Translate("Cancel");
            Title = Translate("Add Submodule");

            // 对照 WPF: TryGetClipboardRepositoryUrl 预填
            string text = TryGetClipboardRepositoryUrl();
            if (!string.IsNullOrEmpty(text))
            {
                GitUrl gitUrl = new GitUrl(text);
                if (gitUrl.IsValid)
                {
                    PathTextBox.Text = gitUrl.RepositoryName;
                    RepositoryUrlTextBox.Text = gitUrl.UrlString;
                }
            }
            RepositoryUrlTextBox.SelectAll();

            // 对照 WPF: RefreshCommandPreview（InitializeComponent 后第一次刷新）
            RefreshCommandPreview();
            UpdateSubmitButton();
        }

        // 对照 WPF: protected override bool IsSubmitAllowed
        protected override bool IsSubmitAllowed
        {
            get
            {
                if (!string.IsNullOrWhiteSpace(RepositoryUrlTextBox.Text)
                    && !string.IsNullOrWhiteSpace(PathTextBox.Text))
                {
                    return _repositoryPathValid;
                }
                return false;
            }
        }

        // 对照 WPF: protected override string GetCommandPreview()
        private string GetCommandPreview()
        {
            string url = RepositoryUrlTextBox.Text?.Trim() ?? "";
            string path = PathTextBox.Text?.Trim() ?? "";
            if (string.IsNullOrWhiteSpace(path))
            {
                return null;
            }
            string Quote(string s) => s.Contains(" ") ? "\"" + s + "\"" : s;
            string normalizedPath = PathHelper.NormalizeUnix(path);
            if (string.IsNullOrWhiteSpace(url))
            {
                return "git submodule add " + Quote(normalizedPath);
            }
            return "git submodule add " + Quote(url) + " " + Quote(normalizedPath);
        }

        private void RefreshCommandPreview()
        {
            CommandPreviewTextBox.Text = GetCommandPreview();
        }

        // 对照 WPF: protected override void OnSubmit()
        protected override void OnSubmit()
        {
            string url = RepositoryUrlTextBox.Text;
            string path = PathHelper.NormalizeUnix(PathTextBox.Text);
            bool fetchNestedSubmodules = FetchNestedSubmodulesCheckBox.IsChecked.GetValueOrDefault();
            SubmodulesToUpdate submodulesToUpdate = _submodulesToUpdate;

            DisableEditableControls();
            SetStatus(ForkPlusDialogStatus.InProgress, Translate("Adding submodule..."));

            // 对照 WPF: MainWindow.ActiveRepositoryUserControl.JobQueue.Add(...)
            // Avalonia: Task.Run + Dispatcher.UIThread.Post + JobMonitor
            Task.Run(delegate
            {
                JobMonitor monitor = new JobMonitor();
                GitCommandResult addSubmoduleResult = new AddSubmoduleGitCommand().Execute(_gitModule, url, path, monitor);
                if (!addSubmoduleResult.Succeeded)
                {
                    Dispatcher.UIThread.Post(delegate
                    {
                        OnCompleted(addSubmoduleResult);
                        Close(addSubmoduleResult);
                    });
                    return;
                }

                // 对照 WPF: fetchNestedSubmodules && submodulesToUpdate.Length > 0
                if (fetchNestedSubmodules && submodulesToUpdate.Length > 0)
                {
                    Dispatcher.UIThread.Post(delegate
                    {
                        SetStatus(ForkPlusDialogStatus.InProgress, Translate("Fetching nested submodules..."));
                    });
                    GitCommandResult updateSubmoduleResult = new UpdateSubmodulesGitCommand().Execute(_gitModule, submodulesToUpdate, monitor);
                    if (!updateSubmoduleResult.Succeeded)
                    {
                        Dispatcher.UIThread.Post(delegate
                        {
                            OnCompleted(updateSubmoduleResult);
                            Close(updateSubmoduleResult);
                        });
                        return;
                    }
                }

                Dispatcher.UIThread.Post(delegate
                {
                    OnCompleted(addSubmoduleResult);
                    Close(addSubmoduleResult);
                });
            });
        }

        private void OnCompleted(GitCommandResult result)
        {
            try
            {
                _onCompleted?.Invoke(result);
            }
            catch (Exception ex)
            {
                Log.Error("AddSubmoduleWindow onCompleted callback failed", ex);
            }
        }

        // 对照 WPF: PathTextBox_TextChanged
        public void PathTextBox_TextChanged(object? sender, TextChangedEventArgs e)
        {
            _repositoryPathValid = true;
            try
            {
                string text = PathTextBox.Text;
                if (string.IsNullOrEmpty(text))
                {
                    FinalPathHintTextBlock.IsVisible = false;
                }
                else
                {
                    FinalPathHintTextBlock.Text = PathHelper.Normalize(_gitModule.MakePath(text));
                    FinalPathHintTextBlock.IsVisible = true;
                }
            }
            catch
            {
                _repositoryPathValid = false;
            }
            UpdateSubmitButton();
            RefreshCommandPreview();
        }

        // 对照 WPF: RepositoryUrlTextBox_TextChanged
        public void RepositoryUrlTextBox_TextChanged(object? sender, TextChangedEventArgs e)
        {
            UpdateSubmitButton();
            RefreshCommandPreview();
        }

        // spike 版：手动禁用可编辑控件
        private void DisableEditableControls()
        {
            RepositoryUrlTextBox.IsEnabled = false;
            PathTextBox.IsEnabled = false;
            FetchNestedSubmodulesCheckBox.IsEnabled = false;
        }

        // 对照 WPF: TryGetClipboardRepositoryUrl
        private static string TryGetClipboardRepositoryUrl()
        {
            string text = ServiceLocator.Clipboard?.GetText();
            if (string.IsNullOrWhiteSpace(text))
            {
                return null;
            }
            text = NormalizeClipboardRepositoryUrl(text);
            try
            {
                GitUrl gitUrl = new GitUrl(text);
                if (!gitUrl.IsValid || !LooksLikeRepositoryReference(text))
                {
                    return null;
                }
            }
            catch (ArgumentException)
            {
                return null;
            }
            return text;
        }

        // 对照 WPF: NormalizeClipboardRepositoryUrl
        private static string NormalizeClipboardRepositoryUrl(string clipboardText)
        {
            string text = clipboardText.Trim();
            const string prefix = "git clone ";
            if (text.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            {
                text = text.Substring(prefix.Length);
            }
            return text.Trim().Trim('"');
        }

        // 对照 WPF: LooksLikeRepositoryReference
        private static bool LooksLikeRepositoryReference(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                return false;
            }
            if (text.StartsWith("git@", StringComparison.OrdinalIgnoreCase)
                || text.Contains("@vs-ssh.visualstudio.com")
                || text.Contains("://"))
            {
                return true;
            }
            if (!Path.IsPathRooted(text)
                && !text.StartsWith(".", StringComparison.Ordinal)
                && !text.StartsWith("\\\\", StringComparison.Ordinal))
            {
                return false;
            }
            if (Directory.Exists(text))
            {
                return true;
            }
            return text.EndsWith(".git", StringComparison.OrdinalIgnoreCase);
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

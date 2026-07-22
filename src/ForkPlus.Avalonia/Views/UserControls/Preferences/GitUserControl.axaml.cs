// 对照 WPF 工程 src/ForkPlus/UI/UserControls/Preferences/GitUserControl.xaml.cs（494 行）：
//   - public partial class GitUserControl : UserControl
//   - 嵌套类 GitInstanceItem（FileName / GitPath / GitInstanceType）+
//     静态工厂方法（CreateEnvironmentGitInstance / CreateLocalGitInstance /
//     CreateSystemGitInstance / CreateCustomGitInstance / CreateSeparator /
//     CreateAddCustomGitInstance / CreateAddCustomGitMmInstance）
//   - 嵌套枚举 GitInstanceType（Environment/Local/System/Custom/Separator/AddCustom）
//   - 字段：DelayedAction<UserIdentity> _updateAvatarAction /
//     ForkPlusDialogWindow _parentWindow / bool _isRefreshingGitMm
//   - Initialize(ForkPlusDialogWindow)：RefreshGitInstanceComboBox +
//     RefreshGitMmInstanceComboBox + VerboseGitOutputCheckBox + 读 UserIdentity
//   - GitInstanceComboBox_SelectionChanged：写 GitInstancePath + 弹 OpenDialog +
//     WarnIfGitVersionUnsupported
//   - SetGlobalUserIdentity：async + new SetGlobalUserIdentityGitCommand().Execute
//   - RefreshGitInstanceComboBox / RefreshGitMmInstanceComboBox：构造 GitInstanceItem 列表
//
// Avalonia 版差异（spike 简化策略）：
//   1. WPF ForkPlusDialogWindow _parentWindow == spike 用 object? 占位
//   2. WPF DelayedAction<UserIdentity> == spike 移除（spike 不做防抖）
//   3. WPF AuthorAvatarImage.ShowAvatarNoCache(userIdentity) == spike 移除
//   4. WPF ErrorWindow.ShowDialog() == spike 移除（spike 用 Log.Error 替代）
//   5. WPF OpenDialog.SelectExecutableFile == spike 移除（spike 用 stub 注释）
//   6. WPF TextBlock ToolTip = new TextBlock { ... } == spike 用 ToolTip.SetTip
//   7. WPF Hyperlink RequestNavigate == spike 移除
//   8. WPF App.EnvironmentGitInstancePath / App.ForkGitInstancePath / App.GitPath /
//      App.GitMmPathFromPath == spike 保留（Core 的 App 类）
//   9. WPF GitVersionChecker.Check == spike 保留（Core 的 GitVersionChecker）
//  10. WPF GetGitVersionGitCommand / SetGlobalUserIdentityGitCommand /
//      GetGlobalUserIdentityGitCommand == spike 保留（Core 的 Git Commands）
//  11. WPF PathHelper.Normalize == spike 保留（Core 的 PathHelper）
//  12. WPF IReadOnlyListExtensions.FirstItem / ContainsItem == spike 保留（Core 扩展方法）
//  13. namespace 改为 ForkPlus.Avalonia.Views.UserControls.Preferences
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using ForkPlus.Git;
using ForkPlus.Git.Commands;
using ForkPlus.Settings;

namespace ForkPlus.Avalonia.Views.UserControls.Preferences
{
    public partial class GitUserControl : UserControl
    {
        // ===== 嵌套类 GitInstanceItem（对照 WPF 完整保留）=====
        public class GitInstanceItem
        {
            public string FileName { get; }

            public string GitPath { get; }

            public GitInstanceType GitInstanceType { get; }

            public static GitInstanceItem? CreateEnvironmentGitInstance()
            {
                string text = GitVersion(App.EnvironmentGitInstancePath);
                if (text != null)
                {
                    return new GitInstanceItem(text + " - ENV git instance " + App.EnvironmentGitInstancePath, App.EnvironmentGitInstancePath, GitInstanceType.Environment);
                }
                return null;
            }

            public static GitInstanceItem? CreateLocalGitInstance()
            {
                string text = GitVersion(App.ForkGitInstancePath);
                if (text != null)
                {
                    return new GitInstanceItem(text + " - Fork git instance", App.ForkGitInstancePath, GitInstanceType.Local);
                }
                return null;
            }

            public static GitInstanceItem? CreateCustomGitInstance(string normalizedPath)
            {
                if (ValidatePath(normalizedPath))
                {
                    string text = GitVersion(normalizedPath);
                    if (text != null)
                    {
                        return new GitInstanceItem(text + " - " + normalizedPath, normalizedPath, GitInstanceType.Custom);
                    }
                }
                return null;
            }

            public static GitInstanceItem? CreateSystemGitInstance()
            {
                string text = TryFindExistingInstance(new string[3] { "%programfiles(x86)%\\Git\\bin\\git.exe", "%programfiles%\\Git\\bin\\git.exe", "%ProgramW6432%\\Git\\bin\\git.exe" });
                if (text != null)
                {
                    string text2 = GitVersion(text);
                    if (text2 != null)
                    {
                        return new GitInstanceItem(text2 + " - " + text, text, GitInstanceType.System);
                    }
                }
                return null;
            }

            public static GitInstanceItem CreateSeparator()
            {
                return new GitInstanceItem(string.Empty, string.Empty, GitInstanceType.Separator);
            }

            public static GitInstanceItem CreateAddCustomGitInstance()
            {
                return new GitInstanceItem(PreferencesLocalization.Current("Custom Git Instance..."), string.Empty, GitInstanceType.AddCustom);
            }

            public static GitInstanceItem CreateAddCustomGitMmInstance()
            {
                return new GitInstanceItem(PreferencesLocalization.Current("Custom git-mm Instance..."), string.Empty, GitInstanceType.AddCustom);
            }

            internal GitInstanceItem(string fileName, string path, GitInstanceType itemType)
            {
                FileName = fileName;
                GitPath = path;
                GitInstanceType = itemType;
            }

            private static string? GitVersion(string path)
            {
                GitCommandResult<string> gitCommandResult = new GetGitVersionGitCommand().Execute(path);
                if (gitCommandResult.Succeeded)
                {
                    return gitCommandResult.Result;
                }
                return null;
            }

            private static string? TryFindExistingInstance(string[] possiblePaths)
            {
                foreach (string text in possiblePaths)
                {
                    try
                    {
                        string text2 = Environment.ExpandEnvironmentVariables(text);
                        if (File.Exists(text2))
                        {
                            return text2;
                        }
                    }
                    catch (Exception ex)
                    {
                        Log.Error("Failed to check '" + text + "' existence", ex);
                    }
                }
                return null;
            }

            // 对照 WPF: private static bool ValidatePath(string gitExecutablePath)
            // spike 版：移除 ErrorWindow.ShowDialog()（spike 用 Log.Error 替代）
            private static bool ValidatePath(string gitExecutablePath)
            {
                try
                {
                    if (!File.Exists(gitExecutablePath))
                    {
                        Log.Error("Cannot find git instance at: '" + gitExecutablePath + "'", null);
                        return false;
                    }
                    if (Path.GetFileName(gitExecutablePath) != "git.exe")
                    {
                        Log.Error("Invalid git binary: '" + gitExecutablePath + "'", null);
                        return false;
                    }
                    string directoryName = Path.GetDirectoryName(gitExecutablePath);
                    if (Directory.Exists(directoryName))
                    {
                        if (!File.Exists(Path.Combine(directoryName, "bash.exe")))
                        {
                            Log.Error("Cannot find git instance at: '" + gitExecutablePath + "'. Missing bash.exe", null);
                            return false;
                        }
                        if (!File.Exists(Path.Combine(directoryName, "sh.exe")))
                        {
                            Log.Error("Cannot find git instance at: '" + gitExecutablePath + "'. Missing sh.exe", null);
                            return false;
                        }
                    }
                }
                catch (Exception ex)
                {
                    Log.Error("Path validation failed '" + gitExecutablePath + "'", ex);
                }
                return true;
            }
        }

        // ===== 嵌套枚举 GitInstanceType（对照 WPF 完整保留）=====
        public enum GitInstanceType
        {
            Environment,
            Local,
            System,
            Custom,
            Separator,
            AddCustom
        }

        // 对照 WPF: private static readonly string VerboseGitOutputTooltip = "..."
        private static readonly string VerboseGitOutputTooltip = "GIT_TRACE=true\nEnables general trace output. Shows internal Git operations like command execution, file operations, and subprocess spawning.\n\nGIT_TRACE_CURL=true\nEnables verbose output from libcurl for HTTP/HTTPS operations. Shows request/response headers, SSL handshake details, and transfer progress when using HTTP-based remotes.\n\nGIT_SSH_COMMAND=\"ssh -vvv\"\nSets the SSH command with maximum verbosity (-vvv). Shows detailed SSH connection debugging: key exchange, authentication attempts, channel operations, and protocol negotiation when using SSH-based remotes.\n\nGIT_TRACE_PACKFILE=true\nTraces packfile operations. Shows details about how Git packs and unpacks objects during fetch/push operations.\n\nGIT_TRACE_PERFORMANCE=true\nShows performance timing data. Reports how long various Git operations take, useful for diagnosing slow operations.";

        // 对照 WPF: private DelayedAction<UserIdentity> _updateAvatarAction;
        // spike 版：移除 DelayedAction（spike 不做防抖）

        // 对照 WPF: private ForkPlusDialogWindow _parentWindow;
        // spike 版：用 object? 占位（避免 WPF ForkPlusDialogWindow 依赖）
        private object? _parentWindow;

        // 对照 WPF: private bool _isRefreshingGitMm;
        private bool _isRefreshingGitMm;

        public GitUserControl()
        {
            InitializeComponent();
            // spike: _updateAvatarAction = new DelayedAction<UserIdentity>(UpdateAvatar, 0.3); // 移除
        }

        // 对照 WPF: public void Initialize(ForkPlusDialogWindow parentWindow)
        // spike 版：parentWindow 类型改为 object?（spike 占位）
        public void Initialize(object? parentWindow)
        {
            _parentWindow = parentWindow;
            RefreshGitInstanceComboBox();
            RefreshGitMmInstanceComboBox();
            VerboseGitOutputCheckBox.IsChecked = ForkPlusSettings.Default.VerboseGitOutput;
            // spike 版：用 ToolTip.SetTip 替代 WPF TextBlock ToolTip
            ToolTip.SetTip(VerboseGitOutputCheckBox, VerboseGitOutputTooltip);
            UserIdentity result = new GetGlobalUserIdentityGitCommand().Execute().Result;
            UserNameTextBox.Text = result.Name ?? "";
            EmailTextBox.Text = result.Email ?? "";
            // spike: _updateAvatarAction.InvokeNow(new UserIdentity(UserNameTextBox.Text, EmailTextBox.Text)); // 移除
        }

        // 对照 WPF: private void VerboseGitOutputCheckBox_Checked(object sender, RoutedEventArgs e)
        private void VerboseGitOutputCheckBox_Click(object? sender, RoutedEventArgs e)
        {
            ForkPlusSettings.Default.VerboseGitOutput = VerboseGitOutputCheckBox.IsChecked.GetValueOrDefault();
        }

        // 对照 WPF: private void UserNameTextBox_LostFocus(object sender, RoutedEventArgs e)
        private void UserNameTextBox_LostFocus(object? sender, RoutedEventArgs e)
        {
            SetGlobalUserIdentity();
        }

        // 对照 WPF: private void EmailTextBox_LostFocus(object sender, RoutedEventArgs e)
        private void EmailTextBox_LostFocus(object? sender, RoutedEventArgs e)
        {
            SetGlobalUserIdentity();
        }

        // 对照 WPF: private void UserNameTextBox_TextChanged(object sender, TextChangedEventArgs e)
        // spike 版：移除 _updateAvatarAction.InvokeWithDelay
        private void UserNameTextBox_TextChanged(object? sender, TextChangedEventArgs e)
        {
            // spike: _updateAvatarAction.InvokeWithDelay(new UserIdentity(UserNameTextBox.Text, EmailTextBox.Text)); // 移除
        }

        // 对照 WPF: private void EmailTextBox_TextChanged(object sender, TextChangedEventArgs e)
        // spike 版：移除 _updateAvatarAction.InvokeWithDelay
        private void EmailTextBox_TextChanged(object? sender, TextChangedEventArgs e)
        {
            // spike: _updateAvatarAction.InvokeWithDelay(new UserIdentity(UserNameTextBox.Text, EmailTextBox.Text)); // 移除
        }

        // 对照 WPF: private void UpdateAvatar(UserIdentity userIdentity)
        // spike 版：移除（spike 不渲染头像）
        private void UpdateAvatar(UserIdentity userIdentity)
        {
            // spike: AuthorAvatarImage.ShowAvatarNoCache(userIdentity); // 移除
        }

        // 对照 WPF: private async void SetGlobalUserIdentity()
        // spike 版：保留 async + SetGlobalUserIdentityGitCommand，移除 ErrorWindow.ShowDialog
        private async void SetGlobalUserIdentity()
        {
            try
            {
                string userName = (UserNameTextBox.Text ?? "").Trim();
                string email = (EmailTextBox.Text ?? "").Trim();
                GitCommandResult gitCommandResult = await Task.Run(delegate
                {
                    GitCommandResult gitCommandResult2 = new SetGlobalUserIdentityGitCommand().Execute(new UserIdentity(userName, email));
                    return (!gitCommandResult2.Succeeded) ? gitCommandResult2 : GitCommandResult.Success();
                });
                if (!gitCommandResult.Succeeded)
                {
                    // spike: new ErrorWindow(null, gitCommandResult.Error).ShowDialog(); // 移除
                    Log.Error("SetGlobalUserIdentity failed: " + (gitCommandResult.Error?.FriendlyDescription ?? ""), null);
                }
            }
            catch (Exception ex)
            {
                Log.Error("SetGlobalUserIdentity failed", ex);
            }
        }

        // 对照 WPF: private void GitInstanceComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        // spike 版：移除 OpenDialog.SelectExecutableFile + ErrorWindow.ShowDialog
        private void GitInstanceComboBox_SelectionChanged(object? sender, SelectionChangedEventArgs e)
        {
            GitInstanceItem? selectedItem = ((e.RemovedItems != null && e.RemovedItems.Count > 0) ? (e.RemovedItems[0] as GitInstanceItem) : null);
            if (!(GitInstanceComboBox.SelectedItem is GitInstanceItem gitInstanceItem))
            {
                return;
            }
            switch (gitInstanceItem.GitInstanceType)
            {
                case GitInstanceType.Local:
                    ForkPlusSettings.Default.GitInstancePath = null;
                    break;
                case GitInstanceType.System:
                case GitInstanceType.Custom:
                    ForkPlusSettings.Default.GitInstancePath = gitInstanceItem.GitPath;
                    break;
                case GitInstanceType.AddCustom:
                    {
                        // spike 版：移除 OpenDialog.SelectExecutableFile（spike 不弹文件对话框）
                        // spike: string initialDirectory = Environment.ExpandEnvironmentVariables("%userprofile%");
                        //        if (OpenDialog.SelectExecutableFile(_parentWindow, PreferencesLocalization.Current("Select git instance"), initialDirectory, out var filePath))
                        //        { ... }
                        //        else { GitInstanceComboBox.SelectedItem = selectedItem; }
                        // spike 版：回退到之前选中项
                        GitInstanceComboBox.SelectedItem = selectedItem;
                        break;
                    }
            }
            Log.Info("Git Location: " + App.GitPath);
            WarnIfGitVersionUnsupported(App.GitPath);
        }

        // 对照 WPF: private void GitMmInstanceComboBox_SelectionChanged(...)
        // spike 版：spike 不处理 git-mm 选择（spike 占位）
        private void GitMmInstanceComboBox_SelectionChanged(object? sender, SelectionChangedEventArgs e)
        {
            // spike: 完整逻辑在 WPF 中处理 GitMmInstancePath 选择 + OpenDialog，
            //        spike 版仅占位（RefreshGitMmInstanceComboBox 已填充候选项）
        }

        // 对照 WPF: private static void WarnIfGitVersionUnsupported(string gitPath)
        // spike 版：保留 GitVersionChecker.Check，移除 ErrorWindow.ShowDialog
        private static void WarnIfGitVersionUnsupported(string gitPath)
        {
            try
            {
                GitVersionCheckResult result = GitVersionChecker.Check(gitPath);
                if (result.Status == GitVersionStatus.Unsupported)
                {
                    string versionText = result.Version != null ? result.Version.ToString(3) : "?";
                    string minText = GitVersionChecker.MinimumRequiredVersion.ToString(2);
                    // spike: new ErrorWindow(...).ShowDialog(); // 移除
                    Log.Error(PreferencesLocalization.FormatCurrent(
                        "Detected git version {0} is older than the required {1}. Some features (diff, status, empty-changes detection) may not work correctly. Please upgrade git.",
                        versionText, minText), null);
                }
                else if (result.Status == GitVersionStatus.Outdated)
                {
                    string versionText = result.Version != null ? result.Version.ToString(3) : "?";
                    string recText = GitVersionChecker.RecommendedVersion.ToString(2);
                    // spike: new ErrorWindow(...).ShowDialog(); // 移除
                    Log.Error(PreferencesLocalization.FormatCurrent(
                        "Detected git version {0} is below the recommended {1}. Consider upgrading for better compatibility.",
                        versionText, recText), null);
                }
            }
            catch (Exception ex)
            {
                Log.Error("Failed to check git version on selection", ex);
            }
        }

        // 对照 WPF: private void RefreshGitInstanceComboBox()
        private void RefreshGitInstanceComboBox()
        {
            List<GitInstanceItem> list = new List<GitInstanceItem>(5);
            GitInstanceItem? gitInstanceItem = GitInstanceItem.CreateEnvironmentGitInstance();
            if (gitInstanceItem != null)
            {
                list.Add(gitInstanceItem);
            }
            GitInstanceItem? gitInstanceItem2 = GitInstanceItem.CreateLocalGitInstance();
            if (gitInstanceItem2 != null)
            {
                list.Add(gitInstanceItem2);
            }
            GitInstanceItem? gitInstanceItem3 = GitInstanceItem.CreateSystemGitInstance();
            if (gitInstanceItem3 != null)
            {
                list.Add(gitInstanceItem3);
            }
            string? currentGitInstancePath = ForkPlusSettings.Default.GitInstancePath;
            GitInstanceItem? gitInstanceItem4 = null;
            if (currentGitInstancePath != null && !list.ContainsItem((GitInstanceItem x) => x.GitPath == currentGitInstancePath))
            {
                gitInstanceItem4 = GitInstanceItem.CreateCustomGitInstance(currentGitInstancePath);
                if (gitInstanceItem4 != null)
                {
                    list.Add(gitInstanceItem4);
                }
            }
            list.Add(GitInstanceItem.CreateSeparator());
            list.Add(GitInstanceItem.CreateAddCustomGitInstance());
            GitInstanceComboBox.ItemsSource = list.ToArray();
            GitInstanceComboBox.IsEnabled = true;
            if (gitInstanceItem != null)
            {
                GitInstanceComboBox.SelectedItem = gitInstanceItem;
                GitInstanceComboBox.IsEnabled = false;
            }
            else if (currentGitInstancePath == null)
            {
                GitInstanceComboBox.SelectedItem = gitInstanceItem2;
            }
            else if (gitInstanceItem3 != null && currentGitInstancePath == gitInstanceItem3.GitPath)
            {
                GitInstanceComboBox.SelectedItem = gitInstanceItem3;
            }
            else
            {
                GitInstanceComboBox.SelectedItem = gitInstanceItem4 ?? gitInstanceItem2;
            }
        }

        // 对照 WPF: private void RefreshGitMmInstanceComboBox()
        // spike 版：简化为只填充 "添加自定义..." 入口（spike 不扫 PATH）
        private void RefreshGitMmInstanceComboBox()
        {
            _isRefreshingGitMm = true;
            try
            {
                List<GitInstanceItem> list = new List<GitInstanceItem>(4);
                // spike 版：跳过 PATH 扫描 + 用户保存路径加载（spike 占位）
                list.Add(GitInstanceItem.CreateAddCustomGitMmInstance());
                GitMmInstanceComboBox.ItemsSource = list.ToArray();
            }
            finally
            {
                _isRefreshingGitMm = false;
            }
        }
    }
}

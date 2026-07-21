using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Diagnostics;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using ForkPlus.Git.Commands;
using ForkPlus.Services;
using ForkPlus.Settings;

namespace ForkPlus.Avalonia.Dialogs
{
    // Phase 4.x：Avalonia 版 ConfigureGitInstanceWindow（真实迁移版，对照 WPF ConfigureGitInstanceWindow.xaml.cs 292 行）。
    //
    // 对照 WPF 工程 src/ForkPlus/UI/Dialogs/ConfigureGitInstanceWindow.xaml.cs：
    //   - public partial class ConfigureGitInstanceWindow : ForkPlusDialogWindow
    //   - 嵌套类：public sealed class GitCandidate { Source / Version / Path / Title }
    //   - IsSubmitAllowed: IsGitPathValid(GitPathTextBox.Text.Trim()) && base.IsSubmitAllowed
    //   - 构造函数：DialogTitle/Description/SubmitButtonTitle/CancelButtonTitle 翻译 +
    //     ShowWarningIcon=true + PreferencesLocalization.Apply +
    //     GetGitCandidates() → GitCandidatesListBox.ItemsSource +
    //     GitPathTextBox.Text = candidates.FirstOrDefault()?.Path ?? ExistingGitPath() +
    //     Loaded += SelectCurrentCandidate + GitPathTextBox.Focus + UpdateSubmitButton
    //   - OnSubmit: PathHelper.Normalize + ValidateGitPath(showError=true) + 保存
    //     ForkPlusSettings.Default.GitInstancePath + ForkPlusSettings.Default.Save() + CloseWithOk()
    //   - BrowseButton_Click: OpenFileDialog 选 git.exe（Filter "Applications (*.exe)|*.exe"）
    //     失败时 new ErrorWindow(message).ShowDialog()
    //   - GitPathTextBox_TextChanged: SelectCurrentCandidate + UpdateSubmitButton
    //   - GitCandidatesListBox_SelectionChanged: 选中候选路径填入 GitPathTextBox
    //   - SelectCurrentCandidate: 找到与 GitPathTextBox.Text 匹配的候选并选中
    //   - ExistingGitPath: ForkPlusSettings.Default.GitInstancePath 或 %programfiles%\Git\bin\git.exe
    //   - GetGitCandidates: Saved Git / Environment Git / ForkPlus Git / System PATH / Common location / PortableGit
    //   - GetPathGitCandidates: 遍历 PATH 环境变量，每条目录下找 git.exe
    //   - GetCommonGitCandidates: 6 个常见 Windows 路径
    //   - GetPortableGitCandidates: 4 个根目录下查 PortableGit* 目录
    //   - AddCandidate: 验证文件名是 git.exe + PathHelper.Normalize + GetGitVersionGitCommand 执行成功
    //   - IsGitPathValid / ValidateGitPath: 文件存在 + 文件名为 git.exe + GetGitVersionGitCommand 成功
    //   - IsSamePath: PathHelper.Normalize 后比较
    //   - Translate: PreferencesLocalization.Translate(text, ForkPlusSettings.Default.UiLanguage)
    //
    // Avalonia 版差异（spike 简化策略）：
    //   1. spike 模式：构造函数 SetFooter/Footer 注入
    //   2. spike 构造函数注入 (Action<string>? onGitPathConfigured) 回调替代直接保存
    //   3. OpenFileDialog → StorageProvider.OpenFilePickerAsync
    //   4. spike 版跨平台：Windows 检查 git.exe，Unix 检查 git（无扩展名）
    //      （GetGitVersionGitCommand.Execute 已支持任意路径，spike 版放宽 filename 校验）
    //   5. spike 版省略 GetPortableGitCandidates（依赖 Windows 特定路径，spike 简化）
    //   6. spike 版 ShowWarningIcon=true 改为在 DescriptionTextBlock 上方加 emoji "⚠" TextBlock
    //   7. SelectionChangedEventArgs → Avalonia.Controls.SelectionChangedEventArgs
    //   8. PreferencesLocalization → ServiceLocator.Localization.Translate
    //   9. Process.Start 跨平台下载 URL → ProcessStartInfo + UseShellExecute=true
    //  10. spike 版 GitCandidate 嵌套类与 WPF 一致（保留以兼容 ListBox.ItemsSource 绑定）
    //  11. spike 基类不提供 GetCommandPreview/RefreshCommandPreview/DisableEditableControls
    public partial class ConfigureGitInstanceWindow : global::ForkPlus.Avalonia.Dialogs.ForkPlusDialogWindow
    {
        // 对照 WPF: public sealed class GitCandidate
        public sealed class GitCandidate
        {
            public string Source { get; }
            public string Version { get; }
            public string Path { get; }
            public string Title => Source + " - " + Version;

            public GitCandidate(string source, string version, string path)
            {
                Source = source;
                Version = version;
                Path = path;
            }

            public override string ToString() => Title;
        }

        private readonly Action<string>? _onGitPathConfigured;

        // 构造函数签名与 WPF 不同：注入 Action<string>? onGitPathConfigured 回调替代直接保存
        public ConfigureGitInstanceWindow(Action<string>? onGitPathConfigured = null)
        {
            ShowFooter = true;
            InitializeComponent();
            SetFooter(Footer);
            SetTitleTextBlock(TitleTextBlock);
            SetDescriptionTextBlock(DescriptionTextBlock);

            _onGitPathConfigured = onGitPathConfigured;

            // 对照 WPF: base.DialogTitle / DialogDescription / SubmitButtonTitle / CancelButtonTitle
            DialogTitle = Translate("Configure Git");
            DialogDescription = Translate("ForkPlus requires a valid Git executable to continue.");
            SubmitButtonTitle = Translate("Continue");
            CancelButtonTitle = Translate("Exit");
            Title = Translate("Configure Git");

            // 对照 WPF: base.ShowWarningIcon = true;
            // spike 版：用 WarningIconTextBlock (emoji "⚠") 替代 PNG 图标（已在 axaml 中声明）

            // 对照 WPF: PreferencesLocalization.Apply(this, ForkPlusSettings.Default.UiLanguage);
            // spike 版：spike 基类不提供 ApplyAutomaticLocalization，子类自己 Translate

            // 对照 WPF: List<GitCandidate> candidates = GetGitCandidates();
            //           GitCandidatesListBox.ItemsSource = candidates;
            List<GitCandidate> candidates = GetGitCandidates();
            GitCandidatesListBox.ItemsSource = candidates;

            // 对照 WPF: GitPathTextBox.Text = candidates.FirstOrDefault()?.Path ?? ExistingGitPath();
            GitPathTextBox.Text = candidates.FirstOrDefault()?.Path ?? ExistingGitPath();

            // 对照 WPF: base.Loaded += delegate { SelectCurrentCandidate(); GitPathTextBox.Focus(); UpdateSubmitButton(); };
            Loaded += (_, _) =>
            {
                SelectCurrentCandidate();
                GitPathTextBox.Focus();
                UpdateSubmitButton();
            };

            // 对照 WPF: DownloadButton（spike 版新增，提供下载 git 的入口）
            DownloadButton.Content = Translate("Download Git");
        }

        // 对照 WPF: protected override bool IsSubmitAllowed => IsGitPathValid(GitPathTextBox.Text.Trim()) && base.IsSubmitAllowed;
        protected override bool IsSubmitAllowed =>
            IsGitPathValid((GitPathTextBox.Text ?? "").Trim()) && base.IsSubmitAllowed;

        // 对照 WPF: protected override void OnSubmit()
        protected override void OnSubmit()
        {
            // 对照 WPF: string gitPath = PathHelper.Normalize(GitPathTextBox.Text.Trim());
            string gitPath = PathHelper.Normalize((GitPathTextBox.Text ?? "").Trim());
            if (!ValidateGitPath(gitPath, showError: true))
            {
                UpdateSubmitButton();
                return;
            }
            // 对照 WPF: ForkPlusSettings.Default.GitInstancePath = gitPath;
            //           ForkPlusSettings.Default.Save();
            //           CloseWithOk();
            ForkPlusSettings.Default.GitInstancePath = gitPath;
            ForkPlusSettings.Default.Save();

            // spike 版：通过回调通知调用方 git 路径已配置
            try { _onGitPathConfigured?.Invoke(gitPath); }
            catch (Exception ex) { Log.Error("ConfigureGitInstanceWindow onGitPathConfigured callback failed", ex); }

            CloseWithOk();
        }

        // 对照 WPF: private void BrowseButton_Click(object sender, RoutedEventArgs e)
        public async void BrowseButton_Click(object? sender, RoutedEventArgs e)
        {
            try
            {
                // 对照 WPF: string initialDirectory = Directory.Exists(Path.GetDirectoryName(GitPathTextBox.Text)) ?
                //   Path.GetDirectoryName(GitPathTextBox.Text) : Environment.ExpandEnvironmentVariables("%programfiles%");
                string currentText = (GitPathTextBox.Text ?? "").Trim();
                string initialDirectory = Directory.Exists(Path.GetDirectoryName(currentText))
                    ? Path.GetDirectoryName(currentText)
                    : GetDefaultProgramFilesPath();

                var topLevel = TopLevel.GetTopLevel(this);
                if (topLevel == null) return;

                var options = new FilePickerOpenOptions
                {
                    Title = Translate("Select git instance"),
                    AllowMultiple = false,
                    FileTypeFilter = new[]
                    {
                        new FilePickerFileType(Translate("Applications"))
                        {
                            Patterns = new[] { "*.exe" }
                        }
                    }
                };
                if (Directory.Exists(initialDirectory))
                {
                    try
                    {
                        var uri = new Uri(Path.GetFullPath(initialDirectory));
                        var folder = await topLevel.StorageProvider.TryGetFolderFromPathAsync(uri);
                        if (folder != null) options.SuggestedStartLocation = folder;
                    }
                    catch { }
                }

                var result = await topLevel.StorageProvider.OpenFilePickerAsync(options);
                if (result != null && result.Count > 0)
                {
                    GitPathTextBox.Text = result[0].Path.LocalPath;
                    GitPathTextBox.Focus();
                }
            }
            catch (Exception ex)
            {
                // 对照 WPF: Log.Error("Failed to show git instance picker", ex);
                //           new ErrorWindow(Translate("Unable to open file picker. Please type the git.exe path manually.")).ShowDialog();
                // spike 版：在 DialogDescription 中显示错误（不弹 ErrorWindow）
                Log.Error("Failed to show git instance picker", ex);
                DialogDescription = Translate("Unable to open file picker. Please type the git.exe path manually.");
            }
        }

        // 对照 WPF: spike 版新增 DownloadButton_Click：跨平台打开 git 下载页
        public void DownloadButton_Click(object? sender, RoutedEventArgs e)
        {
            OpenUrl("https://git-scm.com/downloads");
        }

        // 对照 WPF: private void GitPathTextBox_TextChanged(object sender, TextChangedEventArgs e)
        public void GitPathTextBox_TextChanged(object? sender, TextChangedEventArgs e)
        {
            SelectCurrentCandidate();
            UpdateSubmitButton();
        }

        // 对照 WPF: private void GitCandidatesListBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        public void GitCandidatesListBox_SelectionChanged(object? sender, SelectionChangedEventArgs e)
        {
            if (GitCandidatesListBox.SelectedItem is GitCandidate candidate
                && !string.Equals(GitPathTextBox.Text, candidate.Path, StringComparison.OrdinalIgnoreCase))
            {
                GitPathTextBox.Text = candidate.Path;
                GitPathTextBox.Focus();
            }
        }

        // 对照 WPF: private void SelectCurrentCandidate()
        private void SelectCurrentCandidate()
        {
            if (GitCandidatesListBox == null) return;
            string currentPath = (GitPathTextBox.Text ?? "").Trim();
            GitCandidate? candidate = GitCandidatesListBox.Items
                .OfType<GitCandidate>()
                .FirstOrDefault((GitCandidate item) => IsSamePath(item.Path, currentPath));
            if (!Equals(GitCandidatesListBox.SelectedItem, candidate))
            {
                GitCandidatesListBox.SelectedItem = candidate;
            }
        }

        // 对照 WPF: private static string ExistingGitPath()
        private static string ExistingGitPath()
        {
            if (!string.IsNullOrWhiteSpace(ForkPlusSettings.Default.GitInstancePath))
            {
                return ForkPlusSettings.Default.GitInstancePath;
            }
            // spike 版跨平台：Windows 检查 %programfiles%\Git\bin\git.exe，Unix 用 which git
            if (Environment.OSVersion.Platform == PlatformID.Win32NT)
            {
                string programFiles = Environment.ExpandEnvironmentVariables("%programfiles%\\Git\\bin\\git.exe");
                if (File.Exists(programFiles))
                {
                    return programFiles;
                }
                string programFilesX86 = Environment.ExpandEnvironmentVariables("%programfiles(x86)%\\Git\\bin\\git.exe");
                if (File.Exists(programFilesX86))
                {
                    return programFilesX86;
                }
            }
            return "";
        }

        // 对照 WPF: private static List<GitCandidate> GetGitCandidates()
        private static List<GitCandidate> GetGitCandidates()
        {
            List<GitCandidate> result = new List<GitCandidate>();
            HashSet<string> seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            // 对照 WPF: AddCandidate(result, seen, Translate("Saved Git"), ForkPlusSettings.Default.GitInstancePath);
            AddCandidate(result, seen, Translate("Saved Git"), ForkPlusSettings.Default.GitInstancePath);

            // 对照 WPF: AddCandidate(result, seen, Translate("Environment Git"), App.EnvironmentGitInstancePath);
            // spike 版：用 ServiceLocator.GitEnvironment 替代 App.EnvironmentGitInstancePath
            string envGitPath = ServiceLocator.GitEnvironment?.EnvironmentGitInstancePath;
            AddCandidate(result, seen, Translate("Environment Git"), envGitPath);

            // 对照 WPF: AddCandidate(result, seen, Translate("ForkPlus Git"), App.ForkGitInstancePath);
            // spike 版：用 ServiceLocator.GitEnvironment 替代 App.ForkGitInstancePath
            string forkGitPath = ServiceLocator.GitEnvironment?.ForkGitInstancePath;
            AddCandidate(result, seen, Translate("ForkPlus Git"), forkGitPath);

            // 对照 WPF: foreach (string path in GetPathGitCandidates()) AddCandidate(result, seen, Translate("System PATH"), path);
            foreach (string path in GetPathGitCandidates())
            {
                AddCandidate(result, seen, Translate("System PATH"), path);
            }

            // 对照 WPF: foreach (string path in GetCommonGitCandidates()) AddCandidate(result, seen, Translate("Common location"), path);
            foreach (string path in GetCommonGitCandidates())
            {
                AddCandidate(result, seen, Translate("Common location"), path);
            }

            // 对照 WPF: foreach (string path in GetPortableGitCandidates()) AddCandidate(result, seen, Translate("PortableGit"), path);
            // spike 版：省略 GetPortableGitCandidates（依赖 Windows 特定路径，spike 简化）

            return result;
        }

        // 对照 WPF: private static IEnumerable<string> GetPathGitCandidates()
        private static IEnumerable<string> GetPathGitCandidates()
        {
            string pathVariable = Environment.GetEnvironmentVariable("PATH") ?? "";
            // spike 版跨平台：Windows 查找 git.exe，Unix 查找 git
            string gitExeName = Environment.OSVersion.Platform == PlatformID.Win32NT ? "git.exe" : "git";
            foreach (string directory in pathVariable.Split(Path.PathSeparator))
            {
                if (string.IsNullOrWhiteSpace(directory)) continue;
                string gitPath;
                try
                {
                    gitPath = Path.Combine(Environment.ExpandEnvironmentVariables(directory.Trim()), gitExeName);
                }
                catch
                {
                    continue;
                }
                if (File.Exists(gitPath))
                {
                    yield return gitPath;
                }
            }
        }

        // 对照 WPF: private static IEnumerable<string> GetCommonGitCandidates()
        // spike 版跨平台：Unix 上无候选（返回空），Windows 上检查 6 个常见路径
        private static IEnumerable<string> GetCommonGitCandidates()
        {
            if (Environment.OSVersion.Platform != PlatformID.Win32NT)
            {
                yield break;
            }
            string[] paths =
            {
                "%programfiles%\\Git\\bin\\git.exe",
                "%programfiles%\\Git\\cmd\\git.exe",
                "%programfiles(x86)%\\Git\\bin\\git.exe",
                "%programfiles(x86)%\\Git\\cmd\\git.exe",
                "%localappdata%\\Programs\\Git\\bin\\git.exe",
                "%localappdata%\\Programs\\Git\\cmd\\git.exe"
            };
            foreach (string path in paths)
            {
                string expanded = Environment.ExpandEnvironmentVariables(path);
                if (File.Exists(expanded))
                {
                    yield return expanded;
                }
            }
        }

        // 对照 WPF: private static void AddCandidate(List<GitCandidate> result, HashSet<string> seen, string source, string gitPath)
        private static void AddCandidate(List<GitCandidate> result, HashSet<string> seen, string source, string gitPath)
        {
            if (string.IsNullOrWhiteSpace(gitPath) || !File.Exists(gitPath)) return;
            // spike 版跨平台：Windows 要求文件名为 git.exe，Unix 要求文件名为 git
            string fileName = Path.GetFileName(gitPath);
            string expectedName = Environment.OSVersion.Platform == PlatformID.Win32NT ? "git.exe" : "git";
            if (!string.Equals(fileName, expectedName, StringComparison.OrdinalIgnoreCase))
            {
                return;
            }
            string normalizedPath = PathHelper.Normalize(gitPath);
            if (!seen.Add(normalizedPath)) return;
            // 对照 WPF: GitCommandResult<string> versionResult = new GetGitVersionGitCommand().Execute(normalizedPath);
            GitCommandResult<string> versionResult = new GetGitVersionGitCommand().Execute(normalizedPath);
            if (versionResult.Succeeded)
            {
                result.Add(new GitCandidate(source, versionResult.Result, normalizedPath));
            }
        }

        // 对照 WPF: private static bool IsGitPathValid(string gitPath) => ValidateGitPath(gitPath, showError: false);
        // spike 版：改为实例方法，因 ValidateGitPath 需要写入 this.DialogDescription 显示错误
        private bool IsGitPathValid(string gitPath) => ValidateGitPath(gitPath, showError: false);

        // 对照 WPF: private static bool ValidateGitPath(string gitPath, bool showError)
        // spike 版：改为实例方法，因 spike 版在 DialogDescription 中显示错误（不弹 ErrorWindow）
        private bool ValidateGitPath(string gitPath, bool showError)
        {
            // spike 版跨平台：Windows 要求文件名为 git.exe，Unix 要求文件名为 git
            string expectedName = Environment.OSVersion.Platform == PlatformID.Win32NT ? "git.exe" : "git";
            if (string.IsNullOrWhiteSpace(gitPath)
                || !File.Exists(gitPath)
                || !string.Equals(Path.GetFileName(gitPath), expectedName, StringComparison.OrdinalIgnoreCase))
            {
                if (showError)
                {
                    // 对照 WPF: new ErrorWindow(Translate("Please select a valid git.exe file.")).ShowDialog();
                    // spike 版：在 DialogDescription 中显示错误
                    DialogDescription = Translate("Please select a valid git.exe file.");
                }
                return false;
            }
            if (!new GetGitVersionGitCommand().Execute(gitPath).Succeeded)
            {
                if (showError)
                {
                    // 对照 WPF: new ErrorWindow(Translate("Unable to run selected Git executable.")).ShowDialog();
                    // spike 版：在 DialogDescription 中显示错误
                    DialogDescription = Translate("Unable to run selected Git executable.");
                }
                return false;
            }
            return true;
        }

        // 对照 WPF: private static bool IsSamePath(string left, string right)
        private static bool IsSamePath(string left, string right)
        {
            if (string.IsNullOrWhiteSpace(left) || string.IsNullOrWhiteSpace(right)) return false;
            return string.Equals(PathHelper.Normalize(left), PathHelper.Normalize(right), StringComparison.OrdinalIgnoreCase);
        }

        // spike 版：跨平台获取 Program Files 路径（对照 WPF Environment.ExpandEnvironmentVariables("%programfiles%")）
        private static string GetDefaultProgramFilesPath()
        {
            if (Environment.OSVersion.Platform == PlatformID.Win32NT)
            {
                return Environment.ExpandEnvironmentVariables("%programfiles%");
            }
            // Unix 无 Program Files 概念，用 /usr/bin 作为浏览起点
            return "/usr/bin";
        }

        // spike 版：跨平台打开 URL（对照 WPF Process.Start(url)）
        // ProcessStartInfo + UseShellExecute = true
        private static void OpenUrl(string url)
        {
            try
            {
                var psi = new ProcessStartInfo { UseShellExecute = true };
                if (OperatingSystem.IsWindows())
                {
                    psi.FileName = url;
                }
                else if (OperatingSystem.IsMacOS())
                {
                    psi.FileName = "open";
                    psi.Arguments = $"\"{url}\"";
                }
                else
                {
                    psi.FileName = "xdg-open";
                    psi.Arguments = $"\"{url}\"";
                }
                Process.Start(psi);
            }
            catch (Exception ex)
            {
                Log.Error("Failed to open URL '" + url + "'", ex);
            }
        }

        // 对照 WPF: private static string Translate(string text)
        //   return PreferencesLocalization.Translate(text, ForkPlusSettings.Default.UiLanguage);
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

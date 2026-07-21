using System;
using System.Threading.Tasks;
using Avalonia.Threading;
using ForkPlus.Git;
using ForkPlus.Git.Commands;
using ForkPlus.Jobs;
using ForkPlus.Services;

namespace ForkPlus.Avalonia.Dialogs
{
    // Phase 4.31b：Avalonia 版 DeleteSubmoduleWindow（真实迁移版，对照 WPF DeleteSubmoduleWindow.xaml.cs 56 行）。
    //
    // 对照 WPF 工程 src/ForkPlus/UI/Dialogs/DeleteSubmoduleWindow.xaml.cs：
    //   - public partial class DeleteSubmoduleWindow : ForkPlusDialogWindow
    //   - 字段: GitModule _gitModule / Submodule _submodule
    //   - 构造函数 (GitModule gitModule, Submodule submodule)
    //   - GetCommandPreview override: "git submodule deinit -f {path} && git rm -f {path}"
    //   - OnSubmit: DisableEditableControls + SetStatus(InProgress) + JobQueue.Add
    //     → DeleteSubmoduleGitCommand().Execute(_gitModule, path, monitor) → Dispatcher.Async(Close(result))
    //
    // Avalonia 版差异：
    //   1. spike 模式：构造函数 SetFooter/Footer 注入
    //   2. spike 基类不提供 GetCommandPreview → 自行维护 CommandPreviewTextBlock
    //   3. spike 基类不提供 DisableEditableControls/EnableEditableControls → 手动禁用
    //   4. JobQueue + Dispatcher.Async → Task.Run + Dispatcher.UIThread.Post + JobMonitor
    //   5. PreferencesLocalization.Translate → ServiceLocator.Localization.Translate
    public partial class DeleteSubmoduleWindow : global::ForkPlus.Avalonia.Dialogs.ForkPlusDialogWindow
    {
        private readonly GitModule _gitModule;
        private readonly Submodule _submodule;

        public DeleteSubmoduleWindow(GitModule gitModule, Submodule submodule)
        {
            ShowFooter = true;
            InitializeComponent();
            SetFooter(Footer);
            SetTitleTextBlock(TitleTextBlock);
            SetDescriptionTextBlock(DescriptionTextBlock);

            _gitModule = gitModule ?? throw new ArgumentNullException(nameof(gitModule));
            _submodule = submodule ?? throw new ArgumentNullException(nameof(submodule));

            // 对照 WPF: DialogTitle = FormatTranslate("Are you sure you want to delete submodule {0}?", friendlyName)
            string title = FormatTranslate("Are you sure you want to delete submodule {0}?", _submodule.FriendlyName);
            Title = title;
            DialogTitle = title;
            DialogDescription = FormatTranslate("Do you want to delete submodule {0}?", _submodule.Path);
            SubmitButtonTitle = Translate("Delete");
            CancelButtonTitle = Translate("Cancel");

            // 对照 WPF: GetCommandPreview override
            CommandPreviewTextBox.Text = GetCommandPreview();
        }

        // 对照 WPF: protected override string GetCommandPreview()
        private string GetCommandPreview()
        {
            string path = _submodule.Path;
            string Quote(string s) => s.Contains(" ") ? "\"" + s + "\"" : s;
            return "git submodule deinit -f " + Quote(path) + " && git rm -f " + Quote(path);
        }

        // 对照 WPF: protected override void OnSubmit()
        protected override void OnSubmit()
        {
            DisableEditableControls();
            SetStatus(ForkPlusDialogStatus.InProgress, Translate("Deleting submodule..."));

            // 对照 WPF: MainWindow.ActiveRepositoryUserControl.JobQueue.Add(...)
            // Avalonia: Task.Run + Dispatcher.UIThread.Post + JobMonitor
            Task.Run(delegate
            {
                JobMonitor monitor = new JobMonitor();
                GitCommandResult result = new DeleteSubmoduleGitCommand().Execute(_gitModule, _submodule.Path, monitor);
                Dispatcher.UIThread.Post(delegate
                {
                    Close(result);
                });
            });
        }

        // spike 版：手动禁用可编辑控件（基类不提供 DisableEditableControls）
        private void DisableEditableControls()
        {
            // 本对话框无可编辑控件，只需禁用 Submit/Cancel 按钮（通过 SetStatus InProgress 自动处理）
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

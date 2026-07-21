using System;
using System.Collections.Generic;
using Avalonia.Controls;
using ForkPlus.Git;
using ForkPlus.Services;

namespace ForkPlus.Avalonia.Dialogs
{
    // Phase 4.42b：Avalonia 版 RepositorySettingsWindow（真实迁移版，对照 WPF RepositorySettingsWindow.xaml.cs 56 行）。
    //
    // 对照 WPF 工程 src/ForkPlus/UI/Dialogs/RepositorySettingsWindow.xaml.cs：
    //   - public partial class RepositorySettingsWindow : ForkPlusDialogWindow
    //   - 字段: GitModule _gitModule, RepositoryData _repositoryData
    //   - 构造函数 (GitModule gitModule, RepositoryData repositoryData)
    //   - Initialize(): GeneralUserControl.Initialize(_gitModule) / IssueTrackerUserControl.Initialize /
    //     CommitTemplateUserControl.Initialize / CustomCommandsUserControl.InitializeLocal
    //   - OnSubmit / OnClosing: 调用各子控件的 Save() 方法
    //   - SubmitButtonTitle = "Close" / ShowCancelButton = false / SizeToContent = WidthAndHeight
    //
    // Avalonia 版差异：
    //   1. spike 模式：构造函数 SetFooter/Footer 注入
    //   2. 4 个 RepositorySettings 子 UserControl（GeneralUserControl / IssueTrackerUserControl /
    //      CommitTemplateUserControl / CustomCommandsUserControl）spike 版未迁移，
    //      用 TextBlock 占位说明 Phase 4 后续迁移
    //   3. ModernTabControl → Avalonia 原生 TabControl
    //   4. PreferencesLocalization.Current → ServiceLocator.Localization.Translate
    //   5. OnSubmit / OnClosing 调用各子控件的 Save() → spike 版无子控件，跳过
    //   6. SubmitButtonTitle = "Close" / ShowCancelButton = false（与 WPF 一致）
    public partial class RepositorySettingsWindow : global::ForkPlus.Avalonia.Dialogs.ForkPlusDialogWindow
    {
        private readonly GitModule _gitModule;
        private readonly RepositoryData _repositoryData;

        public RepositorySettingsWindow(GitModule gitModule, RepositoryData repositoryData)
        {
            ShowFooter = true;
            InitializeComponent();
            SetFooter(Footer);
            SetTitleTextBlock(TitleTextBlock);
            SetDescriptionTextBlock(DescriptionTextBlock);

            _gitModule = gitModule ?? throw new ArgumentNullException(nameof(gitModule));
            _repositoryData = repositoryData ?? throw new ArgumentNullException(nameof(repositoryData));

            // 对照 WPF: base.ShowLogo = false; base.ShowCancelButton = false;
            ShowCancelButton = false;

            // 对照 WPF: base.SubmitButtonTitle = PreferencesLocalization.Current("Close");
            SubmitButtonTitle = Translate("Close");

            // 对照 WPF: DialogTitle / DialogDescription
            string title = Translate("Repository Settings");
            Title = title;
            DialogTitle = title;
            DialogDescription = Translate("Configure repository-specific settings such as commit template, issue tracker, and custom commands.");

            // 对照 WPF: Initialize();
            // spike 版：4 个子 UserControl 未迁移，TabItem 中用 TextBlock 占位（已在 axaml 内联）
            // Initialize();

            // 对照 WPF: OnClosing → 持久化（Avalonia 用 Closed 事件等价）
            Closed += RepositorySettingsWindow_Closed;
        }

        // 对照 WPF: public void Initialize()
        // spike 版：4 个子 UserControl 未迁移，方法体为空（TabItem 占位内容由 axaml 内联提供）
        public void Initialize()
        {
            // GeneralUserControl.Initialize(_gitModule);
            // IssueTrackerUserControl.Initialize(this, _gitModule);
            // CommitTemplateUserControl.Initialize(_gitModule);
            // CustomCommandsUserControl.InitializeLocal(this, _gitModule, _repositoryData);
        }

        // 对照 WPF: protected override void OnSubmit()
        protected override void OnSubmit()
        {
            base.OnSubmit();
            SaveAllSettings();
        }

        // 对照 WPF: protected override void OnClosing(CancelEventArgs e)
        // Avalonia: Closed 事件（不可取消，但 WPF 版只在 Save 子控件，不需要取消）
        private void RepositorySettingsWindow_Closed(object? sender, EventArgs e)
        {
            SaveAllSettings();
        }

        // 对照 WPF: 调用各子控件的 Save() 方法
        // spike 版：4 个子 UserControl 未迁移，方法体为空
        private void SaveAllSettings()
        {
            // GeneralUserControl.Save();
            // IssueTrackerUserControl.Save();
            // CommitTemplateUserControl.Save();
            // CustomCommandsUserControl.Save();
        }

        // 对照 WPF: PreferencesLocalization.Current(text)
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

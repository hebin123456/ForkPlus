using System;
using ForkPlus.Git;
using ForkPlus.Git.Commands;
using ForkPlus.Services;

namespace ForkPlus.Avalonia.Dialogs
{
    // Phase 4.30b：Avalonia 版 TagDetailsWindow（真实迁移版，对照 WPF TagDetailsWindow.xaml.cs 45 行）。
    //
    // 对照 WPF 工程 src/ForkPlus/UI/Dialogs/TagDetailsWindow.xaml.cs：
    //   - public partial class TagDetailsWindow : ForkPlusDialogWindow
    //   - 构造函数 (GitModule gitModule, Tag tag)
    //   - 只读对话框，无 OnSubmit
    //   - 调用 GetTagMessageGitCommand().Execute(gitModule, tagSha) 获取 AnnotatedTagDetails
    //   - 显示: TaggerAvatarImage / TaggerTextBlock / TaggerEmailTextBlock / TaggerDateTextBlock
    //           / GitPointView / TagDetailsTextBox
    //
    // Avalonia 版差异：
    //   1. spike 模式：构造函数 SetFooter/Footer 注入
    //   2. AvatarImage 自定义控件 → spike 版用 Border + TextBlock 显示 tagger name 首字母占位
    //   3. SelectableTextBlock → Avalonia TextBlock（原生支持文本选择）
    //   4. GitPointView 自定义控件 → spike 版用 TextBlock 显示 tag name 简化
    //   5. PreferencesLocalization.Current → ServiceLocator.Localization.Translate
    //   6. ShowSubmitButton = false（只读对话框，只有 Close 按钮）
    public partial class TagDetailsWindow : global::ForkPlus.Avalonia.Dialogs.ForkPlusDialogWindow
    {
        public TagDetailsWindow(GitModule gitModule, Tag tag)
        {
            ShowFooter = true;
            InitializeComponent();
            SetFooter(Footer);
            SetTitleTextBlock(TitleTextBlock);
            SetDescriptionTextBlock(DescriptionTextBlock);

            // 对照 WPF: DialogTitle = PreferencesLocalization.Current("Tag Details");
            string title = Translate("Tag Details");
            Title = title;
            DialogTitle = title;
            DialogDescription = "";
            // 只读对话框：隐藏 Submit 按钮，只保留 Close
            ShowSubmitButton = false;
            CancelButtonTitle = Translate("Close");

            // 对照 WPF: GitPointView.Value = tag;
            // Avalonia spike: 用 TextBlock 显示 tag name 简化
            TagNameTextBlock.Text = tag.Name;

            // 对照 WPF: 调用 GetTagMessageGitCommand().Execute() 获取 AnnotatedTagDetails
            var gitCommandResult = new GetTagMessageGitCommand().Execute(gitModule, tag.TargetObjectSha.Value);
            if (gitCommandResult.Succeeded)
            {
                var result = gitCommandResult.Result;
                // spike 版：AvatarImage 用 tagger name 首字母占位
                TaggerAvatarPlaceholder.Text = GetInitial(result.Tagger?.Name);
                TaggerTextBlock.Text = result.Tagger?.Name ?? "";
                TaggerEmailTextBlock.Text = result.Tagger?.Email ?? "";
                TaggerDateTextBlock.Text = result.TaggerDate.ToString(Consts.FullDateTimeFormat);
                TagDetailsTextBox.Text = result.Message;
            }
            else
            {
                TaggerAvatarPlaceholder.Text = "";
                TaggerTextBlock.Text = "";
                TaggerEmailTextBlock.Text = "";
                TaggerDateTextBlock.Text = "";
                // 对照 WPF: TagDetailsTextBox.Text = new GetTagMessageGitCommand().Execute(gitModule, tag.Name).Result;
                TagDetailsTextBox.Text = new GetTagMessageGitCommand().Execute(gitModule, tag.Name).Result;
            }
        }

        // spike 版：从 tagger name 提取首字母作为头像占位（参考 AvatarImage 简化逻辑）
        private static string GetInitial(string? name)
        {
            if (string.IsNullOrEmpty(name))
            {
                return "?";
            }
            return name.Substring(0, 1).ToUpperInvariant();
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

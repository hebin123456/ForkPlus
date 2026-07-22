using ForkPlus.Avalonia.Views.UserControls;
using ForkPlus.Services;
using ForkPlus.Settings;

namespace ForkPlus.Avalonia.Commands
{
    // 对照 WPF src/ForkPlus/UI/Commands/ShowAddGitignoreTemplateWindowCommand.cs
    // WPF: 弹出 AddGitignoreTemplateWindow 从模板列表添加 .gitignore 内容。
    public class ShowAddGitignoreTemplateWindowCommand : IUICommand
    {
        public string Id => "ShowAddGitignoreTemplateWindow";
        public string Header => ServiceLocator.Localization.Translate("Add .gitignore Template", ForkPlusSettings.Default.UiLanguage);
        public string Icon => "📄";
        public string ShortcutText => "";

        public void Execute(RepositoryUserControl? repo)
        {
            // spike: new AddGitignoreTemplateWindow().ShowDialog() → 追加模板到 .gitignore
        }

        public bool CanExecute(RepositoryUserControl? repo) => repo?.GitModule != null;
    }
}

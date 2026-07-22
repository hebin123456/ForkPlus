using ForkPlus.Avalonia.Views.UserControls;
using ForkPlus.Services;
using ForkPlus.Settings;

namespace ForkPlus.Avalonia.Commands
{
    // 对照 WPF src/ForkPlus/UI/Commands/ShowAddGitIgnorePatternWindowCommand.cs
    // WPF: 弹出 AddGitIgnorePatternWindow 添加单条 .gitignore 规则。
    public class ShowAddGitIgnorePatternWindowCommand : IUICommand
    {
        public string Id => "ShowAddGitIgnorePatternWindow";
        public string Header => ServiceLocator.Localization.Translate("Add to .gitignore", ForkPlusSettings.Default.UiLanguage);
        public string Icon => "🚫";
        public string ShortcutText => "";

        public void Execute(RepositoryUserControl? repo)
        {
            // spike: new AddGitIgnorePatternWindow().ShowDialog() → 写入 .gitignore
        }

        public bool CanExecute(RepositoryUserControl? repo) => repo?.GitModule != null;
    }
}

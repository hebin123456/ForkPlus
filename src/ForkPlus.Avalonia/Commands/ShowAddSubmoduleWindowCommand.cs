using ForkPlus.Avalonia.Views.UserControls;
using ForkPlus.Services;
using ForkPlus.Settings;

namespace ForkPlus.Avalonia.Commands
{
    // 对照 WPF src/ForkPlus/UI/Commands/ShowAddSubmoduleWindowCommand.cs
    // WPF: 弹出 AddSubmoduleWindow 添加子模块（git submodule add）。
    public class ShowAddSubmoduleWindowCommand : IUICommand
    {
        public string Id => "ShowAddSubmoduleWindow";
        public string Header => ServiceLocator.Localization.Translate("Add Submodule...", ForkPlusSettings.Default.UiLanguage);
        public string Icon => "📦";
        public string ShortcutText => "";

        public void Execute(RepositoryUserControl? repo)
        {
            // spike: new AddSubmoduleWindow(repo).ShowDialog() → AddSubmoduleGitCommand
        }

        public bool CanExecute(RepositoryUserControl? repo) => repo?.GitModule != null;
    }
}

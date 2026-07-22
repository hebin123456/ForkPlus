using ForkPlus.Avalonia.Views.UserControls;
using ForkPlus.Services;
using ForkPlus.Settings;

namespace ForkPlus.Avalonia.Commands
{
    // 对照 WPF src/ForkPlus/UI/Commands/ShowDeleteSubmoduleWindowCommand.cs
    // WPF: 弹出 DeleteSubmoduleWindow 确认删除子模块（git submodule deinit + rm）。
    public class ShowDeleteSubmoduleWindowCommand : IUICommand
    {
        public string Id => "ShowDeleteSubmoduleWindow";
        public string Header => ServiceLocator.Localization.Translate("Delete Submodule...", ForkPlusSettings.Default.UiLanguage);
        public string Icon => "🗑";
        public string ShortcutText => "";

        public void Execute(RepositoryUserControl? repo)
        {
            // spike: new DeleteSubmoduleWindow(repo, submodule).ShowDialog() → DeleteSubmoduleGitCommand
        }

        public bool CanExecute(RepositoryUserControl? repo) => repo?.GitModule != null;
    }
}

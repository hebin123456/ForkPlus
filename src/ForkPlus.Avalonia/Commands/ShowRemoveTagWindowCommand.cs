using ForkPlus.Avalonia.Views.UserControls;
using ForkPlus.Services;
using ForkPlus.Settings;

namespace ForkPlus.Avalonia.Commands
{
    // 对照 WPF src/ForkPlus/UI/Commands/ShowRemoveTagWindowCommand.cs
    // WPF: 弹出 RemoveTagWindow 删除本地 + 远端 tag（git tag -d / git push :refs/tags/）。
    public class ShowRemoveTagWindowCommand : IUICommand
    {
        public string Id => "ShowRemoveTagWindow";
        public string Header => ServiceLocator.Localization.Translate("Remove Tag...", ForkPlusSettings.Default.UiLanguage);
        public string Icon => "🗑";
        public string ShortcutText => "";

        public void Execute(RepositoryUserControl? repo)
        {
            // spike: new RemoveTagWindow(repo, tag).ShowDialog() → DeleteTagGitCommand
        }

        public bool CanExecute(RepositoryUserControl? repo) => repo?.GitModule != null;
    }
}

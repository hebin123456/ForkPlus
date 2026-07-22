using ForkPlus.Avalonia.Views.UserControls;
using ForkPlus.Services;
using ForkPlus.Settings;

namespace ForkPlus.Avalonia.Commands
{
    // 对照 WPF src/ForkPlus/UI/Commands/ShowHeadCommand.cs
    // WPF: 在 revision 列表中跳到 HEAD。Ctrl+0。
    public class ShowHeadCommand : IUICommand
    {
        public string Id => "ShowHead";
        public string Header => ServiceLocator.Localization.Translate("Show HEAD", ForkPlusSettings.Default.UiLanguage);
        public string Icon => "🎯";
        public string ShortcutText => "Ctrl+0";

        public void Execute(RepositoryUserControl? repo)
        {
            // spike: repo.ActivateRevisionView() → repo.SelectAndScrollIntoView(Head) → SidebarRevealActiveBranch
        }

        public bool CanExecute(RepositoryUserControl? repo) => repo?.GitModule != null;
    }
}

using ForkPlus.Avalonia.Views.UserControls;
using ForkPlus.Services;
using ForkPlus.Settings;

namespace ForkPlus.Avalonia.Commands
{
    // 对照 WPF src/ForkPlus/UI/Commands/ShowCheckoutRevisionWindowCommand.cs
    // WPF: 弹出 CheckoutRevisionWindow 检出到指定 revision（detached HEAD）。
    public class ShowCheckoutRevisionWindowCommand : IUICommand
    {
        public string Id => "ShowCheckoutRevisionWindow";
        public string Header => ServiceLocator.Localization.Translate("Checkout Revision...", ForkPlusSettings.Default.UiLanguage);
        public string Icon => "🔀";
        public string ShortcutText => "";

        public void Execute(RepositoryUserControl? repo)
        {
            // spike: new CheckoutRevisionWindow(repo).ShowDialog() → CheckoutRevisionGitCommand
        }

        public bool CanExecute(RepositoryUserControl? repo) => repo?.GitModule != null;
    }
}

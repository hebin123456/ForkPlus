using ForkPlus.Avalonia.Views.UserControls;
using ForkPlus.Services;
using ForkPlus.Settings;

namespace ForkPlus.Avalonia.Commands
{
    // 对照 WPF src/ForkPlus/UI/Commands/ShowCheckoutBranchWindowCommand.cs
    // WPF: 弹出 CheckoutBranchWindow 切换本地分支（git checkout branch）。
    public class ShowCheckoutBranchWindowCommand : IUICommand
    {
        public string Id => "ShowCheckoutBranchWindow";
        public string Header => ServiceLocator.Localization.Translate("Checkout Branch...", ForkPlusSettings.Default.UiLanguage);
        public string Icon => "🔀";
        public string ShortcutText => "";

        public void Execute(RepositoryUserControl? repo)
        {
            // spike: new CheckoutBranchWindow(repo).ShowDialog() → CheckoutBranchGitCommand
        }

        public bool CanExecute(RepositoryUserControl? repo) => repo?.GitModule != null;
    }
}

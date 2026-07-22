using ForkPlus.Avalonia.Views.UserControls;
using ForkPlus.Services;
using ForkPlus.Settings;

namespace ForkPlus.Avalonia.Commands
{
    // 对照 WPF src/ForkPlus/UI/Commands/RescanUserRepositoriesCommand.cs
    // WPF: 重新扫描用户配置的仓库目录，发现新增/丢失的仓库。
    public class RescanUserRepositoriesCommand : IUICommand
    {
        public string Id => "RescanUserRepositories";
        public string Header => ServiceLocator.Localization.Translate("Rescan Repositories...", ForkPlusSettings.Default.UiLanguage);
        public string Icon => "🔍";
        public string ShortcutText => "";

        public void Execute(RepositoryUserControl? repo)
        {
            // spike: 弹出 RescanRepositoriesWindow → RepositoryManager.Instance.Rescan
        }

        public bool CanExecute(RepositoryUserControl? repo) => true;
    }
}

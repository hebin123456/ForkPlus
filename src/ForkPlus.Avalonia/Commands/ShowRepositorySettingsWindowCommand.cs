using ForkPlus.Avalonia.Views.UserControls;
using ForkPlus.Services;
using ForkPlus.Settings;

namespace ForkPlus.Avalonia.Commands
{
    // 对照 WPF src/ForkPlus/UI/Commands/ShowRepositorySettingsWindowCommand.cs
    // WPF: 打开 RepositorySettingsWindow 显示仓库级设置（git config / .gitattributes / ...）。
    public class ShowRepositorySettingsWindowCommand : IUICommand
    {
        public string Id => "ShowRepositorySettingsWindow";
        public string Header => ServiceLocator.Localization.Translate("Repository Settings", ForkPlusSettings.Default.UiLanguage);
        public string Icon => "⚙️";
        public string ShortcutText => "";

        public void Execute(RepositoryUserControl? repo)
        {
            // spike: new RepositorySettingsWindow(repo).ShowDialog()
        }

        public bool CanExecute(RepositoryUserControl? repo) => repo?.GitModule != null;
    }
}

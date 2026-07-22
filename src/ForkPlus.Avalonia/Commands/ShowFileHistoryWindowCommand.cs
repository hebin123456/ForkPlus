using ForkPlus.Avalonia.Views.UserControls;
using ForkPlus.Services;
using ForkPlus.Settings;

namespace ForkPlus.Avalonia.Commands
{
    // 对照 WPF src/ForkPlus/UI/Commands/ShowFileHistoryWindowCommand.cs
    // WPF: 打开 FileHistoryWindow 显示文件历史（git log --follow <file>）。
    public class ShowFileHistoryWindowCommand : IUICommand
    {
        public string Id => "ShowFileHistoryWindow";
        public string Header => ServiceLocator.Localization.Translate("File History", ForkPlusSettings.Default.UiLanguage);
        public string Icon => "📜";
        public string ShortcutText => "";

        public void Execute(RepositoryUserControl? repo)
        {
            // spike: new FileHistoryWindow(repo, file).ShowDialog()
        }

        public bool CanExecute(RepositoryUserControl? repo) => repo?.GitModule != null;
    }
}

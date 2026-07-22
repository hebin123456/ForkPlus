using ForkPlus.Avalonia.Views.UserControls;
using ForkPlus.Services;
using ForkPlus.Settings;

namespace ForkPlus.Avalonia.Commands
{
    // 对照 WPF src/ForkPlus/UI/Commands/ShowBlameWindowCommand.cs
    // WPF: 打开 BlameWindow 显示文件的 git blame（每行最后修改者）。
    public class ShowBlameWindowCommand : IUICommand
    {
        public string Id => "ShowBlameWindow";
        public string Header => ServiceLocator.Localization.Translate("Blame", ForkPlusSettings.Default.UiLanguage);
        public string Icon => "🔍";
        public string ShortcutText => "";

        public void Execute(RepositoryUserControl? repo)
        {
            // spike: new BlameWindow(repo, file, revision).ShowDialog()
        }

        public bool CanExecute(RepositoryUserControl? repo) => repo?.GitModule != null;
    }
}

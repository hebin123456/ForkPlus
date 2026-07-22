using ForkPlus.Avalonia.Views.UserControls;
using ForkPlus.Services;
using ForkPlus.Settings;

namespace ForkPlus.Avalonia.Commands
{
    // 对照 WPF src/ForkPlus/UI/Commands/ResetFileToStateAtRevisionCommand.cs
    // WPF: 把选中的文件重置到指定 revision 的状态（CheckoutFileGitCommand）。
    public class ResetFileToStateAtRevisionCommand : IUICommand
    {
        public string Id => "ResetFileToStateAtRevision";
        public string Header => ServiceLocator.Localization.Translate("Reset to this Revision", ForkPlusSettings.Default.UiLanguage);
        public string Icon => "↩";
        public string ShortcutText => "";

        public void Execute(RepositoryUserControl? repo)
        {
            // spike: new CheckoutFileGitCommand().Execute(gitModule, file, revision)
        }

        public bool CanExecute(RepositoryUserControl? repo) => repo?.GitModule != null;
    }
}

using ForkPlus.Avalonia.Views.UserControls;
using ForkPlus.Services;
using ForkPlus.Settings;

namespace ForkPlus.Avalonia.Commands
{
    // 对照 WPF src/ForkPlus/UI/Commands/CopyRevisionShaCommand.cs
    // WPF: 把选中提交的 SHA 复制到剪贴板（Ctrl+C）。
    public class CopyRevisionShaCommand : IUICommand
    {
        public string Id => "CopyRevisionSha";
        public string Header => ServiceLocator.Localization.Translate("Copy Commit SHA", ForkPlusSettings.Default.UiLanguage);
        public string Icon => "📋";
        public string ShortcutText => "Ctrl+C";

        public void Execute(RepositoryUserControl? repo)
        {
            // spike: ServiceLocator.Clipboard.SetText(revision.Sha.ToString())
        }

        public bool CanExecute(RepositoryUserControl? repo) => repo?.GitModule != null;
    }
}

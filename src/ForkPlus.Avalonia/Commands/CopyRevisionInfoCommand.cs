using ForkPlus.Avalonia.Views.UserControls;
using ForkPlus.Services;
using ForkPlus.Settings;

namespace ForkPlus.Avalonia.Commands
{
    // 对照 WPF src/ForkPlus/UI/Commands/CopyRevisionInfoCommand.cs
    // WPF: 把 "SHA - Message" 格式的提交信息复制到剪贴板（Ctrl+Shift+C）。
    public class CopyRevisionInfoCommand : IUICommand
    {
        public string Id => "CopyRevisionInfo";
        public string Header => ServiceLocator.Localization.Translate("Copy Commit Info", ForkPlusSettings.Default.UiLanguage);
        public string Icon => "📋";
        public string ShortcutText => "Ctrl+Shift+C";

        public void Execute(RepositoryUserControl? repo)
        {
            // spike: ServiceLocator.Clipboard.SetText("SHA - Message" 拼接)
        }

        public bool CanExecute(RepositoryUserControl? repo) => repo?.GitModule != null;
    }
}

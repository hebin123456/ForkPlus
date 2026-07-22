using ForkPlus.Avalonia.Views.UserControls;
using ForkPlus.Services;
using ForkPlus.Settings;

namespace ForkPlus.Avalonia.Commands
{
    // 对照 WPF src/ForkPlus/UI/Commands/CopyRemoteAddressCommand.cs
    // WPF: ServiceLocator.Clipboard.SetText(remoteAddress) — 复制远端 URL 到剪贴板。
    public class CopyRemoteAddressCommand : IUICommand
    {
        public string Id => "CopyRemoteAddress";
        public string Header => ServiceLocator.Localization.Translate("Copy Remote Address", ForkPlusSettings.Default.UiLanguage);
        public string Icon => "📋";
        public string ShortcutText => "";

        public void Execute(RepositoryUserControl? repo)
        {
            // spike: ServiceLocator.Clipboard.SetText(remoteAddress)
        }

        public bool CanExecute(RepositoryUserControl? repo) => repo?.GitModule != null;
    }
}

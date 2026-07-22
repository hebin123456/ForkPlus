using ForkPlus.Avalonia.Views.UserControls;
using ForkPlus.Services;
using ForkPlus.Settings;

namespace ForkPlus.Avalonia.Commands
{
    // 对照 WPF src/ForkPlus/UI/Commands/CopyReferenceNameCommand.cs
    // WPF: ServiceLocator.Clipboard.SetText(reference.Name) — 复制引用名到剪贴板。
    public class CopyReferenceNameCommand : IUICommand
    {
        public string Id => "CopyReferenceName";
        public string Header => ServiceLocator.Localization.Translate("Copy Reference Name", ForkPlusSettings.Default.UiLanguage);
        public string Icon => "📋";
        public string ShortcutText => "";

        public void Execute(RepositoryUserControl? repo)
        {
            // spike: ServiceLocator.Clipboard.SetText(reference.Name)
        }

        public bool CanExecute(RepositoryUserControl? repo) => repo?.GitModule != null;
    }
}

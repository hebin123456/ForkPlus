using ForkPlus.Avalonia.Views.UserControls;
using ForkPlus.Services;
using ForkPlus.Settings;

namespace ForkPlus.Avalonia.Commands
{
    // 对照 WPF src/ForkPlus/UI/Commands/OpenSubmoduleCommand.cs
    // WPF: 打开选中的子模块作为独立仓库。
    public class OpenSubmoduleCommand : IUICommand
    {
        public string Id => "OpenSubmodule";
        public string Header => ServiceLocator.Localization.Translate("Open Submodule", ForkPlusSettings.Default.UiLanguage);
        public string Icon => "📦";
        public string ShortcutText => "";

        public void Execute(RepositoryUserControl? repo)
        {
            // spike: 调用 TabManager.OpenRepository(submodulePath)
        }

        public bool CanExecute(RepositoryUserControl? repo) => repo?.GitModule != null;
    }
}

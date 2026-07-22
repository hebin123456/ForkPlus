using ForkPlus.Avalonia.Views.UserControls;
using ForkPlus.Services;
using ForkPlus.Settings;

namespace ForkPlus.Avalonia.Commands
{
    // 对照 WPF src/ForkPlus/UI/Commands/UpdateSubmoduleCommand.cs
    // WPF: 更新选中的子模块到最新 commit（git submodule update）。
    public class UpdateSubmoduleCommand : IUICommand
    {
        public string Id => "UpdateSubmodule";
        public string Header => ServiceLocator.Localization.Translate("Update Submodule", ForkPlusSettings.Default.UiLanguage);
        public string Icon => "📦";
        public string ShortcutText => "";

        public void Execute(RepositoryUserControl? repo)
        {
            // spike: SubmoduleUpdateGitCommand → repo.InvalidateAndRefresh
        }

        public bool CanExecute(RepositoryUserControl? repo) => repo?.GitModule != null;
    }
}

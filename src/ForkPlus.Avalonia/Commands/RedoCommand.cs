using ForkPlus.Avalonia.Views.UserControls;
using ForkPlus.Services;
using ForkPlus.Settings;

namespace ForkPlus.Avalonia.Commands
{
    // 对照 WPF src/ForkPlus/UI/Commands/RedoCommand.cs
    // WPF: 重做最近被撤销的仓库操作（v3.0.0+）。Ctrl+Shift+Z（兼容 Ctrl+Y）。
    public class RedoCommand : IUICommand
    {
        public string Id => "Redo";
        public string Header => ServiceLocator.Localization.Translate("Redo", ForkPlusSettings.Default.UiLanguage);
        public string Icon => "↷";
        public string ShortcutText => "Ctrl+Shift+Z";

        public void Execute(RepositoryUserControl? repo)
        {
            // spike: repo.Redo()
        }

        public bool CanExecute(RepositoryUserControl? repo) => repo != null;
    }
}

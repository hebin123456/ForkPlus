using ForkPlus.Avalonia.Views.UserControls;
using ForkPlus.Services;
using ForkPlus.Settings;

namespace ForkPlus.Avalonia.Commands
{
    // 对照 WPF src/ForkPlus/UI/Commands/UndoCommand.cs
    // WPF: 撤销最近一次仓库操作（commit / reset / checkout / merge / 等，v3.0.0+）。Ctrl+Z。
    public class UndoCommand : IUICommand
    {
        public string Id => "Undo";
        public string Header => ServiceLocator.Localization.Translate("Undo", ForkPlusSettings.Default.UiLanguage);
        public string Icon => "↶";
        public string ShortcutText => "Ctrl+Z";

        public void Execute(RepositoryUserControl? repo)
        {
            // spike: repo.Undo()
        }

        public bool CanExecute(RepositoryUserControl? repo) => repo != null;
    }
}

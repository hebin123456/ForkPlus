using ForkPlus.Avalonia.Views.UserControls;
using ForkPlus.Services;
using ForkPlus.Settings;

namespace ForkPlus.Avalonia.Commands
{
    // 对照 WPF src/ForkPlus/UI/Commands/ShowApplyPatchWindowCommand.cs
    // WPF: 弹出 ApplyPatchWindow 选择并应用 patch 文件（git apply）。
    public class ShowApplyPatchWindowCommand : IUICommand
    {
        public string Id => "ShowApplyPatchWindow";
        public string Header => ServiceLocator.Localization.Translate("Apply Patch...", ForkPlusSettings.Default.UiLanguage);
        public string Icon => "🩹";
        public string ShortcutText => "";

        public void Execute(RepositoryUserControl? repo)
        {
            // spike: new ApplyPatchWindow(repo).ShowDialog() → ApplyPatchGitCommand
        }

        public bool CanExecute(RepositoryUserControl? repo) => repo?.GitModule != null;
    }
}

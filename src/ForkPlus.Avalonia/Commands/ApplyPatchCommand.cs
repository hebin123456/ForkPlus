using ForkPlus.Avalonia.Views.UserControls;
using ForkPlus.Services;
using ForkPlus.Settings;

namespace ForkPlus.Avalonia.Commands
{
    // 对照 WPF src/ForkPlus/UI/Commands/ApplyPatchCommand.cs
    // WPF: OpenDialog.SelectFile 选 patch → ShowApplyPatchWindowCommand.Execute(repo, patchPath)。
    public class ApplyPatchCommand : IUICommand
    {
        public string Id => "ApplyPatch";
        public string Header => ServiceLocator.Localization.Translate("Apply Patch…", ForkPlusSettings.Default.UiLanguage);
        public string Icon => "📦";
        public string ShortcutText => "";

        public void Execute(RepositoryUserControl? repo)
        {
            // spike: OpenDialog.SelectFile → ShowApplyPatchWindowCommand.Execute(repo, patchPath)
        }

        public bool CanExecute(RepositoryUserControl? repo) => repo?.GitModule != null;
    }
}

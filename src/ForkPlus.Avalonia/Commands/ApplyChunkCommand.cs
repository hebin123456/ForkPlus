using ForkPlus.Avalonia.Views.UserControls;
using ForkPlus.Services;
using ForkPlus.Settings;

namespace ForkPlus.Avalonia.Commands
{
    // 对照 WPF src/ForkPlus/UI/Commands/ApplyChunkCommand.cs
    // WPF: 对选中 diff chunk 执行 stage/unstage（CommitUserControl + RepositoryUserControl + Patch）。
    // spike: 核心逻辑在 CommitUserControl 视图层，命令仅暴露 Execute 签名。
    public class ApplyChunkCommand : IUICommand
    {
        public string Id => "ApplyChunk";
        public string Header => ServiceLocator.Localization.Translate("Apply", ForkPlusSettings.Default.UiLanguage);
        public string Icon => "✅";
        public string ShortcutText => "";

        public void Execute(RepositoryUserControl? repo)
        {
            // spike: ApplyGitCommand.Execute(gitModule, staged, patchData) → RefreshFileStatusCommand
        }

        public bool CanExecute(RepositoryUserControl? repo) => repo?.GitModule != null;
    }
}

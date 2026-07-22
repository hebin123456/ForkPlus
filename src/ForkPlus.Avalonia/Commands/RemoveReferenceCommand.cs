using ForkPlus.Avalonia.Views.UserControls;
using ForkPlus.Services;
using ForkPlus.Settings;

namespace ForkPlus.Avalonia.Commands
{
    // 对照 WPF src/ForkPlus/UI/Commands/RemoveReferenceCommand.cs
    // WPF: 删除选中的本地分支 / 远端分支 / tag（带确认对话框）。
    public class RemoveReferenceCommand : IUICommand
    {
        public string Id => "RemoveReference";
        public string Header => ServiceLocator.Localization.Translate("Remove...", ForkPlusSettings.Default.UiLanguage);
        public string Icon => "🗑";
        public string ShortcutText => "Delete";

        public void Execute(RepositoryUserControl? repo)
        {
            // spike: 根据引用类型调用 ShowRemoveLocalBranchWindow / ShowRemoveRemoteBranchWindow / ShowRemoveTagWindow
        }

        public bool CanExecute(RepositoryUserControl? repo) => repo?.GitModule != null;
    }
}

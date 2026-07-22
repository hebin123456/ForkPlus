using ForkPlus.Avalonia.Views.UserControls;

namespace ForkPlus.Avalonia.Commands
{
    // 对照 WPF src/ForkPlus/UI/Commands/UnpinReferenceCommand.cs
    // WPF: 取消置顶（unpin）一个 reference。
    // 注意：WPF 中该类不实现 IUICommand（程序化调用，无菜单项）。
    // spike: 实现 IForkPlusCommand 即可，无 Icon / ShortcutText。
    public class UnpinReferenceCommand : IForkPlusCommand
    {
        public string Id => "UnpinReference";
        public string Header => "Unpin Reference";

        public void Execute(RepositoryUserControl? repo)
        {
            // spike: gitModule.Settings.PinnedReferences 移除并 Save → InvalidateAndRefresh(ReferenceSettings)
        }

        public bool CanExecute(RepositoryUserControl? repo) => repo?.GitModule != null;
    }
}

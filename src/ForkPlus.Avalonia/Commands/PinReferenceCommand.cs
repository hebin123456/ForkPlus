using ForkPlus.Avalonia.Views.UserControls;

namespace ForkPlus.Avalonia.Commands
{
    // 对照 WPF src/ForkPlus/UI/Commands/PinReferenceCommand.cs
    // WPF: 在侧边栏置顶（pin）一个 reference（branch / tag）。
    // 注意：WPF 中该类不实现 IUICommand（程序化调用，无菜单项）。
    // spike: 实现 IForkPlusCommand 即可，无 Icon / ShortcutText。
    public class PinReferenceCommand : IForkPlusCommand
    {
        public string Id => "PinReference";
        public string Header => "Pin Reference";

        public void Execute(RepositoryUserControl? repo)
        {
            // spike: gitModule.Settings.PinnedReferences 添加并 Save → InvalidateAndRefresh(ReferenceSettings)
        }

        public bool CanExecute(RepositoryUserControl? repo) => repo?.GitModule != null;
    }
}

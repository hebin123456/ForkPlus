// RemoteItem.cs：远程仓库条目（POCO）。
//
// WPF 源对照：src/ForkPlus/UI/QuickLaunch/RemoteItem.cs（namespace ForkPlus.UI.QuickLaunch）
//   - class RemoteItem : CommandProviderItem
//   - override ImageSource Icon => Remote.GetIconImage()
//   - override ImageSource SelectedIcon => Remote.GetIconImage()
//   - Remote Remote { get; }
//
// Avalonia 版差异：
//   1. namespace ForkPlus.UI.QuickLaunch → ForkPlus.Avalonia.QuickLaunch
//   2. ImageSource → IImage（Avalonia.Media.IImage）
//   3. Remote.GetIconImage() 为 WPF 专有扩展方法（BridgeExtensions，返回 ImageSource）
//      → spike 返回 null（与 RemoteSidebarItem.cs spike 一致，不迁移图标解析逻辑）
//   4. ForkPlus.Git.Remote 来自 ForkPlus.Core（零修改复用）

using Avalonia.Media;
using ForkPlus.Git;

namespace ForkPlus.Avalonia.QuickLaunch
{
    public class RemoteItem : CommandProviderItem
    {
        // 对照 WPF: public override ImageSource Icon => Remote.GetIconImage()
        // spike: GetIconImage 为 WPF 专有扩展，spike 返回 null
        public override IImage Icon => null;

        // 对照 WPF: public override ImageSource SelectedIcon => Remote.GetIconImage()
        // spike: 同上返回 null
        public override IImage SelectedIcon => null;

        public Remote Remote { get; }

        public RemoteItem(Remote remote)
            : base(remote, remote.Name, "")
        {
            Remote = remote;
        }
    }
}

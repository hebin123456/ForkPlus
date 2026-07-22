// WorkspaceItem.cs：工作区条目（POCO）。
//
// WPF 源对照：src/ForkPlus/UI/QuickLaunch/WorkspaceItem.cs（namespace ForkPlus.UI.QuickLaunch）
//   - class WorkspaceItem : CommandProviderItem
//   - override ImageSource Icon => Application.Current.TryFindResource("WorkspaceIcon") as ImageSource
//   - override ImageSource SelectedIcon => Application.Current.TryFindResource("WorkspaceIcon") as ImageSource
//   - Workspace Workspace { get; }
//
// Avalonia 版差异：
//   1. namespace ForkPlus.UI.QuickLaunch → ForkPlus.Avalonia.QuickLaunch
//   2. ImageSource → IImage（Avalonia.Media.IImage）
//   3. Application.Current.TryFindResource(key) as ImageSource → GetIconResource(key)
//   4. ForkPlus.UI.Workspace 来自 ForkPlus.Core（零修改复用，无 WPF 依赖）

using Avalonia.Media;
using ForkPlus.UI;

namespace ForkPlus.Avalonia.QuickLaunch
{
    public class WorkspaceItem : CommandProviderItem
    {
        // 对照 WPF: public override ImageSource Icon => Application.Current.TryFindResource("WorkspaceIcon") as ImageSource
        public override IImage Icon => GetIconResource("WorkspaceIcon");

        // 对照 WPF: public override ImageSource SelectedIcon => Application.Current.TryFindResource("WorkspaceIcon") as ImageSource
        public override IImage SelectedIcon => GetIconResource("WorkspaceIcon");

        public Workspace Workspace { get; }

        public WorkspaceItem(Workspace workspace)
            : base(workspace, workspace.Name, "")
        {
            Workspace = workspace;
        }
    }
}

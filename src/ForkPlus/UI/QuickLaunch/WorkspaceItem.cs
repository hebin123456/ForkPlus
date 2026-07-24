// 阶段 4.5：WPF→Avalonia 迁移。
// - using System.Windows → using Avalonia
// - using System.Windows.Media → using Avalonia.Media
// - ImageSource → IImage
// - Application.Current.TryFindResource(key) as ImageSource → Theme.FindImage(key)
using Avalonia;
using Avalonia.Media;

namespace ForkPlus.UI.QuickLaunch
{
	public class WorkspaceItem : CommandProviderItem
	{
		public override IImage Icon => Theme.FindImage("WorkspaceIcon");

		public override IImage SelectedIcon => Theme.FindImage("WorkspaceIcon");

		public Workspace Workspace { get; }

		public WorkspaceItem(Workspace workspace)
			: base(workspace, workspace.Name, "")
		{
			Workspace = workspace;
		}
	}
}

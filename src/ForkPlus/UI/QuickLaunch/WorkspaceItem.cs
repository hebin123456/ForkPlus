using Avalonia;
using Avalonia.Media;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Styling;

namespace ForkPlus.UI.QuickLaunch
{
	public class WorkspaceItem : CommandProviderItem
	{
		public override global::Avalonia.Media.IImage Icon => Application.Current.TryFindResource("WorkspaceIcon") as global::Avalonia.Media.IImage;

		public override global::Avalonia.Media.IImage SelectedIcon => Application.Current.TryFindResource("WorkspaceIcon") as global::Avalonia.Media.IImage;

		public Workspace Workspace { get; }

		public WorkspaceItem(Workspace workspace)
			: base(workspace, workspace.Name, "")
		{
			Workspace = workspace;
		}
	}
}

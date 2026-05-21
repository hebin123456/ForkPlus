using System.Windows;
using System.Windows.Media;

namespace ForkPlus.UI.QuickLaunch
{
	public class WorkspaceItem : CommandProviderItem
	{
		public override ImageSource Icon => Application.Current.TryFindResource("WorkspaceIcon") as ImageSource;

		public override ImageSource SelectedIcon => Application.Current.TryFindResource("WorkspaceIcon") as ImageSource;

		public Workspace Workspace { get; }

		public WorkspaceItem(Workspace workspace)
			: base(workspace, workspace.Name, "")
		{
			Workspace = workspace;
		}
	}
}

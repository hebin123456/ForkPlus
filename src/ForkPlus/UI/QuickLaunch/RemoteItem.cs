using System.Windows.Media;
using ForkPlus.Git;

namespace ForkPlus.UI.QuickLaunch
{
	public class RemoteItem : CommandProviderItem
	{
		public override ImageSource Icon => Remote.GetIconImage();

		public override ImageSource SelectedIcon => Remote.GetIconImage();

		public Remote Remote { get; }

		public RemoteItem(Remote remote)
			: base(remote, remote.Name, "")
		{
			Remote = remote;
		}
	}
}

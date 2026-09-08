using Avalonia.Media;
using ForkPlus.Git;

namespace ForkPlus.UI.QuickLaunch
{
	public class RemoteItem : CommandProviderItem
	{
		public override global::Avalonia.Media.IImage Icon => Remote.Icon;

		public override global::Avalonia.Media.IImage SelectedIcon => Remote.Icon;

		public Remote Remote { get; }

		public RemoteItem(Remote remote)
			: base(remote, remote.Name, "")
		{
			Remote = remote;
		}
	}
}

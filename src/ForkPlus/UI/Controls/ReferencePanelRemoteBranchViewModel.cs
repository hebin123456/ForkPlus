using Avalonia.Media;
using ForkPlus.Git;

namespace ForkPlus.UI.Controls
{
	public class ReferencePanelRemoteBranchViewModel : ReferencePanelReferenceViewModel
	{
		private RemoteBranch _remoteBranch;

		public override string Name => _remoteBranch.Name;

		public global::Avalonia.Media.IImage RemoteIcon { get; }

		public ReferencePanelRemoteBranchViewModel(RemoteBranch remoteBranch, global::Avalonia.Media.IImage remoteIcon)
		{
			_remoteBranch = remoteBranch;
			RemoteIcon = remoteIcon;
		}
	}
}

using System.Windows.Media;
using ForkPlus.Git;

namespace ForkPlus.UI.Controls
{
	public class ReferencePanelRemoteBranchViewModel : ReferencePanelReferenceViewModel
	{
		private RemoteBranch _remoteBranch;

		public override string Name => _remoteBranch.Name;

		public ImageSource RemoteIcon { get; }

		public ReferencePanelRemoteBranchViewModel(RemoteBranch remoteBranch, ImageSource remoteIcon)
		{
			_remoteBranch = remoteBranch;
			RemoteIcon = remoteIcon;
		}
	}
}

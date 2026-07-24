using Avalonia.Media;
using ForkPlus.Git;

namespace ForkPlus.UI.Controls
{
	// 阶段 4.5：WPF System.Windows.Media.ImageSource → Avalonia.Media.IImage。
	public class ReferencePanelRemoteBranchViewModel : ReferencePanelReferenceViewModel
	{
		private RemoteBranch _remoteBranch;

		public override string Name => _remoteBranch.Name;

		public IImage RemoteIcon { get; }

		public ReferencePanelRemoteBranchViewModel(RemoteBranch remoteBranch, IImage remoteIcon)
		{
			_remoteBranch = remoteBranch;
			RemoteIcon = remoteIcon;
		}
	}
}

using Avalonia.Media;
using ForkPlus.Git;

namespace ForkPlus.UI
{
	public class RemoteBranchViewModel : BranchViewModel
	{
		public override Reference Reference => RemoteBranch;

		public string Name => RemoteBranch.Name;

		public RemoteBranch RemoteBranch { get; }

		public global::Avalonia.Media.IImage RemoteIcon { get; }

		public bool HasDownstream { get; set; }

		public RemoteBranchViewModel(int graphColumn, RemoteBranch remoteBranch, global::Avalonia.Media.IImage remoteIcon)
			: base(graphColumn)
		{
			RemoteBranch = remoteBranch;
			RemoteIcon = remoteIcon;
		}
	}
}

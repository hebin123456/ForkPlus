// 阶段 4.5：WPF→Avalonia 迁移。
// - using System.Windows.Media → using Avalonia.Media
// - ImageSource → IImage
using Avalonia.Media;
using ForkPlus.Git;

namespace ForkPlus.UI
{
	public class RemoteBranchViewModel : BranchViewModel
	{
		public override Reference Reference => RemoteBranch;

		public string Name => RemoteBranch.Name;

		public RemoteBranch RemoteBranch { get; }

		public IImage RemoteIcon { get; }

		public bool HasDownstream { get; set; }

		public RemoteBranchViewModel(int graphColumn, RemoteBranch remoteBranch, IImage remoteIcon)
			: base(graphColumn)
		{
			RemoteBranch = remoteBranch;
			RemoteIcon = remoteIcon;
		}
	}
}

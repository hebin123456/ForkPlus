using ForkPlus.Git;

namespace ForkPlus.UI.Controls
{
	public class ReferencePanelLocalBranchViewModel : ReferencePanelReferenceViewModel
	{
		private LocalBranch _localBranch;

		public override string Name => _localBranch.Name;

		public ReferencePanelLocalBranchViewModel(LocalBranch localBranch)
		{
			_localBranch = localBranch;
		}
	}
}

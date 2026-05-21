using ForkPlus.Git;

namespace ForkPlus.UI
{
	public class LocalBranchViewModel : BranchViewModel
	{
		private readonly LocalBranch _localBranch;

		private readonly bool _isWorktree;

		public override Reference Reference => _localBranch;

		public string Name => _localBranch.Name;

		public bool IsActive => _localBranch.IsActive;

		public bool IsWorktree => _isWorktree;

		public LocalBranchViewModel(int graphColumn, LocalBranch localBranch, bool isWorktree)
			: base(graphColumn)
		{
			_localBranch = localBranch;
			_isWorktree = isWorktree;
		}
	}
}

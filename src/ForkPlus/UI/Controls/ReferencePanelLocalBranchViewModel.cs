using ForkPlus.Git;

namespace ForkPlus.UI.Controls
{
	public class ReferencePanelLocalBranchViewModel : ReferencePanelReferenceViewModel
	{
		private LocalBranch _localBranch;

		public override string Name => _localBranch.Name;

		// Migration note：WPF 原版 DataTrigger 按 IsActive 加粗当前分支文字，
		// DataTrigger 丢失后模板改绑定，补暴露此属性。
		public bool IsActive => _localBranch.IsActive;

		public ReferencePanelLocalBranchViewModel(LocalBranch localBranch)
		{
			_localBranch = localBranch;
		}
	}
}

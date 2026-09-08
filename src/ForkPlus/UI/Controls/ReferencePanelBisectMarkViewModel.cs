using ForkPlus.Git;

namespace ForkPlus.UI.Controls
{
	public class ReferencePanelBisectMarkViewModel : ReferencePanelReferenceViewModel
	{
		private readonly BisectMark _bisectMark;

		public override string Name => "bisect: " + _bisectMark.ShortName;

		// Migration note：WPF 原版 DataTrigger 按 IsGood 切换 Good/Bad 图标与配色，
		// DataTrigger 丢失后模板改双分支绑定，补暴露此属性。
		public bool IsGood => _bisectMark.IsGood;

		public ReferencePanelBisectMarkViewModel(BisectMark bisectMark)
		{
			_bisectMark = bisectMark;
		}
	}
}

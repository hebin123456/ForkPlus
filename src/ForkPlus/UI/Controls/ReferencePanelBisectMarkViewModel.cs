using ForkPlus.Git;

namespace ForkPlus.UI.Controls
{
	public class ReferencePanelBisectMarkViewModel : ReferencePanelReferenceViewModel
	{
		private readonly BisectMark _bisectMark;

		public override string Name => "bisect: " + _bisectMark.ShortName;

		public ReferencePanelBisectMarkViewModel(BisectMark bisectMark)
		{
			_bisectMark = bisectMark;
		}
	}
}

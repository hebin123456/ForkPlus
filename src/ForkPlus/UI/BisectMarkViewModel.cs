using ForkPlus.Git;

namespace ForkPlus.UI
{
	public class BisectMarkViewModel : BranchViewModel
	{
		private readonly BisectMark _bisectMark;

		public override Reference Reference => _bisectMark;

		public string Name => "bisect: " + _bisectMark.ShortName;

		public bool IsGood => _bisectMark.IsGood;

		public string Image { get; }

		public BisectMarkViewModel(int graphColumn, BisectMark bisectMark)
			: base(graphColumn)
		{
			_bisectMark = bisectMark;
		}
	}
}

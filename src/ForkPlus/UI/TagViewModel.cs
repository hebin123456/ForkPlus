using ForkPlus.Git;

namespace ForkPlus.UI
{
	public class TagViewModel : ReferenceViewModel
	{
		private Tag _tag;

		public override Reference Reference => _tag;

		public string Name => _tag.Name;

		public TagViewModel(int graphColumn, Tag tag)
			: base(graphColumn)
		{
			_tag = tag;
		}
	}
}

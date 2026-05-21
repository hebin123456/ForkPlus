using ForkPlus.Git;

namespace ForkPlus.UI.Controls
{
	public class ReferencePanelTagViewModel : ReferencePanelReferenceViewModel
	{
		private Tag _tag;

		public override string Name => _tag.Name;

		public ReferencePanelTagViewModel(Tag tag)
		{
			_tag = tag;
		}
	}
}

namespace ForkPlus.UI
{
	public interface IRoundedSelectionListBoxViewModel
	{
		int Row { get; }

		ListBoxSelectionType SelectionType { get; set; }
	}
}

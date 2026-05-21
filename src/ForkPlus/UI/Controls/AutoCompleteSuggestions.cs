namespace ForkPlus.UI.Controls
{
	public class AutoCompleteSuggestions
	{
		public int DropdownPosition { get; }

		public AutoCompleteSuggestion[] Suggestions { get; }

		public AutoCompleteSuggestions(int dropdownPosition, AutoCompleteSuggestion[] suggestions)
		{
			DropdownPosition = dropdownPosition;
			Suggestions = suggestions;
		}
	}
}

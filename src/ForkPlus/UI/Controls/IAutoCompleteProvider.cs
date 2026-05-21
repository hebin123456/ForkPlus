namespace ForkPlus.UI.Controls
{
	public interface IAutoCompleteProvider
	{
		[Null]
		AutoCompleteSuggestions GetSuggestions(string text, int caretIndex);
	}
}

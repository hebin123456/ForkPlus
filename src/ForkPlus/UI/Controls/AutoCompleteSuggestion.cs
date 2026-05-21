namespace ForkPlus.UI.Controls
{
	public class AutoCompleteSuggestion
	{
		public Range Range { get; }

		public string Suggestion { get; }

		public AutoCompleteSuggestion(Range range, string suggestion)
		{
			Range = range;
			Suggestion = suggestion;
		}
	}
}

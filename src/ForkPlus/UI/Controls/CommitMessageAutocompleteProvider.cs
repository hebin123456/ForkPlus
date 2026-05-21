using System;
using System.Collections.Generic;
using ForkPlus.Git;

namespace ForkPlus.UI.Controls
{
	public class CommitMessageAutocompleteProvider : IAutoCompleteProvider
	{
		private Dictionary<string, UserIdentity> _userIdentities = new Dictionary<string, UserIdentity>();

		public void UpdateUserIdentities([Null] Dictionary<string, UserIdentity> userIdentities)
		{
			_userIdentities = userIdentities ?? new Dictionary<string, UserIdentity>();
		}

		[Null]
		public AutoCompleteSuggestions GetSuggestions(string text, int caretIndex)
		{
			if (text.Length == 0 || caretIndex == 0)
			{
				return null;
			}
			List<AutoCompleteSuggestion> list = new List<AutoCompleteSuggestion>(8);
			Range currentTokenRange = GetCurrentTokenRange(text, caretIndex - 1, '\n');
			int start = currentTokenRange.Start;
			if (currentTokenRange.Length > 1)
			{
				string text2 = text.Substring(currentTokenRange);
				if ("Co-authored-by:".StartsWith(text2, StringComparison.OrdinalIgnoreCase))
				{
					list.Add(new AutoCompleteSuggestion(currentTokenRange, "Co-authored-by: "));
				}
				else if (text2.StartsWith("Co-authored-by: "))
				{
					Range range = new Range(currentTokenRange.Start + "Co-authored-by: ".Length, caretIndex);
					start = range.Start;
					string text3 = text.Substring(range);
					foreach (UserIdentity value in _userIdentities.Values)
					{
						if (text3.Length == 0 || value.Name.IndexOf(text3, StringComparison.OrdinalIgnoreCase) >= 0)
						{
							list.Add(new UserIdentityAutoCompleteSuggestion(range, value));
						}
					}
				}
				list.Sort((AutoCompleteSuggestion x, AutoCompleteSuggestion y) => x.Suggestion.CompareTo(y.Suggestion));
			}
			return new AutoCompleteSuggestions(start, list.ToArray());
		}

		private static Range GetCurrentTokenRange(string text, int cursor, char terminator)
		{
			int num = 1;
			return new Range(text.LastIndexOf(terminator, cursor) + num, cursor + 1);
		}
	}
}

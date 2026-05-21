using System;
using System.Collections.Generic;
using ForkPlus.Git;

namespace ForkPlus.UI.Controls
{
	public class ReferenceNameAutocompleteProvider : IAutoCompleteProvider
	{
		private readonly Reference[] _references;

		private readonly string[] _autocompleteSource;

		public ReferenceNameAutocompleteProvider(Reference[] references)
		{
			_references = references;
			List<string> list = new List<string>(8);
			for (int i = 0; i < _references.Length; i++)
			{
				string folderName = GetFolderName(_references[i]);
				if (folderName != null && !list.Contains(folderName))
				{
					list.Add(folderName);
				}
			}
			_autocompleteSource = list.ToArray();
		}

		[Null]
		public AutoCompleteSuggestions GetSuggestions(string text, int caretIndex)
		{
			if (text.Length == 0)
			{
				return null;
			}
			int num = text.LastIndexOf('/');
			if (num == -1)
			{
				num = 0;
			}
			List<AutoCompleteSuggestion> list = new List<AutoCompleteSuggestion>(8);
			string[] array = text.Split('/');
			int num2 = array.Length - 1;
			string searchText = text.ToLower();
			foreach (string item in _autocompleteSource.Filter((string x) => x.ToLower().StartsWith(searchText)))
			{
				string[] array2 = item.Split(new char[1] { '/' }, StringSplitOptions.RemoveEmptyEntries);
				string suggestion = array2[num2] + "/";
				if (!list.ContainsItem((AutoCompleteSuggestion x) => x.Suggestion == suggestion))
				{
					int start = ((array.Length > 1) ? (num + 1) : num);
					list.Add(new AutoCompleteSuggestion(new Range(start, text.Length), suggestion));
				}
			}
			return new AutoCompleteSuggestions(num, list.ToArray());
		}

		[Null]
		private static string GetFolderName(Reference reference)
		{
			string text;
			if (reference is LocalBranch localBranch)
			{
				text = localBranch.Name;
			}
			else if (reference is RemoteBranch remoteBranch)
			{
				text = remoteBranch.ShortName;
			}
			else
			{
				if (!(reference is Tag tag))
				{
					return null;
				}
				text = tag.Name;
			}
			for (int num = text.Length - 1; num >= 0; num--)
			{
				if (text[num] == '/')
				{
					return text.Substring(0, num);
				}
			}
			return null;
		}
	}
}

using System;
using System.Collections.Generic;
using ForkPlus.Git;
using ForkPlus.Git.Commands;
using ForkPlus.UI.Commands;

namespace ForkPlus.UI.QuickLaunch
{
	public class RepositoryFileCommandProvider : ICommandProvider
	{
		private readonly RepositoryFileItem[] _allItems;

		public ArgumentType Type => ArgumentType.RepositoryFile;

		public CommandProviderItem[] Items { get; private set; }

		public CommandProviderItem SelectedItem { get; set; }

		public RepositoryFileCommandProvider(GitModule gitModule)
		{
			_allItems = CreateViewModels(GetAllRepositoryFiles(gitModule));
			Items = GetFilteredItems("");
		}

		public void Refresh(string filterString)
		{
			Items = GetFilteredItems(filterString);
		}

		private CommandProviderItem[] GetFilteredItems(string filterString)
		{
			IReadOnlyList<RepositoryFileItem> readOnlyList = _allItems.FuzzyFilter(filterString, (RepositoryFileItem x) => x.Title, (RepositoryFileItem x) => x.FilePath);
			CommandProviderItem[] array = new CommandProviderItem[readOnlyList.Count + 1];
			array[0] = new HeaderCommandProviderItem("Repository Files");
			for (int i = 1; i < array.Length; i++)
			{
				readOnlyList[i - 1].FuzzySearchString = filterString;
				array[i] = readOnlyList[i - 1];
			}
			return array;
		}

		private RepositoryFileItem[] CreateViewModels(string[] rawItems)
		{
			RepositoryFileItem[] array = rawItems.Map((string file) => new RepositoryFileItem(file));
			Array.Sort(array, (RepositoryFileItem x, RepositoryFileItem y) => NaturalStringComparer.Instance.Compare(x.Title, y.Title));
			return array;
		}

		private string[] GetAllRepositoryFiles(GitModule gitModule)
		{
			GitCommandResult<string[]> gitCommandResult = new GetAllRepositoryFilesGitCommand().Execute(gitModule);
			if (gitCommandResult.Succeeded)
			{
				return gitCommandResult.Result;
			}
			return new string[0];
		}
	}
}

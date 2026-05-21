using ForkPlus.Git;
using ForkPlus.UI.Commands;

namespace ForkPlus.UI.QuickLaunch
{
	public class RemoteCommandProvider : ICommandProvider
	{
		private readonly Remote[] _allRemotes;

		public ArgumentType Type => ArgumentType.Remote;

		public CommandProviderItem[] Items { get; private set; }

		public RemoteCommandProvider(RepositoryData repositoryData)
		{
			_allRemotes = repositoryData.Remotes.Items.ToSortedArray(Remote.ComparerIgnoreCaseNumeric);
		}

		public void Refresh(string filterString)
		{
			CommandProviderItem[] filteredRemotes = GetFilteredRemotes(filterString);
			Items = filteredRemotes;
		}

		private RemoteItem[] GetFilteredRemotes(string filterString)
		{
			return _allRemotes.FuzzyFilter(filterString, (Remote x) => x.Name).Map((Remote x) => new RemoteItem(x)
			{
				FuzzySearchString = filterString
			});
		}
	}
}

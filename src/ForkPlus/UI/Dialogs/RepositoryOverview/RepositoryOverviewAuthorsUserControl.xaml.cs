using System;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Markup;
using ForkPlus.Git;

namespace ForkPlus.UI.Dialogs.RepositoryOverview
{
	public partial class RepositoryOverviewAuthorsUserControl : UserControl
	{

		public RepositoryOverviewAuthorsUserControl()
		{
			InitializeComponent();
		}

		public void UpdateData((int, int, DateTime)[] authorStats, UserIdentity[] userIdentities)
		{
			int allCommitsCount = 0;
			for (int i = 0; i < authorStats.Length; i++)
			{
				(int, int, DateTime) tuple = authorStats[i];
				allCommitsCount += tuple.Item2;
			}
			RepositoryOverviewAuthorViewModel[] itemsSource = authorStats.Map(((int, int, DateTime) x) => new RepositoryOverviewAuthorViewModel(userIdentities[x.Item1], x.Item2, x.Item3, allCommitsCount)).ToSortedArray((RepositoryOverviewAuthorViewModel x, RepositoryOverviewAuthorViewModel y) => y.CommitsCount.CompareTo(x.CommitsCount));
			AuthorsListBox.ItemsSource = itemsSource;
		}

	}
}

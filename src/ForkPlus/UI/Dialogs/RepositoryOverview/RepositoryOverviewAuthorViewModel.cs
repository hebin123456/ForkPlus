using System;
using System.ComponentModel;
using ForkPlus.Git;

namespace ForkPlus.UI.Dialogs.RepositoryOverview
{
	public class RepositoryOverviewAuthorViewModel : INotifyPropertyChanged
	{
		public UserIdentity Author { get; }

		public int CommitsCount { get; }

		public DateTime LastCommit { get; }

		public int AllCommitsCount { get; }

		public string AuthorName => Author.Name;

		public string FullCredentialsString => Author.Name + " <" + Author.Email + ">";

		public double Progress { get; set; }

		public event PropertyChangedEventHandler PropertyChanged
		{
			add
			{
			}
			remove
			{
			}
		}

		public RepositoryOverviewAuthorViewModel(UserIdentity author, int commitsCount, DateTime lastCommit, int allCommitsCount)
		{
			Author = author;
			CommitsCount = commitsCount;
			AllCommitsCount = allCommitsCount;
			LastCommit = lastCommit;
			Progress = commitsCount;
		}
	}
}

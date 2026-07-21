using System;
using System.ComponentModel;
using System.IO;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Markup;
using System.Windows.Media;
using System.Windows.Navigation;
using ForkPlus.Git;
using ForkPlus.Git.Commands;
using ForkPlus.UI.Dialogs;
using ForkPlus.Settings;
using ForkPlus.UI.UserControls.Preferences;

namespace ForkPlus.UI.UserControls
{
	public partial class RepositoryDetailsUserControl : UserControl, ForkPlus.UI.ILocalizableControl
	{
		public class RemoteViewModel : INotifyPropertyChanged
		{
			private readonly Remote _remote;

			public ImageSource RemoteIcon => _remote.GetIconImage();

			public string Name => _remote.Name;

			public string WebsiteName { get; }

			public string WebsiteUrl { get; }

			public string IssuesUrl { get; }

			public string PullRequestsUrl { get; }

			public Visibility IsWebsiteVisible { get; }

			public Visibility IsIssuesVisible { get; }

			public Visibility IsPullRequestsVisible { get; }

			public event PropertyChangedEventHandler PropertyChanged
			{
				add
				{
				}
				remove
				{
				}
			}

			public RemoteViewModel(Remote remote)
			{
				_remote = remote;
				RepositoryUrlBuilder repositoryUrlBuilder = new RepositoryUrlBuilder(remote);
				WebsiteName = remote.RemoteType.FriendlyName();
				WebsiteUrl = repositoryUrlBuilder.RepositoryWebpageUrl ?? string.Empty;
				IsWebsiteVisible = (string.IsNullOrEmpty(WebsiteName) ? Visibility.Hidden : Visibility.Visible);
				IssuesUrl = repositoryUrlBuilder.IssuesUrl ?? string.Empty;
				IsIssuesVisible = (string.IsNullOrEmpty(IssuesUrl) ? Visibility.Hidden : Visibility.Visible);
				PullRequestsUrl = repositoryUrlBuilder.PullRequestsUrl ?? string.Empty;
				IsPullRequestsVisible = (string.IsNullOrEmpty(PullRequestsUrl) ? Visibility.Hidden : Visibility.Visible);
			}
		}

		public class ReferenceViewModel : INotifyPropertyChanged
		{
			private readonly GetRecentReferencesGitCommand.RecentReference _reference;

			public string Name => _reference.Reference.Name;

			public UserIdentity Committer => _reference.Committer;

			public string CommitterName => _reference.Committer.Name;

			public string RevisionSubject => _reference.RevisionSubject;

			public string RelativeDate { get; }

			public event PropertyChangedEventHandler PropertyChanged
			{
				add
				{
				}
				remove
				{
				}
			}

			public ReferenceViewModel(GetRecentReferencesGitCommand.RecentReference reference)
			{
				_reference = reference;
				RelativeDate = DateTimeHelper.ToRelativeString(_reference.CommitterDate);
			}
		}

		public class RevisionViewModel : INotifyPropertyChanged
		{
			private readonly GetRecentRevisionsGitCommand.RecentRevision _revision;

			public UserIdentity Author => _revision.Author;

			public string AuthorName => _revision.Author.Name;

			public string RevisionSubject => _revision.Subject;

			public string RelativeDate { get; }

			public event PropertyChangedEventHandler PropertyChanged
			{
				add
				{
				}
				remove
				{
				}
			}

			public RevisionViewModel(GetRecentRevisionsGitCommand.RecentRevision revision)
			{
				_revision = revision;
				RelativeDate = DateTimeHelper.ToRelativeString(_revision.AuthorDate);
			}
		}

		private readonly DelayedAction<RepositoryManager.Repository?> _updatePreviewAction;

		private RepositoryManager.Repository? _selectedRepository;

		public RepositoryManagerUserControl RepositoryManagerUserControl { get; set; }

		public RepositoryDetailsUserControl()
		{
			InitializeComponent();
			_updatePreviewAction = new DelayedAction<RepositoryManager.Repository?>(UpdatePreview);
			ApplyLocalization();
		}

		public void ApplyLocalization()
		{
			PreferencesLocalization.Apply(this, ForkPlusSettings.Default.UiLanguage);
			StatisticsUserControl.ApplyLocalization();
		}

		public void ShowDetails(RepositoryManager.Repository? repository)
		{
			_selectedRepository = repository;
			_updatePreviewAction.InvokeWithDelay(repository);
		}

		public void RefreshRepositoryName()
		{
			RepositoryManager.Repository? selectedRepository = _selectedRepository;
			if (selectedRepository.HasValue)
			{
				RepositoryManager.Repository valueOrDefault = selectedRepository.GetValueOrDefault();
				RepositoryName.Text = GitMmUserControl.IsGitMmWorkspace(valueOrDefault.Path) ? "git mm: " + valueOrDefault.Name() : valueOrDefault.Name();
				RepositoryPath.Text = valueOrDefault.Path;
			}
		}

		private async void UpdatePreview(RepositoryManager.Repository? repo)
		{
			try
			{
				UnsafeRepositoryFallbackUserControl.Hide();
				if (repo.HasValue)
				{
					RepositoryManager.Repository repository = repo.GetValueOrDefault();
					DirectoryFallbackUserControl.Hide();
					if (_selectedRepository?.Path != repository.Path)
					{
						return;
					}
					if (GitMmUserControl.IsGitMmWorkspace(repository.Path))
					{
						await ShowGitMmDetails(repository);
						return;
					}
					GitCommandResult<GitModule> gitCommandResult = await Task.Run(() => new OpenGitRepositoryGitCommand().Execute(repository.Path));
					if (_selectedRepository?.Path != repository.Path)
					{
						return;
					}
					if (!gitCommandResult.Succeeded)
					{
						if (gitCommandResult.Error is GitCommandError.UnsafeRepository)
						{
							UnsafeRepositoryFallbackUserControl.Tag = gitCommandResult.Error;
							UnsafeRepositoryFallbackUserControl.FallbackTitle = string.Format(Translate("{0} is located in directory owned by someone else"), repository.Name());
							UnsafeRepositoryFallbackUserControl.FallbackMessage = string.Format(Translate("Is '{0}' location safe?"), repository.Path);
							UnsafeRepositoryFallbackUserControl.Button1Title = string.Format(Translate("Mark {0} as safe"), repository.Name());
							UnsafeRepositoryFallbackUserControl.Show();
						}
						else
						{
							FallbackUserControl.Show();
						}
						return;
					}
					FallbackUserControl.Hide();
					GitModule gitModule = gitCommandResult.Result;
					RepositoryName.Text = repository.Name();
					RepositoryPath.Text = repository.Path;
					GitCommandResult<int> changedFilesCountResult = await Task.Run(() => new GetChangedFilesCountGitCommand().Execute(gitModule));
					if (_selectedRepository?.Path != repository.Path)
					{
						return;
					}
					GitCommandResult<int> revisionsCountResult = await Task.Run(() => new GetRevisionCountGitCommand().Execute(gitModule));
					if (_selectedRepository?.Path != repository.Path)
					{
						return;
					}
					DateTime? initialRevisionDateResult = await Task.Run(() => new GetInitialRevisionDateGitCommand().Execute(gitModule));
					if (_selectedRepository?.Path != repository.Path)
					{
						return;
					}
					DateTime? dateTime = await Task.Run(() => new GetLastRevisionDateGitCommand().Execute(gitModule));
					if (_selectedRepository?.Path != repository.Path)
					{
						return;
					}
					if (changedFilesCountResult.Succeeded)
					{
						ChangedFilesTextBlock.Text = changedFilesCountResult.Result.ToString();
					}
					if (revisionsCountResult.Succeeded)
					{
						CommitsTextBlock.Text = revisionsCountResult.Result.ToString();
					}
					InitialCommitDateTextBlock.Text = ((!initialRevisionDateResult.HasValue) ? "" : DateTimeHelper.ToRelativeString(initialRevisionDateResult.Value));
					LastCommitDateTextBlock.Text = ((!dateTime.HasValue) ? "" : DateTimeHelper.ToRelativeString(dateTime.Value));
					GitCommandResult<RepositoryRemotes> gitCommandResult2 = await Task.Run(() => new GetRemotesGitCommand().Execute(gitModule));
					if (_selectedRepository?.Path != repository.Path)
					{
						return;
					}
					if (gitCommandResult2.Succeeded)
					{
						RemotesItemsControl.ItemsSource = gitCommandResult2.Result.Items.Map((Remote x) => new RemoteViewModel(x));
					}
					if (StatisticsTabItem.IsSelected)
					{
						StatisticsUserControl.ShowStatistics(gitModule);
						return;
					}
					GitCommandResult<GetRecentReferencesGitCommand.RecentReferences> gitCommandResult3 = await Task.Run(() => new GetRecentReferencesGitCommand().Execute(gitModule));
					if (_selectedRepository?.Path != repository.Path)
					{
						return;
					}
					if (gitCommandResult3.Succeeded)
					{
						BranchesItemsControl.ItemsSource = gitCommandResult3.Result.RemoteBranches.Map((GetRecentReferencesGitCommand.RecentReference x) => new ReferenceViewModel(x));
						TagsItemsControl.ItemsSource = gitCommandResult3.Result.Tags.Map((GetRecentReferencesGitCommand.RecentReference x) => new ReferenceViewModel(x));
					}
					GitCommandResult<GetRecentRevisionsGitCommand.RecentRevision[]> gitCommandResult4 = await Task.Run(() => new GetRecentRevisionsGitCommand().Execute(gitModule));
					if (_selectedRepository?.Path != repository.Path)
					{
						return;
					}
					if (gitCommandResult4.Succeeded)
					{
						CommitsItemsControl.ItemsSource = gitCommandResult4.Result.Map((GetRecentRevisionsGitCommand.RecentRevision x) => new RevisionViewModel(x));
					}
					try
					{
						if (File.Exists(gitModule.MakePath("readme.md")))
						{
							ReadmeContainer.Show();
							ReadmeTextBox.Text = File.ReadAllText(gitModule.MakePath("readme.md"));
						}
						else
						{
							ReadmeContainer.Hide();
							ReadmeTextBox.Text = "";
						}
						return;
					}
					catch
					{
						ReadmeContainer.Hide();
						ReadmeTextBox.Text = "";
						return;
					}
				}
				DirectoryFallbackUserControl.Show();
			}
			catch (Exception ex)
			{
				Log.Error("UpdatePreview failed", ex);
			}
		}

		private async Task ShowGitMmDetails(RepositoryManager.Repository repository)
		{
			FallbackUserControl.Hide();
			RepositoryName.Text = PreferencesLocalization.FormatCurrent("git mm: {0}", repository.Name());
			RepositoryPath.Text = repository.Path;
			ChangedFilesTextBlock.Text = "";
			CommitsTextBlock.Text = "";
			InitialCommitDateTextBlock.Text = "";
			LastCommitDateTextBlock.Text = "";
			RemotesItemsControl.ItemsSource = null;
			BranchesItemsControl.ItemsSource = null;
			TagsItemsControl.ItemsSource = null;
			CommitsItemsControl.ItemsSource = null;
			ReadmeContainer.Hide();
			ReadmeTextBox.Text = "";

			int subrepoCount = await Task.Run(() => GitMmUserControl.CountSubrepos(repository.Path));
			if (_selectedRepository?.Path == repository.Path)
			{
				CommitsTextBlock.Text = PreferencesLocalization.FormatCurrent("{0} sub repositories", subrepoCount);
			}
		}

		private void OpenRepositoryButton_Click(object sender, RoutedEventArgs e)
		{
			RepositoryManager.Repository? selectedRepository = _selectedRepository;
			if (selectedRepository.HasValue)
			{
				RepositoryManager.Repository valueOrDefault = selectedRepository.GetValueOrDefault();
				Application.Current.TabManager()?.OpenRepository(valueOrDefault.Path);
			}
		}

		private void Hyperlink_RequestNavigate(object sender, RequestNavigateEventArgs e)
		{
			e.Uri.OpenInBrowser();
			e.Handled = true;
		}

		private void FallbackUserControl_Button1Click(object sender, RoutedEventArgs e)
		{
			RepositoryManager.Repository? selectedRepository = _selectedRepository;
			if (selectedRepository.HasValue)
			{
				RepositoryManager.Repository valueOrDefault = selectedRepository.GetValueOrDefault();
				RepositoryManager.Instance.DeleteRepositories(new string[1] { valueOrDefault.Path });
				RepositoryManagerUserControl.Refresh();
				RepositoryManagerUserControl.SelectFirstRepository();
			}
		}

		private void UnsafeRepositoryFallbackUserControl_Button1Click(object sender, RoutedEventArgs e)
		{
			RepositoryManager.Repository? selectedRepository = _selectedRepository;
			if (!selectedRepository.HasValue)
			{
				return;
			}
			RepositoryManager.Repository valueOrDefault = selectedRepository.GetValueOrDefault();
			if (UnsafeRepositoryFallbackUserControl.Tag is GitCommandError.UnsafeRepository unsafeRepository)
			{
				GitCommandResult gitCommandResult = new AddRepositoryToSafeDirectoriesListGitCommand().Execute(unsafeRepository.ProposedRepositoryPath);
				if (!gitCommandResult.Succeeded)
				{
					new ErrorWindow(null, gitCommandResult.Error).ShowDialog();
				}
				RepositoryManagerUserControl.Refresh();
				RepositoryManagerUserControl.SelectRepositoryWithPath(valueOrDefault.Path);
			}
		}

		private void ModernTabControl_SelectionChanged(object sender, SelectionChangedEventArgs e)
		{
			if (e.AddedItems.Count >= 1 && e.AddedItems[0] is TabItem)
			{
				_updatePreviewAction.ReinvokeNow();
			}
		}

		private void OpeneInFileExplorerButton_Click(object sender, RoutedEventArgs e)
		{
			RepositoryManager.Repository? selectedRepository = _selectedRepository;
			if (selectedRepository.HasValue)
			{
				RepositoryManager.Repository valueOrDefault = selectedRepository.GetValueOrDefault();
				MainWindow.Commands.OpenRepositoryInFileExplorer.Execute(valueOrDefault.Path);
			}
		}

		private static string Translate(string text)
		{
			return PreferencesLocalization.Translate(text, ForkPlusSettings.Default.UiLanguage);
		}

	}
}

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Markup;
using ForkPlus.Git;
using ForkPlus.Git.Commands;
using ForkPlus.UI.Controls;
using ForkPlus.UI.UserControls.Preferences;

namespace ForkPlus.UI.UserControls
{
	public partial class TooltipRevisionDetailsUserControl : UserControl
	{
		public EventHandler ShowRevisionInSeparateWindowButtonClicked;

		private readonly RepositoryUserControl _repositoryUserControl;

		private readonly Sha _sha;

		public TooltipRevisionDetailsUserControl(RepositoryUserControl repositoryUserControl, Sha sha)
		{
			InitializeComponent();
			_repositoryUserControl = repositoryUserControl;
			_sha = sha;
			RefreshControls();
		}

		private void RefreshControls()
		{
			RepositoryData repositoryData = _repositoryUserControl.RepositoryData;
			if (repositoryData != null)
			{
				GitModule gitModule = _repositoryUserControl.GitModule;
				if (gitModule != null)
				{
					GitCommandResult<Revision> revisionHeader = GetRevisionHeader(gitModule, _sha);
					if (!revisionHeader.Succeeded)
					{
						DetailsContainer.Collapse();
						FallbackMessageTextBlock.Show();
						FallbackMessageTextBlock.Text = revisionHeader.Error.FriendlyDescription;
						return;
					}
					Revision result = revisionHeader.Result;
					DetailsContainer.Show();
					FallbackMessageTextBlock.Collapse();
					List<ForkPlus.Git.Reference> list = repositoryData.References.Items.Filter((ForkPlus.Git.Reference x) => x.Sha == _sha);
					BugtrackerLinkDefinition[] bugtrackers = repositoryData.Bugtrackers;
					Remote[] items = repositoryData.Remotes.Items;
					AuthorAvatarImage.UserIdentity = result.Author;
					AuthorTextBlock.Text = result.Author.Name;
					AuthorDateTextBlock.Text = result.AuthorDate.ToString(Consts.NormalDateTimeFormat);
					ShaTextBlock.Text = result.Sha.ToAbbreviatedString();
					SubjectTextBlock.Text = result.Message;
					SubjectTextBlock.ApplySearchAndButrackerHighlighting(null, bugtrackers);
					if (list.Count > 0)
					{
						ReferencePanel.Refresh(list, items);
						ReferencePanel.Show();
					}
					else
					{
						ReferencePanel.Refresh(new ForkPlus.Git.Reference[0], new Remote[0]);
						ReferencePanel.Collapse();
					}
					return;
				}
			}
			DetailsContainer.Collapse();
			FallbackMessageTextBlock.Show();
			FallbackMessageTextBlock.Text = PreferencesLocalization.Current("Not available");
		}

		private static GitCommandResult<Revision> GetRevisionHeader(GitModule gitModule, Sha sha)
		{
			GitCommandResult<Revision[]> gitCommandResult = new GetRevisionsGitCommand().Execute(gitModule, new Sha[1] { sha });
			if (!gitCommandResult.Succeeded)
			{
				Log.Error(gitCommandResult.Error.FriendlyDescription);
				return GitCommandResult<Revision>.Failure(gitCommandResult.Error);
			}
			Revision revision = gitCommandResult.Result.FirstItem();
			if (revision == null)
			{
				return GitCommandResult<Revision>.Failure(new GitCommandError.NotFound());
			}
			return GitCommandResult<Revision>.Success(revision);
		}

		private void ShowRevisionInSeparateWindowButton_Click(object sender, RoutedEventArgs e)
		{
			ShowRevisionInSeparateWindowButtonClicked?.Invoke(this, EventArgs.Empty);
			RepositoryUserControl.Commands.ShowRevisionInSeparateWindow.Execute(_repositoryUserControl.GitModule, new RevisionDiffTarget.Revision(_sha));
		}

	}
}

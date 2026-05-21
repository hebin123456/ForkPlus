using System;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Markup;
using ForkPlus.Git;
using ForkPlus.Git.Commands;
using ForkPlus.Jobs;
using ForkPlus.UI.Controls;
using ForkPlus.UI.UserControls;
using ForkPlus.UI.UserControls.Preferences;

namespace ForkPlus.UI.Dialogs
{
	public partial class RemoveTagWindow : ForkPlusDialogWindow
	{
		private readonly RepositoryUserControl _repositoryUserControl;

		private readonly RepositoryReferences _references;

		private readonly Tag[] _tags;

		private readonly RepositoryRemotes _remotes;

		public RemoveTagWindow(RepositoryUserControl repositoryUserControl, Tag[] tags, RepositoryReferences references)
		{
			InitializeComponent();
			_repositoryUserControl = repositoryUserControl;
			_tags = tags;
			_remotes = repositoryUserControl.RepositoryData.Remotes;
			_references = references;
			if (_tags.Length == 1)
			{
				GitPointsContainer.Collapse();
				GitPointView.Show();
				GitPointView.Value = _tags.FirstItem();
				base.DialogTitle = "Delete Tag";
				base.DialogDescription = "Delete tag from your repository";
				StartPointTextBlock.Text = PreferencesLocalization.Current("Tag:");
				DeleteFromRemotesCheckBox.Content = PreferencesLocalization.Current("Delete tag from remote repositories");
				base.SubmitButtonTitle = "Delete";
			}
			else
			{
				GitPointView.Collapse();
				GitPointsContainer.Show();
				GitPoints.ItemsSource = _tags;
				base.DialogTitle = "Delete Tags";
				base.DialogDescription = "Delete tags from your repository";
				StartPointTextBlock.Text = PreferencesLocalization.Current("Tags:");
				DeleteFromRemotesCheckBox.Content = PreferencesLocalization.Current("Delete tags from remote repositories");
				base.SubmitButtonTitle = $"Delete {_tags.Length} tags";
			}
		}

		protected override void OnSubmit()
		{
			GitModule gitModule = _repositoryUserControl.GitModule;
			Tag[] tags = _tags;
			bool valueOrDefault = DeleteFromRemotesCheckBox.IsChecked.GetValueOrDefault();
			Remote[] remotes = (valueOrDefault ? _remotes.Items : new Remote[0]);
			DisableEditableControls();
			string name = ((tags.Length > 1) ? $"Delete {tags.Length} tags" : ("Delete '" + tags[0].Name + "'"));
			_repositoryUserControl.JobQueue.Add(name, delegate(JobMonitor monitor)
			{
				base.Dispatcher.Async(delegate
				{
					SetStatus(ForkPlusDialogStatus.InProgress, "Deleting...");
				});
				GitCommandResult removeTagResult = new RemoveTagGitCommand().Execute(gitModule, tags, remotes, monitor);
				if (!removeTagResult.Succeeded)
				{
					base.Dispatcher.Async(delegate
					{
						Close(removeTagResult);
					});
				}
				else
				{
					gitModule.Settings.PinnedReferences = _references.PinnedReferences.Filter((string p) => !tags.ContainsItem((Tag t) => t.FullReference == p)).ToArray();
					gitModule.Settings.FilterReferences = _references.FilterReferences.Filter((string p) => !tags.ContainsItem((Tag t) => t.FullReference == p)).ToArray();
					gitModule.Settings.Save();
					base.Dispatcher.Async(delegate
					{
						Close(GitCommandResult.Success());
					});
				}
			}, JobFlags.SaveToLog);
		}

	}
}

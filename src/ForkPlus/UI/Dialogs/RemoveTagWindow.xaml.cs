using Avalonia.Interactivity;
using Avalonia.Threading;
using System;
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

		protected override string GetCommandPreview()
		{
			if (_tags == null || _tags.Length == 0)
			{
				return null;
			}
			var parts = new System.Collections.Generic.List<string> { "git", "tag", "-d" };
			foreach (Tag t in _tags)
			{
				parts.Add(t.Name);
			}
			string command = string.Join(" ", parts);
			if (DeleteFromRemotesCheckBox.IsChecked.GetValueOrDefault() && _remotes != null)
			{
				foreach (Remote remote in _remotes.Items)
				{
					var pushParts = new System.Collections.Generic.List<string> { "git", "push", remote.Name, "--delete" };
					foreach (Tag t in _tags)
					{
						pushParts.Add(t.Name);
					}
					command += "\n" + string.Join(" ", pushParts);
				}
			}
			return command;
		}

		private void DeleteFromRemotesCheckBox_Changed(object sender, RoutedEventArgs e)
		{
			RefreshCommandPreview();
		}

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
				base.DialogTitle = PreferencesLocalization.Current("Delete Tag");
				base.DialogDescription = PreferencesLocalization.Current("Delete tag from your repository");
				StartPointTextBlock.Text = PreferencesLocalization.Current("Tag:");
				DeleteFromRemotesCheckBox.Content = PreferencesLocalization.Current("Delete tag from remote repositories");
				base.SubmitButtonTitle = PreferencesLocalization.Current("Delete");
			}
			else
			{
				GitPointView.Collapse();
				GitPointsContainer.Show();
				GitPoints.ItemsSource = _tags;
				base.DialogTitle = PreferencesLocalization.Current("Delete Tags");
				base.DialogDescription = PreferencesLocalization.Current("Delete tags from your repository");
				StartPointTextBlock.Text = PreferencesLocalization.Current("Tags:");
				DeleteFromRemotesCheckBox.Content = PreferencesLocalization.Current("Delete tags from remote repositories");
				base.SubmitButtonTitle = PreferencesLocalization.FormatCurrent("Delete {0} tags", _tags.Length);
			}
		}

		protected override void OnSubmit()
		{
			GitModule gitModule = _repositoryUserControl.GitModule;
			Tag[] tags = _tags;
			bool valueOrDefault = DeleteFromRemotesCheckBox.IsChecked.GetValueOrDefault();
			Remote[] remotes = (valueOrDefault ? _remotes.Items : new Remote[0]);
			DisableEditableControls();
			// v3.4.1：状态栏标题国际化（之前是硬编码英文）
		string name = ((tags.Length > 1)
			? PreferencesLocalization.FormatCurrent("Delete {0} tags", tags.Length)
			: PreferencesLocalization.FormatCurrent("Delete '{0}'", tags[0].Name));
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

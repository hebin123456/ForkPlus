using System;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Markup;
using ForkPlus.Git;
using ForkPlus.Git.Commands;
using ForkPlus.Jobs;
using ForkPlus.Settings;
using ForkPlus.UI.Controls;
using ForkPlus.UI.UserControls;
using ForkPlus.UI.UserControls.Preferences;

namespace ForkPlus.UI.Dialogs
{
	public partial class CreateTagWindow : ForkPlusDialogWindow
	{
		private readonly GitModule _gitModule;

		private readonly Tag[] _tags;

		private readonly Remote[] _remotes;

		private readonly IGitPoint _gitPoint;

		protected override bool IsSubmitAllowed
		{
			get
			{
				SetStatus(ForkPlusDialogStatus.None, string.Empty);
				string tagName = TagNameTextBox.Text.ToLower();
				if (string.IsNullOrEmpty(tagName))
				{
					return false;
				}
				string text = ReferenceNameValidator.Validate(tagName);
				if (text != null)
				{
					SetStatus(ForkPlusDialogStatus.Warning, text);
					return false;
				}
				if (_tags.AnyItem((Tag x) => x.Name.ToLower() == tagName))
				{
					SetStatus(ForkPlusDialogStatus.Warning, "Tag '" + TagNameTextBox.Text + "' already exists");
					return false;
				}
				return true;
			}
		}

		protected override string GetCommandPreview()
		{
			string tagName = TagNameTextBox.Text;
			if (string.IsNullOrWhiteSpace(tagName))
			{
				return null;
			}
			var parts = new System.Collections.Generic.List<string> { "git", "tag", "-a" };
			string message = TagMessageTextBox.Text;
			if (!string.IsNullOrEmpty(message))
			{
				parts.Add("-m");
				parts.Add(message.Contains(" ") ? "\"" + message + "\"" : message);
			}
			parts.Add(tagName);
			string commit = _gitPoint?.FriendlyName;
			if (!string.IsNullOrEmpty(commit))
			{
				parts.Add(commit);
			}
			string command = string.Join(" ", parts);
			if (PushCheckBox.IsChecked.GetValueOrDefault() && _remotes != null)
			{
				foreach (Remote remote in _remotes)
				{
					command += "\ngit push " + remote.Name + " refs/tags/" + tagName;
				}
			}
			return command;
		}

		public CreateTagWindow(GitModule gitModule, RepositoryReferences refs, Remote[] remotes, IGitPoint startPoint)
		{
			InitializeComponent();
			_gitModule = gitModule;
			_tags = refs.Tags;
			_remotes = remotes;
			_gitPoint = startPoint;
			GitPointView.Value = startPoint;
			base.DialogTitle = Translate("Create Tag");
			base.DialogDescription = Translate("Create annotated tag at the selected point");
			ReferenceTextBox tagNameTextBox = TagNameTextBox;
			ForkPlus.Git.Reference[] tags = _tags;
			tagNameTextBox.SetAutocompleteProvider(new ReferenceNameAutocompleteProvider(tags));
			PushCheckBox.Content = Translate((remotes.Length > 1) ? "Push to all remotes" : "Push");
			PushCheckBox.IsChecked = ForkPlusSettings.Default.CreateTag_Push;
			RefreshButtonTitle();
		}

		protected override void OnSubmit()
		{
			RepositoryUserControl activeRepositoryUserControl = MainWindow.ActiveRepositoryUserControl;
			if (activeRepositoryUserControl == null)
			{
				return;
			}
			string tagName = TagNameTextBox.Text;
			string tagMessage = TagMessageTextBox.Text;
			bool push = PushCheckBox.IsChecked.GetValueOrDefault();
			ForkPlusSettings.Default.CreateTag_Push = push;
			ForkPlusSettings.Default.Save();
			DisableEditableControls();
			string name = Translate(push ? ("Create and push tag '" + tagName + "'") : ("Create tag '" + tagName + "'"));
			activeRepositoryUserControl.JobQueue.Add(name, delegate(JobMonitor monitor)
			{
				GitCommandResult result = PerformCreateTag(tagName, tagMessage, push, monitor);
				base.Dispatcher.Async(delegate
				{
					Close(result);
				});
			}, JobFlags.SaveToLog);
		}

		private void TagNameTextBox_TextChanged(object sender, TextChangedEventArgs e)
		{
			UpdateSubmitButton();
			RefreshCommandPreview();
		}

		private void TagMessageTextBox_TextChanged(object sender, TextChangedEventArgs e)
		{
			RefreshCommandPreview();
		}

		private void PushCheckBox_Changed(object sender, RoutedEventArgs e)
		{
			RefreshButtonTitle();
			RefreshCommandPreview();
		}

		private GitCommandResult PerformCreateTag(string tagName, string tagMessage, bool push, JobMonitor monitor)
		{
			base.Dispatcher.Async(delegate
			{
				SetStatus(ForkPlusDialogStatus.InProgress, "Creating '" + tagName + "'...");
			});
			GitCommandResult gitCommandResult = new CreateTagGitCommand().Execute(_gitModule, tagName, tagMessage, _gitPoint, monitor);
			if (!gitCommandResult.Succeeded)
			{
				return gitCommandResult;
			}
			if (!push)
			{
				return gitCommandResult;
			}
			string tagFullReference = "refs/tags/" + tagName;
			Remote[] remotes = _remotes;
			foreach (Remote remote in remotes)
			{
				base.Dispatcher.Async(delegate
				{
					SetStatus(ForkPlusDialogStatus.InProgress, "Pushing '" + tagName + "' to '" + remote.Name + "'...");
				});
				GitCommandResult gitCommandResult2 = new PushTagGitCommand().Execute(_gitModule, remote.Name, tagFullReference, monitor);
				if (!gitCommandResult2.Succeeded)
				{
					return gitCommandResult2;
				}
			}
			return gitCommandResult;
		}

		private void RefreshButtonTitle()
		{
			base.SubmitButtonTitle = Translate(PushCheckBox.IsChecked.GetValueOrDefault() ? "Create and Push" : "Create");
		}

		private static string Translate(string text)
		{
			return PreferencesLocalization.Translate(text, ForkPlusSettings.Default.UiLanguage);
		}
	}
}

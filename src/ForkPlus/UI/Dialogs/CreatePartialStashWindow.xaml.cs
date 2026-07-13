using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Markup;
using ForkPlus.Git;
using ForkPlus.Git.Commands;
using ForkPlus.Jobs;
using ForkPlus.Settings;
using ForkPlus.UI.Controls;
using ForkPlus.UI.UserControls.Preferences;

namespace ForkPlus.UI.Dialogs
{
	public partial class CreatePartialStashWindow : ForkPlusDialogWindow
	{
		private GitModule _gitModule;

		protected override bool IsSubmitAllowed => GetFirstSelectedFile() != null;

		public CreatePartialStashWindow(GitModule gitModule, ChangedFile[] filesToStash, ChangedFile[] allChangedFiles)
		{
			InitializeComponent();
			base.DialogTitle = Translate("Save stash");
			base.DialogDescription = Translate("Save your local modifications to a new stash. BOTH staged and unstaged changes will be stashed");
			base.SubmitButtonTitle = Translate("Save Stash");
			base.ResizeMode = ResizeMode.CanResizeWithGrip;
			StashMessageTextBox.Placeholder = Translate("Stash message (optional)");
			_gitModule = gitModule;
			HashSet<string> hashSet = new HashSet<string>();
			List<PartialStashFileViewModel> list = new List<PartialStashFileViewModel>();
			foreach (ChangedFile changedFile in allChangedFiles)
			{
				string filePath = changedFile.Path;
				if (!hashSet.Contains(filePath))
				{
					hashSet.Add(filePath);
					bool selected = filesToStash.ContainsItem((ChangedFile x) => x.Path == filePath);
					list.Add(new PartialStashFileViewModel(changedFile, filePath, selected));
				}
			}
			list.Sort((PartialStashFileViewModel x, PartialStashFileViewModel y) => NaturalStringComparer.Instance.Compare(x.FilePath, y.FilePath));
			PartialStashListBox.ItemsSource = list;
			PartialStashFileViewModel firstSelectedFile = GetFirstSelectedFile();
			if (firstSelectedFile != null)
			{
				PartialStashListBox.ScrollIntoView(firstSelectedFile);
			}
			UpdateSubmitButton();
			base.Dispatcher.Async(delegate
			{
				StashMessageTextBox.Focus();
			});
		RefreshCommandPreview();
	}

		private void FileCheckBox_Changed(object sender, RoutedEventArgs e)
	{
		UpdateSubmitButton();
		RefreshCommandPreview();
	}

	private void StashMessage_TextChanged(object sender, TextChangedEventArgs e)
	{
		RefreshCommandPreview();
	}

		protected override string GetCommandPreview()
	{
		List<string> files = new List<string>();
		foreach (PartialStashFileViewModel item in (IEnumerable)PartialStashListBox.Items)
		{
			if (item.Selected)
			{
				files.Add(item.FilePath);
			}
		}
		if (files.Count == 0)
		{
			return null;
		}
		List<string> parts = new List<string> { "git", "stash", "push" };
		string stashMessage = StashMessageTextBox.Text;
		if (!string.IsNullOrWhiteSpace(stashMessage))
		{
			string quoted = stashMessage.IndexOf(' ') >= 0 ? ("\"" + stashMessage + "\"") : stashMessage;
			parts.Add("-m");
			parts.Add(quoted);
		}
		parts.Add("--");
		foreach (string f in files)
		{
			parts.Add(f.IndexOf(' ') >= 0 ? ("\"" + f + "\"") : f);
		}
		return string.Join(" ", parts);
	}

	protected override void OnSubmit()
	{
		List<ChangedFile> filesToStash = new List<ChangedFile>();
			foreach (PartialStashFileViewModel item in (IEnumerable)PartialStashListBox.Items)
			{
				if (item.Selected)
				{
					filesToStash.Add(item.ChangedFile);
				}
			}
			string stashMessage = (string.IsNullOrWhiteSpace(StashMessageTextBox.Text) ? null : StashMessageTextBox.Text);
			DisableEditableControls();
			SetStatus(ForkPlusDialogStatus.InProgress, "Stashing...");
			MainWindow.ActiveRepositoryUserControl.JobQueue.Add(Translate("Partial stash"), delegate(JobMonitor monitor)
			{
				GitCommandResult result = new SaveStashGitCommand().Execute(_gitModule, stashMessage, filesToStash.ToArray(), monitor);
				base.Dispatcher.Async(delegate
				{
					Close(result);
				});
			}, JobFlags.SaveToLog);
		}

		private PartialStashFileViewModel GetFirstSelectedFile()
		{
			foreach (PartialStashFileViewModel item in (IEnumerable)PartialStashListBox.Items)
			{
				if (item.Selected)
				{
					return item;
				}
			}
			return null;
		}

		private static string Translate(string text)
		{
			return PreferencesLocalization.Translate(text, ForkPlusSettings.Default.UiLanguage);
		}

	}
}

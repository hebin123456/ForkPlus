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
	public partial class RenameLocalBranchWindow : ForkPlusDialogWindow
	{
		private readonly GitModule _gitModule;

		private readonly RepositoryReferences _references;

		private readonly LocalBranch _localBranch;

		private readonly RemoteBranch _remoteBranch;

		protected override bool IsSubmitAllowed
		{
			get
			{
				SetStatus(ForkPlusDialogStatus.None, string.Empty);
				string newName = BranchNameTextBox.Text.ToLower();
				if (string.IsNullOrEmpty(newName))
				{
					return false;
				}
				if (RenameRemoteBranchCheckbox.IsChecked.GetValueOrDefault())
				{
					SetStatus(ForkPlusDialogStatus.Warning, Translate("Renaming can break tracking references for other users"));
					string upstreamRemote = UpstreamRemote(_localBranch);
					RemoteBranch remoteBranch = IReadOnlyListExtensions.FirstItem(_references.RemoteBranches, (RemoteBranch x) => x.ShortName.ToLower() == newName && x.Remote == upstreamRemote);
					if (remoteBranch != null)
					{
						SetStatus(ForkPlusDialogStatus.Warning, string.Format(Translate("Branch {0} already exists"), remoteBranch.Name));
						return false;
					}
				}
				if (!(newName != _localBranch.Name.ToLower()))
				{
					return false;
				}
				string text = ReferenceNameValidator.Validate(newName);
				if (text != null)
				{
					SetStatus(ForkPlusDialogStatus.Warning, text);
					return false;
				}
				if (_references.LocalBranches.AnyItem((LocalBranch x) => x.Name.ToLower() == newName))
				{
					SetStatus(ForkPlusDialogStatus.Warning, string.Format(Translate("Branch '{0}' already exists"), BranchNameTextBox.Text));
					return false;
				}
				return true;
			}
		}

		protected override string GetCommandPreview()
		{
			string newName = BranchNameTextBox.Text;
			if (string.IsNullOrWhiteSpace(newName))
			{
				return null;
			}
			string command = "git branch -m " + _localBranch.Name + " " + newName;
			if (RenameRemoteBranchCheckbox.IsChecked.GetValueOrDefault() && _remoteBranch != null)
			{
				command += "\ngit push " + _remoteBranch.Remote + " " + newName + " :" + _remoteBranch.ShortName;
			}
			return command;
		}

		public RenameLocalBranchWindow(GitModule gitModule, RepositoryReferences references, LocalBranch localBranch, [Null] string newName)
		{
			_gitModule = gitModule;
			_references = references;
			_localBranch = localBranch;
			InitializeComponent();
			RemoteBranch remoteBranch = IReadOnlyListExtensions.FirstItem(_references.RemoteBranches, (RemoteBranch x) => x.FullReference == localBranch.UpstreamFullReference);
			if (remoteBranch != null && remoteBranch.ShortName == _localBranch.Name)
			{
				_remoteBranch = remoteBranch;
				RenameRemoteBranchCheckbox.Show();
				RenameRemoteBranchCheckbox.Content = string.Format(Translate("Also rename {0}"), remoteBranch.Name.Replace("_", "__"));
			}
			else
			{
				RenameRemoteBranchCheckbox.Collapse();
			}
			base.DialogTitle = Translate("Rename Local Branch");
			base.DialogDescription = Translate("Rename local branch");
			base.SubmitButtonTitle = Translate("Rename");
			BranchNameTextBox.Text = newName ?? localBranch.Name;
			BranchNameTextBox.SelectAll();
			ReferenceTextBox branchNameTextBox = BranchNameTextBox;
			ForkPlus.Git.Reference[] references2 = references.Items.CompactMap((ForkPlus.Git.Reference x) => x as Branch);
			branchNameTextBox.SetAutocompleteProvider(new ReferenceNameAutocompleteProvider(references2));
			UpdateSubmitButton();
		}

		protected override void OnSubmit()
		{
			RepositoryUserControl activeRepositoryUserControl = MainWindow.ActiveRepositoryUserControl;
			if (activeRepositoryUserControl == null)
			{
				return;
			}
			GitModule gitModule = _gitModule;
			LocalBranch localBranch = _localBranch;
			RemoteBranch remoteBranch = _remoteBranch;
			string newName = BranchNameTextBox.Text;
			string[] pinned = _references.PinnedReferences;
			string[] filtered = _references.FilterReferences;
			bool renameUpstream = RenameRemoteBranchCheckbox.IsChecked.GetValueOrDefault();
			DisableEditableControls();
			SetStatus(ForkPlusDialogStatus.InProgress, Translate("Renaming branch..."));
			activeRepositoryUserControl.JobQueue.Add(string.Format(Translate("Rename branch '{0}'"), localBranch.Name), delegate(JobMonitor monitor)
			{
				GitCommandResult renameResult = new RenameLocalBranchGitCommand().Execute(gitModule, localBranch.Name, newName, monitor);
				if (!renameResult.Succeeded)
				{
					base.Dispatcher.Async(delegate
					{
						Close(renameResult);
					});
				}
				else
				{
					string fullReference = "refs/heads/" + newName;
					LocalBranch localBranch2 = new LocalBranch(localBranch.Sha, fullReference, newName, isActive: false, null, DateTime.Now);
					int num = Array.IndexOf(pinned, localBranch.FullReference);
					if (num != -1)
					{
						pinned[num] = localBranch2.FullReference;
					}
					int num2 = Array.IndexOf(filtered, localBranch.FullReference);
					if (num2 != -1)
					{
						filtered[num2] = localBranch2.FullReference;
					}
					if (renameUpstream && remoteBranch != null)
					{
						base.Dispatcher.Async(delegate
						{
							SetStatus(ForkPlusDialogStatus.InProgress, Translate("Renaming remote branch..."));
						});
						GitCommandResult renameRemoteBranchResult = new RenameRemoteBranchGitCommand().Execute(gitModule, remoteBranch, newName, monitor);
						if (!renameRemoteBranchResult.Succeeded)
						{
							base.Dispatcher.Async(delegate
							{
								Close(renameRemoteBranchResult);
							});
							return;
						}
						base.Dispatcher.Async(delegate
						{
							SetStatus(ForkPlusDialogStatus.InProgress, Translate("Updating tracking reference..."));
						});
						string fullReference2 = "refs/remotes/" + remoteBranch.Remote + "/" + newName;
						string fullName = remoteBranch.Remote + "/" + newName;
						RemoteBranch remoteBranch2 = new RemoteBranch(remoteBranch.Sha, fullReference2, fullName, newName, remoteBranch.Remote, remoteBranch.CommitterDate);
						GitCommandResult updateTrackingResult = new UpdateTrackingReferenceGitCommand().Execute(gitModule, localBranch2, remoteBranch2, monitor);
						if (!updateTrackingResult.Succeeded)
						{
							base.Dispatcher.Async(delegate
							{
								Close(updateTrackingResult);
							});
							return;
						}
						int num3 = Array.IndexOf(pinned, remoteBranch.FullReference);
						if (num3 != -1)
						{
							pinned[num3] = remoteBranch2.FullReference;
						}
						int num4 = Array.IndexOf(filtered, remoteBranch.FullReference);
						if (num4 != -1)
						{
							filtered[num4] = remoteBranch2.FullReference;
						}
					}
					gitModule.Settings.PinnedReferences = pinned;
					gitModule.Settings.FilterReferences = filtered;
					gitModule.Settings.Save();
					base.Dispatcher.Async(delegate
					{
						Close(renameResult);
					});
				}
			}, JobFlags.SaveToLog);
		}

		private void BranchName_TextChanged(object sender, TextChangedEventArgs e)
		{
			UpdateSubmitButton();
			RefreshCommandPreview();
		}

		private void RenameRemoteBranchCheckbox_Changed(object sender, RoutedEventArgs e)
		{
			UpdateSubmitButton();
			RefreshCommandPreview();
		}

		private static string UpstreamRemote(LocalBranch localBranch)
		{
			string upstreamFullName = localBranch.UpstreamFullName;
			if (upstreamFullName == null)
			{
				return null;
			}
			int num = upstreamFullName.IndexOf("/");
			if (num == -1)
			{
				return null;
			}
			return upstreamFullName.Substring(0, num);
		}

		private static string Translate(string text)
		{
			return PreferencesLocalization.Translate(text, ForkPlusSettings.Default.UiLanguage);
		}

	}
}

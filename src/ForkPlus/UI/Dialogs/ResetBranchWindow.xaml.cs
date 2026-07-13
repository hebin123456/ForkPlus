using System;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Markup;
using ForkPlus.Git;
using ForkPlus.Git.Commands;
using ForkPlus.Jobs;
using ForkPlus.UI.Controls;
using ForkPlus.UI.UserControls;
using ForkPlus.UI.UserControls.Preferences;

namespace ForkPlus.UI.Dialogs
{
	public partial class ResetBranchWindow : ForkPlusDialogWindow
	{
		private readonly RepositoryUserControl _repositoryUserControl;

		private readonly Revision _destination;

		[Null]
		private readonly LocalBranch _branch;

		private BranchResetType _resetType = BranchResetType.Mixed;

		protected override string GetCommandPreview()
		{
			string flag = _resetType switch
			{
				BranchResetType.Soft => "--soft",
				BranchResetType.Mixed => "--mixed",
				BranchResetType.Hard => "--hard",
				_ => null
			};
			if (flag == null)
			{
				return null;
			}
			string sha = _destination?.Sha?.ToAbbreviatedString();
			if (string.IsNullOrEmpty(sha))
			{
				return null;
			}
			return "git reset " + flag + " " + sha;
		}

		public ResetBranchWindow(RepositoryUserControl repositoryUserControl, [Null] LocalBranch activeBranch, Revision destination)
		{
			InitializeComponent();
			_repositoryUserControl = repositoryUserControl;
			_branch = activeBranch;
			_destination = destination;
			if (activeBranch != null)
		{
			base.DialogTitle = PreferencesLocalization.Current("Reset Current Branch to Revision");
			base.DialogDescription = PreferencesLocalization.FormatCurrent("Move the '{0}' branch HEAD to the selected revision", activeBranch.Name);
			ActiveBranchGitPointView.Value = activeBranch;
		}
		else
		{
			base.DialogTitle = PreferencesLocalization.Current("Reset HEAD to Revision");
			base.DialogDescription = PreferencesLocalization.Current("Move HEAD to the selected revision");
			ActiveBranchGitPointView.Value = new SymbolicReference("HEAD");
		}
		base.SubmitButtonTitle = PreferencesLocalization.Current("Reset");
			DestinationGitPointView.Value = _destination;
		}

		protected override void OnKeyDown(KeyEventArgs e)
		{
			base.OnKeyDown(e);
			if (e.Key == Key.S)
			{
				ResetTypeCombobox.SelectedIndex = 0;
			}
			else if (e.Key == Key.M)
			{
				ResetTypeCombobox.SelectedIndex = 1;
			}
			else if (e.Key == Key.H)
			{
				ResetTypeCombobox.SelectedIndex = 2;
			}
		}

		protected override void OnSubmit()
		{
			GitModule gitModule = _repositoryUserControl.GitModule;
			if (gitModule == null)
			{
				return;
			}
			string branchName = _branch?.Name ?? "HEAD";
			BranchResetType resetType = _resetType;
			Sha destinationSha = _destination.Sha;
			string resetTypeName = GetResetTypeName(_resetType);
			SubmodulesToUpdate submodulesToUpdate = _repositoryUserControl.SubmodulesToUpdate();
			DisableEditableControls();
			_repositoryUserControl.JobQueue.Add(PreferencesLocalization.FormatCurrent("Reset '{0}' ({1})", branchName, resetTypeName), delegate(JobMonitor monitor)
			{
				base.Dispatcher.Async(delegate
				{
					SetStatus(ForkPlusDialogStatus.InProgress, "Resetting '" + branchName + "'...");
				});
				GitCommandResult resetBranchResult = new ResetCurrentBranchToRevisionGitCommand().Execute(gitModule, destinationSha, resetType, monitor);
				GitCommandResult updateSubmodulesResult = GitCommandResult.Success();
				if (submodulesToUpdate.Length > 0 && resetType == BranchResetType.Hard)
				{
					base.Dispatcher.Async(delegate
					{
						SetStatus(ForkPlusDialogStatus.InProgress, "Updating submodules...");
					});
					updateSubmodulesResult = new UpdateSubmodulesGitCommand().Execute(gitModule, submodulesToUpdate, monitor);
				}
				base.Dispatcher.Async(delegate
				{
					if (!resetBranchResult.Succeeded)
					{
						Close(resetBranchResult);
					}
					else if (!updateSubmodulesResult.Succeeded)
					{
						Close(updateSubmodulesResult);
					}
					else
					{
						Close(resetBranchResult);
					}
				});
			});
		}

		private void ResetTypeCombobox_SelectionChanged(object sender, SelectionChangedEventArgs e)
		{
			ComboBoxItem comboBoxItem = e.AddedItems[0] as ComboBoxItem;
			_resetType = (BranchResetType)comboBoxItem.Tag;
			RefreshCommandPreview();
		}

		public static string GetResetTypeName(BranchResetType resetType)
		{
			return resetType switch
			{
				BranchResetType.Mixed => "mixed", 
				BranchResetType.Hard => "hard", 
				BranchResetType.Soft => "soft", 
				_ => throw new Exception("Cannot reach here"), 
			};
		}

	}
}

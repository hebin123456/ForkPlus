using Avalonia.Controls.Selection;
using Avalonia.Threading;
using System;
using ForkPlus.Git;
using ForkPlus.Git.Commands;
using ForkPlus.Jobs;
using ForkPlus.Settings;
using ForkPlus.UI.Controls;
using ForkPlus.UI.UserControls;
using ForkPlus.UI.UserControls.Preferences;

namespace ForkPlus.UI.Dialogs
{
	public partial class MergeBranchWindow : ForkPlusDialogWindow
	{
		public class MergeOptionComboBoxItem : INotifyPropertyChanged
		{
			public MergeType MergeType { get; }

			public string Title { get; }

			public string Description { get; }

			public string Command { get; }

			public bool IsSeparator { get; }

			public event PropertyChangedEventHandler PropertyChanged;

			private MergeOptionComboBoxItem(string title, string description, string command, MergeType mergeType, bool isSeparator)
			{
				MergeType = mergeType;
				Title = title;
				Description = description;
				Command = command;
				IsSeparator = isSeparator;
			}

			public MergeOptionComboBoxItem(string title, string description, string command, MergeType mergeType)
				: this(title, description, command, mergeType, isSeparator: false)
			{
			}

			public static MergeOptionComboBoxItem Separator()
			{
				return new MergeOptionComboBoxItem("", "", "", MergeType.FastForward, isSeparator: true);
			}
		}

		private readonly RepositoryUserControl _repositoryUserControl;

		private readonly ForkPlus.Git.Reference _source;

		private readonly LocalBranch _destination;

		private readonly MergeOptionComboBoxItem[] _mergeOptionsComboBoxItems = new MergeOptionComboBoxItem[5]
		{
			new MergeOptionComboBoxItem("Default", "Fast-forward if possible", "", MergeType.FastForward),
			new MergeOptionComboBoxItem("No Fast-Forward", "Always create a merge commit", "--no-ff", MergeType.NoFastForward),
			MergeOptionComboBoxItem.Separator(),
			new MergeOptionComboBoxItem("Squash", "Squash merge", "--squash", MergeType.Squash),
			new MergeOptionComboBoxItem("Don't Commit", "Merge without commit", "--no-commit", MergeType.NoCommit)
		};

		public MergeType SelectedMergeType => ((MergeOptionComboBoxItem)MergeTypeComboBox.SelectedItem).MergeType;

		public MergeBranchWindow(RepositoryUserControl repositoryUserControl, ForkPlus.Git.Reference source, LocalBranch destination)
		{
			_repositoryUserControl = repositoryUserControl;
			_source = source;
			_destination = destination;
			InitializeComponent();
			base.DialogTitle = PreferencesLocalization.Current("Merge Branch");
			base.DialogDescription = PreferencesLocalization.Current("Merge branch into another one");
			base.SubmitButtonTitle = PreferencesLocalization.Current("Merge");
			SourceGitPointView.Value = source;
			DestinationGitPointView.Value = destination;
			MergeTypeComboBox.ItemsSource = _mergeOptionsComboBoxItems;
			MergeType mergeTypeToSelect = ForkPlusSettings.Default.MergeType;
			MergeTypeComboBox.SelectedItem = IReadOnlyListExtensions.FirstItem(_mergeOptionsComboBoxItems, (MergeOptionComboBoxItem x) => x.MergeType == mergeTypeToSelect);
			GitCommandResult<MergeBranchTestGitCommand.TestResult> gitCommandResult = new MergeBranchTestGitCommand().Execute(repositoryUserControl.GitModule, _source, _destination);
			if (gitCommandResult.Succeeded)
			{
				if (gitCommandResult.Result == MergeBranchTestGitCommand.TestResult.Success)
				{
					SetStatus(ForkPlusDialogStatus.Success, "Merge can be done without conflicts");
				}
				else if (gitCommandResult.Result == MergeBranchTestGitCommand.TestResult.Conflict)
				{
					SetStatus(ForkPlusDialogStatus.Warning, "Merge will cause conflicts");
				}
			}
		}

		protected override string GetCommandPreview()
		{
			if (_source == null || !(MergeTypeComboBox.SelectedItem is MergeOptionComboBoxItem selected))
			{
				return null;
			}
			var parts = new System.Collections.Generic.List<string> { "git", "merge" };
			if (!string.IsNullOrEmpty(selected.Command))
			{
				parts.Add(selected.Command);
			}
			parts.Add(_source.Name);
			return string.Join(" ", parts);
		}

		protected override void OnSubmit()
		{
			GitModule gitModule = _repositoryUserControl.GitModule;
			if (gitModule == null)
			{
				return;
			}
			RepositoryData repositoryData = _repositoryUserControl.RepositoryData;
			if (repositoryData == null)
			{
				return;
			}
			ForkPlus.Git.Reference source = _source;
			LocalBranch destination = _destination;
			MergeType selectedMergeType = SelectedMergeType;
			SubmodulesToUpdate submodulesToUpdate = _repositoryUserControl.SubmodulesToUpdate();
			ForkPlusSettings.Default.MergeType = selectedMergeType;
			ForkPlusSettings.Default.Save();
			DisableEditableControls();
			_repositoryUserControl.AddUndoable(PreferencesLocalization.FormatCurrent("Merge '{0}' into '{1}'", source.Name, destination.Name), delegate(JobMonitor monitor)
		{
			if (!destination.IsActive)
			{
				base.Dispatcher.Async(delegate
				{
					SetStatus(ForkPlusDialogStatus.InProgress, "Checkout...");
				});
				GitCommandResult checkoutResult = new CheckoutBranchGitCommand().Execute(gitModule, destination, monitor);
				if (!checkoutResult.Succeeded)
				{
					base.Dispatcher.Async(delegate
					{
						Close(checkoutResult);
					});
					return checkoutResult;
				}
			}
			base.Dispatcher.Async(delegate
			{
				SetStatus(ForkPlusDialogStatus.InProgress, "Merging...");
			});
			GitCommandResult mergeResult = new MergeGitCommand().Execute(gitModule, source, selectedMergeType, repositoryData.References, monitor);
			GitCommandResult updateSubmodulesResult = GitCommandResult.Success();
			if (submodulesToUpdate.Length > 0)
			{
				base.Dispatcher.Async(delegate
				{
					SetStatus(ForkPlusDialogStatus.InProgress, "Updating submodules...");
				});
				updateSubmodulesResult = new UpdateSubmodulesGitCommand().Execute(gitModule, submodulesToUpdate, monitor);
			}
			base.Dispatcher.Async(delegate
			{
				if (!mergeResult.Succeeded)
				{
					Close(mergeResult);
				}
				else if (!updateSubmodulesResult.Succeeded)
				{
					Close(updateSubmodulesResult);
				}
				else
				{
					Close(mergeResult);
				}
			});
			return mergeResult.Succeeded ? updateSubmodulesResult : mergeResult;
		}, JobFlags.SaveToLog);
	}

		private void MergeTypeComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
		{
			RefreshCommandPreview();
		}

	}
}

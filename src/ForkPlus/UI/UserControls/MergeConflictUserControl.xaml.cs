using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Markup;
using System.Threading.Tasks;
using ForkPlus.Accounts.AiServices;
using ForkPlus.Git;
using ForkPlus.Git.Commands;
using ForkPlus.Jobs;
using ForkPlus.Settings;
using ForkPlus.UI.Commands;
using ForkPlus.UI.Controls;
using ForkPlus.UI.Dialogs;
using ForkPlus.UI.UserControls.Preferences;

namespace ForkPlus.UI.UserControls
{
	public partial class MergeConflictUserControl : UserControl
	{
		private RepositoryUserControl _repositoryUserControl;

		private DiffContent _fileContent;

		private RepositoryState _repositoryState;

		private ChangedFile _changedFile;

		private readonly MergeConflictUserControlViewModel _viewModel = new MergeConflictUserControlViewModel();

		private GitModule GitModule => _repositoryUserControl.GitModule;

		public MergeConflictUserControl()
		{
			InitializeComponent();
			LocalGitPointView.CustomFontStyle = true;
			RemoteGitPointView.CustomFontStyle = true;
		}

		public void SetConflict(RepositoryUserControl repositoryUserControl, DiffContent fileContent, RepositoryState repositoryState, ChangedFile changedFile, bool resolved)
		{
			_repositoryUserControl = repositoryUserControl;
			_fileContent = fileContent;
			_repositoryState = repositoryState;
			_changedFile = changedFile;
			FileIcon.Source = IconTools.GetImageSourceForExtension(Path.GetExtension(changedFile.Path));
			FileNameTextBlock.FilePath = changedFile.Path;
			FileNameTextBlock.ToolTip = changedFile.Path;
			FileDiffControl.RepositoryUserControl = repositoryUserControl;
			// AI Resolve 按钮：仅在 AI 配置完毕且未解决时显示
			if (!resolved && OpenAiService.IsAiReviewConfigured() && MergeConflictUserControlViewModel.IsMergeAllowed(changedFile))
			{
				AiResolveButton.Show();
			}
			else
			{
				AiResolveButton.Collapse();
			}
			if (resolved)
			{
				Path.GetFileName(changedFile.Path);
				ConflictVersionsContainer.Collapse();
				ConflictResolvedContainer.Show();
				GitCommandResult<DiffContent> gitCommandResult = new GetWorkingDirectoryFileChangesGitCommand().Execute(GitModule, changedFile, null, ForkPlusSettings.Default.DiffContextSize, GitModule.Settings.TabWidth, ForkPlusSettings.Default.DiffIgnoreWhitespaces, ForkPlusSettings.Default.DiffShowEntireFile, loadLargeUntrackedFiles: false, resolvedConflict: true);
				FileDiffControl.Content = GitCommandResult<DiffContent>.Success(gitCommandResult.Result);
				return;
			}
			ConflictVersionsContainer.Show();
			ConflictResolvedContainer.Collapse();
			if (MergeConflictUserControlViewModel.IsMergeAllowed(changedFile))
			{
				LocalCheckBox.IsChecked = true;
				RemoteCheckBox.IsChecked = true;
			}
			else
			{
				LocalCheckBox.IsChecked = false;
				RemoteCheckBox.IsChecked = false;
			}
			UpdateResolveButton();
			IGitPoint localGitPoint = MergeConflictRepositoryStateHelper.GetLocalGitPoint(repositoryState);
			IGitPoint remoteGitPoint = MergeConflictRepositoryStateHelper.GetRemoteGitPoint(repositoryState);
			UpdateMergeConflictDetails(_changedFile.Status, LocalChangeTypeImage, LocalChangeTypeTextBlock, LocalGitPointView, localGitPoint);
			UpdateMergeConflictDetails(_changedFile.WorkingDirectoryStatus, RemoteChangeTypeImage, RemoteChangeTypeTextBlock, RemoteGitPointView, remoteGitPoint);
			GitModule gitModule = repositoryUserControl.GitModule;
			string srcSha = MergeConflictUserControlViewModel.GetSha(remoteGitPoint);
			string dstSha = MergeConflictUserControlViewModel.GetSha(localGitPoint);
			repositoryUserControl.JobQueue.Add(PreferencesLocalization.Current("GetConflictDetails"), delegate
			{
				GetConflictFileModificationsGitCommand.ConflictModifications fileModificationsResponse = new GetConflictFileModificationsGitCommand().Execute(gitModule, repositoryState, srcSha, dstSha, changedFile.Path);
				base.Dispatcher.Invoke(delegate
				{
					UpdateRevisionsListBox(DstRevisionsListBox, DstSeparator, fileModificationsResponse.DstRevisions);
					UpdateRevisionsListBox(SrcRevisionsListBox, SrcSeparator, fileModificationsResponse.SrcRevisions);
				});
			}, JobFlags.Hidden);
		}

		private static void UpdateRevisionsListBox(ListBox listBox, Separator separator, Revision[] revisions)
		{
			MergeRevisionViewModel[] array = MergeConflictUserControlViewModel.MapRevisionsToViewModels(revisions);
			if (array.Length != 0)
			{
				listBox.Show();
				listBox.ItemsSource = array;
				separator.Show();
			}
			else
			{
				listBox.Collapse();
				separator.Collapse();
			}
		}

		private static void UpdateMergeConflictDetails(StatusType statusType, Image statusImage, TextBlock changeTypeTextBlock, GitPointView gitPointView, IGitPoint gitPoint)
		{
			statusImage.Source = statusType.GetConflictImageSource();
			statusImage.ToolTip = statusType.ToFriendlyName();
			changeTypeTextBlock.Text = statusType.ToFriendlyName();
			gitPointView.Value = gitPoint;
			gitPointView.ToolTip = gitPoint?.FriendlyName;
		}

		private void StageButton_Click(object sender, RoutedEventArgs e)
		{
			if (_changedFile != null)
			{
				_repositoryUserControl.Content.CommitUserControl.StageSelectedFiles();
			}
		}

		/// <summary>AI 解决当前文件的冲突：纯逻辑交由 VM（<see cref="MergeConflictUserControlViewModel.TryResolveWithAiAsync"/>），
		/// View 仅负责按钮 UI 状态、消费 <see cref="AiResolveResult"/> 弹 MessageBox，以及确认后调用
		/// VM 的 <see cref="MergeConflictUserControlViewModel.ApplyResolvedContent"/> 写回并执行 UI 副作用。</summary>
		private async void AiResolveButton_Click(object sender, RoutedEventArgs e)
		{
			GitModule gitModule = _repositoryUserControl?.GitModule;

			AiResolveButton.IsEnabled = false;
			string originalToolTip = AiResolveButton.ToolTip?.ToString();
			AiResolveButton.ToolTip = PreferencesLocalization.Current("AI is resolving conflicts...");

			AiResolveResult result = await _viewModel.TryResolveWithAiAsync(gitModule, _changedFile).ConfigureAwait(true);

			AiResolveButton.IsEnabled = true;
			AiResolveButton.ToolTip = originalToolTip ?? PreferencesLocalization.Current("Use AI to resolve all conflicts in this file");

			string title = PreferencesLocalization.Current("AI Resolve");
			switch (result.Status)
			{
				case AiResolveStatus.AlreadyResolving:
				case AiResolveStatus.InvalidState:
				case AiResolveStatus.PathResolutionFailed:
				case AiResolveStatus.Canceled:
					break;
				case AiResolveStatus.NotConfigured:
					MessageBox.Show(result.Message, title, MessageBoxButton.OK, MessageBoxImage.Warning);
					break;
				case AiResolveStatus.ReadFailed:
					MessageBox.Show(result.Message, title, MessageBoxButton.OK, MessageBoxImage.Error);
					break;
				case AiResolveStatus.NoConflictMarkers:
					MessageBox.Show(result.Message, title, MessageBoxButton.OK, MessageBoxImage.Information);
					break;
				case AiResolveStatus.RequestFailed:
					MessageBox.Show(result.Message, title, MessageBoxButton.OK, MessageBoxImage.Error);
					break;
				case AiResolveStatus.EmptyContent:
					MessageBox.Show(result.Message, title, MessageBoxButton.OK, MessageBoxImage.Warning);
					break;
				case AiResolveStatus.ConflictMarkersRemain:
					MessageBox.Show(result.Message, title, MessageBoxButton.OK, MessageBoxImage.Warning);
					break;
				case AiResolveStatus.Success:
					MessageBoxResult confirm = MessageBox.Show(result.Message, title, MessageBoxButton.YesNo, MessageBoxImage.Question);
					if (confirm == MessageBoxResult.Yes)
					{
						AiResolveResult applyResult = _viewModel.ApplyResolvedContent(_repositoryUserControl.GitModule, _changedFile, result.ResolvedContent);
						switch (applyResult.Status)
						{
							case AiResolveStatus.Applied:
								_repositoryUserControl.InvalidateAndRefresh(SubDomain.Status, null, RepositoryViewMode.CommitViewMode);
								break;
							case AiResolveStatus.GitFailed:
								new ErrorWindow(_repositoryUserControl, applyResult.GitError).ShowDialog();
								break;
							case AiResolveStatus.WriteFailed:
								MessageBox.Show(applyResult.Message, title, MessageBoxButton.OK, MessageBoxImage.Error);
								break;
						}
					}
					break;
			}
		}

		private void MergeCheckBox_Changed(object sender, RoutedEventArgs e)
		{
			UpdateResolveButton();
		}

		private void ResolveButton_Click(object sender, RoutedEventArgs e)
		{
			if (_changedFile == null)
			{
				return;
			}
			if (LocalCheckBox.IsChecked.GetValueOrDefault() && RemoteCheckBox.IsChecked.GetValueOrDefault())
			{
				Log.Info("Merge conflict in '" + _changedFile.Path + "' using merge tool");
				Log.Info($"Local change type: {_changedFile.Status}");
				Log.Info($"Remote change type: {_changedFile.WorkingDirectoryStatus}");
				if (_changedFile.Status == StatusType.Deleted || _changedFile.WorkingDirectoryStatus == StatusType.Deleted)
				{
					Log.Warn("Cannot merge a conflict when one of files is deleted.");
				}
				else
				{
					new ShowSideBySideMergeWindowCommand().Execute(_repositoryUserControl, _repositoryState, _changedFile);
				}
			}
			else if (LocalCheckBox.IsChecked.GetValueOrDefault())
			{
				GitCommandResult gitCommandResult = ((!(_changedFile is SubmoduleChangedFile changedFile) || !(_fileContent is SubmoduleDiffContent submoduleDiffContent)) ? new ResolveConflictGitCommand().Execute(GitModule, _changedFile, UnmergedFileVersionType.Local) : new ResolveConflictGitCommand().Execute(GitModule, changedFile, submoduleDiffContent.DstSha));
				if (!gitCommandResult.Succeeded)
				{
					new ErrorWindow(_repositoryUserControl, gitCommandResult.Error).ShowDialog();
				}
				_repositoryUserControl.InvalidateAndRefresh(SubDomain.Status, null, RepositoryViewMode.CommitViewMode);
			}
			else if (RemoteCheckBox.IsChecked.GetValueOrDefault())
			{
				GitCommandResult gitCommandResult2 = ((!(_changedFile is SubmoduleChangedFile changedFile2) || !(_fileContent is SubmoduleDiffContent submoduleDiffContent2)) ? new ResolveConflictGitCommand().Execute(GitModule, _changedFile, UnmergedFileVersionType.Remote) : new ResolveConflictGitCommand().Execute(GitModule, changedFile2, submoduleDiffContent2.SrcSha));
				if (!gitCommandResult2.Succeeded)
				{
					new ErrorWindow(_repositoryUserControl, gitCommandResult2.Error).ShowDialog();
				}
				_repositoryUserControl.InvalidateAndRefresh(SubDomain.Status, null, RepositoryViewMode.CommitViewMode);
			}
		}

		private void ResolveInExternalMergerButton_Click(object sender, RoutedEventArgs e)
		{
			if (_changedFile == null || !LocalCheckBox.IsChecked.GetValueOrDefault() || !RemoteCheckBox.IsChecked.GetValueOrDefault())
			{
				return;
			}
			if (!MergeConflictUserControlViewModel.IsMergeAllowed(_changedFile))
			{
				Log.Warn("Cannot merge a conflict when one of files is deleted.");
				return;
			}
			List<ExternalTool> list = ExternalToolManager.RevealAvailableMergeTools().Filter((ExternalTool x) => x.IsVisible);
			if (list.Count > 0)
			{
				ExternalTool mergeTool = list.FirstItemStruct((ExternalTool x) => x.IsPrimary) ?? list[0];
				RepositoryUserControl.Commands.RunExternalMergeTool.Execute(_repositoryUserControl, _changedFile.Path, mergeTool);
			}
		}

		private void UpdateResolveButton()
		{
			ResolveButtonState state = MergeConflictUserControlViewModel.ComputeResolveButtonState(
				LocalCheckBox.IsChecked.GetValueOrDefault(),
				RemoteCheckBox.IsChecked.GetValueOrDefault(),
				MergeConflictUserControlViewModel.IsMergeAllowed(_changedFile),
				_changedFile.Status,
				_changedFile.WorkingDirectoryStatus,
				LocalGitPointView.Value?.FriendlyName,
				RemoteGitPointView.Value?.FriendlyName);

			ResolveButton.Content = state.Label;
			if (state.CanResolve)
			{
				ResolveButton.Enable();
			}
			else
			{
				ResolveButton.Disable();
			}
			if (state.ShowExternalMerger)
			{
				ResolveInExternalMergerButton.Show();
				ResolveInExternalMergerButton.Content = state.ExternalMergerLabel;
				if (state.ShowDropdown)
				{
					ResolveInExternalMergerButton.Style = Theme.CommitUserControl.CommitButtonVisibleDropdownStyle;
					ResolveInExternalMergerDropdownButton.Show();
				}
				else
				{
					ResolveInExternalMergerButton.Style = Theme.CommitUserControl.CommitButtonHiddenDropdownStyle;
					ResolveInExternalMergerDropdownButton.Collapse();
				}
			}
			else
			{
				ResolveInExternalMergerButton.Hide();
				ResolveInExternalMergerDropdownButton.Collapse();
			}
		}

		private void ShaButton_Click(object sender, RoutedEventArgs e)
		{
			if (sender is Button { DataContext: MergeRevisionViewModel dataContext })
			{
				_repositoryUserControl.ActivateRevisionView();
				_repositoryUserControl.SelectRevision(dataContext.Sha, _changedFile.Path);
			}
		}

		private void ResolveInExternalMergerDropdownButtonContextMenu_Opened(object sender, RoutedEventArgs e)
		{
			ContextMenu contextMenu = sender as ContextMenu;
			contextMenu.Items.Clear();
			if (_changedFile == null)
			{
				return;
			}
			List<ExternalTool> list = ExternalToolManager.RevealAvailableMergeTools().Filter((ExternalTool x) => x.IsVisible);
			if (list.Count < 2 || !LocalCheckBox.IsChecked.GetValueOrDefault() || !RemoteCheckBox.IsChecked.GetValueOrDefault())
			{
				return;
			}
			if (!MergeConflictUserControlViewModel.IsMergeAllowed(_changedFile))
			{
				Log.Warn("Cannot merge a conflict when one of files is deleted.");
				return;
			}
			foreach (ExternalTool mergeTool in list)
			{
				MenuItem newItem = RepositoryUserControl.Commands.RunExternalMergeTool.CreateMenuItem(mergeTool.Name ?? "", delegate
				{
					RepositoryUserControl.Commands.RunExternalMergeTool.Execute(_repositoryUserControl, _changedFile.Path, mergeTool);
				}, isEnabled: true, null, mergeTool.IsPrimary);
				contextMenu.Items.Add(newItem);
			}
		}

	}
}

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Text;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup;
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
using ForkPlus.Utils.Http;
using Avalonia.Layout;
using Avalonia.Styling;
using Avalonia.Interactivity;
using Avalonia.Threading;

namespace ForkPlus.UI.UserControls
{
	public partial class MergeConflictUserControl : UserControl
	{
		private RepositoryUserControl _repositoryUserControl;

		private DiffContent _fileContent;

		private RepositoryState _repositoryState;

		private ChangedFile _changedFile;

		private bool _aiResolving;

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
			global::Avalonia.Controls.ToolTip.SetTip(FileNameTextBlock,changedFile.Path);
			FileDiffControl.RepositoryUserControl = repositoryUserControl;
			// AI Resolve 按钮：仅在 AI 配置完毕且未解决时显示
			if (!resolved && OpenAiService.IsAiReviewConfigured() && IsMergeAllowed(changedFile))
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
			if (IsMergeAllowed(changedFile))
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
			string srcSha = GetSha(remoteGitPoint);
			string dstSha = GetSha(localGitPoint);
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

		[Null]
		private static string GetSha(IGitPoint gitPoint)
		{
			if (gitPoint is ForkPlus.Git.Reference { Sha: var sha })
			{
				return sha.ToString();
			}
			return null;
		}

		private static void UpdateRevisionsListBox(ListBox listBox, Separator separator, Revision[] revisions)
		{
			MergeRevisionViewModel[] array = revisions.Map((Revision x) => new MergeRevisionViewModel(x));
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
			global::Avalonia.Controls.ToolTip.SetTip(statusImage,statusType.ToFriendlyName());
			changeTypeTextBlock.Text = statusType.ToFriendlyName();
			gitPointView.Value = gitPoint;
			global::Avalonia.Controls.ToolTip.SetTip(gitPointView,gitPoint?.FriendlyName);
		}

		private void StageButton_Click(object sender, RoutedEventArgs e)
		{
			// v3.8.3：链式访问可能 NRE
			if (_changedFile != null && _repositoryUserControl?.Content?.CommitUserControl != null)
			{
				_repositoryUserControl.Content.CommitUserControl.StageSelectedFiles();
			}
		}

		/// <summary>AI 解决当前文件的冲突：读取磁盘上带冲突标记的文件，发送给 AI，
		/// 用户确认后通过 ResolveMergeConflictGitCommand 写回。</summary>
		private async void AiResolveButton_Click(object sender, RoutedEventArgs e)
		{
			// v3.9.2：修复按钮无反应——之前 _aiResolving 卡在 true 会导致永远静默 return
			if (_aiResolving)
			{
				// v3.9.2：排队时给用户明确反馈，不再完全没反应
				new MessageBoxWindow(
					PreferencesLocalization.Current("AI Resolve"),
					PreferencesLocalization.Current("AI is still resolving conflicts. Please wait for the current task to finish."),
					PreferencesLocalization.Current("OK"),
					showCancelButton: false).ShowDialog();
				return;
			}
			if (_changedFile == null || _repositoryUserControl?.GitModule == null)
			{
				// v3.9.2：状态未就绪也给反馈
				new MessageBoxWindow(
					PreferencesLocalization.Current("AI Resolve"),
					PreferencesLocalization.Current("Cannot resolve conflicts: file or repository is not ready."),
					PreferencesLocalization.Current("OK"),
					showCancelButton: false,
					showWarningIcon: true).ShowDialog();
				return;
			}
			// v3.8.3：async void 方法必须捕获所有异常，否则未处理异常会导致应用闪退
			try
			{
			await AiResolveButton_ClickCore();
			}
			catch (Exception ex)
			{
				Log.Error("AI Resolve crashed: " + ex);
				_aiResolving = false;
				AiResolveButton.SetBusy(false);
			}
		}

		private async Task AiResolveButton_ClickCore()
		{
			if (!OpenAiService.IsAiReviewConfigured())
		{
			// v3.8.2：用 ForkPlus 自定义弹窗替换原生 MessageBox
			new MessageBoxWindow(
				PreferencesLocalization.Current("AI Resolve"),
				PreferencesLocalization.Current("AI is not configured. Please configure AI review settings in Preferences first."),
				PreferencesLocalization.Current("OK"),
				showCancelButton: false,
				showWarningIcon: true).ShowDialog();
			return;
		}
			GitModule gitModule = _repositoryUserControl.GitModule;
			string filePath;
			try
			{
				filePath = gitModule.MakePath(_changedFile.Path);
			}
			catch (Exception ex)
			{
				Log.Error("AI Resolve: failed to resolve file path: " + ex.Message);
				// v3.9.2：路径解析失败不再静默，给用户反馈
				new MessageBoxWindow(
					PreferencesLocalization.Current("AI Resolve"),
					PreferencesLocalization.FormatCurrent("Failed to resolve file path: {0}", ex.Message),
					PreferencesLocalization.Current("OK"),
					showCancelButton: false,
					showWarningIcon: true).ShowDialog();
				return;
			}
			string conflictedContent;
			try
			{
				conflictedContent = File.ReadAllText(filePath);
			}
			catch (Exception ex)
			{
				Log.Error("AI Resolve: failed to read conflict file: " + ex.Message);
			new MessageBoxWindow(
				PreferencesLocalization.Current("AI Resolve"),
				PreferencesLocalization.FormatCurrent("Failed to read conflict file: {0}", ex.Message),
				PreferencesLocalization.Current("OK"),
				showCancelButton: false,
				showWarningIcon: true).ShowDialog();
			return;
		}
			if (string.IsNullOrEmpty(conflictedContent)
				|| !conflictedContent.Contains("<<<<<<<") || !conflictedContent.Contains(">>>>>>>"))
			{
				new MessageBoxWindow(
				PreferencesLocalization.Current("AI Resolve"),
				PreferencesLocalization.Current("No conflict markers found in the file."),
				PreferencesLocalization.Current("OK"),
				showCancelButton: false).ShowDialog();
			return;
		}

			_aiResolving = true;
			// v3.9.0：统一用 AiActionButton.SetBusy 管理 Loading 态
			AiResolveButton.SetBusy(true, PreferencesLocalization.Current("AI is resolving conflicts..."));

			string fileName = Path.GetFileName(_changedFile.Path);
			string prompt = OpenAiService.BuildResolveConflictsPrompt(fileName, conflictedContent);

			StringBuilder responseBuilder = new StringBuilder();
			Exception requestError = null;
			bool canceled = false;

			await Task.Run(delegate
			{
				try
				{
					OpenAiService aiService = OpenAiService.CreateFromAiReviewSettings();
					JobMonitor monitor = new JobMonitor();
					ServiceResult<OpenAiResponse> result = aiService.OpenAiRequestStreamingWithRetry(
						prompt,
						monitor,
						delegate(string delta)
						{
							if (string.IsNullOrEmpty(delta))
							{
								return;
							}
							lock (responseBuilder)
							{
								responseBuilder.Append(delta);
							}
						});
					if (monitor.IsCanceled)
					{
						canceled = true;
						return;
					}
					if (!result.Succeeded)
					{
						requestError = new Exception(result.Error?.FriendlyMessage ?? PreferencesLocalization.Current("Unknown error"));
					}
				}
				catch (Exception ex)
				{
					requestError = ex;
				}
			}).ConfigureAwait(true);

			// v3.8.3：await 后控件可能已卸载（用户切换了 Tab/文件），async void 未捕获异常会闪退
			if (!IsLoaded)
			{
				_aiResolving = false;
				return;
			}
			AiResolveButton.SetBusy(false);
			_aiResolving = false;

			if (canceled)
			{
				return;
			}
			if (requestError != null)
			{
				Log.Error("AI Resolve failed: " + requestError.Message);
				new MessageBoxWindow(
					PreferencesLocalization.Current("AI Resolve"),
					PreferencesLocalization.FormatCurrent("AI resolve failed: {0}", requestError.Message),
					PreferencesLocalization.Current("OK"),
					showCancelButton: false,
					showWarningIcon: true).ShowDialog();
				return;
			}

			string resolved;
			lock (responseBuilder)
			{
				resolved = responseBuilder.ToString();
			}
			resolved = OpenAiService.StripCodeFences(resolved);
			if (string.IsNullOrWhiteSpace(resolved))
			{
				new MessageBoxWindow(
					PreferencesLocalization.Current("AI Resolve"),
					PreferencesLocalization.Current("AI returned empty content. Aborting."),
					PreferencesLocalization.Current("OK"),
					showCancelButton: false,
					showWarningIcon: true).ShowDialog();
				return;
			}
			if (resolved.Contains("<<<<<<<") || resolved.Contains(">>>>>>>") || resolved.Contains("======="))
			{
				new MessageBoxWindow(
					PreferencesLocalization.Current("AI Resolve"),
					PreferencesLocalization.Current("AI output still contains conflict markers. Please review and try again, or resolve manually."),
					PreferencesLocalization.Current("OK"),
					showCancelButton: false,
					showWarningIcon: true).ShowDialog();
				return;
			}

			if (!new MessageBoxWindow(
				PreferencesLocalization.Current("AI Resolve"),
				PreferencesLocalization.Current("AI resolved all conflicts. Apply the resolved content?"),
				PreferencesLocalization.Current("Apply"),
				PreferencesLocalization.Current("Cancel"),
				showCancelButton: true,
				550.0).ShowDialog().GetValueOrDefault())
			{
				return;
			}

			try
			{
				GitCommandResult gitResult = new ResolveMergeConflictGitCommand().Execute(gitModule, _changedFile, resolved);
				if (!gitResult.Succeeded)
				{
					new ErrorWindow(_repositoryUserControl, gitResult.Error).ShowDialog();
				}
				else
				{
					_repositoryUserControl.InvalidateAndRefresh(SubDomain.Status, null, RepositoryViewMode.CommitViewMode);
				}
			}
			catch (Exception ex)
			{
				Log.Error("AI Resolve: failed to write back: " + ex.Message);
				new MessageBoxWindow(
					PreferencesLocalization.Current("AI Resolve"),
					PreferencesLocalization.FormatCurrent("Failed to apply resolved content: {0}", ex.Message),
					PreferencesLocalization.Current("OK"),
					showCancelButton: false,
					showWarningIcon: true).ShowDialog();
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
			if (!IsMergeAllowed(_changedFile))
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
			// v3.8.3：CheckBox 可能在 SetConflict 完成前被操作，_changedFile 可能为 null
			if (_changedFile == null)
			{
				return;
			}
			ResolveInExternalMergerButton.Hide();
			ResolveInExternalMergerDropdownButton.Collapse();
			if (LocalCheckBox.IsChecked.GetValueOrDefault() && RemoteCheckBox.IsChecked.GetValueOrDefault())
			{
				if (IsMergeAllowed(_changedFile))
				{
					ResolveButton.Content = PreferencesLocalization.Current("Merge");
					ResolveButton.Enable();
					List<ExternalTool> list = ExternalToolManager.RevealAvailableMergeTools().Filter((ExternalTool x) => x.IsVisible);
					if (list.Count > 0)
					{
						ResolveInExternalMergerButton.Show();
						ExternalTool externalTool = list.FirstItemStruct((ExternalTool x) => x.IsPrimary) ?? list[0];
						ResolveInExternalMergerButton.Content = PreferencesLocalization.FormatCurrent("Merge in {0}", externalTool.Name);
						if (list.Count > 1)
						{
{							global::ForkPlus.UI.WpfCompat.StyleCompat.SetStyle(ResolveInExternalMergerButton, global::ForkPlus.UI.Theme.CommitUserControl.CommitButtonVisibleDropdownStyle);
}							ResolveInExternalMergerDropdownButton.Show();
						}
						else
						{
{							global::ForkPlus.UI.WpfCompat.StyleCompat.SetStyle(ResolveInExternalMergerButton, global::ForkPlus.UI.Theme.CommitUserControl.CommitButtonHiddenDropdownStyle);
}							ResolveInExternalMergerDropdownButton.Collapse();
						}
					}
				}
				else
				{
					ResolveButton.Content = PreferencesLocalization.Current("Select version to resolve with");
					ResolveButton.Disable();
				}
			}
			else if (LocalCheckBox.IsChecked.GetValueOrDefault())
			{
				ResolveButton.Content = ((_changedFile.Status == StatusType.Deleted) ? PreferencesLocalization.Current("Delete file") : PreferencesLocalization.FormatCurrent("Choose {0}", LocalGitPointView.Value.FriendlyName));
				ResolveButton.Enable();
			}
			else if (RemoteCheckBox.IsChecked.GetValueOrDefault())
			{
				ResolveButton.Content = ((_changedFile.WorkingDirectoryStatus == StatusType.Deleted) ? PreferencesLocalization.Current("Delete file") : PreferencesLocalization.FormatCurrent("Choose {0}", RemoteGitPointView.Value.FriendlyName));
				ResolveButton.Enable();
			}
			else
			{
				ResolveButton.Content = PreferencesLocalization.Current("Merge");
				ResolveButton.Disable();
			}
		}

		private static bool IsMergeAllowed(ChangedFile unmergedFile)
		{
			if (unmergedFile.Status != StatusType.Deleted)
			{
				return unmergedFile.WorkingDirectoryStatus != StatusType.Deleted;
			}
			return false;
		}

		private void ShaButton_Click(object sender, RoutedEventArgs e)
		{
			// v3.8.3：_repositoryUserControl 和 _changedFile 可能为 null
			if (sender is Button { DataContext: MergeRevisionViewModel dataContext }
				&& _repositoryUserControl != null && _changedFile != null)
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
			if (!IsMergeAllowed(_changedFile))
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

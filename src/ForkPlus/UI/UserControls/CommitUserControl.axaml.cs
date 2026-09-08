using System;
using ForkPlus.UI.WpfCompat;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Media;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Markup;
using Avalonia.Media;
using ForkPlus.Accounts;
using ForkPlus.Accounts.AiServices;
using ForkPlus.Biturbo;
using ForkPlus.Git;
using ForkPlus.Git.Commands;
using ForkPlus.Git.Diff;
using ForkPlus.Git.Diff.Presentation;
using ForkPlus.Git.Interaction;
using ForkPlus.Jobs;
using ForkPlus.Settings;
using ForkPlus.UI.Commands;
using ForkPlus.UI.Controls;
using ForkPlus.UI.Controls.Editor.Diff;
using ForkPlus.UI.CustomCommands;
using ForkPlus.UI.Dialogs;
using ForkPlus.UI.UserControls.Preferences;
using ForkPlus.Utils.Http;
using ForkPlus.UI.Helpers;
using Avalonia.Layout;
using Avalonia.Styling;
using Avalonia.Interactivity;
using Avalonia.Threading;

namespace ForkPlus.UI.UserControls
{
	public partial class CommitUserControl : UserControl, ForkPlus.UI.ILocalizableControl
	{
		private static readonly string PrepareCommitMsgHook;

		private static readonly CommitUserControlCommands Commands;

		private readonly CommitMessageAutocompleteProvider _commitMessageAutocompleteProvider = new CommitMessageAutocompleteProvider();

		// Gitmoji 自动补全：commit subject 输入 ":" 触发 emoji 选择器
		private readonly GitmojiAutocompleteProvider _gitmojiAutocompleteProvider = new GitmojiAutocompleteProvider();

		private readonly DelayedAction<ChangedFileArgs> _updateDiffAction;

		private DiffPopupWindow _diffPopupWindow;

		private bool _refreshing;

		private bool _pendingRepositoryStatusUiRefresh;

		private int _stagedDiffStatsRequestId;

		private int _diffRequestId;

		private bool _isLoaded;

		[Null]
		private string _rebaseAmendSha;

		public bool DontRefreshOnAmend { get; set; }

		public bool AmendMode
		{
			get
			{
				if (CheckBoxAmend.Dispatcher.CheckAccess())
				{
					return CheckBoxAmend.IsChecked.GetValueOrDefault();
				}
				return CheckBoxAmend.Dispatcher.Invoke(new Func<bool>(() => CheckBoxAmend.IsChecked.GetValueOrDefault()));
			}
			set
			{
				if (CheckBoxAmend.Dispatcher.CheckAccess())
				{
					CheckBoxAmend.IsChecked = value;
				}
				else
				{
					CheckBoxAmend.Dispatcher.Invoke(new Action(() => CheckBoxAmend.IsChecked = value));
				}
			}
		}

		private bool SquashMode => RepositoryUserControl.RepositoryStatus?.RepositoryState is RepositoryState.SquashInProgress;

		private bool CommitAndPush
		{
			get
			{
				if (AmendMode || RebaseInProgress || SquashMode)
				{
					return false;
				}
				if (!KeyboardHelper.IsShiftDown)
				{
					return ForkPlusSettings.Default.PushAutomaticallyOnCommit;
				}
				return !ForkPlusSettings.Default.PushAutomaticallyOnCommit;
			}
		}

		public RepositoryUserControl RepositoryUserControl { get; private set; }

		public string FullCommitMessage
		{
			get
			{
				string result = CommitSubjectTextBox.Text.Trim(Consts.Chars.NewLines);
				if (!string.IsNullOrEmpty(CommitDescriptionTextBox.Text))
				{
					result = CommitSubjectTextBox.Text ?? "";
					return result + "\n\n" + CommitDescriptionTextBox.Text;
				}
				return result;
			}
			set
			{
				string text = value ?? string.Empty;
				SplitCommitMessageForFields(text, out var subject, out var description);
				CommitDescriptionTextBox.DisableUpdates = true;
				// 同步关闭 subject 框的自动补全：避免 AI 生成 / 最近消息回填等场景下
				// 若 subject 恰好包含 ":" 触发 Gitmoji 选择器弹出
				CommitSubjectTextBox.DisableUpdates = true;
				CommitSubjectTextBox.Text = subject;
				CommitSubjectTextBox.DisableUpdates = false;
				CommitDescriptionTextBox.Text = description;
				CommitDescriptionTextBox.DisableUpdates = false;
			}
		}

		public bool CommittingInProgress { get; set; }

		public Job StageJob { get; set; }

		/// <summary>v3.10.2 修复：清理"卡死"的 StageJob。
		/// Stage/Unstage/Discard 各命令的完成回调里 StageJob=null 都包在 if (!monitor.IsCanceled) 内，
		/// job 一旦被取消（刷新/切 tab 等取消了在跑的 stage job），回调跳过重置 → StageJob 永远非 null
		/// → 之后所有暂存/取消暂存/丢弃在入口处被 StageJob != null 静默拦截，表现为"双击无法暂存"。
		/// 这里兜底：job 已处于 Finished 状态却仍挂着引用，说明回调没重置，直接清掉放行后续操作。
		/// （新 job 只能在 StageJob==null 时启动，Finished 的 job 不可能还在跑，清掉是安全的。）</summary>
		private void ClearStaleStageJob()
		{
			Job stageJob = StageJob;
			if (stageJob != null && stageJob.Status == JobStatus.Finished)
			{
				StageJob = null;
			}
		}

		private GitModule GitModule => RepositoryUserControl.GitModule;

		public bool ShowIgnoredFiles { get; set; }

		public bool IsCommitAllowed
		{
			get
			{
				RepositoryState repositoryState = RepositoryUserControl.RepositoryStatus?.RepositoryState;
				if (repositoryState == null)
				{
					return false;
				}
				if (StageJob != null)
				{
					return false;
				}
				if (CommittingInProgress)
				{
					return false;
				}
				if (repositoryState is RepositoryState.MergeInProgress || repositoryState is RepositoryState.RebaseInProgress || repositoryState is RepositoryState.SequencerInProgress || repositoryState is RepositoryState.SquashInProgress || repositoryState is RepositoryState.AmInProgress)
				{
					return !StageFileUserControl.ContainsUnmergedItems;
				}
				if (StageFileUserControl.StagedFilesFileListUserControl.TreeView.Items.Count == 0 && !AmendMode)
				{
					return false;
				}
				GitModule gitModule = RepositoryUserControl.GitModule;
				if (gitModule != null && gitModule.Settings != null && gitModule.Settings.SkipCommitMessage)
				{
					return true;
				}
				if (string.IsNullOrWhiteSpace(CommitSubjectTextBox.Text))
				{
					return false;
				}
				return true;
			}
		}

		private bool AreCommitFieldsAllowed
		{
			get
			{
				RepositoryState repositoryState = RepositoryUserControl.RepositoryStatus?.RepositoryState;
				if (repositoryState == null)
				{
					return false;
				}
				if (StageJob != null)
				{
					return false;
				}
				if (CommittingInProgress)
				{
					return false;
				}
				if (repositoryState is RepositoryState.RebaseInProgress rebaseInProgress)
				{
					return rebaseInProgress.AmendSha != null;
				}
				if (repositoryState is RepositoryState.AmInProgress)
				{
					return false;
				}
				return true;
			}
		}

		private int MaxDescriptionHeight
		{
			get
			{
				RepositoryState repositoryState = RepositoryUserControl.RepositoryStatus?.RepositoryState;
				if (repositoryState is RepositoryState.AmInProgress || repositoryState is RepositoryState.CherryPickInProgress || repositoryState is RepositoryState.SequencerInProgress || repositoryState is RepositoryState.RevertInProgress || repositoryState is RepositoryState.MergeInProgress || repositoryState is RepositoryState.RebaseInProgress || repositoryState is RepositoryState.UnmergedIndex || repositoryState is RepositoryState.SquashInProgress)
				{
					return 138;
				}
				return 235;
			}
		}

		private bool RebaseInProgress => RepositoryUserControl.RepositoryStatus?.RepositoryState is RepositoryState.RebaseInProgress;

		private bool AmInProgress => RepositoryUserControl.RepositoryStatus?.RepositoryState is RepositoryState.AmInProgress;

		static CommitUserControl()
		{
			PrepareCommitMsgHook = "prepare-commit-msg";
			Commands = new CommitUserControlCommands();
			// Migration note：WPF TabNavigationProperty.OverrideMetadata(Local) → 改构造函数 KeyboardNavigation.SetTabNavigation(this, Local)（Avalonia OverrideMetadata 为私有）。
		}

		public CommitUserControl()
		{
			_updateDiffAction = new DelayedAction<ChangedFileArgs>(UpdateDiff);
			InitializeComponent();
			PreferencesLocalization.ApplyCurrent(this);
			CommitDescriptionTextBox.SetAutocompleteProvider(_commitMessageAutocompleteProvider);
			// 为 commit subject 输入框注册 Gitmoji 自动补全（输入 ":" 触发 emoji 选择器）
			CommitSubjectTextBox.SetAutocompleteProvider(_gitmojiAutocompleteProvider);
			RefreshSubjectLengthLimitToolTip();
			base.Loaded += delegate
			{
				if (!_isLoaded)
				{
					_isLoaded = true;
					InitializeButtonHandlers();
					InitializeKeyBindings();
					RestoreGridColumnWidth();
					RefreshDescriptionFieldHeight();
					StageFileUserControl.RefreshUnstagedStatusLabel(GitModule.Settings.HideUntrackedFiles, ShowIgnoredFiles);
				}
			};
			gridSplitter.DragCompleted += delegate
			{
				SaveGridColumnWidth();
			};
			CommitFileDiffControl fileDiffControl = FileDiffControl;
			fileDiffControl.ShowLargeUntrackedChanges = (EventHandler)Delegate.Combine(fileDiffControl.ShowLargeUntrackedChanges, (EventHandler)delegate
			{
				ChangedFile changedFile2 = StageFileUserControl.SelectedUnstagedFiles.FirstItem();
				_updateDiffAction.InvokeNow(new ChangedFileArgs(changedFile2, loadLargeUntrackedFiles: true));
			});
			StageFileUserControl.KeyDown += delegate(object s, KeyEventArgs e)
			{
				if (e.Key == Key.S && KeyboardHelper.IsCtrlDown && !KeyboardHelper.IsAltDown)
				{
					ToggleStageForSelectedFiles();
					e.Handled = true;
				}
			};
			StageFileUserControl stageFileUserControl = StageFileUserControl;
			stageFileUserControl.ShowDiffPopup = (EventHandler<EventArgs>)Delegate.Combine(stageFileUserControl.ShowDiffPopup, new EventHandler<EventArgs>(StageFileUserControl_ShowDiffPopupChanged));
			StageFileUserControl stageFileUserControl2 = StageFileUserControl;
			stageFileUserControl2.StagedFilesItemSourceChanged = (EventHandler<EventArgs>)Delegate.Combine(stageFileUserControl2.StagedFilesItemSourceChanged, new EventHandler<EventArgs>(StageFileUserControl_StagedFilesItemSourceChanged));
			StageFileUserControl stageFileUserControl3 = StageFileUserControl;
			stageFileUserControl3.SelectionChanged = (EventHandler<FileListEventArgs>)Delegate.Combine(stageFileUserControl3.SelectionChanged, new EventHandler<FileListEventArgs>(StageFileUserControl_SelectionChanged));
			StageFileUserControl.AddCommandBinding(RepositoryUserControl.Commands.OpenFileInDefaultEditor.CreateShortcutCommandBinding(delegate
			{
				if (StageFileUserControl.IsStagedListSelected)
				{
					RepositoryUserControl.Commands.OpenFileInDefaultEditor.Execute(GitModule, StageFileUserControl.SelectedStagedFiles.FirstItem()?.Path);
				}
				else
				{
					RepositoryUserControl.Commands.OpenFileInDefaultEditor.Execute(GitModule, StageFileUserControl.SelectedUnstagedFiles.FirstItem()?.Path);
				}
			}));
			StageFileUserControl.AddCommandBinding(RepositoryUserControl.Commands.CopyFilePaths.CreateShortcutCommandBinding(delegate
			{
				if (StageFileUserControl.IsStagedListSelected)
				{
					RepositoryUserControl.Commands.CopyFilePaths.Execute(StageFileUserControl.SelectedStagedFiles.Map((ChangedFile x) => x.Path));
				}
				else
				{
					RepositoryUserControl.Commands.CopyFilePaths.Execute(StageFileUserControl.SelectedUnstagedFiles.Map((ChangedFile x) => x.Path));
				}
			}));
			StageFileUserControl.AddCommandBinding(RepositoryUserControl.Commands.CopyAbsoluteFilePaths.CreateShortcutCommandBinding(delegate
			{
				if (StageFileUserControl.IsStagedListSelected)
				{
					RepositoryUserControl.Commands.CopyAbsoluteFilePaths.Execute(GitModule, StageFileUserControl.SelectedStagedFiles.Map((ChangedFile x) => x.Path));
				}
				else
				{
					RepositoryUserControl.Commands.CopyAbsoluteFilePaths.Execute(GitModule, StageFileUserControl.SelectedUnstagedFiles.Map((ChangedFile x) => x.Path));
				}
			}));
			StageFileUserControl.AddCommandBinding(RepositoryUserControl.Commands.RunExternalDiffTool.CreateShortcutCommandBinding(delegate
			{
				ChangedFile changedFile;
				RunExternalDiffToolCommand.DiffTarget diffTarget;
				if (StageFileUserControl.IsStagedListSelected)
				{
					changedFile = StageFileUserControl.SelectedStagedFiles.FirstItem();
					diffTarget = new RunExternalDiffToolCommand.DiffTarget.WorkingDirectory(changedFile, AmendMode);
				}
				else
				{
					if (!StageFileUserControl.IsUnstagedListSelected)
					{
						return;
					}
					changedFile = StageFileUserControl.SelectedUnstagedFiles.FirstItem();
					diffTarget = new RunExternalDiffToolCommand.DiffTarget.WorkingDirectory(changedFile, AmendMode);
				}
				if (changedFile.ChangeType == ChangeType.Unmerged)
				{
					List<ExternalTool> list = ExternalToolManager.RevealAvailableMergeTools().Filter((ExternalTool x) => x.IsVisible);
					if (list.Count > 0)
					{
						ExternalTool mergeTool = list.FirstItemStruct((ExternalTool x) => x.IsPrimary) ?? list[0];
						RepositoryUserControl.Commands.RunExternalMergeTool.Execute(RepositoryUserControl, changedFile.Path, mergeTool);
					}
				}
				else
				{
					List<ExternalTool> list2 = ExternalToolManager.RevealAvailableDiffTools().Filter((ExternalTool x) => x.IsVisible);
					if (!changedFile.IsDirectory && list2.Count > 0)
					{
						ExternalTool diffTool = list2.FirstItemStruct((ExternalTool x) => x.IsPrimary) ?? list2[0];
						RepositoryUserControl.Commands.RunExternalDiffTool.Execute(RepositoryUserControl, diffTarget, diffTool);
					}
				}
			}));
			StageFileUserControl.AddCommandBinding(Commands.ToggleFileStage.CreateShortcutCommandBinding(delegate
			{
				ToggleStageForSelectedFiles();
			}));
			StageFileUserControl.AddCommandBinding(Commands.DiscardChangedFilesCommand.CreateShortcutCommandBinding(delegate
			{
				DiscardSelectedFiles();
			}));
			StageFileUserControl.StageAll += delegate
			{
				StageAllFiles();
			};
			StageFileUserControl.UnstageAll += delegate
			{
				UnstageAllFiles();
			};
			StageFileUserControl.Stage += delegate
			{
				StageSelectedFiles();
			};
			StageFileUserControl.Unstage += delegate
			{
				UnstageSelectedFiles();
			};
			StageFileUserControl.UnstagedFilesContextMenuOpening += delegate(object s, ContextMenu contextMenu)
			{
				contextMenu.SetItems(CreateUnstagedFileListContextMenuItems());
				AddCodeAiReviewMenuItem(contextMenu, StageFileUserControl.ExpandedSelectedUnstagedFiles);
			};
			StageFileUserControl.StagedFilesContextMenuOpening += delegate(object s, ContextMenu contextMenu)
			{
				contextMenu.SetItems(CreateStagedFileListContextMenuItems());
				AddCodeAiReviewMenuItem(contextMenu, StageFileUserControl.ExpandedSelectedStagedFiles);
			};
			StageFileUserControl.FileListSettingsMenuOpened += delegate(object s, ContextMenu contextMenu)
			{
				UpdateWorkingDirectoryFileListSettingsDropdownMenuItems(contextMenu);
			};
			InitializeFileDiffControlHandlers(FileDiffControl);
			WeakEventManager<NotificationCenter, RepositoryDataUpdatedEventArgs>.AddHandler(NotificationCenter.Current, "RepositoryDataUpdated", RepositoryDataUpdated);
			WeakEventManager<NotificationCenter, EventArgs<RepositoryUserControl>>.AddHandler(NotificationCenter.Current, "RepositoryStatusUpdated", RepositoryStatusUpdated);
			WeakEventManager<NotificationCenter, EventArgs<ClosableTabItem>>.AddHandler(NotificationCenter.Current, "ActiveTabChanged", ActiveTabChanged);
			WeakEventManager<NotificationCenter, EventArgs<int>>.AddHandler(NotificationCenter.Current,"DiffContextSizeChanged",delegate
(object sender, global::System.EventArgs e)			{
				_updateDiffAction.ReinvokeNow();
			});
			WeakEventManager<NotificationCenter, EventArgs<bool>>.AddHandler(NotificationCenter.Current,"DiffIgnoreWhitespacesChanged",delegate
(object sender, global::System.EventArgs e)			{
				_updateDiffAction.ReinvokeNow();
			});
			WeakEventManager<NotificationCenter, EventArgs<bool>>.AddHandler(NotificationCenter.Current,"DiffShowEntireFileChanged",delegate
(object sender, global::System.EventArgs e)			{
				_updateDiffAction.ReinvokeNow();
			});
			WeakEventManager<NotificationCenter, EventArgs<int>>.AddHandler(NotificationCenter.Current,"CommitSubjectLowLimitChanged",delegate
(object sender, global::System.EventArgs e)			{
				RefreshSubjectLengthLimitToolTip();
				UpdateSubjectLengthLimit();
			});
			WeakEventManager<NotificationCenter, EventArgs<int>>.AddHandler(NotificationCenter.Current,"CommitSubjectHighLimitChanged",delegate
(object sender, global::System.EventArgs e)			{
				RefreshSubjectLengthLimitToolTip();
				UpdateSubjectLengthLimit();
			});
			WeakEventManager<NotificationCenter, EventArgs<bool>>.AddHandler(NotificationCenter.Current,"PushAutomaticallyOnCommitChanged",delegate
(object sender, global::System.EventArgs e)			{
				UpdateCommitButtonTitle();
			});
			CommitSubjectTextBox.AddPastingHandler(OnCommitSubjectPaste);
		}

		public void ApplyLocalization()
		{
			PreferencesLocalization.ApplyCurrent(this);
			RefreshSubjectLengthLimitToolTip();
			StageFileUserControl.ApplyLocalization();
			StageFileUserControl.RefreshStageAllButton();
			StageFileUserControl.RefreshStageButtons();
			if (GitModule != null)
			{
				StageFileUserControl.RefreshUnstagedStatusLabel(GitModule.Settings.HideUntrackedFiles, ShowIgnoredFiles);
			}
			UpdateCommitButtonTitle();
			UpdateCommitWarningMessage();
			FileDiffControl.ApplyLocalization();
		}

		public void Initialize(RepositoryUserControl repositoryUserControl)
		{
			RepositoryUserControl = repositoryUserControl;
			FileDiffControl.RepositoryUserControl = RepositoryUserControl;
			repositoryUserControl.KeyDown += delegate(object s, KeyEventArgs e)
			{
				if (e.Key == Key.LeftShift || e.Key == Key.RightShift)
				{
					OnShiftKeyDown();
				}
			};
			repositoryUserControl.KeyUp += delegate(object s, KeyEventArgs e)
			{
				if (e.Key == Key.LeftShift || e.Key == Key.RightShift)
				{
					OnShiftKeyUp();
				}
			};
		}

		public void FocusCommitMessageField()
		{
			CommitSubjectTextBox.Focus();
		}

		private void OnCommitSubjectPaste(object sender, DataObjectPastingEventArgs e)
		{
			if (e.DataObject.GetDataPresent(DataFormats.UnicodeText) && e.DataObject.GetData(DataFormats.UnicodeText) is string text)
			{
				e.CancelCommand();
				string text2 = FullCommitMessage;
				if (CommitSubjectTextBox.IsSelectionActive)
				{
					int selectionStart = CommitSubjectTextBox.SelectionStart;
					int selectionLength = CommitSubjectTextBox.SelectionLength;
					text2 = FullCommitMessage.Remove(selectionStart, selectionLength);
				}
				int num = Math.Min(text2.Length, CommitSubjectTextBox.CaretIndex);
				text2 = text2.Insert(num, text);
				int num2 = num + text.Length;
				string[] array = text2.Split(new string[1] { "\n" }, 2, StringSplitOptions.RemoveEmptyEntries);
				if (array.Length == 2)
				{
					int num3 = 0;
					num3 += array[0].Length;
					num3 += array[1].Length;
					CommitSubjectTextBox.Text = array[0].Trim();
					CommitDescriptionTextBox.Text = array[1].TrimStart();
					num3 -= CommitSubjectTextBox.Text.Length;
					num3 -= CommitDescriptionTextBox.Text.Length;
					num2 -= num3;
				}
				else
				{
					CommitSubjectTextBox.Text = text2.TrimStart().TrimEnd('\r', '\n');
				}
				if (num2 <= CommitSubjectTextBox.Text.Length)
				{
					CommitSubjectTextBox.CaretIndex = num2;
					return;
				}
				CommitDescriptionTextBox.CaretIndex = Math.Max(0, num2 - CommitSubjectTextBox.Text.Length - "\n".Length);
				CommitDescriptionTextBox.Focus();
			}
		}

		private void ToggleHideUntrackedFiles()
		{
			GitModule.Settings.HideUntrackedFiles = !GitModule.Settings.HideUntrackedFiles;
			GitModule.Settings.Save();
			if (GitModule.Settings.HideUntrackedFiles)
			{
				ShowIgnoredFiles = false;
			}
			RepositoryUserControl.InvalidateAndRefresh(SubDomain.Status, null, RepositoryViewMode.CommitViewMode);
			StageFileUserControl.RefreshUnstagedStatusLabel(GitModule.Settings.HideUntrackedFiles, ShowIgnoredFiles);
		}

		public void ToggleShowIgnoredFiles()
		{
			ShowIgnoredFiles = !ShowIgnoredFiles;
			if (ShowIgnoredFiles)
			{
				GitModule.Settings.HideUntrackedFiles = false;
				GitModule.Settings.Save();
			}
			RepositoryUserControl.InvalidateAndRefresh(SubDomain.Status, null, RepositoryViewMode.CommitViewMode);
			StageFileUserControl.RefreshUnstagedStatusLabel(GitModule.Settings.HideUntrackedFiles, ShowIgnoredFiles);
		}

		/// <summary>v3.8.0：切换"显示完整工作目录文件树"。开启后未变更文件也会出现在列表中（无状态图标）。
		/// 走 InvalidateAndRefresh 触发 RefreshRepositoryStatusUiAsync 重新装配数据，而非 RaiseFileListModeChanged（那个只切视图模板不重拉数据）。</summary>
		public void ToggleShowFullWorkingDirectory()
		{
			ForkPlusSettings.Default.ShowFullWorkingDirectory = !ForkPlusSettings.Default.ShowFullWorkingDirectory;
			ForkPlusSettings.Default.Save();
			RepositoryUserControl.InvalidateAndRefresh(SubDomain.Status, null, RepositoryViewMode.CommitViewMode);
		}

		private void StageAllFiles()
		{
			ClearStaleStageJob();
			if (StageJob == null)
			{
				// v3.8.0：未变更文件不参与 Stage All
				ChangedFile[] changedFiles = StageFileUserControl.ExpandedUnstagedFiles.Filter((ChangedFile x) => !x.IsDirectory && x.ChangeType != ChangeType.Unchanged).ToArray();
				if (changedFiles.Length == 0)
				{
					return;
				}
				if (!TestMergeConflictsResolved(changedFiles))
				{
					SystemSounds.Beep.Play();
				}
				else
				{
					Commands.ToggleAllFilesStageCommand.Execute(this, RepositoryUserControl, changedFiles, AmendMode);
				}
			}
		}

		private void UnstageAllFiles()
		{
			if (StageJob == null)
			{
				ChangedFile[] changedFiles = (StageFileUserControl.IsFiltered ? StageFileUserControl.ExpandedStagedFiles.Filter((ChangedFile x) => !x.IsDirectory).ToArray() : StageFileUserControl.ExpandedStagedFiles);
				Commands.ToggleAllFilesStageCommand.Execute(this, RepositoryUserControl, changedFiles, AmendMode);
			}
		}

		public void StageSelectedFiles()
	{
		ClearStaleStageJob();
		if (StageJob == null && StageFileUserControl.IsUnstagedListSelected)
		{
			// v3.8.0：未变更文件不参与 Stage
			ChangedFile[] selectedNonDirectoryFiles = StageFileUserControl.ExpandedSelectedUnstagedFiles.Filter((ChangedFile x) => !x.IsDirectory).ToArray();
			ChangedFile[] changedFiles = selectedNonDirectoryFiles.Filter((ChangedFile x) => x.ChangeType != ChangeType.Unchanged).ToArray();
			if (changedFiles.Length == 0)
			{
				// v3.10.2 自愈：选中的全是 Unchanged 条目（含"显示完整工作目录"树里的未变更节点）时不再静默返回。
				// 状态快照可能过期：文件实际已修改但列表还停留在旧的"未变更"条目上（点击无 diff、双击被过滤）。
				// 这里对选中的未变更文件按路径做一次后台状态刷新（git status -- <paths>）：
				//   - 若文件确已修改 → 刷新后列表恢复真实状态，用户再双击即可正常暂存；
				//   - 若确实未变更 → 无任何影响（Unchanged 条目本就不在 RepositoryStatus 里，不会误删）。
				ChangedFile[] staleCandidates = selectedNonDirectoryFiles.Filter((ChangedFile x) => x.Tracked && x.ChangeType == ChangeType.Unchanged).ToArray();
				if (staleCandidates.Length > 0)
				{
					new RefreshFileStatusCommand().Execute(this, RepositoryUserControl, staleCandidates.Map((ChangedFile x) => x.Path));
				}
				return;
			}
			if (!TestMergeConflictsResolved(changedFiles))
			{
				SystemSounds.Beep.Play();
			}
			else
			{
				Commands.ToggleFileStage.Execute(this, RepositoryUserControl, changedFiles, AmendMode);
			}
		}
	}

		private void UnstageSelectedFiles()
		{
			if (StageFileUserControl.IsStagedListSelected)
			{
				ChangedFile[] changedFiles = (StageFileUserControl.IsFiltered ? StageFileUserControl.ExpandedSelectedStagedFiles.Filter((ChangedFile x) => !x.IsDirectory).ToArray() : StageFileUserControl.ExpandedSelectedStagedFiles);
				Commands.ToggleFileStage.Execute(this, RepositoryUserControl, changedFiles, AmendMode);
			}
		}

		private void ToggleStageForSelectedFiles()
		{
			if (StageFileUserControl.IsStagedListSelected)
			{
				UnstageSelectedFiles();
			}
			else
			{
				StageSelectedFiles();
			}
		}

		private void DiscardSelectedFiles()
		{
			ClearStaleStageJob();
			if (StageJob == null && StageFileUserControl.IsUnstagedListSelected)
			{
				// v3.8.0：未变更文件不参与 Discard
				ChangedFile[] files = StageFileUserControl.ExpandedSelectedUnstagedFiles.Filter((ChangedFile x) => !x.IsDirectory && x.ChangeType != ChangeType.Unchanged).ToArray();
				if (files.Length > 0)
				{
					Commands.DiscardChangedFilesCommand.Execute(this, RepositoryUserControl, files);
				}
			}
		}

		private void SaveSelectedFilesAsPatch()
		{
			if (StageFileUserControl.IsUnstagedListSelected)
			{
				// v3.8.0：未变更文件不参与 SaveAsPatch
				ChangedFile[] files = StageFileUserControl.ExpandedSelectedUnstagedFiles.Filter((ChangedFile x) => !x.IsDirectory && x.ChangeType != ChangeType.Unchanged).ToArray();
				Commands.ShowSaveAsPatchDialogCommand.Execute(RepositoryUserControl, GitModule, files, AmendMode);
			}
			else
			{
				Commands.ShowSaveAsPatchDialogCommand.Execute(RepositoryUserControl, GitModule, StageFileUserControl.ExpandedSelectedStagedFiles, AmendMode);
			}
		}

		private void UpdateWorkingDirectoryFileListSettingsDropdownMenuItems(MenuBase menu)
		{
			menu.Items.Clear();
			FileListMode fileListMode = ForkPlusSettings.Default.FileListMode;
			MenuItem menuItem = new MenuItem();
			menuItem.Header = PreferencesLocalization.MenuHeader("View as Tree");
			menuItem.IsChecked = fileListMode == FileListMode.Tree;
			menuItem.Click += delegate
			{
				ForkPlusSettings.Default.FileListMode = FileListMode.Tree;
				ForkPlusSettings.Default.Save();
				NotificationCenter.Current.RaiseFileListModeChanged(this, FileListMode.Tree);
			};
			menu.Items.Add(menuItem);
			MenuItem menuItem2 = new MenuItem();
			menuItem2.Header = PreferencesLocalization.MenuHeader("View as List");
			menuItem2.IsChecked = fileListMode == FileListMode.List;
			menuItem2.Click += delegate
			{
				ForkPlusSettings.Default.FileListMode = FileListMode.List;
				ForkPlusSettings.Default.Save();
				NotificationCenter.Current.RaiseFileListModeChanged(this, FileListMode.List);
			};
			menu.Items.Add(menuItem2);
			MenuItem menuItem3 = new MenuItem();
			menuItem3.Header = PreferencesLocalization.MenuHeader("View as Combined List");
			menuItem3.IsChecked = fileListMode == FileListMode.CombinedList;
			menuItem3.Click += delegate
			{
				ForkPlusSettings.Default.FileListMode = FileListMode.CombinedList;
				ForkPlusSettings.Default.Save();
				NotificationCenter.Current.RaiseFileListModeChanged(this, FileListMode.CombinedList);
			};
			menu.Items.Add(menuItem3);

			menu.Items.Add(new Separator());

			// v3.8.0：显示完整工作目录文件树（含未变更文件）。与视图模式正交，默认关闭。
			// 开启后未变更文件出现在 unstaged 侧，无状态图标，且不参与 Stage/Discard/SaveAsPatch。
			MenuItem menuItem6 = new MenuItem();
			menuItem6.Header = PreferencesLocalization.MenuHeader("Show Full Working Directory");
			menuItem6.IsChecked = ForkPlusSettings.Default.ShowFullWorkingDirectory;
			menuItem6.Click += delegate
			{
				ToggleShowFullWorkingDirectory();
			};
			menu.Items.Add(menuItem6);

			menu.Items.Add(new Separator());
			MenuItem menuItem4 = new MenuItem();
			menuItem4.Header = PreferencesLocalization.MenuHeader("Hide Untracked Files");
			menuItem4.IsChecked = GitModule.Settings.HideUntrackedFiles;
			menuItem4.Click += delegate
			{
				ToggleHideUntrackedFiles();
			};
			menu.Items.Add(menuItem4);
			MenuItem menuItem5 = new MenuItem();
			menuItem5.Header = PreferencesLocalization.MenuHeader("Show Ignored Files");
			menuItem5.IsChecked = ShowIgnoredFiles;
			menuItem5.Click += delegate
			{
				ToggleShowIgnoredFiles();
			};
			menu.Items.Add(menuItem5);
		}

		private IEnumerable<Control> CreateUnstagedFileListContextMenuItems()
		{
			ChangedFile[] selectedFiles = StageFileUserControl.SelectedUnstagedFiles;
			ChangedFile selectedFile = selectedFiles.FirstItem();
			bool multipleFilesSelected = selectedFiles.Length > 1;
			bool isSubmodule = false;
			if (selectedFiles.Length == 1 && selectedFile is SubmoduleChangedFile submoduleChangedFile)
			{
				isSubmodule = true;
				yield return RepositoryUserControl.Commands.OpenSubmodule.CreateMenuItem(delegate
				{
					RepositoryUserControl.Commands.OpenSubmodule.Execute(RepositoryUserControl, GitModule, new Submodule[1] { submoduleChangedFile.Submodule });
				});
			}
			if (!isSubmodule)
			{
				bool isEditorAvailable = RepositoryUserControl.Commands.OpenFileInDefaultEditor.IsEditorAvailable(GitModule, selectedFile.Path);
				yield return RepositoryUserControl.Commands.OpenFileInDefaultEditor.CreateMenuItem(delegate
				{
					RepositoryUserControl.Commands.OpenFileInDefaultEditor.Execute(GitModule, selectedFile.Path);
				}, isEditorAvailable);
				if (!selectedFile.IsDirectory)
				{
					if (selectedFile.ChangeType == ChangeType.Unmerged)
					{
						List<ExternalTool> mergeTools = ExternalToolManager.RevealAvailableMergeTools().Filter((ExternalTool x) => x.IsVisible);
						if (mergeTools.Count > 0)
						{
							Control mergeToolMenuItem = CreateMergeToolContextMenuItems(RepositoryUserControl, mergeTools, selectedFile.Path);
							if (mergeToolMenuItem != null)
							{
								yield return mergeToolMenuItem;
							}
						}
					}
					else
					{
						List<ExternalTool> diffTools = ExternalToolManager.RevealAvailableDiffTools().Filter((ExternalTool x) => x.IsVisible);
						if (diffTools.Count > 0)
						{
							bool isEnabled = !multipleFilesSelected;
							RunExternalDiffToolCommand.DiffTarget.WorkingDirectory diffTarget = new RunExternalDiffToolCommand.DiffTarget.WorkingDirectory(selectedFile, AmendMode);
							Control diffToolMenuItem = CreateDiffToolContextMenuItems(RepositoryUserControl, diffTarget, diffTools, isEnabled);
							if (diffToolMenuItem != null)
							{
								yield return diffToolMenuItem;
							}
						}
					}
				}
			}
			yield return RepositoryUserControl.Commands.ShowFileInFileExplorer.CreateMenuItem(delegate
			{
				RepositoryUserControl.Commands.ShowFileInFileExplorer.Execute(GitModule, selectedFile.Path);
			});
			yield return new Separator();
			if (!isSubmodule)
			{
				bool isEnabled = !multipleFilesSelected && !selectedFile.IsDirectory;
				yield return RepositoryUserControl.Commands.ShowBlameWindow.CreateMenuItem(delegate
				{
					RepositoryUserControl.Commands.ShowBlameWindow.Execute(RepositoryUserControl, selectedFile.Path, null);
				}, isEnabled);
			}
			yield return RepositoryUserControl.Commands.ShowFileHistoryWindow.CreateMenuItem(delegate
			{
				RepositoryUserControl.Commands.ShowFileHistoryWindow.Execute(RepositoryUserControl, selectedFile.Mode(), null);
			}, !multipleFilesSelected);
			if (selectedFiles.ContainsItem((ChangedFile x) => x.ChangeType == ChangeType.Unmerged))
			{
				yield return new Separator();
				RepositoryState repositoryState = RepositoryUserControl.RepositoryStatus?.RepositoryState;
				IGitPoint localGitPoint = MergeConflictRepositoryStateHelper.GetLocalGitPoint(repositoryState);
				yield return Commands.ResolveConflictWithExistingVersion.CreateMenuItem("Resolve Using '" + localGitPoint.FriendlyName + "'", delegate
				{
					Commands.ResolveConflictWithExistingVersion.Execute(RepositoryUserControl, StageFileUserControl.ExpandedSelectedUnstagedFiles, UnmergedFileVersionType.Local);
				});
				IGitPoint remoteGitPoint = MergeConflictRepositoryStateHelper.GetRemoteGitPoint(repositoryState);
				yield return Commands.ResolveConflictWithExistingVersion.CreateMenuItem("Resolve Using '" + remoteGitPoint.FriendlyName + "'", delegate
				{
					Commands.ResolveConflictWithExistingVersion.Execute(RepositoryUserControl, StageFileUserControl.ExpandedSelectedUnstagedFiles, UnmergedFileVersionType.Remote);
				});
			}
			yield return new Separator();
		// v3.8.0：选中文件全是未变更文件时，Stage/Discard 菜单禁用（未变更文件不可 stage/discard）
		bool allSelectedUnchanged = selectedFiles.Length > 0;
		for (int i = 0; i < selectedFiles.Length; i++)
		{
			if (selectedFiles[i].ChangeType != ChangeType.Unchanged)
			{
				allSelectedUnchanged = false;
				break;
			}
		}
		yield return Commands.ToggleFileStage.CreateMenuItem("Stage", delegate
		{
			StageSelectedFiles();
		}, !allSelectedUnchanged);
		string header = isSubmodule ? "Discard submodule changes..." : "Discard changes...";
		yield return Commands.DiscardChangedFilesCommand.CreateMenuItem(header, delegate
		{
			DiscardSelectedFiles();
		}, !allSelectedUnchanged);
			if (!multipleFilesSelected)
			{
				yield return new Separator();
				yield return Commands.ToggleAllFilesStageCommand.CreateMenuItem("Stage All", delegate
				{
					StageAllFiles();
				});
			}
			if (!isSubmodule)
			{
				yield return new Separator();
				bool canIgnore = selectedFile.Path != ".gitignore" && selectedFile.Path != ".gitattributes";
				MenuItem ignoreMenuItem = Commands.ShowAddGitIgnorePatternWindowCommand.CreateMenuItem(null, canIgnore);
				ignoreMenuItem.Name = "Ignore";
				ignoreMenuItem.IsEnabled = canIgnore;
				if (canIgnore)
				{
					string fileName = Path.GetFileName(selectedFile.Path);
					if (selectedFile.IsDirectory)
					{
						ignoreMenuItem.Items.Add(Commands.ShowAddGitIgnorePatternWindowCommand.CreateMenuItem("Ignore all files in '" + fileName + "'...", delegate
						{
							Commands.ShowAddGitIgnorePatternWindowCommand.Execute(RepositoryUserControl, GitModule, PathHelper.NormalizeUnix(selectedFile.Path));
						}));
					}
					else
					{
						ignoreMenuItem.Items.Add(Commands.ShowAddGitIgnorePatternWindowCommand.CreateMenuItem("Ignore '" + fileName + "'", delegate
						{
							string pattern = PathHelper.NormalizeUnix(selectedFile.Path).Replace("[", "\\[").Replace("#", "\\#");
							GitCommandResult gitCommandResult = new IgnoreFilesGitCommand().Execute(GitModule, pattern, null);
							RepositoryUserControl.InvalidateAndRefresh(SubDomain.Status, null, RepositoryViewMode.CommitViewMode);
							if (!gitCommandResult.Succeeded)
							{
								new ErrorWindow(RepositoryUserControl, gitCommandResult.Error).ShowDialog();
							}
						}));
						string extension = Path.GetExtension(selectedFile.Path);
						ignoreMenuItem.Items.Add(Commands.ShowAddGitIgnorePatternWindowCommand.CreateMenuItem("Ignore All " + extension + " Files...", delegate
						{
							Commands.ShowAddGitIgnorePatternWindowCommand.Execute(RepositoryUserControl, GitModule, "*" + extension);
						}));
					}
					ignoreMenuItem.Items.Add(new Separator());
					ignoreMenuItem.Items.Add(Commands.ShowAddGitIgnorePatternWindowCommand.CreateMenuItem("Custom Pattern...", delegate
					{
						Commands.ShowAddGitIgnorePatternWindowCommand.Execute(RepositoryUserControl, GitModule, PathHelper.NormalizeUnix(selectedFile.Path));
					}));
				}
				yield return ignoreMenuItem;
				RepositoryData repositoryData = RepositoryUserControl.RepositoryData;
				if (repositoryData != null && repositoryData.GitLfsInitialized)
				{
					MenuItem lfsMenuItem = new MenuItem
					{
						Header = PreferencesLocalization.MenuHeader("LFS"),
						IsEnabled = !multipleFilesSelected
					};
					if (lfsMenuItem.IsEnabled)
					{
						InitializeLfsMenuSubmenuItems(RepositoryUserControl, lfsMenuItem, selectedFile, repositoryData.Remotes.HasLfsCompatibleRemotes());
					}
					yield return lfsMenuItem;
				}
				yield return new Separator();
				ChangedFile[] expandedSelectedFiles = StageFileUserControl.ExpandedSelectedUnstagedFiles;
				int uniqueFilesCount = GetUniqueFilesCount(expandedSelectedFiles);
				string itemType = expandedSelectedFiles.Length > 1 ? "Files" : "File";
				yield return Commands.ShowCreatePartialStashWindowCommand.CreateMenuItem($"Stash {uniqueFilesCount} {itemType}...", delegate
				{
					Commands.ShowCreatePartialStashWindowCommand.Execute(RepositoryUserControl, expandedSelectedFiles);
				});
				yield return Commands.ShowSaveAsPatchDialogCommand.CreateMenuItem(delegate
				{
					SaveSelectedFilesAsPatch();
				});
			}
			CustomCommand[] customCommands = CustomCommandManager.Current.GetCustomCommands(RepositoryUserControl.RepositoryData, CustomCommandTarget.RepositoryFile);
			CustomCommandEnvironment env = new CustomCommandEnvironment(GitModule, selectedFile.Path, null);
			if (selectedFile is SubmoduleChangedFile customCommandSubmoduleChangedFile)
			{
				customCommands = CustomCommandManager.Current.GetCustomCommands(RepositoryUserControl.RepositoryData, CustomCommandTarget.Submodule);
				CustomCommandEnvironment.Parameter[] parameters = new CustomCommandEnvironment.SubmoduleParameter[1]
				{
					new CustomCommandEnvironment.SubmoduleParameter(customCommandSubmoduleChangedFile.Submodule)
				};
				env = new CustomCommandEnvironment(GitModule, parameters);
			}
			if (customCommands.Length != 0)
			{
				yield return new Separator();
				List<MenuItem> menuItems = new List<MenuItem>();
				foreach (CustomCommand customCommand in customCommands)
				{
					if (customCommand.OS.IsSupported())
					{
						customCommand.AddCustomCommandItem(RepositoryUserControl, env, customCommand.Name.Split(Consts.Chars.Slash), 0, menuItems);
					}
				}
				foreach (MenuItem menuItem in menuItems)
				{
					yield return menuItem;
				}
			}
			yield return new Separator();
			yield return RepositoryUserControl.Commands.CopyFilePaths.CreateMenuItem(delegate
			{
				RepositoryUserControl.Commands.CopyFilePaths.Execute(selectedFiles.Map((ChangedFile x) => x.Path));
			});
			yield return RepositoryUserControl.Commands.CopyAbsoluteFilePaths.CreateMenuItem(delegate
			{
				RepositoryUserControl.Commands.CopyAbsoluteFilePaths.Execute(GitModule, selectedFiles.Map((ChangedFile x) => x.Path));
			});
		}

		[Null]
		private Control CreateMergeToolContextMenuItems(RepositoryUserControl repositoryUserControl, IReadOnlyList<ExternalTool> mergeTools, string filePath)
		{
			if (mergeTools.Count == 1)
			{
				ExternalTool mergeTool2 = mergeTools[0];
				return RepositoryUserControl.Commands.RunExternalMergeTool.CreateMenuItem("Merge in " + mergeTool2.Name, delegate
				{
					RepositoryUserControl.Commands.RunExternalMergeTool.Execute(repositoryUserControl, filePath, mergeTool2);
				});
			}
			if (mergeTools.Count > 1)
			{
				MenuItem menuItem = new MenuItem
				{
					Header = PreferencesLocalization.MenuHeader("External Merge")
				};
				{
					foreach (ExternalTool mergeTool in mergeTools)
					{
						MenuItem newItem = RepositoryUserControl.Commands.RunExternalMergeTool.CreateMenuItem(mergeTool.Name ?? "", delegate
						{
							RepositoryUserControl.Commands.RunExternalMergeTool.Execute(repositoryUserControl, filePath, mergeTool);
						}, isEnabled: true, null, mergeTool.IsPrimary);
						menuItem.Items.Add(newItem);
					}
					return menuItem;
				}
			}
			return null;
		}

		[Null]
		private Control CreateDiffToolContextMenuItems(RepositoryUserControl repositoryUserControl, RunExternalDiffToolCommand.DiffTarget diffTarget, IReadOnlyList<ExternalTool> diffTools, bool isEnabled)
		{
			if (diffTools.Count == 1)
			{
				ExternalTool diffTool2 = diffTools[0];
				return RepositoryUserControl.Commands.RunExternalDiffTool.CreateMenuItem("Diff in " + diffTool2.Name, delegate
				{
					RepositoryUserControl.Commands.RunExternalDiffTool.Execute(repositoryUserControl, diffTarget, diffTool2);
				}, isEnabled);
			}
			if (diffTools.Count > 1)
			{
				MenuItem menuItem = new MenuItem
				{
					Header = PreferencesLocalization.MenuHeader("External Diff"),
					IsEnabled = isEnabled
				};
				{
					foreach (ExternalTool diffTool in diffTools)
					{
						MenuItem newItem = RepositoryUserControl.Commands.RunExternalDiffTool.CreateMenuItem(diffTool.Name ?? "", delegate
						{
							RepositoryUserControl.Commands.RunExternalDiffTool.Execute(repositoryUserControl, diffTarget, diffTool);
						}, isEnabled: true, null, diffTool.IsPrimary);
						menuItem.Items.Add(newItem);
					}
					return menuItem;
				}
			}
			return null;
		}

		private IEnumerable<Control> CreateStagedFileListContextMenuItems()
		{
			ChangedFile[] selectedFiles = StageFileUserControl.SelectedStagedFiles;
			ChangedFile selectedFile = selectedFiles.FirstItem();
			bool multipleFilesSelected = selectedFiles.Length > 1;
			bool isSubmodule = false;
			if (selectedFiles.Length == 1 && selectedFile is SubmoduleChangedFile submoduleChangedFile)
			{
				isSubmodule = true;
				yield return RepositoryUserControl.Commands.OpenSubmodule.CreateMenuItem(delegate
				{
					RepositoryUserControl.Commands.OpenSubmodule.Execute(RepositoryUserControl, GitModule, new Submodule[1] { submoduleChangedFile.Submodule });
				});
			}
			if (!isSubmodule)
			{
				bool isEditorAvailable = RepositoryUserControl.Commands.OpenFileInDefaultEditor.IsEditorAvailable(GitModule, selectedFile.Path);
				yield return RepositoryUserControl.Commands.OpenFileInDefaultEditor.CreateMenuItem(delegate
				{
					RepositoryUserControl.Commands.OpenFileInDefaultEditor.Execute(GitModule, selectedFile.Path);
				}, isEditorAvailable);
				List<ExternalTool> diffTools = ExternalToolManager.RevealAvailableDiffTools().Filter((ExternalTool x) => x.IsVisible);
				if (diffTools.Count > 0)
				{
					bool isEnabled = !multipleFilesSelected && !selectedFile.IsDirectory;
					RunExternalDiffToolCommand.DiffTarget.WorkingDirectory diffTarget = new RunExternalDiffToolCommand.DiffTarget.WorkingDirectory(selectedFile, AmendMode);
					Control diffToolMenuItem = CreateDiffToolContextMenuItems(RepositoryUserControl, diffTarget, diffTools, isEnabled);
					if (diffToolMenuItem != null)
					{
						yield return diffToolMenuItem;
					}
				}
			}
			yield return RepositoryUserControl.Commands.ShowFileInFileExplorer.CreateMenuItem(delegate
			{
				RepositoryUserControl.Commands.ShowFileInFileExplorer.Execute(GitModule, selectedFile.Path);
			});
			yield return new Separator();
			if (!isSubmodule)
			{
				bool isEnabled = !multipleFilesSelected && !selectedFile.IsDirectory;
				yield return RepositoryUserControl.Commands.ShowBlameWindow.CreateMenuItem(delegate
				{
					RepositoryUserControl.Commands.ShowBlameWindow.Execute(RepositoryUserControl, selectedFile.Path, null);
				}, isEnabled);
			}
			yield return RepositoryUserControl.Commands.ShowFileHistoryWindow.CreateMenuItem(delegate
			{
				RepositoryUserControl.Commands.ShowFileHistoryWindow.Execute(RepositoryUserControl, selectedFile.Mode(), null);
			}, !multipleFilesSelected);
			yield return new Separator();
			yield return Commands.ToggleFileStage.CreateMenuItem("Unstage", delegate
			{
				UnstageSelectedFiles();
			});
			if (!(RepositoryUserControl.RepositoryStatus?.RepositoryState is RepositoryState.OK))
			{
				bool isEnabled = !multipleFilesSelected && !selectedFile.IsDirectory;
				yield return Commands.ShowResetFileToUnmergedStateWindow.CreateMenuItem(delegate
				{
					Commands.ShowResetFileToUnmergedStateWindow.Execute(RepositoryUserControl, GitModule, selectedFile);
				}, isEnabled);
			}
			if (!multipleFilesSelected)
			{
				yield return new Separator();
				yield return Commands.ToggleAllFilesStageCommand.CreateMenuItem("Unstage All", delegate
				{
					UnstageAllFiles();
				});
			}
			if (!isSubmodule)
			{
				RepositoryData repositoryData = RepositoryUserControl.RepositoryData;
				if (repositoryData != null && repositoryData.GitLfsInitialized)
				{
					yield return new Separator();
					MenuItem lfsMenuItem = new MenuItem
					{
						Header = PreferencesLocalization.MenuHeader("LFS"),
						IsEnabled = !multipleFilesSelected
					};
					if (lfsMenuItem.IsEnabled)
					{
						InitializeLfsMenuSubmenuItems(RepositoryUserControl, lfsMenuItem, selectedFile, repositoryData.Remotes.HasLfsCompatibleRemotes());
					}
					yield return lfsMenuItem;
				}
				yield return new Separator();
				ChangedFile[] expandedSelectedFiles = StageFileUserControl.ExpandedSelectedStagedFiles;
				int uniqueFilesCount = GetUniqueFilesCount(expandedSelectedFiles);
				string itemType = expandedSelectedFiles.Length > 1 ? "Files" : "File";
				yield return Commands.ShowCreatePartialStashWindowCommand.CreateMenuItem($"Stash {uniqueFilesCount} {itemType}...", delegate
				{
					Commands.ShowCreatePartialStashWindowCommand.Execute(RepositoryUserControl, expandedSelectedFiles);
				});
				yield return Commands.ShowSaveAsPatchDialogCommand.CreateMenuItem(delegate
				{
					SaveSelectedFilesAsPatch();
				});
			}
			CustomCommand[] customCommands = CustomCommandManager.Current.GetCustomCommands(RepositoryUserControl.RepositoryData, CustomCommandTarget.RepositoryFile);
			CustomCommandEnvironment env = new CustomCommandEnvironment(GitModule, selectedFile.Path, null);
			if (selectedFile is SubmoduleChangedFile customCommandSubmoduleChangedFile)
			{
				customCommands = CustomCommandManager.Current.GetCustomCommands(RepositoryUserControl.RepositoryData, CustomCommandTarget.Submodule);
				CustomCommandEnvironment.Parameter[] parameters = new CustomCommandEnvironment.SubmoduleParameter[1]
				{
					new CustomCommandEnvironment.SubmoduleParameter(customCommandSubmoduleChangedFile.Submodule)
				};
				env = new CustomCommandEnvironment(GitModule, parameters);
			}
			if (customCommands.Length != 0)
			{
				yield return new Separator();
				List<MenuItem> menuItems = new List<MenuItem>();
				foreach (CustomCommand customCommand in customCommands)
				{
					if (customCommand.OS.IsSupported())
					{
						customCommand.AddCustomCommandItem(RepositoryUserControl, env, customCommand.Name.Split(Consts.Chars.Slash), 0, menuItems);
					}
				}
				foreach (MenuItem menuItem in menuItems)
				{
					yield return menuItem;
				}
			}
			yield return new Separator();
			yield return RepositoryUserControl.Commands.CopyFilePaths.CreateMenuItem(delegate
			{
				RepositoryUserControl.Commands.CopyFilePaths.Execute(selectedFiles.Map((ChangedFile x) => x.Path));
			});
			yield return RepositoryUserControl.Commands.CopyAbsoluteFilePaths.CreateMenuItem(delegate
			{
				RepositoryUserControl.Commands.CopyAbsoluteFilePaths.Execute(GitModule, selectedFiles.Map((ChangedFile x) => x.Path));
			});
		}

		private static void InitializeLfsMenuSubmenuItems(RepositoryUserControl repositoryUserControl, MenuItem lfsMenuItem, ChangedFile changedFile, bool addLockItems)
		{
			GitModule gitModule = repositoryUserControl.GitModule;
			string path = PathHelper.NormalizeUnix(changedFile.Path).Replace("[", "\\[");
			string fileName = Path.GetFileName(path);
			if (changedFile.IsDirectory)
			{
				lfsMenuItem.Items.Add(RepositoryUserControl.Commands.ShowGitLfsTrackWindow.CreateMenuItem("Track All Files in '" + fileName + "'...", delegate
				{
					RepositoryUserControl.Commands.ShowGitLfsTrackWindow.Execute(repositoryUserControl, gitModule, path + "/**");
				}));
			}
			else
			{
				lfsMenuItem.Items.Add(RepositoryUserControl.Commands.ShowGitLfsTrackWindow.CreateMenuItem("Track '" + fileName + "'", delegate
				{
					GitCommandResult gitCommandResult = new AddGitLfsTrackPatternGitCommand().Execute(gitModule, new string[1] { path });
					repositoryUserControl.InvalidateAndRefresh(SubDomain.Status, null, RepositoryViewMode.CommitViewMode);
					if (!gitCommandResult.Succeeded)
					{
						new ErrorWindow(repositoryUserControl, gitCommandResult.Error).ShowDialog();
					}
				}));
				string extension = Path.GetExtension(changedFile.Path);
				string header = string.IsNullOrEmpty(extension) ? "Track All Files..." : "Track All " + extension + " Files...";
				lfsMenuItem.Items.Add(RepositoryUserControl.Commands.ShowGitLfsTrackWindow.CreateMenuItem(header, delegate
				{
					RepositoryUserControl.Commands.ShowGitLfsTrackWindow.Execute(repositoryUserControl, gitModule, "*" + extension);
				}));
			}
			lfsMenuItem.Items.Add(new Separator());
			lfsMenuItem.Items.Add(RepositoryUserControl.Commands.ShowGitLfsTrackWindow.CreateMenuItem("Custom Pattern...", delegate
			{
				RepositoryUserControl.Commands.ShowGitLfsTrackWindow.Execute(repositoryUserControl, gitModule, path);
			}));
			if (addLockItems && !changedFile.IsDirectory)
			{
				lfsMenuItem.Items.Add(new Separator());
				lfsMenuItem.Items.Add(RepositoryUserControl.Commands.GitLfsLockCommand.CreateMenuItem(delegate
				{
					RepositoryUserControl.Commands.GitLfsLockCommand.Execute(repositoryUserControl, new string[1] { path });
				}));
				lfsMenuItem.Items.Add(RepositoryUserControl.Commands.GitLfsUnlockCommand.CreateMenuItem(delegate
				{
					RepositoryUserControl.Commands.GitLfsUnlockCommand.Execute(repositoryUserControl, new string[1] { path });
				}));
			}
		}

		public void Refresh(SubDomain domainsToRefresh = SubDomain.Status)
		{
			RepositoryUserControl.InvalidateAndRefresh(domainsToRefresh, null, RepositoryViewMode.CommitViewMode);
			UpdateCommitSection();
		}

		public void RefreshSelectedFileDiff()
		{
			ChangedFile selectedFile = StageFileUserControl.SelectedUnstagedFiles.FirstItem() ?? StageFileUserControl.SelectedStagedFiles.FirstItem();
			if (selectedFile != null)
			{
				_updateDiffAction.InvokeNow(new ChangedFileArgs(selectedFile, loadLargeUntrackedFiles: false));
			}
		}

		public void SaveCommitMessage()
		{
			GitModule.Settings.DraftMessage = FullCommitMessage;
			GitModule.Settings.Save();
		}

		public void EraseSavedCommitMessage()
		{
			GitModule.Settings.DraftMessage = "";
			GitModule.Settings.Save();
		}

		private void ClearCommitMessageButton_Click(object sender, RoutedEventArgs e)
		{
			FullCommitMessage = "";
			EraseSavedCommitMessage();
			UpdateCommitButtonState();
			UpdateSubjectLengthLimit();
			UpdateCommitWarningMessage();
			CommitSubjectTextBox.Focus();
		}

		private void RepositoryDataUpdated(object sender, RepositoryDataUpdatedEventArgs args)
		{
			if (args.RepositoryUserControl == RepositoryUserControl)
			{
				_commitMessageAutocompleteProvider.UpdateUserIdentities(null);
			}
		}

		private void RepositoryStatusUpdated(object sender, EventArgs<RepositoryUserControl> args)
		{
			if (args.Value != RepositoryUserControl)
			{
				return;
			}
			if (!IsActiveRepository())
			{
				_pendingRepositoryStatusUiRefresh = true;
				return;
			}
			RefreshRepositoryStatusUi();
		}

		private void ActiveTabChanged(object sender, EventArgs<ClosableTabItem> args)
		{
			if (_pendingRepositoryStatusUiRefresh && IsActiveRepository())
			{
				_pendingRepositoryStatusUiRefresh = false;
				RefreshRepositoryStatusUi();
			}
		}

		private bool IsActiveRepository()
		{
			return MainWindow.Instance?.TabManager.ActiveRepositoryUserControl == RepositoryUserControl;
		}

		private async void RefreshRepositoryStatusUi()
		{
			try
			{
				if (RepositoryUserControl.RepositoryStatus == null)
				{
					return;
				}
				await RefreshRepositoryStatusUiAsync(showNestedWait: false);
			}
			catch (Exception ex)
			{
				Log.Error("RefreshRepositoryStatusUi failed", ex);
			}
		}

		private async Task RefreshRepositoryStatusUiAsync(bool showNestedWait)
		{
			if (RepositoryUserControl.RepositoryStatus == null)
			{
				return;
			}
			RefreshDescriptionFieldHeight();
			if (RepositoryUserControl.RepositoryStatus?.RepositoryState is RepositoryState.RebaseInProgress { AmendSha: var amendSha } rebaseInProgress)
			{
				if (amendSha != null && amendSha != _rebaseAmendSha)
				{
					AmendMode = true;
				}
				if (rebaseInProgress.AmendSha == null)
				{
					LoadCommitMessage();
				}
				_rebaseAmendSha = rebaseInProgress.AmendSha;
			}
			else
			{
				_rebaseAmendSha = null;
			}
			ChangedFile[] changedFiles = FilterGitMmManagedSubmoduleChanges(RepositoryUserControl.RepositoryStatus.ChangedFiles);
			// v3.8.0：开启"显示完整工作目录"时，把未变更的已跟踪文件合并进 unstaged 侧，ChangeType=Unchanged（无状态图标）。
			// Amend 模式下 staged 来自不同数据源，不启用此功能以避免混淆。
			bool showFullWorkingDirectory = ForkPlusSettings.Default.ShowFullWorkingDirectory && !AmendMode;
			ChangedFile[] unstagedFiles;
			ChangedFile[] stagedFiles;
			SplitByStaged(changedFiles, out unstagedFiles, out stagedFiles);
			if (AmendMode)
			{
				stagedFiles = FilterGitMmManagedSubmoduleChanges(new GetWorkingDirectoryChangedFilesGitCommand().ExecuteForAmend(GitModule, RepositoryUserControl.RepositoryData?.Submodules.Items).Result);
			}
			if (showFullWorkingDirectory)
			{
				unstagedFiles = MergeWithUnchangedFiles(unstagedFiles, stagedFiles);
			}
			_updateDiffAction.Cancel();
			FileDiffControl.Content = null;
			_diffPopupWindow?.UpdateDiff(null);
			_refreshing = true;
			try
			{
				await SetStageFileDataWithWaitIfNeededAsync(unstagedFiles, stagedFiles);
			}
			finally
			{
				_refreshing = false;
			}
			ChangedFile selectedFile = StageFileUserControl.SelectedUnstagedFiles.FirstItem() ?? StageFileUserControl.SelectedStagedFiles.FirstItem();
			if (selectedFile != null)
			{
				// v3.10.2：移除 5000 文件数阈值，大列表同样自动加载选中文件的 diff。
				_updateDiffAction.InvokeNow(new ChangedFileArgs(selectedFile, loadLargeUntrackedFiles: false));
			}
			UpdateCommitSection();
			_ = RepositoryUserControl.GitModule;
		}

		private async Task SetStageFileDataWithWaitIfNeededAsync(ChangedFile[] unstagedFiles, ChangedFile[] stagedFiles)
		{
			await StageFileUserControl.SetDataAsync(unstagedFiles, stagedFiles, selectFirstAvailableFile: true);
		}

		private ChangedFile[] FilterGitMmManagedSubmoduleChanges(ChangedFile[] changedFiles)
		{
			return RepositoryUserControl.NormalizeChangedFilesForDisplay(changedFiles);
		}

		/// <summary>v3.8.0：把未变更的已跟踪文件合并进 unstaged 列表。
		/// 调 git ls-files --cached -z 取全部已跟踪文件，与已有变更文件做差集，
		/// 把"未出现在变更列表中"的路径构造成 ChangeType.Unchanged 的 ChangedFile（无状态图标，不参与 Stage/Discard）。
		/// git 调用放后台线程避免阻塞 UI。</summary>
		private ChangedFile[] MergeWithUnchangedFiles(ChangedFile[] unstagedFiles, ChangedFile[] stagedFiles)
		{
			GitCommandResult<string[]> allFilesResult = Task.Run(() => new GetAllRepositoryFilesGitCommand().Execute(GitModule)).Result;
			if (!allFilesResult.Succeeded || allFilesResult.Result == null)
			{
				return unstagedFiles;
			}
			string[] allTrackedFiles = allFilesResult.Result;
			// 已有变更文件路径集合（用 Ordinal 忽略大小写，与 git 路径一致）
			// v3.10.2 修复：必须同时收集 unstaged 和 staged 两侧的路径。
			// 此前只收集 unstaged 侧，导致"仅已暂存"（如 "M " 状态）的文件不进差集排除名单，
			// 被当作"未变更文件"以 ChangeType.Unchanged 混入未暂存树：
			//   - 点击它 → staged=false → git diff（index vs 工作区）→ 文件已暂存且工作区干净 → 输出空 →"看不到修改内容"
			//   - 双击它 → StageSelectedFiles 过滤 Unchanged → 静默无反应 →"无法暂存"
			// 这正是"文件明明暂存成功了，却好像没暂存上"的根因。
			HashSet<string> changedPaths = new HashSet<string>(StringComparer.Ordinal);
			foreach (ChangedFile[] side in new ChangedFile[2][] { unstagedFiles, stagedFiles })
			{
				if (side == null)
				{
					continue;
				}
				foreach (ChangedFile cf in side)
				{
					if (!cf.IsDirectory && cf.Path != null)
					{
						changedPaths.Add(cf.Path);
					}
				}
			}
			// 构造未变更文件列表
			List<ChangedFile> unchanged = new List<ChangedFile>(allTrackedFiles.Length);
			foreach (string path in allTrackedFiles)
			{
				if (!changedPaths.Contains(path))
				{
					unchanged.Add(new ChangedFile(path, StatusType.None, StatusType.None, ChangeType.Unchanged, staged: false, isNew: false, tracked: true));
				}
			}
			if (unchanged.Count == 0)
			{
				return unstagedFiles;
			}
			// 合并：变更文件在前（用户更关心），未变更在后
			ChangedFile[] merged = new ChangedFile[(unstagedFiles?.Length ?? 0) + unchanged.Count];
			if (unstagedFiles != null && unstagedFiles.Length > 0)
			{
				Array.Copy(unstagedFiles, merged, unstagedFiles.Length);
			}
			unchanged.CopyTo(merged, unstagedFiles?.Length ?? 0);
			return merged;
		}

		private static void SplitByStaged(ChangedFile[] changedFiles, out ChangedFile[] unstagedFiles, out ChangedFile[] stagedFiles)
		{
			int unstagedCount = 0;
			for (int i = 0; i < changedFiles.Length; i++)
			{
				if (!changedFiles[i].Staged)
				{
					unstagedCount++;
				}
			}
			unstagedFiles = new ChangedFile[unstagedCount];
			stagedFiles = new ChangedFile[changedFiles.Length - unstagedCount];
			int unstagedIndex = 0;
			int stagedIndex = 0;
			for (int j = 0; j < changedFiles.Length; j++)
			{
				if (!changedFiles[j].Staged)
				{
					unstagedFiles[unstagedIndex++] = changedFiles[j];
				}
				else
				{
					stagedFiles[stagedIndex++] = changedFiles[j];
				}
			}
		}

		private void RefreshDescriptionFieldHeight()
		{
			CommitDescriptionTextBox.MaxHeight = MaxDescriptionHeight;
		}

		private void ExtractPatchAndApply(CommitCodeEditor editor, ExtractPatchType type)
		{
			Patch patch = editor.CreatePatchForSelection(type);
			if (patch != null)
			{
				if (type == ExtractPatchType.Discard)
				{
					new DiscardChunkCommand().Execute(this, RepositoryUserControl, patch, editor.IsNewOrUntracked);
				}
				else
				{
					new ApplyChunkCommand().Execute(this, RepositoryUserControl, patch, type == ExtractPatchType.Unstage, editor.IsNewOrUntracked);
				}
			}
		}

		private void InitializeButtonHandlers()
		{
			CommitButton.Click += delegate
			{
				Commands.Commit.Execute(this, CommitAndPush);
			};
		}

		private void InitializeKeyBindings()
		{
			this.AddCommandBinding(Commands.Commit.CreateShortcutCommandBinding(delegate
			{
				Commands.Commit.Execute(this, CommitAndPush);
			}));
			// v3.4.1：WIP 编排为提交绑定 Ctrl+Alt+Enter，避免和 Commit 的 Ctrl+Shift+Enter / Ctrl+Enter 重叠
			this.AddCommandBinding(Commands.ComposeWipCommit.CreateShortcutCommandBinding(delegate
			{
				OpenAiCommitComposer();
			}));
			this.AddCommandBinding(Commands.ToggleAllFilesStageCommand.CreateShortcutCommandBinding(delegate
			{
				if (StageFileUserControl.IsUnstagedListSelected)
				{
					ChangedFile[] array = (StageFileUserControl.IsFiltered ? StageFileUserControl.ExpandedUnstagedFiles.Filter((ChangedFile x) => !x.IsDirectory).ToArray() : StageFileUserControl.ExpandedUnstagedFiles);
					if (array.Length != 0)
					{
						Commands.ToggleAllFilesStageCommand.Execute(this, RepositoryUserControl, array, AmendMode);
						StageFileUserControl.RefreshStageAllButton();
					}
				}
				else if (StageFileUserControl.IsStagedListSelected)
				{
					ChangedFile[] array2 = (StageFileUserControl.IsFiltered ? StageFileUserControl.ExpandedStagedFiles.Filter((ChangedFile x) => !x.IsDirectory).ToArray() : StageFileUserControl.ExpandedStagedFiles);
					if (array2.Length != 0)
					{
						Commands.ToggleAllFilesStageCommand.Execute(this, RepositoryUserControl, array2, AmendMode);
						StageFileUserControl.RefreshStageAllButton();
					}
				}
			}));
		}

		private void RestoreGridColumnWidth()
		{
			double commitViewColumnWidth = ForkPlusSettings.Default.CommitViewColumnWidth;
			CommitGrid.ColumnDefinitions[0].Width = new GridLength(commitViewColumnWidth, GridUnitType.Pixel);
		}

		private void SaveGridColumnWidth()
		{
			double value = CommitGrid.ColumnDefinitions[0].Width.Value;
			ForkPlusSettings.Default.CommitViewColumnWidth = value;
			ForkPlusSettings.Default.Save();
		}

		private void StageFileUserControl_SelectionChanged(object sender, FileListEventArgs e)
		{
			ChangedFile selectedFile = e.SelectedFile;
			_updateDiffAction.InvokeNow(new ChangedFileArgs(selectedFile, loadLargeUntrackedFiles: false));
		}

		private void StageFileUserControl_StagedFilesItemSourceChanged(object sender, EventArgs e)
		{
			UpdateCommitSection();
			UpdateStagedDiffStats();
		}

		private async void UpdateStagedDiffStats()
		{
			try
			{
				int requestId = ++_stagedDiffStatsRequestId;
				bool amendMode = AmendMode;
				if ((!amendMode && StageFileUserControl.StagedItemsCount == 0) || GitModule == null)
				{
					ClearStagedDiffStats();
					return;
				}
				string repositoryPath = GitModule.Path;
				ClearStagedDiffStats();
				(int added, int deleted)? stats = await Task.Run(() => GetStagedDiffStats(repositoryPath, amendMode));
				if (requestId != _stagedDiffStatsRequestId)
				{
					return;
				}
				if (stats.HasValue)
				{
					StagedDiffAddedRun.Text = $"+{stats.Value.added}";
					StagedDiffDeletedRun.Text = $"-{stats.Value.deleted}";
				}
			}
			catch (Exception ex)
			{
				Log.Error("UpdateStagedDiffStats failed", ex);
			}
		}

		private void ClearStagedDiffStats()
		{
			StagedDiffAddedRun.Text = "";
			StagedDiffDeletedRun.Text = "";
		}

		[Null]
		private (int added, int deleted)? GetStagedDiffStats(string repositoryPath, bool amendMode)
		{
			// v3.10.2：与 status 命令完全对齐 fsmonitor/untrackedCache/checkStat/--no-optional-locks 四件套。
			// diff --cached 同样会问 fsmonitor daemon，daemon 漏报会导致统计为 0/+0 与实际 diff 内容不一致。
			GitCommand command = new GitCommand("-c", "core.fsmonitor=false", "-c", "core.untrackedCache=false", "-c", "core.checkStat=default", "--no-optional-locks", "diff", "--cached", "--numstat");
			if (amendMode)
			{
				command.Add("HEAD^");
			}
			GitRequestResult result = default(GitRequest)
				.CurrentDir(repositoryPath)
				.Command(command)
				.Execute(silent: true);
			if (!result.Success && amendMode)
			{
				result = default(GitRequest)
					.CurrentDir(repositoryPath)
					.Command(new GitCommand("-c", "core.fsmonitor=false", "-c", "core.untrackedCache=false", "-c", "core.checkStat=default", "--no-optional-locks", "diff", "--cached", "--numstat"))
					.Execute(silent: true);
			}
			if (!result.Success)
			{
				return null;
			}
			int added = 0;
			int deleted = 0;
			string[] lines = result.Stdout.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n');
			foreach (string line in lines)
			{
				if (string.IsNullOrWhiteSpace(line))
				{
					continue;
				}
				string[] parts = line.Split('\t');
				if (parts.Length < 2)
				{
					continue;
				}
				if (int.TryParse(parts[0], out int fileAdded))
				{
					added += fileAdded;
				}
				if (int.TryParse(parts[1], out int fileDeleted))
				{
					deleted += fileDeleted;
				}
			}
			return (added, deleted);
		}

		private void StageFileUserControl_ShowDiffPopupChanged(object sender, EventArgs e)
		{
			ShowDiffPopup();
		}

		private void AddCodeAiReviewMenuItem(ContextMenu contextMenu, ChangedFile[] selectedFiles)
		{
			if (selectedFiles == null || selectedFiles.Length == 0 || selectedFiles.AllItems((ChangedFile file) => file.IsDirectory || file is SubmoduleChangedFile))
			{
				return;
			}
			contextMenu.Items.Add(new Separator());
			MenuItem menuItem = new MenuItem
			{
				Header = PreferencesLocalization.MenuHeader("Code AI Review..."),
				IsEnabled = ForkPlus.Accounts.AiServices.OpenAiService.IsAiReviewConfigured()
			};
			menuItem.Click += delegate
			{
				ChangedFile[] files = selectedFiles.Filter((ChangedFile file) => !file.IsDirectory && !(file is SubmoduleChangedFile)).ToArray();
				if (files.Length == 0)
				{
					return;
				}
				RepositoryUserControl.Commands.ShowAiResultWindow.Execute(RepositoryUserControl, new AiCodeReviewTarget.Files(files, AmendMode));
			};
			contextMenu.Items.Add(menuItem);
		}

		private void ShowDiffPopup()
		{
			if (_diffPopupWindow == null)
			{
				_diffPopupWindow = CreateNewDiffPopupWindow();
				_diffPopupWindow.ShowAtCenter((global::Avalonia.Application.Current?.ApplicationLifetime as global::Avalonia.Controls.ApplicationLifetimes.IClassicDesktopStyleApplicationLifetime)?.MainWindow);
				_diffPopupWindow.UpdateDiff(FileDiffControl.Content);
			}
			else
			{
				_diffPopupWindow.UpdateDiff(FileDiffControl.Content);
				_diffPopupWindow.Focus();
			}
		}

		private DiffPopupWindow CreateNewDiffPopupWindow()
		{
			DiffPopupWindow diffPopupWindow = DiffPopupWindow.CreateCommitDiff(RepositoryUserControl);
			diffPopupWindow.Closed += delegate
			{
				_diffPopupWindow = null;
			};
			diffPopupWindow.SelectPrevious = (EventHandler)Delegate.Combine(diffPopupWindow.SelectPrevious, (EventHandler)delegate
			{
				StageFileUserControl.SelectPrevious();
			});
			diffPopupWindow.SelectNext = (EventHandler)Delegate.Combine(diffPopupWindow.SelectNext, (EventHandler)delegate
			{
				StageFileUserControl.SelectNext();
			});
			diffPopupWindow.ShowLargeUntrackedChanges = (EventHandler)Delegate.Combine(diffPopupWindow.ShowLargeUntrackedChanges, (EventHandler)delegate
			{
				ChangedFile changedFile = StageFileUserControl.SelectedUnstagedFiles.FirstItem();
				_updateDiffAction.InvokeNow(new ChangedFileArgs(changedFile, loadLargeUntrackedFiles: true));
			});
			InitializeFileDiffControlHandlers((CommitFileDiffControl)diffPopupWindow.FileDiffControl);
			return diffPopupWindow;
		}

		private void InitializeFileDiffControlHandlers(CommitFileDiffControl fileDiffControl)
		{
			fileDiffControl.ToggleStage += delegate(object s, CommitCodeEditor editor)
			{
				ExtractPatchType type = (StageFileUserControl.IsStagedListSelected ? ExtractPatchType.Unstage : ExtractPatchType.Stage);
				ExtractPatchAndApply(editor, type);
			};
			fileDiffControl.Stage += delegate(object s, CommitCodeEditor editor)
			{
				ExtractPatchAndApply(editor, ExtractPatchType.Stage);
			};
			fileDiffControl.UnStage += delegate(object s, CommitCodeEditor editor)
			{
				ExtractPatchAndApply(editor, ExtractPatchType.Unstage);
			};
			fileDiffControl.Discard += delegate(object s, CommitCodeEditor editor)
			{
				ExtractPatchAndApply(editor, ExtractPatchType.Discard);
			};
		}

		private void UpdateDiff(ChangedFileArgs args)
		{
			int requestId = ++_diffRequestId;
			if (args == null || args.ChangedFile == null || args.ChangedFile.IsDirectory)
			{
				FileDiffControl.Content = null;
				_diffPopupWindow?.UpdateDiff(null);
				return;
			}
			GitModule gitModule = GitModule;
			Task.Run(() => LoadWorkingDirectoryDiff(gitModule, args)).ContinueWith(delegate(Task<GitCommandResult<DiffContent>> getFileChangesTask)
			{
				Dispatcher.UIThread.Post(delegate
				{
					if (getFileChangesTask.IsCanceled || requestId != _diffRequestId)
					{
						return;
					}
					if (getFileChangesTask.IsFaulted)
					{
						GitCommandResult<DiffContent> failure = GitCommandResult<DiffContent>.Failure(getFileChangesTask.Exception);
						FileDiffControl.RepositoryUserControl = RepositoryUserControl;
						FileDiffControl.Content = failure;
						_diffPopupWindow?.UpdateDiff(failure);
						Log.Error("Load working directory diff failed", getFileChangesTask.Exception);
						return;
					}
					// git mm 下状态刷新频繁，SetDataAsync 重建列表后同一路径会是新 ChangedFile 实例。
					// 按 Path+Staged 匹配，避免引用不等导致刚加载好的 diff 被丢弃。
					ChangedFile target = args.ChangedFile;
					ChangedFile[] selectedFiles = target.Staged ? StageFileUserControl.SelectedStagedFiles : StageFileUserControl.SelectedUnstagedFiles;
					if (selectedFiles.ContainsItem((ChangedFile x) => x.Path == target.Path && x.Staged == target.Staged))
					{
						FileDiffControl.RepositoryUserControl = RepositoryUserControl;
						FileDiffControl.Content = getFileChangesTask.Result;
						_diffPopupWindow?.UpdateDiff(getFileChangesTask.Result);
					}
				});
			});
		}

		private GitCommandResult<DiffContent> LoadWorkingDirectoryDiff(GitModule gitModule, ChangedFileArgs args)
		{
			if (gitModule == null)
			{
				return GitCommandResult<DiffContent>.Failure(new GitCommandError.Bug("Git module is not available"));
			}
			ChangedFile changedFile = args.ChangedFile;
			GetWorkingDirectoryFileChangesGitCommand.WorkingDirectoryRevisionDiffTarget revisionTarget = null;
			if (changedFile.Staged && AmendMode)
			{
				revisionTarget = new GetWorkingDirectoryFileChangesGitCommand.WorkingDirectoryRevisionDiffTarget.Amend();
			}
			int tabWidth = gitModule.Settings?.TabWidth ?? 4;
			return new GetWorkingDirectoryFileChangesGitCommand().Execute(gitModule, changedFile, revisionTarget, ForkPlusSettings.Default.DiffContextSize, tabWidth, ForkPlusSettings.Default.DiffIgnoreWhitespaces, ForkPlusSettings.Default.DiffShowEntireFile, args.LoadLargeUntrackedFiles, resolvedConflict: false);
		}

		private GitRequestResult LoadRawWorkingDirectoryDiff(GitModule gitModule, ChangedFileArgs args)
		{
			ChangedFile changedFile = args.ChangedFile;
			if (changedFile.ChangeType == ChangeType.Unmerged)
			{
				return new GitRequestResult(0, "Unmerged file:\r\n" + changedFile.Path + "\r\n\r\nUse merge tools or conflict resolution commands to inspect this file.", "");
			}
			if (!changedFile.Tracked)
			{
				return LoadRawWorkingDirectoryDiff(gitModule, changedFile, staged: false, amend: false, includeNoIndex: true);
			}
			return LoadRawWorkingDirectoryDiff(gitModule, changedFile, changedFile.Staged, AmendMode && changedFile.Staged, includeNoIndex: false);
		}

		private GitRequestResult LoadRawWorkingDirectoryDiff(GitModule gitModule, ChangedFile changedFile, bool staged, bool amend, bool includeNoIndex)
		{
			// v3.10.2：与 status 命令完全对齐 fsmonitor/untrackedCache/checkStat/--no-optional-locks 四件套。
			GitCommand command;
			if (staged && amend)
			{
				command = new GitCommand("-c", "core.fsmonitor=false", "-c", "core.untrackedCache=false", "-c", "core.checkStat=default", "--no-optional-locks", "-c", "core.quotepath=false", "--no-pager", "diff-index", "--no-ext-diff", "--no-color", "--src-prefix=forkSrcPrefix/", "--dst-prefix=forkDstPrefix/", "--full-index", "--patch", "HEAD^", "--cached");
			}
			else
			{
				command = new GitCommand("-c", "core.fsmonitor=false", "-c", "core.untrackedCache=false", "-c", "core.checkStat=default", "--no-optional-locks", "-c", "core.quotepath=false", "--no-pager", "diff", "--find-renames", "--no-ext-diff", "--no-color", "--src-prefix=forkSrcPrefix/", "--dst-prefix=forkDstPrefix/", "--full-index");
				if (staged)
				{
					command.Add("--staged");
				}
			}
			if (ForkPlusSettings.Default.DiffIgnoreWhitespaces)
			{
				command.Add("--ignore-all-space");
			}
			if (ForkPlusSettings.Default.DiffShowEntireFile)
			{
				command.Add("--inter-hunk-context=1000000");
				command.Add("--unified=1000000");
			}
			else
			{
				command.Add("--unified=" + ForkPlusSettings.Default.DiffContextSize);
			}
			if (includeNoIndex)
			{
				command.Add("--no-index");
			}
			command.Add("--");
			if (includeNoIndex)
			{
				command.Add("/dev/null");
			}
			command.Add(changedFile.Path.Quotify());
			if (!string.IsNullOrEmpty(changedFile.OldPath))
			{
				command.Add(changedFile.OldPath.Quotify());
			}
			return new GitRequest(gitModule).Command(command).Execute(silent: true);
		}

		public void RefreshStageControls()
		{
			if (StageJob != null)
			{
				StageFileUserControl.Enabled = false;
				return;
			}
			StageFileUserControl.Enabled = true;
			StageFileUserControl.FocusActiveListView();
		}

		public void UpdateCommitSection(bool updateWarningMessage = true)
		{
			if (AreCommitFieldsAllowed)
			{
				CommitSubjectTextBox.IsEnabled = true;
				CommitDescriptionTextBox.IsEnabled = true;
				RecentCommitMessagesDropDownButton.IsEnabled = true;
			}
			else
			{
				CommitSubjectTextBox.IsEnabled = false;
				CommitDescriptionTextBox.IsEnabled = false;
				RecentCommitMessagesDropDownButton.IsEnabled = false;
			}
			CheckBoxAmend.IsEnabled = isAmendAllowed();
			UpdateCommitButtonTitle();
			UpdateCommitButtonState();
			if (updateWarningMessage)
			{
				UpdateCommitWarningMessage();
			}
		}

		private bool isAmendAllowed()
		{
			RepositoryState repositoryState = RepositoryUserControl.RepositoryStatus?.RepositoryState;
			if (repositoryState == null)
			{
				return false;
			}
			if (StageJob != null)
			{
				return false;
			}
			if (CommittingInProgress)
			{
				return false;
			}
			if (repositoryState is RepositoryState.RebaseInProgress rebaseInProgress)
			{
				return rebaseInProgress.AmendSha != null;
			}
			if (repositoryState is RepositoryState.AmInProgress)
			{
				return false;
			}
			return true;
		}

		private void UpdateCommitButtonTitle()
		{
			int stagedItemsCount = StageFileUserControl.StagedItemsCount;
			if (AmendMode)
			{
				CommitButton.Content = PreferencesLocalization.Current("Amend Last Commit");
				HideCommitButtonDropdown();
			}
			else
			{
				RepositoryStatus repositoryStatus = RepositoryUserControl.RepositoryStatus;
				if (repositoryStatus != null && repositoryStatus.RepositoryState is RepositoryState.RebaseInProgress rebaseInProgress && (rebaseInProgress.AmendSha == null || (rebaseInProgress.AmendSha != null && !AmendMode && !repositoryStatus.WorkingDirectoryIsDirty())))
				{
					CommitButton.Content = PreferencesLocalization.Current("Continue Rebase");
					HideCommitButtonDropdown();
				}
				else
				{
					RepositoryStatus repositoryStatus2 = RepositoryUserControl.RepositoryStatus;
					if (repositoryStatus2 != null && repositoryStatus2.RepositoryState is RepositoryState.SequencerInProgress && !AmendMode && !repositoryStatus2.WorkingDirectoryIsDirty())
					{
						CommitButton.Content = PreferencesLocalization.Current("Continue Cherry-Pick");
						HideCommitButtonDropdown();
					}
					else if (AmInProgress)
					{
						CommitButton.Content = PreferencesLocalization.Current("Continue");
					}
					else if (RepositoryUserControl.RepositoryStatus?.RepositoryState is RepositoryState.SquashInProgress)
					{
						CommitButton.Content = PreferencesLocalization.Current("Squash");
						HideCommitButtonDropdown();
					}
					else
					{
						switch (stagedItemsCount)
						{
						case 0:
							CommitButton.Content = PreferencesLocalization.Current("Commit");
							ShowCommitDropdown();
							break;
						case 1:
							CommitButton.Content = PreferencesLocalization.FormatCurrent("Commit {0} File", stagedItemsCount);
							ShowCommitDropdown();
							break;
						default:
							CommitButton.Content = PreferencesLocalization.FormatCurrent("Commit {0} Files", stagedItemsCount);
							ShowCommitDropdown();
							break;
						}
					}
				}
			}
			if (CommitAndPush)
			{
				CommitButton.Content = CommitButton.Content?.ToString() + " and Push";
				ShowCommitDropdown();
			}
		}

		private void ShowCommitDropdown()
		{
{			global::ForkPlus.UI.WpfCompat.StyleCompat.SetStyle(CommitButton, global::ForkPlus.UI.Theme.CommitUserControl.CommitButtonVisibleDropdownStyle);
}			CommitDropdownButton.Show();
		}

		private void HideCommitButtonDropdown()
		{
{			global::ForkPlus.UI.WpfCompat.StyleCompat.SetStyle(CommitButton, global::ForkPlus.UI.Theme.CommitUserControl.CommitButtonHiddenDropdownStyle);
}			CommitDropdownButton.Collapse();
		}

		private void UpdateCommitButtonState()
		{
			CommitButton.IsEnabled = IsCommitAllowed;
		}

		private void UpdateCommitWarningMessage()
		{
			GitModule gitModule = RepositoryUserControl?.GitModule;
			bool skipRegex = gitModule?.Settings?.SkipCommitMessage ?? false;
			if (!skipRegex)
			{
				string commitMessageRegex = gitModule?.Settings?.CommitMessageRegex;
				if (string.IsNullOrWhiteSpace(commitMessageRegex))
				{
					commitMessageRegex = ForkPlusSettings.Default.CommitMessageRegex;
				}
				if (!OpenAiService.MatchesCommitMessageRegex(FullCommitMessage, commitMessageRegex, out string commitRegexWarning))
				{
					ShowCommitWarning(commitRegexWarning, isError: true);
					return;
				}
			}
			if (IsCommitAllowed && !RebaseInProgress)
			{
				if (gitModule != null)
				{
					RepositoryData repositoryData = RepositoryUserControl.RepositoryData;
					if (repositoryData != null)
					{
						if (repositoryData.References.HeadSha.HasValue && repositoryData.References.ActiveBranch == null)
						{
							ShowCommitWarning(PreferencesLocalization.Current("Repository is in detached HEAD state"));
							return;
						}
						if (AmendMode)
						{
							LocalBranch activeBranch = repositoryData.References.ActiveBranch;
							if (activeBranch != null)
							{
								string upstreamFullReference = activeBranch.UpstreamFullReference;
								if (upstreamFullReference != null)
								{
									RemoteBranch remoteBranch = IReadOnlyListExtensions.FirstItem(repositoryData.References.RemoteBranches, (RemoteBranch x) => x.FullReference == upstreamFullReference);
									if (remoteBranch != null)
									{
										CommitGraphCache commitGraphCache = RepositoryUserControl.CommitGraphCache;
										if (commitGraphCache != null && !activeBranch.IsInfrontUpstream(remoteBranch, gitModule, commitGraphCache))
										{
											ShowCommitWarning(PreferencesLocalization.Current("Amending commit that has already been pushed"));
											return;
										}
									}
								}
							}
						}
					}
				}
			}
			WarningMessageContainer.Hide();
		}

		private void ShowCommitWarning(string message, bool isError = false)
		{
			WarningMessageContainer.Show();
			const int maxWarningLength = 60;
			if (message != null && message.Length > maxWarningLength)
			{
				string truncated = message.Substring(0, maxWarningLength) + "...";
				WarningTextBlock.Text = truncated;
				global::Avalonia.Controls.ToolTip.SetTip(WarningTextBlock,message);
				global::Avalonia.Controls.ToolTip.SetTip(WarningMessageContainer,message);
			}
			else
			{
				WarningTextBlock.Text = message;
				global::Avalonia.Controls.ToolTip.SetTip(WarningTextBlock,null);
				global::Avalonia.Controls.ToolTip.SetTip(WarningMessageContainer,null);
			}
			if (isError)
			{
				WarningTextBlock.Foreground = Application.Current.TryFindResource("Diff.Removed.Foreground") as Brush;
			}
			else
			{
				WarningTextBlock.ClearValue(TextBlock.ForegroundProperty);
			}
		}

		private void UpdateSubjectLengthLimit()
		{
			int commitSubjectLowLimit = ForkPlusSettings.Default.CommitSubjectLowLimit;
			int length = CommitSubjectTextBox.Text.Length;
			if (length == 0)
			{
				SubjectLengthLimitTextBlock.Hide();
				return;
			}
			if (length > ForkPlusSettings.Default.CommitSubjectHighLimit)
			{
				SubjectLengthLimitTextBlock.Foreground = Application.Current.TryFindResource("CommitSublectLength.Error.ForegroundBrush") as Brush;
			}
			else if (length > commitSubjectLowLimit)
			{
				SubjectLengthLimitTextBlock.Foreground = Application.Current.TryFindResource("CommitSublectLength.Warning.ForegroundBrush") as Brush;
			}
			else
			{
				SubjectLengthLimitTextBlock.Foreground = Application.Current.TryFindResource("CommitSublectLength.OK.ForegroundBrush") as Brush;
			}
			int num = commitSubjectLowLimit - length;
			SubjectLengthLimitTextBlock.Show();
			SubjectLengthLimitTextBlock.Text = num.ToString();
		}

		private bool TestMergeConflictsResolved(ChangedFile[] changedFiles)
		{
			foreach (ChangedFile changedFile in changedFiles)
			{
				if (changedFile.ChangeType != ChangeType.Unmerged)
				{
					continue;
				}
				GitCommandResult<DiffContent> fileContent = GetFileContent(changedFile, loadLargeUntrackedFiles: false);
				if (fileContent.Succeeded)
				{
					DiffContent result = fileContent.Result;
					if (result != null && !result.IsConflictResolved())
					{
						Log.Info(changedFile.Path + " contains unresolved conflicts and will not be staged");
						return false;
					}
				}
			}
			return true;
		}

		private GitCommandResult<DiffContent> GetFileContent(ChangedFile changedFile, bool loadLargeUntrackedFiles)
		{
			return new GetWorkingDirectoryFileChangesGitCommand().Execute(GitModule, changedFile, null, ForkPlusSettings.Default.DiffContextSize, GitModule.Settings.TabWidth, ForkPlusSettings.Default.DiffIgnoreWhitespaces, ForkPlusSettings.Default.DiffShowEntireFile, loadLargeUntrackedFiles, resolvedConflict: false);
		}

		private void AmendCheckBox_Changed(object sender, RoutedEventArgs e)
		{
			if (!DontRefreshOnAmend)
			{
				UpdateCommitMode();
				UpdateStagedDiffStats();
				Refresh();
			}
		}

		public void UpdateCommitMode()
		{
			if (AmendMode)
			{
				SaveCommitMessage();
				FullCommitMessage = new GetHeadMessageGitCommand().Execute(GitModule).Result ?? string.Empty;
			}
			else
			{
				LoadCommitMessage();
			}
			UpdateCommitSection();
		}

		private void RefreshSubjectLengthLimitToolTip()
		{
			global::Avalonia.Controls.ToolTip.SetTip(SubjectLengthLimitTextBlock,PreferencesLocalization.FormatCurrent("The recommended subject line should be {0} characters or less", ForkPlusSettings.Default.CommitSubjectLowLimit));
		}

		public void LoadCommitMessage()
		{
			GitCommandResult<string> gitCommandResult = new GetMergeCommitMessageGitCommand().Execute(GitModule);
			if (gitCommandResult.Succeeded)
			{
				FullCommitMessage = gitCommandResult.Result;
			}
			else
			{
				FullCommitMessage = GitModule.Settings.DraftMessage;
			}
		}

		private void CommitSubjectTextBox_TextChanged(object sender, TextChangedEventArgs e)
		{
			UpdateCommitButtonState();
			UpdateSubjectLengthLimit();
			UpdateCommitWarningMessage();
		}

		private void CommitDescriptionTextBox_TextChanged(object sender, TextChangedEventArgs e)
		{
			UpdateCommitButtonState();
			RefreshDescriptionFieldHeight();
			UpdateCommitWarningMessage();
		}

		private void CommitTextBox_GotFocus(object sender, RoutedEventArgs e)
		{
			string fullCommitMessage = FullCommitMessage;
			if (!string.IsNullOrEmpty(fullCommitMessage))
			{
				return;
			}
			GetCommitTemplate(delegate(bool success, string commitTemplate)
			{
				if (success && string.IsNullOrEmpty(fullCommitMessage))
				{
					FullCommitMessage = commitTemplate;
				}
			});
		}

		private void CommitTextBox_LostFocus(object sender, RoutedEventArgs e)
		{
			string fullCommitMessage = FullCommitMessage;
			if (string.IsNullOrEmpty(fullCommitMessage))
			{
				FullCommitMessage = "";
				SaveCommitMessage();
				return;
			}
			GetCommitTemplate(delegate(bool success, string commitTemplate)
			{
				if (!CommitDescriptionTextBox.IsFocused && !CommitSubjectTextBox.IsFocused && (string.IsNullOrEmpty(fullCommitMessage) || (success && fullCommitMessage == commitTemplate)))
				{
					FullCommitMessage = "";
				}
				SaveCommitMessage();
			});
		}

		private void GetCommitTemplate(Action<bool, string> callback)
		{
			GitModule gitModule = GitModule;
			if (gitModule == null)
			{
				callback(arg1: false, "");
				return;
			}
			RepositoryUserControl.JobQueue.Add(PreferencesLocalization.Current("Get Commit template"), delegate
			{
				GitCommandResult<CommitTemplate> commitTemplateResult = new GetCommitTemplateGitCommand().Execute(gitModule);
				base.Dispatcher.Post(delegate
				{
					callback(commitTemplateResult.Succeeded, commitTemplateResult.Result?.StringValue);
				});
			}, JobFlags.Hidden);
		}

		private void RecentCommitMessagesContextMenu_Opened(object sender, RoutedEventArgs e)
		{
			ContextMenu contextMenu = sender as ContextMenu;
			contextMenu.Items.Clear();
			AiAgent[] availableAiAgents = AiAgent.GetAvailableAiAgents();
			bool amend = AmendMode;
			AiAgent[] array = availableAiAgents;
			foreach (AiAgent aiAgent in array)
			{
				MenuItem menuItem = new MenuItem
				{
					Header = PreferencesLocalization.FormatCurrent("Generate commit message with {0}", aiAgent.Name)
				};
				menuItem.Click += delegate
				{
					RepositoryUserControl.JobQueue.Add(PreferencesLocalization.Current("Generate commit message"), delegate(JobMonitor monitor)
					{
						GitCommandResult<string> response2 = new GenerateCommitMessageShellCommand().Execute(aiAgent, GitModule.Path, amend, monitor);
					base.Dispatcher.Post(delegate
					{
						// Claude/AiAgent 的输出是缓冲到进程结束才一次性返回，期间用户可能已点取消。
						// 此处必须复查 IsCanceled，否则取消后已返回内容仍会被写入 commit 信息（与 OpenAI 路径一致）。
						if (monitor.IsCanceled)
						{
							return;
						}
						if (!response2.Succeeded)
						{
							new ErrorWindow(response2.Error.FriendlyDescription).ShowDialog();
						}
						else
						{
							FullCommitMessage = response2.Result;
							UpdateCommitButtonState();
							RefreshDescriptionFieldHeight();
						}
					});
					});
				};
				contextMenu.Items.Add(menuItem);
				contextMenu.Items.Add(new Separator());
			}
			if (OpenAiService.IsAiReviewConfigured())
			{
				MenuItem menuItem2 = new MenuItem
				{
					Header = PreferencesLocalization.FormatCurrent("Generate commit message with {0}", ForkPlusSettings.Default.AiReviewSelectedModel ?? "AI")
				};
				menuItem2.Click += delegate
				{
					RepositoryUserControl.JobQueue.Add(PreferencesLocalization.Current("Generate commit message"), delegate(JobMonitor monitor)
					{
						GitCommandResult<string> patchResult = new GetWorkingDirectoryFileChangesGitCommand().GetStagedPatch(GitModule, amend);
						if (!patchResult.Succeeded)
						{
							base.Dispatcher.Post(delegate
							{
								new ErrorWindow(RepositoryUserControl, patchResult.Error).ShowDialog();
							});
							return;
						}
						OpenAiService openAiService = OpenAiService.CreateFromAiReviewSettings();
					// 流式 onChunk 回调：AI 生成的每个 chunk 实时追加到 commit 框，
					// 用户无需等待整个请求完成才能看到内容。
					System.Text.StringBuilder liveCommitMsg = new System.Text.StringBuilder();
					ServiceResult<OpenAiResponse> response = openAiService.GenerateCommitMessage(patchResult.Result, GitModule, monitor, delegate(string chunk)
					{
						liveCommitMsg.Append(chunk);
						string snapshot = liveCommitMsg.ToString();
						base.Dispatcher.Post(delegate
						{
							FullCommitMessage = snapshot;
						});
					});
					base.Dispatcher.Post(delegate
					{
						if (monitor.IsCanceled)
						{
							return;
						}
						if (!response.Succeeded)
						{
							new ErrorWindow(response.Error.FriendlyMessage).ShowDialog();
						}
						else
						{
							// 用最终完整消息覆盖（可能经过 regex retry 修正）
							FullCommitMessage = response.Result.Message;
							UpdateCommitButtonState();
							RefreshDescriptionFieldHeight();
						}
					});
					});
				};
				contextMenu.Items.Add(menuItem2);
				contextMenu.Items.Add(new Separator());
			}
			if (File.Exists(GitModule.HookPath(PrepareCommitMsgHook)))
			{
				Log.Info("Run prepare-commit-msg hook");
				MenuItem menuItem3 = new MenuItem
				{
					Header = PreferencesLocalization.Current("Run prepare-commit-msg hook")
				};
				menuItem3.Click += delegate
				{
					RepositoryUserControl.JobQueue.Add(PreferencesLocalization.Current("Run prepare-commit-msg hook"), delegate(JobMonitor monitor)
					{
						GitCommandResult<string> messageResult = new RunHookShellCommand().Execute(GitModule, PrepareCommitMsgHook, monitor);
					base.Dispatcher.Post(delegate
					{
						if (monitor.IsCanceled)
						{
							return;
						}
						if (!messageResult.Succeeded)
						{
							new ErrorWindow(RepositoryUserControl, messageResult.Error).ShowDialog();
						}
						else
						{
							FullCommitMessage = messageResult.Result.TrimEnd(Consts.Chars.NewLines);
						}
					});
					});
				};
				contextMenu.Items.Add(menuItem3);
				contextMenu.Items.Add(new Separator());
			}
			LocalBranch activeBranch = RepositoryUserControl.RepositoryData?.References.ActiveBranch;
			string[] result = new GetRecentRevisionMessagesGitCommand().Execute(GitModule, activeBranch).Result;
			if (result == null)
			{
				return;
			}
			contextMenu.Items.Add(new MenuItem
			{
				Header = PreferencesLocalization.MenuHeader("Recent Commit Messages"),
				IsEnabled = false
			});
			contextMenu.Items.Add(new Separator());
			string[] array2 = result;
			foreach (string fullMessage in array2)
			{
				SplitCommitMessageForFields(fullMessage, out var subject, out var description);
				string text = subject + (string.IsNullOrEmpty(description) ? "" : "...");
				MenuItem menuItem4 = new MenuItem
				{
					Header = text.Replace("_", "__")
				};
				menuItem4.Click += delegate
				{
					SetRecentCommitMessage(fullMessage);
				};
				contextMenu.Items.Add(menuItem4);
			}
		}

		private void SetRecentCommitMessage(string fullMessage)
		{
			SplitCommitMessageForFields(fullMessage, out var subject, out var description);
			CommitDescriptionTextBox.DisableUpdates = true;
			// 同步关闭 subject 框的自动补全：最近消息回填时若 subject 含 ":" 会误触发 Gitmoji 选择器
			CommitSubjectTextBox.DisableUpdates = true;
			CommitSubjectTextBox.Text = subject;
			CommitSubjectTextBox.DisableUpdates = false;
			CommitDescriptionTextBox.Text = description;
			CommitDescriptionTextBox.DisableUpdates = false;
			RefreshDescriptionFieldHeight();
			UpdateCommitButtonState();
		}

		private static void SplitCommitMessageForFields(string fullMessage, out string subject, out string description)
		{
			string text = (fullMessage ?? string.Empty).Replace("\r\n", "\n").Replace('\r', '\n').TrimEnd(Consts.Chars.NewLines);
			int lineBreakIndex = text.IndexOf('\n');
			if (lineBreakIndex < 0)
			{
				subject = text.Trim(Consts.Chars.NewLines);
				description = string.Empty;
				return;
			}
			subject = text.Substring(0, lineBreakIndex).Trim(Consts.Chars.NewLines);
			description = text.Substring(lineBreakIndex + 1).TrimStart(Consts.Chars.NewLines);
		}

		private void CommitSubjectTextBox_PreviewKeyDown(object sender, KeyEventArgs e)
		{
			if (e.Key == Key.Return && !KeyboardHelper.IsCtrlDown)
			{
				CommitDescriptionTextBox.Focus();
				e.Handled = true;
			}
			else if (e.Key == Key.Up || e.Key == Key.Down)
			{
				RecentCommitMessagesDropDownButton.IsChecked = true;
			}
		}

		private void OnShiftKeyDown()
		{
			if (CommitSubjectTextBox.IsFocused)
			{
				int num = CommitSubjectTextBox.CaretIndex - 1;
				if (num >= 0 && CommitSubjectTextBox.Text[num] == ' ')
				{
					return;
				}
			}
			if (CommitDescriptionTextBox.IsFocused)
			{
				int num2 = CommitDescriptionTextBox.CaretIndex - 1;
				if (num2 >= 0 && CommitDescriptionTextBox.Text[num2] == ' ')
				{
					return;
				}
			}
			UpdateCommitButtonTitle();
			StageFileUserControl.RefreshStageButtons();
		}

		private void OnShiftKeyUp()
		{
			UpdateCommitButtonTitle();
			StageFileUserControl.RefreshStageButtons();
		}

		private void CommitDescriptionTextBox_PreviewKeyDown(object sender, KeyEventArgs e)
		{
			if (e.Key == Key.Back && CommitDescriptionTextBox.CaretIndex == 0 && CommitDescriptionTextBox.SelectionLength == 0)
			{
				CommitSubjectTextBox.Focus();
				CommitSubjectTextBox.CaretIndex = CommitSubjectTextBox.Text.Length;
				e.Handled = true;
			}
		}

		private void CommitDropdownButtonContextMenu_Opened(object sender, RoutedEventArgs e)
		{
			ContextMenu obj = sender as ContextMenu;
			obj.Items.Clear();
			obj.Items.Add(new HeaderMenuItem("Commit Options"));
			obj.Items.Add(new Separator());
			bool commitAndPushEnabled = ForkPlusSettings.Default.PushAutomaticallyOnCommit;
			string header = ((!commitAndPushEnabled) ? "Commit and Push" : "Commit");
			obj.Items.Add(Commands.Commit.CreateMenuItem(header, delegate
			{
				Commands.Commit.Execute(this, !commitAndPushEnabled);
			}));
			// AI Commit Composer (WIP拆分)：仅在 AI 配置完毕时显示
			if (OpenAiService.IsAiReviewConfigured())
			{
				obj.Items.Add(new Separator());
				// v3.4.1：用独立的 ComposeWipCommit 命令，菜单显示正确的 Ctrl+Alt+Enter 快捷键
				obj.Items.Add(Commands.ComposeWipCommit.CreateMenuItem(PreferencesLocalization.Current("Compose WIP into commits..."), delegate
				{
					OpenAiCommitComposer();
				}));
			}
		}

		/// <summary>AI Commit Composer (WIP拆分) 入口：把当前 staged 区的多个变更交给 AI 拆分成多个有逻辑意义的提交。
		/// 弹出 AiCommitComposerWindow 让用户预览/编辑分组后再一键应用。</summary>
		private void OpenAiCommitComposer()
		{
			ChangedFile[] stagedFiles = StageFileUserControl.ExpandedStagedFiles;
			if (stagedFiles == null || stagedFiles.Length == 0)
			{
				new ErrorWindow(PreferencesLocalization.Current("No staged files to compose. Stage some files first.")).ShowDialog();
				return;
			}
			// 至少 2 个 staged 文件才有拆分意义；只有 1 个时也允许（让 AI 生成单条 message）
			AiCommitComposerWindow window = new AiCommitComposerWindow(GitModule, stagedFiles, AmendMode);
			window.SetOwnerCompat(MainWindow.Instance); // Migration note：WPF { Owner=.. }（非模态 Show）→ SetOwnerCompat。
			window.ShowAtOwnerScreen(); // 修复链 23：跟随主窗口所在屏幕
		}

		private static int GetUniqueFilesCount(ChangedFile[] changedFiles)
		{
			HashSet<string> hashSet = new HashSet<string>();
			foreach (ChangedFile changedFile in changedFiles)
			{
				hashSet.Add(changedFile.Path);
			}
			return hashSet.Count;
		}

		private static Range? RangeOfAny(string target, string string1, string string2)
		{
			int num = target.IndexOf(string1);
			int num2 = target.IndexOf(string2);
			if (num != -1)
			{
				if (num2 != -1)
				{
					if (num < num2)
					{
						return new Range(num, num + string1.Length);
					}
					return new Range(num2, num2 + string2.Length);
				}
				return new Range(num, num + string1.Length);
			}
			if (num2 != -1)
			{
				return new Range(num2, num2 + string2.Length);
			}
			return null;
		}

	}
}

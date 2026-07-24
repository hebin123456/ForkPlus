using Avalonia.Controls.Selection;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Markdown.Avalonia;
using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Linq;
using ForkPlus.Accounts;
using ForkPlus.Accounts.AiServices;
using ForkPlus.Biturbo;
using ForkPlus.Git;
using ForkPlus.Git.Commands;
using ForkPlus.Git.Commands.LeanBranching;
using ForkPlus.Git.Diff;
using ForkPlus.Git.Interaction;
using ForkPlus.Jobs;
using ForkPlus.Services;
using ForkPlus.Settings;
using ForkPlus.UI.UserControls;
using ForkPlus.UI.UserControls.Preferences;
using ForkPlus.Utils.Http;
using Newtonsoft.Json.Linq;
using ForkPlus.UI.Helpers;

namespace ForkPlus.UI.Dialogs
{
	public partial class AiCodeReviewWindow : CustomWindow, ILocalizableControl
	{
		private class AiReviewSuggestion
		{
			public string File { get; set; }

			public int Line { get; set; }

			public string Comment { get; set; }

			public string OldText { get; set; }

			public string NewText { get; set; }
		}

		private RepositoryUserControl _repositoryUserControl;

		private bool _startUpFinished;

		private AiCodeReviewTarget _target;

		private AiAgent _aiAgent;

		private Job _aiReviewJob;

		private Job _fileReviewDiffJob;

		private bool _isClosed;

		private AiCodeReviewTarget.Files _fileReviewTarget;

		private List<AiReviewSuggestion> _suggestions = new List<AiReviewSuggestion>();

		private string _aiReviewMarkdown;

		private string _selectedFileReviewPath;

		private string _aiReviewStatusMessage;

		// 文件级 Markdown 缓存（原 _fileReviewHtmlCache，阶段 4.7-c-4 改为缓存 Markdown 而非 HTML）
		private readonly Dictionary<string, string> _fileReviewMarkdownCache = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

		private const int AiResultColumn = 2;

		private const int FileReviewTreeColumn = 0;

		// 流式渲染状态/节流协议由 VM 承载（零 WPF），本类仅负责 MarkdownScrollViewer 实例操作 + UI 切换。
		// 阶段 4.7-c-4：WebView2 + scroll-at-bottom JS 互操作 + 建议卡按钮 JS 回调 →
		//   MarkdownScrollViewer + 原生 ScrollViewer.ScrollChanged + 原生 Button.Click。
		private readonly AiCodeReviewWindowViewModel _viewModel = new AiCodeReviewWindowViewModel();

		public AiCodeReviewWindow()
		{
			base.ShowInTaskbar = true;
			base.WindowStartupLocation = WindowStartupLocation.CenterScreen;
			InitializeComponent();
			FileReviewFileListUserControl.SelectionChanged += FileReviewFileListUserControl_SelectionChanged;
			FileReviewGrid.SizeChanged += FileReviewGrid_SizeChanged;
			if (global::ForkPlus.DesignTimeHelper.IsInDesignMode())
			{
				base.Title = PreferencesLocalization.Current("AI Code Review");
				TitleTextBlock.Text = PreferencesLocalization.Current("AI Code Review");
			}
		}

		public AiCodeReviewWindow(RepositoryUserControl repositoryUserControl, AiCodeReviewTarget target, [Null] AiAgent aiAgent)
			: this()
		{
			AiCodeReviewWindow aiCodeReviewWindow = this;
			if (global::ForkPlus.DesignTimeHelper.IsInDesignMode())
			{
				return;
			}
			_repositoryUserControl = repositoryUserControl;
			_target = target;
			_aiAgent = aiAgent;
			ApplyLocalization();
			base.Loaded += delegate
			{
				aiCodeReviewWindow.AttachScrollTracker();
			};
			base.SizeChanged += Window_SizeChanged;
			base.Activated += Window_Activated;
			RetryAiReview(_target, replaceAll: true);
			RestoreAiResultColumnWidth();
			if (target is AiCodeReviewTarget.Files fileTarget)
			{
				RevisionDetails.Collapse();
				FileReviewGrid.Show();
				_fileReviewTarget = fileTarget;
				FileReviewDiffControl.RepositoryUserControl = repositoryUserControl;
				InitializeFileReviewList(fileTarget);
			}
			else
			{
				RevisionDetails.Initialize(repositoryUserControl, RevisionDetailsUserControlMode.AiReview);
				RevisionDetails.Loaded += delegate
				{
					if (target is AiCodeReviewTarget.Branch branch2)
					{
						aiCodeReviewWindow.RevisionDetails.ShowRevisionDetails(new RevisionDiffTarget.Range(branch2.Dst, branch2.Src));
					}
					else if (target is AiCodeReviewTarget.ShaRange shaRange2)
					{
						aiCodeReviewWindow.RevisionDetails.ShowRevisionDetails(new RevisionDiffTarget.Range(shaRange2.Dst, shaRange2.Src));
					}
				};
				RevisionDetails.RevisionDetailsUpdated += delegate(object s, RevisionDetails e)
				{
					aiCodeReviewWindow.RefreshTitle(e);
				};
			}
			GridSplitter.DragCompleted += delegate
			{
				aiCodeReviewWindow.SaveAiResultColumnWidth();
			};
			FileReviewGridSplitter.DragCompleted += delegate
			{
				aiCodeReviewWindow.SaveFileReviewTreeColumnWidth();
			};
			// 原 WebView2 需订阅 ApplicationThemeChanged 更新 PreferredColorScheme；
			// MarkdownScrollViewer 是原生 Avalonia 控件，主题由 Avalonia 主题系统自动应用，无需手动更新。
			// 初始化模型下拉选择（后台拉取模型列表）
			InitializeModelComboBox();
		}

		public void ApplyLocalization()
		{
			PreferencesLocalization.Apply(this, ForkPlusSettings.Default.UiLanguage);
			RetryButton.Content = PreferencesLocalization.Current("Retry");
			RetryButton.ToolTip = PreferencesLocalization.Current("Retry AI Review");
			StopButton.Content = PreferencesLocalization.Current("Stop");
			StopButton.ToolTip = PreferencesLocalization.Current("Stop the current AI task and abort its request");
			ApplyTargetTitleLocalization();
			RevisionDetails.ApplyLocalization();
			if (FileReviewDiffControl is ILocalizableControl localizableDiffControl)
			{
				localizableDiffControl.ApplyLocalization();
			}
			if (AiResponseWebView != null && !string.IsNullOrWhiteSpace(_aiReviewMarkdown))
			{
				_fileReviewMarkdownCache.Clear();
				RenderAiReviewOutput();
			}
		}

		private void ApplyTargetTitleLocalization()
		{
			if (_target is AiCodeReviewTarget.Branch branch)
			{
				base.Title = PreferencesLocalization.FormatCurrent("{0} - {1} Review", branch.Name, ReviewProviderName(_aiAgent));
				LocalBranch localBranch = _repositoryUserControl?.RepositoryData?.References.LocalMain(_repositoryUserControl.GitModule);
				RemoteBranch remoteBranch = _repositoryUserControl?.RepositoryData?.References.Upstream(localBranch);
				TitleTextBlock.Text = PreferencesLocalization.FormatCurrent("Code review for {0}...{1}", branch.Name, remoteBranch?.Name ?? "");
			}
			else if (_target is AiCodeReviewTarget.ShaRange shaRange)
			{
				base.Title = PreferencesLocalization.FormatCurrent("{0} - {1} Review", shaRange.Dst.ToAbbreviatedString(), ReviewProviderName(_aiAgent));
				TitleTextBlock.Text = PreferencesLocalization.FormatCurrent("Code review for {0}..{1}", shaRange.Src.ToAbbreviatedString(), shaRange.Dst.ToAbbreviatedString());
			}
			else if (_target is AiCodeReviewTarget.Files files)
			{
				base.Title = PreferencesLocalization.FormatCurrent("{0} files - {1} Review", files.ChangedFiles.Length, ReviewProviderName(_aiAgent));
				TitleTextBlock.Text = PreferencesLocalization.FormatCurrent("Code review for {0} files", files.ChangedFiles.Length);
			}
			else
			{
				base.Title = PreferencesLocalization.Current("AI Code Review");
				TitleTextBlock.Text = PreferencesLocalization.Current("AI Code Review");
			}
		}

		/// <summary>订阅 AiResponseScrollViewer 的 ScrollChanged 事件，跟踪用户滚动位置（是否在底部）。
		/// 原 WebView2 通过 JS postMessage 上报 scroll-at-bottom + NavigationCompleted 事件执行 scrollTo；
		/// MarkdownScrollViewer 是原生 Avalonia 控件，直接用 ScrollViewer.ScrollChanged 即可。</summary>
		private void AttachScrollTracker()
		{
			try
			{
				AiResponseScrollViewer.ScrollChanged += InnerScrollViewer_ScrollChanged;
			}
			catch (Exception ex)
			{
				Log.Warn("AiCodeReviewWindow scroll tracker attach failed: " + ex.Message);
			}
		}

		/// <summary>ScrollChanged 事件处理：计算用户是否在底部，更新 VM 状态。
		/// 原 WebView2 通过 JS postMessage('scroll-at-bottom:1/0') 上报；这里直接用 Avalonia 原生事件。
		/// 判定：Offset.Y + Viewport.Height >= Extent.Height - 80（容差，与原 JS 脚本一致）。</summary>
		private void InnerScrollViewer_ScrollChanged(object sender, ScrollChangedEventArgs e)
		{
			double offset = AiResponseScrollViewer.Offset.Y;
			double viewport = AiResponseScrollViewer.Viewport.Height;
			double extent = AiResponseScrollViewer.Extent.Height;
			bool atBottom = viewport <= 0 || (offset + viewport >= extent - 80);
			_viewModel.SetUserAtBottom(atBottom);
		}

		/// <summary>滚动 AiResponseScrollViewer 到底。
		/// 原 WebView2 用 ExecuteScriptAsync("window.scrollTo(...)")；这里用原生 ScrollViewer.ScrollToEnd()。</summary>
		private void ScrollInnerViewerToEnd()
		{
			AiResponseScrollViewer.ScrollToEnd();
		}

		protected override void OnSourceInitialized(EventArgs e)
		{
			base.OnSourceInitialized(e);
			if (global::ForkPlus.DesignTimeHelper.IsInDesignMode())
			{
				return;
			}
			this.SetWindowLocationState(ForkPlusSettings.Default.AiResultWindowLocationState);
			if (Application.Current?.MainWindow?.WindowState == WindowState.Maximized)
			{
				WindowState = WindowState.Maximized;
			}
		}

		protected override void OnLocationChanged(EventArgs e)
		{
			base.OnLocationChanged(e);
			if (_startUpFinished)
			{
				ForkPlusSettings.Default.AiResultWindowLocationState = this.GetWindowLocationState();
			}
		}

		protected override void OnKeyDown(KeyEventArgs e)
		{
			if (e.Key == Key.Escape)
			{
				Close();
			}
			else
			{
				base.OnKeyDown(e);
			}
		}

		protected override void OnClosed(EventArgs e)
		{
			base.OnClosed(e);
			if (global::ForkPlus.DesignTimeHelper.IsInDesignMode())
			{
				return;
			}
			_isClosed = true;
			_aiReviewJob?.Monitor.Cancel();
			_fileReviewDiffJob?.Monitor.Cancel();
			AiResponseWebView?.Dispose();
			ActivateMainWindow();
		}

		private static void ActivateMainWindow()
		{
			Window mainWindow = Application.Current?.MainWindow;
			if (mainWindow == null)
			{
				return;
			}
			if (mainWindow.WindowState == WindowState.Minimized)
			{
				mainWindow.WindowState = WindowState.Normal;
			}
			mainWindow.Activate();
			mainWindow.Topmost = true;
			mainWindow.Topmost = false;
		}

		private void Window_SizeChanged(object sender, SizeChangedEventArgs e)
		{
			if (_startUpFinished)
			{
				ForkPlusSettings.Default.AiResultWindowLocationState = this.GetWindowLocationState();
			}
		}

		private void FileReviewGrid_SizeChanged(object sender, SizeChangedEventArgs e)
		{
			double maxFileTreeWidth = Math.Max(160, FileReviewGrid.ActualWidth - 205);
			FileReviewGrid.ColumnDefinitions[0].MaxWidth = maxFileTreeWidth;
		}

		private void Window_Activated(object sender, EventArgs e)
		{
			if (!_startUpFinished)
			{
				_startUpFinished = true;
			}
		}

		private void RefreshTitle(RevisionDetails revisionDetails)
		{
			revisionDetails.MessageParts(out var subject, out var _);
			base.Title = revisionDetails.Sha.ToAbbreviatedString() + " " + subject;
		}

		private static string ReviewProviderName([Null] AiAgent aiAgent)
		{
			return aiAgent?.Name ?? ForkPlusSettings.Default.AiReviewSelectedModel ?? "AI";
		}

		private void RestoreAiResultColumnWidth()
		{
			double aiResultColumnWidth = ForkPlusSettings.Default.AiResultColumnWidth;
			AiResultGrid.ColumnDefinitions[2].Width = new GridLength(aiResultColumnWidth, GridUnitType.Pixel);
			double fileTreeColumnWidth = ForkPlusSettings.Default.AiReviewFileTreeColumnWidth;
			FileReviewGrid.ColumnDefinitions[FileReviewTreeColumn].Width = new GridLength(fileTreeColumnWidth, GridUnitType.Pixel);
		}

		private void SaveAiResultColumnWidth()
		{
			double value = AiResultGrid.ColumnDefinitions[2].Width.Value;
			ForkPlusSettings.Default.AiResultColumnWidth = value;
			ForkPlusSettings.Default.Save();
		}

		private void SaveFileReviewTreeColumnWidth()
		{
			double value = FileReviewGrid.ColumnDefinitions[FileReviewTreeColumn].Width.Value;
			ForkPlusSettings.Default.AiReviewFileTreeColumnWidth = value;
			ForkPlusSettings.Default.Save();
		}

		private void RetryAiReview(AiCodeReviewTarget target, bool replaceAll)
		{
			if (_repositoryUserControl?.GitModule == null || target == null)
			{
				return;
			}
			_aiReviewJob?.Monitor.Cancel();
			PrepareAiReviewUi(replaceAll, target);
			if (_aiAgent != null)
			{
				ReviewWithAiAgent(_repositoryUserControl.GitModule, target, _aiAgent, replaceAll);
			}
			else if (OpenAiService.IsAiReviewConfigured())
			{
				ReviewWithOpenAi(_repositoryUserControl.GitModule, target, replaceAll);
			}
			else
			{
				ShowError(PreferencesLocalization.Current("AI Review is not configured."));
			}
		}

		private void PrepareAiReviewUi(bool replaceAll, AiCodeReviewTarget target)
		{
			BusyIndicator.Show();
			RetryButton.IsEnabled = false;
			AiResponseFallback.Collapse();
			StopButton.Visibility = Visibility.Visible;
			StatusProgressBar.Visibility = Visibility.Visible;
			if (replaceAll)
			{
				AiResponseScrollViewer.Collapse();
				_suggestions.Clear();
				_aiReviewMarkdown = "";
				_aiReviewStatusMessage = null;
				_fileReviewMarkdownCache.Clear();
			}
			else if (target is AiCodeReviewTarget.Files files)
			{
				_aiReviewStatusMessage = PreferencesLocalization.FormatCurrent("Retrying AI review for {0} files...", ReviewableFiles(files.ChangedFiles).Length);
			}
			// 重置流式状态（VM 承载）：清空缓冲 + 重置节流计时 + 激活流式；初始状态提示“排队中”
			_viewModel.ResetForNewRequest();
			StatusTextBlock.Text = PreferencesLocalization.Current("Queued...");
			StatusProgressBar.Visibility = Visibility.Visible;
			StopButton.Visibility = Visibility.Visible;
		}

		private void ReviewWithOpenAi(GitModule gitModule, AiCodeReviewTarget target, bool replaceAll)
		{
			if (target is AiCodeReviewTarget.Files files)
			{
				ReviewFilesWithOpenAi(gitModule, files, replaceAll);
				return;
			}
			Sha src;
			Sha dst;
			if (target is AiCodeReviewTarget.Branch branch)
			{
				src = branch.Src;
				dst = branch.Dst;
			}
			else
			{
				if (!(target is AiCodeReviewTarget.ShaRange shaRange))
				{
					return;
				}
				src = shaRange.Src;
				dst = shaRange.Dst;
			}
			_aiReviewJob = _repositoryUserControl.JobQueue.Add(PreferencesLocalization.Current("AI Code Review"), delegate(JobMonitor monitor)
			{
				// 订阅进度回调：monitor.Update 触发时把阶段文字（排队/请求中/生成中等）同步到状态栏
				monitor.SetProgressAction(delegate
				{
					UpdateStatus(monitor.ProgressMessage);
				});
				UpdateStatus(PreferencesLocalization.Current("Collecting diff..."));
				GitCommandResult<string> patchResult = new GetRangePatchGitCommand().Execute(gitModule, src, dst);
				if (monitor.IsCanceled)
				{
					return;
				}
				if (!patchResult.Succeeded)
				{
					base.Dispatcher.Async(delegate
					{
						if (_isClosed || monitor.IsCanceled)
						{
							return;
						}
						StopStreamingRender();
						BusyIndicator.Collapse();
						StatusProgressBar.Visibility = Visibility.Collapsed;
						StopButton.Visibility = Visibility.Collapsed;
						StatusTextBlock.Text = "";
						RetryButton.IsEnabled = true;
						AiResponseScrollViewer.Collapse();
						AiResponseFallback.Show();
						AiResponseFallback.FallbackTitle = PreferencesLocalization.Current("Error");
						AiResponseFallback.FallbackMessage = PreferencesLocalization.Current("Cannot get diff:\n") + patchResult.Error.FriendlyDescription;
						SendAiReviewCompletedNotification(gitModule, success: false);
					});
				}
				else
				{
					OpenAiService openAiService = OpenAiService.CreateFromAiReviewSettings();
					ServiceResult<OpenAiResponse> codeReviewResult = openAiService.CodeReview(patchResult.Result, monitor, OnStreamingChunk);
					if (monitor.IsCanceled)
					{
						return;
					}
					if (!codeReviewResult.Succeeded)
					{
						base.Dispatcher.Async(delegate
						{
							if (_isClosed || monitor.IsCanceled)
							{
								return;
							}
							ShowError(codeReviewResult.Error.FriendlyMessage);
							SendAiReviewCompletedNotification(gitModule, success: false);
						});
					}
					else
					{
						string aiReviewMarkdown = codeReviewResult.Result.Message;
						string aiReviewDisplayMarkdown = RemoveSuggestionBlocks(aiReviewMarkdown);
						// 原 WebView2 需 ConvertMarkdownToHtml 转为 HTML 再 NavigateToString；
						// MarkdownScrollViewer 直接渲染 Markdown，无需 HTML 转换。
						base.Dispatcher.Async(delegate
						{
							if (_isClosed || monitor.IsCanceled)
							{
								return;
							}
							ApplyAiReviewResult(target, aiReviewDisplayMarkdown, aiReviewMarkdown, replaceAll);
							SendAiReviewCompletedNotification(gitModule, success: true);
						});
					}
				}
			});
		}

		private void ReviewFilesWithOpenAi(GitModule gitModule, AiCodeReviewTarget.Files target, bool replaceAll)
		{
			_aiReviewJob = _repositoryUserControl.JobQueue.Add(PreferencesLocalization.Current("AI Code Review"), delegate(JobMonitor monitor)
			{
				// 订阅进度回调：monitor.Update 触发时把阶段文字（排队/请求中/生成中等）同步到状态栏
				monitor.SetProgressAction(delegate
				{
					UpdateStatus(monitor.ProgressMessage);
				});
				UpdateStatus(PreferencesLocalization.Current("Collecting diff..."));
				GitCommandResult<string> contextResult = BuildFileReviewContext(gitModule, target, monitor);
				if (monitor.IsCanceled)
				{
					return;
				}
				if (!contextResult.Succeeded)
				{
					base.Dispatcher.Async(delegate
					{
						if (_isClosed || monitor.IsCanceled)
						{
							return;
						}
						ShowError(contextResult.Error.FriendlyDescription);
						SendAiReviewCompletedNotification(gitModule, success: false);
					});
					return;
				}
				OpenAiService openAiService = OpenAiService.CreateFromAiReviewSettings();
				ServiceResult<OpenAiResponse> codeReviewResult = openAiService.CodeReviewFiles(contextResult.Result, monitor, OnStreamingChunk);
				if (monitor.IsCanceled)
				{
					return;
				}
				if (!codeReviewResult.Succeeded)
				{
					base.Dispatcher.Async(delegate
					{
						if (_isClosed || monitor.IsCanceled)
						{
							return;
						}
						ShowError(codeReviewResult.Error.FriendlyMessage + "\n\n" + PreferencesLocalization.Current("AI service may be busy or timed out. Please retry later, reduce selected files, or increase retry/timeout in Preferences > AI Enhancement."));
						SendAiReviewCompletedNotification(gitModule, success: false);
					});
					return;
				}
				string aiReviewMarkdown = codeReviewResult.Result.Message;
				string aiReviewDisplayMarkdown = RemoveSuggestionBlocks(aiReviewMarkdown);
				// 原 WebView2 需 ConvertMarkdownToHtml 转为 HTML 再 NavigateToString；
				// MarkdownScrollViewer 直接渲染 Markdown，无需 HTML 转换。
				base.Dispatcher.Async(delegate
				{
					if (_isClosed || monitor.IsCanceled)
					{
						return;
					}
					ApplyAiReviewResult(target, aiReviewDisplayMarkdown, aiReviewMarkdown, replaceAll);
					SendAiReviewCompletedNotification(gitModule, success: true);
				});
			});
		}

		private void InitializeFileReviewList(AiCodeReviewTarget.Files target)
		{
			ChangedFile[] files = target.ChangedFiles.Where((ChangedFile x) => !x.IsDirectory && !(x is SubmoduleChangedFile)).ToArray();
			FileReviewFileListUserControl.Mode = FileListMode.Tree;
			FileReviewFileListUserControl.SetItemSource(files, forceRefresh: true, restoreSelection: false);
			if (!FileReviewFileListUserControl.SelectFirstAvailableFile() && files.FirstOrDefault() is ChangedFile firstFile)
			{
				ShowFileDiff(_repositoryUserControl.GitModule, firstFile, target.Amend);
			}
		}

		private void FileReviewFileListUserControl_SelectionChanged(object sender, FileListEventArgs e)
		{
			if (_fileReviewTarget == null || _repositoryUserControl?.GitModule == null)
			{
				return;
			}
			if (e.SelectedFile != null && !e.SelectedFile.IsDirectory && !(e.SelectedFile is SubmoduleChangedFile))
			{
				ShowFileDiff(_repositoryUserControl.GitModule, e.SelectedFile, _fileReviewTarget.Amend);
			}
		}

		private void RetryButton_Click(object sender, RoutedEventArgs e)
		{
			RetryAiReview(_target, replaceAll: true);
		}

		private void FileReviewFileListUserControl_ContextMenuOpening(object sender, ContextRequestedEventArgs e)
		{
			if (!(_target is AiCodeReviewTarget.Files filesTarget))
			{
				e.Handled = true;
				return;
			}
			ChangedFile[] files = ReviewableFiles(ClickedFileReviewItems());
			if (files.Length == 0)
			{
				e.Handled = true;
				return;
			}
			ContextMenu contextMenu = FileReviewFileListUserControl.ContextMenu;
			contextMenu.Items.Clear();
			MenuItem retryMenuItem = new MenuItem
			{
				Header = files.Length == 1
					? PreferencesLocalization.Current("Retry AI Review")
					: PreferencesLocalization.FormatCurrent("Retry AI Review for {0} files", files.Length),
				IsEnabled = RetryButton.IsEnabled
			};
			retryMenuItem.Click += delegate
			{
				RetryAiReview(new AiCodeReviewTarget.Files(files, filesTarget.Amend), replaceAll: false);
			};
			contextMenu.Items.Add(retryMenuItem);
		}

		private ChangedFile[] ClickedFileReviewItems()
		{
			if (FileReviewFileListUserControl.TreeView.LastClickedItem is FileListItem clickedItem)
			{
				if (FileReviewFileListUserControl.TreeView.SelectedItems.Contains(clickedItem))
				{
					return ExpandFileListItems(FileReviewFileListUserControl.TreeView.SelectedItems.CompactMap((object x) => x as FileListItem));
				}
				return ExpandFileListItems(new FileListItem[1] { clickedItem });
			}
			return ExpandFileListItems(FileReviewFileListUserControl.TreeView.SelectedItems.CompactMap((object x) => x as FileListItem));
		}

		private static ChangedFile[] ExpandFileListItems(IEnumerable<FileListItem> items)
		{
			List<ChangedFile> files = new List<ChangedFile>();
			foreach (FileListItem item in items ?? Enumerable.Empty<FileListItem>())
			{
				AppendFileListItemFiles(item, files);
			}
			return files.ToArray();
		}

		private static void AppendFileListItemFiles(FileListItem item, List<ChangedFile> files)
		{
			if (item == null || item.IsHidden)
			{
				return;
			}
			if (!item.HasChildren)
			{
				files.Add(item.ChangedFile);
				return;
			}
			foreach (FileListItem child in item.Children.OfType<FileListItem>())
			{
				AppendFileListItemFiles(child, files);
			}
		}

		private void ShowFileDiff(GitModule gitModule, ChangedFile changedFile, bool amend)
		{
			if (changedFile == null)
			{
				return;
			}
			_selectedFileReviewPath = changedFile.Path;
			if (!string.IsNullOrEmpty(_aiReviewMarkdown))
			{
				RenderAiReviewOutput();
			}
			_fileReviewDiffJob?.Monitor.Cancel();
			_fileReviewDiffJob = _repositoryUserControl.JobQueue.Add(PreferencesLocalization.Current("Load AI review diff"), delegate(JobMonitor monitor)
			{
				GitCommandResult<DiffContent> diff = new GetWorkingDirectoryFileChangesGitCommand().Execute(gitModule, changedFile, amend && changedFile.Staged ? new GetWorkingDirectoryFileChangesGitCommand.WorkingDirectoryRevisionDiffTarget.Amend() : null, ForkPlusSettings.Default.DiffContextSize, gitModule.Settings.TabWidth, ForkPlusSettings.Default.DiffIgnoreWhitespaces, ForkPlusSettings.Default.DiffShowEntireFile, loadLargeUntrackedFiles: true, resolvedConflict: false);
				if (monitor.IsCanceled)
				{
					return;
				}
				base.Dispatcher.Async(delegate
				{
					if (_isClosed || monitor.IsCanceled)
					{
						return;
					}
					FileReviewDiffControl.Content = diff;
				});
			}, JobFlags.Hidden, showMessageWhenDone: false);
		}

		private GitCommandResult<string> BuildFileReviewContext(GitModule gitModule, AiCodeReviewTarget.Files target, JobMonitor monitor)
		{
			StringBuilder builder = new StringBuilder();
			int reviewedCount = 0;
			foreach (ChangedFile changedFile in target.ChangedFiles)
			{
				if (monitor.IsCanceled)
				{
					return GitCommandResult<string>.Failure(new GitCommandError.Cancelled());
				}
				if (changedFile.IsDirectory || changedFile is SubmoduleChangedFile)
				{
					continue;
				}
				reviewedCount++;
				monitor.Update(Math.Min(0.8, reviewedCount / Math.Max(1.0, target.ChangedFiles.Length)), PreferencesLocalization.FormatCurrent("Collecting {0}", changedFile.Path));
				builder.AppendLine("## File: " + changedFile.Path);
				builder.AppendLine();
				builder.AppendLine("### Diff");
				builder.AppendLine("```diff");
				GitCommandResult<string> diffResult = new GetWorkingDirectoryFileChangesGitCommand().GetChangesAsBinaryPatch(gitModule, changedFile, target.Amend);
				if (diffResult.Succeeded)
				{
					builder.AppendLine(TrimForPrompt(diffResult.Result, 60000));
				}
				else
				{
					builder.AppendLine("Cannot load diff: " + diffResult.Error.FriendlyDescription);
				}
				builder.AppendLine("```");
				builder.AppendLine();
				builder.AppendLine("### Full file");
				builder.AppendLine("```");
				builder.AppendLine(TrimForPrompt(ReadFileForReview(gitModule, changedFile), 80000));
				builder.AppendLine("```");
				builder.AppendLine();
			}
			if (reviewedCount == 0)
			{
				return GitCommandResult<string>.Failure(new GitCommandError.GenericError("No reviewable files selected."));
			}
			return GitCommandResult<string>.Success(builder.ToString());
		}

		private static string ReadFileForReview(GitModule gitModule, ChangedFile changedFile)
		{
			if (changedFile.ChangeType == ChangeType.Deleted)
			{
				return "(file deleted)";
			}
			try
			{
				string filePath = gitModule.MakePath(changedFile.Path);
				if (File.Exists(filePath))
				{
					return File.ReadAllText(filePath);
				}
			}
			catch (Exception ex)
			{
				Log.Warn("Failed to read working tree file for AI review", ex);
			}
			try
			{
				GitRequestResult result = new GitRequest(gitModule).Command("show", (":" + changedFile.Path).Quotify()).Execute(silent: true);
				if (result.Success)
				{
					return result.Stdout;
				}
			}
			catch (Exception ex)
			{
				Log.Warn("Failed to read index file for AI review", ex);
			}
			return "(file content unavailable)";
		}

		private static string TrimForPrompt(string text, int maxLength)
		{
			if (string.IsNullOrEmpty(text) || text.Length <= maxLength)
			{
				return text ?? "";
			}
			return text.Substring(0, maxLength) + "\n\n... truncated for AI review ...";
		}

		private static List<AiReviewSuggestion> ExtractSuggestions(string markdown)
		{
			List<AiReviewSuggestion> result = new List<AiReviewSuggestion>();
			string marker = "```forkplus-ai-suggestions";
			int start = (markdown ?? "").IndexOf(marker, StringComparison.OrdinalIgnoreCase);
			if (start < 0)
			{
				return result;
			}
			start = (markdown ?? "").IndexOf('\n', start);
			if (start < 0)
			{
				return result;
			}
			int end = markdown.IndexOf("```", start + 1, StringComparison.Ordinal);
			if (end < 0)
			{
				return result;
			}
			string json = markdown.Substring(start + 1, end - start - 1).Trim();
			try
			{
				if (!(JToken.Parse(json) is JArray array))
				{
					return result;
				}
				foreach (JToken token in array)
				{
					string file = token["file"]?.Value<string>();
					string oldText = token["oldText"]?.Value<string>();
					string newText = token["newText"]?.Value<string>();
					if (string.IsNullOrWhiteSpace(file) || string.IsNullOrEmpty(oldText) || newText == null)
					{
						continue;
					}
					result.Add(new AiReviewSuggestion
					{
						File = file,
						Line = token["line"]?.Value<int>() ?? 0,
						Comment = token["comment"]?.Value<string>() ?? "",
						OldText = oldText,
						NewText = newText
					});
				}
			}
			catch (Exception ex)
			{
				Log.Warn("Failed to parse AI review suggestions", ex);
			}
			return result;
		}

		private void PreviewSuggestion(int index)
		{
			if (_suggestions == null || index < 0 || index >= _suggestions.Count)
			{
				return;
			}
			AiReviewSuggestion suggestion = _suggestions[index];
			try
			{
				AiSuggestionPreviewWindow window = new AiSuggestionPreviewWindow(_repositoryUserControl, suggestion.File, suggestion.Comment, suggestion.OldText, suggestion.NewText)
				{
					Owner = this
				};
				if (window.ShowDialog().GetValueOrDefault())
				{
					ApplySuggestion(index);
				}
			}
			catch (Exception ex)
			{
				Log.Warn("Failed to show AI suggestion preview", ex);
				ShowError(ex.Message);
			}
		}

		private void ApplySuggestion(int index)
		{
			if (_suggestions == null || index < 0 || index >= _suggestions.Count)
			{
				return;
			}
			AiReviewSuggestion suggestion = _suggestions[index];
			try
			{
				string filePath = _repositoryUserControl.GitModule.MakePath(suggestion.File);
				if (!File.Exists(filePath))
				{
					ShowError(PreferencesLocalization.FormatCurrent("Cannot find file: {0}", suggestion.File));
					return;
				}
				string content = File.ReadAllText(filePath);
				int matchIndex = content.IndexOf(suggestion.OldText, StringComparison.Ordinal);
				if (matchIndex < 0)
				{
					ShowError(PreferencesLocalization.FormatCurrent("Cannot apply suggestion because the target text was not found in {0}.", suggestion.File));
					return;
				}
				string updated = content.Remove(matchIndex, suggestion.OldText.Length).Insert(matchIndex, suggestion.NewText);
				File.WriteAllText(filePath, updated, Encoding.UTF8);
				_suggestions.RemoveAt(index);
				_aiReviewStatusMessage = PreferencesLocalization.FormatCurrent("Applied suggestion to {0}.", suggestion.File);
				RenderAiReviewOutput();
				_repositoryUserControl.InvalidateAndRefresh(SubDomain.Status, null, RepositoryViewMode.CommitViewMode);
			}
			catch (Exception ex)
			{
				Log.Warn("Failed to apply AI suggestion", ex);
				ShowError(ex.Message);
			}
		}

		private static string RemoveSuggestionBlocks(string markdown)
		{
			if (string.IsNullOrEmpty(markdown))
			{
				return markdown ?? "";
			}
			return Regex.Replace(markdown, "```forkplus-ai-suggestions\\s*[\\s\\S]*?```", "", RegexOptions.IgnoreCase).Trim();
		}

		private void ApplyAiReviewResult(AiCodeReviewTarget target, string displayMarkdown, string rawMarkdown, bool replaceAll)
		{
			// 检视成功完成：停止流式预览并清除状态栏（进度条/Stop 按钮/状态文字）
			ClearStatus();
			List<AiReviewSuggestion> newSuggestions = ExtractSuggestions(rawMarkdown);
			if (!replaceAll && target is AiCodeReviewTarget.Files files)
			{
				ChangedFile[] reviewableFiles = ReviewableFiles(files.ChangedFiles);
				displayMarkdown = MergeFileReviewMarkdown(_aiReviewMarkdown, displayMarkdown, reviewableFiles);
				_suggestions = MergeSuggestions(_suggestions, newSuggestions, reviewableFiles);
				_aiReviewStatusMessage = PreferencesLocalization.FormatCurrent("Retried AI review for {0} files.", reviewableFiles.Length);
			}
			else
			{
				_suggestions = newSuggestions;
			}
			ShowMarkdownOutput(displayMarkdown, preserveStatusMessage: !replaceAll);
		}

		private static ChangedFile[] ReviewableFiles(IEnumerable<ChangedFile> files)
		{
			if (files == null)
			{
				return new ChangedFile[0];
			}
			return files
				.Where((ChangedFile file) => file != null && !file.IsDirectory && !(file is SubmoduleChangedFile))
				.GroupBy((ChangedFile file) => NormalizeReviewPath(file.Path))
				.Select((IGrouping<string, ChangedFile> group) => group.First())
				.ToArray();
		}

		private static List<AiReviewSuggestion> MergeSuggestions(List<AiReviewSuggestion> existingSuggestions, List<AiReviewSuggestion> newSuggestions, ChangedFile[] retriedFiles)
		{
			List<AiReviewSuggestion> mergedSuggestions = (existingSuggestions ?? new List<AiReviewSuggestion>())
				.Where((AiReviewSuggestion suggestion) => !ContainsReviewFile(retriedFiles, suggestion.File))
				.ToList();
			mergedSuggestions.AddRange(newSuggestions ?? new List<AiReviewSuggestion>());
			return mergedSuggestions;
		}

		private static string MergeFileReviewMarkdown(string existingMarkdown, string retryMarkdown, ChangedFile[] retriedFiles)
		{
			string existingWithoutRetriedFiles = RemoveFileReviewSections(existingMarkdown, retriedFiles);
			if (string.IsNullOrWhiteSpace(existingWithoutRetriedFiles))
			{
				return retryMarkdown ?? "";
			}
			if (string.IsNullOrWhiteSpace(retryMarkdown))
			{
				return existingWithoutRetriedFiles.Trim();
			}
			return existingWithoutRetriedFiles.TrimEnd() + "\n\n" + retryMarkdown.Trim();
		}

		private static string RemoveFileReviewSections(string markdown, ChangedFile[] retriedFiles)
		{
			if (string.IsNullOrWhiteSpace(markdown) || retriedFiles == null || retriedFiles.Length == 0)
			{
				return markdown ?? "";
			}
			string[] lines = NormalizeLineEndings(markdown).Split('\n');
			StringBuilder builder = new StringBuilder();
			for (int i = 0; i < lines.Length; i++)
			{
				if (TryParseMarkdownHeading(lines[i], out int level, out string heading) && HeadingMatchesAnyReviewFile(heading, retriedFiles))
				{
					i++;
					while (i < lines.Length)
					{
						if (TryParseMarkdownHeading(lines[i], out int nextLevel, out _) && nextLevel <= level)
						{
							i--;
							break;
						}
						i++;
					}
					continue;
				}
				builder.AppendLine(lines[i]);
			}
			return builder.ToString().Trim();
		}

		private void ReviewWithAiAgent(GitModule gitModule, AiCodeReviewTarget target, AiAgent aiAgent, bool replaceAll)
		{
			_aiReviewJob = _repositoryUserControl.JobQueue.Add(PreferencesLocalization.Current("AI Code Review"), delegate(JobMonitor monitor)
			{
				GitCommandResult<string> codeReviewResult = new MakeCodeReviewShellCommand().Execute(aiAgent, target, gitModule.Path, monitor);
				if (monitor.IsCanceled)
				{
					return;
				}
				if (!codeReviewResult.Succeeded)
				{
					base.Dispatcher.Async(delegate
					{
						if (_isClosed || monitor.IsCanceled)
						{
							return;
						}
						ShowError(codeReviewResult.Error.FriendlyDescription);
						SendAiReviewCompletedNotification(gitModule, success: false);
					});
				}
				else
				{
					string aiReviewMarkdown = codeReviewResult.Result;
					string aiReviewDisplayMarkdown = RemoveSuggestionBlocks(aiReviewMarkdown);
					GitCommandResult<string> btResult = BtRequest.Run(() => default(BtMdToHtmlResult), delegate(ref BtMdToHtmlResult x)
					{
						return Bt.bt_md_to_html(aiReviewDisplayMarkdown, ref x);
					}, delegate(ref BtMdToHtmlResult x)
					{
						return GitCommandResult<string>.Success(x.html.GetUtf8String());
					}, delegate(ref BtMdToHtmlResult x)
					{
						Bt.bt_release_md_to_html(ref x);
					});
					base.Dispatcher.Async(delegate
					{
						if (_isClosed || monitor.IsCanceled)
						{
							return;
						}
						if (!btResult.Succeeded)
						{
							ShowError(btResult.Error.FriendlyDescription);
							SendAiReviewCompletedNotification(gitModule, success: false);
						}
						else
						{
							ApplyAiReviewResult(target, aiReviewDisplayMarkdown, aiReviewMarkdown, btResult.Result, replaceAll);
							SendAiReviewCompletedNotification(gitModule, success: true);
						}
					});
				}
			});
		}

		private void ShowError(string error)
		{
			StopStreamingRender();
			BusyIndicator.Collapse();
			StatusProgressBar.Visibility = Visibility.Collapsed;
			StopButton.Visibility = Visibility.Collapsed;
			StatusTextBlock.Text = "";
			RetryButton.IsEnabled = true;
			AiResponseScrollViewer.Collapse();
			AiResponseFallback.Show();
			AiResponseFallback.FallbackTitle = PreferencesLocalization.Current("Error");
			AiResponseFallback.FallbackMessage = error;
		}

		/// <summary>更新状态栏文字 + 显示进度条（用于排队/请求中/收集 diff/生成中等阶段提示）。</summary>
		private void UpdateStatus(string message)
		{
			base.Dispatcher.Async(delegate
			{
				if (_isClosed)
				{
					return;
				}
				StatusTextBlock.Text = message ?? "";
				StatusProgressBar.Visibility = Visibility.Visible;
				BusyIndicator.Show();
				StopButton.Visibility = Visibility.Visible;
			});
		}

		/// <summary>清除状态栏 + 隐藏进度条（用于完成/取消/出错）。</summary>
		private void ClearStatus()
		{
			StopStreamingRender();
			base.Dispatcher.Async(delegate
			{
				if (_isClosed)
				{
					return;
				}
				StatusTextBlock.Text = "";
				StatusProgressBar.Visibility = Visibility.Collapsed;
				BusyIndicator.Collapse();
				StopButton.Visibility = Visibility.Collapsed;
			});
		}

		/// <summary>Stop 按钮：取消当前 AI 检视任务。</summary>
		private void StopButton_Click(object sender, RoutedEventArgs e)
		{
			_aiReviewJob?.Monitor.Cancel();
			ClearStatus();
			StatusTextBlock.Text = PreferencesLocalization.Current("Stopped");
			RetryButton.IsEnabled = true;
		}

		/// <summary>
		/// 流式 chunk 回调（由后台 job 线程在 SSE 解析时调用）：
		/// 追加到 VM 缓冲，并在 UI 线程节流触发实时预览渲染。
		/// </summary>
		private void OnStreamingChunk(string chunk)
		{
			(bool shouldRender, int lengthSoFar) = _viewModel.OnChunk(chunk);
			if (!shouldRender)
			{
				return;
			}
			base.Dispatcher.Async(delegate
			{
				TryRenderStreamingPreview(lengthSoFar);
			});
		}

		/// <summary>节流后的实时预览渲染：把当前已收到的 Markdown 直接写入 MarkdownScrollViewer。</summary>
		private void TryRenderStreamingPreview(int lengthSoFar)
		{
			if (_isClosed || !_viewModel.IsStreamingActive)
			{
				return;
			}
			if (AiResponseWebView == null)
			{
				return;
			}
			// 节流：首个 chunk 立即渲染；之后每隔 StreamingRenderIntervalMs 渲染一次（VM 承载判定）
			if (!_viewModel.ShouldRenderNow())
			{
				return;
			}
			// 在状态栏展示已接收字数，让用户感知进度（替代一直转圈圈）
			StatusTextBlock.Text = PreferencesLocalization.FormatCurrent("Generating... ({0} chars)", lengthSoFar);
			StatusProgressBar.Visibility = Visibility.Visible;
			string md = _viewModel.GetMarkdownSnapshot();
			if (string.IsNullOrEmpty(md))
			{
				return;
			}
			if (_isClosed || !_viewModel.IsStreamingActive)
			{
				return;
			}
			// 渲染前快照用户是否在底部。如果在底部，渲染后延迟一轮布局再 ScrollToEnd（跟随最新内容）。
			// 用户主动上滚时不滚动，保持阅读位置。（滚动状态由 VM 承载）
			bool shouldScrollToEnd = _viewModel.StreamingUserAtBottom;
			try
			{
				AiResponseScrollViewer.Show();
				AiResponseWebView.Markdown = md;
				BusyIndicator.Collapse();
				if (shouldScrollToEnd)
				{
					// Markdown 渲染后需要一轮布局才能测得正确高度，延迟滚动到底
					Dispatcher.Post(ScrollInnerViewerToEnd);
				}
			}
			catch (Exception ex)
			{
				Log.Warn("Streaming markdown render failed: " + ex.Message);
			}
		}

		/// <summary>停止流式预览渲染（完成/取消/出错时调用，阻止已排队的渲染任务写入 WebView）。</summary>
		private void StopStreamingRender()
		{
			_viewModel.StopStreaming();
		}

		/// <summary>初始化模型下拉选择：先用当前选中模型占位，后台拉取完整列表。
		/// 模型拉取逻辑由 AiModelListLoader 承载（零 WPF），本方法仅负责 ComboBox 填充 + Dispatcher 调度。</summary>
		private void InitializeModelComboBox()
		{
			string currentModel = AiModelListLoader.CurrentModel;
			if (!string.IsNullOrWhiteSpace(currentModel))
			{
				ModelComboBox.Items.Add(currentModel);
				ModelComboBox.SelectedIndex = 0;
			}
			else
			{
				ModelComboBox.Items.Add(PreferencesLocalization.Current("Select model..."));
				ModelComboBox.SelectedIndex = 0;
			}
			// 后台拉取模型列表（不阻塞 UI 线程）
			System.Threading.ThreadPool.QueueUserWorkItem(delegate(object state)
			{
				List<string> models = AiModelListLoader.LoadModels();
				if (models == null)
				{
					return;
				}
				base.Dispatcher.Async(delegate
				{
					try
					{
						if (_viewModel.ModelListLoaded)
						{
							return;
						}
						_viewModel.ModelListLoaded = true;
						string selected = AiModelListLoader.CurrentModel;
						ModelComboBox.Items.Clear();
						foreach (string m in models)
						{
							ModelComboBox.Items.Add(m);
						}
						(int idx, bool shouldInsertCurrent) = AiModelListLoader.FindSelectedIndex(models, selected);
						if (idx >= 0)
						{
							ModelComboBox.SelectedIndex = idx;
						}
						else if (shouldInsertCurrent)
						{
							ModelComboBox.Items.Insert(0, selected);
							ModelComboBox.SelectedIndex = 0;
						}
						else if (ModelComboBox.Items.Count > 0)
						{
							ModelComboBox.SelectedIndex = 0;
						}
					}
					catch (Exception ex)
					{
						Log.Warn("Failed to populate model combo box: " + ex.Message);
					}
				});
			});
		}

		/// <summary>切换模型时保存到设置（AiModelListLoader 负责持久化）。</summary>
		private void ModelComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
		{
			if (ModelComboBox.SelectedItem == null)
			{
				return;
			}
			string selected = (string)ModelComboBox.SelectedItem;
			if (string.IsNullOrWhiteSpace(selected) || selected == PreferencesLocalization.Current("Select model..."))
			{
				return;
			}
			AiModelListLoader.CurrentModel = selected;
		}

		// 原 CoreWebView2_WebMessageReceived 处理三种 JS postMessage：
		//   preview-suggestion:{i} → PreviewSuggestion(i)
		//   apply-suggestion:{i}   → ApplySuggestion(i)
		//   scroll-at-bottom:1/0   → _viewModel.SetUserAtBottom
		// 阶段 4.7-c-4：建议卡改为原生 Button.Click（见 BuildSuggestionCards），
		//   滚动跟踪改为 ScrollViewer.ScrollChanged（见 InnerScrollViewer_ScrollChanged）。
		//   该方法已删除。

		private void ShowMarkdownOutput(string markdown, bool preserveStatusMessage = false)
		{
			_aiReviewMarkdown = markdown ?? "";
			if (!preserveStatusMessage)
			{
				_aiReviewStatusMessage = null;
			}
			_fileReviewMarkdownCache.Clear();
			RenderAiReviewOutput();
		}

		/// <summary>渲染 AI 审查结果：Markdown 正文 + 原生建议卡。
		/// 原 WebView2 把状态/当前文件/审查结果/建议卡/所有结果拼成 HTML + NavigateToString；
		/// MarkdownScrollViewer 直接渲染 Markdown，建议卡用原生 Border + Button（无需 JS 互操作）。</summary>
		private void RenderAiReviewOutput()
		{
			BusyIndicator.Collapse();
			RetryButton.IsEnabled = true;
			AiResponseFallback.Collapse();
			AiResponseScrollViewer.Show();
			try
			{
				string selectedFile = SelectedFileReviewPath();
				// 组合 Markdown：状态 + 当前文件 + 审查结果 + 所有结果（建议卡由原生控件渲染，不在 Markdown 中）
				StringBuilder markdownBuilder = new StringBuilder();
				string statusMd = CreateStatusMarkdown();
				if (!string.IsNullOrEmpty(statusMd))
				{
					markdownBuilder.Append(statusMd).Append("\n\n");
				}
				string bodyMd = CreateReviewBodyMarkdown(selectedFile);
				if (!string.IsNullOrEmpty(bodyMd))
				{
					markdownBuilder.Append(bodyMd).Append("\n\n");
				}
				string allMd = CreateAllReviewResultsMarkdown(selectedFile);
				if (!string.IsNullOrEmpty(allMd))
				{
					markdownBuilder.Append("---\n\n").Append(allMd);
				}
				AiResponseWebView.Markdown = markdownBuilder.ToString();
				// 动态构建建议卡（原生控件，替代原 HTML <button onclick=...> + JS postMessage）
				BuildSuggestionCards(selectedFile);
			}
			catch (Exception ex)
			{
				Log.Error("Failed to render AI review markdown", ex);
			}
		}

		/// <summary>状态信息 Markdown（引用块）。</summary>
		private string CreateStatusMarkdown()
		{
			if (string.IsNullOrWhiteSpace(_aiReviewStatusMessage))
			{
				return "";
			}
			return "> " + _aiReviewStatusMessage.Replace("\n", "\n> ");
		}

		/// <summary>审查结果 Markdown：文件审查模式下显示当前文件的审查结果，否则显示全部审查结果。</summary>
		private string CreateReviewBodyMarkdown(string selectedFile)
		{
			if (!(_target is AiCodeReviewTarget.Files) || string.IsNullOrWhiteSpace(selectedFile))
			{
				return _aiReviewMarkdown ?? "";
			}
			string fileMarkdown = CreateSelectedFileReviewMarkdown(selectedFile);
			StringBuilder builder = new StringBuilder();
			builder.Append("**").Append(PreferencesLocalization.Current("Current file")).Append(": ").Append(selectedFile).Append("**\n\n");
			if (string.IsNullOrWhiteSpace(fileMarkdown))
			{
				builder.Append("*").Append(PreferencesLocalization.Current("No findings were reported for this file.")).Append("*");
			}
			else
			{
				builder.Append(fileMarkdown);
			}
			return builder.ToString();
		}

		/// <summary>"所有审查结果" Markdown（文件审查模式下折叠显示完整审查结果）。</summary>
		private string CreateAllReviewResultsMarkdown(string selectedFile)
		{
			if (string.IsNullOrWhiteSpace(_aiReviewMarkdown) || !(_target is AiCodeReviewTarget.Files) || string.IsNullOrWhiteSpace(selectedFile))
			{
				return "";
			}
			return "## " + PreferencesLocalization.Current("All review results") + "\n\n" + _aiReviewMarkdown;
		}

		/// <summary>从完整审查 Markdown 中提取指定文件的审查段落（带缓存）。
		/// 原 _fileReviewHtmlCache 缓存 HTML；现 _fileReviewMarkdownCache 缓存 Markdown。</summary>
		private string CreateSelectedFileReviewMarkdown(string selectedFile)
		{
			string markdown = ExtractFileReviewMarkdown(_aiReviewMarkdown, selectedFile);
			if (string.IsNullOrWhiteSpace(markdown))
			{
				return "";
			}
			string cacheKey = NormalizeReviewPath(selectedFile);
			if (_fileReviewMarkdownCache.TryGetValue(cacheKey, out string cachedMarkdown))
			{
				return cachedMarkdown;
			}
			_fileReviewMarkdownCache[cacheKey] = markdown;
			return markdown;
		}

		/// <summary>动态构建建议卡（原生 Avalonia 控件，替代原 HTML <button onclick=...> + JS postMessage）。
		/// 每个建议卡是一个 Border + TextBlock（文件:行号 + 评论）+ 两个 Button（Preview/Apply）。
		/// Button.Click 直接调用 PreviewSuggestion/ApplySuggestion，无需 JS 互操作。</summary>
		private void BuildSuggestionCards(string selectedFile)
		{
			SuggestionsPanel.Children.Clear();
			if (_suggestions == null || _suggestions.Count == 0)
			{
				return;
			}
			bool hasSuggestion = false;
			for (int i = 0; i < _suggestions.Count; i++)
			{
				AiReviewSuggestion suggestion = _suggestions[i];
				if (!string.IsNullOrWhiteSpace(selectedFile) && !IsSameReviewFile(suggestion.File, selectedFile))
				{
					continue;
				}
				hasSuggestion = true;
				int index = i; // 闭包捕获
				Border card = new Border
				{
					BorderBrush = new SolidColorBrush(Color.FromArgb(60, 0x88, 0x88, 0x88)),
					BorderThickness = new Thickness(1),
					CornerRadius = new CornerRadius(4),
					Padding = new Thickness(8),
					Margin = new Thickness(0, 10, 0, 0),
					Background = new SolidColorBrush(Color.FromArgb(20, 0x88, 0x88, 0x88))
				};
				StackPanel cardPanel = new StackPanel();
				// 文件:行号（粗体）
				string header = suggestion.File ?? "";
				if (suggestion.Line > 0)
				{
					header += ":" + suggestion.Line;
				}
				TextBlock headerBlock = new TextBlock
				{
					Text = header,
					FontWeight = FontWeights.Bold,
					Margin = new Thickness(0, 0, 0, 4)
				};
				cardPanel.Children.Add(headerBlock);
				// 评论（如有）
				if (!string.IsNullOrWhiteSpace(suggestion.Comment))
				{
					TextBlock commentBlock = new TextBlock
					{
						Text = suggestion.Comment,
						TextWrapping = TextWrapping.Wrap,
						Margin = new Thickness(0, 0, 0, 8)
					};
					cardPanel.Children.Add(commentBlock);
				}
				// 按钮行：Preview replacement + Apply suggestion
				StackPanel buttonPanel = new StackPanel
				{
					Orientation = Orientation.Horizontal,
					Margin = new Thickness(0, 8, 0, 0)
				};
				Button previewButton = new Button
				{
					Content = PreferencesLocalization.Current("Preview replacement"),
					Margin = new Thickness(0, 0, 6, 0),
					Padding = new Thickness(10, 2, 10, 2)
				};
				previewButton.Click += delegate { PreviewSuggestion(index); };
				Button applyButton = new Button
				{
					Content = PreferencesLocalization.Current("Apply suggestion"),
					Padding = new Thickness(10, 2, 10, 2)
				};
				applyButton.Click += delegate { ApplySuggestion(index); };
				buttonPanel.Children.Add(previewButton);
				buttonPanel.Children.Add(applyButton);
				cardPanel.Children.Add(buttonPanel);
				card.Child = cardPanel;
				SuggestionsPanel.Children.Add(card);
			}
			if (!hasSuggestion && !string.IsNullOrWhiteSpace(selectedFile))
			{
				TextBlock emptyBlock = new TextBlock
				{
					Text = PreferencesLocalization.Current("No applicable suggestions for this file."),
					Foreground = new SolidColorBrush(Color.FromArgb(180, 0x88, 0x88, 0x88)),
					Margin = new Thickness(0, 8, 0, 14)
				};
				SuggestionsPanel.Children.Add(emptyBlock);
			}
		}

		private string SelectedFileReviewPath()
		{
			if (!(_target is AiCodeReviewTarget.Files))
			{
				return null;
			}
			return _selectedFileReviewPath;
		}

		private string ExtractFileReviewMarkdown(string markdown, string selectedFile)
		{
			if (string.IsNullOrWhiteSpace(markdown) || string.IsNullOrWhiteSpace(selectedFile))
			{
				return "";
			}
			string[] lines = NormalizeLineEndings(markdown).Split('\n');
			string selectedPath = NormalizeReviewPath(selectedFile);
			int start = -1;
			int headingLevel = 0;
			for (int i = 0; i < lines.Length; i++)
			{
				if (TryParseMarkdownHeading(lines[i], out int level, out string heading) && HeadingMatchesReviewFile(heading, selectedPath))
				{
					start = i;
					headingLevel = level;
					break;
				}
			}
			if (start >= 0)
			{
				int end = lines.Length;
				for (int i = start + 1; i < lines.Length; i++)
				{
					if (TryParseMarkdownHeading(lines[i], out int level, out _) && level <= headingLevel)
					{
						end = i;
						break;
					}
				}
				return string.Join("\n", lines.Skip(start).Take(end - start)).Trim();
			}
			return ExtractInlineFindingsForFile(lines, selectedFile, selectedPath);
		}

		private string ExtractInlineFindingsForFile(string[] lines, string selectedFile, string selectedPath)
		{
			StringBuilder builder = new StringBuilder();
			for (int i = 0; i < lines.Length; i++)
			{
				if (!NormalizeReviewPath(lines[i]).Contains(selectedPath))
				{
					continue;
				}
				if (builder.Length == 0)
				{
					builder.Append("## ").Append(selectedFile).AppendLine();
					builder.AppendLine();
				}
				builder.AppendLine(lines[i]);
				for (int j = i + 1; j < lines.Length; j++)
				{
					if (string.IsNullOrWhiteSpace(lines[j]) || TryParseMarkdownHeading(lines[j], out _, out _))
					{
						break;
					}
					builder.AppendLine(lines[j]);
				}
			}
			return builder.ToString().Trim();
		}

		private static bool TryParseMarkdownHeading(string line, out int level, out string heading)
		{
			level = 0;
			heading = null;
			if (string.IsNullOrWhiteSpace(line))
			{
				return false;
			}
			Match match = Regex.Match(line, "^(#{1,6})\\s+(.+?)\\s*$");
			if (!match.Success)
			{
				return false;
			}
			level = match.Groups[1].Value.Length;
			heading = match.Groups[2].Value;
			return true;
		}

		private static bool HeadingMatchesReviewFile(string heading, string selectedPath)
		{
			string normalizedHeading = NormalizeReviewPath(heading)
				.Replace("file:", "")
				.Replace("`", "")
				.Trim();
			return normalizedHeading.Contains(selectedPath);
		}

		private static bool HeadingMatchesAnyReviewFile(string heading, ChangedFile[] files)
		{
			return files != null && files.Any((ChangedFile file) => HeadingMatchesReviewFile(heading, NormalizeReviewPath(file.Path)));
		}

		private static bool ContainsReviewFile(ChangedFile[] files, string path)
		{
			return files != null && files.Any((ChangedFile file) => IsSameReviewFile(file.Path, path));
		}

		private static bool IsSameReviewFile(string left, string right)
		{
			return string.Equals(NormalizeReviewPath(left), NormalizeReviewPath(right), StringComparison.OrdinalIgnoreCase);
		}

		private static string NormalizeReviewPath(string path)
		{
			if (path == null)
			{
				return "";
			}
			string result = path.Replace('\\', '/').Trim().Trim('`', '"', '\'');
			while (result.StartsWith("./", StringComparison.Ordinal))
			{
				result = result.Substring(2);
			}
			return result.ToLowerInvariant();
		}

		private static string NormalizeLineEndings(string value)
		{
			return (value ?? "").Replace("\r\n", "\n").Replace('\r', '\n');
		}

		private void SendAiReviewCompletedNotification(GitModule gitModule, bool success)
		{
			string text = RepositoryName(gitModule);
			if (text != null && !base.IsActive)
			{
				string arg = WebUtility.HtmlEncode("ai-review:" + base.Title);
				string arg2 = PreferencesLocalization.Current(success ? "AI Code Review Completed" : "AI Code Review Failed");
				string text2 = null;
				if (_target is AiCodeReviewTarget.Branch branch)
				{
					text2 = branch.Name;
				}
				else if (_target is AiCodeReviewTarget.ShaRange { Dst: var dst })
				{
					text2 = dst.ToAbbreviatedString();
				}
				else if (_target is AiCodeReviewTarget.Files files)
				{
					text2 = PreferencesLocalization.FormatCurrent("{0} files", files.ChangedFiles.Length);
				}
				string arg3 = WebUtility.HtmlEncode(text + ": " + text2);
				NotificationManager.SendWindowsNotification($"<?xml version=\"1.0\" encoding =\"utf-8\" ?>\n<toast launch=\"{arg}\" >\n<audio silent=\"true\"/>\n<visual>\n    <binding template=\"ToastGeneric\">\n        <text hint-maxLines=\"1\" >{arg2}</text>\n        <text>{arg3}</text>\n    </binding>\n</visual>\n</toast>\n");
			}
		}

		[Null]
		private static string RepositoryName(GitModule gitModule)
		{
			RepositoryManager.Repository? repository = RepositoryManager.Instance.Repositories.FirstItemStruct((RepositoryManager.Repository x) => x.Path == gitModule.Path);
			if (repository.HasValue)
			{
				RepositoryManager.Repository valueOrDefault = repository.GetValueOrDefault();
				return valueOrDefault.Name();
			}
			return null;
		}

	}
}

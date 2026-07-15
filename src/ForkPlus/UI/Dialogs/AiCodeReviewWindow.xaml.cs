using System;
using System.ComponentModel;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Markup;
using ForkPlus.Accounts;
using ForkPlus.Accounts.AiServices;
using ForkPlus.Biturbo;
using ForkPlus.Git;
using ForkPlus.Git.Commands;
using ForkPlus.Git.Commands.LeanBranching;
using ForkPlus.Git.Diff;
using ForkPlus.Git.Interaction;
using ForkPlus.Jobs;
using ForkPlus.Settings;
using ForkPlus.UI.UserControls;
using ForkPlus.UI.UserControls.Preferences;
using ForkPlus.Utils.Http;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.Wpf;
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

		private string _aiReviewHtml;

		private string _selectedFileReviewPath;

		private string _aiReviewStatusMessage;

		private readonly Dictionary<string, string> _fileReviewHtmlCache = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

		private const int AiResultColumn = 2;

		private const int FileReviewTreeColumn = 0;

		private static string _cachedCss;

		// 模型列表是否已后台加载完成
		private bool _modelListLoaded;

		// 流式输出的实时 Markdown 缓冲（边收边渲染到 WebView）
		private StringBuilder _streamingMarkdown;

		// 保护 _streamingMarkdown 的并发追加（chunk 来自后台 job 线程，渲染来自 UI 线程）
		private readonly object _streamingLock = new object();

		// 流式预览渲染节流：避免每个 chunk 都触发一次 markdown→html→NavigateToString 造成卡顿
		private DateTime _lastStreamingRenderUtc = DateTime.MinValue;
		private const int StreamingRenderIntervalMs = 400;

		// 流式是否处于活动状态（完成/取消/出错后置 false，阻止已排队的渲染任务再写入 WebView）
		private bool _streamingActive;

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
			base.Loaded += async delegate
			{
				await aiCodeReviewWindow.InitializeWebView();
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
			WeakEventManager<NotificationCenter, EventArgs<ThemeType>>.AddHandler(NotificationCenter.Current, "ApplicationThemeChanged", ApplicationThemeChanged);
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
			if (AiResponseWebView?.CoreWebView2 != null && !string.IsNullOrWhiteSpace(_aiReviewHtml))
			{
				_fileReviewHtmlCache.Clear();
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

		private async Task InitializeWebView()
		{
			await AiResponseWebView.EnsureCoreWebView2Async(await WebView2EnvironmentHelper.GetEnvironmentAsync());
			UpdateWebViewTheme();
			AiResponseWebView.CoreWebView2.ContextMenuRequested += delegate(object s, CoreWebView2ContextMenuRequestedEventArgs e)
			{
				e.Handled = true;
			};
			AiResponseWebView.CoreWebView2.WebMessageReceived += CoreWebView2_WebMessageReceived;
		}

		private void ApplicationThemeChanged(object sender, EventArgs<ThemeType> e)
		{
			UpdateWebViewTheme();
		}

		private void UpdateWebViewTheme()
		{
			if (base.IsLoaded && AiResponseWebView.CoreWebView2 != null)
			{
				AiResponseWebView.CoreWebView2.Profile.PreferredColorScheme = ((ForkPlusSettings.Default.Theme != ThemeType.Dark) ? CoreWebView2PreferredColorScheme.Light : CoreWebView2PreferredColorScheme.Dark);
			}
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
				AiResponseWebView.Collapse();
				_suggestions.Clear();
				_aiReviewMarkdown = "";
				_aiReviewHtml = "";
				_aiReviewStatusMessage = null;
				_fileReviewHtmlCache.Clear();
				lock (_streamingLock)
				{
					_streamingMarkdown = new StringBuilder();
				}
			}
			else if (target is AiCodeReviewTarget.Files files)
			{
				_aiReviewStatusMessage = PreferencesLocalization.FormatCurrent("Retrying AI review for {0} files...", ReviewableFiles(files.ChangedFiles).Length);
			}
			// 重置节流计时，使首个 chunk 立即渲染；初始状态提示“排队中”
			_lastStreamingRenderUtc = DateTime.MinValue;
			_streamingActive = true;
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
						AiResponseWebView.Collapse();
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
						GitCommandResult<string> btResult = ConvertMarkdownToHtml(aiReviewDisplayMarkdown);
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
				GitCommandResult<string> btResult = ConvertMarkdownToHtml(aiReviewDisplayMarkdown);
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

		private void FileReviewFileListUserControl_ContextMenuOpening(object sender, ContextMenuEventArgs e)
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
			if (!string.IsNullOrEmpty(_aiReviewHtml))
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

		private static GitCommandResult<string> ConvertMarkdownToHtml(string markdown)
		{
			return BtRequest.Run(() => default(BtMdToHtmlResult), delegate(ref BtMdToHtmlResult x)
			{
				return Bt.bt_md_to_html(markdown, ref x);
			}, delegate(ref BtMdToHtmlResult x)
			{
				return GitCommandResult<string>.Success(x.html.GetUtf8String());
			}, delegate(ref BtMdToHtmlResult x)
			{
				Bt.bt_release_md_to_html(ref x);
			});
		}

		private void ApplyAiReviewResult(AiCodeReviewTarget target, string displayMarkdown, string rawMarkdown, string html, bool replaceAll)
		{
			// 检视成功完成：停止流式预览并清除状态栏（进度条/Stop 按钮/状态文字）
			ClearStatus();
			List<AiReviewSuggestion> newSuggestions = ExtractSuggestions(rawMarkdown);
			if (!replaceAll && target is AiCodeReviewTarget.Files files)
			{
				ChangedFile[] reviewableFiles = ReviewableFiles(files.ChangedFiles);
				displayMarkdown = MergeFileReviewMarkdown(_aiReviewMarkdown, displayMarkdown, reviewableFiles);
				_suggestions = MergeSuggestions(_suggestions, newSuggestions, reviewableFiles);
				GitCommandResult<string> mergedHtml = ConvertMarkdownToHtml(displayMarkdown);
				html = mergedHtml.Succeeded ? mergedHtml.Result : html;
				_aiReviewStatusMessage = PreferencesLocalization.FormatCurrent("Retried AI review for {0} files.", reviewableFiles.Length);
			}
			else
			{
				_suggestions = newSuggestions;
			}
			ShowMarkdownOutput(displayMarkdown, html, preserveStatusMessage: !replaceAll);
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
			AiResponseWebView.Collapse();
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
		/// 追加到 _streamingMarkdown，并在 UI 线程节流触发实时预览渲染。
		/// </summary>
		private void OnStreamingChunk(string chunk)
		{
			if (string.IsNullOrEmpty(chunk))
			{
				return;
			}
			lock (_streamingLock)
			{
				if (_streamingMarkdown == null)
				{
					_streamingMarkdown = new StringBuilder();
				}
				_streamingMarkdown.Append(chunk);
			}
			int lengthSoFar;
			lock (_streamingLock)
			{
				lengthSoFar = _streamingMarkdown.Length;
			}
			base.Dispatcher.Async(delegate
			{
				TryRenderStreamingPreview(lengthSoFar);
			});
		}

		/// <summary>节流后的实时预览渲染：把当前已收到的 Markdown 转为 HTML 并写入 WebView。</summary>
		private void TryRenderStreamingPreview(int lengthSoFar)
		{
			if (_isClosed || !_streamingActive)
			{
				return;
			}
			if (AiResponseWebView?.CoreWebView2 == null)
			{
				return;
			}
			// 节流：首个 chunk 立即渲染；之后每隔 StreamingRenderIntervalMs 渲染一次
			DateTime now = DateTime.UtcNow;
			if (now - _lastStreamingRenderUtc < TimeSpan.FromMilliseconds(StreamingRenderIntervalMs))
			{
				return;
			}
			_lastStreamingRenderUtc = now;
			// 在状态栏展示已接收字数，让用户感知进度（替代一直转圈圈）
			StatusTextBlock.Text = PreferencesLocalization.FormatCurrent("Generating... ({0} chars)", lengthSoFar);
			StatusProgressBar.Visibility = Visibility.Visible;
			string md;
			lock (_streamingLock)
			{
				md = _streamingMarkdown?.ToString() ?? "";
			}
			if (string.IsNullOrEmpty(md))
			{
				return;
			}
			// markdown→html 在 UI 线程完成（与最终 ApplyAiReviewResult 的转换串行，避免 native Bt 库并发问题；
			// 检视响应体量有限，转换很快，节流后不会造成可感知卡顿）
			string body;
			try
			{
				GitCommandResult<string> htmlResult = ConvertMarkdownToHtml(md);
				body = htmlResult.Succeeded ? htmlResult.Result : WebUtility.HtmlEncode(md);
			}
			catch (Exception ex)
			{
				Log.Warn("Streaming markdown render failed: " + ex.Message);
				body = WebUtility.HtmlEncode(md);
			}
			if (_isClosed || !_streamingActive)
			{
				return;
			}
			string css = GetCss();
			string html = "<!DOCTYPE html>\n<html>\n<head><meta charset='utf-8'><style>" + css + "\n</style></head>\n<body>" + body + "\n</body>\n</html>";
			try
			{
				AiResponseWebView.NavigateToString(html);
				AiResponseWebView.Show();
				BusyIndicator.Collapse();
			}
			catch (Exception ex)
			{
				Log.Warn("Streaming WebView navigate failed: " + ex.Message);
			}
		}

		/// <summary>停止流式预览渲染（完成/取消/出错时调用，阻止已排队的渲染任务写入 WebView）。</summary>
		private void StopStreamingRender()
		{
			_streamingActive = false;
		}

		/// <summary>初始化模型下拉选择：先用当前选中模型占位，后台拉取完整列表。</summary>
		private void InitializeModelComboBox()
		{
			string currentModel = ForkPlusSettings.Default.AiReviewSelectedModel;
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
				List<string> models = null;
				try
				{
					if (OpenAiService.IsAiReviewConfigured())
					{
						OpenAiService aiService = OpenAiService.CreateFromAiReviewSettings();
						ServiceResult<string[]> result = aiService.ListModels();
						if (result.Succeeded && result.Result != null)
						{
							models = new List<string>(result.Result);
						}
					}
				}
				catch (Exception ex)
				{
					Log.Warn("Failed to load AI model list: " + ex.Message);
				}
				if (models == null || models.Count == 0)
				{
					return;
				}
				base.Dispatcher.Async(delegate
				{
					try
					{
						if (_modelListLoaded)
						{
							return;
						}
						_modelListLoaded = true;
						string selected = ForkPlusSettings.Default.AiReviewSelectedModel;
						ModelComboBox.Items.Clear();
						foreach (string m in models)
						{
							if (!string.IsNullOrWhiteSpace(m))
							{
								ModelComboBox.Items.Add(m);
							}
						}
						int idx = -1;
						for (int i = 0; i < ModelComboBox.Items.Count; i++)
						{
							if (string.Equals((string)ModelComboBox.Items[i], selected, StringComparison.OrdinalIgnoreCase))
							{
								idx = i;
								break;
							}
						}
						if (idx >= 0)
						{
							ModelComboBox.SelectedIndex = idx;
						}
						else if (!string.IsNullOrWhiteSpace(selected))
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

		/// <summary>切换模型时保存到设置。</summary>
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
			if (string.Equals(selected, ForkPlusSettings.Default.AiReviewSelectedModel, StringComparison.OrdinalIgnoreCase))
			{
				return;
			}
			ForkPlusSettings.Default.AiReviewSelectedModel = selected;
			ForkPlusSettings.Default.Save();
		}

		private void CoreWebView2_WebMessageReceived(object sender, CoreWebView2WebMessageReceivedEventArgs e)
		{
			string message = e.TryGetWebMessageAsString();
			if (message != null && message.StartsWith("preview-suggestion:", StringComparison.Ordinal))
			{
				if (int.TryParse(message.Substring("preview-suggestion:".Length), out int index))
				{
					Dispatcher.BeginInvoke(new Action(delegate
					{
						PreviewSuggestion(index);
					}));
				}
			}
			else if (message != null && message.StartsWith("apply-suggestion:", StringComparison.Ordinal))
			{
				if (int.TryParse(message.Substring("apply-suggestion:".Length), out int index))
				{
					ApplySuggestion(index);
				}
			}
		}

		private void ShowMarkdownOutput(string markdown, string html, bool preserveStatusMessage = false)
		{
			_aiReviewMarkdown = markdown ?? "";
			_aiReviewHtml = html ?? "";
			if (!preserveStatusMessage)
			{
				_aiReviewStatusMessage = null;
			}
			_fileReviewHtmlCache.Clear();
			RenderAiReviewOutput();
		}

		private void RenderAiReviewOutput()
		{
			BusyIndicator.Collapse();
			RetryButton.IsEnabled = true;
			AiResponseFallback.Collapse();
			AiResponseWebView.Show();
			string css = GetCss();
			try
			{
				string selectedFile = SelectedFileReviewPath();
				string htmlContent = "<!DOCTYPE html>\n<html>\n    <head>\n        <meta charset='utf-8'>\n        <style>\n            " + css + "\n            .ai-current-file{border:1px solid #8883;border-radius:4px;padding:8px;margin:0 0 12px;background:#8881;font-size:12px;}\n            .ai-status{border:1px solid #2e7d3233;border-radius:4px;padding:8px;margin:0 0 12px;background:#2e7d3218;}\n            .ai-suggestion{border:1px solid #8883;border-radius:4px;padding:8px;margin:10px 0;background:#8881;}\n            .ai-suggestion button{margin-top:8px;margin-right:6px;}\n            .ai-empty{color:#888;margin:8px 0 14px;}\n            .ai-all-results{margin-top:18px;padding-top:10px;border-top:1px solid #8883;}\n        </style>\n    </head>\n    <body>\n        " + CreateStatusHtml() + "\n        " + CreateReviewBodyHtml(selectedFile) + "\n        " + CreateSuggestionsHtml(selectedFile) + "\n        " + CreateAllReviewResultsHtml(selectedFile) + "\n        <script>function previewSuggestion(i){window.chrome.webview.postMessage('preview-suggestion:' + i);}function applySuggestion(i){window.chrome.webview.postMessage('apply-suggestion:' + i);}</script>\n    </body>\n</html>";
				AiResponseWebView.NavigateToString(htmlContent);
			}
			catch (Exception ex)
			{
				Log.Error("Failed to navigate WebView to markdown HTML", ex);
			}
		}

		private string CreateStatusHtml()
		{
			if (string.IsNullOrWhiteSpace(_aiReviewStatusMessage))
			{
				return "";
			}
			return "<div class='ai-status'>" + WebUtility.HtmlEncode(_aiReviewStatusMessage) + "</div>";
		}

		private string CreateReviewBodyHtml(string selectedFile)
		{
			if (!(_target is AiCodeReviewTarget.Files) || string.IsNullOrWhiteSpace(selectedFile))
			{
				return _aiReviewHtml ?? "";
			}
			string fileHtml = CreateSelectedFileReviewHtml(selectedFile);
			StringBuilder builder = new StringBuilder();
			builder.Append("<div class='ai-current-file'>")
				.Append(WebUtility.HtmlEncode(PreferencesLocalization.Current("Current file")))
				.Append(": <b>")
				.Append(WebUtility.HtmlEncode(selectedFile))
				.Append("</b></div>");
			if (string.IsNullOrWhiteSpace(fileHtml))
			{
				builder.Append("<p class='ai-empty'>")
					.Append(WebUtility.HtmlEncode(PreferencesLocalization.Current("No findings were reported for this file.")))
					.Append("</p>");
			}
			else
			{
				builder.Append(fileHtml);
			}
			return builder.ToString();
		}

		private string CreateAllReviewResultsHtml(string selectedFile)
		{
			if (string.IsNullOrWhiteSpace(_aiReviewHtml) || !(_target is AiCodeReviewTarget.Files) || string.IsNullOrWhiteSpace(selectedFile))
			{
				return "";
			}
			return "<details class='ai-all-results'><summary>"
				+ WebUtility.HtmlEncode(PreferencesLocalization.Current("All review results"))
				+ "</summary>"
				+ _aiReviewHtml
				+ "</details>";
		}

		private string CreateSelectedFileReviewHtml(string selectedFile)
		{
			string markdown = ExtractFileReviewMarkdown(_aiReviewMarkdown, selectedFile);
			if (string.IsNullOrWhiteSpace(markdown))
			{
				return "";
			}
			string cacheKey = NormalizeReviewPath(selectedFile);
			if (_fileReviewHtmlCache.TryGetValue(cacheKey, out string cachedHtml))
			{
				return cachedHtml;
			}
			GitCommandResult<string> htmlResult = ConvertMarkdownToHtml(markdown);
			string html = htmlResult.Succeeded ? htmlResult.Result : "<pre>" + WebUtility.HtmlEncode(markdown) + "</pre>";
			_fileReviewHtmlCache[cacheKey] = html;
			return html;
		}

		private string CreateSuggestionsHtml(string selectedFile)
		{
			if (_suggestions == null || _suggestions.Count == 0)
			{
				return "";
			}
			StringBuilder builder = new StringBuilder();
			builder.Append("<h2>").Append(WebUtility.HtmlEncode(PreferencesLocalization.Current("Applicable suggestions"))).Append("</h2>");
			bool hasSuggestion = false;
			for (int i = 0; i < _suggestions.Count; i++)
			{
				AiReviewSuggestion suggestion = _suggestions[i];
				if (!string.IsNullOrWhiteSpace(selectedFile) && !IsSameReviewFile(suggestion.File, selectedFile))
				{
					continue;
				}
				hasSuggestion = true;
				builder.Append("<div class='ai-suggestion'>");
				builder.Append("<b>").Append(WebUtility.HtmlEncode(suggestion.File));
				if (suggestion.Line > 0)
				{
					builder.Append(":").Append(suggestion.Line);
				}
				builder.Append("</b>");
				if (!string.IsNullOrWhiteSpace(suggestion.Comment))
				{
					builder.Append("<p>").Append(WebUtility.HtmlEncode(suggestion.Comment)).Append("</p>");
				}
				builder.Append("<button onclick='previewSuggestion(").Append(i).Append(")'>").Append(WebUtility.HtmlEncode(PreferencesLocalization.Current("Preview replacement"))).Append("</button>");
				builder.Append("<button onclick='applySuggestion(").Append(i).Append(")'>").Append(WebUtility.HtmlEncode(PreferencesLocalization.Current("Apply suggestion"))).Append("</button>");
				builder.Append("</div>");
			}
			if (!hasSuggestion && !string.IsNullOrWhiteSpace(selectedFile))
			{
				builder.Append("<p class='ai-empty'>")
					.Append(WebUtility.HtmlEncode(PreferencesLocalization.Current("No applicable suggestions for this file.")))
					.Append("</p>");
			}
			return builder.ToString();
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

		private static string GetCss()
		{
			if (_cachedCss != null)
			{
				return _cachedCss;
			}
			try
			{
				Assembly executingAssembly = Assembly.GetExecutingAssembly();
				string name = "ForkPlus.Assets.md-ai-output.css";
				using Stream stream = executingAssembly.GetManifestResourceStream(name);
				using StreamReader streamReader = new StreamReader(stream);
				_cachedCss = streamReader.ReadToEnd();
				return _cachedCss;
			}
			catch (Exception ex)
			{
				Log.Error("Failed to read CSS resource", ex);
				return string.Empty;
			}
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

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Media3D;
using System.Windows.Shapes;
using ForkPlus.Git;
using ForkPlus.Git.Commands;
using ForkPlus.Git.Interaction;
using ForkPlus.Jobs;
using ForkPlus.Settings;
using ForkPlus.UI.Controls;
using ForkPlus.UI.Dialogs;
using ForkPlus.UI.UserControls.Preferences;
using ForkPlus.Accounts;
using ForkPlus.UI.Helpers;

namespace ForkPlus.UI.UserControls
{
	public partial class GitMmUserControl : UserControl, ForkPlus.UI.ILocalizableControl
	{
		private const int SubrepoScanDepth = 4;

		private const double SubrepoTabMinWidth = 140.0;

		private static readonly TimeSpan RuntimeStateCacheTtl = TimeSpan.FromSeconds(60.0);

		private static readonly TimeSpan DefaultBranchCacheTtl = TimeSpan.FromMinutes(30.0);

		private static readonly Dictionary<string, Tuple<string, DateTime>> _defaultBranchCache = new Dictionary<string, Tuple<string, DateTime>>(StringComparer.OrdinalIgnoreCase);

		private readonly DelayedAction<object> _saveSettingsAction;

		private readonly DelayedAction<object> _updateTabWidthsAction;

		private readonly JobQueue _jobQueue = new JobQueue();

		private readonly GitMmWorkspaceItem _workspace;

		private HashSet<string> _submoduleSubrepoPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

		private bool _restoringSettings;

		private bool _isBusy;

		private GridLength _expandedCommandOutputHeight = new GridLength(150.0);

		private Point _tabDragStartPoint;

		[Null]
		private TabItem _subrepoTabDragItem;

		[Null]
		private HashSet<string> _visibleSubrepoPaths;

		private bool _hasPersistedVisibleSubrepoFilter;

		private HashSet<string> _knownSubrepoPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

		private readonly Dictionary<string, Button> _summaryButtons = new Dictionary<string, Button>(StringComparer.OrdinalIgnoreCase);

		private bool _filterNonDefaultBranchOnly;

		private bool _filterFailedOnly;

		[Null]
		private string _activeSummaryFilterMode;

		private int _runtimeStateRequestId;

		[Null]
		private Job _activeJob;

		[Null]
		private Job _activeStatusRefreshJob;

		public JobQueue JobQueue => _jobQueue;

		public string WorkspacePath => _workspace.Path;

		public string WorkspaceTitle => "git mm: " + (RepositoryManager.Instance.FindRepositoryName(_workspace.Path) ?? _workspace.Name);

		[Null]
		public RepositoryUserControl ActiveRepositoryUserControl => _workspace.SelectedSubrepo?.RepositoryControl as RepositoryUserControl;

		[Null]
		public string SelectedSubrepoTitle => _workspace.SelectedSubrepo?.DisplayName;

		public bool ContainsSubrepoPath(string path)
		{
			string normalizedPath = NormalizePath(path);
			if (normalizedPath == null)
			{
				return false;
			}
			return _workspace.Subrepos.Any(delegate(GitMmSubrepoItem subrepo)
			{
				string subrepoPath = NormalizePath(subrepo.Path);
				return subrepoPath != null
					&& (string.Equals(subrepoPath, normalizedPath, StringComparison.OrdinalIgnoreCase)
						|| normalizedPath.StartsWith(subrepoPath + System.IO.Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)
						|| normalizedPath.StartsWith(subrepoPath + System.IO.Path.AltDirectorySeparatorChar, StringComparison.OrdinalIgnoreCase));
			});
		}

		public string StagedDiffSummary
		{
			get
			{
				int added = _workspace.Subrepos.Sum((GitMmSubrepoItem subrepo) => subrepo.StagedAdded);
				int deleted = _workspace.Subrepos.Sum((GitMmSubrepoItem subrepo) => subrepo.StagedDeleted);
				return added == 0 && deleted == 0 ? "" : $"+{added} -{deleted}";
			}
		}

		public GitMmUserControl(string workspacePath)
		{
			InitializeComponent();
			_workspace = new GitMmWorkspaceItem(workspacePath);
			_workspace.PropertyChanged += Workspace_PropertyChanged;
			WeakEventManager<NotificationCenter, EventArgs<string>>.AddHandler(NotificationCenter.Current, "RepositoryNameChanged", RepositoryNameChanged);
			WeakEventManager<NotificationCenter, EventArgs<RepositoryManager.Repository>>.AddHandler(NotificationCenter.Current, "RepositoryColorChanged", RepositoryColorChanged);
			SubreposTabControl.SelectionChanged += SubreposTabControl_SelectionChanged;
			_saveSettingsAction = new DelayedAction<object>(delegate { SaveSettingsImmediate(); }, 1.0);
			_updateTabWidthsAction = new DelayedAction<object>(delegate { UpdateSubrepoTabWidths(); }, 0.1);
			SubreposTabControl.SizeChanged += delegate
			{
				_updateTabWidthsAction.InvokeWithDelay(null);
			};
			PreferencesLocalization.Apply(this, ForkPlusSettings.Default.UiLanguage);
			RefreshCommandButtonTooltips();
			SetBusy(isBusy: false);
			RestoreSettings();
		}

		public static bool IsGitMmWorkspace(string path)
		{
			if (string.IsNullOrWhiteSpace(path))
			{
				return false;
			}
			return Directory.Exists(System.IO.Path.Combine(path, ".repo"))
				|| Directory.Exists(System.IO.Path.Combine(path, ".mm"));
		}

		public static int CountSubrepos(string workspacePath)
		{
			return ScanSubrepos(workspacePath, SubrepoScanDepth).Count;
		}

		public void Refresh()
		{
			if (_isBusy)
			{
				return;
			}
			RepositoryUserControl repositoryUserControl = ActiveRepositoryUserControl;
			if (repositoryUserControl == null)
			{
				return;
			}
			if (repositoryUserControl.ViewMode == RepositoryViewMode.CommitViewMode)
			{
				repositoryUserControl.InvalidateAndRefresh(SubDomain.DefaultRefresh, null, RepositoryViewMode.CommitViewMode);
			}
			else
			{
				repositoryUserControl.InvalidateAndRefresh(SubDomain.DefaultRefresh);
			}
		}

		public void Save()
		{
			SaveSettings();
		}

		public void ApplyLocalization()
		{
			PreferencesLocalization.Apply(this, ForkPlusSettings.Default.UiLanguage);
			RefreshCommandButtonTooltips();
			RefreshSubreposTitle();
			RefreshSubrepoTabHeaders();
			foreach (GitMmSubrepoItem subrepo in _workspace.Subrepos)
			{
				if (subrepo.RepositoryControl is ForkPlus.UI.ILocalizableControl localizableControl)
				{
					localizableControl.ApplyLocalization();
				}
			}
		}

		private void RefreshSubrepoTabHeaders()
		{
			foreach (TabItem tabItem in SubreposTabControl.Items.OfType<TabItem>())
			{
				if (tabItem.Tag is GitMmSubrepoItem subrepo)
				{
					RefreshSubrepoTabHeader(tabItem, subrepo);
					if (tabItem.Content is TextBlock placeholder && subrepo.RepositoryControl == null)
					{
						placeholder.Text = subrepo.DisplayName;
					}
				}
			}
		}

		private void SyncButton_Click(object sender, RoutedEventArgs e)
		{
			SaveSettings();
			if (KeyboardHelper.IsCtrlDown)
			{
				RunGitMm(CreateQuickSyncArgs());
				return;
			}
			GitMmSyncWindow window = new GitMmSyncWindow(_workspace.Path);
			if (window.ShowDialog().GetValueOrDefault())
			{
				RunGitMm(window.SyncArgs);
			}
		}

		private void StartButton_Click(object sender, RoutedEventArgs e)
		{
			SaveSettings();
			if (KeyboardHelper.IsCtrlDown)
			{
				RunGitMm(CreateQuickStartArgs());
				return;
			}
			GitMmStartWindow window = new GitMmStartWindow(_workspace.Subrepos, _workspace.SelectedSubrepo);
			if (window.ShowDialog().GetValueOrDefault())
			{
				RunGitMm(window.StartArgs);
			}
		}

		private void UploadButton_Click(object sender, RoutedEventArgs e)
		{
			SaveSettings();
			if (KeyboardHelper.IsCtrlDown)
			{
				RunGitMm(CreateQuickUploadArgs());
				return;
			}
			GitMmUploadWindow window = new GitMmUploadWindow(_workspace.Path);
			if (window.ShowDialog().GetValueOrDefault())
			{
				RunGitMm(window.UploadArgs);
			}
		}

		private void RefreshCommandButtonTooltips()
		{
			StartButton.ToolTip = Translate("Start") + Environment.NewLine + Translate("Hold Ctrl for Quick Start");
			SyncButton.ToolTip = Translate("Sync") + Environment.NewLine + Translate("Hold Ctrl for Quick Sync");
			UploadButton.ToolTip = Translate("Upload") + Environment.NewLine + Translate("Hold Ctrl for Quick Upload");
		}

		private static string[] CreateQuickStartArgs()
		{
			return new string[5] { "start", "develop", "-j", "8", "--all" };
		}

		private static string[] CreateQuickSyncArgs()
		{
			ForkPlusSettings.GitMmSettings settings = ForkPlusSettings.Default.GitMm;
			string checkoutJobs = string.IsNullOrWhiteSpace(settings.SyncJobs) ? "4" : settings.SyncJobs;
			string fetchJobs = settings.GetDialogOption("sync.fetchJobs", "8");
			return new string[5] { "sync", "-J", checkoutJobs, "-j", string.IsNullOrWhiteSpace(fetchJobs) ? "8" : fetchJobs };
		}

		private static string[] CreateQuickUploadArgs()
		{
			return new string[2] { "upload", "-y" };
		}

		private void RefreshSubrepos()
		{
			string selectedSubrepoPath = GetPreferredSubrepoPath();
			RunBackground("git mm scan repositories", delegate(JobMonitor monitor)
			{
				List<string> paths = ScanSubrepos(_workspace.Path, SubrepoScanDepth, out var submodulePaths);
				if (monitor.IsCanceled)
				{
					return;
				}
				Dispatcher.Async(delegate
				{
					if (monitor.IsCanceled)
					{
						return;
					}
					_submoduleSubrepoPaths = submodulePaths;
					_workspace.PreferredSubrepoPath = selectedSubrepoPath;
					_workspace.Subrepos = CreateSubrepoItems(paths, _workspace.Path);
					EnsureVisibleSubrepos();
					RebuildSubrepoTabs();
					RefreshSubreposTitle();
					RefreshSubrepoRuntimeState(force: true);
					SetStatus("");
					SaveSettings();
				});
			});
		}

		private void RefreshSubreposTitle()
		{
			SubreposTitleTextBlock.Text = PreferencesLocalization.FormatCurrent("{0} repositories", _workspace.Subrepos.Count);
			GitMmHelpButton.ToolTip = Translate("Show git mm reference");
			RefreshSubrepoSummary();
			RefreshSubrepoFilterButton();
		}

		private void GitMmHelpButton_Click(object sender, RoutedEventArgs e)
		{
			new GitMmReferenceWindow().ShowDialog();
		}

		private void RefreshSubrepoSummary()
		{
			int hiddenCount = _workspace.Subrepos.Count - _workspace.Subrepos.Count(IsSubrepoVisible);
			int loadedCount = _workspace.Subrepos.Count((GitMmSubrepoItem subrepo) => subrepo.RepositoryControl != null);
			int conflictCount = _workspace.Subrepos.Count((GitMmSubrepoItem subrepo) => subrepo.HasConflicts);
			int nonDefaultBranchCount = _workspace.Subrepos.Count((GitMmSubrepoItem subrepo) => subrepo.IsNonDefaultBranch);
			int aheadCount = _workspace.Subrepos.Count((GitMmSubrepoItem subrepo) => subrepo.AheadCount > 0);
			int behindCount = _workspace.Subrepos.Count((GitMmSubrepoItem subrepo) => subrepo.BehindCount > 0);
			HashSet<string> visibleButtonKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
			AddClearFilterSummaryButton(hiddenCount, visibleButtonKeys);
			AddSummaryButton("Conflicts: {0}", conflictCount, "conflicts", visibleButtonKeys);
			AddSummaryButton("Non-default: {0}", nonDefaultBranchCount, "nonDefault", visibleButtonKeys);
			AddSummaryButton("Ahead: {0}", aheadCount, "ahead", visibleButtonKeys);
			AddSummaryButton("Behind: {0}", behindCount, "behind", visibleButtonKeys);
			AddSummaryButton("Loaded: {0}", loadedCount, "loaded", visibleButtonKeys);
			AddSummaryButton("Hidden: {0}", hiddenCount, "hidden", visibleButtonKeys);
			foreach (KeyValuePair<string, Button> item in _summaryButtons)
			{
				item.Value.Visibility = visibleButtonKeys.Contains(item.Key) ? Visibility.Visible : Visibility.Collapsed;
			}
		}

		private void AddClearFilterSummaryButton(int hiddenCount, HashSet<string> visibleButtonKeys)
		{
			if (hiddenCount <= 0)
			{
				return;
			}
			const string key = "clear";
			visibleButtonKeys.Add(key);
			Button button = GetOrCreateSummaryButton(key);
			button.Tag = key;
			button.Content = PreferencesLocalization.Current("Show all");
			button.Foreground = Application.Current.TryFindResource("AccentBrush") as Brush;
			button.Margin = new Thickness(0.0, 0.0, 8.0, 0.0);
			button.ToolTip = Translate("Clear repository filter");
		}

		private void AddSummaryButton(string format, int value, string filterMode, HashSet<string> visibleButtonKeys)
		{
			if (value <= 0)
			{
				return;
			}
			visibleButtonKeys.Add(filterMode);
			Button button = GetOrCreateSummaryButton(filterMode);
			button.Tag = filterMode;
			button.Content = PreferencesLocalization.FormatCurrent(format, value);
			button.Foreground = Application.Current.TryFindResource("SecondaryLabelBrush") as Brush;
			button.Margin = new Thickness(0.0, 0.0, 6.0, 0.0);
			button.ToolTip = Translate("Click to show matching repositories");
		}

		private Button GetOrCreateSummaryButton(string key)
		{
			if (_summaryButtons.TryGetValue(key, out Button button))
			{
				return button;
			}
			button = new Button
			{
				Style = Theme.TransparentButtonStyle,
				FontSize = 12.0,
				Padding = new Thickness(3.0, 0.0, 3.0, 0.0)
			};
			button.Click += SummaryButton_Click;
			_summaryButtons[key] = button;
			SubrepoSummaryPanel.Children.Add(button);
			return button;
		}

		private void SummaryButton_Click(object sender, RoutedEventArgs e)
		{
			string filterMode = (sender as FrameworkElement)?.Tag as string;
			if (filterMode == "clear")
			{
				ClearSubrepoFilter();
				return;
			}
			if (!string.IsNullOrWhiteSpace(filterMode))
			{
				TryApplySummaryFilterMode(filterMode, save: true);
			}
		}

		private bool ApplySubrepoSummaryFilter(string filterMode, Func<GitMmSubrepoItem, bool> predicate, bool save)
		{
			GitMmSubrepoItem[] matchingSubrepos = _workspace.Subrepos.Where(predicate).ToArray();
			if (matchingSubrepos.Length == 0)
			{
				return false;
			}
			_activeSummaryFilterMode = filterMode;
			_visibleSubrepoPaths = new HashSet<string>(matchingSubrepos
				.Select((GitMmSubrepoItem subrepo) => NormalizePath(subrepo.Path))
				.Where((string path) => !string.IsNullOrWhiteSpace(path)), StringComparer.OrdinalIgnoreCase);
			_hasPersistedVisibleSubrepoFilter = true;
			RebuildSubrepoTabs();
			RefreshSubreposTitle();
			if (save)
			{
				SaveSettings();
			}
			return true;
		}

		private bool TryApplySummaryFilterMode(string filterMode, bool save)
		{
			switch (filterMode)
			{
				case "conflicts":
					return ApplySubrepoSummaryFilter(filterMode, (GitMmSubrepoItem subrepo) => subrepo.HasConflicts, save);
				case "nonDefault":
					return ApplySubrepoSummaryFilter(filterMode, (GitMmSubrepoItem subrepo) => subrepo.IsNonDefaultBranch, save);
				case "ahead":
					return ApplySubrepoSummaryFilter(filterMode, (GitMmSubrepoItem subrepo) => subrepo.AheadCount > 0, save);
				case "behind":
					return ApplySubrepoSummaryFilter(filterMode, (GitMmSubrepoItem subrepo) => subrepo.BehindCount > 0, save);
				case "loaded":
					return ApplySubrepoSummaryFilter(filterMode, (GitMmSubrepoItem subrepo) => subrepo.RepositoryControl != null, save);
				case "hidden":
					return ApplySubrepoSummaryFilter(null, (GitMmSubrepoItem subrepo) => !IsSubrepoVisible(subrepo), save);
				default:
					return false;
			}
		}

		private void ClearSubrepoFilter()
		{
			EnsureVisibleSubrepos();
			foreach (GitMmSubrepoItem subrepo in _workspace.Subrepos)
			{
				SetSubrepoVisible(subrepo, isVisible: true);
			}
			_filterNonDefaultBranchOnly = false;
			_filterFailedOnly = false;
			_activeSummaryFilterMode = null;
			RebuildSubrepoTabs();
			RefreshSubreposTitle();
			SaveSettings();
		}

		private void RefreshSubrepoFilterButton()
		{
			int totalCount = _workspace.Subrepos.Count;
			int visibleCount = _workspace.Subrepos.Count(IsSubrepoVisible);
			SubrepoFilterButton.Content = PreferencesLocalization.FormatCurrent("{0}/{1} shown", visibleCount, totalCount);
		}

		private void CommandHistoryButton_Click(object sender, RoutedEventArgs e)
		{
			ContextMenu contextMenu = new ContextMenu();
			string[] history = ForkPlusSettings.Default.GitMm.CommandHistory ?? new string[0];
			if (history.Length == 0)
			{
				MenuItem emptyItem = new MenuItem
				{
					Header = Translate("No command history"),
					IsEnabled = false
				};
				contextMenu.Items.Add(emptyItem);
			}
			foreach (string command in history)
			{
				MenuItem item = new MenuItem
				{
					Header = command
				};
				item.Click += delegate
				{
					string[] args = ParseCommandHistory(command);
					if (args.Length > 0)
					{
						RunGitMm(args);
					}
				};
				contextMenu.Items.Add(item);
			}
			CommandHistoryButton.ContextMenu = contextMenu;
			contextMenu.PlacementTarget = CommandHistoryButton;
			contextMenu.IsOpen = true;
		}

		private static string[] ParseCommandHistory(string command)
		{
			if (string.IsNullOrWhiteSpace(command))
			{
				return new string[0];
			}
			if (command.StartsWith("git mm "))
			{
				command = command.Substring("git mm ".Length);
			}
			List<string> args = new List<string>();
			bool quoted = false;
			System.Text.StringBuilder current = new System.Text.StringBuilder();
			for (int i = 0; i < command.Length; i++)
			{
				char c = command[i];
				if (c == '"')
				{
					quoted = !quoted;
					continue;
				}
				if (char.IsWhiteSpace(c) && !quoted)
				{
					if (current.Length > 0)
					{
						args.Add(current.ToString());
						current.Clear();
					}
					continue;
				}
				current.Append(c);
			}
			if (current.Length > 0)
			{
				args.Add(current.ToString());
			}
			return args.ToArray();
		}

		private void RunGitMm(string[] args, byte[] stdin = null)
		{
			string commandText = FormatCommand(args);
			ClearOutput();
			SetStatus(commandText);
			SaveCommandHistory(commandText);
			SetCommandStateForVisibleSubrepos(GitMmSubrepoCommandState.Running);
			RunBackground(commandText, delegate(JobMonitor monitor)
			{
				GitCommand command = new GitCommand("mm");
				command.AddRange(args);
				GitRequest request = default(GitRequest)
					.CurrentDir(_workspace.Path)
					.Command(command)
					.Env(new (string, string)[1] { ("GIT_TERMINAL_PROMPT", "0") });
				GitRequestResult result;
				if (stdin != null)
				{
					result = request.Stdin(stdin).ExecuteBt(monitor);
					AppendOutputText(result.Stdout);
					AppendOutputText(result.Stderr);
				}
				else
				{
					result = request.ExecuteLong(
							delegate(string line)
							{
								AppendOutput(line);
							},
							delegate(string line)
							{
								AppendOutput(line);
							},
							monitor);
				}
				Dispatcher.Async(delegate
				{
					AppendOutput("");
					AppendOutput(string.Format(Translate("Exit code: {0}"), result.ExitCode));
					if (args.FirstItem() == "upload")
					{
						SaveUploadLinks(ExtractUrls(result.FullReadableOutput()));
					}
					SetCommandStateForVisibleSubrepos(result.Success ? GitMmSubrepoCommandState.Success : GitMmSubrepoCommandState.Failed);
					SetStatus(result.Success ? Translate("git mm command finished") : Translate("git mm command finished with errors"));
					string commandName = args.FirstItem();
					string title = PreferencesLocalization.FormatCurrent("git mm {0}", commandName);
					string body = result.Success
						? PreferencesLocalization.Current("Command succeeded")
						: PreferencesLocalization.Current("Command failed");
					NotificationManager.SendWindowsNotification(
						$"<?xml version=\"1.0\" encoding =\"utf-8\" ?>\n<toast>\n<audio silent=\"true\"/>\n<visual>\n    <binding template=\"ToastGeneric\">\n        <text hint-maxLines=\"1\" >{System.Net.WebUtility.HtmlEncode(title)}</text>\n        <text>{System.Net.WebUtility.HtmlEncode(body)}</text>\n    </binding>\n</visual>\n</toast>\n");
				});
				if (!monitor.IsCanceled)
				{
					if (ShouldRescanSubreposAfterCommand(args))
					{
						List<string> paths = ScanSubrepos(_workspace.Path, SubrepoScanDepth, out var submodulePaths);
						Dispatcher.Async(delegate
						{
							_submoduleSubrepoPaths = submodulePaths;
							_workspace.PreferredSubrepoPath = _workspace.SelectedSubrepo?.Path ?? _workspace.PreferredSubrepoPath;
							_workspace.Subrepos = CreateSubrepoItems(paths, _workspace.Path);
							EnsureVisibleSubrepos();
							RebuildSubrepoTabs();
							RefreshSubreposTitle();
							RefreshSubrepoRuntimeState();
							SaveSettings();
						});
					}
					else
					{
						Dispatcher.Async(RefreshLoadedSubrepoControls);
						Dispatcher.Async(delegate
						{
							RefreshSubrepoRuntimeState();
						});
					}
				}
			});
		}

		private static bool ShouldRescanSubreposAfterCommand(string[] args)
		{
			return args.FirstItem() == "sync";
		}

		private void SaveCommandHistory(string commandText)
		{
			if (string.IsNullOrWhiteSpace(commandText))
			{
				return;
			}
			ForkPlusSettings.GitMmSettings settings = ForkPlusSettings.Default.GitMm;
			string[] commandHistory = new string[1] { commandText }
				.Concat(settings.CommandHistory ?? new string[0])
				.Where((string command) => !string.IsNullOrWhiteSpace(command))
				.Distinct(StringComparer.OrdinalIgnoreCase)
				.Take(20)
				.ToArray();
			ForkPlusSettings.Default.GitMm = new ForkPlusSettings.GitMmSettings(
				settings.Workspaces,
				settings.ActiveWorkspace,
				settings.ActiveSubrepo,
				settings.ActiveSubrepos,
				settings.SubrepoOrders,
				settings.VisibleSubrepos,
				settings.CommandOutputCollapsed,
				settings.CommandOutputHeight,
				commandHistory,
				settings.UploadLinks,
				settings.UploadLinksByWorkspace,
				settings.SyncJobs,
				settings.StartBranch,
				settings.InitUrl,
				settings.InitManifest,
				settings.InitBranch,
				settings.InitGroup,
				settings.DialogOptions);
			ForkPlusSettings.Default.Save();
		}

		private void SaveUploadLinks(string[] links)
		{
			if (links == null || links.Length == 0)
			{
				return;
			}
			ForkPlusSettings.GitMmSettings settings = ForkPlusSettings.Default.GitMm;
			Dictionary<string, string[]> uploadLinksByWorkspace = new Dictionary<string, string[]>(settings.UploadLinksByWorkspace, StringComparer.OrdinalIgnoreCase);
			Dictionary<string, string> dialogOptions = new Dictionary<string, string>(settings.DialogOptions, StringComparer.OrdinalIgnoreCase);
			string[] uploadLinks = links
				.Concat(settings.GetUploadLinks(_workspace.Path))
				.Select(CleanUrl)
				.Where((string link) => TryCreateHttpUri(link, out _))
				.Distinct(StringComparer.OrdinalIgnoreCase)
				.Take(20)
				.ToArray();
			uploadLinksByWorkspace[_workspace.Path] = uploadLinks;
			dialogOptions[UploadLinksCollapsedOptionKey()] = "false";
			ForkPlusSettings.Default.GitMm = new ForkPlusSettings.GitMmSettings(
				settings.Workspaces,
				settings.ActiveWorkspace,
				settings.ActiveSubrepo,
				settings.ActiveSubrepos,
				settings.SubrepoOrders,
				settings.VisibleSubrepos,
				settings.CommandOutputCollapsed,
				settings.CommandOutputHeight,
				settings.CommandHistory,
				uploadLinks,
				uploadLinksByWorkspace,
				settings.SyncJobs,
				settings.StartBranch,
				settings.InitUrl,
				settings.InitManifest,
				settings.InitBranch,
				settings.InitGroup,
				dialogOptions);
			ForkPlusSettings.Default.Save();
			RefreshUploadLinksPanel(uploadLinks);
		}

		private void RefreshLoadedSubrepoControls()
		{
			foreach (GitMmSubrepoItem subrepo in _workspace.Subrepos)
			{
				if (subrepo.RepositoryControl is RepositoryUserControl repositoryUserControl)
				{
					repositoryUserControl.InvalidateAndRefresh(SubDomain.DefaultRefresh);
				}
			}
		}

		private void RunBackground(string title, Action<JobMonitor> action)
		{
			_activeJob?.Monitor.Cancel();
			SetBusy(isBusy: true);
			Job job = null;
			job = _jobQueue.Add(title, delegate(JobMonitor monitor)
			{
				try
				{
					action(monitor);
				}
				catch (Exception ex)
				{
					Dispatcher.Async(delegate
					{
						AppendOutput(ex.ToString());
						SetStatus(ex.Message);
					});
				}
				finally
				{
					Dispatcher.Async(delegate
					{
						if (_activeJob == job)
						{
							_activeJob = null;
						}
						SetBusy(isBusy: false);
					});
				}
			});
			_activeJob = job;
		}

		private void CancelStatusRefresh()
		{
			_activeStatusRefreshJob?.Monitor.Cancel();
			_activeStatusRefreshJob = null;
			_runtimeStateRequestId++;
		}

		private void SetBusy(bool isBusy)
		{
			_isBusy = isBusy;
			BusyProgressBar.Visibility = isBusy ? Visibility.Visible : Visibility.Collapsed;
			StartButton.IsEnabled = !isBusy;
			SyncButton.IsEnabled = !isBusy;
			UploadButton.IsEnabled = !isBusy;
			CancelCommandButton.Visibility = isBusy ? Visibility.Visible : Visibility.Collapsed;
			SubreposTabControl.IsEnabled = !isBusy;
			SubrepoFilterButton.IsEnabled = !isBusy;
		}

		private void SetStatus(string text)
		{
			StatusTextBlock.Text = text ?? "";
		}

		private void CancelCommandButton_Click(object sender, RoutedEventArgs e)
		{
			_activeJob?.Monitor.Cancel();
			SetStatus(Translate("Canceling..."));
		}

		private void ToggleCommandOutputButton_Click(object sender, RoutedEventArgs e)
		{
			SetCommandOutputCollapsed(CommandOutputPanel.Visibility == Visibility.Visible, save: true);
		}

		private void SetCommandOutputCollapsed(bool isCollapsed, bool save)
		{
			if (isCollapsed)
			{
				if (RootGrid.RowDefinitions[2].ActualHeight > 0.0)
				{
					_expandedCommandOutputHeight = new GridLength(RootGrid.RowDefinitions[2].ActualHeight);
				}
				CommandOutputPanel.Collapse();
				CommandOutputGridSplitter.Collapse();
				ExpandCommandOutputButton.Show();
				RootGrid.RowDefinitions[1].Height = GridLength.Auto;
				RootGrid.RowDefinitions[2].Height = new GridLength(0.0);
			}
			else
			{
				CommandOutputPanel.Show();
				CommandOutputGridSplitter.Show();
				ExpandCommandOutputButton.Collapse();
				RootGrid.RowDefinitions[1].Height = GridLength.Auto;
				RootGrid.RowDefinitions[2].Height = _expandedCommandOutputHeight.Value > 0.0 ? _expandedCommandOutputHeight : new GridLength(150.0);
			}
			if (save)
			{
				SaveSettings();
			}
		}

		private bool IsCommandOutputCollapsed()
		{
			return CommandOutputPanel.Visibility != Visibility.Visible;
		}

		private double CommandOutputHeight()
		{
			if (!IsCommandOutputCollapsed() && RootGrid.RowDefinitions[2].ActualHeight > 0.0)
			{
				return RootGrid.RowDefinitions[2].ActualHeight;
			}
			return _expandedCommandOutputHeight.Value > 0.0 ? _expandedCommandOutputHeight.Value : 150.0;
		}

		private void RestoreSettings()
		{
			_restoringSettings = true;
			try
			{
				ForkPlusSettings.GitMmSettings settings = ForkPlusSettings.Default.GitMm;
				_workspace.PreferredSubrepoPath = settings.GetActiveSubrepo(_workspace.Path);
				_activeSummaryFilterMode = settings.GetDialogOption(SummaryFilterOptionKey(), null);
				if (_activeSummaryFilterMode == "changed")
				{
					_activeSummaryFilterMode = null;
				}
				string[] visibleSubrepoPaths = settings.GetVisibleSubrepos(_workspace.Path);
				if (visibleSubrepoPaths != null)
				{
					_visibleSubrepoPaths = new HashSet<string>(visibleSubrepoPaths.Select(NormalizePath).Where((string path) => !string.IsNullOrWhiteSpace(path)), StringComparer.OrdinalIgnoreCase);
					_knownSubrepoPaths = new HashSet<string>(_visibleSubrepoPaths, StringComparer.OrdinalIgnoreCase);
					_hasPersistedVisibleSubrepoFilter = true;
				}
				_expandedCommandOutputHeight = new GridLength(settings.CommandOutputHeight);
				SetCommandOutputCollapsed(settings.CommandOutputCollapsed, save: false);
				RefreshUploadLinksPanel(settings.GetUploadLinks(_workspace.Path), autoHide: false);
				if (UploadLinksCollapsed())
				{
					HideUploadLinksPanel(save: false);
				}
			}
			finally
			{
				_restoringSettings = false;
			}
			RefreshSubrepos();
		}

		private void SaveSettings()
		{
			if (_restoringSettings)
			{
				return;
			}
			_saveSettingsAction.InvokeWithDelay(null);
		}

		private void SaveSettingsImmediate()
		{
			if (_restoringSettings)
			{
				return;
			}
			string[] workspaces = (ForkPlusSettings.Default.GitMm.Workspaces ?? new string[0])
				.Concat(new string[1] { _workspace.Path })
				.Where((string path) => !string.IsNullOrWhiteSpace(path))
				.Distinct(StringComparer.OrdinalIgnoreCase)
				.ToArray();
			Dictionary<string, string> activeSubrepos = new Dictionary<string, string>(ForkPlusSettings.Default.GitMm.ActiveSubrepos, StringComparer.OrdinalIgnoreCase);
			if (_workspace.SelectedSubrepo?.Path != null)
			{
				activeSubrepos[_workspace.Path] = _workspace.SelectedSubrepo.Path;
			}
			Dictionary<string, string[]> subrepoOrders = new Dictionary<string, string[]>(ForkPlusSettings.Default.GitMm.SubrepoOrders, StringComparer.OrdinalIgnoreCase);
			if (_workspace.Subrepos.Count > 0)
			{
				subrepoOrders[_workspace.Path] = _workspace.Subrepos.Map((GitMmSubrepoItem subrepo) => subrepo.Path);
			}
			Dictionary<string, string[]> visibleSubrepos = new Dictionary<string, string[]>(ForkPlusSettings.Default.GitMm.VisibleSubrepos, StringComparer.OrdinalIgnoreCase);
			if (_visibleSubrepoPaths != null)
			{
				visibleSubrepos[_workspace.Path] = _workspace.Subrepos
					.Where(IsSubrepoVisible)
					.Select((GitMmSubrepoItem subrepo) => subrepo.Path)
					.ToArray();
			}
			ForkPlusSettings.Default.GitMm = new ForkPlusSettings.GitMmSettings(
				workspaces,
				_workspace.Path,
				_workspace.SelectedSubrepo?.Path,
				activeSubrepos,
				subrepoOrders,
				visibleSubrepos,
				IsCommandOutputCollapsed(),
				CommandOutputHeight(),
				ForkPlusSettings.Default.GitMm.CommandHistory,
				ForkPlusSettings.Default.GitMm.UploadLinks,
				ForkPlusSettings.Default.GitMm.UploadLinksByWorkspace,
				ForkPlusSettings.Default.GitMm.SyncJobs,
				ForkPlusSettings.Default.GitMm.StartBranch,
				ForkPlusSettings.Default.GitMm.InitUrl,
				ForkPlusSettings.Default.GitMm.InitManifest,
				ForkPlusSettings.Default.GitMm.InitBranch,
				ForkPlusSettings.Default.GitMm.InitGroup,
				SaveSummaryFilterMode(ForkPlusSettings.Default.GitMm.DialogOptions));
			ForkPlusSettings.Default.Save();
		}

		private Dictionary<string, string> SaveSummaryFilterMode(Dictionary<string, string> existingOptions)
		{
			Dictionary<string, string> dialogOptions = new Dictionary<string, string>(existingOptions, StringComparer.OrdinalIgnoreCase);
			string key = SummaryFilterOptionKey();
			if (string.IsNullOrWhiteSpace(_activeSummaryFilterMode))
			{
				dialogOptions.Remove(key);
			}
			else
			{
				dialogOptions[key] = _activeSummaryFilterMode;
			}
			return dialogOptions;
		}

		private string SummaryFilterOptionKey()
		{
			return "summaryFilter:" + (NormalizePath(_workspace.Path) ?? _workspace.Path ?? "");
		}

		private bool UploadLinksCollapsed()
		{
			return string.Equals(ForkPlusSettings.Default.GitMm.GetDialogOption(UploadLinksCollapsedOptionKey(), "false"), "true", StringComparison.OrdinalIgnoreCase);
		}

		private void SaveUploadLinksCollapsed(bool isCollapsed)
		{
			ForkPlusSettings.GitMmSettings settings = ForkPlusSettings.Default.GitMm;
			Dictionary<string, string> dialogOptions = new Dictionary<string, string>(settings.DialogOptions, StringComparer.OrdinalIgnoreCase);
			dialogOptions[UploadLinksCollapsedOptionKey()] = isCollapsed ? "true" : "false";
			ForkPlusSettings.Default.GitMm = new ForkPlusSettings.GitMmSettings(
				settings.Workspaces,
				settings.ActiveWorkspace,
				settings.ActiveSubrepo,
				settings.ActiveSubrepos,
				settings.SubrepoOrders,
				settings.VisibleSubrepos,
				settings.CommandOutputCollapsed,
				settings.CommandOutputHeight,
				settings.CommandHistory,
				settings.UploadLinks,
				settings.UploadLinksByWorkspace,
				settings.SyncJobs,
				settings.StartBranch,
				settings.InitUrl,
				settings.InitManifest,
				settings.InitBranch,
				settings.InitGroup,
				dialogOptions);
			ForkPlusSettings.Default.Save();
		}

		private string UploadLinksCollapsedOptionKey()
		{
			return "uploadLinksCollapsed:" + (NormalizePath(_workspace.Path) ?? _workspace.Path ?? "");
		}

		private void Workspace_PropertyChanged(object sender, PropertyChangedEventArgs e)
		{
			if (e.PropertyName == nameof(GitMmWorkspaceItem.SelectedSubrepo))
			{
				SaveSettings();
			}
		}

		private void RepositoryNameChanged(object sender, EventArgs<string> e)
		{
			foreach (TabItem tabItem in SubreposTabControl.Items.OfType<TabItem>())
			{
				if (tabItem.Tag is GitMmSubrepoItem subrepo && IsSamePath(subrepo.Path, e.Value))
				{
					RefreshSubrepoTabHeader(tabItem, subrepo);
					if (subrepo == _workspace.SelectedSubrepo)
					{
						NotificationCenter.Current.RaiseActiveTabChanged(this, MainWindow.Instance?.TabManager.ActiveTab);
					}
				}
			}
		}

		private void RepositoryColorChanged(object sender, EventArgs<RepositoryManager.Repository> e)
		{
			foreach (TabItem tabItem in SubreposTabControl.Items.OfType<TabItem>())
			{
				if (tabItem.Tag is GitMmSubrepoItem subrepo && IsSamePath(subrepo.Path, e.Value.Path))
				{
					RefreshSubrepoTabHeader(tabItem, subrepo);
					break;
				}
			}
		}

		private void SubreposTabControl_SelectionChanged(object sender, SelectionChangedEventArgs e)
		{
			if (_isBusy)
			{
				return;
			}
			CancelStatusRefresh();
			if (SubreposTabControl.SelectedItem is TabItem tabItem && tabItem.Tag is GitMmSubrepoItem subrepo)
			{
				_workspace.SelectedSubrepo = subrepo;
				EnsureSubrepoContent(tabItem, subrepo);
				NotificationCenter.Current.RaiseActiveTabChanged(this, MainWindow.Instance?.TabManager.ActiveTab);
			}
		}

		private void RebuildSubrepoTabs()
		{
			SubreposTabControl.Items.Clear();
			TabItem tabToSelect = null;
			string preferredSubrepoPath = GetPreferredSubrepoPath();
			foreach (GitMmSubrepoItem subrepo in _workspace.Subrepos.Where(IsSubrepoVisible))
			{
				TabItem tabItem = new TabItem
				{
					Header = CreateSubrepoTabHeader(subrepo),
					Content = CreateSubrepoPlaceholder(subrepo),
					Tag = subrepo,
					ToolTip = subrepo.Path,
					ContextMenu = CreateSubrepoTabContextMenu(subrepo),
					AllowDrop = true,
					HorizontalContentAlignment = HorizontalAlignment.Stretch,
					VerticalContentAlignment = VerticalAlignment.Stretch
				};
				tabItem.PreviewMouseDown += SubrepoTabItem_PreviewMouseDown;
				tabItem.PreviewMouseMove += SubrepoTabItem_PreviewMouseMove;
				tabItem.PreviewMouseUp += SubrepoTabItem_PreviewMouseUp;
				tabItem.Drop += SubrepoTabItem_Drop;
				SubreposTabControl.Items.Add(tabItem);
				if (IsSamePath(subrepo.Path, preferredSubrepoPath))
				{
					tabToSelect = tabItem;
				}
			}
			if (tabToSelect == null && SubreposTabControl.Items.Count > 0)
			{
				tabToSelect = SubreposTabControl.Items[0] as TabItem;
			}
			if (tabToSelect != null && tabToSelect.Tag is GitMmSubrepoItem selectedSubrepo)
			{
				SubreposTabControl.SelectedItem = tabToSelect;
				_workspace.SelectedSubrepo = selectedSubrepo;
				EnsureSubrepoContent(tabToSelect, selectedSubrepo);
				NotificationCenter.Current.RaiseActiveTabChanged(this, MainWindow.Instance?.TabManager.ActiveTab);
			}
			else
			{
				_workspace.SelectedSubrepo = null;
				NotificationCenter.Current.RaiseActiveTabChanged(this, MainWindow.Instance?.TabManager.ActiveTab);
			}
			UpdateSubrepoTabWidths();
		}

		private void UpdateSubrepoTabWidths()
		{
			int tabCount = SubreposTabControl.Items.Count;
			if (tabCount <= 0)
			{
				return;
			}
			double availableWidth = SubreposTabControl.ActualWidth;
			if (availableWidth <= 0.0)
			{
				return;
			}
			double tabWidth = Math.Max(SubrepoTabMinWidth, Math.Floor(availableWidth / tabCount));
			foreach (TabItem tabItem in SubreposTabControl.Items.OfType<TabItem>())
			{
				tabItem.MinWidth = SubrepoTabMinWidth;
				tabItem.Width = tabWidth;
			}
		}

		private void EnsureVisibleSubrepos()
		{
			HashSet<string> currentPaths = new HashSet<string>(_workspace.Subrepos
				.Select((GitMmSubrepoItem subrepo) => NormalizePath(subrepo.Path))
				.Where((string path) => !string.IsNullOrWhiteSpace(path)), StringComparer.OrdinalIgnoreCase);
			HashSet<string> visibleSubrepoPaths = _visibleSubrepoPaths;
			if (visibleSubrepoPaths == null)
			{
				_visibleSubrepoPaths = new HashSet<string>(currentPaths, StringComparer.OrdinalIgnoreCase);
				_knownSubrepoPaths = currentPaths;
				return;
			}
			visibleSubrepoPaths.RemoveWhere((string path) => !currentPaths.Contains(path));
			if (!_hasPersistedVisibleSubrepoFilter)
			{
				foreach (string path in currentPaths)
				{
					if (!_knownSubrepoPaths.Contains(path))
					{
						visibleSubrepoPaths.Add(path);
					}
				}
			}
			_knownSubrepoPaths = currentPaths;
		}

		private bool IsSubrepoVisible(GitMmSubrepoItem subrepo)
		{
			string path = NormalizePath(subrepo.Path);
			return path != null && (_visibleSubrepoPaths == null || _visibleSubrepoPaths.Contains(path));
		}

		private void SetSubrepoVisible(GitMmSubrepoItem subrepo, bool isVisible)
		{
			EnsureVisibleSubrepos();
			string path = NormalizePath(subrepo.Path);
			if (path == null)
			{
				return;
			}
			if (isVisible)
			{
				_visibleSubrepoPaths.Add(path);
			}
			else
			{
				_visibleSubrepoPaths.Remove(path);
			}
			_hasPersistedVisibleSubrepoFilter = true;
		}

		private void SubrepoFilterButton_Click(object sender, RoutedEventArgs e)
		{
			EnsureVisibleSubrepos();
			Popup popup = new Popup
			{
				PlacementTarget = SubrepoFilterButton,
				Placement = PlacementMode.Bottom,
				StaysOpen = false,
				AllowsTransparency = true
			};
			StackPanel itemsPanel = new StackPanel();
			TextBox searchTextBox = new TextBox
			{
				Width = 210.0,
				Height = 30.0,
				Margin = new Thickness(8.0),
				Padding = new Thickness(6.0, 4.0, 6.0, 4.0),
				ToolTip = Translate("Search repositories")
			};
			CheckBox nonDefaultBranchCheckBox = CreateSubrepoQuickFilterCheckBox(Translate("Non-default branch"), _filterNonDefaultBranchOnly);
			CheckBox failedOnlyCheckBox = CreateSubrepoQuickFilterCheckBox(Translate("Failed repositories"), _filterFailedOnly);
			Button showAllButton = CreateSubrepoFilterIconButton("StageAllIcon", Translate("Show all repositories"), new Thickness(0.0, 8.0, 8.0, 8.0));
			showAllButton.Click += delegate
			{
				_activeSummaryFilterMode = null;
				EnsureVisibleSubrepos();
				foreach (GitMmSubrepoItem subrepo in _workspace.Subrepos)
				{
					SetSubrepoVisible(subrepo, isVisible: true);
				}
				RebuildSubrepoTabs();
				RefreshSubreposTitle();
				SaveSettings();
				RefreshSubrepoFilterMenuItems(itemsPanel, searchTextBox.Text);
			};
			Button invertSelectionButton = CreateSubrepoFilterIconButton("SwapIcon", Translate("Invert repository selection"), new Thickness(0.0, 8.0, 4.0, 8.0));
			invertSelectionButton.Click += delegate
			{
				_activeSummaryFilterMode = null;
				EnsureVisibleSubrepos();
				foreach (GitMmSubrepoItem subrepo in _workspace.Subrepos)
				{
					SetSubrepoVisible(subrepo, !IsSubrepoVisible(subrepo));
				}
				RebuildSubrepoTabs();
				RefreshSubreposTitle();
				SaveSettings();
				RefreshSubrepoFilterMenuItems(itemsPanel, searchTextBox.Text);
			};
			DockPanel searchPanel = new DockPanel();
			DockPanel.SetDock(showAllButton, Dock.Right);
			DockPanel.SetDock(invertSelectionButton, Dock.Right);
			searchPanel.Children.Add(showAllButton);
			searchPanel.Children.Add(invertSelectionButton);
			searchPanel.Children.Add(searchTextBox);
			StackPanel quickFiltersPanel = new StackPanel
			{
				Margin = new Thickness(8.0, 0.0, 8.0, 8.0)
			};
			quickFiltersPanel.Children.Add(nonDefaultBranchCheckBox);
			quickFiltersPanel.Children.Add(failedOnlyCheckBox);
			nonDefaultBranchCheckBox.Checked += delegate
			{
				_filterNonDefaultBranchOnly = true;
				_activeSummaryFilterMode = "nonDefault";
				TryApplySummaryFilterMode("nonDefault", save: true);
				RefreshSubrepoFilterMenuItems(itemsPanel, searchTextBox.Text);
			};
			nonDefaultBranchCheckBox.Unchecked += delegate
			{
				_filterNonDefaultBranchOnly = false;
				if (_activeSummaryFilterMode == "nonDefault")
				{
					_activeSummaryFilterMode = null;
				}
				RefreshSubrepoFilterMenuItems(itemsPanel, searchTextBox.Text);
				SaveSettings();
			};
			failedOnlyCheckBox.Checked += delegate { _filterFailedOnly = true; RefreshSubrepoFilterMenuItems(itemsPanel, searchTextBox.Text); };
			failedOnlyCheckBox.Unchecked += delegate { _filterFailedOnly = false; RefreshSubrepoFilterMenuItems(itemsPanel, searchTextBox.Text); };
			Border popupContent = new Border
			{
				Child = new StackPanel
				{
					Children =
					{
						searchPanel,
						quickFiltersPanel,
						new Separator(),
						new ScrollViewer
						{
							MaxHeight = 360.0,
							VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
							Content = itemsPanel
						}
					}
				},
				BorderThickness = new Thickness(1.0),
				Padding = new Thickness(0.0)
			};
			popupContent.SetResourceReference(Border.BackgroundProperty, "BackgroundBrush");
			popupContent.SetResourceReference(Border.BorderBrushProperty, "BorderBrush");
			popup.Child = popupContent;
			searchTextBox.TextChanged += delegate
			{
				RefreshSubrepoFilterMenuItems(itemsPanel, searchTextBox.Text);
			};
			popup.Opened += delegate
			{
				searchTextBox.Focus();
			};
			RefreshSubrepoFilterMenuItems(itemsPanel, "");
			popup.IsOpen = true;
		}

		private static CheckBox CreateSubrepoQuickFilterCheckBox(string text, bool isChecked)
		{
			return new CheckBox
			{
				Content = text,
				IsChecked = isChecked,
				Margin = new Thickness(0.0, 2.0, 0.0, 2.0)
			};
		}

		private static Button CreateSubrepoFilterIconButton(string iconResourceKey, string tooltip, Thickness margin)
		{
			Image image = new Image
			{
				Width = 16.0,
				Height = 16.0,
				VerticalAlignment = VerticalAlignment.Center
			};
			image.SetResourceReference(Image.SourceProperty, iconResourceKey);
			return new Button
			{
				Content = image,
				Width = 28.0,
				Height = 30.0,
				Margin = margin,
				Padding = new Thickness(4.0),
				ToolTip = tooltip,
				Style = Theme.TransparentButtonStyle
			};
		}

		private void RefreshSubrepoFilterMenuItems(StackPanel itemsPanel, string filterText)
		{
			itemsPanel.Children.Clear();
			foreach (GitMmSubrepoItem subrepo in FilterSubrepos(filterText))
			{
				CheckBox checkBox = new CheckBox
				{
					Content = new TextBlock
					{
						Text = subrepo.DisplayName
					},
					IsChecked = IsSubrepoVisible(subrepo),
					ToolTip = subrepo.Path,
					Margin = new Thickness(8.0, 3.0, 8.0, 3.0)
				};
				checkBox.Checked += delegate
				{
					_activeSummaryFilterMode = null;
					SetSubrepoVisible(subrepo, isVisible: true);
					RebuildSubrepoTabs();
					RefreshSubreposTitle();
					SaveSettings();
				};
				checkBox.Unchecked += delegate
				{
					_activeSummaryFilterMode = null;
					SetSubrepoVisible(subrepo, isVisible: false);
					RebuildSubrepoTabs();
					RefreshSubreposTitle();
					SaveSettings();
				};
				itemsPanel.Children.Add(checkBox);
			}
		}

		private IEnumerable<GitMmSubrepoItem> FilterSubrepos(string filterText)
		{
			IEnumerable<GitMmSubrepoItem> subrepos = _workspace.Subrepos;
			if (_filterNonDefaultBranchOnly)
			{
				subrepos = subrepos.Where((GitMmSubrepoItem subrepo) => subrepo.IsNonDefaultBranch);
			}
			if (_filterFailedOnly)
			{
				subrepos = subrepos.Where((GitMmSubrepoItem subrepo) => subrepo.CommandState == GitMmSubrepoCommandState.Failed);
			}
			if (string.IsNullOrWhiteSpace(filterText))
			{
				return subrepos;
			}
			return subrepos.Where(delegate(GitMmSubrepoItem subrepo)
			{
				return subrepo.DisplayName.IndexOf(filterText, StringComparison.OrdinalIgnoreCase) >= 0
					|| subrepo.Path.IndexOf(filterText, StringComparison.OrdinalIgnoreCase) >= 0;
			});
		}

		private void EnsureSubrepoContent(TabItem tabItem, GitMmSubrepoItem subrepo)
		{
			if (subrepo.RepositoryControl == null)
			{
				subrepo.RepositoryControl = CreateRepositoryContent(subrepo.Path);
			}
			if (tabItem.Content != subrepo.RepositoryControl)
			{
				tabItem.Content = subrepo.RepositoryControl;
			}
			RefreshSubrepoSummary();
		}

		private static FrameworkElement CreateSubrepoPlaceholder(GitMmSubrepoItem subrepo)
		{
			return new TextBlock
			{
				Text = subrepo.DisplayName,
				Margin = new Thickness(10.0),
				Foreground = Application.Current.TryFindResource("SecondaryLabelBrush") as Brush
			};
		}

		private void SubreposTabControl_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
		{
			ScrollViewer scrollViewer = FindVisualChild<ScrollViewer>(SubreposTabControl);
			if (scrollViewer == null || scrollViewer.ScrollableWidth <= 0)
			{
				return;
			}
			scrollViewer.ScrollToHorizontalOffset(scrollViewer.HorizontalOffset - e.Delta);
			e.Handled = true;
		}

		[Null]
		private string GetPreferredSubrepoPath()
		{
			return ForkPlusSettings.Default.GitMm.GetActiveSubrepo(_workspace.Path)
				?? _workspace.SelectedSubrepo?.Path
				?? _workspace.PreferredSubrepoPath;
		}

		private void SubrepoTabItem_PreviewMouseDown(object sender, MouseButtonEventArgs e)
		{
			_subrepoTabDragItem = null;
			if (e.LeftButton == MouseButtonState.Pressed && sender is TabItem tabItem && IsFromSubrepoTabHeader(tabItem, e.OriginalSource as DependencyObject))
			{
				_tabDragStartPoint = e.GetPosition(null);
				_subrepoTabDragItem = tabItem;
			}
		}

		private void SubrepoTabItem_PreviewMouseMove(object sender, MouseEventArgs e)
		{
			if (Mouse.PrimaryDevice.LeftButton != MouseButtonState.Pressed || !(sender is TabItem tabItem))
			{
				return;
			}
			if (_subrepoTabDragItem != tabItem)
			{
				return;
			}
			Point currentPoint = e.GetPosition(null);
			if (Math.Abs(_tabDragStartPoint.X - currentPoint.X) < SystemParameters.MinimumHorizontalDragDistance
				&& Math.Abs(_tabDragStartPoint.Y - currentPoint.Y) < SystemParameters.MinimumVerticalDragDistance)
			{
				return;
			}
			try
			{
				DragDrop.DoDragDrop(tabItem, new WeakReference<TabItem>(tabItem), DragDropEffects.Move);
			}
			finally
			{
				_subrepoTabDragItem = null;
			}
		}

		private void SubrepoTabItem_PreviewMouseUp(object sender, MouseButtonEventArgs e)
		{
			_subrepoTabDragItem = null;
		}

		private void SubrepoTabItem_Drop(object sender, DragEventArgs e)
		{
			if (!(sender is TabItem targetTabItem) || !(e.Data.GetData(typeof(WeakReference<TabItem>)) is WeakReference<TabItem> weakReference) || !weakReference.TryGetTarget(out var draggedTabItem))
			{
				return;
			}
			if (!IsFromSubrepoTabHeader(targetTabItem, e.OriginalSource as DependencyObject) && e.GetPosition(targetTabItem).Y > targetTabItem.ActualHeight)
			{
				return;
			}
			if (draggedTabItem == targetTabItem)
			{
				return;
			}
			int oldIndex = SubreposTabControl.Items.IndexOf(draggedTabItem);
			int newIndex = SubreposTabControl.Items.IndexOf(targetTabItem);
			if (oldIndex < 0 || newIndex < 0)
			{
				return;
			}
			if (e.GetPosition(targetTabItem).X > targetTabItem.ActualWidth / 2.0)
			{
				newIndex++;
			}
			if (oldIndex < newIndex)
			{
				newIndex--;
			}
			newIndex = Math.Max(0, Math.Min(SubreposTabControl.Items.Count - 1, newIndex));
			if (oldIndex == newIndex)
			{
				return;
			}
			SubreposTabControl.Items.Remove(draggedTabItem);
			SubreposTabControl.Items.Insert(newIndex, draggedTabItem);
			SubreposTabControl.SelectedItem = draggedTabItem;
			SaveSubrepoOrder();
			e.Handled = true;
		}

		private static bool IsFromSubrepoTabHeader(TabItem tabItem, DependencyObject source)
		{
			if (tabItem?.Header is DependencyObject header)
			{
				for (DependencyObject current = source; current != null; current = GetParentObject(current))
				{
					if (current == header)
					{
						return true;
					}
				}
			}
			return false;
		}

		[Null]
		private static DependencyObject GetParentObject(DependencyObject source)
		{
			if (source == null)
			{
				return null;
			}
			if (source is ContentElement contentElement)
			{
				DependencyObject parent = ContentOperations.GetParent(contentElement);
				if (parent != null)
				{
					return parent;
				}
				return (contentElement as FrameworkContentElement)?.Parent;
			}
			if (source is Visual || source is Visual3D)
			{
				return VisualTreeHelper.GetParent(source);
			}
			return null;
		}

		private void SaveSubrepoOrder()
		{
			List<GitMmSubrepoItem> reorderedSubrepos = SubreposTabControl.Items.OfType<TabItem>()
				.Select((TabItem item) => item.Tag as GitMmSubrepoItem)
				.Where((GitMmSubrepoItem item) => item != null)
				.ToList();
			_workspace.SetSubrepos(reorderedSubrepos, selectPreferred: false);
			SaveSettings();
		}

		private static FrameworkElement CreateSubrepoTabHeader(GitMmSubrepoItem subrepo)
		{
			DockPanel panel = new DockPanel
			{
				LastChildFill = true
			};
			Ellipse colorEllipse = new Ellipse
			{
				Width = 8.0,
				Height = 8.0,
				Margin = new Thickness(0.0, 0.0, 6.0, -2.0),
				VerticalAlignment = VerticalAlignment.Center,
				StrokeThickness = 2.0,
				Tag = "ColorEllipse"
			};
			DockPanel.SetDock(colorEllipse, Dock.Left);
			panel.Children.Add(colorEllipse);
			StackPanel statusPanel = new StackPanel
			{
				Height = 22.0,
				Margin = new Thickness(6.0, 2.0, 0.0, 0.0),
				VerticalAlignment = VerticalAlignment.Center,
				Orientation = Orientation.Horizontal,
				Tag = "StatusIcons"
			};
			DockPanel.SetDock(statusPanel, Dock.Right);
			panel.Children.Add(statusPanel);
			panel.Children.Add(new EditableTextBlock
			{
				Value = subrepo.DisplayName,
				Height = 22.0,
				Padding = new Thickness(0.0, 2.0, 0.0, 2.0),
				HorizontalAlignment = HorizontalAlignment.Left,
				MaxWidth = 240.0,
				Tag = "Title"
			});
			RefreshSubrepoTabHeader(panel, subrepo);
			return panel;
		}

		private ContextMenu CreateSubrepoTabContextMenu(GitMmSubrepoItem subrepo)
		{
			ContextMenu contextMenu = new ContextMenu();
			MenuItem renameMenuItem = new MenuItem
			{
				Header = PreferencesLocalization.MenuHeader("Rename")
			};
			renameMenuItem.Click += delegate
			{
				TabItem tabItem = SubreposTabControl.Items.OfType<TabItem>().FirstOrDefault((TabItem item) => item.Tag == subrepo);
				EditableTextBlock editableTextBlock = FindSubrepoHeaderTitle(tabItem);
				if (editableTextBlock != null)
				{
					editableTextBlock.ShowEditor(subrepo.BaseDisplayName, delegate(bool success, string newName)
					{
						editableTextBlock.HideEditor();
						if (success)
						{
							RenameSubrepo(subrepo, newName);
						}
					});
				}
			};
			contextMenu.Items.Add(renameMenuItem);
			MenuItem hideMenuItem = new MenuItem
			{
				Header = PreferencesLocalization.MenuHeader("Hide")
			};
			hideMenuItem.Click += delegate
			{
				SetSubrepoVisible(subrepo, isVisible: false);
				RebuildSubrepoTabs();
				RefreshSubreposTitle();
				SaveSettings();
			};
			contextMenu.Items.Add(hideMenuItem);
			contextMenu.Items.Add(new Separator());
			contextMenu.Items.Add(CreateSubrepoColorsMenuItem(subrepo));
			return contextMenu;
		}

		private static Control CreateSubrepoColorsMenuItem(GitMmSubrepoItem subrepo)
		{
			RepositoryManager.Repository repository = EnsureRepositoryManagerEntry(subrepo.Path);
			return new MenuItem
			{
				Header = new RepositoryColorsUserControl(repository),
				Style = Theme.CustomContentMenuItemStyle
			};
		}

		private void RenameSubrepo(GitMmSubrepoItem subrepo, string newName)
		{
			if (string.IsNullOrWhiteSpace(newName))
			{
				return;
			}
			RepositoryManager.Instance.RenameRepository(subrepo.Path, newName);
			RepositoryManager.Instance.Save();
			NotificationCenter.Current.RaiseRepositoryNameChanged(this, PathHelper.Normalize(subrepo.Path));
		}

		private static void RefreshSubrepoTabHeader(TabItem tabItem, GitMmSubrepoItem subrepo)
		{
			if (tabItem.Header is DockPanel panel)
			{
				RefreshSubrepoTabHeader(panel, subrepo);
			}
			else
			{
				tabItem.Header = CreateSubrepoTabHeader(subrepo);
			}
		}

		private static void RefreshSubrepoTabHeader(DockPanel panel, GitMmSubrepoItem subrepo)
		{
			EditableTextBlock title = panel.Children.OfType<EditableTextBlock>().FirstOrDefault();
			if (title != null)
			{
				title.Value = subrepo.DisplayName;
				title.FontWeight = subrepo.IsRootRepository ? FontWeights.Bold : FontWeights.Normal;
			}
			Ellipse colorEllipse = panel.Children.OfType<Ellipse>().FirstOrDefault();
			if (colorEllipse != null)
			{
				SolidColorBrush brush = RepositoryColorsUserControl.GetBrush(EnsureRepositoryManagerEntry(subrepo.Path).Color);
				if (brush == null && subrepo.IsRootRepository)
				{
					brush = Application.Current.TryFindResource("SystemAccentBrush") as SolidColorBrush ?? Brushes.DodgerBlue;
				}
				colorEllipse.Visibility = brush == null ? Visibility.Collapsed : Visibility.Visible;
				colorEllipse.Width = subrepo.IsRootRepository ? 11.0 : 8.0;
				colorEllipse.Height = subrepo.IsRootRepository ? 11.0 : 8.0;
				colorEllipse.StrokeThickness = subrepo.IsRootRepository ? 1.0 : 2.0;
				colorEllipse.Stroke = brush;
				colorEllipse.Fill = brush;
				colorEllipse.ToolTip = subrepo.IsRootRepository ? Translate("Main repository") : null;
			}
			StackPanel statusPanel = panel.Children.OfType<StackPanel>().FirstOrDefault((StackPanel stackPanel) => (stackPanel.Tag as string) == "StatusIcons");
			if (statusPanel != null)
			{
				RefreshSubrepoStatusIcons(statusPanel, subrepo);
			}
		}

		private void SetCommandStateForVisibleSubrepos(GitMmSubrepoCommandState commandState)
		{
			foreach (GitMmSubrepoItem subrepo in _workspace.Subrepos.Where(IsSubrepoVisible))
			{
				subrepo.CommandState = commandState;
			}
			RefreshSubrepoTabHeaders();
		}

		private void RefreshSubrepoRuntimeState(bool force = false)
		{
			CancelStatusRefresh();
			int requestId = ++_runtimeStateRequestId;
			GitMmSubrepoItem selectedSubrepo = _workspace.SelectedSubrepo;
			GitMmSubrepoItem[] subrepos = _workspace.Subrepos
				.OrderBy((GitMmSubrepoItem subrepo) => selectedSubrepo == null || !IsSamePath(subrepo.Path, selectedSubrepo.Path))
				.ThenBy((GitMmSubrepoItem subrepo) => subrepo.RepositoryControl == null)
				.ThenBy((GitMmSubrepoItem subrepo) => !IsSubrepoVisible(subrepo))
				.ToArray();
			Job job = null;
			job = _jobQueue.Add("git mm status refresh", delegate(JobMonitor monitor)
			{
				Stopwatch stopwatch = Stopwatch.StartNew();
				GitMmSubrepoRuntimeState[] states = new GitMmSubrepoRuntimeState[subrepos.Length];
				try
				{
					for (int i = 0; i < subrepos.Length; i++)
					{
						if (monitor.IsCanceled)
						{
							return;
						}
						monitor.Update(subrepos.Length == 0 ? 100.0 : i * 100.0 / subrepos.Length, PreferencesLocalization.FormatCurrent("Refreshing {0}", subrepos[i].DisplayName));
						if (!force && subrepos[i].RuntimeStateUpdatedAtUtc.HasValue && DateTime.UtcNow - subrepos[i].RuntimeStateUpdatedAtUtc.Value < RuntimeStateCacheTtl)
						{
							continue;
						}
						states[i] = GetSubrepoRuntimeState(subrepos[i], monitor);
					}
				}
				finally
				{
					PerformanceTelemetry.Record("git mm status refresh", stopwatch.ElapsedMilliseconds, backgroundThread: true);
				}
				monitor.Success(Translate("git mm status refresh finished"));
				Dispatcher.Async(delegate
				{
					if (_activeStatusRefreshJob == job)
					{
						_activeStatusRefreshJob = null;
					}
					if (monitor.IsCanceled || requestId != _runtimeStateRequestId)
					{
						return;
					}
					for (int i = 0; i < subrepos.Length && i < states.Length; i++)
					{
						if (states[i] == null)
						{
							continue;
						}
						subrepos[i].HasLocalChanges = states[i].HasLocalChanges;
						subrepos[i].ChangedFilesCount = states[i].ChangedFilesCount;
						subrepos[i].HasConflicts = states[i].HasConflicts;
						subrepos[i].ConflictFilesCount = states[i].ConflictFilesCount;
						subrepos[i].IsNonDefaultBranch = states[i].IsNonDefaultBranch;
						subrepos[i].CurrentBranch = states[i].CurrentBranch;
						subrepos[i].DefaultBranch = states[i].DefaultBranch;
						subrepos[i].AheadCount = states[i].AheadCount;
						subrepos[i].BehindCount = states[i].BehindCount;
						subrepos[i].StagedAdded = states[i].StagedAdded;
						subrepos[i].StagedDeleted = states[i].StagedDeleted;
						subrepos[i].RuntimeStateUpdatedAtUtc = DateTime.UtcNow;
					}
					if (!string.IsNullOrWhiteSpace(_activeSummaryFilterMode) && TryApplySummaryFilterMode(_activeSummaryFilterMode, save: false))
					{
						NotificationCenter.Current.RaiseActiveTabChanged(this, MainWindow.Instance?.TabManager.ActiveTab);
						return;
					}
					RefreshSubrepoTabHeaders();
					RefreshSubrepoSummary();
					NotificationCenter.Current.RaiseActiveTabChanged(this, MainWindow.Instance?.TabManager.ActiveTab);
				});
			}, JobFlags.SaveToLog | JobFlags.Background, showMessageWhenDone: false);
			_activeStatusRefreshJob = job;
		}

		private static GitMmSubrepoRuntimeState GetSubrepoRuntimeState(GitMmSubrepoItem subrepo, JobMonitor monitor)
		{
			GitMmSubrepoRuntimeState state = new GitMmSubrepoRuntimeState();
			GitRequestResult statusResult = RunGit(subrepo.Path, new GitCommand("status", "--porcelain"), monitor);
			state.ConflictFilesCount = statusResult.Success ? CountConflicts(statusResult.Stdout) : 0;
			state.HasConflicts = state.ConflictFilesCount > 0;
			state.ChangedFilesCount = statusResult.Success ? CountVisibleLocalChanges(subrepo.Path, statusResult.Stdout, monitor) : 0;
			state.HasLocalChanges = state.ChangedFilesCount > 0;
			if (monitor.IsCanceled)
			{
				return state;
			}
			GitRequestResult branchResult = RunGit(subrepo.Path, new GitCommand("branch", "--show-current"), monitor);
			state.CurrentBranch = branchResult.Success ? branchResult.Stdout.Trim() : "";
			state.DefaultBranch = GetDefaultBranch(subrepo.Path, monitor);
			state.IsNonDefaultBranch = !string.IsNullOrWhiteSpace(state.CurrentBranch)
				&& !string.IsNullOrWhiteSpace(state.DefaultBranch)
				&& !string.Equals(state.CurrentBranch, state.DefaultBranch, StringComparison.OrdinalIgnoreCase);
			(int ahead, int behind)? aheadBehind = GetAheadBehind(subrepo.Path, monitor);
			if (aheadBehind.HasValue)
			{
				state.AheadCount = aheadBehind.Value.ahead;
				state.BehindCount = aheadBehind.Value.behind;
			}
			(int added, int deleted)? stagedStats = GetStagedDiffStats(subrepo.Path, monitor);
			if (stagedStats.HasValue)
			{
				state.StagedAdded = stagedStats.Value.added;
				state.StagedDeleted = stagedStats.Value.deleted;
			}
			return state;
		}

		private static int CountVisibleLocalChanges(string path, string porcelainStatus, JobMonitor monitor)
		{
			return CountVisibleLocalChanges(path, porcelainStatus, new HashSet<string>(StringComparer.OrdinalIgnoreCase), depth: 0, monitor);
		}

		private static int CountVisibleLocalChanges(string path, string porcelainStatus, HashSet<string> visitedPaths, int depth, JobMonitor monitor)
		{
			if (depth > SubrepoScanDepth || monitor.IsCanceled)
			{
				return 0;
			}
			string normalizedPath = NormalizePath(path);
			if (normalizedPath == null || !visitedPaths.Add(normalizedPath))
			{
				return 0;
			}
			int count = CountPorcelainChangedFiles(porcelainStatus);
			foreach (string submodulePath in GetSubmodulePaths(path, monitor))
			{
				string fullSubmodulePath = System.IO.Path.Combine(path, submodulePath);
				if (IsGitWorkTree(fullSubmodulePath))
				{
					GitRequestResult submoduleStatusResult = RunGit(fullSubmodulePath, new GitCommand("status", "--porcelain"), monitor);
					if (submoduleStatusResult.Success)
					{
						count += CountVisibleLocalChanges(fullSubmodulePath, submoduleStatusResult.Stdout, visitedPaths, depth + 1, monitor);
					}
				}
			}
			return count;
		}

		private static int CountPorcelainChangedFiles(string porcelainStatus)
		{
			int count = 0;
			foreach (string line in porcelainStatus.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n'))
			{
				if (line.Length >= 3)
				{
					count++;
				}
			}
			return count;
		}

		private static IEnumerable<string> GetSubmodulePaths(string path, JobMonitor monitor)
		{
			if (monitor.IsCanceled)
			{
				yield break;
			}
			GitRequestResult result = RunGit(path, new GitCommand("config", "--file", ".gitmodules", "--get-regexp", "path"), monitor);
			if (!result.Success)
			{
				yield break;
			}
			foreach (string line in result.Stdout.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n'))
			{
				string[] parts = line.Split(new char[2] { ' ', '\t' }, 2, StringSplitOptions.RemoveEmptyEntries);
				if (parts.Length == 2 && !string.IsNullOrWhiteSpace(parts[1]))
				{
					yield return parts[1].Trim();
				}
			}
		}

		private static int CountConflicts(string porcelainStatus)
		{
			int count = 0;
			foreach (string line in porcelainStatus.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n'))
			{
				if (line.Length < 2)
				{
					continue;
				}
				string code = line.Substring(0, 2);
				if (code.IndexOf('U') >= 0 || code == "AA" || code == "DD")
				{
					count++;
				}
			}
			return count;
		}

		private static GitRequestResult RunGit(string path, GitCommand command, JobMonitor monitor = null)
		{
			GitRequest request = default(GitRequest)
				.CurrentDir(path)
				.Command(command);
			return monitor == null ? request.Execute(silent: true) : request.Execute(monitor, silent: true, appendOutput: false);
		}

		private static string GetDefaultBranch(string path, JobMonitor monitor = null)
		{
			string normalizedPath = NormalizePath(path);
			if (normalizedPath != null && _defaultBranchCache.TryGetValue(normalizedPath, out Tuple<string, DateTime> cached))
			{
				if (DateTime.UtcNow - cached.Item2 < DefaultBranchCacheTtl)
				{
					return cached.Item1;
				}
				_defaultBranchCache.Remove(normalizedPath);
			}
			GitRequestResult result = RunGit(path, new GitCommand("symbolic-ref", "--short", "refs/remotes/origin/HEAD"), monitor);
			if (result.Success)
			{
				string value = result.Stdout.Trim();
				const string originPrefix = "origin/";
				if (value.StartsWith(originPrefix, StringComparison.OrdinalIgnoreCase))
				{
					value = value.Substring(originPrefix.Length);
				}
				if (!string.IsNullOrWhiteSpace(value))
				{
					if (normalizedPath != null)
					{
						_defaultBranchCache[normalizedPath] = Tuple.Create(value, DateTime.UtcNow);
					}
					return value;
				}
			}
			return "master";
		}

		[Null]
		private static (int ahead, int behind)? GetAheadBehind(string path, JobMonitor monitor = null)
		{
			GitRequestResult result = RunGit(path, new GitCommand("rev-list", "--left-right", "--count", "@{upstream}...HEAD"), monitor);
			if (!result.Success)
			{
				return null;
			}
			string[] parts = result.Stdout.Trim().Split(new char[2] { '\t', ' ' }, StringSplitOptions.RemoveEmptyEntries);
			if (parts.Length < 2 || !int.TryParse(parts[0], out int behind) || !int.TryParse(parts[1], out int ahead))
			{
				return null;
			}
			return (ahead, behind);
		}

		[Null]
		private static (int added, int deleted)? GetStagedDiffStats(string path, JobMonitor monitor = null)
		{
			GitRequestResult result = RunGit(path, new GitCommand("diff", "--cached", "--numstat"), monitor);
			if (!result.Success)
			{
				return null;
			}
			int added = 0;
			int deleted = 0;
			foreach (string line in result.Stdout.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n'))
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

		private static void RefreshSubrepoStatusIcons(StackPanel statusPanel, GitMmSubrepoItem subrepo)
		{
			statusPanel.Children.Clear();
			switch (subrepo.CommandState)
			{
				case GitMmSubrepoCommandState.Running:
					AddSubrepoStatusIcon(statusPanel, "RefreshIcon", Translate("Command running"));
					break;
				case GitMmSubrepoCommandState.Success:
					AddSubrepoStatusIcon(statusPanel, "BisectGoodIcon", Translate("Command succeeded"));
					break;
				case GitMmSubrepoCommandState.Failed:
					AddSubrepoStatusIcon(statusPanel, "ErrorIcon", Translate("Command failed"));
					break;
			}
			if (subrepo.HasConflicts)
			{
				AddSubrepoStatusIcon(statusPanel, "WarningIcon", BuildSubrepoStatusToolTip(subrepo));
			}
			else if (subrepo.HasLocalChanges)
			{
				AddSubrepoStatusIcon(statusPanel, "ChangesIcon", BuildSubrepoStatusToolTip(subrepo));
			}
			else if (subrepo.IsNonDefaultBranch || subrepo.AheadCount > 0 || subrepo.BehindCount > 0)
			{
				AddSubrepoStatusIcon(statusPanel, "BranchIcon", BuildSubrepoStatusToolTip(subrepo));
			}
			statusPanel.ToolTip = statusPanel.Children.Count == 0 ? null : BuildSubrepoStatusToolTip(subrepo);
			statusPanel.Visibility = statusPanel.Children.Count == 0 ? Visibility.Collapsed : Visibility.Visible;
		}

		private static string BuildSubrepoStatusToolTip(GitMmSubrepoItem subrepo)
		{
			List<string> lines = new List<string>
			{
				subrepo.DisplayName
			};
			if (subrepo.HasLocalChanges)
			{
				lines.Add(PreferencesLocalization.FormatCurrent("Changed: {0}", subrepo.ChangedFilesCount));
			}
			if (subrepo.HasConflicts)
			{
				lines.Add(PreferencesLocalization.FormatCurrent("Conflicts: {0}", subrepo.ConflictFilesCount));
			}
			if (subrepo.IsNonDefaultBranch)
			{
				lines.Add(PreferencesLocalization.FormatCurrent("Non-default: {0}", string.IsNullOrWhiteSpace(subrepo.CurrentBranch) ? "-" : subrepo.CurrentBranch));
				if (!string.IsNullOrWhiteSpace(subrepo.DefaultBranch))
				{
					lines.Add(PreferencesLocalization.FormatCurrent("Default: {0}", subrepo.DefaultBranch));
				}
			}
			if (subrepo.AheadCount > 0)
			{
				lines.Add(PreferencesLocalization.FormatCurrent("Ahead: {0}", subrepo.AheadCount));
			}
			if (subrepo.BehindCount > 0)
			{
				lines.Add(PreferencesLocalization.FormatCurrent("Behind: {0}", subrepo.BehindCount));
			}
			if (subrepo.StagedAdded != 0 || subrepo.StagedDeleted != 0)
			{
				lines.Add($"+{subrepo.StagedAdded} -{subrepo.StagedDeleted}");
			}
			return string.Join(Environment.NewLine, lines);
		}

		private static void AddSubrepoStatusIcon(StackPanel statusPanel, string iconResourceKey, string tooltip)
		{
			Image image = new Image
			{
				Width = 13.0,
				Height = 13.0,
				Margin = new Thickness(2.0, 0.0, 0.0, 0.0),
				VerticalAlignment = VerticalAlignment.Center,
				ToolTip = tooltip
			};
			image.SetResourceReference(Image.SourceProperty, iconResourceKey);
			statusPanel.Children.Add(image);
		}

		[Null]
		private static EditableTextBlock FindSubrepoHeaderTitle([Null] TabItem tabItem)
		{
			return (tabItem?.Header as DockPanel)?.Children.OfType<EditableTextBlock>().FirstOrDefault();
		}

		private static RepositoryManager.Repository EnsureRepositoryManagerEntry(string repositoryPath)
		{
			string normalizedPath = PathHelper.Normalize(repositoryPath);
			RepositoryManager.Repository? repository = RepositoryManager.Instance.Repositories.FirstItemStruct((RepositoryManager.Repository x) => x.Path == normalizedPath);
			if (!repository.HasValue)
			{
				RepositoryManager.Instance.AddRepositories(new string[1] { normalizedPath });
				RepositoryManager.Instance.Save();
				repository = RepositoryManager.Instance.Repositories.FirstItemStruct((RepositoryManager.Repository x) => x.Path == normalizedPath);
			}
			return repository.GetValueOrDefault();
		}

		private List<GitMmSubrepoItem> CreateSubrepoItems(IEnumerable<string> paths, string workspacePath)
		{
			List<string> orderedPaths = ApplySavedSubrepoOrder(paths, workspacePath);
			List<GitMmSubrepoItem> items = new List<GitMmSubrepoItem>();
			foreach (string path in orderedPaths)
			{
				items.Add(new GitMmSubrepoItem(path, workspacePath, _submoduleSubrepoPaths.Contains(NormalizePath(path))));
			}
			return items;
		}

		private List<string> ApplySavedSubrepoOrder(IEnumerable<string> paths, string workspacePath)
		{
			List<string> remainingPaths = (paths ?? new string[0]).ToList();
			List<string> orderedPaths = new List<string>();
			int rootIndex = remainingPaths.FindIndex((string path) => IsSamePath(path, workspacePath));
			if (rootIndex >= 0)
			{
				orderedPaths.Add(remainingPaths[rootIndex]);
				remainingPaths.RemoveAt(rootIndex);
			}
			string[] savedOrder = ForkPlusSettings.Default.GitMm.GetSubrepoOrder(workspacePath);
			foreach (string savedPath in savedOrder)
			{
				if (IsSamePath(savedPath, workspacePath))
				{
					continue;
				}
				int index = remainingPaths.FindIndex((string path) => IsSamePath(path, savedPath));
				if (index >= 0)
				{
					orderedPaths.Add(remainingPaths[index]);
					remainingPaths.RemoveAt(index);
				}
			}
			orderedPaths.AddRange(remainingPaths);
			return orderedPaths;
		}

		private FrameworkElement CreateRepositoryContent(string path)
		{
			GitCommandResult<GitModule> result = new OpenGitRepositoryGitCommand().Execute(path);
			if (!result.Succeeded)
			{
				return new TextBlock
				{
					Text = result.Error.FriendlyDescription,
					Margin = new Thickness(10.0),
					TextWrapping = TextWrapping.Wrap
				};
			}
			RepositoryUserControl repositoryUserControl = new RepositoryUserControl
			{
				HorizontalAlignment = HorizontalAlignment.Stretch,
				VerticalAlignment = VerticalAlignment.Stretch,
				DataContext = null
			};
			repositoryUserControl.OpenRepository(result.Result);
			repositoryUserControl.InvalidateAndRefresh(SubDomain.DefaultRefresh);
			repositoryUserControl.ApplyLocalization();
			return repositoryUserControl;
		}

		private static string FormatCommand(string[] args)
		{
			return "git mm " + string.Join(" ", args.Select(QuoteIfNeeded));
		}

		private static string QuoteIfNeeded(string value)
		{
			if (string.IsNullOrEmpty(value))
			{
				return "\"\"";
			}
			if (value.IndexOfAny(new char[2] { ' ', '\t' }) < 0)
			{
				return value;
			}
			return "\"" + value.Replace("\\", "\\\\").Replace("\"", "\\\"") + "\"";
		}

		private static List<string> ScanSubrepos(string rootPath, int maxDepth)
		{
			return ScanSubrepos(rootPath, maxDepth, out _);
		}

		private static List<string> ScanSubrepos(string rootPath, int maxDepth, out HashSet<string> submodulePaths)
		{
			List<string> result = new List<string>();
			HashSet<string> seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
			HashSet<string> discoveredSubmodulePaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
			Dictionary<string, Submodule[]> submodulesByRepositoryPath = new Dictionary<string, Submodule[]>(StringComparer.OrdinalIgnoreCase);
			Dictionary<string, string> worktreeByGitDirectory = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
			if (string.IsNullOrWhiteSpace(rootPath) || !Directory.Exists(rootPath))
			{
				submodulePaths = discoveredSubmodulePaths;
				return result;
			}
			void AddIfGitWorkTree(string path, int depth, bool isSubmodule)
			{
				string normalizedPath = NormalizePath(path);
				if (normalizedPath == null || !IsGitWorkTree(normalizedPath) || !seen.Add(normalizedPath))
				{
					return;
				}
				result.Add(normalizedPath);
				if (isSubmodule)
				{
					discoveredSubmodulePaths.Add(normalizedPath);
				}
				AddSubmodules(normalizedPath, depth + 1);
			}
			void AddSubmodules(string repositoryPath, int depth)
			{
				if (depth > maxDepth)
				{
					return;
				}
				string normalizedRepositoryPath = NormalizePath(repositoryPath);
				if (normalizedRepositoryPath == null)
				{
					return;
				}
				if (!submodulesByRepositoryPath.TryGetValue(normalizedRepositoryPath, out Submodule[] submodules))
				{
					GitCommandResult<Submodule[]> submodulesResult = new GetSubmodulesGitCommand().Execute(System.IO.Path.Combine(repositoryPath, ".gitmodules"));
					submodules = submodulesResult.Succeeded ? submodulesResult.Result : new Submodule[0];
					submodulesByRepositoryPath[normalizedRepositoryPath] = submodules;
				}
				foreach (Submodule submodule in submodules)
				{
					if (!submodule.IsActive || string.IsNullOrWhiteSpace(submodule.Path))
					{
						continue;
					}
					string submodulePath = System.IO.Path.Combine(repositoryPath, submodule.Path);
					if (IsGitWorkTree(submodulePath))
					{
						AddIfGitWorkTree(submodulePath, depth, isSubmodule: true);
					}
				}
			}
			void Walk(string directory, int depth)
			{
				if (depth > maxDepth)
				{
					return;
				}
				DirectoryInfo[] directories;
				try
				{
					directories = new DirectoryInfo(directory).GetDirectories();
				}
				catch
				{
					return;
				}
				foreach (DirectoryInfo child in directories)
				{
					if (child.Name == ".git" || child.Name == ".repo" || child.Name == ".mm" || child.Name == "node_modules" || child.Name == "bin" || child.Name == "obj")
					{
						continue;
					}
					string fullName = child.FullName;
					if (IsGitWorkTree(fullName))
					{
						AddIfGitWorkTree(fullName, depth, isSubmodule: false);
						Walk(fullName, depth + 1);
						continue;
					}
					Walk(fullName, depth + 1);
				}
			}
			void WalkMmProjects(string directory, int depth)
			{
				if (depth > Math.Max(maxDepth, 8) || !Directory.Exists(directory))
				{
					return;
				}
				DirectoryInfo[] directories;
				try
				{
					directories = new DirectoryInfo(directory).GetDirectories();
				}
				catch
				{
					return;
				}
				foreach (DirectoryInfo child in directories)
				{
					if (child.Name == ".git" || child.Name == "objects" || child.Name == "refs" || child.Name == "logs" || child.Name == "hooks" || child.Name == "info")
					{
						continue;
					}
					string fullName = child.FullName;
					if (IsGitWorkTree(fullName))
					{
						AddIfGitWorkTree(fullName, depth, isSubmodule: true);
					}
					else
					{
						if (!worktreeByGitDirectory.TryGetValue(fullName, out string worktreePath))
						{
							worktreePath = ResolveWorktreePathFromGitDirectory(fullName);
							worktreeByGitDirectory[fullName] = worktreePath;
						}
						if (worktreePath != null && IsGitWorkTree(worktreePath))
						{
							AddIfGitWorkTree(worktreePath, depth, isSubmodule: true);
						}
					}
					WalkMmProjects(fullName, depth + 1);
				}
			}
			AddIfGitWorkTree(rootPath, 0, isSubmodule: false);
			Walk(rootPath, 0);
			WalkMmProjects(System.IO.Path.Combine(rootPath, ".mm", "projects"), 0);
			result.Sort(StringComparer.OrdinalIgnoreCase);
			int rootIndex = result.FindIndex((string path) => IsSamePath(path, rootPath));
			if (rootIndex > 0)
			{
				string root = result[rootIndex];
				result.RemoveAt(rootIndex);
				result.Insert(0, root);
			}
			submodulePaths = discoveredSubmodulePaths;
			return result;
		}

		private static bool IsGitWorkTree(string path)
		{
			return Directory.Exists(System.IO.Path.Combine(path, ".git")) || File.Exists(System.IO.Path.Combine(path, ".git"));
		}

		[Null]
		private static string ResolveWorktreePathFromGitDirectory(string gitDirectory)
		{
			if (string.IsNullOrWhiteSpace(gitDirectory) || !Directory.Exists(gitDirectory))
			{
				return null;
			}
			string configPath = System.IO.Path.Combine(gitDirectory, "config");
			if (!File.Exists(configPath))
			{
				return null;
			}
			try
			{
				GitCommandResult<GitConfig> gitConfigResult = new GetGitConfigGitCommand().Execute(configPath);
				if (!gitConfigResult.Succeeded)
				{
					return null;
				}
				foreach (GitConfig.Section section in gitConfigResult.Result.Sections)
				{
					if (section.Name != "core")
					{
						continue;
					}
					foreach (GitConfig.Variable variable in section.Variables)
					{
						if (variable.Name != "worktree" || string.IsNullOrWhiteSpace(variable.Value))
						{
							continue;
						}
						string worktreePath = System.IO.Path.IsPathRooted(variable.Value) ? variable.Value : System.IO.Path.GetFullPath(System.IO.Path.Combine(gitDirectory, variable.Value));
						return NormalizePath(worktreePath);
					}
				}
			}
			catch (Exception ex)
			{
				Log.Warn("Failed to resolve worktree for git directory '" + gitDirectory + "'", ex);
			}
			return null;
		}

		internal static bool IsSamePath(string lhs, string rhs)
		{
			string normalizedLhs = NormalizePath(lhs);
			string normalizedRhs = NormalizePath(rhs);
			return !string.IsNullOrWhiteSpace(normalizedLhs)
				&& !string.IsNullOrWhiteSpace(normalizedRhs)
				&& string.Equals(normalizedLhs, normalizedRhs, StringComparison.OrdinalIgnoreCase);
		}

		[Null]
		private static string NormalizePath([Null] string path)
		{
			if (string.IsNullOrWhiteSpace(path))
			{
				return null;
			}
			try
			{
				path = System.IO.Path.GetFullPath(path);
			}
			catch
			{
			}
			return PathHelper.Normalize(path).TrimEnd(System.IO.Path.DirectorySeparatorChar, System.IO.Path.AltDirectorySeparatorChar, '\\', '/');
		}

		[Null]
		private static T FindVisualChild<T>(DependencyObject parent) where T : DependencyObject
		{
			if (parent == null)
			{
				return null;
			}
			int childCount = VisualTreeHelper.GetChildrenCount(parent);
			for (int i = 0; i < childCount; i++)
			{
				DependencyObject child = VisualTreeHelper.GetChild(parent, i);
				if (child is T result)
				{
					return result;
				}
				T nested = FindVisualChild<T>(child);
				if (nested != null)
				{
					return nested;
				}
			}
			return null;
		}

		private static string Translate(string text)
		{
			return PreferencesLocalization.Translate(text, ForkPlusSettings.Default.UiLanguage);
		}
	}

	public sealed class GitMmWorkspaceItem : INotifyPropertyChanged
	{
		private List<GitMmSubrepoItem> _subrepos = new List<GitMmSubrepoItem>();

		[Null]
		private GitMmSubrepoItem _selectedSubrepo;

		public string Path { get; }

		public string Name { get; }

		[Null]
		public string PreferredSubrepoPath { get; set; }

		public List<GitMmSubrepoItem> Subrepos
		{
			get
			{
				return _subrepos;
			}
			set
			{
				SetSubrepos(value, selectPreferred: true);
			}
		}

		[Null]
		public GitMmSubrepoItem SelectedSubrepo
		{
			get
			{
				return _selectedSubrepo;
			}
			set
			{
				if (_selectedSubrepo != value)
				{
					_selectedSubrepo = value;
					PreferredSubrepoPath = value?.Path;
					PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(SelectedSubrepo)));
				}
			}
		}

		public event PropertyChangedEventHandler PropertyChanged;

		public GitMmWorkspaceItem(string path)
		{
			Path = path;
			Name = System.IO.Path.GetFileName(path.TrimEnd(System.IO.Path.DirectorySeparatorChar, System.IO.Path.AltDirectorySeparatorChar)) ?? path;
		}

		public void SetSubrepos(List<GitMmSubrepoItem> subrepos, bool selectPreferred)
		{
			_subrepos = subrepos ?? new List<GitMmSubrepoItem>();
			PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Subrepos)));
			if (selectPreferred)
			{
				SelectedSubrepo = _subrepos.FirstOrDefault((GitMmSubrepoItem item) => GitMmUserControl.IsSamePath(item.Path, PreferredSubrepoPath)) ?? _subrepos.FirstOrDefault();
			}
		}
	}

	public sealed class GitMmSubrepoItem
	{
		public string Path { get; }

		public string Name { get; }

		public bool IsRootRepository { get; }

		public bool IsSubmodule { get; }

		public GitMmSubrepoCommandState CommandState { get; set; }

		public bool HasLocalChanges { get; set; }

		public int ChangedFilesCount { get; set; }

		public bool HasConflicts { get; set; }

		public int ConflictFilesCount { get; set; }

		public bool IsNonDefaultBranch { get; set; }

		public string CurrentBranch { get; set; }

		public string DefaultBranch { get; set; }

		public int AheadCount { get; set; }

		public int BehindCount { get; set; }

		public int StagedAdded { get; set; }

		public int StagedDeleted { get; set; }

		[Null]
		public DateTime? RuntimeStateUpdatedAtUtc { get; set; }

		public string BaseDisplayName => FindRepositoryAlias(Path) ?? Name;

		public string DisplayName => BaseDisplayName + (IsRootRepository ? PreferencesLocalization.Current("[Main]") : IsSubmodule ? PreferencesLocalization.Current("[Submodule]") : PreferencesLocalization.Current("[Sub]"));

		[Null]
		public FrameworkElement RepositoryControl { get; set; }

		public GitMmSubrepoItem(string path, string rootPath, bool isSubmodule)
		{
			Path = path;
			Name = CreateName(path, rootPath);
			IsRootRepository = GitMmUserControl.IsSamePath(path, rootPath);
			IsSubmodule = isSubmodule;
		}

		[Null]
		private static string FindRepositoryAlias(string path)
		{
			string normalizedPath = PathHelper.Normalize(path);
			RepositoryManager.Repository? repository = RepositoryManager.Instance.Repositories.FirstItemStruct((RepositoryManager.Repository item) => item.Path == normalizedPath);
			return repository?.Alias;
		}

		private static string CreateName(string path, string rootPath)
		{
			string relative = path;
			if (!string.IsNullOrEmpty(rootPath) && path.StartsWith(rootPath, StringComparison.OrdinalIgnoreCase))
			{
				relative = path.Substring(rootPath.Length).TrimStart(System.IO.Path.DirectorySeparatorChar, System.IO.Path.AltDirectorySeparatorChar);
			}
			if (string.IsNullOrWhiteSpace(relative))
			{
				return System.IO.Path.GetFileName(path.TrimEnd(System.IO.Path.DirectorySeparatorChar, System.IO.Path.AltDirectorySeparatorChar));
			}
			return relative;
		}
	}

	public enum GitMmSubrepoCommandState
	{
		None,
		Running,
		Success,
		Failed
	}

	internal sealed class GitMmSubrepoRuntimeState
	{
		public bool HasLocalChanges { get; set; }

		public int ChangedFilesCount { get; set; }

		public bool HasConflicts { get; set; }

		public int ConflictFilesCount { get; set; }

		public bool IsNonDefaultBranch { get; set; }

		public string CurrentBranch { get; set; }

		public string DefaultBranch { get; set; }

		public int AheadCount { get; set; }

		public int BehindCount { get; set; }

		public int StagedAdded { get; set; }

		public int StagedDeleted { get; set; }
	}
}

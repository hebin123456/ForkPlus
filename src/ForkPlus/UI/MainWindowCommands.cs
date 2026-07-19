using ForkPlus.UI.Commands;

namespace ForkPlus.UI
{
	public class MainWindowCommands : CommandContainer
	{
		private ActivateCommitViewCommand _activateCommitView;

		private ActivateRevisionListCommand _activateRevisionList;

		private ActivateRepositoryTabCommand _activateRepositoryTab;

		private ActivateSearchTabCommand _activateSearchTab;

		private ShowHeadCommand _showHead;

		private ShowDebugUpdateWindowCommand _showDebugUpdateWindow;

		private UpdateApplicationCommand _checkForUpdates;

		private CloseActiveTabCommand _closeActiveTab;

		private ExitApplicationCommand _exitApplication;

		private NewTabCommand _newTab;

		private OpenRepositoryCommand _openRepository;

		private RefreshRepositoryDataCommand _refreshRepositoryData;

		private ShowAskPassWindowCommand _showAskPassWindow;

		private ShowCloneWindowCommand _showCloneWindow;

		private ShowInitGitMmRepositoryWindowCommand _showInitGitMmRepositoryWindow;

		private ShowCreateWorktreeWindowCommand _showCreateWorktreeWindow;

		private ShowCheckoutBranchAsWorktreeWindowCommand _showCheckoutBranchAsWorktreeWindow;

		private ShowBenchmarkWindowCommand _showBenchmarkWindow;

		private ShowCreateBranchWindowCommand _showCreateBranchWindow;

		private ShowInitRepositoryWindowCommand _showCreateRepositoryWindow;

		private ShowCreateTagWindowCommand _showCreateTagWindow;

		private CopyRevisionShaCommand _copyRevisionSha;

		private CopyRevisionInfoCommand _copyRevisionInfo;

		private ShowFetchWindowCommand _showFetchWindow;

		private ShowConfigureSshKeysCommand _showConfigureSSHKeysWindow;

		private ShowConfigureWorkspacesWindowCommand _showConfigureWorkspacesWindow;

		private SendCrashReportCommand _sendCrashReport;

		private ShowQuickLaunchWindowCommand _showQuickLaunchWindow;

		private ShowQuickLaunchCheckoutWindowCommand _showQuickLaunchCheckouWindow;

		private ShowPreferencesWindowCommand _showPreferencesWindow;

		private ShowAccountsWindowCommand _showAccountsWindow;

		private ShowPullWindowCommand _showPullWindow;

		private ShowPushWindowCommand _showPushWindow;

		private QuickPushCommand _quickPush;

		private QuickFetchCommand _quickFetch;

		private QuickPullCommand _quickPull;

		private SelectPreviousTabCommand _selectPreviousTab;

		private SelectNextTabCommand _selectNextTab;

		private SwitchApplicationThemeCommand _switchApplicationTheme;

		private SwitchWorkspaceCommand _switchWorkspace;

		private SwitchRevisionListOrientationCommand _switchRevisionListOrientation;

		private OpenRepositoryInShellToolCommand _openRepositoryInShellTool;

		private OpenRepositoryInDefaultShellToolCommand _openRepositoryInDefaultShellTool;

		private OpenRepositoryInFileExplorerCommand _openRepositoryInFileExplorer;

		private OpenRepositoryInExternalEditorCommand _openRepositoryInExternalEditor;

		private OpenUrlCommand _openUrl;

		private ToggleShowReflogInRevisionListCommand _toggleShowReflogInRevisionList;

		private ToggleCollapseAllMergeRevisionsCommand _toggleCollapseAllMergeRevisions;

		private ToggleHideTagsCommand _toggleHideTagsInRevisionList;

		private ToggleHideStashesInRevisionListCommand _toggleHideStashesInRevisionList;

		private ToggleReferenceFilterCommand _toggleReferenceFilter;

		private IncreaseLayoutScaleCommand _increaseLayoutScale;

		private DecreaseLayoutScaleCommand _decreaseLayoutScale;

		private ToggleDisableRefreshOnActivateCommand _toggleRefreshOnActivate;

		private ToggleTraceElapsedTimeCommand _toggleTraceElapsedTime;

		private OpenApplicationDataDirectoryCommand _openApplicationDataDirectory;

		private ShowAboutWindowCommand _showAboutWindow;

		private OpenForkPlusTwitterCommand _openForkTwitter;

		private OpenForkPlusWebsiteCommand _openForkWebsite;

		private OpenIssueTrackerCommand _openIssueTracker;

		private OpenKeyboardShortcutsCommand _openKeyboardShortcuts;

		private OpenReleaseNotesCommand _openReleaseNotes;

		private ShowPerformanceDiagnosticsWindowCommand _showPerformanceDiagnosticsWindow;

		private ShowSaveStashWindowCommand _showSaveStashWindow;

		private UndoCommand _undo;

		private RedoCommand _redo;

		public ActivateCommitViewCommand ActivateCommitView => CommandContainer.Lazy(ref _activateCommitView);

		public ActivateRevisionListCommand ActivateRevisionList => CommandContainer.Lazy(ref _activateRevisionList);

		public ActivateRepositoryTabCommand ActivateRepositoryTab => CommandContainer.Lazy(ref _activateRepositoryTab);

		public ActivateSearchTabCommand ActivateSearchTab => CommandContainer.Lazy(ref _activateSearchTab);

		public ShowHeadCommand ShowHead => CommandContainer.Lazy(ref _showHead);

		public ShowDebugUpdateWindowCommand ShowDebugUpdateWindow => CommandContainer.Lazy(ref _showDebugUpdateWindow);

		public UpdateApplicationCommand UpdateApplication => CommandContainer.Lazy(ref _checkForUpdates);

		public CloseActiveTabCommand CloseActiveTab => CommandContainer.Lazy(ref _closeActiveTab);

		public ExitApplicationCommand ExitApplication => CommandContainer.Lazy(ref _exitApplication);

		public NewTabCommand NewTab => CommandContainer.Lazy(ref _newTab);

		public OpenRepositoryCommand OpenRepository => CommandContainer.Lazy(ref _openRepository);

		public RefreshRepositoryDataCommand RefreshRepositoryData => CommandContainer.Lazy(ref _refreshRepositoryData);

		public ShowAskPassWindowCommand ShowAskPassWindow => CommandContainer.Lazy(ref _showAskPassWindow);

		public ShowCloneWindowCommand ShowCloneWindow => CommandContainer.Lazy(ref _showCloneWindow);

		public ShowInitGitMmRepositoryWindowCommand ShowInitGitMmRepositoryWindow => CommandContainer.Lazy(ref _showInitGitMmRepositoryWindow);

		public ShowCreateWorktreeWindowCommand ShowCreateWorktreeWindow => CommandContainer.Lazy(ref _showCreateWorktreeWindow);

		public ShowCheckoutBranchAsWorktreeWindowCommand ShowCheckoutBranchAsWorktreeWindow => CommandContainer.Lazy(ref _showCheckoutBranchAsWorktreeWindow);

		public ShowBenchmarkWindowCommand ShowBenchmarkWindow => CommandContainer.Lazy(ref _showBenchmarkWindow);

		public ShowCreateBranchWindowCommand ShowCreateBranchWindow => CommandContainer.Lazy(ref _showCreateBranchWindow);

		public ShowInitRepositoryWindowCommand ShowCreateRepositoryWindow => CommandContainer.Lazy(ref _showCreateRepositoryWindow);

		public ShowCreateTagWindowCommand ShowCreateTagWindow => CommandContainer.Lazy(ref _showCreateTagWindow);

		public CopyRevisionShaCommand CopyRevisionSha => CommandContainer.Lazy(ref _copyRevisionSha);

		public CopyRevisionInfoCommand CopyRevisionInfo => CommandContainer.Lazy(ref _copyRevisionInfo);

		public ShowFetchWindowCommand ShowFetchWindow => CommandContainer.Lazy(ref _showFetchWindow);

		public ShowConfigureSshKeysCommand ShowConfigureSSHKeysWindow => CommandContainer.Lazy(ref _showConfigureSSHKeysWindow);

		public ShowConfigureWorkspacesWindowCommand ShowConfigureWorkspacesWindow => CommandContainer.Lazy(ref _showConfigureWorkspacesWindow);

		public SendCrashReportCommand SendCrashReport => CommandContainer.Lazy(ref _sendCrashReport);

		public ShowQuickLaunchWindowCommand ShowQuickLaunchWindow => CommandContainer.Lazy(ref _showQuickLaunchWindow);

		public ShowQuickLaunchCheckoutWindowCommand ShowQuickLaunchCheckoutWindow => CommandContainer.Lazy(ref _showQuickLaunchCheckouWindow);

		public ShowPreferencesWindowCommand ShowPreferencesWindow => CommandContainer.Lazy(ref _showPreferencesWindow);

		public ShowAccountsWindowCommand ShowAccountsWindow => CommandContainer.Lazy(ref _showAccountsWindow);

		public ShowPullWindowCommand ShowPullWindow => CommandContainer.Lazy(ref _showPullWindow);

		public ShowPushWindowCommand ShowPushWindow => CommandContainer.Lazy(ref _showPushWindow);

		public QuickPushCommand QuickPush => CommandContainer.Lazy(ref _quickPush);

		public QuickFetchCommand QuickFetch => CommandContainer.Lazy(ref _quickFetch);

		public QuickPullCommand QuickPull => CommandContainer.Lazy(ref _quickPull);

		public SelectPreviousTabCommand SelectPreviousTab => CommandContainer.Lazy(ref _selectPreviousTab);

		public SelectNextTabCommand SelectNextTab => CommandContainer.Lazy(ref _selectNextTab);

		public SwitchApplicationThemeCommand SwitchApplicationTheme => CommandContainer.Lazy(ref _switchApplicationTheme);

		public SwitchWorkspaceCommand SwitchWorkspace => CommandContainer.Lazy(ref _switchWorkspace);

		public SwitchRevisionListOrientationCommand SwitchRevisionListOrientation => CommandContainer.Lazy(ref _switchRevisionListOrientation);

		public OpenRepositoryInShellToolCommand OpenRepositoryInShellTool => CommandContainer.Lazy(ref _openRepositoryInShellTool);

		public OpenRepositoryInDefaultShellToolCommand OpenRepositoryInDefaultShellTool => CommandContainer.Lazy(ref _openRepositoryInDefaultShellTool);

		public OpenRepositoryInFileExplorerCommand OpenRepositoryInFileExplorer => CommandContainer.Lazy(ref _openRepositoryInFileExplorer);

		public OpenRepositoryInExternalEditorCommand OpenRepositoryInExternalEditor => CommandContainer.Lazy(ref _openRepositoryInExternalEditor);

		public OpenUrlCommand OpenUrl => CommandContainer.Lazy(ref _openUrl);

		public ToggleShowReflogInRevisionListCommand ToggleShowReflogInRevisionList => CommandContainer.Lazy(ref _toggleShowReflogInRevisionList);

		public ToggleCollapseAllMergeRevisionsCommand ToggleCollapseAllMergeRevisions => CommandContainer.Lazy(ref _toggleCollapseAllMergeRevisions);

		public ToggleHideTagsCommand ToggleHideTags => CommandContainer.Lazy(ref _toggleHideTagsInRevisionList);

		public ToggleHideStashesInRevisionListCommand ToggleHideStashesInRevisionList => CommandContainer.Lazy(ref _toggleHideStashesInRevisionList);

		public ToggleReferenceFilterCommand ToggleReferenceFilter => CommandContainer.Lazy(ref _toggleReferenceFilter);

		public IncreaseLayoutScaleCommand IncreaseLayoutScale => CommandContainer.Lazy(ref _increaseLayoutScale);

		public DecreaseLayoutScaleCommand DecreaseLayoutScale => CommandContainer.Lazy(ref _decreaseLayoutScale);

		public ToggleDisableRefreshOnActivateCommand ToggleRefreshOnActivate => CommandContainer.Lazy(ref _toggleRefreshOnActivate);

		public ToggleTraceElapsedTimeCommand ToggleTraceElapsedTime => CommandContainer.Lazy(ref _toggleTraceElapsedTime);

		public OpenApplicationDataDirectoryCommand OpenApplicationDataDirectory => CommandContainer.Lazy(ref _openApplicationDataDirectory);

		public ShowAboutWindowCommand ShowAboutWindow => CommandContainer.Lazy(ref _showAboutWindow);

		public OpenForkPlusTwitterCommand OpenForkTwitter => CommandContainer.Lazy(ref _openForkTwitter);

		public OpenForkPlusWebsiteCommand OpenForkWebsite => CommandContainer.Lazy(ref _openForkWebsite);

		public OpenIssueTrackerCommand OpenIssueTracker => CommandContainer.Lazy(ref _openIssueTracker);

		public OpenKeyboardShortcutsCommand OpenKeyboardShortcuts => CommandContainer.Lazy(ref _openKeyboardShortcuts);

		public OpenReleaseNotesCommand OpenReleaseNotes => CommandContainer.Lazy(ref _openReleaseNotes);

		public ShowPerformanceDiagnosticsWindowCommand ShowPerformanceDiagnosticsWindow => CommandContainer.Lazy(ref _showPerformanceDiagnosticsWindow);

		public ShowSaveStashWindowCommand ShowSaveStashWindow => CommandContainer.Lazy(ref _showSaveStashWindow);

		/// <summary>撤销最近一次仓库操作。v3.0.0 新增。</summary>
		public UndoCommand Undo => CommandContainer.Lazy(ref _undo);

		/// <summary>重做最近被撤销的操作。v3.0.0 新增。</summary>
		public RedoCommand Redo => CommandContainer.Lazy(ref _redo);
	}
}

using ForkPlus.UI.Commands;

namespace ForkPlus.UI.UserControls
{
	public class RepositoryUserControlCommands : CommandContainer
	{
		private UpdateReferenceFilterCommand _updateReferenceFilter;

		private PinReferenceCommand _addReferenceStar;

		private UnpinReferenceCommand _removeReferenceStar;

		private ShowRepositorySettingsWindowCommand _showRepositorySettingsWindow;

		private ShowRepositoryStatisticsWindowCommand _showRepositoryStatisticsWindow;

		private ShowRepositoryOverviewWindowCommand _showRepositoryOverviewWindow;

		private ShowAiResultWindowCommand _showAiResultWindow;

		private RemoveReferenceCommand _removeReferenceCommand;

		private CreatePullRequestCommand _createPullRequest;

		private BisectCommand _bisect;

		private ToggleShowReflogInRevisionListCommand _toggleShowReflogInRevisionList;

		private ShowCreateBranchWindowCommand _showCreateBranchWindow;

		private ShowMergeBranchWindowCommand _showMergeBranchWindow;

		private ShowRebaseBranchWindowCommand _showRebaseBranchWindow;

		private ShowInteractiveRebaseWindowCommand _showInteractiveRebaseWindow;

		private ShowRemoveLocalBranchWindowCommand _showRemoveLocalBranchWindow;

		private ShowRemoveRemoteBranchWindowCommand _showRemoveRemoteBranchWindow;

		private ShowCheckoutBranchWindowCommand _showCheckoutBranchWindow;

		private ShowRenameLocalBranchWindowCommand _showRenameLocalBranchWindow;

		private ShowResetBranchWindowCommand _showResetBranchWindow;

		private ShowPushBranchWindowCommand _showPushBranchWindow;

		private ShowPushMultipleBranchesWindowCommand _showPushMultipleBranchesWindow;

		private ShowPushMultipleTagsWindowCommand _showPushMultipleTagsWindow;

		private ShowPullWindowCommand _showPullWindow;

		private FastForwardCommand _fastForward;

		private FastForwardPullCommand _fastForwardPull;

		private UpdateTrackingReferenceCommand _updateTrackingReference;

		private ShowChangeTrackingReferenceWindowCommand _showChangeTrackingReferenceWindow;

		private ShowCreateTagWindowCommand _showCreateTagWindow;

		private ShowRemoveTagWindowCommand _showRemoveTagWindow;

		private ShowPushTagWindowCommand _showPushTagWindowCommand;

		private ShowTagDetailsWindowCommand _showTagDetailsWindow;

		private CopyReferenceNameCommand _copyReferenceName;

		private ShowCheckoutRevisionWindowCommand _showCheckoutRevisionWindow;

		private ShowCherryPickWindowCommand _showCherryPickWindowCommand;

		private ShowRevertRevisionWindowCommand _showRevertRevisionWindow;

		private ShowRevisionInSeparateWindowCommand _showRevisionInSeparateWindow;

		private CopyRevisionShaCommand _copyRevisionSha;

		private CopyRevisionInfoCommand _copyRevisionInfo;

		private RunCustomCommandCommand _runRevisionCustomCommand;

		private ShowSaveRevisionsAsPatchWindowCommand _showSaveRevisionsAsPatchWindow;

		private CompareRevisionToWorkingDirectoryCommand _compareRevisionToWorkingDirectory;

		private ShowAddRemoteWindowCommand _showAddRemoteWindow;

		private CopyRemoteAddressCommand _copyRemoteAddress;

		private ShowEditRemoteWindowCommand _showEditRemoteWindow;

		private ShowRemoveRemoteWindowCommand _showRemoveRemoteWindow;

		private DisableImplicitRemoteFetchCommand _disableImplicitRemoteFetch;

		private ShowGitLfsFetchWindowCommand _showGitLfsFetchWindow;

		private ShowGitLfsPullWindowCommand _showGitLfsPullWindow;

		private ShowGitLfsTrackWindowCommand _showGitLfsTrackWindow;

		private ShowGitLfsStatusWindowCommand _showGitLfsStatusWindow;

		private InitGitLfsCommand _initGitLfs;

		private DeinitializeGitLfsCommand _removeGitLfs;

		private GitLfsPruneCommand _gitLfsPrune;

		private GitLfsLockCommand _gitLfsLockCommand;

		private GitLfsUnlockCommand _gitLfsUnlockCommand;

		private ShowGitFlowInitWindowCommand _showGitFlowInitWindow;

		private DeinitializeGitFlowCommand _deinitializeGitFlow;

		private ShowGitFlowStartFeatureWindowCommand _showGitFlowStartFeatureWindow;

		private ShowGitFlowStartReleaseWindowCommand _showGitFlowStartReleaseWindow;

		private ShowGitFlowStartHotfixWindowCommand _showGitFlowStartHotfixWindow;

		private ShowGitFlowFinishFeatureWindowCommand _showGitFlowFinishFeatureWindow;

		private ShowGitFlowFinishReleaseWindowCommand _showGitFlowFinishReleaseWindow;

		private ShowGitFlowFinishHotfixWindowCommand _showGitFlowFinishHotfixWindow;

		private ShowLeanBranchingStartWindowCommand _showLeanBranchingStartWindow;

		private ShowLeanBranchingFinishWindowCommand _showLeanBranchingFinishWindow;

		private LeanBranchingSyncCommand _leanBranchingSync;

		private ShowApplyStashWindowCommand _showApplyStashWindow;

		private ShowRenameStashWindowCommand _showRenameStashWindow;

		private ShowRemoveStashWindowCommand _showRemoveStashWindow;

		private ShowSaveSnapshotWindowCommand _showSaveSnapshotWindow;

		private OpenSubmoduleCommand _openSubmodule;

		private UpdateSubmoduleCommand _updateSubmodule;

		private MoveSubmoduleCommand _moveSubmodule;

		private ShowDeleteSubmoduleWindowCommand _deleteSubmodule;

		private ShowAddSubmoduleWindowCommand _showAddSubmoduleWindow;

		private OpenWorktreeCommand _openWorktree;

		private ShowDeleteWorktreeWindowCommand _showDeleteWorktreeWindow;

		private ShowFileInFileTreeCommand _showFileInFileTree;

		private ShowFileHistoryWindowCommand _showFileHistoryWindow;

		private ShowBlameWindowCommand _showBlameWindow;

		private OpenFileInDefaultEditorCommand _openFileInDefaultEditor;

		private ShowFileInFileExplorerCommand _showFileInFileExplorer;

		private ResetFileToStateAtRevisionCommand _resetFileToStateAtRevision;

		private CopyAbsoluteFilePathsCommand _copyAbsoluteFilePaths;

		private CopyFilePathsCommand _copyFilePaths;

		private CopyWorktreePathsCommand _copyWorktreePaths;

		private RunExternalDiffToolCommand _runExternalDiffTool;

		private RunExternalMergeToolCommand _runExternalMergeTool;

		private ApplyPatchCommand _applyPatch;

		private SaveFileCommand _saveFile;

		private ShowAddGitignoreTemplateWindowCommand _showAddGitignoreTemplateWindow;

		public UpdateReferenceFilterCommand UpdateReferenceFilter => CommandContainer.Lazy(ref _updateReferenceFilter);

		public PinReferenceCommand AddReferenceStar => CommandContainer.Lazy(ref _addReferenceStar);

		public UnpinReferenceCommand RemoveReferenceStar => CommandContainer.Lazy(ref _removeReferenceStar);

		public ShowRepositorySettingsWindowCommand ShowRepositorySettingsWindow => CommandContainer.Lazy(ref _showRepositorySettingsWindow);

		public ShowRepositoryStatisticsWindowCommand ShowRepositoryStatisticsWindow => CommandContainer.Lazy(ref _showRepositoryStatisticsWindow);

		public ShowRepositoryOverviewWindowCommand ShowRepositoryOverviewWindow => CommandContainer.Lazy(ref _showRepositoryOverviewWindow);

		public ShowAiResultWindowCommand ShowAiResultWindow => CommandContainer.Lazy(ref _showAiResultWindow);

		public RemoveReferenceCommand RemoveReferenceCommand => CommandContainer.Lazy(ref _removeReferenceCommand);

		public CreatePullRequestCommand CreatePullRequest => CommandContainer.Lazy(ref _createPullRequest);

		public BisectCommand Bisect => CommandContainer.Lazy(ref _bisect);

		public ToggleShowReflogInRevisionListCommand ToggleShowReflogInRevisionList => CommandContainer.Lazy(ref _toggleShowReflogInRevisionList);

		public ShowCreateBranchWindowCommand ShowCreateBranchWindow => CommandContainer.Lazy(ref _showCreateBranchWindow);

		public ShowMergeBranchWindowCommand ShowMergeBranchWindow => CommandContainer.Lazy(ref _showMergeBranchWindow);

		public ShowRebaseBranchWindowCommand ShowRebaseBranchWindow => CommandContainer.Lazy(ref _showRebaseBranchWindow);

		public ShowInteractiveRebaseWindowCommand ShowInteractiveRebaseWindow => CommandContainer.Lazy(ref _showInteractiveRebaseWindow);

		public ShowRemoveLocalBranchWindowCommand ShowRemoveLocalBranchWindow => CommandContainer.Lazy(ref _showRemoveLocalBranchWindow);

		public ShowRemoveRemoteBranchWindowCommand ShowRemoveRemoteBranchWindow => CommandContainer.Lazy(ref _showRemoveRemoteBranchWindow);

		public ShowCheckoutBranchWindowCommand ShowCheckoutBranchWindow => CommandContainer.Lazy(ref _showCheckoutBranchWindow);

		public ShowRenameLocalBranchWindowCommand ShowRenameLocalBranchWindow => CommandContainer.Lazy(ref _showRenameLocalBranchWindow);

		public ShowResetBranchWindowCommand ShowResetBranchWindow => CommandContainer.Lazy(ref _showResetBranchWindow);

		public ShowPushBranchWindowCommand ShowPushBranchWindow => CommandContainer.Lazy(ref _showPushBranchWindow);

		public ShowPushMultipleBranchesWindowCommand ShowPushMultipleBranchesWindow => CommandContainer.Lazy(ref _showPushMultipleBranchesWindow);

		public ShowPushMultipleTagsWindowCommand ShowPushMultipleTagsWindow => CommandContainer.Lazy(ref _showPushMultipleTagsWindow);

		public ShowPullWindowCommand ShowPullWindow => CommandContainer.Lazy(ref _showPullWindow);

		public FastForwardCommand FastForward => CommandContainer.Lazy(ref _fastForward);

		public FastForwardPullCommand FastForwardPull => CommandContainer.Lazy(ref _fastForwardPull);

		public UpdateTrackingReferenceCommand UpdateTrackingReference => CommandContainer.Lazy(ref _updateTrackingReference);

		public ShowChangeTrackingReferenceWindowCommand ShowChangeTrackingReferenceWindow => CommandContainer.Lazy(ref _showChangeTrackingReferenceWindow);

		public ShowCreateTagWindowCommand ShowCreateTagWindow => CommandContainer.Lazy(ref _showCreateTagWindow);

		public ShowRemoveTagWindowCommand ShowRemoveTagWindow => CommandContainer.Lazy(ref _showRemoveTagWindow);

		public ShowPushTagWindowCommand ShowPushTagWindowCommand => CommandContainer.Lazy(ref _showPushTagWindowCommand);

		public ShowTagDetailsWindowCommand ShowTagDetailsWindow => CommandContainer.Lazy(ref _showTagDetailsWindow);

		public CopyReferenceNameCommand CopyReferenceName => CommandContainer.Lazy(ref _copyReferenceName);

		public ShowCheckoutRevisionWindowCommand ShowCheckoutRevisionWindow => CommandContainer.Lazy(ref _showCheckoutRevisionWindow);

		public ShowCherryPickWindowCommand ShowCherryPickWindow => CommandContainer.Lazy(ref _showCherryPickWindowCommand);

		public ShowRevertRevisionWindowCommand ShowRevertRevisionWindow => CommandContainer.Lazy(ref _showRevertRevisionWindow);

		public ShowRevisionInSeparateWindowCommand ShowRevisionInSeparateWindow => CommandContainer.Lazy(ref _showRevisionInSeparateWindow);

		public CopyRevisionShaCommand CopyRevisionSha => CommandContainer.Lazy(ref _copyRevisionSha);

		public CopyRevisionInfoCommand CopyRevisionInfo => CommandContainer.Lazy(ref _copyRevisionInfo);

		public RunCustomCommandCommand RunCustomCommand => CommandContainer.Lazy(ref _runRevisionCustomCommand);

		public ShowSaveRevisionsAsPatchWindowCommand ShowSaveRevisionsAsPatchWindow => CommandContainer.Lazy(ref _showSaveRevisionsAsPatchWindow);

		public CompareRevisionToWorkingDirectoryCommand CompareRevisionToWorkingDirectory => CommandContainer.Lazy(ref _compareRevisionToWorkingDirectory);

		public ShowAddRemoteWindowCommand ShowAddRemoteWindow => CommandContainer.Lazy(ref _showAddRemoteWindow);

		public CopyRemoteAddressCommand CopyRemoteAddress => CommandContainer.Lazy(ref _copyRemoteAddress);

		public ShowEditRemoteWindowCommand ShowEditRemoteWindow => CommandContainer.Lazy(ref _showEditRemoteWindow);

		public ShowRemoveRemoteWindowCommand ShowRemoveRemoteWindow => CommandContainer.Lazy(ref _showRemoveRemoteWindow);

		public DisableImplicitRemoteFetchCommand DisableImplicitRemoteFetch => CommandContainer.Lazy(ref _disableImplicitRemoteFetch);

		public ShowGitLfsFetchWindowCommand ShowGitLfsFetchWindow => CommandContainer.Lazy(ref _showGitLfsFetchWindow);

		public ShowGitLfsPullWindowCommand ShowGitLfsPullWindow => CommandContainer.Lazy(ref _showGitLfsPullWindow);

		public ShowGitLfsTrackWindowCommand ShowGitLfsTrackWindow => CommandContainer.Lazy(ref _showGitLfsTrackWindow);

		public ShowGitLfsStatusWindowCommand ShowGitLfsStatusWindow => CommandContainer.Lazy(ref _showGitLfsStatusWindow);

		public InitGitLfsCommand InitGitLfs => CommandContainer.Lazy(ref _initGitLfs);

		public DeinitializeGitLfsCommand DeinitializeGitLfs => CommandContainer.Lazy(ref _removeGitLfs);

		public GitLfsPruneCommand GitLfsPrune => CommandContainer.Lazy(ref _gitLfsPrune);

		public GitLfsLockCommand GitLfsLockCommand => CommandContainer.Lazy(ref _gitLfsLockCommand);

		public GitLfsUnlockCommand GitLfsUnlockCommand => CommandContainer.Lazy(ref _gitLfsUnlockCommand);

		public ShowGitFlowInitWindowCommand ShowGitFlowInitWindow => CommandContainer.Lazy(ref _showGitFlowInitWindow);

		public DeinitializeGitFlowCommand DeinitializeGitFlow => CommandContainer.Lazy(ref _deinitializeGitFlow);

		public ShowGitFlowStartFeatureWindowCommand ShowGitFlowStartFeatureWindow => CommandContainer.Lazy(ref _showGitFlowStartFeatureWindow);

		public ShowGitFlowStartReleaseWindowCommand ShowGitFlowStartReleaseWindow => CommandContainer.Lazy(ref _showGitFlowStartReleaseWindow);

		public ShowGitFlowStartHotfixWindowCommand ShowGitFlowStartHotfixWindow => CommandContainer.Lazy(ref _showGitFlowStartHotfixWindow);

		public ShowGitFlowFinishFeatureWindowCommand ShowGitFlowFinishFeatureWindow => CommandContainer.Lazy(ref _showGitFlowFinishFeatureWindow);

		public ShowGitFlowFinishReleaseWindowCommand ShowGitFlowFinishReleaseWindow => CommandContainer.Lazy(ref _showGitFlowFinishReleaseWindow);

		public ShowGitFlowFinishHotfixWindowCommand ShowGitFlowFinishHotfixWindow => CommandContainer.Lazy(ref _showGitFlowFinishHotfixWindow);

		public ShowLeanBranchingStartWindowCommand ShowLeanBranchingStartWindow => CommandContainer.Lazy(ref _showLeanBranchingStartWindow);

		public ShowLeanBranchingFinishWindowCommand ShowLeanBranchingFinishWindow => CommandContainer.Lazy(ref _showLeanBranchingFinishWindow);

		public LeanBranchingSyncCommand LeanBranchingSync => CommandContainer.Lazy(ref _leanBranchingSync);

		public ShowApplyStashWindowCommand ShowApplyStashWindow => CommandContainer.Lazy(ref _showApplyStashWindow);

		public ShowRenameStashWindowCommand ShowRenameStashWindow => CommandContainer.Lazy(ref _showRenameStashWindow);

		public ShowRemoveStashWindowCommand ShowRemoveStashWindow => CommandContainer.Lazy(ref _showRemoveStashWindow);

		public ShowSaveSnapshotWindowCommand ShowSaveSnapshotWindow => CommandContainer.Lazy(ref _showSaveSnapshotWindow);

		public OpenSubmoduleCommand OpenSubmodule => CommandContainer.Lazy(ref _openSubmodule);

		public UpdateSubmoduleCommand UpdateSubmodule => CommandContainer.Lazy(ref _updateSubmodule);

		public MoveSubmoduleCommand MoveSubmodule => CommandContainer.Lazy(ref _moveSubmodule);

		public ShowDeleteSubmoduleWindowCommand ShowDeleteSubmoduleWindow => CommandContainer.Lazy(ref _deleteSubmodule);

		public ShowAddSubmoduleWindowCommand ShowAddSubmoduleWindow => CommandContainer.Lazy(ref _showAddSubmoduleWindow);

		public OpenWorktreeCommand OpenWorktree => CommandContainer.Lazy(ref _openWorktree);

		public ShowDeleteWorktreeWindowCommand ShowDeleteWorktreeWindow => CommandContainer.Lazy(ref _showDeleteWorktreeWindow);

		public ShowFileInFileTreeCommand ShowFileInFileTree => CommandContainer.Lazy(ref _showFileInFileTree);

		public ShowFileHistoryWindowCommand ShowFileHistoryWindow => CommandContainer.Lazy(ref _showFileHistoryWindow);

		public ShowBlameWindowCommand ShowBlameWindow => CommandContainer.Lazy(ref _showBlameWindow);

		public OpenFileInDefaultEditorCommand OpenFileInDefaultEditor => CommandContainer.Lazy(ref _openFileInDefaultEditor);

		public ShowFileInFileExplorerCommand ShowFileInFileExplorer => CommandContainer.Lazy(ref _showFileInFileExplorer);

		public ResetFileToStateAtRevisionCommand ResetFileToStateAtRevision => CommandContainer.Lazy(ref _resetFileToStateAtRevision);

		public CopyAbsoluteFilePathsCommand CopyAbsoluteFilePaths => CommandContainer.Lazy(ref _copyAbsoluteFilePaths);

		public CopyFilePathsCommand CopyFilePaths => CommandContainer.Lazy(ref _copyFilePaths);

		public CopyWorktreePathsCommand CopyWorktreePaths => CommandContainer.Lazy(ref _copyWorktreePaths);

		public RunExternalDiffToolCommand RunExternalDiffTool => CommandContainer.Lazy(ref _runExternalDiffTool);

		public RunExternalMergeToolCommand RunExternalMergeTool => CommandContainer.Lazy(ref _runExternalMergeTool);

		public ApplyPatchCommand ApplyPatch => CommandContainer.Lazy(ref _applyPatch);

		public SaveFileCommand SaveFile => CommandContainer.Lazy(ref _saveFile);

		public ShowAddGitignoreTemplateWindowCommand ShowAddGitignoreTemplateWindow => CommandContainer.Lazy(ref _showAddGitignoreTemplateWindow);
	}
}

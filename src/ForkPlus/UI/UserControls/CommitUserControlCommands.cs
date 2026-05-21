using ForkPlus.UI.Commands;

namespace ForkPlus.UI.UserControls
{
	public class CommitUserControlCommands : CommandContainer
	{
		private CommitCommand _commitCommand;

		private ResolveConflictWithExistingVersionCommand _resolveConflictWithExistingVersionCommand;

		private ToggleFileStageCommand _toggleFileStageCommand;

		private ToggleAllFilesStageCommand _toggleAllFilesStageCommand;

		private DiscardChangedFilesCommand _discardChangedFilesCommand;

		private ShowResetFileToUnmergedStateWindowCommand _showResetFileToUnmergedStateWindowCommand;

		private ShowAddGitIgnorePatternWindowCommand _showAddGitIgnorePatternWindowCommand;

		private ShowSaveAsPatchDialogCommand _showSaveAsPatchDialogCommand;

		private ShowCreatePartialStashWindowCommand _showCreatePartialStashWindowCommand;

		public CommitCommand Commit => CommandContainer.Lazy(ref _commitCommand);

		public ResolveConflictWithExistingVersionCommand ResolveConflictWithExistingVersion => CommandContainer.Lazy(ref _resolveConflictWithExistingVersionCommand);

		public ToggleFileStageCommand ToggleFileStage => CommandContainer.Lazy(ref _toggleFileStageCommand);

		public ToggleAllFilesStageCommand ToggleAllFilesStageCommand => CommandContainer.Lazy(ref _toggleAllFilesStageCommand);

		public DiscardChangedFilesCommand DiscardChangedFilesCommand => CommandContainer.Lazy(ref _discardChangedFilesCommand);

		public ShowResetFileToUnmergedStateWindowCommand ShowResetFileToUnmergedStateWindow => CommandContainer.Lazy(ref _showResetFileToUnmergedStateWindowCommand);

		public ShowAddGitIgnorePatternWindowCommand ShowAddGitIgnorePatternWindowCommand => CommandContainer.Lazy(ref _showAddGitIgnorePatternWindowCommand);

		public ShowSaveAsPatchDialogCommand ShowSaveAsPatchDialogCommand => CommandContainer.Lazy(ref _showSaveAsPatchDialogCommand);

		public ShowCreatePartialStashWindowCommand ShowCreatePartialStashWindowCommand => CommandContainer.Lazy(ref _showCreatePartialStashWindowCommand);
	}
}

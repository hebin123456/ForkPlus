using System;

namespace ForkPlus.UI.Controls.Editor.Diff
{
	public interface ICommitTextDiffControl : ITextDiffControl, DiffControlContainer.IFileDiffControlSubControl
	{
		bool IsStaged { get; set; }

		bool IsNewOrUntracked { get; set; }

		event EventHandler<CommitCodeEditor> ToggleStage;

		event EventHandler<CommitCodeEditor> Stage;

		event EventHandler<CommitCodeEditor> Unstage;

		event EventHandler<CommitCodeEditor> Discard;
	}
}
